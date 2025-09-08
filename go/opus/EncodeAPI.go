package opus

import (
	"math"

	"github.com/lostromb/concentus/go/comm"
	"github.com/lostromb/concentus/go/silk"
)

func silk_InitEncoder(encState *silk.SilkEncoder, encStatus *silk.EncControlState) int {
	ret := SilkError.SILK_NO_ERROR
	encState.Reset()
	for n := 0; n < SilkConstants.ENCODER_NUM_CHANNELS; n++ {
		ret += silk.Silk_init_encoder(encState.State_Fxx[n])
		inlines.OpusAssert(ret == SilkError.SILK_NO_ERROR)
	}
	encState.NChannelsAPI = 1
	encState.NChannelsInternal = 1
	ret += silk_QueryEncoder(encState, encStatus)
	inlines.OpusAssert(ret == SilkError.SILK_NO_ERROR)
	return ret
}

func silk_QueryEncoder(encState *silk.SilkEncoder, encStatus *silk.EncControlState) int {
	ret := SilkError.SILK_NO_ERROR
	State_Fxx := encState.State_Fxx[0]
	encStatus.Reset()
	encStatus.NChannelsAPI = encState.NChannelsAPI
	encStatus.NChannelsInternal = encState.NChannelsInternal
	encStatus.API_sampleRate = State_Fxx.API_fs_Hz
	encStatus.MaxInternalSampleRate = State_Fxx.MaxInternal_fs_Hz
	encStatus.MinInternalSampleRate = State_Fxx.MinInternal_fs_Hz
	encStatus.DesiredInternalSampleRate = State_Fxx.DesiredInternal_fs_Hz
	encStatus.PayloadSize_ms = State_Fxx.PacketSize_ms
	encStatus.BitRate = State_Fxx.TargetRate_bps
	encStatus.PacketLossPercentage = State_Fxx.PacketLoss_perc
	encStatus.Complexity = State_Fxx.Complexity
	encStatus.UseInBandFEC = State_Fxx.UseInBandFEC
	encStatus.UseDTX = State_Fxx.UseDTX
	encStatus.UseCBR = State_Fxx.UseCBR
	encStatus.InternalSampleRate = inlines.Silk_SMULBB(State_Fxx.Fs_kHz, 1000)
	encStatus.AllowBandwidthSwitch = State_Fxx.Allow_bandwidth_switch
	if State_Fxx.Fs_kHz == 16 && State_Fxx.SLP.Mode == 0 {
		encStatus.InWBmodeWithoutVariableLP = 1
	} else {
		encStatus.InWBmodeWithoutVariableLP = 0
	}
	return ret
}

func silk_Encode(
	psEnc *silk.SilkEncoder,
	encControl *silk.EncControlState,
	samplesIn []int16,
	nSamplesIn int,
	psRangeEnc *comm.EntropyCoder,
	nBytesOut *comm.BoxedValueInt,
	PrefillFlag int) int {
	ret := SilkError.SILK_NO_ERROR
	var nBits, flags, tmp_PayloadSize_ms, tmp_Complexity int
	var nSamplesToBuffer, nSamplesToBufferMax, nBlocksOf10ms int
	var nSamplesFromInput, nSamplesFromInputMax int
	var speech_act_thr_for_switch_Q8 int
	var TargetRate_bps, channelRate_bps, LBRR_symbol, sum int
	MStargetRates_bps := [2]int{0, 0}
	var buf []int16
	var transition, curr_block, tot_blocks int
	nBytesOut.Val = 0

	if encControl.ReducedDependency != 0 {
		psEnc.State_Fxx[0].First_frame_after_reset = 1
		psEnc.State_Fxx[1].First_frame_after_reset = 1
	}
	psEnc.State_Fxx[0].NFramesEncoded = 0
	psEnc.State_Fxx[1].NFramesEncoded = 0

	ret += encControl.Check_control_input()
	if ret != SilkError.SILK_NO_ERROR {
		inlines.OpusAssert(false)
		return ret
	}

	encControl.SwitchReady = 0

	if encControl.NChannelsInternal > psEnc.NChannelsInternal {
		ret += silk.Silk_init_encoder(psEnc.State_Fxx[1])
		for i := range psEnc.SStereo.Pred_prev_Q13 {
			psEnc.SStereo.Pred_prev_Q13[i] = 0
		}
		for i := range psEnc.SStereo.SSide {
			psEnc.SStereo.SSide[i] = 0
		}
		psEnc.SStereo.Mid_side_amp_Q0[0] = 0
		psEnc.SStereo.Mid_side_amp_Q0[1] = 1
		psEnc.SStereo.Mid_side_amp_Q0[2] = 0
		psEnc.SStereo.Mid_side_amp_Q0[3] = 1
		psEnc.SStereo.Width_prev_Q14 = 0
		psEnc.SStereo.Smth_width_Q14 = int16(inlines.SILK_CONST(1.0, 14))
		if psEnc.NChannelsAPI == 2 {
			psEnc.State_Fxx[1].Resampler_state = psEnc.State_Fxx[0].Resampler_state
			copy(psEnc.State_Fxx[1].In_HP_State[:], psEnc.State_Fxx[0].In_HP_State[:])
		}
	}

	transition = 0
	if encControl.PayloadSize_ms != psEnc.State_Fxx[0].PacketSize_ms || psEnc.NChannelsInternal != encControl.NChannelsInternal {
		transition = 1
	}

	psEnc.NChannelsAPI = encControl.NChannelsAPI
	psEnc.NChannelsInternal = encControl.NChannelsInternal

	nBlocksOf10ms = inlines.Silk_DIV32(100*nSamplesIn, encControl.API_sampleRate)
	if nBlocksOf10ms > 1 {
		tot_blocks = nBlocksOf10ms >> 1
	} else {
		tot_blocks = 1
	}
	curr_block = 0
	if PrefillFlag != 0 {
		if nBlocksOf10ms != 1 {
			inlines.OpusAssert(false)
			return SilkError.SILK_ENC_INPUT_INVALID_NO_OF_SAMPLES
		}
		tmp_PayloadSize_ms = encControl.PayloadSize_ms
		encControl.PayloadSize_ms = 10
		tmp_Complexity = encControl.Complexity
		encControl.Complexity = 0
		for n := 0; n < encControl.NChannelsInternal; n++ {
			psEnc.State_Fxx[n].Controlled_since_last_payload = 0
			psEnc.State_Fxx[n].PrefillFlag = 1
		}
	} else {
		if nBlocksOf10ms*encControl.API_sampleRate != 100*nSamplesIn || nSamplesIn < 0 {
			inlines.OpusAssert(false)
			return SilkError.SILK_ENC_INPUT_INVALID_NO_OF_SAMPLES
		}
		if 1000*nSamplesIn > encControl.PayloadSize_ms*encControl.API_sampleRate {
			inlines.OpusAssert(false)
			return SilkError.SILK_ENC_INPUT_INVALID_NO_OF_SAMPLES
		}
	}

	TargetRate_bps = int(encControl.BitRate >> (encControl.NChannelsInternal - 1))

	for n := 0; n < encControl.NChannelsInternal; n++ {
		force_Fs_kHz := 0
		if n == 1 {
			force_Fs_kHz = psEnc.State_Fxx[0].Fs_kHz
		}
		ret += psEnc.State_Fxx[n].Silk_control_encoder(encControl, TargetRate_bps, psEnc.AllowBandwidthSwitch, n, force_Fs_kHz)
		if ret != SilkError.SILK_NO_ERROR {
			inlines.OpusAssert(false)
			return ret
		}

		if psEnc.State_Fxx[n].First_frame_after_reset != 0 || transition != 0 {
			for i := 0; i < psEnc.State_Fxx[0].NFramesPerPacket; i++ {
				psEnc.State_Fxx[n].LBRR_flags[i] = 0
			}
		}

		psEnc.State_Fxx[n].InDTX = psEnc.State_Fxx[n].UseDTX
	}

	inlines.OpusAssert(encControl.NChannelsInternal == 1 || psEnc.State_Fxx[0].Fs_kHz == psEnc.State_Fxx[1].Fs_kHz)

	nSamplesToBufferMax = 10 * nBlocksOf10ms * psEnc.State_Fxx[0].Fs_kHz
	nSamplesFromInputMax = inlines.Silk_DIV32_16(nSamplesToBufferMax*psEnc.State_Fxx[0].API_fs_Hz, int(psEnc.State_Fxx[0].Fs_kHz*1000))

	buf = make([]int16, nSamplesFromInputMax)

	samplesIn_ptr := 0
	for {
		nSamplesToBuffer = psEnc.State_Fxx[0].Frame_length - psEnc.State_Fxx[0].InputBufIx
		if nSamplesToBuffer > nSamplesToBufferMax {
			nSamplesToBuffer = nSamplesToBufferMax
		}
		nSamplesFromInput = inlines.Silk_DIV32_16(nSamplesToBuffer*psEnc.State_Fxx[0].API_fs_Hz, int(psEnc.State_Fxx[0].Fs_kHz*1000))

		if encControl.NChannelsAPI == 2 && encControl.NChannelsInternal == 2 {
			id := psEnc.State_Fxx[0].NFramesEncoded
			for n := 0; n < nSamplesFromInput; n++ {
				buf[n] = samplesIn[samplesIn_ptr+2*n]
			}

			if psEnc.NPrevChannelsInternal == 1 && id == 0 {
				psEnc.State_Fxx[1].Resampler_state = psEnc.State_Fxx[0].Resampler_state
			}
			/*

				ret += silk_resampler(
					psEnc.State_Fxx[0].resampler_state,
					psEnc.State_Fxx[0].InputBuf[psEnc.State_Fxx[0].InputBufIx+2:],
					buf[:nSamplesFromInput],
					nSamplesFromInput)
			*/

			ret += silk.Silk_resampler(
				psEnc.State_Fxx[0].Resampler_state,
				psEnc.State_Fxx[0].InputBuf,
				psEnc.State_Fxx[0].InputBufIx+2,
				buf,
				0,
				nSamplesFromInput)

			psEnc.State_Fxx[0].InputBufIx += nSamplesToBuffer

			nSamplesToBuffer = psEnc.State_Fxx[1].Frame_length - psEnc.State_Fxx[1].InputBufIx
			if nSamplesToBuffer > 10*nBlocksOf10ms*psEnc.State_Fxx[1].Fs_kHz {
				nSamplesToBuffer = 10 * nBlocksOf10ms * psEnc.State_Fxx[1].Fs_kHz
			}
			for n := 0; n < nSamplesFromInput; n++ {
				buf[n] = samplesIn[samplesIn_ptr+2*n+1]
			}
			ret += silk.Silk_resampler(
				psEnc.State_Fxx[1].Resampler_state,
				psEnc.State_Fxx[1].InputBuf,
				psEnc.State_Fxx[1].InputBufIx+2,
				buf,
				0,
				nSamplesFromInput)

			psEnc.State_Fxx[1].InputBufIx += nSamplesToBuffer
		} else if encControl.NChannelsAPI == 2 && encControl.NChannelsInternal == 1 {
			for n := 0; n < nSamplesFromInput; n++ {
				sum = int(samplesIn[samplesIn_ptr+2*n]) + int(samplesIn[samplesIn_ptr+2*n+1])
				buf[n] = int16(sum >> 1)
			}

			ret += silk.Silk_resampler(
				psEnc.State_Fxx[0].Resampler_state,
				psEnc.State_Fxx[0].InputBuf,
				psEnc.State_Fxx[0].InputBufIx+2,
				buf,
				0,
				nSamplesFromInput)

			if psEnc.NPrevChannelsInternal == 2 && psEnc.State_Fxx[0].NFramesEncoded == 0 {
				ret += silk.Silk_resampler(
					psEnc.State_Fxx[1].Resampler_state,
					psEnc.State_Fxx[1].InputBuf,
					psEnc.State_Fxx[1].InputBufIx+2,
					buf,
					0,
					nSamplesFromInput)

				for n := 0; n < psEnc.State_Fxx[0].Frame_length; n++ {
					psEnc.State_Fxx[0].InputBuf[psEnc.State_Fxx[0].InputBufIx+n+2] = int16(
						(psEnc.State_Fxx[0].InputBuf[psEnc.State_Fxx[0].InputBufIx+n+2] +
							psEnc.State_Fxx[1].InputBuf[psEnc.State_Fxx[1].InputBufIx+n+2]) >> 1)
				}
			}

			psEnc.State_Fxx[0].InputBufIx += nSamplesToBuffer
		} else {
			inlines.OpusAssert(encControl.NChannelsAPI == 1 && encControl.NChannelsInternal == 1)
			copy(buf, samplesIn[samplesIn_ptr:samplesIn_ptr+nSamplesFromInput])
			ret += silk.Silk_resampler(
				psEnc.State_Fxx[0].Resampler_state,
				psEnc.State_Fxx[0].InputBuf,
				psEnc.State_Fxx[0].InputBufIx+2,
				buf,
				0,
				nSamplesFromInput)
			psEnc.State_Fxx[0].InputBufIx += nSamplesToBuffer
		}

		samplesIn_ptr += nSamplesFromInput * encControl.NChannelsAPI
		nSamplesIn -= nSamplesFromInput

		psEnc.AllowBandwidthSwitch = 0

		if psEnc.State_Fxx[0].InputBufIx >= psEnc.State_Fxx[0].Frame_length {
			inlines.OpusAssert(psEnc.State_Fxx[0].InputBufIx == psEnc.State_Fxx[0].Frame_length)
			inlines.OpusAssert(encControl.NChannelsInternal == 1 || psEnc.State_Fxx[1].InputBufIx == psEnc.State_Fxx[1].Frame_length)

			if psEnc.State_Fxx[0].NFramesEncoded == 0 && PrefillFlag == 0 {
				iCDF := make([]int16, 2)
				iCDF[0] = int16(256 - inlines.Silk_RSHIFT(256, (psEnc.State_Fxx[0].NFramesPerPacket+1)*encControl.NChannelsInternal))

				//iCDF := []int16{0, int16(256 - (256 >> ((psEnc.State_Fxx[0].NFramesPerPacket + 1) * encControl.NChannelsInternal)))}
				psRangeEnc.Enc_icdf(0, iCDF, 8)

				for n := 0; n < encControl.NChannelsInternal; n++ {
					LBRR_symbol = 0
					for i := 0; i < psEnc.State_Fxx[n].NFramesPerPacket; i++ {
						if psEnc.State_Fxx[n].LBRR_flags[i] != 0 {
							LBRR_symbol |= 1 << i
						}
					}

					if LBRR_symbol > 0 {
						psEnc.State_Fxx[n].LBRR_flag = 1
					} else {
						psEnc.State_Fxx[n].LBRR_flag = 0
					}
					if LBRR_symbol != 0 && psEnc.State_Fxx[n].NFramesPerPacket > 1 {
						psRangeEnc.Enc_icdf(LBRR_symbol-1, silk.Silk_LBRR_flags_iCDF_ptr[psEnc.State_Fxx[n].NFramesPerPacket-2], 8)
					}
				}

				for i := 0; i < psEnc.State_Fxx[0].NFramesPerPacket; i++ {
					for n := 0; n < encControl.NChannelsInternal; n++ {
						if psEnc.State_Fxx[n].LBRR_flags[i] != 0 {
							if encControl.NChannelsInternal == 2 && n == 0 {
								silk.Silk_stereo_encode_pred(psRangeEnc, psEnc.SStereo.PredIx[i])
								if psEnc.State_Fxx[1].LBRR_flags[i] == 0 {
									silk.Silk_stereo_encode_mid_only(psRangeEnc, psEnc.SStereo.Mid_only_flags[i])
								}
							}

							condCoding := SilkConstants.CODE_INDEPENDENTLY
							if i > 0 && psEnc.State_Fxx[n].LBRR_flags[i-1] != 0 {
								condCoding = SilkConstants.CODE_CONDITIONALLY
							}

							silk.Silk_encode_indices(psEnc.State_Fxx[n], psRangeEnc, i, 1, condCoding)
							silk.Silk_encode_pulses(psRangeEnc, int(psEnc.State_Fxx[n].Indices_LBRR[i].SignalType), int(psEnc.State_Fxx[n].Indices_LBRR[i].QuantOffsetType),
								psEnc.State_Fxx[n].Pulses_LBRR[i], psEnc.State_Fxx[n].Frame_length)
						}
					}
				}

				for n := 0; n < encControl.NChannelsInternal; n++ {
					for i := range psEnc.State_Fxx[n].LBRR_flags {
						psEnc.State_Fxx[n].LBRR_flags[i] = 0
					}
				}

				psEnc.NBitsUsedLBRR = psRangeEnc.Tell()
			}

			silk_HP_variable_cutoff(psEnc.State_Fxx)

			nBits = inlines.Silk_DIV32_16(int(encControl.BitRate*encControl.PayloadSize_ms), 1000)
			if PrefillFlag == 0 {
				nBits -= psEnc.NBitsUsedLBRR
			}
			nBits = inlines.Silk_DIV32_16(int(nBits), int(psEnc.State_Fxx[0].NFramesPerPacket))
			if encControl.PayloadSize_ms == 10 {
				TargetRate_bps = nBits * 100
			} else {
				TargetRate_bps = nBits * 50
			}
			TargetRate_bps -= inlines.Silk_DIV32_16(int(psEnc.NBitsExceeded*1000), TuningParameters.BITRESERVOIR_DECAY_TIME_MS)
			if PrefillFlag == 0 && psEnc.State_Fxx[0].NFramesEncoded > 0 {
				bitsBalance := psRangeEnc.Tell() - psEnc.NBitsUsedLBRR - nBits*psEnc.State_Fxx[0].NFramesEncoded
				TargetRate_bps -= inlines.Silk_DIV32_16(int(bitsBalance*1000), TuningParameters.BITRESERVOIR_DECAY_TIME_MS)
			}
			if TargetRate_bps > encControl.BitRate {
				TargetRate_bps = encControl.BitRate
			} else if TargetRate_bps < 5000 {
				TargetRate_bps = 5000
			}

			if encControl.NChannelsInternal == 2 {

				midOnlyFlag := psEnc.SStereo.Mid_only_flags[psEnc.State_Fxx[0].NFramesEncoded]

				silk.Silk_stereo_LR_to_MS(
					psEnc.SStereo,
					psEnc.State_Fxx[0].InputBuf,
					2,
					psEnc.State_Fxx[1].InputBuf,
					2,
					psEnc.SStereo.PredIx[psEnc.State_Fxx[0].NFramesEncoded],
					&midOnlyFlag,
					MStargetRates_bps[:],
					TargetRate_bps,
					psEnc.State_Fxx[0].Speech_activity_Q8,
					encControl.ToMono,
					psEnc.State_Fxx[0].Fs_kHz,
					psEnc.State_Fxx[0].Frame_length)

				psEnc.SStereo.Mid_only_flags[psEnc.State_Fxx[0].NFramesEncoded] = midOnlyFlag

				if midOnlyFlag == 0 {
					if psEnc.Prev_decode_only_middle == 1 {
						psEnc.State_Fxx[1].SShape.Reset()
						psEnc.State_Fxx[1].SPrefilt.Reset()
						psEnc.State_Fxx[1].SNSQ.Reset()
						for i := range psEnc.State_Fxx[1].Prev_NLSFq_Q15 {
							psEnc.State_Fxx[1].Prev_NLSFq_Q15[i] = 0
						}
						for i := range psEnc.State_Fxx[1].SLP.In_LP_State {
							psEnc.State_Fxx[1].SLP.In_LP_State[i] = 0
						}
						psEnc.State_Fxx[1].PrevLag = 100
						psEnc.State_Fxx[1].SNSQ.LagPrev = 100
						psEnc.State_Fxx[1].SShape.LastGainIndex = 10
						psEnc.State_Fxx[1].PrevSignalType = byte(SilkConstants.TYPE_NO_VOICE_ACTIVITY)
						psEnc.State_Fxx[1].SNSQ.Prev_gain_Q16 = 65536
						psEnc.State_Fxx[1].First_frame_after_reset = 1
					}
					psEnc.State_Fxx[1].Silk_encode_do_VAD()
				} else {
					psEnc.State_Fxx[1].VAD_flags[psEnc.State_Fxx[0].NFramesEncoded] = 0
				}

				if PrefillFlag == 0 {
					silk.Silk_stereo_encode_pred(psRangeEnc, psEnc.SStereo.PredIx[psEnc.State_Fxx[0].NFramesEncoded])
					if psEnc.State_Fxx[1].VAD_flags[psEnc.State_Fxx[0].NFramesEncoded] == 0 {
						silk.Silk_stereo_encode_mid_only(psRangeEnc, midOnlyFlag)
					}
				}
			} else {
				copy(psEnc.SStereo.SMid[:], psEnc.State_Fxx[0].InputBuf[:2])
				copy(psEnc.State_Fxx[0].InputBuf[psEnc.State_Fxx[0].Frame_length:psEnc.State_Fxx[0].Frame_length+2], psEnc.SStereo.SMid[:])
			}

			psEnc.State_Fxx[0].Silk_encode_do_VAD()

			for n := 0; n < encControl.NChannelsInternal; n++ {
				maxBits := encControl.MaxBits
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
				if encControl.UseCBR != 0 && curr_block == tot_blocks-1 {
					useCBR = 1
				}

				if encControl.NChannelsInternal == 1 {
					channelRate_bps = TargetRate_bps
				} else {
					channelRate_bps = MStargetRates_bps[n]

					if n == 0 && MStargetRates_bps[1] > 0 {
						useCBR = 0
						maxBits -= encControl.MaxBits / (tot_blocks * 2)
					}
				}

				if channelRate_bps > 0 {
					psEnc.State_Fxx[n].Silk_control_SNR(channelRate_bps)

					condCoding := SilkConstants.CODE_INDEPENDENTLY
					if psEnc.State_Fxx[0].NFramesEncoded-n > 0 {
						if n > 0 && psEnc.Prev_decode_only_middle != 0 {
							condCoding = SilkConstants.CODE_INDEPENDENTLY_NO_LTP_SCALING
						} else {
							condCoding = SilkConstants.CODE_CONDITIONALLY
						}
					}

					ret += psEnc.State_Fxx[n].Silk_encode_frame(nBytesOut, psRangeEnc, condCoding, maxBits, useCBR)
					inlines.OpusAssert(ret == SilkError.SILK_NO_ERROR)
				}

				psEnc.State_Fxx[n].Controlled_since_last_payload = 0
				psEnc.State_Fxx[n].InputBufIx = 0
				psEnc.State_Fxx[n].NFramesEncoded++
			}

			psEnc.Prev_decode_only_middle = int(psEnc.SStereo.Mid_only_flags[psEnc.State_Fxx[0].NFramesEncoded-1])

			if nBytesOut.Val > 0 && psEnc.State_Fxx[0].NFramesEncoded == psEnc.State_Fxx[0].NFramesPerPacket {
				flags = 0
				for n := 0; n < encControl.NChannelsInternal; n++ {
					for i := 0; i < psEnc.State_Fxx[n].NFramesPerPacket; i++ {
						flags <<= 1
						if psEnc.State_Fxx[n].VAD_flags[i] != 0 {
							flags |= 1
						}
					}
					flags <<= 1
					if psEnc.State_Fxx[n].LBRR_flag != 0 {
						flags |= 1
					}
				}

				if PrefillFlag == 0 {
					psRangeEnc.Enc_patch_initial_bits(int64(flags), (psEnc.State_Fxx[0].NFramesPerPacket+1)*encControl.NChannelsInternal)
				}

				if psEnc.State_Fxx[0].InDTX != 0 && (encControl.NChannelsInternal == 1 || psEnc.State_Fxx[1].InDTX != 0) {
					nBytesOut.Val = 0
				}

				psEnc.NBitsExceeded += nBytesOut.Val * 8
				psEnc.NBitsExceeded -= (encControl.BitRate * encControl.PayloadSize_ms) / 1000
				if psEnc.NBitsExceeded < 0 {
					psEnc.NBitsExceeded = 0
				} else if psEnc.NBitsExceeded > 10000 {
					psEnc.NBitsExceeded = 10000
				}

				speech_act_thr_for_switch_Q8 = inlines.Silk_SMLAWB(
					int(math.Trunc(float64(TuningParameters.SPEECH_ACTIVITY_DTX_THRES)*256.0+0.5)),
					int(math.Trunc(
						((1.0-float64(TuningParameters.SPEECH_ACTIVITY_DTX_THRES))/
							float64(TuningParameters.MAX_BANDWIDTH_SWITCH_DELAY_MS))*
							16777216.0+0.5,
					)),
					psEnc.TimeSinceSwitchAllowed_ms,
				)
				if psEnc.State_Fxx[0].Speech_activity_Q8 < speech_act_thr_for_switch_Q8 {
					psEnc.AllowBandwidthSwitch = 1
					psEnc.TimeSinceSwitchAllowed_ms = 0
				} else {
					psEnc.AllowBandwidthSwitch = 0
					psEnc.TimeSinceSwitchAllowed_ms += encControl.PayloadSize_ms
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

	psEnc.NPrevChannelsInternal = encControl.NChannelsInternal

	encControl.AllowBandwidthSwitch = psEnc.AllowBandwidthSwitch
	if psEnc.State_Fxx[0].Fs_kHz == 16 && psEnc.State_Fxx[0].SLP.Mode == 0 {
		encControl.InWBmodeWithoutVariableLP = 1
	} else {
		encControl.InWBmodeWithoutVariableLP = 0
	}
	encControl.InternalSampleRate = inlines.Silk_SMULBB(psEnc.State_Fxx[0].Fs_kHz, 1000)
	if encControl.ToMono != 0 {
		encControl.StereoWidth_Q14 = 0
	} else {
		encControl.StereoWidth_Q14 = int(psEnc.SStereo.Smth_width_Q14)
	}

	if PrefillFlag != 0 {
		encControl.PayloadSize_ms = tmp_PayloadSize_ms
		encControl.Complexity = tmp_Complexity
		for n := 0; n < encControl.NChannelsInternal; n++ {
			psEnc.State_Fxx[n].Controlled_since_last_payload = 0
			psEnc.State_Fxx[n].PrefillFlag = 0
		}
	}

	return ret
}
