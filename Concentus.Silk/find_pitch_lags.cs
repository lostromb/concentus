using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class find_pitch_lags
    {
        /* Find pitch lags */
        public static void silk_find_pitch_lags_FIX(
            silk_encoder_state_fix psEnc,                                 /* I/O  encoder state                                                               */
            silk_encoder_control psEncCtrl,                             /* I/O  encoder control                                                             */
            Pointer<short> res,                                  /* O    residual                                                                    */
            Pointer<short> x,                                    /* I    Speech signal                                                               */
            int arch                                    /* I    Run-time architecture                                                       */
        )
        {
            int buf_len, i, scale;
            int thrhld_Q13, res_nrg;
            Pointer<short> x_buf, x_buf_ptr;
            Pointer<short> Wsig;
            Pointer<short> Wsig_ptr;
            Pointer<int> auto_corr = Pointer.Malloc<int>(SilkConstants.MAX_FIND_PITCH_LPC_ORDER + 1);
            Pointer<short> rc_Q15 = Pointer.Malloc<short>(SilkConstants.MAX_FIND_PITCH_LPC_ORDER);
            Pointer<int> A_Q24 = Pointer.Malloc<int>(SilkConstants.MAX_FIND_PITCH_LPC_ORDER);
            Pointer<short> A_Q12 = Pointer.Malloc<short>(SilkConstants.MAX_FIND_PITCH_LPC_ORDER);


            /******************************************/
            /* Set up buffer lengths etc based on Fs  */
            /******************************************/
            buf_len = psEnc.sCmn.la_pitch + psEnc.sCmn.frame_length + psEnc.sCmn.ltp_mem_length;

            /* Safety check */
            Debug.Assert(buf_len >= psEnc.sCmn.pitch_LPC_win_length);

            x_buf = x.Point(0 - psEnc.sCmn.ltp_mem_length);

            /*************************************/
            /* Estimate LPC AR coefficients      */
            /*************************************/

            /* Calculate windowed signal */

            Wsig = Pointer.Malloc<short>(psEnc.sCmn.pitch_LPC_win_length);

            /* First LA_LTP samples */
            x_buf_ptr = x_buf.Point(buf_len - psEnc.sCmn.pitch_LPC_win_length);
            Wsig_ptr = Wsig;
            apply_sine_window.silk_apply_sine_window(Wsig_ptr, x_buf_ptr, 1, psEnc.sCmn.la_pitch);

            /* Middle un - windowed samples */
            Wsig_ptr = Wsig_ptr.Point(psEnc.sCmn.la_pitch);
            x_buf_ptr = x_buf_ptr.Point(psEnc.sCmn.la_pitch);
            x_buf_ptr.MemCopyTo(Wsig_ptr, (psEnc.sCmn.pitch_LPC_win_length - Inlines.silk_LSHIFT(psEnc.sCmn.la_pitch, 1)));

            /* Last LA_LTP samples */
            Wsig_ptr = Wsig_ptr.Point(psEnc.sCmn.pitch_LPC_win_length - Inlines.silk_LSHIFT(psEnc.sCmn.la_pitch, 1));
            x_buf_ptr = x_buf_ptr.Point(psEnc.sCmn.pitch_LPC_win_length - Inlines.silk_LSHIFT(psEnc.sCmn.la_pitch, 1));
            apply_sine_window.silk_apply_sine_window(Wsig_ptr, x_buf_ptr, 2, psEnc.sCmn.la_pitch);

            /* Calculate autocorrelation sequence */
            BoxedValue<int> boxed_scale = new BoxedValue<int>();
            autocorr.silk_autocorr(auto_corr, boxed_scale, Wsig, psEnc.sCmn.pitch_LPC_win_length, psEnc.sCmn.pitchEstimationLPCOrder + 1, arch);
            scale = boxed_scale.Val;

            /* Add white noise, as fraction of energy */
            auto_corr[0] = Inlines.silk_SMLAWB(auto_corr[0], auto_corr[0], Inlines.SILK_FIX_CONST(TuningParameters.FIND_PITCH_WHITE_NOISE_FRACTION, 16)) + 1;

            /* Calculate the reflection coefficients using schur */
            res_nrg = schur.silk_schur(rc_Q15, auto_corr, psEnc.sCmn.pitchEstimationLPCOrder);

            /* Prediction gain */
            psEncCtrl.predGain_Q16 = Inlines.silk_DIV32_varQ(auto_corr[0], Inlines.silk_max_int(res_nrg, 1), 16);

            /* Convert reflection coefficients to prediction coefficients */
            k2a.silk_k2a(A_Q24, rc_Q15, psEnc.sCmn.pitchEstimationLPCOrder);

            /* Convert From 32 bit Q24 to 16 bit Q12 coefs */
            for (i = 0; i < psEnc.sCmn.pitchEstimationLPCOrder; i++)
            {
                A_Q12[i] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT(A_Q24[i], 12));
            }

            /* Do BWE */
            bwexpander.silk_bwexpander(A_Q12, psEnc.sCmn.pitchEstimationLPCOrder, Inlines.SILK_FIX_CONST(TuningParameters.FIND_PITCH_BANDWIDTH_EXPANSION, 16));

            /*****************************************/
            /* LPC analysis filtering                */
            /*****************************************/
            Filters.silk_LPC_analysis_filter(res, x_buf, A_Q12, buf_len, psEnc.sCmn.pitchEstimationLPCOrder, psEnc.sCmn.arch);

            if (psEnc.sCmn.indices.signalType != SilkConstants.TYPE_NO_VOICE_ACTIVITY && psEnc.sCmn.first_frame_after_reset == 0)
            {
                /* Threshold for pitch estimator */
                thrhld_Q13 = Inlines.SILK_FIX_CONST(0.6f, 13);
                thrhld_Q13 = Inlines.silk_SMLABB(thrhld_Q13, Inlines.SILK_FIX_CONST(-0.004f, 13), psEnc.sCmn.pitchEstimationLPCOrder);
                thrhld_Q13 = Inlines.silk_SMLAWB(thrhld_Q13, Inlines.SILK_FIX_CONST(-0.1f, 21), psEnc.sCmn.speech_activity_Q8);
                thrhld_Q13 = Inlines.silk_SMLABB(thrhld_Q13, Inlines.SILK_FIX_CONST(-0.15f, 13), Inlines.silk_RSHIFT(psEnc.sCmn.prevSignalType, 1));
                thrhld_Q13 = Inlines.silk_SMLAWB(thrhld_Q13, Inlines.SILK_FIX_CONST(-0.1f, 14), psEnc.sCmn.input_tilt_Q15);
                thrhld_Q13 = Inlines.silk_SAT16(thrhld_Q13);

                /*****************************************/
                /* Call pitch estimator                  */
                /*****************************************/
                BoxedValue<short> boxed_lagIndex = new BoxedValue<short>(psEnc.sCmn.indices.lagIndex);
                BoxedValue<sbyte> boxed_contourIndex = new BoxedValue<sbyte>(psEnc.sCmn.indices.contourIndex);
                BoxedValue<int> boxed_LTPcorr = new BoxedValue<int>(psEnc.LTPCorr_Q15);
                if (pitch_analysis_core.silk_pitch_analysis_core(res, psEncCtrl.pitchL, boxed_lagIndex, boxed_contourIndex,
                        boxed_LTPcorr, psEnc.sCmn.prevLag, psEnc.sCmn.pitchEstimationThreshold_Q16,
                        (int)thrhld_Q13, psEnc.sCmn.fs_kHz, psEnc.sCmn.pitchEstimationComplexity, psEnc.sCmn.nb_subfr,
                        psEnc.sCmn.arch) == 0)
                {
                    psEnc.sCmn.indices.signalType = SilkConstants.TYPE_VOICED;
                }
                else {
                    psEnc.sCmn.indices.signalType = SilkConstants.TYPE_UNVOICED;
                }

                psEnc.sCmn.indices.lagIndex = boxed_lagIndex.Val;
                psEnc.sCmn.indices.contourIndex = boxed_contourIndex.Val;
                psEnc.LTPCorr_Q15 = boxed_LTPcorr.Val;
            }
            else {
                psEncCtrl.pitchL.MemSet(0, SilkConstants.MAX_NB_SUBFR);
                psEnc.sCmn.indices.lagIndex = 0;
                psEnc.sCmn.indices.contourIndex = 0;
                psEnc.LTPCorr_Q15 = 0;
            }

        }
    }
}
