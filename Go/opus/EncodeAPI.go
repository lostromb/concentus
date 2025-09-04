package opus

import (
	"math"
)

func silk_InitEncoder(encState *SilkEncoder, encStatus *EncControlState) int {
	ret := SilkError.SILK_NO_ERROR
	encState.Reset()
	for n := 0; n < ENCODER_NUM_CHANNELS; n++ {
		ret += silk_init_encoder(encState.state_Fxx[n])
		OpusAssert(ret == SilkError.SILK_NO_ERROR)
	}
	encState.nChannelsAPI = 1
	encState.nChannelsInternal = 1
	ret += silk_QueryEncoder(encState, encStatus)
	OpusAssert(ret == SilkError.SILK_NO_ERROR)
	return ret
}

func silk_QueryEncoder(encState *SilkEncoder, encStatus *EncControlState) int {
	ret := SilkError.SILK_NO_ERROR
	state_Fxx := encState.state_Fxx[0]
	encStatus.Reset()
	encStatus.nChannelsAPI = encState.nChannelsAPI
	encStatus.nChannelsInternal = encState.nChannelsInternal
	encStatus.API_sampleRate = state_Fxx.API_fs_Hz
	encStatus.maxInternalSampleRate = state_Fxx.maxInternal_fs_Hz
	encStatus.minInternalSampleRate = state_Fxx.minInternal_fs_Hz
	encStatus.desiredInternalSampleRate = state_Fxx.desiredInternal_fs_Hz
	encStatus.payloadSize_ms = state_Fxx.PacketSize_ms
	encStatus.bitRate = state_Fxx.TargetRate_bps
	encStatus.packetLossPercentage = state_Fxx.PacketLoss_perc
	encStatus.complexity = state_Fxx.Complexity
	encStatus.useInBandFEC = state_Fxx.useInBandFEC
	encStatus.useDTX = state_Fxx.useDTX
	encStatus.useCBR = state_Fxx.useCBR
	encStatus.internalSampleRate = silk_SMULBB(state_Fxx.fs_kHz, 1000)
	encStatus.allowBandwidthSwitch = state_Fxx.allow_bandwidth_switch
	if state_Fxx.fs_kHz == 16 && state_Fxx.sLP.mode == 0 {
		encStatus.inWBmodeWithoutVariableLP = 1
	} else {
		encStatus.inWBmodeWithoutVariableLP = 0
	}
	return ret
}

func silk_Encode(
	psEnc *SilkEncoder,
	encControl *EncControlState,
	samplesIn []int16,
	nSamplesIn int,
	psRangeEnc *EntropyCoder,
	nBytesOut *BoxedValueInt,
	prefillFlag int) int {
	ret := SilkError.SILK_NO_ERROR
	var nBits, flags, tmp_payloadSize_ms, tmp_complexity int
	var nSamplesToBuffer, nSamplesToBufferMax, nBlocksOf10ms int
	var nSamplesFromInput, nSamplesFromInputMax int
	var speech_act_thr_for_switch_Q8 int
	var TargetRate_bps, channelRate_bps, LBRR_symbol, sum int
	MStargetRates_bps := [2]int{0, 0}
	var buf []int16
	var transition, curr_block, tot_blocks int
	nBytesOut.Val = 0

	if encControl.reducedDependency != 0 {
		psEnc.state_Fxx[0].first_frame_after_reset = 1
		psEnc.state_Fxx[1].first_frame_after_reset = 1
	}
	psEnc.state_Fxx[0].nFramesEncoded = 0
	psEnc.state_Fxx[1].nFramesEncoded = 0

	ret += encControl.check_control_input()
	if ret != SilkError.SILK_NO_ERROR {
		OpusAssert(false)
		return ret
	}

	encControl.switchReady = 0

	if encControl.nChannelsInternal > psEnc.nChannelsInternal {
		ret += silk_init_encoder(psEnc.state_Fxx[1])
		for i := range psEnc.sStereo.pred_prev_Q13 {
			psEnc.sStereo.pred_prev_Q13[i] = 0
		}
		for i := range psEnc.sStereo.sSide {
			psEnc.sStereo.sSide[i] = 0
		}
		psEnc.sStereo.mid_side_amp_Q0[0] = 0
		psEnc.sStereo.mid_side_amp_Q0[1] = 1
		psEnc.sStereo.mid_side_amp_Q0[2] = 0
		psEnc.sStereo.mid_side_amp_Q0[3] = 1
		psEnc.sStereo.width_prev_Q14 = 0
		psEnc.sStereo.smth_width_Q14 = int16(SILK_CONST(1.0, 14))
		if psEnc.nChannelsAPI == 2 {
			psEnc.state_Fxx[1].resampler_state = psEnc.state_Fxx[0].resampler_state
			copy(psEnc.state_Fxx[1].In_HP_State[:], psEnc.state_Fxx[0].In_HP_State[:])
		}
	}

	transition = 0
	if encControl.payloadSize_ms != psEnc.state_Fxx[0].PacketSize_ms || psEnc.nChannelsInternal != encControl.nChannelsInternal {
		transition = 1
	}

	psEnc.nChannelsAPI = encControl.nChannelsAPI
	psEnc.nChannelsInternal = encControl.nChannelsInternal

	nBlocksOf10ms = silk_DIV32(100*nSamplesIn, encControl.API_sampleRate)
	if nBlocksOf10ms > 1 {
		tot_blocks = nBlocksOf10ms >> 1
	} else {
		tot_blocks = 1
	}
	curr_block = 0
	if prefillFlag != 0 {
		if nBlocksOf10ms != 1 {
			OpusAssert(false)
			return SilkError.SILK_ENC_INPUT_INVALID_NO_OF_SAMPLES
		}
		tmp_payloadSize_ms = encControl.payloadSize_ms
		encControl.payloadSize_ms = 10
		tmp_complexity = encControl.complexity
		encControl.complexity = 0
		for n := 0; n < encControl.nChannelsInternal; n++ {
			psEnc.state_Fxx[n].controlled_since_last_payload = 0
			psEnc.state_Fxx[n].prefillFlag = 1
		}
	} else {
		if nBlocksOf10ms*encControl.API_sampleRate != 100*nSamplesIn || nSamplesIn < 0 {
			OpusAssert(false)
			return SilkError.SILK_ENC_INPUT_INVALID_NO_OF_SAMPLES
		}
		if 1000*nSamplesIn > encControl.payloadSize_ms*encControl.API_sampleRate {
			OpusAssert(false)
			return SilkError.SILK_ENC_INPUT_INVALID_NO_OF_SAMPLES
		}
	}

	TargetRate_bps = int(encControl.bitRate >> (encControl.nChannelsInternal - 1))

	for n := 0; n < encControl.nChannelsInternal; n++ {
		force_fs_kHz := 0
		if n == 1 {
			force_fs_kHz = psEnc.state_Fxx[0].fs_kHz
		}
		ret += psEnc.state_Fxx[n].silk_control_encoder(encControl, TargetRate_bps, psEnc.allowBandwidthSwitch, n, force_fs_kHz)
		if ret != SilkError.SILK_NO_ERROR {
			OpusAssert(false)
			return ret
		}

		if psEnc.state_Fxx[n].first_frame_after_reset != 0 || transition != 0 {
			for i := 0; i < psEnc.state_Fxx[0].nFramesPerPacket; i++ {
				psEnc.state_Fxx[n].LBRR_flags[i] = 0
			}
		}

		psEnc.state_Fxx[n].inDTX = psEnc.state_Fxx[n].useDTX
	}

	OpusAssert(encControl.nChannelsInternal == 1 || psEnc.state_Fxx[0].fs_kHz == psEnc.state_Fxx[1].fs_kHz)

	nSamplesToBufferMax = 10 * nBlocksOf10ms * psEnc.state_Fxx[0].fs_kHz
	nSamplesFromInputMax = silk_DIV32_16(nSamplesToBufferMax*psEnc.state_Fxx[0].API_fs_Hz, int(psEnc.state_Fxx[0].fs_kHz*1000))

	buf = make([]int16, nSamplesFromInputMax)

	samplesIn_ptr := 0
	for {
		nSamplesToBuffer = psEnc.state_Fxx[0].frame_length - psEnc.state_Fxx[0].inputBufIx
		if nSamplesToBuffer > nSamplesToBufferMax {
			nSamplesToBuffer = nSamplesToBufferMax
		}
		nSamplesFromInput = silk_DIV32_16(nSamplesToBuffer*psEnc.state_Fxx[0].API_fs_Hz, int(psEnc.state_Fxx[0].fs_kHz*1000))

		if encControl.nChannelsAPI == 2 && encControl.nChannelsInternal == 2 {
			id := psEnc.state_Fxx[0].nFramesEncoded
			for n := 0; n < nSamplesFromInput; n++ {
				buf[n] = samplesIn[samplesIn_ptr+2*n]
			}

			if psEnc.nPrevChannelsInternal == 1 && id == 0 {
				psEnc.state_Fxx[1].resampler_state = psEnc.state_Fxx[0].resampler_state
			}
			/*

				ret += silk_resampler(
					psEnc.state_Fxx[0].resampler_state,
					psEnc.state_Fxx[0].inputBuf[psEnc.state_Fxx[0].inputBufIx+2:],
					buf[:nSamplesFromInput],
					nSamplesFromInput)
			*/

			ret += silk_resampler(
				psEnc.state_Fxx[0].resampler_state,
				psEnc.state_Fxx[0].inputBuf,
				psEnc.state_Fxx[0].inputBufIx+2,
				buf,
				0,
				nSamplesFromInput)

			psEnc.state_Fxx[0].inputBufIx += nSamplesToBuffer

			nSamplesToBuffer = psEnc.state_Fxx[1].frame_length - psEnc.state_Fxx[1].inputBufIx
			if nSamplesToBuffer > 10*nBlocksOf10ms*psEnc.state_Fxx[1].fs_kHz {
				nSamplesToBuffer = 10 * nBlocksOf10ms * psEnc.state_Fxx[1].fs_kHz
			}
			for n := 0; n < nSamplesFromInput; n++ {
				buf[n] = samplesIn[samplesIn_ptr+2*n+1]
			}
			ret += silk_resampler(
				psEnc.state_Fxx[1].resampler_state,
				psEnc.state_Fxx[1].inputBuf,
				psEnc.state_Fxx[1].inputBufIx+2,
				buf,
				0,
				nSamplesFromInput)

			psEnc.state_Fxx[1].inputBufIx += nSamplesToBuffer
		} else if encControl.nChannelsAPI == 2 && encControl.nChannelsInternal == 1 {
			for n := 0; n < nSamplesFromInput; n++ {
				sum = int(samplesIn[samplesIn_ptr+2*n]) + int(samplesIn[samplesIn_ptr+2*n+1])
				buf[n] = int16(sum >> 1)
			}

			ret += silk_resampler(
				psEnc.state_Fxx[0].resampler_state,
				psEnc.state_Fxx[0].inputBuf,
				psEnc.state_Fxx[0].inputBufIx+2,
				buf,
				0,
				nSamplesFromInput)

			if psEnc.nPrevChannelsInternal == 2 && psEnc.state_Fxx[0].nFramesEncoded == 0 {
				ret += silk_resampler(
					psEnc.state_Fxx[1].resampler_state,
					psEnc.state_Fxx[1].inputBuf,
					psEnc.state_Fxx[1].inputBufIx+2,
					buf,
					0,
					nSamplesFromInput)

				for n := 0; n < psEnc.state_Fxx[0].frame_length; n++ {
					psEnc.state_Fxx[0].inputBuf[psEnc.state_Fxx[0].inputBufIx+n+2] = int16(
						(psEnc.state_Fxx[0].inputBuf[psEnc.state_Fxx[0].inputBufIx+n+2] +
							psEnc.state_Fxx[1].inputBuf[psEnc.state_Fxx[1].inputBufIx+n+2]) >> 1)
				}
			}

			psEnc.state_Fxx[0].inputBufIx += nSamplesToBuffer
		} else {
			OpusAssert(encControl.nChannelsAPI == 1 && encControl.nChannelsInternal == 1)
			copy(buf, samplesIn[samplesIn_ptr:samplesIn_ptr+nSamplesFromInput])
			ret += silk_resampler(
				psEnc.state_Fxx[0].resampler_state,
				psEnc.state_Fxx[0].inputBuf,
				psEnc.state_Fxx[0].inputBufIx+2,
				buf,
				0,
				nSamplesFromInput)
			psEnc.state_Fxx[0].inputBufIx += nSamplesToBuffer
		}

		samplesIn_ptr += nSamplesFromInput * encControl.nChannelsAPI
		nSamplesIn -= nSamplesFromInput

		psEnc.allowBandwidthSwitch = 0

		if psEnc.state_Fxx[0].inputBufIx >= psEnc.state_Fxx[0].frame_length {
			OpusAssert(psEnc.state_Fxx[0].inputBufIx == psEnc.state_Fxx[0].frame_length)
			OpusAssert(encControl.nChannelsInternal == 1 || psEnc.state_Fxx[1].inputBufIx == psEnc.state_Fxx[1].frame_length)

			if psEnc.state_Fxx[0].nFramesEncoded == 0 && prefillFlag == 0 {
				iCDF := make([]int16, 2)
				iCDF[0] = int16(256 - silk_RSHIFT(256, (psEnc.state_Fxx[0].nFramesPerPacket+1)*encControl.nChannelsInternal))

				//iCDF := []int16{0, int16(256 - (256 >> ((psEnc.state_Fxx[0].nFramesPerPacket + 1) * encControl.nChannelsInternal)))}
				psRangeEnc.enc_icdf(0, iCDF, 8)

				for n := 0; n < encControl.nChannelsInternal; n++ {
					LBRR_symbol = 0
					for i := 0; i < psEnc.state_Fxx[n].nFramesPerPacket; i++ {
						if psEnc.state_Fxx[n].LBRR_flags[i] != 0 {
							LBRR_symbol |= 1 << i
						}
					}

					if LBRR_symbol > 0 {
						psEnc.state_Fxx[n].LBRR_flag = 1
					} else {
						psEnc.state_Fxx[n].LBRR_flag = 0
					}
					if LBRR_symbol != 0 && psEnc.state_Fxx[n].nFramesPerPacket > 1 {
						psRangeEnc.enc_icdf(LBRR_symbol-1, silk_LBRR_flags_iCDF_ptr[psEnc.state_Fxx[n].nFramesPerPacket-2], 8)
					}
				}

				for i := 0; i < psEnc.state_Fxx[0].nFramesPerPacket; i++ {
					for n := 0; n < encControl.nChannelsInternal; n++ {
						if psEnc.state_Fxx[n].LBRR_flags[i] != 0 {
							if encControl.nChannelsInternal == 2 && n == 0 {
								silk_stereo_encode_pred(psRangeEnc, psEnc.sStereo.predIx[i])
								if psEnc.state_Fxx[1].LBRR_flags[i] == 0 {
									silk_stereo_encode_mid_only(psRangeEnc, psEnc.sStereo.mid_only_flags[i])
								}
							}

							condCoding := CODE_INDEPENDENTLY
							if i > 0 && psEnc.state_Fxx[n].LBRR_flags[i-1] != 0 {
								condCoding = CODE_CONDITIONALLY
							}

							silk_encode_indices(psEnc.state_Fxx[n], psRangeEnc, i, 1, condCoding)
							silk_encode_pulses(psRangeEnc, int(psEnc.state_Fxx[n].indices_LBRR[i].signalType), int(psEnc.state_Fxx[n].indices_LBRR[i].quantOffsetType),
								psEnc.state_Fxx[n].pulses_LBRR[i], psEnc.state_Fxx[n].frame_length)
						}
					}
				}

				for n := 0; n < encControl.nChannelsInternal; n++ {
					for i := range psEnc.state_Fxx[n].LBRR_flags {
						psEnc.state_Fxx[n].LBRR_flags[i] = 0
					}
				}

				psEnc.nBitsUsedLBRR = psRangeEnc.tell()
			}

			silk_HP_variable_cutoff(psEnc.state_Fxx)

			nBits = silk_DIV32_16(int(encControl.bitRate*encControl.payloadSize_ms), 1000)
			if prefillFlag == 0 {
				nBits -= psEnc.nBitsUsedLBRR
			}
			nBits = silk_DIV32_16(int(nBits), int(psEnc.state_Fxx[0].nFramesPerPacket))
			if encControl.payloadSize_ms == 10 {
				TargetRate_bps = nBits * 100
			} else {
				TargetRate_bps = nBits * 50
			}
			TargetRate_bps -= silk_DIV32_16(int(psEnc.nBitsExceeded*1000), TuningParameters.BITRESERVOIR_DECAY_TIME_MS)
			if prefillFlag == 0 && psEnc.state_Fxx[0].nFramesEncoded > 0 {
				bitsBalance := psRangeEnc.tell() - psEnc.nBitsUsedLBRR - nBits*psEnc.state_Fxx[0].nFramesEncoded
				TargetRate_bps -= silk_DIV32_16(int(bitsBalance*1000), TuningParameters.BITRESERVOIR_DECAY_TIME_MS)
			}
			if TargetRate_bps > encControl.bitRate {
				TargetRate_bps = encControl.bitRate
			} else if TargetRate_bps < 5000 {
				TargetRate_bps = 5000
			}

			if encControl.nChannelsInternal == 2 {

				midOnlyFlag := psEnc.sStereo.mid_only_flags[psEnc.state_Fxx[0].nFramesEncoded]

				silk_stereo_LR_to_MS(
					psEnc.sStereo,
					psEnc.state_Fxx[0].inputBuf,
					2,
					psEnc.state_Fxx[1].inputBuf,
					2,
					psEnc.sStereo.predIx[psEnc.state_Fxx[0].nFramesEncoded],
					&midOnlyFlag,
					MStargetRates_bps[:],
					TargetRate_bps,
					psEnc.state_Fxx[0].speech_activity_Q8,
					encControl.toMono,
					psEnc.state_Fxx[0].fs_kHz,
					psEnc.state_Fxx[0].frame_length)

				psEnc.sStereo.mid_only_flags[psEnc.state_Fxx[0].nFramesEncoded] = midOnlyFlag

				if midOnlyFlag == 0 {
					if psEnc.prev_decode_only_middle == 1 {
						psEnc.state_Fxx[1].sShape.Reset()
						psEnc.state_Fxx[1].sPrefilt.Reset()
						psEnc.state_Fxx[1].sNSQ.Reset()
						for i := range psEnc.state_Fxx[1].prev_NLSFq_Q15 {
							psEnc.state_Fxx[1].prev_NLSFq_Q15[i] = 0
						}
						for i := range psEnc.state_Fxx[1].sLP.In_LP_State {
							psEnc.state_Fxx[1].sLP.In_LP_State[i] = 0
						}
						psEnc.state_Fxx[1].prevLag = 100
						psEnc.state_Fxx[1].sNSQ.lagPrev = 100
						psEnc.state_Fxx[1].sShape.LastGainIndex = 10
						psEnc.state_Fxx[1].prevSignalType = TYPE_NO_VOICE_ACTIVITY
						psEnc.state_Fxx[1].sNSQ.prev_gain_Q16 = 65536
						psEnc.state_Fxx[1].first_frame_after_reset = 1
					}
					psEnc.state_Fxx[1].silk_encode_do_VAD()
				} else {
					psEnc.state_Fxx[1].VAD_flags[psEnc.state_Fxx[0].nFramesEncoded] = 0
				}

				if prefillFlag == 0 {
					silk_stereo_encode_pred(psRangeEnc, psEnc.sStereo.predIx[psEnc.state_Fxx[0].nFramesEncoded])
					if psEnc.state_Fxx[1].VAD_flags[psEnc.state_Fxx[0].nFramesEncoded] == 0 {
						silk_stereo_encode_mid_only(psRangeEnc, midOnlyFlag)
					}
				}
			} else {
				copy(psEnc.sStereo.sMid[:], psEnc.state_Fxx[0].inputBuf[:2])
				copy(psEnc.state_Fxx[0].inputBuf[psEnc.state_Fxx[0].frame_length:psEnc.state_Fxx[0].frame_length+2], psEnc.sStereo.sMid[:])
			}

			psEnc.state_Fxx[0].silk_encode_do_VAD()

			for n := 0; n < encControl.nChannelsInternal; n++ {
				maxBits := encControl.maxBits
				if tot_blocks == 2 && curr_block == 0 {
					maxBits = maxBits * 3 / 5
				} else if tot_blocks == 3 {
					if curr_block == 0 {
						maxBits = maxBits * 2 / 5
					} else if curr_block == 1 {
						maxBits = maxBits * 3 / 4
					}
				}

				useCBR := 0
				if encControl.useCBR != 0 && curr_block == tot_blocks-1 {
					useCBR = 1
				}

				if encControl.nChannelsInternal == 1 {
					channelRate_bps = TargetRate_bps
				} else {
					channelRate_bps = MStargetRates_bps[n]

					if n == 0 && MStargetRates_bps[1] > 0 {
						useCBR = 0
						maxBits -= encControl.maxBits / (tot_blocks * 2)
					}
				}

				if channelRate_bps > 0 {
					psEnc.state_Fxx[n].silk_control_SNR(channelRate_bps)

					condCoding := CODE_INDEPENDENTLY
					if psEnc.state_Fxx[0].nFramesEncoded-n > 0 {
						if n > 0 && psEnc.prev_decode_only_middle != 0 {
							condCoding = CODE_INDEPENDENTLY_NO_LTP_SCALING
						} else {
							condCoding = CODE_CONDITIONALLY
						}
					}

					ret += psEnc.state_Fxx[n].silk_encode_frame(nBytesOut, psRangeEnc, condCoding, maxBits, useCBR)
					OpusAssert(ret == SilkError.SILK_NO_ERROR)
				}

				psEnc.state_Fxx[n].controlled_since_last_payload = 0
				psEnc.state_Fxx[n].inputBufIx = 0
				psEnc.state_Fxx[n].nFramesEncoded++
			}

			psEnc.prev_decode_only_middle = int(psEnc.sStereo.mid_only_flags[psEnc.state_Fxx[0].nFramesEncoded-1])

			if nBytesOut.Val > 0 && psEnc.state_Fxx[0].nFramesEncoded == psEnc.state_Fxx[0].nFramesPerPacket {
				flags = 0
				for n := 0; n < encControl.nChannelsInternal; n++ {
					for i := 0; i < psEnc.state_Fxx[n].nFramesPerPacket; i++ {
						flags <<= 1
						if psEnc.state_Fxx[n].VAD_flags[i] != 0 {
							flags |= 1
						}
					}
					flags <<= 1
					if psEnc.state_Fxx[n].LBRR_flag != 0 {
						flags |= 1
					}
				}

				if prefillFlag == 0 {
					psRangeEnc.enc_patch_initial_bits(int64(flags), (psEnc.state_Fxx[0].nFramesPerPacket+1)*encControl.nChannelsInternal)
				}

				if psEnc.state_Fxx[0].inDTX != 0 && (encControl.nChannelsInternal == 1 || psEnc.state_Fxx[1].inDTX != 0) {
					nBytesOut.Val = 0
				}

				psEnc.nBitsExceeded += nBytesOut.Val * 8
				psEnc.nBitsExceeded -= (encControl.bitRate * encControl.payloadSize_ms) / 1000
				if psEnc.nBitsExceeded < 0 {
					psEnc.nBitsExceeded = 0
				} else if psEnc.nBitsExceeded > 10000 {
					psEnc.nBitsExceeded = 10000
				}

				speech_act_thr_for_switch_Q8 = silk_SMLAWB(
					int(math.Trunc(float64(TuningParameters.SPEECH_ACTIVITY_DTX_THRES)*256.0+0.5)),
					int(math.Trunc(
						((1.0-float64(TuningParameters.SPEECH_ACTIVITY_DTX_THRES))/
							float64(TuningParameters.MAX_BANDWIDTH_SWITCH_DELAY_MS))*
							16777216.0+0.5,
					)),
					psEnc.timeSinceSwitchAllowed_ms,
				)
				if psEnc.state_Fxx[0].speech_activity_Q8 < speech_act_thr_for_switch_Q8 {
					psEnc.allowBandwidthSwitch = 1
					psEnc.timeSinceSwitchAllowed_ms = 0
				} else {
					psEnc.allowBandwidthSwitch = 0
					psEnc.timeSinceSwitchAllowed_ms += encControl.payloadSize_ms
				}
			}

			if nSamplesIn == 0 {
				break
			}
		} else {
			break
		}

		curr_block++
	}

	psEnc.nPrevChannelsInternal = encControl.nChannelsInternal

	encControl.allowBandwidthSwitch = psEnc.allowBandwidthSwitch
	if psEnc.state_Fxx[0].fs_kHz == 16 && psEnc.state_Fxx[0].sLP.mode == 0 {
		encControl.inWBmodeWithoutVariableLP = 1
	} else {
		encControl.inWBmodeWithoutVariableLP = 0
	}
	encControl.internalSampleRate = silk_SMULBB(psEnc.state_Fxx[0].fs_kHz, 1000)
	if encControl.toMono != 0 {
		encControl.stereoWidth_Q14 = 0
	} else {
		encControl.stereoWidth_Q14 = int(psEnc.sStereo.smth_width_Q14)
	}

	if prefillFlag != 0 {
		encControl.payloadSize_ms = tmp_payloadSize_ms
		encControl.complexity = tmp_complexity
		for n := 0; n < encControl.nChannelsInternal; n++ {
			psEnc.state_Fxx[n].controlled_since_last_payload = 0
			psEnc.state_Fxx[n].prefillFlag = 0
		}
	}

	return ret
}
