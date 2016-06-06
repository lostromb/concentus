using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class prefilter
    {
        public static void silk_warped_LPC_analysis_filter_FIX_c(
            Pointer<int> state,                    /* I/O  State [order + 1]                   */
            Pointer<int> res_Q2,                   /* O    Residual signal [length]            */
            Pointer<short> coef_Q13,                 /* I    Coefficients [order]                */
            Pointer<short> input,                    /* I    Input signal [length]               */
            short lambda_Q16,                 /* I    Warping factor                      */
            int length,                     /* I    Length of input signal              */
            int order                       /* I    Filter order (even)                 */
        )
        {
            int n, i;
            int acc_Q11, tmp1, tmp2;

            /* Order must be even */
            Inlines.OpusAssert((order & 1) == 0);

            for (n = 0; n < length; n++)
            {
                /* Output of lowpass section */
                tmp2 = Inlines.silk_SMLAWB(state[0], state[1], lambda_Q16);
                state[0] = Inlines.silk_LSHIFT(input[n], 14);
                /* Output of allpass section */
                tmp1 = Inlines.silk_SMLAWB(state[1], state[2] - tmp2, lambda_Q16);
                state[1] = tmp2;
                acc_Q11 = Inlines.silk_RSHIFT(order, 1);
                acc_Q11 = Inlines.silk_SMLAWB(acc_Q11, tmp2, coef_Q13[0]);
                /* Loop over allpass sections */
                for (i = 2; i < order; i += 2)
                {
                    /* Output of allpass section */
                    tmp2 = Inlines.silk_SMLAWB(state[i], state[i + 1] - tmp1, lambda_Q16);
                    state[i] = tmp1;
                    acc_Q11 = Inlines.silk_SMLAWB(acc_Q11, tmp1, coef_Q13[i - 1]);
                    /* Output of allpass section */
                    tmp1 = Inlines.silk_SMLAWB(state[i + 1], state[i + 2] - tmp2, lambda_Q16);
                    state[i + 1] = tmp2;
                    acc_Q11 = Inlines.silk_SMLAWB(acc_Q11, tmp2, coef_Q13[i]);
                }
                state[order] = tmp1;
                acc_Q11 = Inlines.silk_SMLAWB(acc_Q11, tmp1, coef_Q13[order - 1]);
                res_Q2[n] = Inlines.silk_LSHIFT((int)input[n], 2) - Inlines.silk_RSHIFT_ROUND(acc_Q11, 9);
            }
        }

        public static void silk_prefilter_FIX(
            silk_encoder_state_fix psEnc,                                 /* I/O  Encoder state                                                               */
            silk_encoder_control psEncCtrl,                             /* I    Encoder control                                                             */
            Pointer<int> xw_Q3,                                /* O    Weighted signal                                                             */
            Pointer<short> x                                     /* I    Speech signal                                                               */
)
        {
            silk_prefilter_state P = psEnc.sPrefilt;
            int j, k, lag;
            int tmp_32;
            Pointer<short> AR1_shp_Q13;
            Pointer<short> px;
            Pointer<int> pxw_Q3;
            int HarmShapeGain_Q12, Tilt_Q14;
            int HarmShapeFIRPacked_Q12, LF_shp_Q14;
            Pointer<int> x_filt_Q12;
            Pointer<int> st_res_Q2;
            Pointer<short> B_Q10 = Pointer.Malloc<short>(2);
            
            /* Set up pointers */
            px = x;
            pxw_Q3 = xw_Q3;
            lag = P.lagPrev;
            x_filt_Q12 = Pointer.Malloc<int>(psEnc.sCmn.subfr_length);
            st_res_Q2 = Pointer.Malloc<int>(psEnc.sCmn.subfr_length);
            for (k = 0; k < psEnc.sCmn.nb_subfr; k++)
            {
                /* Update Variables that change per sub frame */
                if (psEnc.sCmn.indices.signalType == SilkConstants.TYPE_VOICED)
                {
                    lag = psEncCtrl.pitchL[k];
                }

                /* Noise shape parameters */
                HarmShapeGain_Q12 = Inlines.silk_SMULWB((int)psEncCtrl.HarmShapeGain_Q14[k], 16384 - psEncCtrl.HarmBoost_Q14[k]);
                Inlines.OpusAssert(HarmShapeGain_Q12 >= 0);
                HarmShapeFIRPacked_Q12 = Inlines.silk_RSHIFT(HarmShapeGain_Q12, 2);
                HarmShapeFIRPacked_Q12 |= Inlines.silk_LSHIFT((int)Inlines.silk_RSHIFT(HarmShapeGain_Q12, 1), 16);
                Tilt_Q14 = psEncCtrl.Tilt_Q14[k];
                LF_shp_Q14 = psEncCtrl.LF_shp_Q14[k];
                AR1_shp_Q13 = psEncCtrl.AR1_Q13.Point(k * SilkConstants.MAX_SHAPE_LPC_ORDER);

                /* Short term FIR filtering*/
                silk_warped_LPC_analysis_filter_FIX_c(P.sAR_shp, st_res_Q2, AR1_shp_Q13, px,
                    Inlines.CHOP16(psEnc.sCmn.warping_Q16), psEnc.sCmn.subfr_length, psEnc.sCmn.shapingLPCOrder);

                /* Reduce (mainly) low frequencies during harmonic emphasis */
                B_Q10[0] = Inlines.CHOP16(Inlines.silk_RSHIFT_ROUND(psEncCtrl.GainsPre_Q14[k], 4));
                tmp_32 = Inlines.silk_SMLABB(Inlines.SILK_FIX_CONST(TuningParameters.INPUT_TILT, 26), psEncCtrl.HarmBoost_Q14[k], HarmShapeGain_Q12);   /* Q26 */
                tmp_32 = Inlines.silk_SMLABB(tmp_32, psEncCtrl.coding_quality_Q14, Inlines.SILK_FIX_CONST(TuningParameters.HIGH_RATE_INPUT_TILT, 12));    /* Q26 */
                tmp_32 = Inlines.silk_SMULWB(tmp_32, -psEncCtrl.GainsPre_Q14[k]);                                                /* Q24 */
                tmp_32 = Inlines.silk_RSHIFT_ROUND(tmp_32, 14);                                                                     /* Q10 */
                B_Q10[1] = Inlines.CHOP16(Inlines.silk_SAT16(tmp_32));
                x_filt_Q12[0] = Inlines.silk_MLA(Inlines.silk_MUL(st_res_Q2[0], B_Q10[0]), P.sHarmHP_Q2, B_Q10[1]);
                for (j = 1; j < psEnc.sCmn.subfr_length; j++)
                {
                    x_filt_Q12[j] = Inlines.silk_MLA(Inlines.silk_MUL(st_res_Q2[j], B_Q10[0]), st_res_Q2[j - 1], B_Q10[1]);
                }
                P.sHarmHP_Q2 = st_res_Q2[psEnc.sCmn.subfr_length - 1];

                silk_prefilt_FIX(P, x_filt_Q12, pxw_Q3, HarmShapeFIRPacked_Q12, Tilt_Q14, LF_shp_Q14, lag, psEnc.sCmn.subfr_length);

                px = px.Point(psEnc.sCmn.subfr_length);
                pxw_Q3 = pxw_Q3.Point(psEnc.sCmn.subfr_length);
            }

            P.lagPrev = psEncCtrl.pitchL[psEnc.sCmn.nb_subfr - 1];

        }
        
        /* Prefilter for finding Quantizer input signal */
        static void silk_prefilt_FIX(
            silk_prefilter_state P,                         /* I/O  state                               */
            Pointer<int> st_res_Q12,               /* I    short term residual signal          */
            Pointer<int> xw_Q3,                    /* O    prefiltered signal                  */
            int HarmShapeFIRPacked_Q12,     /* I    Harmonic shaping coeficients        */
            int Tilt_Q14,                   /* I    Tilt shaping coeficient             */
            int LF_shp_Q14,                 /* I    Low-frequancy shaping coeficients   */
            int lag,                        /* I    Lag for harmonic shaping            */
            int length                      /* I    Length of signals                   */
        )
        {
            int i, idx, LTP_shp_buf_idx;
            int n_LTP_Q12, n_Tilt_Q10, n_LF_Q10;
            int sLF_MA_shp_Q12, sLF_AR_shp_Q12;
            Pointer<short> LTP_shp_buf;

            /* To speed up use temp variables instead of using the struct */
            LTP_shp_buf = P.sLTP_shp;
            LTP_shp_buf_idx = P.sLTP_shp_buf_idx;
            sLF_AR_shp_Q12 = P.sLF_AR_shp_Q12;
            sLF_MA_shp_Q12 = P.sLF_MA_shp_Q12;

            for (i = 0; i < length; i++)
            {
                if (lag > 0)
                {
                    /* unrolled loop */
                    Inlines.OpusAssert(SilkConstants.HARM_SHAPE_FIR_TAPS == 3);
                    idx = lag + LTP_shp_buf_idx;
                    n_LTP_Q12 = Inlines.silk_SMULBB(LTP_shp_buf[(idx - SilkConstants.HARM_SHAPE_FIR_TAPS / 2 - 1) & SilkConstants.LTP_MASK], HarmShapeFIRPacked_Q12);
                    n_LTP_Q12 = Inlines.silk_SMLABT(n_LTP_Q12, LTP_shp_buf[(idx - SilkConstants.HARM_SHAPE_FIR_TAPS / 2) & SilkConstants.LTP_MASK], HarmShapeFIRPacked_Q12);
                    n_LTP_Q12 = Inlines.silk_SMLABB(n_LTP_Q12, LTP_shp_buf[(idx - SilkConstants.HARM_SHAPE_FIR_TAPS / 2 + 1) & SilkConstants.LTP_MASK], HarmShapeFIRPacked_Q12);
                }
                else {
                    n_LTP_Q12 = 0;
                }

                n_Tilt_Q10 = Inlines.silk_SMULWB(sLF_AR_shp_Q12, Tilt_Q14);
                n_LF_Q10 = Inlines.silk_SMLAWB(Inlines.silk_SMULWT(sLF_AR_shp_Q12, LF_shp_Q14), sLF_MA_shp_Q12, LF_shp_Q14);

                sLF_AR_shp_Q12 = Inlines.silk_SUB32(st_res_Q12[i], Inlines.silk_LSHIFT(n_Tilt_Q10, 2));
                sLF_MA_shp_Q12 = Inlines.silk_SUB32(sLF_AR_shp_Q12, Inlines.silk_LSHIFT(n_LF_Q10, 2));

                LTP_shp_buf_idx = (LTP_shp_buf_idx - 1) & SilkConstants.LTP_MASK;
                LTP_shp_buf[LTP_shp_buf_idx] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT_ROUND(sLF_MA_shp_Q12, 12));

                xw_Q3[i] = Inlines.silk_RSHIFT_ROUND(Inlines.silk_SUB32(sLF_MA_shp_Q12, n_LTP_Q12), 9);
            }

            /* Copy temp variable back to state */
            P.sLF_AR_shp_Q12 = sLF_AR_shp_Q12;
            P.sLF_MA_shp_Q12 = sLF_MA_shp_Q12;
            P.sLTP_shp_buf_idx = LTP_shp_buf_idx;
        }
    }
}
