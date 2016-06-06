using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Silk
{
    public static class dec_API
    {
        /// <summary>
        /// Reset decoder state
        /// </summary>
        /// <param name="decState">I/O  Stat</param>
        /// <returns>Returns error code</returns>
        public static int silk_InitDecoder(silk_decoder decState)
        {
            /* Reset decoder */
            decState.Reset();

            int n, ret = SilkError.SILK_NO_ERROR;
            Pointer<silk_decoder_state> channel_states = decState.channel_state;

            for (n = 0; n < SilkConstants.DECODER_NUM_CHANNELS; n++)
            {
                ret = silk_decoder_state.silk_init_decoder(channel_states[n]);
            }

            decState.sStereo.Reset();

            /* Not strictly needed, but it's cleaner that way */
            decState.prev_decode_only_middle = 0;

            return ret;
        }

        /* Decode a frame */
        public static int silk_Decode(                                   /* O    Returns error code                              */
            silk_decoder psDec,           /* I/O  State                                           */
            silk_DecControlStruct decControl,         /* I/O  Control Structure                               */
            int lostFlag,           /* I    0: no loss, 1 loss, 2 decode fec                */
            int newPacketFlag,      /* I    Indicates first decoder call for this packet    */
            ec_ctx psRangeDec,        /* I/O  Compressor data structure                       */
            Pointer<short> samplesOut,        /* O    Decoded output speech vector                    */
            BoxedValue<int> nSamplesOut,       /* O    Number of samples decoded                       */
            int arch                /* I    Run-time architecture                           */
        )
        {
            int i, n, decode_only_middle = 0, ret = SilkError.SILK_NO_ERROR;
            int LBRR_symbol;
            BoxedValue<int> nSamplesOutDec = new BoxedValue<int>();
            Pointer<Pointer<short>> samplesOut1_tmp = Pointer.Malloc<Pointer<short>>(2);
            Pointer<short> samplesOut1_tmp_storage1;
            Pointer<short> samplesOut1_tmp_storage2;
            Pointer<short> samplesOut2_tmp;
            Pointer<int> MS_pred_Q13 = new Pointer<int>(new int[] { 0, 0 });
            Pointer<short> resample_out_ptr;
            Pointer<silk_decoder_state> channel_state = psDec.channel_state;
            int has_side;
            int stereo_to_mono;
            int delay_stack_alloc;

            Debug.Assert(decControl.nChannelsInternal == 1 || decControl.nChannelsInternal == 2);

            /**********************************/
            /* Test if first frame in payload */
            /**********************************/
            if (newPacketFlag != 0)
            {
                for (n = 0; n < decControl.nChannelsInternal; n++)
                {
                    channel_state[n].nFramesDecoded = 0;  /* Used to count frames in packet */
                }
            }

            /* If Mono . Stereo transition in bitstream: init state of second channel */
            if (decControl.nChannelsInternal > psDec.nChannelsInternal)
            {
                ret += silk_decoder_state.silk_init_decoder(channel_state[1]);
            }

            stereo_to_mono = (decControl.nChannelsInternal == 1 && psDec.nChannelsInternal == 2 &&
                             (decControl.internalSampleRate == 1000 * channel_state[0].fs_kHz)) ? 1 : 0;

            if (channel_state[0].nFramesDecoded == 0)
            {
                for (n = 0; n < decControl.nChannelsInternal; n++)
                {
                    int fs_kHz_dec;
                    if (decControl.payloadSize_ms == 0)
                    {
                        /* Assuming packet loss, use 10 ms */
                        channel_state[n].nFramesPerPacket = 1;
                        channel_state[n].nb_subfr = 2;
                    }
                    else if (decControl.payloadSize_ms == 10)
                    {
                        channel_state[n].nFramesPerPacket = 1;
                        channel_state[n].nb_subfr = 2;
                    }
                    else if (decControl.payloadSize_ms == 20)
                    {
                        channel_state[n].nFramesPerPacket = 1;
                        channel_state[n].nb_subfr = 4;
                    }
                    else if (decControl.payloadSize_ms == 40)
                    {
                        channel_state[n].nFramesPerPacket = 2;
                        channel_state[n].nb_subfr = 4;
                    }
                    else if (decControl.payloadSize_ms == 60)
                    {
                        channel_state[n].nFramesPerPacket = 3;
                        channel_state[n].nb_subfr = 4;
                    }
                    else {
                        Debug.Assert(false);
                        return SilkError.SILK_DEC_INVALID_FRAME_SIZE;
                    }
                    fs_kHz_dec = (decControl.internalSampleRate >> 10) + 1;
                    if (fs_kHz_dec != 8 && fs_kHz_dec != 12 && fs_kHz_dec != 16)
                    {
                        Debug.Assert(false);
                        return SilkError.SILK_DEC_INVALID_SAMPLING_FREQUENCY;
                    }
                    ret += decoder_set_fs.silk_decoder_set_fs(channel_state[n], fs_kHz_dec, decControl.API_sampleRate);
                }
            }

            if (decControl.nChannelsAPI == 2 && decControl.nChannelsInternal == 2 && (psDec.nChannelsAPI == 1 || psDec.nChannelsInternal == 1))
            {
                psDec.sStereo.pred_prev_Q13.MemSet(0, 2);
                psDec.sStereo.sSide.MemSet(0, 2);
                channel_state[1].resampler_state.Assign(channel_state[0].resampler_state);
            }
            psDec.nChannelsAPI = decControl.nChannelsAPI;
            psDec.nChannelsInternal = decControl.nChannelsInternal;

            if (decControl.API_sampleRate > (int)SilkConstants.MAX_API_FS_KHZ * 1000 || decControl.API_sampleRate < 8000)
            {
                ret = SilkError.SILK_DEC_INVALID_SAMPLING_FREQUENCY;
                return (ret);
            }

            if (lostFlag != DecoderAPIFlag.FLAG_PACKET_LOST && channel_state[0].nFramesDecoded == 0)
            {
                /* First decoder call for this payload */
                /* Decode VAD flags and LBRR flag */
                for (n = 0; n < decControl.nChannelsInternal; n++)
                {
                    for (i = 0; i < channel_state[n].nFramesPerPacket; i++)
                    {
                        channel_state[n].VAD_flags[i] = EntropyCoder.ec_dec_bit_logp(psRangeDec, 1);
                    }
                    channel_state[n].LBRR_flag = EntropyCoder.ec_dec_bit_logp(psRangeDec, 1);
                }
                /* Decode LBRR flags */
                for (n = 0; n < decControl.nChannelsInternal; n++)
                {
                    channel_state[n].LBRR_flags.MemSet(0, SilkConstants.MAX_FRAMES_PER_PACKET);
                    if (channel_state[n].LBRR_flag != 0)
                    {
                        if (channel_state[n].nFramesPerPacket == 1)
                        {
                            channel_state[n].LBRR_flags[0] = 1;
                        }
                        else {
                            LBRR_symbol = EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_LBRR_flags_iCDF_ptr[channel_state[n].nFramesPerPacket - 2].GetPointer(), 8) + 1;
                            for (i = 0; i < channel_state[n].nFramesPerPacket; i++)
                            {
                                channel_state[n].LBRR_flags[i] = Inlines.silk_RSHIFT(LBRR_symbol, i) & 1;
                            }
                        }
                    }
                }

                if (lostFlag == DecoderAPIFlag.FLAG_DECODE_NORMAL)
                {
                    /* Regular decoding: skip all LBRR data */
                    for (i = 0; i < channel_state[0].nFramesPerPacket; i++)
                    {
                        for (n = 0; n < decControl.nChannelsInternal; n++)
                        {
                            if (channel_state[n].LBRR_flags[i] != 0)
                            {
                                Pointer<short> pulses = Pointer.Malloc<short>(SilkConstants.MAX_FRAME_LENGTH);
                                int condCoding;

                                if (decControl.nChannelsInternal == 2 && n == 0)
                                {
                                    Stereo.silk_stereo_decode_pred(psRangeDec, MS_pred_Q13);
                                    if (channel_state[1].LBRR_flags[i] == 0)
                                    {
                                        BoxedValue<int> decodeOnlyMiddleBoxed = new BoxedValue<int>(decode_only_middle);
                                        Stereo.silk_stereo_decode_mid_only(psRangeDec, decodeOnlyMiddleBoxed);
                                        decode_only_middle = decodeOnlyMiddleBoxed.Val;
                                    }
                                }
                                /* Use conditional coding if previous frame available */
                                if (i > 0 && (channel_state[n].LBRR_flags[i - 1] != 0))
                                {
                                    condCoding = SilkConstants.CODE_CONDITIONALLY;
                                }
                                else
                                {
                                    condCoding = SilkConstants.CODE_INDEPENDENTLY;
                                }
                                decode_indices.silk_decode_indices(channel_state[n], psRangeDec, i, 1, condCoding);
                                decode_pulses.silk_decode_pulses(psRangeDec, pulses, channel_state[n].indices.signalType,
                                    channel_state[n].indices.quantOffsetType, channel_state[n].frame_length);
                            }
                        }
                    }
                }
            }

            /* Get MS predictor index */
            if (decControl.nChannelsInternal == 2)
            {
                if (lostFlag == DecoderAPIFlag.FLAG_DECODE_NORMAL ||
                    (lostFlag == DecoderAPIFlag.FLAG_DECODE_LBRR && channel_state[0].LBRR_flags[channel_state[0].nFramesDecoded] == 1))
                {
                    Stereo.silk_stereo_decode_pred(psRangeDec, MS_pred_Q13);
                    /* For LBRR data, decode mid-only flag only if side-channel's LBRR flag is false */
                    if ((lostFlag == DecoderAPIFlag.FLAG_DECODE_NORMAL && channel_state[1].VAD_flags[channel_state[0].nFramesDecoded] == 0) ||
                        (lostFlag == DecoderAPIFlag.FLAG_DECODE_LBRR && channel_state[1].LBRR_flags[channel_state[0].nFramesDecoded] == 0))
                    {
                        BoxedValue<int> decodeOnlyMiddleBoxed = new BoxedValue<int>(decode_only_middle);
                        Stereo.silk_stereo_decode_mid_only(psRangeDec, decodeOnlyMiddleBoxed);
                        decode_only_middle = decodeOnlyMiddleBoxed.Val;
                    }
                    else
                    {
                        decode_only_middle = 0;
                    }
                }
                else
                {
                    for (n = 0; n < 2; n++)
                    {
                        MS_pred_Q13[n] = psDec.sStereo.pred_prev_Q13[n];
                    }
                }
            }

            /* Reset side channel decoder prediction memory for first frame with side coding */
            if (decControl.nChannelsInternal == 2 && decode_only_middle == 0 && psDec.prev_decode_only_middle == 1)
            {
                psDec.channel_state[1].outBuf.MemSet(0, SilkConstants.MAX_FRAME_LENGTH + 2 * SilkConstants.MAX_SUB_FRAME_LENGTH);
                psDec.channel_state[1].sLPC_Q14_buf.MemSet(0, SilkConstants.MAX_LPC_ORDER);
                psDec.channel_state[1].lagPrev = 100;
                psDec.channel_state[1].LastGainIndex = 10;
                psDec.channel_state[1].prevSignalType = SilkConstants.TYPE_NO_VOICE_ACTIVITY;
                psDec.channel_state[1].first_frame_after_reset = 1;
            }

            /* Check if the temp buffer fits into the output PCM buffer. If it fits,
               we can delay allocating the temp buffer until after the SILK peak stack
               usage. We need to use a < and not a <= because of the two extra samples. */
            delay_stack_alloc = (decControl.internalSampleRate * decControl.nChannelsInternal
                   < decControl.API_sampleRate * decControl.nChannelsAPI) ? 1 : 0;
            
            if (delay_stack_alloc != 0)
            {
                samplesOut1_tmp[0] = samplesOut;
                samplesOut1_tmp[1] = samplesOut.Point(channel_state[0].frame_length + 2);
            }
            else
            {
                samplesOut1_tmp_storage1 = Pointer.Malloc<short>(decControl.nChannelsInternal * (channel_state[0].frame_length + 2));
                samplesOut1_tmp[0] = samplesOut1_tmp_storage1;
                samplesOut1_tmp[1] = samplesOut1_tmp_storage1.Point(channel_state[0].frame_length + 2);
            }

            if (lostFlag == DecoderAPIFlag.FLAG_DECODE_NORMAL)
            {
                has_side = (decode_only_middle == 0) ? 1 : 0;
            }
            else
            {
                has_side = (psDec.prev_decode_only_middle == 0
                      || (decControl.nChannelsInternal == 2 &&
                        lostFlag == DecoderAPIFlag.FLAG_DECODE_LBRR &&
                        channel_state[1].LBRR_flags[channel_state[1].nFramesDecoded] == 1)) ? 1 : 0;
            }
            /* Call decoder for one frame */
            for (n = 0; n < decControl.nChannelsInternal; n++)
            {
                if (n == 0 || (has_side != 0))
                {
                    int FrameIndex;
                    int condCoding;

                    FrameIndex = channel_state[0].nFramesDecoded - n;
                    /* Use independent coding if no previous frame available */
                    if (FrameIndex <= 0)
                    {
                        condCoding = SilkConstants.CODE_INDEPENDENTLY;
                    }
                    else if (lostFlag == DecoderAPIFlag.FLAG_DECODE_LBRR)
                    {
                        condCoding = (channel_state[n].LBRR_flags[FrameIndex - 1] != 0) ? SilkConstants.CODE_CONDITIONALLY : SilkConstants.CODE_INDEPENDENTLY;
                    }
                    else if (n > 0 && (psDec.prev_decode_only_middle != 0))
                    {
                        /* If we skipped a side frame in this packet, we don't
                           need LTP scaling; the LTP state is well-defined. */
                        condCoding = SilkConstants.CODE_INDEPENDENTLY_NO_LTP_SCALING;
                    }
                    else
                    {
                        condCoding = SilkConstants.CODE_CONDITIONALLY;
                    }
                    ret += decode_frame.silk_decode_frame(channel_state[n], psRangeDec, samplesOut1_tmp[n].Point(2), nSamplesOutDec, lostFlag, condCoding, arch);
                }
                else
                {
                    samplesOut1_tmp[n].Point(2).MemSet(0, nSamplesOutDec.Val);
                }
                channel_state[n].nFramesDecoded++;
            }

            if (decControl.nChannelsAPI == 2 && decControl.nChannelsInternal == 2)
            {
                /* Convert Mid/Side to Left/Right */
                Stereo.silk_stereo_MS_to_LR(psDec.sStereo, samplesOut1_tmp[0], samplesOut1_tmp[1], MS_pred_Q13, channel_state[0].fs_kHz, nSamplesOutDec.Val);
            }
            else
            {
                /* Buffering */
                psDec.sStereo.sMid.MemCopyTo(samplesOut1_tmp[0], 2);
                samplesOut1_tmp[0].Point(nSamplesOutDec.Val).MemCopyTo(psDec.sStereo.sMid, 2);
            }

            /* Number of output samples */
            nSamplesOut.Val = Inlines.silk_DIV32(nSamplesOutDec.Val * decControl.API_sampleRate, Inlines.silk_SMULBB(channel_state[0].fs_kHz, 1000));

            /* Set up pointers to temp buffers */
            if (decControl.nChannelsAPI == 2)
            {
                samplesOut2_tmp = Pointer.Malloc<short>(nSamplesOut.Val);
                resample_out_ptr = samplesOut2_tmp;
            }
            else {
                resample_out_ptr = samplesOut;
            }
            
            if (delay_stack_alloc != 0)
            {
                samplesOut1_tmp_storage2 = Pointer.Malloc<short>(decControl.nChannelsInternal * (channel_state[0].frame_length + 2));
                samplesOut.MemCopyTo(samplesOut1_tmp_storage2, decControl.nChannelsInternal * (channel_state[0].frame_length + 2));
                samplesOut1_tmp[0] = samplesOut1_tmp_storage2;
                samplesOut1_tmp[1] = samplesOut1_tmp_storage2.Point(channel_state[0].frame_length + 2);
            }
            for (n = 0; n < Inlines.silk_min(decControl.nChannelsAPI, decControl.nChannelsInternal); n++)
            {

                /* Resample decoded signal to API_sampleRate */
                ret += Resampler.silk_resampler(channel_state[n].resampler_state, resample_out_ptr, samplesOut1_tmp[n].Point(1), nSamplesOutDec.Val);

                /* Interleave if stereo output and stereo stream */
                if (decControl.nChannelsAPI == 2)
                {
                    for (i = 0; i < nSamplesOut.Val; i++)
                    {
                        samplesOut[n + 2 * i] = resample_out_ptr[i];
                    }
                }
            }

            /* Create two channel output from mono stream */
            if (decControl.nChannelsAPI == 2 && decControl.nChannelsInternal == 1)
            {
                if (stereo_to_mono != 0)
                {
                    /* Resample right channel for newly collapsed stereo just in case
                       we weren't doing collapsing when switching to mono */
                    ret += Resampler.silk_resampler(channel_state[1].resampler_state, resample_out_ptr, samplesOut1_tmp[0].Point(1), nSamplesOutDec.Val);

                    for (i = 0; i < nSamplesOut.Val; i++)
                    {
                        samplesOut[1 + 2 * i] = resample_out_ptr[i];
                    }
                }
                else {
                    for (i = 0; i < nSamplesOut.Val; i++)
                    {
                        samplesOut[1 + 2 * i] = samplesOut[0 + 2 * i];
                    }
                }
            }

            /* Export pitch lag, measured at 48 kHz sampling rate */
            if (channel_state[0].prevSignalType == SilkConstants.TYPE_VOICED)
            {
                int[] mult_tab = { 6, 4, 3 };
                decControl.prevPitchLag = channel_state[0].lagPrev * mult_tab[(channel_state[0].fs_kHz - 8) >> 2];
            }
            else
            {
                decControl.prevPitchLag = 0;
            }

            if (lostFlag == DecoderAPIFlag.FLAG_PACKET_LOST)
            {
                /* On packet loss, remove the gain clamping to prevent having the energy "bounce back"
                   if we lose packets when the energy is going down */
                for (i = 0; i < psDec.nChannelsInternal; i++)
                    psDec.channel_state[i].LastGainIndex = 10;
            }
            else
            {
                psDec.prev_decode_only_middle = decode_only_middle;
            }

            return ret;
        }
    }
}
