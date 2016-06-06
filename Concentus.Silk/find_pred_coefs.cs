using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class find_pred_coefs
    {
        // fixme: this is a priority for testing

        public static void silk_find_pred_coefs_FIX(
            silk_encoder_state_fix psEnc,                                 /* I/O  encoder state                                                               */
            silk_encoder_control psEncCtrl,                             /* I/O  encoder control                                                             */
            Pointer<short> res_pitch,                            /* I    Residual from pitch analysis                                                */
            Pointer<short> x,                                    /* I    Speech signal                                                               */
            int condCoding                              /* I    The type of conditional coding to use                                       */
        )
        {
            int i;
            Pointer<int> invGains_Q16 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
            Pointer<int> local_gains = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
            Pointer<int> Wght_Q15 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
            Pointer<short> NLSF_Q15 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);
            Pointer<short> x_ptr;
            Pointer<short> x_pre_ptr;
            Pointer<short> LPC_in_pre;
            int tmp, min_gain_Q16, minInvGain_Q30;
            Pointer<int> LTP_corrs_rshift = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);


            /* weighting for weighted least squares */
            min_gain_Q16 = int.MaxValue >> 6;
            for (i = 0; i < psEnc.sCmn.nb_subfr; i++)
            {
                min_gain_Q16 = Inlines.silk_min(min_gain_Q16, psEncCtrl.Gains_Q16[i]);
            }
            for (i = 0; i < psEnc.sCmn.nb_subfr; i++)
            {
                /* Divide to Q16 */
                Debug.Assert(psEncCtrl.Gains_Q16[i] > 0);
                /* Invert and normalize gains, and ensure that maximum invGains_Q16 is within range of a 16 bit int */
                invGains_Q16[i] = Inlines.silk_DIV32_varQ(min_gain_Q16, psEncCtrl.Gains_Q16[i], 16 - 2);

                /* Ensure Wght_Q15 a minimum value 1 */
                invGains_Q16[i] = Inlines.silk_max(invGains_Q16[i], 363);

                /* Square the inverted gains */
                Debug.Assert(invGains_Q16[i] == Inlines.silk_SAT16(invGains_Q16[i]));
                tmp = Inlines.silk_SMULWB(invGains_Q16[i], invGains_Q16[i]);
                Wght_Q15[i] = Inlines.silk_RSHIFT(tmp, 1);

                /* Invert the inverted and normalized gains */
                local_gains[i] = Inlines.silk_DIV32(((int)1 << 16), invGains_Q16[i]);
            }

            LPC_in_pre = Pointer.Malloc<short>(psEnc.sCmn.nb_subfr * psEnc.sCmn.predictLPCOrder + psEnc.sCmn.frame_length);
            if (psEnc.sCmn.indices.signalType == SilkConstants.TYPE_VOICED)
            {
                Pointer<int> WLTP;

                /**********/
                /* VOICED */
                /**********/
                Debug.Assert(psEnc.sCmn.ltp_mem_length - psEnc.sCmn.predictLPCOrder >= psEncCtrl.pitchL[0] + SilkConstants.LTP_ORDER / 2);

                WLTP = Pointer.Malloc<int>(psEnc.sCmn.nb_subfr * SilkConstants.LTP_ORDER * SilkConstants.LTP_ORDER);

                /* LTP analysis */
                BoxedValue<int> boxed_codgain = new BoxedValue<int>(psEncCtrl.LTPredCodGain_Q7);
                find_LTP.silk_find_LTP_FIX(psEncCtrl.LTPCoef_Q14, WLTP, boxed_codgain,
                    res_pitch, psEncCtrl.pitchL, Wght_Q15, psEnc.sCmn.subfr_length,
                    psEnc.sCmn.nb_subfr, psEnc.sCmn.ltp_mem_length, LTP_corrs_rshift, psEnc.sCmn.arch);
                psEncCtrl.LTPredCodGain_Q7 = boxed_codgain.Val;

                /* Quantize LTP gain parameters */
                BoxedValue<sbyte> boxed_periodicity = new BoxedValue<sbyte>(psEnc.sCmn.indices.PERIndex);
                BoxedValue<int> boxed_gain = new BoxedValue<int>(psEnc.sCmn.sum_log_gain_Q7);
                quant_LTP_gains.silk_quant_LTP_gains(psEncCtrl.LTPCoef_Q14, psEnc.sCmn.indices.LTPIndex, boxed_periodicity,
                    boxed_gain, WLTP, psEnc.sCmn.mu_LTP_Q9, psEnc.sCmn.LTPQuantLowComplexity, psEnc.sCmn.nb_subfr,
                    psEnc.sCmn.arch);
                psEnc.sCmn.indices.PERIndex = boxed_periodicity.Val;
                psEnc.sCmn.sum_log_gain_Q7 = boxed_gain.Val;

                /* Control LTP scaling */
                LTP_scale_ctrl.silk_LTP_scale_ctrl_FIX(psEnc, psEncCtrl, condCoding);

                /* Create LTP residual */
                LTP_analysis_filter.silk_LTP_analysis_filter_FIX(LPC_in_pre, x.Point(0 - psEnc.sCmn.predictLPCOrder), psEncCtrl.LTPCoef_Q14,
                    psEncCtrl.pitchL, invGains_Q16, psEnc.sCmn.subfr_length, psEnc.sCmn.nb_subfr, psEnc.sCmn.predictLPCOrder);

            }
            else {
                /************/
                /* UNVOICED */
                /************/
                /* Create signal with prepended subframes, scaled by inverse gains */
                x_ptr = x.Point(0 - psEnc.sCmn.predictLPCOrder);
                x_pre_ptr = LPC_in_pre;
                for (i = 0; i < psEnc.sCmn.nb_subfr; i++)
                {
                    Inlines.silk_scale_copy_vector16(x_pre_ptr, x_ptr, invGains_Q16[i],
                        psEnc.sCmn.subfr_length + psEnc.sCmn.predictLPCOrder);
                    x_pre_ptr = x_pre_ptr.Point(psEnc.sCmn.subfr_length + psEnc.sCmn.predictLPCOrder);
                    x_ptr = x_ptr.Point(psEnc.sCmn.subfr_length);
                }

                psEncCtrl.LTPCoef_Q14.MemSet(0, psEnc.sCmn.nb_subfr * SilkConstants.LTP_ORDER);
                psEncCtrl.LTPredCodGain_Q7 = 0;
                psEnc.sCmn.sum_log_gain_Q7 = 0;
            }

            /* Limit on total predictive coding gain */
            if (psEnc.sCmn.first_frame_after_reset != 0)
            {
                minInvGain_Q30 = Inlines.SILK_FIX_CONST(1.0f / SilkConstants.MAX_PREDICTION_POWER_GAIN_AFTER_RESET, 30);
            }
            else {
                minInvGain_Q30 = Inlines.silk_log2lin(Inlines.silk_SMLAWB(16 << 7, (int)psEncCtrl.LTPredCodGain_Q7, Inlines.SILK_FIX_CONST(1.0f / 3f, 16)));      /* Q16 */
                minInvGain_Q30 = Inlines.silk_DIV32_varQ(minInvGain_Q30,
                    Inlines.silk_SMULWW(Inlines.SILK_FIX_CONST(SilkConstants.MAX_PREDICTION_POWER_GAIN, 0),
                        Inlines.silk_SMLAWB(Inlines.SILK_FIX_CONST(0.25f, 18), Inlines.SILK_FIX_CONST(0.75f, 18), psEncCtrl.coding_quality_Q14)), 14);
            }

            /* LPC_in_pre contains the LTP-filtered input for voiced, and the unfiltered input for unvoiced */
            find_LPC.silk_find_LPC_FIX(psEnc.sCmn, NLSF_Q15, LPC_in_pre, minInvGain_Q30);

            /* Quantize LSFs */
            NLSF.silk_process_NLSFs(psEnc.sCmn, psEncCtrl.PredCoef_Q12, NLSF_Q15, psEnc.sCmn.prev_NLSFq_Q15);

            /* Calculate residual energy using quantized LPC coefficients */
            residual_energy.silk_residual_energy_FIX(psEncCtrl.ResNrg, psEncCtrl.ResNrgQ, LPC_in_pre, psEncCtrl.PredCoef_Q12, local_gains,
                psEnc.sCmn.subfr_length, psEnc.sCmn.nb_subfr, psEnc.sCmn.predictLPCOrder, psEnc.sCmn.arch);

            /* Copy to prediction struct for use in next frame for interpolation */
            NLSF_Q15.MemCopyTo(psEnc.sCmn.prev_NLSFq_Q15, SilkConstants.MAX_LPC_ORDER);

        }
    }
}
