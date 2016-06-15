using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class encode_frame
    {
        private const bool TRACE_FILE = true;

        public static void silk_encode_do_VAD_FIX(
            silk_encoder_state_fix psEnc                                  /* I/O  Pointer to Silk FIX encoder state                                           */
        )
        {
            /****************************/
            /* Voice Activity Detection */
            /****************************/
            VAD.silk_VAD_GetSA_Q8_c(psEnc.sCmn, psEnc.sCmn.inputBuf.Point(1));

            /**************************************************/
            /* Convert speech activity into VAD and DTX flags */
            /**************************************************/
            if (psEnc.sCmn.speech_activity_Q8 < Inlines.SILK_FIX_CONST(TuningParameters.SPEECH_ACTIVITY_DTX_THRES, 8))
            {
                psEnc.sCmn.indices.signalType = SilkConstants.TYPE_NO_VOICE_ACTIVITY;
                psEnc.sCmn.noSpeechCounter++;
                if (psEnc.sCmn.noSpeechCounter < SilkConstants.NB_SPEECH_FRAMES_BEFORE_DTX)
                {
                    psEnc.sCmn.inDTX = 0;
                }
                else if (psEnc.sCmn.noSpeechCounter > SilkConstants.MAX_CONSECUTIVE_DTX + SilkConstants.NB_SPEECH_FRAMES_BEFORE_DTX)
                {
                    psEnc.sCmn.noSpeechCounter = SilkConstants.NB_SPEECH_FRAMES_BEFORE_DTX;
                    psEnc.sCmn.inDTX = 0;
                }
                psEnc.sCmn.VAD_flags[psEnc.sCmn.nFramesEncoded] = 0;
            }
            else {
                psEnc.sCmn.noSpeechCounter = 0;
                psEnc.sCmn.inDTX = 0;
                psEnc.sCmn.indices.signalType = SilkConstants.TYPE_UNVOICED;
                psEnc.sCmn.VAD_flags[psEnc.sCmn.nFramesEncoded] = 1;
            }
        }

        /****************/
        /* Encode frame */
        /****************/
        public static int silk_encode_frame_FIX(
            silk_encoder_state_fix psEnc,                                 /* I/O  Pointer to Silk FIX encoder state                                           */
            BoxedValue<int> pnBytesOut,                            /* O    Pointer to number of payload bytes;                                         */
            ec_ctx psRangeEnc,                            /* I/O  compressor data structure                                                   */
            int condCoding,                             /* I    The type of conditional coding to use                                       */
            int maxBits,                                /* I    If > 0: maximum number of output bits                                       */
            int useCBR                                  /* I    Flag to force constant-bitrate operation                                    */
        )
        {
            if (TRACE_FILE) Debug.WriteLine("Entering silk encode frame");
            if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state1", psEnc);

            silk_encoder_control sEncCtrl = new silk_encoder_control();
            int i, iter, maxIter, found_upper, found_lower, ret = 0;
            Pointer<short> x_frame;
            ec_ctx sRangeEnc_copy = new ec_ctx();
            ec_ctx sRangeEnc_copy2 = new ec_ctx();
            silk_nsq_state sNSQ_copy = new silk_nsq_state();
            silk_nsq_state sNSQ_copy2 = new silk_nsq_state();
            int nBits, nBits_lower, nBits_upper, gainMult_lower, gainMult_upper;
            int gainsID, gainsID_lower, gainsID_upper;
            short gainMult_Q8;
            short ec_prevLagIndex_copy;
            int ec_prevSignalType_copy;
            sbyte LastGainIndex_copy2;
            sbyte seed_copy;

            /* This is totally unnecessary but many compilers (including gcc) are too dumb to realise it */
            LastGainIndex_copy2 = 0;
            nBits_lower = nBits_upper = gainMult_lower = gainMult_upper = 0;

            psEnc.sCmn.indices.Seed = Inlines.CHOP8(psEnc.sCmn.frameCounter++ & 3);

            /**************************************************************/
            /* Set up Input Pointers, and insert frame in input buffer   */
            /*************************************************************/
            /* start of frame to encode */
            x_frame = psEnc.x_buf.Point(psEnc.sCmn.ltp_mem_length);

            /***************************************/
            /* Ensure smooth bandwidth transitions */
            /***************************************/
            Filters.silk_LP_variable_cutoff(psEnc.sCmn.sLP, psEnc.sCmn.inputBuf.Point(1), psEnc.sCmn.frame_length);

            /*******************************************/
            /* Copy new frame to front of input buffer */
            /*******************************************/
            psEnc.sCmn.inputBuf.Point(1).MemCopyTo(x_frame.Point(SilkConstants.LA_SHAPE_MS * psEnc.sCmn.fs_kHz), psEnc.sCmn.frame_length);

            if (psEnc.sCmn.prefillFlag == 0)
            {
                Pointer<int> xfw_Q3;
                Pointer<short> res_pitch;
                Pointer<byte> ec_buf_copy;
                Pointer<short> res_pitch_frame;

                res_pitch = Pointer.Malloc<short>(psEnc.sCmn.la_pitch + psEnc.sCmn.frame_length + psEnc.sCmn.ltp_mem_length);
                /* start of pitch LPC residual frame */
                res_pitch_frame = res_pitch.Point(psEnc.sCmn.ltp_mem_length);

                /*****************************************/
                /* Find pitch lags, initial LPC analysis */
                /*****************************************/
                find_pitch_lags.silk_find_pitch_lags_FIX(psEnc, sEncCtrl, res_pitch, x_frame, psEnc.sCmn.arch);

                if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state2", psEnc);

                /************************/
                /* Noise shape analysis */
                /************************/
                noise_shape_analysis.silk_noise_shape_analysis_FIX(psEnc, sEncCtrl, res_pitch_frame, x_frame, psEnc.sCmn.arch);

                if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state3", psEnc);

                /***************************************************/
                /* Find linear prediction coefficients (LPC + LTP) */
                /***************************************************/
                find_pred_coefs.silk_find_pred_coefs_FIX(psEnc, sEncCtrl, res_pitch, x_frame, condCoding);

                if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state4", psEnc);

                /****************************************/
                /* Process gains                        */
                /****************************************/
                process_gains.silk_process_gains_FIX(psEnc, sEncCtrl, condCoding);

                if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state5", psEnc);

                /*****************************************/
                /* Prefiltering for noise shaper         */
                /*****************************************/
                xfw_Q3 = Pointer.Malloc<int>(psEnc.sCmn.frame_length);
                prefilter.silk_prefilter_FIX(psEnc, sEncCtrl, xfw_Q3, x_frame);

                if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state6", psEnc);

                /****************************************/
                /* Low Bitrate Redundant Encoding       */
                /****************************************/
                silk_LBRR_encode_FIX(psEnc, sEncCtrl, xfw_Q3, condCoding);

                if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state7", psEnc);

                /* Loop over quantizer and entropy coding to control bitrate */
                maxIter = 6;
                gainMult_Q8 = (short)(Inlines.SILK_FIX_CONST(1, 8));
                found_lower = 0;
                found_upper = 0;
                gainsID = GainQuantization.silk_gains_ID(psEnc.sCmn.indices.GainsIndices, psEnc.sCmn.nb_subfr);
                gainsID_lower = -1;
                gainsID_upper = -1;
                /* Copy part of the input state */
                sRangeEnc_copy.Assign(psRangeEnc);
                sNSQ_copy.Assign(psEnc.sCmn.sNSQ);
                seed_copy = psEnc.sCmn.indices.Seed;
                ec_prevLagIndex_copy = psEnc.sCmn.ec_prevLagIndex;
                ec_prevSignalType_copy = psEnc.sCmn.ec_prevSignalType;
                ec_buf_copy = Pointer.Malloc<byte>(1275);
                for (iter = 0; ; iter++)
                {
                    if (gainsID == gainsID_lower)
                    {
                        nBits = nBits_lower;
                    }
                    else if (gainsID == gainsID_upper)
                    {
                        nBits = nBits_upper;
                    }
                    else {
                        /* Restore part of the input state */
                        if (iter > 0)
                        {
                            psRangeEnc.Assign(sRangeEnc_copy);
                            psEnc.sCmn.sNSQ.Assign(sNSQ_copy);
                            psEnc.sCmn.indices.Seed = seed_copy;
                            psEnc.sCmn.ec_prevLagIndex = ec_prevLagIndex_copy;
                            psEnc.sCmn.ec_prevSignalType = ec_prevSignalType_copy;
                        }

                        /*****************************************/
                        /* Noise shaping quantization            */
                        /*****************************************/
                        if (psEnc.sCmn.nStatesDelayedDecision > 1 || psEnc.sCmn.warping_Q16 > 0)
                        {
                            NSQ.silk_NSQ_del_dec_c(psEnc.sCmn, psEnc.sCmn.sNSQ, psEnc.sCmn.indices, xfw_Q3, psEnc.sCmn.pulses,
                                   sEncCtrl.PredCoef_Q12, sEncCtrl.LTPCoef_Q14, sEncCtrl.AR2_Q13, sEncCtrl.HarmShapeGain_Q14,
                                   sEncCtrl.Tilt_Q14, sEncCtrl.LF_shp_Q14, sEncCtrl.Gains_Q16, sEncCtrl.pitchL, sEncCtrl.Lambda_Q10, sEncCtrl.LTP_scale_Q14);
                        }
                        else {
                            NSQ.silk_NSQ_c(psEnc.sCmn, psEnc.sCmn.sNSQ, psEnc.sCmn.indices, xfw_Q3, psEnc.sCmn.pulses,
                                    sEncCtrl.PredCoef_Q12, sEncCtrl.LTPCoef_Q14, sEncCtrl.AR2_Q13, sEncCtrl.HarmShapeGain_Q14,
                                    sEncCtrl.Tilt_Q14, sEncCtrl.LF_shp_Q14, sEncCtrl.Gains_Q16, sEncCtrl.pitchL, sEncCtrl.Lambda_Q10, sEncCtrl.LTP_scale_Q14);
                        }

                        if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state8", psEnc);

                        /****************************************/
                        /* Encode Parameters                    */
                        /****************************************/
                        encode_indices.silk_encode_indices(psEnc.sCmn, psRangeEnc, psEnc.sCmn.nFramesEncoded, 0, condCoding);

                        if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state9", psEnc);

                        /****************************************/
                        /* Encode Excitation Signal             */
                        /****************************************/
                        encode_pulses.silk_encode_pulses(psRangeEnc, psEnc.sCmn.indices.signalType, psEnc.sCmn.indices.quantOffsetType,
                            psEnc.sCmn.pulses, psEnc.sCmn.frame_length);

                        if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state10", psEnc);

                        nBits = EntropyCoder.ec_tell(psRangeEnc);

                        if (useCBR == 0 && iter == 0 && nBits <= maxBits)
                        {
                            break;
                        }
                    }

                    if (iter == maxIter)
                    {
                        if (found_lower != 0 && (gainsID == gainsID_lower || nBits > maxBits))
                        {
                            /* Restore output state from earlier iteration that did meet the bitrate budget */
                            psRangeEnc.Assign(sRangeEnc_copy2);
                            Inlines.OpusAssert(sRangeEnc_copy2.offs <= 1275);
                            ec_buf_copy.MemCopyTo(psRangeEnc.buf, (int)sRangeEnc_copy2.offs);
                            psEnc.sCmn.sNSQ.Assign(sNSQ_copy2);
                            psEnc.sShape.LastGainIndex = LastGainIndex_copy2;
                        }
                        break;
                    }

                    if (nBits > maxBits)
                    {
                        if (found_lower == 0 && iter >= 2)
                        {
                            /* Adjust the quantizer's rate/distortion tradeoff and discard previous "upper" results */
                            sEncCtrl.Lambda_Q10 = Inlines.silk_ADD_RSHIFT32(sEncCtrl.Lambda_Q10, sEncCtrl.Lambda_Q10, 1);
                            found_upper = 0;
                            gainsID_upper = -1;
                        }
                        else {
                            found_upper = 1;
                            nBits_upper = nBits;
                            gainMult_upper = gainMult_Q8;
                            gainsID_upper = gainsID;
                        }
                    }
                    else if (nBits < maxBits - 5)
                    {
                        found_lower = 1;
                        nBits_lower = nBits;
                        gainMult_lower = gainMult_Q8;
                        if (gainsID != gainsID_lower)
                        {
                            gainsID_lower = gainsID;
                            /* Copy part of the output state */
                            sRangeEnc_copy2.Assign(psRangeEnc);
                            Inlines.OpusAssert(psRangeEnc.offs <= 1275);
                            psRangeEnc.buf.MemCopyTo(ec_buf_copy, (int)psRangeEnc.offs);
                            sNSQ_copy2.Assign(psEnc.sCmn.sNSQ);
                            LastGainIndex_copy2 = psEnc.sShape.LastGainIndex;
                        }
                    }
                    else {
                        /* Within 5 bits of budget: close enough */
                        break;
                    }

                    if ((found_lower & found_upper) == 0)
                    {
                        /* Adjust gain according to high-rate rate/distortion curve */
                        int gain_factor_Q16;
                        gain_factor_Q16 = Inlines.silk_log2lin(Inlines.silk_LSHIFT(nBits - maxBits, 7) / psEnc.sCmn.frame_length + Inlines.SILK_FIX_CONST(16, 7));
                        gain_factor_Q16 = Inlines.silk_min_32(gain_factor_Q16, Inlines.SILK_FIX_CONST(2, 16));
                        if (nBits > maxBits)
                        {
                            gain_factor_Q16 = Inlines.silk_max_32(gain_factor_Q16, Inlines.SILK_FIX_CONST(1.3f, 16));
                        }

                        gainMult_Q8 = Inlines.CHOP16(Inlines.silk_SMULWB(gain_factor_Q16, (int)gainMult_Q8));
                    }
                    else
                    {
                        /* Adjust gain by interpolating */
                        gainMult_Q8 = Inlines.CHOP16(gainMult_lower + Inlines.silk_DIV32_16(Inlines.silk_MUL(gainMult_upper - gainMult_lower, maxBits - nBits_lower), nBits_upper - nBits_lower));
                         /* New gain multplier must be between 25% and 75% of old range (note that gainMult_upper < gainMult_lower) */
                        if (gainMult_Q8 > Inlines.silk_ADD_RSHIFT32(gainMult_lower, gainMult_upper - gainMult_lower, 2))
                        {
                            gainMult_Q8 = Inlines.CHOP16(Inlines.silk_ADD_RSHIFT32(gainMult_lower, gainMult_upper - gainMult_lower, 2));
                        }
                        else if (gainMult_Q8 < Inlines.silk_SUB_RSHIFT32(gainMult_upper, gainMult_upper - gainMult_lower, 2))
                        {
                            gainMult_Q8 = Inlines.CHOP16(Inlines.silk_SUB_RSHIFT32(gainMult_upper, gainMult_upper - gainMult_lower, 2));
                        }
                    }

                    for (i = 0; i < psEnc.sCmn.nb_subfr; i++)
                    {
                        sEncCtrl.Gains_Q16[i] = Inlines.silk_LSHIFT_SAT32(Inlines.silk_SMULWB(sEncCtrl.GainsUnq_Q16[i], gainMult_Q8), 8);
                    }

                    /* Quantize gains */
                    psEnc.sShape.LastGainIndex = sEncCtrl.lastGainIndexPrev;
                    BoxedValue<sbyte> boxed_gainIndex = new BoxedValue<sbyte>(psEnc.sShape.LastGainIndex);
                    GainQuantization.silk_gains_quant(psEnc.sCmn.indices.GainsIndices, sEncCtrl.Gains_Q16,
                          boxed_gainIndex, condCoding == SilkConstants.CODE_CONDITIONALLY ? 1 : 0, psEnc.sCmn.nb_subfr);
                    psEnc.sShape.LastGainIndex = boxed_gainIndex.Val;

                    if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state11", psEnc);

                    /* Unique identifier of gains vector */
                    gainsID = GainQuantization.silk_gains_ID(psEnc.sCmn.indices.GainsIndices, psEnc.sCmn.nb_subfr);
                }
            }

            /* Update input buffer */
            psEnc.x_buf.Point(psEnc.sCmn.frame_length).MemMoveTo(psEnc.x_buf, psEnc.sCmn.ltp_mem_length + SilkConstants.LA_SHAPE_MS * psEnc.sCmn.fs_kHz);

            /* Exit without entropy coding */
            if (psEnc.sCmn.prefillFlag != 0)
            {
                /* No payload */
                pnBytesOut.Val = 0;

                return ret;
            }

            /* Parameters needed for next frame */
            psEnc.sCmn.prevLag = sEncCtrl.pitchL[psEnc.sCmn.nb_subfr - 1];
            psEnc.sCmn.prevSignalType = psEnc.sCmn.indices.signalType;

            /****************************************/
            /* Finalize payload                     */
            /****************************************/
            psEnc.sCmn.first_frame_after_reset = 0;
            /* Payload size */
            pnBytesOut.Val = Inlines.silk_RSHIFT(EntropyCoder.ec_tell(psRangeEnc) + 7, 3);

            if (TRACE_FILE) NailTester.NailTesterPrint_silk_encoder_state_FIX("state12", psEnc);
            if (TRACE_FILE) Debug.WriteLine("Exiting silk encode frame");

            return ret;
        }

        /* Low-Bitrate Redundancy (LBRR) encoding. Reuse all parameters but encode excitation at lower bitrate  */
        public static void silk_LBRR_encode_FIX(
            silk_encoder_state_fix psEnc,                                 /* I/O  Pointer to Silk FIX encoder state                                           */
            silk_encoder_control psEncCtrl,                             /* I/O  Pointer to Silk FIX encoder control struct                                  */
            Pointer<int> xfw_Q3,                               /* I    Input signal                                                                */
            int condCoding                              /* I    The type of conditional coding used so far for this frame                   */
        )
        {
            Pointer<int> TempGains_Q16 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
            SideInfoIndices psIndices_LBRR = psEnc.sCmn.indices_LBRR[psEnc.sCmn.nFramesEncoded];
            silk_nsq_state sNSQ_LBRR = new silk_nsq_state();

            /*******************************************/
            /* Control use of inband LBRR              */
            /*******************************************/
            if (psEnc.sCmn.LBRR_enabled != 0 && psEnc.sCmn.speech_activity_Q8 > Inlines.SILK_FIX_CONST(TuningParameters.LBRR_SPEECH_ACTIVITY_THRES, 8))
            {
                psEnc.sCmn.LBRR_flags[psEnc.sCmn.nFramesEncoded] = 1;

                /* Copy noise shaping quantizer state and quantization indices from regular encoding */
                sNSQ_LBRR.Assign(psEnc.sCmn.sNSQ);
                psIndices_LBRR.Assign(psEnc.sCmn.indices);

                /* Save original gains */
                psEncCtrl.Gains_Q16.MemCopyTo(TempGains_Q16, psEnc.sCmn.nb_subfr);

                if (psEnc.sCmn.nFramesEncoded == 0 || psEnc.sCmn.LBRR_flags[psEnc.sCmn.nFramesEncoded - 1] == 0)
                {
                    /* First frame in packet or previous frame not LBRR coded */
                    psEnc.sCmn.LBRRprevLastGainIndex = psEnc.sShape.LastGainIndex;

                    /* Increase Gains to get target LBRR rate */
                    psIndices_LBRR.GainsIndices[0] = Inlines.CHOP8(psIndices_LBRR.GainsIndices[0] + psEnc.sCmn.LBRR_GainIncreases);
                    psIndices_LBRR.GainsIndices[0] = Inlines.CHOP8(Inlines.silk_min_int(psIndices_LBRR.GainsIndices[0], SilkConstants.N_LEVELS_QGAIN - 1));
                }

                /* Decode to get gains in sync with decoder         */
                /* Overwrite unquantized gains with quantized gains */
                BoxedValue<sbyte> boxed_gainIndex = new BoxedValue<sbyte>(psEnc.sCmn.LBRRprevLastGainIndex);
                GainQuantization.silk_gains_dequant(psEncCtrl.Gains_Q16, psIndices_LBRR.GainsIndices,
                    boxed_gainIndex, condCoding == SilkConstants.CODE_CONDITIONALLY ? 1 : 0, psEnc.sCmn.nb_subfr);
                psEnc.sCmn.LBRRprevLastGainIndex = boxed_gainIndex.Val;

                /*****************************************/
                /* Noise shaping quantization            */
                /*****************************************/
                if (psEnc.sCmn.nStatesDelayedDecision > 1 || psEnc.sCmn.warping_Q16 > 0)
                {
                    NSQ.silk_NSQ_del_dec_c(psEnc.sCmn, sNSQ_LBRR, psIndices_LBRR, xfw_Q3,
                        psEnc.sCmn.pulses_LBRR[psEnc.sCmn.nFramesEncoded], psEncCtrl.PredCoef_Q12, psEncCtrl.LTPCoef_Q14,
                        psEncCtrl.AR2_Q13, psEncCtrl.HarmShapeGain_Q14, psEncCtrl.Tilt_Q14, psEncCtrl.LF_shp_Q14,
                        psEncCtrl.Gains_Q16, psEncCtrl.pitchL, psEncCtrl.Lambda_Q10, psEncCtrl.LTP_scale_Q14);
                }
                else {
                    NSQ.silk_NSQ_c(psEnc.sCmn, sNSQ_LBRR, psIndices_LBRR, xfw_Q3,
                        psEnc.sCmn.pulses_LBRR[psEnc.sCmn.nFramesEncoded], psEncCtrl.PredCoef_Q12, psEncCtrl.LTPCoef_Q14,
                        psEncCtrl.AR2_Q13, psEncCtrl.HarmShapeGain_Q14, psEncCtrl.Tilt_Q14, psEncCtrl.LF_shp_Q14,
                        psEncCtrl.Gains_Q16, psEncCtrl.pitchL, psEncCtrl.Lambda_Q10, psEncCtrl.LTP_scale_Q14);
                }

                /* Restore original gains */
                TempGains_Q16.MemCopyTo(psEncCtrl.Gains_Q16, psEnc.sCmn.nb_subfr);
            }
        }

    }
}
