package opus

func silk_InitDecoder(decState *SilkDecoder) int {
	decState.Reset()
	ret := SilkError.SILK_NO_ERROR
	channel_states := decState.channel_state
	for n := 0; n < DECODER_NUM_CHANNELS; n++ {
		ret = channel_states[n].silk_init_decoder()
	}
	decState.sStereo.Reset()
	decState.prev_decode_only_middle = 0
	return ret
}

func silk_Decode(
	psDec *SilkDecoder,
	decControl *DecControlState,
	lostFlag int,
	newPacketFlag int,
	psRangeDec *EntropyCoder,
	samplesOut []int16,
	samplesOut_ptr int,
	nSamplesOut *BoxedValueInt,
) int {
	var i, n, decode_only_middle = 0, 0, 0

	var ret = SilkError.SILK_NO_ERROR
	var LBRR_symbol int
	var nSamplesOutDec = &BoxedValueInt{0}
	var samplesOut_tmp []int16
	var samplesOut_tmp_ptrs = make([]int, 2)
	var samplesOut1_tmp_storage1 []int16
	var samplesOut1_tmp_storage2 []int16
	var samplesOut2_tmp []int16
	var MS_pred_Q13 = []int{0, 0}
	var resample_out []int16
	var resample_out_ptr int
	channel_state := psDec.channel_state
	var has_side int
	var stereo_to_mono int
	var delay_stack_alloc int
	nSamplesOut.Val = 0

	OpusAssert(decControl.nChannelsInternal == 1 || decControl.nChannelsInternal == 2)

	/**
	 * *******************************
	 */
	/* Test if first frame in payload */
	/**
	 * *******************************
	 */
	if newPacketFlag != 0 {
		for n = 0; n < decControl.nChannelsInternal; n++ {
			channel_state[n].nFramesDecoded = 0
			/* Used to count frames in packet */
		}
	}

	/* If Mono . Stereo transition in bitstream: init state of second channel */
	if decControl.nChannelsInternal > psDec.nChannelsInternal {
		ret += channel_state[1].silk_init_decoder()
	}

	stereo_to_mono = boolToInt(decControl.nChannelsInternal == 1 && psDec.nChannelsInternal == 2 && (decControl.internalSampleRate == 1000*channel_state[0].fs_kHz))

	if channel_state[0].nFramesDecoded == 0 {
		for n = 0; n < decControl.nChannelsInternal; n++ {
			var fs_kHz_dec int
			if decControl.payloadSize_ms == 0 {
				/* Assuming packet loss, use 10 ms */
				channel_state[n].nFramesPerPacket = 1
				channel_state[n].nb_subfr = 2
			} else if decControl.payloadSize_ms == 10 {
				channel_state[n].nFramesPerPacket = 1
				channel_state[n].nb_subfr = 2
			} else if decControl.payloadSize_ms == 20 {
				channel_state[n].nFramesPerPacket = 1
				channel_state[n].nb_subfr = 4
			} else if decControl.payloadSize_ms == 40 {
				channel_state[n].nFramesPerPacket = 2
				channel_state[n].nb_subfr = 4
			} else if decControl.payloadSize_ms == 60 {
				channel_state[n].nFramesPerPacket = 3
				channel_state[n].nb_subfr = 4
			} else {
				OpusAssert(false)
				return SilkError.SILK_DEC_INVALID_FRAME_SIZE
			}
			fs_kHz_dec = (decControl.internalSampleRate >> 10) + 1
			if fs_kHz_dec != 8 && fs_kHz_dec != 12 && fs_kHz_dec != 16 {
				OpusAssert(false)
				return SilkError.SILK_DEC_INVALID_SAMPLING_FREQUENCY
			}
			ret += channel_state[n].silk_decoder_set_fs(fs_kHz_dec, decControl.API_sampleRate)
		}
	}

	if decControl.nChannelsAPI == 2 && decControl.nChannelsInternal == 2 && (psDec.nChannelsAPI == 1 || psDec.nChannelsInternal == 1) {
		MemSetLen(psDec.sStereo.pred_prev_Q13[:], 0, 2)
		MemSetLen(psDec.sStereo.sSide[:], 0, 2)
		channel_state[1].resampler_state.Assign(channel_state[0].resampler_state)
	}
	psDec.nChannelsAPI = decControl.nChannelsAPI
	psDec.nChannelsInternal = decControl.nChannelsInternal

	if decControl.API_sampleRate > SilkConstants.MAX_API_FS_KHZ*1000 || decControl.API_sampleRate < 8000 {
		ret = SilkError.SILK_DEC_INVALID_SAMPLING_FREQUENCY
		return (ret)
	}

	if lostFlag != FLAG_PACKET_LOST && channel_state[0].nFramesDecoded == 0 {
		/* First decoder call for this payload */
		/* Decode VAD flags and LBRR flag */
		for n = 0; n < decControl.nChannelsInternal; n++ {
			for i = 0; i < channel_state[n].nFramesPerPacket; i++ {
				channel_state[n].VAD_flags[i] = psRangeDec.dec_bit_logp(1)
			}
			channel_state[n].LBRR_flag = psRangeDec.dec_bit_logp(1)
		}
		/* Decode LBRR flags */
		for n = 0; n < decControl.nChannelsInternal; n++ {
			MemSetLen(channel_state[n].LBRR_flags[:], 0, SilkConstants.MAX_FRAMES_PER_PACKET)
			if channel_state[n].LBRR_flag != 0 {
				if channel_state[n].nFramesPerPacket == 1 {
					channel_state[n].LBRR_flags[0] = 1
				} else {
					LBRR_symbol = psRangeDec.dec_icdf(silk_LBRR_flags_iCDF_ptr[channel_state[n].nFramesPerPacket-2], 8) + 1
					for i = 0; i < channel_state[n].nFramesPerPacket; i++ {
						channel_state[n].LBRR_flags[i] = silk_RSHIFT(LBRR_symbol, i) & 1
					}
				}
			}
		}

		if lostFlag == FLAG_DECODE_NORMAL {
			/* Regular decoding: skip all LBRR data */
			for i = 0; i < channel_state[0].nFramesPerPacket; i++ {
				for n = 0; n < decControl.nChannelsInternal; n++ {
					if channel_state[n].LBRR_flags[i] != 0 {
						pulses := make([]int16, SilkConstants.MAX_FRAME_LENGTH)
						var condCoding int

						if decControl.nChannelsInternal == 2 && n == 0 {
							silk_stereo_decode_pred(psRangeDec, MS_pred_Q13)
							if channel_state[1].LBRR_flags[i] == 0 {
								decodeOnlyMiddleBoxed := &BoxedValueInt{decode_only_middle}
								silk_stereo_decode_mid_only(psRangeDec, decodeOnlyMiddleBoxed)
								decode_only_middle = decodeOnlyMiddleBoxed.Val
							}
						}
						/* Use conditional coding if previous frame available */
						if i > 0 && (channel_state[n].LBRR_flags[i-1] != 0) {
							condCoding = SilkConstants.CODE_CONDITIONALLY
						} else {
							condCoding = SilkConstants.CODE_INDEPENDENTLY
						}
						silk_decode_indices(channel_state[n], psRangeDec, i, 1, condCoding)
						silk_decode_pulses(psRangeDec, pulses, int(channel_state[n].indices.signalType),
							int(channel_state[n].indices.quantOffsetType), channel_state[n].frame_length)
					}
				}
			}
		}
	}

	/* Get MS predictor index */
	if decControl.nChannelsInternal == 2 {
		if lostFlag == FLAG_DECODE_NORMAL || (lostFlag == FLAG_DECODE_LBRR && channel_state[0].LBRR_flags[channel_state[0].nFramesDecoded] == 1) {
			silk_stereo_decode_pred(psRangeDec, MS_pred_Q13)
			/* For LBRR data, decode mid-only flag only if side-channel's LBRR flag is false */
			if (lostFlag == FLAG_DECODE_NORMAL && channel_state[1].VAD_flags[channel_state[0].nFramesDecoded] == 0) ||
				(lostFlag == FLAG_DECODE_LBRR && channel_state[1].LBRR_flags[channel_state[0].nFramesDecoded] == 0) {
				decodeOnlyMiddleBoxed := &BoxedValueInt{decode_only_middle}
				silk_stereo_decode_mid_only(psRangeDec, decodeOnlyMiddleBoxed)
				decode_only_middle = decodeOnlyMiddleBoxed.Val
			} else {
				decode_only_middle = 0
			}
		} else {
			for n = 0; n < 2; n++ {
				MS_pred_Q13[n] = int(psDec.sStereo.pred_prev_Q13[n])
			}
		}
	}

	/* Reset side channel decoder prediction memory for first frame with side coding */
	if decControl.nChannelsInternal == 2 && decode_only_middle == 0 && psDec.prev_decode_only_middle == 1 {
		MemSetLen(psDec.channel_state[1].outBuf, 0, SilkConstants.MAX_FRAME_LENGTH+2*SilkConstants.MAX_SUB_FRAME_LENGTH)
		MemSetLen(psDec.channel_state[1].sLPC_Q14_buf, 0, SilkConstants.MAX_LPC_ORDER)
		psDec.channel_state[1].lagPrev = 100
		psDec.channel_state[1].LastGainIndex = 10
		psDec.channel_state[1].prevSignalType = SilkConstants.TYPE_NO_VOICE_ACTIVITY
		psDec.channel_state[1].first_frame_after_reset = 1
	}

	/* Check if the temp buffer fits into the output PCM buffer. If it fits,
	   we can delay allocating the temp buffer until after the SILK peak stack
	   usage. We need to use a < and not a <= because of the two extra samples. */
	delay_stack_alloc = boolToInt(decControl.internalSampleRate*decControl.nChannelsInternal < decControl.API_sampleRate*decControl.nChannelsAPI)

	if delay_stack_alloc != 0 {
		samplesOut_tmp = samplesOut
		samplesOut_tmp_ptrs[0] = samplesOut_ptr
		samplesOut_tmp_ptrs[1] = samplesOut_ptr + channel_state[0].frame_length + 2
	} else {
		samplesOut1_tmp_storage1 = make([]int16, decControl.nChannelsInternal*(channel_state[0].frame_length+2))
		samplesOut_tmp = samplesOut1_tmp_storage1
		samplesOut_tmp_ptrs[0] = 0
		samplesOut_tmp_ptrs[1] = channel_state[0].frame_length + 2
	}

	if lostFlag == FLAG_DECODE_NORMAL {
		has_side = boolToInt(decode_only_middle == 0)
	} else {
		has_side = boolToInt(psDec.prev_decode_only_middle == 0 ||
			(decControl.nChannelsInternal == 2 &&
				lostFlag == FLAG_DECODE_LBRR &&
				channel_state[1].LBRR_flags[channel_state[1].nFramesDecoded] == 1))
	}
	/* Call decoder for one frame */
	for n = 0; n < decControl.nChannelsInternal; n++ {
		if n == 0 || (has_side != 0) {
			var FrameIndex int
			var condCoding int

			FrameIndex = channel_state[0].nFramesDecoded - n
			/* Use independent coding if no previous frame available */
			if FrameIndex <= 0 {
				condCoding = SilkConstants.CODE_INDEPENDENTLY
			} else if lostFlag == FLAG_DECODE_LBRR {
				condCoding = SilkConstants.CODE_INDEPENDENTLY
				if channel_state[n].LBRR_flags[FrameIndex-1] != 0 {
					condCoding = SilkConstants.CODE_CONDITIONALLY
				}

			} else if n > 0 && (psDec.prev_decode_only_middle != 0) {
				/* If we skipped a side frame in this packet, we don't
				   need LTP scaling; the LTP state is well-defined. */
				condCoding = SilkConstants.CODE_INDEPENDENTLY_NO_LTP_SCALING
			} else {
				condCoding = SilkConstants.CODE_CONDITIONALLY
			}
			ret += channel_state[n].silk_decode_frame(psRangeDec, samplesOut_tmp, samplesOut_tmp_ptrs[n]+2, nSamplesOutDec, lostFlag, condCoding)
		} else {
			MemSetWithOffset(samplesOut_tmp, 0, samplesOut_tmp_ptrs[n]+2, nSamplesOutDec.Val)
		}
		channel_state[n].nFramesDecoded++
	}

	if decControl.nChannelsAPI == 2 && decControl.nChannelsInternal == 2 {
		/* Convert Mid/Side to Left/Right */
		silk_stereo_MS_to_LR(psDec.sStereo, samplesOut_tmp, samplesOut_tmp_ptrs[0], samplesOut_tmp, samplesOut_tmp_ptrs[1], MS_pred_Q13, channel_state[0].fs_kHz, nSamplesOutDec.Val)
	} else {
		/* Buffering */
		//	System.arraycopy(psDec.sStereo.sMid, 0, samplesOut_tmp, samplesOut_tmp_ptrs[0], 2)
		copy(samplesOut_tmp[samplesOut_tmp_ptrs[0]:samplesOut_tmp_ptrs[0]+2], psDec.sStereo.sMid[:2])

		//System.arraycopy(samplesOut_tmp, samplesOut_tmp_ptrs[0]+nSamplesOutDec.Val, psDec.sStereo.sMid, 0, 2)
		copy(psDec.sStereo.sMid[:2], samplesOut_tmp[samplesOut_tmp_ptrs[0]+nSamplesOutDec.Val:])
	}

	/* Number of output samples */
	nSamplesOut.Val = silk_DIV32(nSamplesOutDec.Val*decControl.API_sampleRate, silk_SMULBB(channel_state[0].fs_kHz, 1000))

	/* Set up pointers to temp buffers */
	if decControl.nChannelsAPI == 2 {
		samplesOut2_tmp = make([]int16, nSamplesOut.Val)
		resample_out = samplesOut2_tmp
		resample_out_ptr = 0
	} else {
		resample_out = samplesOut
		resample_out_ptr = samplesOut_ptr
	}

	if delay_stack_alloc != 0 {
		samplesOut1_tmp_storage2 = make([]int16, decControl.nChannelsInternal*(channel_state[0].frame_length+2))
		//	System.arraycopy(samplesOut, samplesOut_ptr, samplesOut1_tmp_storage2, 0, decControl.nChannelsInternal*(channel_state[0].frame_length+2))
		copy(samplesOut1_tmp_storage2, samplesOut[samplesOut_ptr:decControl.nChannelsInternal*(channel_state[0].frame_length+2)])
		samplesOut_tmp = samplesOut1_tmp_storage2
		samplesOut_tmp_ptrs[0] = 0
		samplesOut_tmp_ptrs[1] = channel_state[0].frame_length + 2
	}

	for n = 0; n < silk_min(decControl.nChannelsAPI, decControl.nChannelsInternal); n++ {

		/* Resample decoded signal to API_sampleRate */
		ret += silk_resampler(channel_state[n].resampler_state, resample_out, resample_out_ptr, samplesOut_tmp, samplesOut_tmp_ptrs[n]+1, nSamplesOutDec.Val)

		/* Interleave if stereo output and stereo stream */
		if decControl.nChannelsAPI == 2 {
			nptr := samplesOut_ptr + n
			for i = 0; i < nSamplesOut.Val; i++ {
				samplesOut[nptr+2*i] = resample_out[resample_out_ptr+i]
			}
		}
	}

	/* Create two channel output from mono stream */
	if decControl.nChannelsAPI == 2 && decControl.nChannelsInternal == 1 {
		if stereo_to_mono != 0 {
			/* Resample right channel for newly collapsed stereo just in case
			   we weren't doing collapsing when switching to mono */
			ret += silk_resampler(channel_state[1].resampler_state, resample_out, resample_out_ptr, samplesOut_tmp, samplesOut_tmp_ptrs[0]+1, nSamplesOutDec.Val)

			for i = 0; i < nSamplesOut.Val; i++ {
				samplesOut[samplesOut_ptr+1+2*i] = resample_out[resample_out_ptr+i]
			}
		} else {
			for i = 0; i < nSamplesOut.Val; i++ {
				samplesOut[samplesOut_ptr+1+2*i] = samplesOut[samplesOut_ptr+2*i]
			}
		}
	}

	/* Export pitch lag, measured at 48 kHz sampling rate */
	if channel_state[0].prevSignalType == SilkConstants.TYPE_VOICED {
		mult_tab := []int{6, 4, 3}
		decControl.prevPitchLag = channel_state[0].lagPrev * mult_tab[(channel_state[0].fs_kHz-8)>>2]
	} else {
		decControl.prevPitchLag = 0
	}

	if lostFlag == FLAG_PACKET_LOST {
		/* On packet loss, remove the gain clamping to prevent having the energy "bounce back"
		   if we lose packets when the energy is going down */
		for i = 0; i < psDec.nChannelsInternal; i++ {
			psDec.channel_state[i].LastGainIndex = 10
		}
	} else {
		psDec.prev_decode_only_middle = decode_only_middle
	}

	return ret
}
