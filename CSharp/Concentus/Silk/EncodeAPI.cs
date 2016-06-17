using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    internal static class EncodeAPI
    {
        /// <summary>
        /// Init or Reset encoder
        /// </summary>
        /// <param name="encState">I/O  State</param>
        /// <param name="arch">I    Run-time architecture</param>
        /// <param name="encStatus">O    Encoder Status</param>
        /// <returns>O    Returns error code</returns>
        internal static int silk_InitEncoder(SilkEncoder encState, EncControlState encStatus)
        {
            int ret = SilkError.SILK_NO_ERROR;

            /* Reset encoder */
            encState.Reset();

            for (int n = 0; n < SilkConstants.ENCODER_NUM_CHANNELS; n++)
            {
                ret += SilkEncoder.silk_init_encoder(encState.state_Fxx[n]);
                Inlines.OpusAssert(ret == SilkError.SILK_NO_ERROR);
            }

            encState.nChannelsAPI = 1;
            encState.nChannelsInternal = 1;

            /* Read control structure */
            ret += silk_QueryEncoder(encState, encStatus);
            Inlines.OpusAssert(ret == SilkError.SILK_NO_ERROR);

            return ret;
        }

        /// <summary>
        /// Read control structure from encode
        /// </summary>
        /// <param name="encState">I    State</param>
        /// <param name="encStatus">O    Encoder Status</param>
        /// <returns>Returns error code</returns>
        internal static int silk_QueryEncoder(SilkEncoder encState, EncControlState encStatus)
        {
            int ret = SilkError.SILK_NO_ERROR;
            SilkChannelEncoder state_Fxx = encState.state_Fxx[0];
            
            encStatus.Reset();

            encStatus.nChannelsAPI = encState.nChannelsAPI;
            encStatus.nChannelsInternal = encState.nChannelsInternal;
            encStatus.API_sampleRate = state_Fxx.API_fs_Hz;
            encStatus.maxInternalSampleRate = state_Fxx.maxInternal_fs_Hz;
            encStatus.minInternalSampleRate = state_Fxx.minInternal_fs_Hz;
            encStatus.desiredInternalSampleRate = state_Fxx.desiredInternal_fs_Hz;
            encStatus.payloadSize_ms = state_Fxx.PacketSize_ms;
            encStatus.bitRate = state_Fxx.TargetRate_bps;
            encStatus.packetLossPercentage = state_Fxx.PacketLoss_perc;
            encStatus.complexity = state_Fxx.Complexity;
            encStatus.useInBandFEC = state_Fxx.useInBandFEC;
            encStatus.useDTX = state_Fxx.useDTX;
            encStatus.useCBR = state_Fxx.useCBR;
            encStatus.internalSampleRate = Inlines.silk_SMULBB(state_Fxx.fs_kHz, 1000);
            encStatus.allowBandwidthSwitch = state_Fxx.allow_bandwidth_switch;
            encStatus.inWBmodeWithoutVariableLP = (state_Fxx.fs_kHz == 16 && state_Fxx.sLP.mode == 0) ? 1 : 0;

            return ret;
        }

        /// <summary>
        /// Encode frame with Silk
        /// Note: if prefillFlag is set, the input must contain 10 ms of audio, irrespective of what
        /// encControl.payloadSize_ms is set to  
        /// </summary>
        /// <param name="psEnc">I/O  State</param>
        /// <param name="encControl">I    Control status</param>
        /// <param name="samplesIn">I    Speech sample input vector</param>
        /// <param name="nSamplesIn">I    Number of samples in input vector</param>
        /// <param name="psRangeEnc">I/O  Compressor data structure</param>
        /// <param name="nBytesOut">I/O  Number of bytes in payload (input: Max bytes)</param>
        /// <param name="prefillFlag">I    Flag to indicate prefilling buffers no coding</param>
        /// <returns>error code</returns>
        internal static int silk_Encode(
            SilkEncoder psEnc,
            EncControlState encControl,
            Pointer<short> samplesIn,
            int nSamplesIn,
            ec_ctx psRangeEnc,
            BoxedValue<int> nBytesOut,
            int prefillFlag)
        {
            int ret = SilkError.SILK_NO_ERROR;
            int n, i, nBits, flags, tmp_payloadSize_ms = 0, tmp_complexity = 0;
            int nSamplesToBuffer, nSamplesToBufferMax, nBlocksOf10ms;
            int nSamplesFromInput = 0, nSamplesFromInputMax;
            int speech_act_thr_for_switch_Q8;
            int TargetRate_bps, channelRate_bps, LBRR_symbol, sum;
            int[] MStargetRates_bps = new int[2];
            Pointer<short> buf;
            int transition, curr_block, tot_blocks;
            nBytesOut.Val = 0;

            if (encControl.reducedDependency != 0)
            {
                psEnc.state_Fxx[0].first_frame_after_reset = 1;
                psEnc.state_Fxx[1].first_frame_after_reset = 1;
            }
            psEnc.state_Fxx[0].nFramesEncoded = psEnc.state_Fxx[1].nFramesEncoded = 0;

            /* Check values in encoder control structure */
            ret += encControl.check_control_input();
            if (ret != SilkError.SILK_NO_ERROR)
            {
                Inlines.OpusAssert(false);
                return ret;
            }

            encControl.switchReady = 0;

            if (encControl.nChannelsInternal > psEnc.nChannelsInternal)
            {
                /* Mono . Stereo transition: init state of second channel and stereo state */
                ret += SilkEncoder.silk_init_encoder(psEnc.state_Fxx[1]);

                psEnc.sStereo.pred_prev_Q13.MemSet(0, 2);
                psEnc.sStereo.sSide.MemSet(0, 2);
                psEnc.sStereo.mid_side_amp_Q0[0] = 0;
                psEnc.sStereo.mid_side_amp_Q0[1] = 1;
                psEnc.sStereo.mid_side_amp_Q0[2] = 0;
                psEnc.sStereo.mid_side_amp_Q0[3] = 1;
                psEnc.sStereo.width_prev_Q14 = 0;
                psEnc.sStereo.smth_width_Q14 = Inlines.CHOP16(Inlines.SILK_CONST(1.0f, 14));
                if (psEnc.nChannelsAPI == 2)
                {
                    // silk_memcpy(&psEnc.state_Fxx[1].resampler_state, &psEnc.state_Fxx[0].resampler_state, sizeof(silk_resampler_state_struct));
                    psEnc.state_Fxx[1].resampler_state.Assign(psEnc.state_Fxx[0].resampler_state);

                    //silk_memcpy(&psEnc.state_Fxx[1].In_HP_State, &psEnc.state_Fxx[0].In_HP_State, sizeof(psEnc.state_Fxx[1].In_HP_State) );
                    psEnc.state_Fxx[0].In_HP_State.MemCopyTo(psEnc.state_Fxx[1].In_HP_State, 2);
                }
            }

            transition = ((encControl.payloadSize_ms != psEnc.state_Fxx[0].PacketSize_ms) || (psEnc.nChannelsInternal != encControl.nChannelsInternal)) ? 1 : 0;

            psEnc.nChannelsAPI = encControl.nChannelsAPI;
            psEnc.nChannelsInternal = encControl.nChannelsInternal;

            nBlocksOf10ms = Inlines.silk_DIV32(100 * nSamplesIn, encControl.API_sampleRate);
            tot_blocks = (nBlocksOf10ms > 1) ? nBlocksOf10ms >> 1 : 1;
            curr_block = 0;
            if (prefillFlag != 0)
            {
                /* Only accept input length of 10 ms */
                if (nBlocksOf10ms != 1)
                {
                    Inlines.OpusAssert(false);
                    return SilkError.SILK_ENC_INPUT_INVALID_NO_OF_SAMPLES;
                }
                /* Reset Encoder */
                for (n = 0; n < encControl.nChannelsInternal; n++)
                {
                    ret += SilkEncoder.silk_init_encoder(psEnc.state_Fxx[n]);
                    Inlines.OpusAssert(ret == SilkError.SILK_NO_ERROR);
                }
                tmp_payloadSize_ms = encControl.payloadSize_ms;
                encControl.payloadSize_ms = 10;
                tmp_complexity = encControl.complexity;
                encControl.complexity = 0;
                for (n = 0; n < encControl.nChannelsInternal; n++)
                {
                    psEnc.state_Fxx[n].controlled_since_last_payload = 0;
                    psEnc.state_Fxx[n].prefillFlag = 1;
                }
            }
            else
            {
                /* Only accept input lengths that are a multiple of 10 ms */
                if (nBlocksOf10ms * encControl.API_sampleRate != 100 * nSamplesIn || nSamplesIn < 0)
                {
                    Inlines.OpusAssert(false);
                    return SilkError.SILK_ENC_INPUT_INVALID_NO_OF_SAMPLES;
                }
                /* Make sure no more than one packet can be produced */
                if (1000 * (int)nSamplesIn > encControl.payloadSize_ms * encControl.API_sampleRate)
                {
                    Inlines.OpusAssert(false);
                    return SilkError.SILK_ENC_INPUT_INVALID_NO_OF_SAMPLES;
                }
            }

            TargetRate_bps = Inlines.silk_RSHIFT32(encControl.bitRate, encControl.nChannelsInternal - 1);

            for (n = 0; n < encControl.nChannelsInternal; n++)
            {
                /* Force the side channel to the same rate as the mid */
                int force_fs_kHz = (n == 1) ? psEnc.state_Fxx[0].fs_kHz : 0;
                ret += ControlCodec.silk_control_encoder(psEnc.state_Fxx[n], encControl, TargetRate_bps, psEnc.allowBandwidthSwitch, n, force_fs_kHz);

                if (ret != SilkError.SILK_NO_ERROR)
                {
                    Inlines.OpusAssert(false);
                    return ret;
                }

                if (psEnc.state_Fxx[n].first_frame_after_reset != 0 || transition != 0)
                {
                    for (i = 0; i < psEnc.state_Fxx[0].nFramesPerPacket; i++)
                    {
                        psEnc.state_Fxx[n].LBRR_flags[i] = 0;
                    }
                }

                psEnc.state_Fxx[n].inDTX = psEnc.state_Fxx[n].useDTX;
            }

            Inlines.OpusAssert(encControl.nChannelsInternal == 1 || psEnc.state_Fxx[0].fs_kHz == psEnc.state_Fxx[1].fs_kHz);

            /* Input buffering/resampling and encoding */
            nSamplesToBufferMax = 10 * nBlocksOf10ms * psEnc.state_Fxx[0].fs_kHz;
            nSamplesFromInputMax =
                    Inlines.silk_DIV32_16(nSamplesToBufferMax *
                                       psEnc.state_Fxx[0].API_fs_Hz,
                                   Inlines.CHOP16(psEnc.state_Fxx[0].fs_kHz * 1000));

            buf = Pointer.Malloc<short>(nSamplesFromInputMax);

            while (true)
            {
                nSamplesToBuffer = psEnc.state_Fxx[0].frame_length - psEnc.state_Fxx[0].inputBufIx;
                nSamplesToBuffer = Inlines.silk_min(nSamplesToBuffer, nSamplesToBufferMax);
                nSamplesFromInput = Inlines.silk_DIV32_16(nSamplesToBuffer * psEnc.state_Fxx[0].API_fs_Hz, psEnc.state_Fxx[0].fs_kHz * 1000);

                /* Resample and write to buffer */
                if (encControl.nChannelsAPI == 2 && encControl.nChannelsInternal == 2)
                {
                    int id = psEnc.state_Fxx[0].nFramesEncoded;
                    for (n = 0; n < nSamplesFromInput; n++)
                    {
                        buf[n] = samplesIn[2 * n];
                    }

                    /* Making sure to start both resamplers from the same state when switching from mono to stereo */
                    if (psEnc.nPrevChannelsInternal == 1 && id == 0)
                    {
                        //silk_memcpy(&psEnc.state_Fxx[1].resampler_state, &psEnc.state_Fxx[0].resampler_state, sizeof(psEnc.state_Fxx[1].resampler_state));
                        psEnc.state_Fxx[1].resampler_state.Assign(psEnc.state_Fxx[0].resampler_state);
                    }

                    ret += Resampler.silk_resampler(
                        psEnc.state_Fxx[0].resampler_state,
                        psEnc.state_Fxx[0].inputBuf.Point(psEnc.state_Fxx[0].inputBufIx + 2),
                        buf,
                        nSamplesFromInput);

                    psEnc.state_Fxx[0].inputBufIx += nSamplesToBuffer;

                    nSamplesToBuffer = psEnc.state_Fxx[1].frame_length - psEnc.state_Fxx[1].inputBufIx;
                    nSamplesToBuffer = Inlines.silk_min(nSamplesToBuffer, 10 * nBlocksOf10ms * psEnc.state_Fxx[1].fs_kHz);
                    for (n = 0; n < nSamplesFromInput; n++)
                    {
                        buf[n] = samplesIn[2 * n + 1];
                    }
                    ret += Resampler.silk_resampler(
                        psEnc.state_Fxx[1].resampler_state,
                        psEnc.state_Fxx[1].inputBuf.Point(psEnc.state_Fxx[1].inputBufIx + 2),
                        buf,
                        nSamplesFromInput);

                    psEnc.state_Fxx[1].inputBufIx += nSamplesToBuffer;
                }
                else if (encControl.nChannelsAPI == 2 && encControl.nChannelsInternal == 1)
                {
                    /* Combine left and right channels before resampling */
                    for (n = 0; n < nSamplesFromInput; n++)
                    {
                        sum = samplesIn[2 * n] + samplesIn[2 * n + 1];
                        buf[n] = (short)Inlines.silk_RSHIFT_ROUND(sum, 1);
                    }

                    ret += Resampler.silk_resampler(
                        psEnc.state_Fxx[0].resampler_state,
                        psEnc.state_Fxx[0].inputBuf.Point(psEnc.state_Fxx[0].inputBufIx + 2),
                        buf,
                        nSamplesFromInput);

                    /* On the first mono frame, average the results for the two resampler states  */
                    if (psEnc.nPrevChannelsInternal == 2 && psEnc.state_Fxx[0].nFramesEncoded == 0)
                    {
                        ret += Resampler.silk_resampler(
                            psEnc.state_Fxx[1].resampler_state,
                            psEnc.state_Fxx[1].inputBuf.Point(psEnc.state_Fxx[1].inputBufIx + 2),
                            buf,
                            nSamplesFromInput);

                        for (n = 0; n < psEnc.state_Fxx[0].frame_length; n++)
                        {
                            psEnc.state_Fxx[0].inputBuf[psEnc.state_Fxx[0].inputBufIx + n + 2] =
                                  Inlines.CHOP16(Inlines.silk_RSHIFT(psEnc.state_Fxx[0].inputBuf[psEnc.state_Fxx[0].inputBufIx + n + 2]
                                            + psEnc.state_Fxx[1].inputBuf[psEnc.state_Fxx[1].inputBufIx + n + 2], 1));
                        }
                    }

                    psEnc.state_Fxx[0].inputBufIx += nSamplesToBuffer;
                }
                else
                {
                    Inlines.OpusAssert(encControl.nChannelsAPI == 1 && encControl.nChannelsInternal == 1);
                    samplesIn.MemCopyTo(buf, nSamplesFromInput);
                    ret += Resampler.silk_resampler(
                        psEnc.state_Fxx[0].resampler_state,
                        psEnc.state_Fxx[0].inputBuf.Point(psEnc.state_Fxx[0].inputBufIx + 2),
                        buf,
                        nSamplesFromInput);

                    psEnc.state_Fxx[0].inputBufIx += nSamplesToBuffer;
                }

                samplesIn = samplesIn.Point(nSamplesFromInput * encControl.nChannelsAPI);
                nSamplesIn -= nSamplesFromInput;

                /* Default */
                psEnc.allowBandwidthSwitch = 0;

                /* Silk encoder */
                if (psEnc.state_Fxx[0].inputBufIx >= psEnc.state_Fxx[0].frame_length)
                {
                    /* Enough data in input buffer, so encode */
                    Inlines.OpusAssert(psEnc.state_Fxx[0].inputBufIx == psEnc.state_Fxx[0].frame_length);
                    Inlines.OpusAssert(encControl.nChannelsInternal == 1 || psEnc.state_Fxx[1].inputBufIx == psEnc.state_Fxx[1].frame_length);

                    /* Deal with LBRR data */
                    if (psEnc.state_Fxx[0].nFramesEncoded == 0 && prefillFlag == 0)
                    {
                        /* Create space at start of payload for VAD and FEC flags */
                        byte[] iCDF = { 0, 0 };
                        iCDF[0] = Inlines.CHOP8U(256 - Inlines.silk_RSHIFT(256, (psEnc.state_Fxx[0].nFramesPerPacket + 1) * encControl.nChannelsInternal));
                        EntropyCoder.ec_enc_icdf(psRangeEnc, 0, iCDF.GetPointer(), 8);

                        /* Encode any LBRR data from previous packet */
                        /* Encode LBRR flags */
                        for (n = 0; n < encControl.nChannelsInternal; n++)
                        {
                            LBRR_symbol = 0;
                            for (i = 0; i < psEnc.state_Fxx[n].nFramesPerPacket; i++)
                            {
                                LBRR_symbol |= Inlines.silk_LSHIFT(psEnc.state_Fxx[n].LBRR_flags[i], i);
                            }

                            psEnc.state_Fxx[n].LBRR_flag = (sbyte)(LBRR_symbol > 0 ? 1 : 0);
                            if (LBRR_symbol != 0 && psEnc.state_Fxx[n].nFramesPerPacket > 1)
                            {
                                EntropyCoder.ec_enc_icdf(psRangeEnc, LBRR_symbol - 1, Tables.silk_LBRR_flags_iCDF_ptr[psEnc.state_Fxx[n].nFramesPerPacket - 2].GetPointer(), 8);
                            }
                        }

                        /* Code LBRR indices and excitation signals */
                        for (i = 0; i < psEnc.state_Fxx[0].nFramesPerPacket; i++)
                        {
                            for (n = 0; n < encControl.nChannelsInternal; n++)
                            {
                                if (psEnc.state_Fxx[n].LBRR_flags[i] != 0)
                                {
                                    int condCoding;

                                    if (encControl.nChannelsInternal == 2 && n == 0)
                                    {
                                        Stereo.silk_stereo_encode_pred(psRangeEnc, psEnc.sStereo.predIx[i]);
                                        /* For LBRR data there's no need to code the mid-only flag if the side-channel LBRR flag is set */
                                        if (psEnc.state_Fxx[1].LBRR_flags[i] == 0)
                                        {
                                            Stereo.silk_stereo_encode_mid_only(psRangeEnc, psEnc.sStereo.mid_only_flags[i]);
                                        }
                                    }

                                    /* Use conditional coding if previous frame available */
                                    if (i > 0 && psEnc.state_Fxx[n].LBRR_flags[i - 1] != 0)
                                    {
                                        condCoding = SilkConstants.CODE_CONDITIONALLY;
                                    }
                                    else
                                    {
                                        condCoding = SilkConstants.CODE_INDEPENDENTLY;
                                    }

                                    EncodeIndices.silk_encode_indices(psEnc.state_Fxx[n], psRangeEnc, i, 1, condCoding);
                                    EncodePulses.silk_encode_pulses(psRangeEnc, psEnc.state_Fxx[n].indices_LBRR[i].signalType, psEnc.state_Fxx[n].indices_LBRR[i].quantOffsetType,
                                        psEnc.state_Fxx[n].pulses_LBRR[i], psEnc.state_Fxx[n].frame_length);
                                }
                            }
                        }

                        /* Reset LBRR flags */
                        for (n = 0; n < encControl.nChannelsInternal; n++)
                        {
                            psEnc.state_Fxx[n].LBRR_flags.MemSet(0, SilkConstants.MAX_FRAMES_PER_PACKET);
                        }

                        psEnc.nBitsUsedLBRR = EntropyCoder.ec_tell(psRangeEnc);
                    }

                    HPVariableCutoff.silk_HP_variable_cutoff(psEnc.state_Fxx);

                    /* Total target bits for packet */
                    nBits = Inlines.silk_DIV32_16(Inlines.silk_MUL(encControl.bitRate, encControl.payloadSize_ms), 1000);

                    /* Subtract bits used for LBRR */
                    if (prefillFlag == 0)
                    {
                        nBits -= psEnc.nBitsUsedLBRR;
                    }

                    /* Divide by number of uncoded frames left in packet */
                    nBits = Inlines.silk_DIV32_16(nBits, psEnc.state_Fxx[0].nFramesPerPacket);

                    /* Convert to bits/second */
                    if (encControl.payloadSize_ms == 10)
                    {
                        TargetRate_bps = Inlines.silk_SMULBB(nBits, 100);
                    }
                    else
                    {
                        TargetRate_bps = Inlines.silk_SMULBB(nBits, 50);
                    }

                    /* Subtract fraction of bits in excess of target in previous frames and packets */
                    TargetRate_bps -= Inlines.silk_DIV32_16(Inlines.silk_MUL(psEnc.nBitsExceeded, 1000), TuningParameters.BITRESERVOIR_DECAY_TIME_MS);

                    if (prefillFlag == 0 && psEnc.state_Fxx[0].nFramesEncoded > 0)
                    {
                        /* Compare actual vs target bits so far in this packet */
                        int bitsBalance = EntropyCoder.ec_tell(psRangeEnc) - psEnc.nBitsUsedLBRR - nBits * psEnc.state_Fxx[0].nFramesEncoded;
                        TargetRate_bps -= Inlines.silk_DIV32_16(Inlines.silk_MUL(bitsBalance, 1000), TuningParameters.BITRESERVOIR_DECAY_TIME_MS);
                    }

                    /* Never exceed input bitrate */
                    TargetRate_bps = Inlines.silk_LIMIT(TargetRate_bps, encControl.bitRate, 5000);

                    /* Convert Left/Right to Mid/Side */
                    if (encControl.nChannelsInternal == 2)
                    {
                        BoxedValue<sbyte> midOnlyFlagBoxed = new BoxedValue<sbyte>(psEnc.sStereo.mid_only_flags[psEnc.state_Fxx[0].nFramesEncoded]);
                        Stereo.silk_stereo_LR_to_MS(psEnc.sStereo,
                            psEnc.state_Fxx[0].inputBuf.Point(2),
                            psEnc.state_Fxx[1].inputBuf.Point(2),
                            psEnc.sStereo.predIx[psEnc.state_Fxx[0].nFramesEncoded],
                            midOnlyFlagBoxed,
                            MStargetRates_bps.GetPointer(),
                            TargetRate_bps,
                            psEnc.state_Fxx[0].speech_activity_Q8,
                            encControl.toMono,
                            psEnc.state_Fxx[0].fs_kHz,
                            psEnc.state_Fxx[0].frame_length);

                        psEnc.sStereo.mid_only_flags[psEnc.state_Fxx[0].nFramesEncoded] = midOnlyFlagBoxed.Val;

                        if (midOnlyFlagBoxed.Val == 0)
                        {
                            /* Reset side channel encoder memory for first frame with side coding */
                            if (psEnc.prev_decode_only_middle == 1)
                            {
                                // silk_memset(&psEnc.state_Fxx[1].sShape, 0, sizeof(psEnc.state_Fxx[1].sShape) );
                                psEnc.state_Fxx[1].sShape.Reset();
                                // silk_memset(&psEnc.state_Fxx[1].sPrefilt, 0, sizeof(psEnc.state_Fxx[1].sPrefilt) );
                                psEnc.state_Fxx[1].sPrefilt.Reset();
                                // silk_memset(&psEnc.state_Fxx[1].sNSQ, 0, sizeof(psEnc.state_Fxx[1].sNSQ) );
                                psEnc.state_Fxx[1].sNSQ.Reset();
                                // silk_memset(psEnc.state_Fxx[1].prev_NLSFq_Q15, 0, sizeof(psEnc.state_Fxx[1].prev_NLSFq_Q15) );
                                psEnc.state_Fxx[1].prev_NLSFq_Q15.MemSet(0, SilkConstants.MAX_LPC_ORDER);
                                //silk_memset(&psEnc.state_Fxx[1].sLP.In_LP_State, 0, sizeof(psEnc.state_Fxx[1].sLP.In_LP_State) );
                                psEnc.state_Fxx[1].sLP.In_LP_State.MemSet(0, 2);

                                psEnc.state_Fxx[1].prevLag = 100;
                                psEnc.state_Fxx[1].sNSQ.lagPrev = 100;
                                psEnc.state_Fxx[1].sShape.LastGainIndex = 10;
                                psEnc.state_Fxx[1].prevSignalType = SilkConstants.TYPE_NO_VOICE_ACTIVITY;
                                psEnc.state_Fxx[1].sNSQ.prev_gain_Q16 = 65536;
                                psEnc.state_Fxx[1].first_frame_after_reset = 1;
                            }

                            EncodeFrame.silk_encode_do_VAD(psEnc.state_Fxx[1]);
                        }
                        else
                        {
                            psEnc.state_Fxx[1].VAD_flags[psEnc.state_Fxx[0].nFramesEncoded] = 0;
                        }

                        if (prefillFlag == 0)
                        {
                            Stereo.silk_stereo_encode_pred(psRangeEnc, psEnc.sStereo.predIx[psEnc.state_Fxx[0].nFramesEncoded]);
                            if (psEnc.state_Fxx[1].VAD_flags[psEnc.state_Fxx[0].nFramesEncoded] == 0)
                            {
                                Stereo.silk_stereo_encode_mid_only(psRangeEnc, psEnc.sStereo.mid_only_flags[psEnc.state_Fxx[0].nFramesEncoded]);
                            }
                        }
                    }
                    else
                    {
                        /* Buffering */
                        psEnc.sStereo.sMid.MemCopyTo(psEnc.state_Fxx[0].inputBuf, 2);
                        psEnc.state_Fxx[0].inputBuf.Point(psEnc.state_Fxx[0].frame_length).MemCopyTo(psEnc.sStereo.sMid, 2);
                    }

                    EncodeFrame.silk_encode_do_VAD(psEnc.state_Fxx[0]);

                    /* Encode */
                    for (n = 0; n < encControl.nChannelsInternal; n++)
                    {
                        int maxBits, useCBR;

                        /* Handling rate constraints */
                        maxBits = encControl.maxBits;
                        if (tot_blocks == 2 && curr_block == 0)
                        {
                            maxBits = maxBits * 3 / 5;
                        }

                        else if (tot_blocks == 3)
                        {
                            if (curr_block == 0)
                            {
                                maxBits = maxBits * 2 / 5;
                            }
                            else if (curr_block == 1)
                            {
                                maxBits = maxBits * 3 / 4;
                            }
                        }

                        useCBR = (encControl.useCBR != 0 && curr_block == tot_blocks - 1) ? 1 : 0;

                        if (encControl.nChannelsInternal == 1)
                        {
                            channelRate_bps = TargetRate_bps;
                        }
                        else
                        {
                            channelRate_bps = MStargetRates_bps[n];
                            if (n == 0 && MStargetRates_bps[1] > 0)
                            {
                                useCBR = 0;
                                /* Give mid up to 1/2 of the max bits for that frame */
                                maxBits -= encControl.maxBits / (tot_blocks * 2);
                            }
                        }

                        if (channelRate_bps > 0)
                        {
                            int condCoding;

                            ControlSNR.silk_control_SNR(psEnc.state_Fxx[n], channelRate_bps);

                            /* Use independent coding if no previous frame available */
                            if (psEnc.state_Fxx[0].nFramesEncoded - n <= 0)
                            {
                                condCoding = SilkConstants.CODE_INDEPENDENTLY;
                            }
                            else if (n > 0 && psEnc.prev_decode_only_middle != 0)
                            {
                                /* If we skipped a side frame in this packet, we don't
                                   need LTP scaling; the LTP state is well-defined. */
                                condCoding = SilkConstants.CODE_INDEPENDENTLY_NO_LTP_SCALING;
                            }
                            else
                            {
                                condCoding = SilkConstants.CODE_CONDITIONALLY;
                            }

                            ret += EncodeFrame.silk_encode_frame(psEnc.state_Fxx[n], nBytesOut, psRangeEnc, condCoding, maxBits, useCBR);
                            Inlines.OpusAssert(ret == SilkError.SILK_NO_ERROR);
                        }

                        psEnc.state_Fxx[n].controlled_since_last_payload = 0;
                        psEnc.state_Fxx[n].inputBufIx = 0;
                        psEnc.state_Fxx[n].nFramesEncoded++;
                    }

                    psEnc.prev_decode_only_middle = psEnc.sStereo.mid_only_flags[psEnc.state_Fxx[0].nFramesEncoded - 1];

                    /* Insert VAD and FEC flags at beginning of bitstream */
                    if (nBytesOut.Val > 0 && psEnc.state_Fxx[0].nFramesEncoded == psEnc.state_Fxx[0].nFramesPerPacket)
                    {
                        flags = 0;
                        for (n = 0; n < encControl.nChannelsInternal; n++)
                        {
                            for (i = 0; i < psEnc.state_Fxx[n].nFramesPerPacket; i++)
                            {
                                flags = Inlines.silk_LSHIFT(flags, 1);
                                flags |= psEnc.state_Fxx[n].VAD_flags[i];
                            }
                            flags = Inlines.silk_LSHIFT(flags, 1);
                            flags |= psEnc.state_Fxx[n].LBRR_flag;
                        }

                        if (prefillFlag == 0)
                        {
                            EntropyCoder.ec_enc_patch_initial_bits(psRangeEnc, (uint)flags, (uint)((psEnc.state_Fxx[0].nFramesPerPacket + 1) * encControl.nChannelsInternal));
                        }

                        /* Return zero bytes if all channels DTXed */
                        if (psEnc.state_Fxx[0].inDTX != 0 && (encControl.nChannelsInternal == 1 || psEnc.state_Fxx[1].inDTX != 0))
                        {
                            nBytesOut.Val = 0;
                        }

                        psEnc.nBitsExceeded += nBytesOut.Val * 8;
                        psEnc.nBitsExceeded -= Inlines.silk_DIV32_16(Inlines.silk_MUL(encControl.bitRate, encControl.payloadSize_ms), 1000);
                        psEnc.nBitsExceeded = Inlines.silk_LIMIT(psEnc.nBitsExceeded, 0, 10000);

                        /* Update flag indicating if bandwidth switching is allowed */
                        speech_act_thr_for_switch_Q8 = Inlines.silk_SMLAWB(Inlines.SILK_CONST(TuningParameters.SPEECH_ACTIVITY_DTX_THRES, 8),
                                            Inlines.SILK_CONST((1 - TuningParameters.SPEECH_ACTIVITY_DTX_THRES) / TuningParameters.MAX_BANDWIDTH_SWITCH_DELAY_MS, 16 + 8),
                                                psEnc.timeSinceSwitchAllowed_ms);
                        if (psEnc.state_Fxx[0].speech_activity_Q8 < speech_act_thr_for_switch_Q8)
                        {
                            psEnc.allowBandwidthSwitch = 1;
                            psEnc.timeSinceSwitchAllowed_ms = 0;
                        }
                        else
                        {
                            psEnc.allowBandwidthSwitch = 0;
                            psEnc.timeSinceSwitchAllowed_ms += encControl.payloadSize_ms;
                        }
                    }

                    if (nSamplesIn == 0)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }

                curr_block++;
            }

            psEnc.nPrevChannelsInternal = encControl.nChannelsInternal;

            encControl.allowBandwidthSwitch = psEnc.allowBandwidthSwitch;
            encControl.inWBmodeWithoutVariableLP = (psEnc.state_Fxx[0].fs_kHz == 16 && psEnc.state_Fxx[0].sLP.mode == 0) ? 1 : 0;
            encControl.internalSampleRate = Inlines.silk_SMULBB(psEnc.state_Fxx[0].fs_kHz, 1000);
            encControl.stereoWidth_Q14 = encControl.toMono != 0 ? 0 : psEnc.sStereo.smth_width_Q14;

            if (prefillFlag != 0)
            {
                encControl.payloadSize_ms = tmp_payloadSize_ms;
                encControl.complexity = tmp_complexity;

                for (n = 0; n < encControl.nChannelsInternal; n++)
                {
                    psEnc.state_Fxx[n].controlled_since_last_payload = 0;
                    psEnc.state_Fxx[n].prefillFlag = 0;
                }
            }

            return ret;
        }
    }
}

//────────────────────▄▄▄▄▄▄▄▄▄▄────────────────────
//─────────────▄▄▄▄████▓▓▓▓▓▓▓▓▓██▄▄────────────────
//─────────▄▄██▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▒▒▒▒▒░▀▀▄─────────────
//────────▀▀▀▀▀█████▓▓▓▓▒▒▒▒▒▒▒▒▒▒░░░░░▀▄───────────
//────────────▄▄█▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒░░░░░░░░░▀▄─────────
//─────▄▀▀▄─▄█▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒▒░░░░░░░░░░░█▄▀▀▄─────
//────█░░░▄█▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒▒▒░░░░░░░░░░░░█░░░░░█────
//───█░░░█▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒░░░░░░░░░░░░▄▀░░░▄░░█───
//──█░░░█▓▓▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒░░░░░░░░░░░░░░█░░░░░█░░█──
//──█░░█▓▒▒▄▄▒▒▒▒▒▒▒▒▒▒▒▒░░░░░░░░░░░░░░██░░░░░░█░█──
//─█░░█▓▄▄▀█▒▒▒▒▒▒▒▒▒▒▒▒░░░░░░▄▄▀█░░░░▄▀█░░░░░░█░░█─
//─█░██▀░▄▀▒▒▒▒▒▒▒▒▒▒▒▒▄▄▄▄▀▀▀░▄▀░░░▄▀░░░░░░░░░█░░█─
//─██▀░░█▒▒▒▒▒▒▒▒▒▄▄▀▀▀░░░░░░▄▀░░▄▄▀░░░░░░░░░░░░░░█─
//──█░░█▒▒▒▒▒▒▒▄▀▀░░░░░░░░░░▀▀▀▀▀░░░░░░░░░░▄▄░░░░░█─
//───██▒▒▒▒▒▒▄▀██▄▄░░░░░█░░░░░░░░░░░░░▄▄▄█▀░░░░░░█──
//────█▒▒▒▒▒█─▓██████▄▄█░░░█░░░░▄▄▄████▓─█▀▀░░░░█───
//───█▒▒▒▒▄█─▓█──████▓─█░░░░████████──█▓─█▄░░░▄▀────
//───█▒▒▄▀█──▓█▒▓████▓─█░░░░█─▓█████▓▒█▓─█░░█▀──────
//──█▒▄▀█░█──▓███─███▓─█░░░░█─▓████─██▓──█░█────────
//──█▀──█░█──▓▓██████▓─█░░░░█─▓██████▓▓──█░█────────
//──────█░░█──▓▓████▓─█░░░░░░█─▓████▓▓──█░░█────────
//──────█░░▀▄───▓▓▓▓──█░░░░░░█──▓▓▓▓───▄▀░░█────────
//───────█░░▀▄───────█░░░░░░░░█───────▄▀░░█─────────
//───────█░░░░▀▄▄▄▄▄▀░░▄▀▀▀▀▄░░▀▄▄▄▄▄▀░░░░█─────────
//────────█░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░█──────────
//─────────█░░░░░░░░░░▀▄░░░░▄▀░░░░░░░░░░█───────────
//──────────▀▄░░░░░░░░░░░░░░░░░░░░░░░░▄▀────────────
//────────────▀▄░░░░░░▀▄░░░░▄▀░░░░░░▄▀──────────────
//──────────────▀▄▄░░░░░▀▀▀▀░░░░░▄▄▀────────────────
//─────────────────▀▀▄▄▄▄▄▄▄▄▄▄▀▀───────────────────
//──────────────────────────────────────────────────
//───────█───█───█─████──███──███──█───█─████─█─────
//──────█─█──█───█─█────█────█───█─██─██─█────█─────
//─────█▄▄▄█─█─█─█─██────██──█───█─█─█─█─██───█─────
//─────█───█──█─█──█───────█─█───█─█───█─█──────────
//─────█───█──█─█──████─███───███──█───█─████─█─────
//──────────────────────────────────────────────────
//──────────────────────────────────────────────────