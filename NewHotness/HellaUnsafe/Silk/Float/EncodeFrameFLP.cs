/***********************************************************************
Copyright (c) 2006-2011, Skype Limited. All rights reserved.
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
- Redistributions of source code must retain the above copyright notice,
this list of conditions and the following disclaimer.
- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.
- Neither the name of Internet Society, IETF or IETF Trust, nor the
names of specific contributors, may be used to endorse or promote
products derived from this software without specific prior written
permission.
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
***********************************************************************/

using System;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.EncodeIndices;
using static HellaUnsafe.Silk.EncodePulses;
using static HellaUnsafe.Silk.Float.FindPitchLagsFLP;
using static HellaUnsafe.Silk.Float.FindPredCoefsFLP;
using static HellaUnsafe.Silk.Float.NoiseShapeAnalysisFLP;
using static HellaUnsafe.Silk.Float.ProcessGainsFLP;
using static HellaUnsafe.Silk.Float.SigProcFLP;
using static HellaUnsafe.Silk.Float.StructsFLP;
using static HellaUnsafe.Silk.Float.WrappersFLP;
using static HellaUnsafe.Silk.GainQuant;
using static HellaUnsafe.Silk.LPVariableCutoff;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.TuningParameters;
using static HellaUnsafe.Silk.VAD;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class EncodeFrameFLP
    {
        internal static unsafe void silk_encode_do_VAD_FLP(
            silk_encoder_state_FLP* psEnc,                             /* I/O  Encoder state FLP                           */
            int activity                            /* I    Decision of Opus voice activity detector    */
        )
        {
            int activity_threshold = SILK_FIX_CONST(SPEECH_ACTIVITY_DTX_THRES, 8);

            /****************************/
            /* Voice Activity Detection */
            /****************************/
            silk_VAD_GetSA_Q8(&psEnc->sCmn, psEnc->sCmn.inputBuf + 1);
            /* If Opus VAD is inactive and Silk VAD is active: lower Silk VAD to just under the threshold */
            if (activity == VAD_NO_ACTIVITY && psEnc->sCmn.speech_activity_Q8 >= activity_threshold)
            {
                psEnc->sCmn.speech_activity_Q8 = activity_threshold - 1;
            }

            /**************************************************/
            /* Convert speech activity into VAD and DTX flags */
            /**************************************************/
            if (psEnc->sCmn.speech_activity_Q8 < activity_threshold)
            {
                psEnc->sCmn.indices.signalType = TYPE_NO_VOICE_ACTIVITY;
                psEnc->sCmn.noSpeechCounter++;
                if (psEnc->sCmn.noSpeechCounter <= NB_SPEECH_FRAMES_BEFORE_DTX)
                {
                    psEnc->sCmn.inDTX = 0;
                }
                else if (psEnc->sCmn.noSpeechCounter > MAX_CONSECUTIVE_DTX + NB_SPEECH_FRAMES_BEFORE_DTX)
                {
                    psEnc->sCmn.noSpeechCounter = NB_SPEECH_FRAMES_BEFORE_DTX;
                    psEnc->sCmn.inDTX = 0;
                }
                psEnc->sCmn.VAD_flags[psEnc->sCmn.nFramesEncoded] = 0;
            }
            else
            {
                psEnc->sCmn.noSpeechCounter = 0;
                psEnc->sCmn.inDTX = 0;
                psEnc->sCmn.indices.signalType = TYPE_UNVOICED;
                psEnc->sCmn.VAD_flags[psEnc->sCmn.nFramesEncoded] = 1;
            }
        }

        /****************/
        /* Encode frame */
        /****************/
        internal static unsafe int silk_encode_frame_FLP(
            silk_encoder_state_FLP* psEnc,                             /* I/O  Encoder state FLP                           */
            int* pnBytesOut,                        /* O    Number of payload bytes;                    */
            ec_ctx* psRangeEnc,                        /* I/O  compressor data structure                   */
            int condCoding,                         /* I    The type of conditional coding to use       */
            int maxBits,                            /* I    If > 0: maximum number of output bits       */
            int useCBR                              /* I    Flag to force constant-bitrate operation    */
        )
        {
            silk_encoder_control_FLP sEncCtrl;
            int i, iter, maxIter, found_upper, found_lower, ret = 0;
            float* x_frame, res_pitch_frame;
            float* res_pitch = stackalloc float[2 * MAX_FRAME_LENGTH + LA_PITCH_MAX];
            ec_ctx sRangeEnc_copy = default;
            ec_ctx sRangeEnc_copy2 = default;
            silk_nsq_state sNSQ_copy = default;
            silk_nsq_state sNSQ_copy2 = default;
            int seed_copy, nBits, nBits_lower, nBits_upper, gainMult_lower, gainMult_upper;
            int gainsID, gainsID_lower, gainsID_upper;
            short gainMult_Q8;
            short ec_prevLagIndex_copy;
            int ec_prevSignalType_copy;
            sbyte LastGainIndex_copy2;
            int* pGains_Q16 = stackalloc int[MAX_NB_SUBFR];
            byte* ec_buf_copy = stackalloc byte[1275];
            int* gain_lock = stackalloc int[MAX_NB_SUBFR];
            new Span<int>(gain_lock, MAX_NB_SUBFR).Fill(0);
            short* best_gain_mult = stackalloc short[MAX_NB_SUBFR];
            int* best_sum = stackalloc int[MAX_NB_SUBFR];
            int bits_margin;

            /* For CBR, 5 bits below budget is close enough. For VBR, allow up to 25% below the cap if we initially busted the budget. */
            bits_margin = useCBR != 0 ? 5 : maxBits / 4;
            /* This is totally unnecessary but many compilers (including gcc) are too dumb to realise it */
            LastGainIndex_copy2 = 0;
            nBits_lower = 0;
            nBits_upper = 0;
            gainMult_lower = 0;
            gainMult_upper = 0;

            psEnc->sCmn.indices.Seed = (sbyte)(psEnc->sCmn.frameCounter++ & 3);

            /**************************************************************/
            /* Set up Input Pointers, and insert frame in input buffer    */
            /**************************************************************/
            /* pointers aligned with start of frame to encode */
            x_frame = psEnc->x_buf + psEnc->sCmn.ltp_mem_length;    /* start of frame to encode */
            res_pitch_frame = res_pitch + psEnc->sCmn.ltp_mem_length;    /* start of pitch LPC residual frame */

            /***************************************/
            /* Ensure smooth bandwidth transitions */
            /***************************************/
            silk_LP_variable_cutoff(&psEnc->sCmn.sLP, psEnc->sCmn.inputBuf + 1, psEnc->sCmn.frame_length);

            /*******************************************/
            /* Copy new frame to front of input buffer */
            /*******************************************/
            silk_short2float_array(x_frame + LA_SHAPE_MS * psEnc->sCmn.fs_kHz, psEnc->sCmn.inputBuf + 1, psEnc->sCmn.frame_length);

            /* Add tiny signal to avoid high CPU load from denormalized floating point numbers */
            for (i = 0; i < 8; i++)
            {
                x_frame[LA_SHAPE_MS * psEnc->sCmn.fs_kHz + i * (psEnc->sCmn.frame_length >> 3)] += (1 - (i & 2)) * 1e-6f;
            }

            if (psEnc->sCmn.prefillFlag == 0)
            {
                /*****************************************/
                /* Find pitch lags, initial LPC analysis */
                /*****************************************/
                silk_find_pitch_lags_FLP(psEnc, &sEncCtrl, res_pitch, x_frame);

                /************************/
                /* Noise shape analysis */
                /************************/
                silk_noise_shape_analysis_FLP(psEnc, &sEncCtrl, res_pitch_frame, x_frame);

                /***************************************************/
                /* Find linear prediction coefficients (LPC + LTP) */
                /***************************************************/
                silk_find_pred_coefs_FLP(psEnc, &sEncCtrl, res_pitch_frame, x_frame, condCoding);

                /****************************************/
                /* Process gains                        */
                /****************************************/
                silk_process_gains_FLP(psEnc, &sEncCtrl, condCoding);

                /****************************************/
                /* Low Bitrate Redundant Encoding       */
                /****************************************/
                silk_LBRR_encode_FLP(psEnc, &sEncCtrl, x_frame, condCoding);

                /* Loop over quantizer and entroy coding to control bitrate */
                maxIter = 6;
                gainMult_Q8 = (short)SILK_FIX_CONST(1, 8);
                found_lower = 0;
                found_upper = 0;
                gainsID = silk_gains_ID(psEnc->sCmn.indices.GainsIndices, psEnc->sCmn.nb_subfr);
                gainsID_lower = -1;
                gainsID_upper = -1;
                /* Copy part of the input state */
                sRangeEnc_copy = *psRangeEnc; // silk_memcpy(&sRangeEnc_copy, psRangeEnc, sizeof(ec_ctx));
                sNSQ_copy = psEnc->sCmn.sNSQ; // silk_memcpy(&sNSQ_copy, &psEnc->sCmn.sNSQ, sizeof(silk_nsq_state));
                seed_copy = psEnc->sCmn.indices.Seed;
                ec_prevLagIndex_copy = psEnc->sCmn.ec_prevLagIndex;
                ec_prevSignalType_copy = psEnc->sCmn.ec_prevSignalType;
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
                    else
                    {
                        /* Restore part of the input state */
                        if (iter > 0)
                        {
                            *psRangeEnc = sRangeEnc_copy; // silk_memcpy(psRangeEnc, &sRangeEnc_copy, sizeof(ec_ctx));
                            psEnc->sCmn.sNSQ = sNSQ_copy; // silk_memcpy(&psEnc->sCmn.sNSQ, &sNSQ_copy, sizeof(silk_nsq_state));
                            psEnc->sCmn.indices.Seed = (sbyte)seed_copy;
                            psEnc->sCmn.ec_prevLagIndex = ec_prevLagIndex_copy;
                            psEnc->sCmn.ec_prevSignalType = ec_prevSignalType_copy;
                        }

                        /*****************************************/
                        /* Noise shaping quantization            */
                        /*****************************************/
                        silk_NSQ_wrapper_FLP(psEnc, &sEncCtrl, &psEnc->sCmn.indices, &psEnc->sCmn.sNSQ, psEnc->sCmn.pulses, x_frame);

                        if (iter == maxIter && found_lower == 0)
                        {
                            sRangeEnc_copy2 = *psRangeEnc; // silk_memcpy(&sRangeEnc_copy2, psRangeEnc, sizeof(ec_ctx));
                        }

                        /****************************************/
                        /* Encode Parameters                    */
                        /****************************************/
                        silk_encode_indices(&psEnc->sCmn, psRangeEnc, psEnc->sCmn.nFramesEncoded, 0, condCoding);

                        /****************************************/
                        /* Encode Excitation Signal             */
                        /****************************************/
                        silk_encode_pulses(psRangeEnc, psEnc->sCmn.indices.signalType, psEnc->sCmn.indices.quantOffsetType,
                              psEnc->sCmn.pulses, psEnc->sCmn.frame_length);

                        nBits = ec_tell(psRangeEnc);

                        /* If we still bust after the last iteration, do some damage control. */
                        if (iter == maxIter && found_lower == 0 && nBits > maxBits)
                        {
                            *psRangeEnc = sRangeEnc_copy2; // silk_memcpy(psRangeEnc, &sRangeEnc_copy2, sizeof(ec_ctx));

                            /* Keep gains the same as the last frame. */
                            psEnc->sShape.LastGainIndex = sEncCtrl.lastGainIndexPrev;
                            for (i = 0; i < psEnc->sCmn.nb_subfr; i++)
                            {
                                psEnc->sCmn.indices.GainsIndices[i] = 4;
                            }
                            if (condCoding != CODE_CONDITIONALLY)
                            {
                                psEnc->sCmn.indices.GainsIndices[0] = sEncCtrl.lastGainIndexPrev;
                            }
                            psEnc->sCmn.ec_prevLagIndex = ec_prevLagIndex_copy;
                            psEnc->sCmn.ec_prevSignalType = ec_prevSignalType_copy;
                            /* Clear all pulses. */
                            for (i = 0; i < psEnc->sCmn.frame_length; i++)
                            {
                                psEnc->sCmn.pulses[i] = 0;
                            }

                            silk_encode_indices(&psEnc->sCmn, psRangeEnc, psEnc->sCmn.nFramesEncoded, 0, condCoding);

                            silk_encode_pulses(psRangeEnc, psEnc->sCmn.indices.signalType, psEnc->sCmn.indices.quantOffsetType,
                                psEnc->sCmn.pulses, psEnc->sCmn.frame_length);

                            nBits = ec_tell(psRangeEnc);
                        }

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
                            *psRangeEnc = sRangeEnc_copy2; // silk_memcpy(psRangeEnc, &sRangeEnc_copy2, sizeof(ec_enc));
                            celt_assert(sRangeEnc_copy2.offs <= 1275);
                            silk_memcpy(psRangeEnc->buf, ec_buf_copy, (int)sRangeEnc_copy2.offs);
                            psEnc->sCmn.sNSQ = sNSQ_copy2; // silk_memcpy(&psEnc->sCmn.sNSQ, &sNSQ_copy2, sizeof(silk_nsq_state));
                            psEnc->sShape.LastGainIndex = LastGainIndex_copy2;
                        }
                        break;
                    }

                    if (nBits > maxBits)
                    {
                        if (found_lower == 0 && iter >= 2)
                        {
                            /* Adjust the quantizer's rate/distortion tradeoff and discard previous "upper" results */
                            sEncCtrl.Lambda = silk_max_float(sEncCtrl.Lambda * 1.5f, 1.5f);
                            /* Reducing dithering can help us hit the target. */
                            psEnc->sCmn.indices.quantOffsetType = 0;
                            found_upper = 0;
                            gainsID_upper = -1;
                        }
                        else
                        {
                            found_upper = 1;
                            nBits_upper = nBits;
                            gainMult_upper = gainMult_Q8;
                            gainsID_upper = gainsID;
                        }
                    }
                    else if (nBits < maxBits - bits_margin)
                    {
                        found_lower = 1;
                        nBits_lower = nBits;
                        gainMult_lower = gainMult_Q8;
                        if (gainsID != gainsID_lower)
                        {
                            gainsID_lower = gainsID;
                            /* Copy part of the output state */
                            sRangeEnc_copy2 = *psRangeEnc; // silk_memcpy(&sRangeEnc_copy2, psRangeEnc, sizeof(ec_enc));
                            celt_assert(psRangeEnc->offs <= 1275);
                            silk_memcpy(ec_buf_copy, psRangeEnc->buf, (int)psRangeEnc->offs);
                            sNSQ_copy2 = psEnc->sCmn.sNSQ; // silk_memcpy(&sNSQ_copy2, &psEnc->sCmn.sNSQ, sizeof(silk_nsq_state));
                            LastGainIndex_copy2 = psEnc->sShape.LastGainIndex;
                        }
                    }
                    else
                    {
                        /* Close enough */
                        break;
                    }

                    if (found_lower == 0 && nBits > maxBits)
                    {
                        int j;
                        for (i = 0; i < psEnc->sCmn.nb_subfr; i++)
                        {
                            int sum = 0;
                            for (j = i * psEnc->sCmn.subfr_length; j < (i + 1) * psEnc->sCmn.subfr_length; j++)
                            {
                                sum += abs(psEnc->sCmn.pulses[j]);
                            }
                            if (iter == 0 || (sum < best_sum[i] && gain_lock[i] == 0))
                            {
                                best_sum[i] = sum;
                                best_gain_mult[i] = gainMult_Q8;
                            }
                            else
                            {
                                gain_lock[i] = 1;
                            }
                        }
                    }
                    if ((found_lower & found_upper) == 0)
                    {
                        /* Adjust gain according to high-rate rate/distortion curve */
                        if (nBits > maxBits)
                        {
                            gainMult_Q8 = (short)silk_min_32(1024, gainMult_Q8 * 3 / 2);
                        }
                        else
                        {
                            gainMult_Q8 = (short)silk_max_32(64, gainMult_Q8 * 4 / 5);
                        }
                    }
                    else
                    {
                        /* Adjust gain by interpolating */
                        gainMult_Q8 = (short)(gainMult_lower + ((gainMult_upper - gainMult_lower) * (maxBits - nBits_lower)) / (nBits_upper - nBits_lower));
                        /* New gain multplier must be between 25% and 75% of old range (note that gainMult_upper < gainMult_lower) */
                        if (gainMult_Q8 > silk_ADD_RSHIFT32(gainMult_lower, gainMult_upper - gainMult_lower, 2))
                        {
                            gainMult_Q8 = (short)silk_ADD_RSHIFT32(gainMult_lower, gainMult_upper - gainMult_lower, 2);
                        }
                        else
                        if (gainMult_Q8 < silk_SUB_RSHIFT32(gainMult_upper, gainMult_upper - gainMult_lower, 2))
                        {
                            gainMult_Q8 = (short)silk_SUB_RSHIFT32(gainMult_upper, gainMult_upper - gainMult_lower, 2);
                        }
                    }

                    for (i = 0; i < psEnc->sCmn.nb_subfr; i++)
                    {
                        short tmp;
                        if (gain_lock[i] != 0)
                        {
                            tmp = best_gain_mult[i];
                        }
                        else
                        {
                            tmp = gainMult_Q8;
                        }
                        pGains_Q16[i] = silk_LSHIFT_SAT32(silk_SMULWB(sEncCtrl.GainsUnq_Q16[i], tmp), 8);
                    }

                    /* Quantize gains */
                    psEnc->sShape.LastGainIndex = sEncCtrl.lastGainIndexPrev;
                    silk_gains_quant(psEnc->sCmn.indices.GainsIndices, pGains_Q16,
                          &psEnc->sShape.LastGainIndex, BOOL2INT(condCoding == CODE_CONDITIONALLY), psEnc->sCmn.nb_subfr);

                    /* Unique identifier of gains vector */
                    gainsID = silk_gains_ID(psEnc->sCmn.indices.GainsIndices, psEnc->sCmn.nb_subfr);

                    /* Overwrite unquantized gains with quantized gains and convert back to Q0 from Q16 */
                    for (i = 0; i < psEnc->sCmn.nb_subfr; i++)
                    {
                        sEncCtrl.Gains[i] = pGains_Q16[i] / 65536.0f;
                    }
                }
            }

            /* Update input buffer */
            silk_memmove(psEnc->x_buf, &psEnc->x_buf[psEnc->sCmn.frame_length],
                (psEnc->sCmn.ltp_mem_length + LA_SHAPE_MS * psEnc->sCmn.fs_kHz) * sizeof(float));

            /* Exit without entropy coding */
            if (psEnc->sCmn.prefillFlag != 0)
            {
                /* No payload */
                *pnBytesOut = 0;
                return ret;
            }

            /* Parameters needed for next frame */
            psEnc->sCmn.prevLag = sEncCtrl.pitchL[psEnc->sCmn.nb_subfr - 1];
            psEnc->sCmn.prevSignalType = psEnc->sCmn.indices.signalType;

            /****************************************/
            /* Finalize payload                     */
            /****************************************/
            psEnc->sCmn.first_frame_after_reset = 0;
            /* Payload size */
            *pnBytesOut = silk_RSHIFT(ec_tell(psRangeEnc) + 7, 3);

            return ret;
        }

        /* Low-Bitrate Redundancy (LBRR) encoding. Reuse all parameters but encode excitation at lower bitrate  */
        internal static unsafe void silk_LBRR_encode_FLP(
            silk_encoder_state_FLP* psEnc,                             /* I/O  Encoder state FLP                           */
            silk_encoder_control_FLP* psEncCtrl,                         /* I/O  Encoder control FLP                         */
            in float* xfw,                              /* I    Input signal                                */
            int condCoding                          /* I    The type of conditional coding used so far for this frame */
        )
        {
            int k;
            int* Gains_Q16 = stackalloc int[MAX_NB_SUBFR];
            float* TempGains = stackalloc float[MAX_NB_SUBFR];
            SideInfoIndices* psIndices_LBRR = &psEnc->sCmn.indices_LBRR[psEnc->sCmn.nFramesEncoded];
            silk_nsq_state sNSQ_LBRR;

            /*******************************************/
            /* Control use of inband LBRR              */
            /*******************************************/
            if (psEnc->sCmn.LBRR_enabled != 0 && psEnc->sCmn.speech_activity_Q8 > SILK_FIX_CONST(LBRR_SPEECH_ACTIVITY_THRES, 8))
            {
                psEnc->sCmn.LBRR_flags[psEnc->sCmn.nFramesEncoded] = 1;

                /* Copy noise shaping quantizer state and quantization indices from regular encoding */
                sNSQ_LBRR = psEnc->sCmn.sNSQ; // silk_memcpy(&sNSQ_LBRR, &psEnc->sCmn.sNSQ, sizeof(silk_nsq_state));
                *psIndices_LBRR = psEnc->sCmn.indices; // silk_memcpy(psIndices_LBRR, &psEnc->sCmn.indices, sizeof(SideInfoIndices));

                /* Save original gains */
                silk_memcpy(TempGains, psEncCtrl->Gains, psEnc->sCmn.nb_subfr * sizeof(float));

                if (psEnc->sCmn.nFramesEncoded == 0 || psEnc->sCmn.LBRR_flags[psEnc->sCmn.nFramesEncoded - 1] == 0)
                {
                    /* First frame in packet or previous frame not LBRR coded */
                    psEnc->sCmn.LBRRprevLastGainIndex = psEnc->sShape.LastGainIndex;

                    /* Increase Gains to get target LBRR rate */
                    psIndices_LBRR->GainsIndices[0] = (sbyte)(psIndices_LBRR->GainsIndices[0] + psEnc->sCmn.LBRR_GainIncreases);
                    psIndices_LBRR->GainsIndices[0] = (sbyte)silk_min_int(psIndices_LBRR->GainsIndices[0], N_LEVELS_QGAIN - 1);
                }

                /* Decode to get gains in sync with decoder */
                silk_gains_dequant(Gains_Q16, psIndices_LBRR->GainsIndices,
                    &psEnc->sCmn.LBRRprevLastGainIndex, BOOL2INT(condCoding == CODE_CONDITIONALLY), psEnc->sCmn.nb_subfr);

                /* Overwrite unquantized gains with quantized gains and convert back to Q0 from Q16 */
                for (k = 0; k < psEnc->sCmn.nb_subfr; k++)
                {
                    psEncCtrl->Gains[k] = Gains_Q16[k] * (1.0f / 65536.0f);
                }

                /*****************************************/
                /* Noise shaping quantization            */
                /*****************************************/
                silk_NSQ_wrapper_FLP(psEnc, psEncCtrl, psIndices_LBRR, &sNSQ_LBRR,
                    psEnc->sCmn.pulses_LBRR[psEnc->sCmn.nFramesEncoded], xfw);

                /* Restore original gains */
                silk_memcpy(psEncCtrl->Gains, TempGains, psEnc->sCmn.nb_subfr * sizeof(float));
            }
        }
    }
}
