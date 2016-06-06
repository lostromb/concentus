using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    public static class NSQ
    {
        public static void silk_NSQ_c
            (
                silk_encoder_state psEncC,                                    /* I/O  Encoder State                   */
                silk_nsq_state NSQ,                                       /* I/O  NSQ state                       */
                SideInfoIndices psIndices,                                 /* I/O  Quantization Indices            */
                Pointer<int> x_Q3,                                     /* I    Prefiltered input signal        */
                Pointer<sbyte> pulses,                                   /* O    Quantized pulse signal          */
                Pointer<short> PredCoef_Q12,          /* I    Short term prediction coefs [2 * SilkConstants.MAX_LPC_ORDER]    */
                Pointer<short> LTPCoef_Q14,    /* I    Long term prediction coefs [SilkConstants.LTP_ORDER * MAX_NB_SUBFR]     */
                Pointer<short> AR2_Q13, /* I Noise shaping coefs [MAX_NB_SUBFR * SilkConstants.MAX_SHAPE_LPC_ORDER]            */
                Pointer<int> HarmShapeGain_Q14,          /* I    Long term shaping coefs [MAX_NB_SUBFR]        */
                Pointer<int> Tilt_Q14,                   /* I    Spectral tilt [MAX_NB_SUBFR]                  */
                Pointer<int> LF_shp_Q14,                 /* I    Low frequency shaping coefs [MAX_NB_SUBFR]    */
                Pointer<int> Gains_Q16,                  /* I    Quantization step sizes [MAX_NB_SUBFR]        */
                Pointer<int> pitchL,                     /* I    Pitch lags [MAX_NB_SUBFR]                     */
                int Lambda_Q10,                                 /* I    Rate/distortion tradeoff        */
                int LTP_scale_Q14                               /* I    LTP state scaling               */
            )
        {
            int k, lag, start_idx, LSF_interpolation_flag;
            Pointer<short> A_Q12, B_Q14, AR_shp_Q13;
            Pointer<short> pxq;
            Pointer<int> sLTP_Q15;
            Pointer<short> sLTP;
            int HarmShapeFIRPacked_Q14;
            int offset_Q10;
            Pointer<int> x_sc_Q10;

            NSQ.rand_seed = psIndices.Seed;

            /* Set unvoiced lag to the previous one, overwrite later for voiced */
            lag = NSQ.lagPrev;

            Inlines.OpusAssert(NSQ.prev_gain_Q16 != 0);

            offset_Q10 = Tables.silk_Quantization_Offsets_Q10[psIndices.signalType >> 1][psIndices.quantOffsetType];

            if (psIndices.NLSFInterpCoef_Q2 == 4)
            {
                LSF_interpolation_flag = 0;
            }
            else {
                LSF_interpolation_flag = 1;
            }

            sLTP_Q15 = Pointer.Malloc<int>(psEncC.ltp_mem_length + psEncC.frame_length);
            sLTP = Pointer.Malloc<short>(psEncC.ltp_mem_length + psEncC.frame_length);
            x_sc_Q10 = Pointer.Malloc<int>(psEncC.subfr_length);
            /* Set up pointers to start of sub frame */
            NSQ.sLTP_shp_buf_idx = psEncC.ltp_mem_length;
            NSQ.sLTP_buf_idx = psEncC.ltp_mem_length;
            pxq = NSQ.xq.Point(psEncC.ltp_mem_length);
            for (k = 0; k < psEncC.nb_subfr; k++)
            {
                A_Q12 = PredCoef_Q12.Point(((k >> 1) | (1 - LSF_interpolation_flag)) * SilkConstants.MAX_LPC_ORDER);
                B_Q14 = LTPCoef_Q14.Point(k * SilkConstants.LTP_ORDER);
                AR_shp_Q13 = AR2_Q13.Point(k * SilkConstants.MAX_SHAPE_LPC_ORDER);

                /* Noise shape parameters */
                Inlines.OpusAssert(HarmShapeGain_Q14[k] >= 0);
                HarmShapeFIRPacked_Q14 = Inlines.silk_RSHIFT(HarmShapeGain_Q14[k], 2);
                HarmShapeFIRPacked_Q14 |= Inlines.silk_LSHIFT((int)Inlines.silk_RSHIFT(HarmShapeGain_Q14[k], 1), 16);

                NSQ.rewhite_flag = 0;
                if (psIndices.signalType == SilkConstants.TYPE_VOICED)
                {
                    /* Voiced */
                    lag = pitchL[k];

                    /* Re-whitening */
                    if ((k & (3 - Inlines.silk_LSHIFT(LSF_interpolation_flag, 1))) == 0)
                    {
                        /* Rewhiten with new A coefs */
                        start_idx = psEncC.ltp_mem_length - lag - psEncC.predictLPCOrder - SilkConstants.LTP_ORDER / 2;
                        Inlines.OpusAssert(start_idx > 0);

                        Filters.silk_LPC_analysis_filter(sLTP.Point(start_idx), NSQ.xq.Point(start_idx + k * psEncC.subfr_length),
                            A_Q12, psEncC.ltp_mem_length - start_idx, psEncC.predictLPCOrder, psEncC.arch);

                        NSQ.rewhite_flag = 1;
                        NSQ.sLTP_buf_idx = psEncC.ltp_mem_length;
                    }
                }

                silk_nsq_scale_states(psEncC, NSQ, x_Q3, x_sc_Q10, sLTP, sLTP_Q15, k, LTP_scale_Q14, Gains_Q16, pitchL, psIndices.signalType);

                silk_noise_shape_quantizer(NSQ, psIndices.signalType, x_sc_Q10, pulses, pxq, sLTP_Q15, A_Q12, B_Q14,
                    AR_shp_Q13, lag, HarmShapeFIRPacked_Q14, Tilt_Q14[k], LF_shp_Q14[k], Gains_Q16[k], Lambda_Q10,
                    offset_Q10, psEncC.subfr_length, psEncC.shapingLPCOrder, psEncC.predictLPCOrder);

                x_Q3 = x_Q3.Point(psEncC.subfr_length);
                pulses = pulses.Point(psEncC.subfr_length);
                pxq = pxq.Point(psEncC.subfr_length);
            }

            /* Update lagPrev for next frame */
            NSQ.lagPrev = pitchL[psEncC.nb_subfr - 1];

            /* Save quantized speech and noise shaping signals */
            /* DEBUG_STORE_DATA( enc.pcm, &NSQ.xq[ psEncC.ltp_mem_length ], psEncC.frame_length * sizeof( short ) ) */
            // silk_memmove(NSQ.xq, &NSQ.xq[psEncC.frame_length], psEncC.ltp_mem_length * sizeof(short));
            NSQ.xq.Point(psEncC.frame_length).MemMove(0 - psEncC.frame_length, psEncC.frame_length);

            // silk_memmove(NSQ.sLTP_shp_Q14, &NSQ.sLTP_shp_Q14[psEncC.frame_length], psEncC.ltp_mem_length * sizeof(int));
            NSQ.sLTP_shp_Q14.Point(psEncC.frame_length).MemMove(0 - psEncC.frame_length, psEncC.ltp_mem_length);
        }

        /***********************************/
        /* silk_noise_shape_quantizer  */
        /***********************************/
        public static void silk_noise_shape_quantizer(
                silk_nsq_state NSQ_state,                   /* I/O  NSQ state                       */
                int signalType,             /* I    Signal type                     */
                Pointer<int> x_sc_Q10,             /* I [length]                                   */
                Pointer<sbyte> pulses,               /* O [length]                                    */
                Pointer<short> xq,                   /* O [length]                                    */
                Pointer<int> sLTP_Q15,             /* I/O  LTP state                       */
                Pointer<short> a_Q12,                /* I    Short term prediction coefs     */
                Pointer<short> b_Q14,                /* I    Long term prediction coefs      */
                Pointer<short> AR_shp_Q13,           /* I    Noise shaping AR coefs          */
                int lag,                    /* I    Pitch lag                       */
                int HarmShapeFIRPacked_Q14, /* I                                    */
                int Tilt_Q14,               /* I    Spectral tilt                   */
                int LF_shp_Q14,             /* I                                    */
                int Gain_Q16,               /* I                                    */
                int Lambda_Q10,             /* I                                    */
                int offset_Q10,             /* I                                    */
                int length,                 /* I    Input length                    */
                int shapingLPCOrder,        /* I    Noise shaping AR filter order   */
                int predictLPCOrder         /* I    Prediction filter order         */
            )
        {
            

            int i, j;
            int LTP_pred_Q13, LPC_pred_Q10, n_AR_Q12, n_LTP_Q13;
            int n_LF_Q12, r_Q10, rr_Q10, q1_Q0, q1_Q10, q2_Q10, rd1_Q20, rd2_Q20;
            int exc_Q14, LPC_exc_Q14, xq_Q14, Gain_Q10;
            int tmp1, tmp2, sLF_AR_shp_Q14;
            Pointer<int> psLPC_Q14, shp_lag_ptr, pred_lag_ptr;

            shp_lag_ptr = NSQ_state.sLTP_shp_Q14.Point(NSQ_state.sLTP_shp_buf_idx - lag + SilkConstants.HARM_SHAPE_FIR_TAPS / 2);
            pred_lag_ptr = sLTP_Q15.Point(NSQ_state.sLTP_buf_idx - lag + SilkConstants.LTP_ORDER / 2);
            Gain_Q10 = Inlines.silk_RSHIFT(Gain_Q16, 6);

            /* Set up short term AR state */
            psLPC_Q14 = NSQ_state.sLPC_Q14.Point(SilkConstants.NSQ_LPC_BUF_LENGTH - 1);

            for (i = 0; i < length; i++)
            {
                /* Generate dither */
                NSQ_state.rand_seed = Inlines.silk_RAND(NSQ_state.rand_seed);

                /* Short-term prediction */
                Inlines.OpusAssert(predictLPCOrder == 10 || predictLPCOrder == 16);
                /* Avoids introducing a bias because Inlines.silk_SMLAWB() always rounds to -inf */
                LPC_pred_Q10 = Inlines.silk_RSHIFT(predictLPCOrder, 1);
                LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[0], a_Q12[0]);
                LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-1], a_Q12[1]);
                LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-2], a_Q12[2]);
                LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-3], a_Q12[3]);
                LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-4], a_Q12[4]);
                LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-5], a_Q12[5]);
                LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-6], a_Q12[6]);
                LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-7], a_Q12[7]);
                LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-8], a_Q12[8]);
                LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-9], a_Q12[9]);
                if (predictLPCOrder == 16)
                {
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-10], a_Q12[10]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-11], a_Q12[11]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-12], a_Q12[12]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-13], a_Q12[13]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-14], a_Q12[14]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, psLPC_Q14[-15], a_Q12[15]);
                }

                /* Long-term prediction */
                if (signalType == SilkConstants.TYPE_VOICED)
                {
                    /* Unrolled loop */
                    /* Avoids introducing a bias because Inlines.silk_SMLAWB() always rounds to -inf */
                    LTP_pred_Q13 = 2;
                    LTP_pred_Q13 = Inlines.silk_SMLAWB(LTP_pred_Q13, pred_lag_ptr[0], b_Q14[0]);
                    LTP_pred_Q13 = Inlines.silk_SMLAWB(LTP_pred_Q13, pred_lag_ptr[-1], b_Q14[1]);
                    LTP_pred_Q13 = Inlines.silk_SMLAWB(LTP_pred_Q13, pred_lag_ptr[-2], b_Q14[2]);
                    LTP_pred_Q13 = Inlines.silk_SMLAWB(LTP_pred_Q13, pred_lag_ptr[-3], b_Q14[3]);
                    LTP_pred_Q13 = Inlines.silk_SMLAWB(LTP_pred_Q13, pred_lag_ptr[-4], b_Q14[4]);
                    pred_lag_ptr = pred_lag_ptr.Point(1); ;
                }
                else {
                    LTP_pred_Q13 = 0;
                }

                /* Noise shape feedback */
                Inlines.OpusAssert((shapingLPCOrder & 1) == 0);   /* check that order is even */
                tmp2 = psLPC_Q14[0];
                tmp1 = NSQ_state.sAR2_Q14[0];
                NSQ_state.sAR2_Q14[0] = tmp2;
                n_AR_Q12 = Inlines.silk_RSHIFT(shapingLPCOrder, 1);
                n_AR_Q12 = Inlines.silk_SMLAWB(n_AR_Q12, tmp2, AR_shp_Q13[0]);
                for (j = 2; j < shapingLPCOrder; j += 2)
                {
                    tmp2 = NSQ_state.sAR2_Q14[j - 1];
                    NSQ_state.sAR2_Q14[j - 1] = tmp1;
                    n_AR_Q12 = Inlines.silk_SMLAWB(n_AR_Q12, tmp1, AR_shp_Q13[j - 1]);
                    tmp1 = NSQ_state.sAR2_Q14[j + 0];
                    NSQ_state.sAR2_Q14[j + 0] = tmp2;
                    n_AR_Q12 = Inlines.silk_SMLAWB(n_AR_Q12, tmp2, AR_shp_Q13[j]);
                }
                NSQ_state.sAR2_Q14[shapingLPCOrder - 1] = tmp1;
                n_AR_Q12 = Inlines.silk_SMLAWB(n_AR_Q12, tmp1, AR_shp_Q13[shapingLPCOrder - 1]);

                n_AR_Q12 = Inlines.silk_LSHIFT32(n_AR_Q12, 1);                                /* Q11 . Q12 */
                n_AR_Q12 = Inlines.silk_SMLAWB(n_AR_Q12, NSQ_state.sLF_AR_shp_Q14, Tilt_Q14);

                n_LF_Q12 = Inlines.silk_SMULWB(NSQ_state.sLTP_shp_Q14[NSQ_state.sLTP_shp_buf_idx - 1], LF_shp_Q14);
                n_LF_Q12 = Inlines.silk_SMLAWT(n_LF_Q12, NSQ_state.sLF_AR_shp_Q14, LF_shp_Q14);
                
                Inlines.OpusAssert(lag > 0 || signalType != SilkConstants.TYPE_VOICED);

                /* Combine prediction and noise shaping signals */
                tmp1 = Inlines.silk_SUB32(Inlines.silk_LSHIFT32(LPC_pred_Q10, 2), n_AR_Q12);        /* Q12 */
                tmp1 = Inlines.silk_SUB32(tmp1, n_LF_Q12);                                    /* Q12 */
                if (lag > 0)
                {
                    /* Symmetric, packed FIR coefficients */
                    n_LTP_Q13 = Inlines.silk_SMULWB(Inlines.silk_ADD32(shp_lag_ptr[0], shp_lag_ptr[-2]), HarmShapeFIRPacked_Q14);
                    n_LTP_Q13 = Inlines.silk_SMLAWT(n_LTP_Q13, shp_lag_ptr[-1], HarmShapeFIRPacked_Q14);
                    n_LTP_Q13 = Inlines.silk_LSHIFT(n_LTP_Q13, 1);
                    shp_lag_ptr = shp_lag_ptr.Point(1);

                    tmp2 = Inlines.silk_SUB32(LTP_pred_Q13, n_LTP_Q13);                       /* Q13 */
                    tmp1 = Inlines.silk_ADD_LSHIFT32(tmp2, tmp1, 1);                          /* Q13 */
                    tmp1 = Inlines.silk_RSHIFT_ROUND(tmp1, 3);                                /* Q10 */
                }
                else {
                    tmp1 = Inlines.silk_RSHIFT_ROUND(tmp1, 2);                                /* Q10 */
                }

                r_Q10 = Inlines.silk_SUB32(x_sc_Q10[i], tmp1);                              /* residual error Q10 */

                /* Flip sign depending on dither */
                if (NSQ_state.rand_seed < 0)
                {
                    r_Q10 = -r_Q10;
                }
                r_Q10 = Inlines.silk_LIMIT_32(r_Q10, -(31 << 10), 30 << 10);

                /* Find two quantization level candidates and measure their rate-distortion */
                q1_Q10 = Inlines.silk_SUB32(r_Q10, offset_Q10);
                q1_Q0 = Inlines.silk_RSHIFT(q1_Q10, 10);
                if (q1_Q0 > 0)
                {
                    q1_Q10 = Inlines.silk_SUB32(Inlines.silk_LSHIFT(q1_Q0, 10), SilkConstants.QUANT_LEVEL_ADJUST_Q10);
                    q1_Q10 = Inlines.silk_ADD32(q1_Q10, offset_Q10);
                    q2_Q10 = Inlines.silk_ADD32(q1_Q10, 1024);
                    rd1_Q20 = Inlines.silk_SMULBB(q1_Q10, Lambda_Q10);
                    rd2_Q20 = Inlines.silk_SMULBB(q2_Q10, Lambda_Q10);
                }
                else if (q1_Q0 == 0)
                {
                    q1_Q10 = offset_Q10;
                    q2_Q10 = Inlines.silk_ADD32(q1_Q10, 1024 - SilkConstants.QUANT_LEVEL_ADJUST_Q10);
                    rd1_Q20 = Inlines.silk_SMULBB(q1_Q10, Lambda_Q10);
                    rd2_Q20 = Inlines.silk_SMULBB(q2_Q10, Lambda_Q10);
                }
                else if (q1_Q0 == -1)
                {
                    q2_Q10 = offset_Q10;
                    q1_Q10 = Inlines.silk_SUB32(q2_Q10, 1024 - SilkConstants.QUANT_LEVEL_ADJUST_Q10);
                    rd1_Q20 = Inlines.silk_SMULBB(-q1_Q10, Lambda_Q10);
                    rd2_Q20 = Inlines.silk_SMULBB(q2_Q10, Lambda_Q10);
                }
                else {            /* Q1_Q0 < -1 */
                    q1_Q10 = Inlines.silk_ADD32(Inlines.silk_LSHIFT(q1_Q0, 10), SilkConstants.QUANT_LEVEL_ADJUST_Q10);
                    q1_Q10 = Inlines.silk_ADD32(q1_Q10, offset_Q10);
                    q2_Q10 = Inlines.silk_ADD32(q1_Q10, 1024);
                    rd1_Q20 = Inlines.silk_SMULBB(-q1_Q10, Lambda_Q10);
                    rd2_Q20 = Inlines.silk_SMULBB(-q2_Q10, Lambda_Q10);
                }
                rr_Q10 = Inlines.silk_SUB32(r_Q10, q1_Q10);
                rd1_Q20 = Inlines.silk_SMLABB(rd1_Q20, rr_Q10, rr_Q10);
                rr_Q10 = Inlines.silk_SUB32(r_Q10, q2_Q10);
                rd2_Q20 = Inlines.silk_SMLABB(rd2_Q20, rr_Q10, rr_Q10);

                if (rd2_Q20 < rd1_Q20)
                {
                    q1_Q10 = q2_Q10;
                }

                pulses[i] = (sbyte)Inlines.silk_RSHIFT_ROUND(q1_Q10, 10);

                /* Excitation */
                exc_Q14 = Inlines.silk_LSHIFT(q1_Q10, 4);
                if (NSQ_state.rand_seed < 0)
                {
                    exc_Q14 = -exc_Q14;
                }

                /* Add predictions */
                LPC_exc_Q14 = Inlines.silk_ADD_LSHIFT32(exc_Q14, LTP_pred_Q13, 1);
                xq_Q14 = Inlines.silk_ADD_LSHIFT32(LPC_exc_Q14, LPC_pred_Q10, 4);

                /* Scale XQ back to normal level before saving */
                xq[i] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT_ROUND(Inlines.silk_SMULWW(xq_Q14, Gain_Q10), 8));

                /* Update states */
                psLPC_Q14 = psLPC_Q14.Point(1);
                psLPC_Q14[0] = xq_Q14;
                sLF_AR_shp_Q14 = Inlines.silk_SUB_LSHIFT32(xq_Q14, n_AR_Q12, 2);
                NSQ_state.sLF_AR_shp_Q14 = sLF_AR_shp_Q14;

                NSQ_state.sLTP_shp_Q14[NSQ_state.sLTP_shp_buf_idx] = Inlines.silk_SUB_LSHIFT32(sLF_AR_shp_Q14, n_LF_Q12, 2);
                sLTP_Q15[NSQ_state.sLTP_buf_idx] = Inlines.silk_LSHIFT(LPC_exc_Q14, 1);
                NSQ_state.sLTP_shp_buf_idx++;
                NSQ_state.sLTP_buf_idx++;

                /* Make dither dependent on quantized signal */
                NSQ_state.rand_seed = Inlines.silk_ADD32_ovflw(NSQ_state.rand_seed, pulses[i]);
            }

            /* Update LPC synth buffer */
            NSQ_state.sLPC_Q14.Point(length).MemCopyTo(NSQ_state.sLPC_Q14, SilkConstants.NSQ_LPC_BUF_LENGTH);
        }

        public static void silk_nsq_scale_states(
                silk_encoder_state psEncC,           /* I    Encoder State                   */
                silk_nsq_state NSQ,                   /* I/O  NSQ state                       */
                Pointer<int> x_Q3,                 /* I    input in Q3                     */
                Pointer<int> x_sc_Q10,             /* O    input scaled with 1/Gain        */
                Pointer<short> sLTP,                 /* I    re-whitened LTP state in Q0     */
                Pointer<int> sLTP_Q15,             /* O    LTP state matching scaled input */
                int subfr,                  /* I    subframe number                 */
                int LTP_scale_Q14,          /* I                                    */
                Pointer<int> Gains_Q16, /* I [MAX_NB_SUBFR]                                */
                Pointer<int> pitchL, /* I    Pitch lag [MAX_NB_SUBFR]                      */
                int signal_type             /* I    Signal type                     */
            )
        {
            int i, lag;
            int gain_adj_Q16, inv_gain_Q31, inv_gain_Q23;

            lag = pitchL[subfr];
            inv_gain_Q31 = Inlines.silk_INVERSE32_varQ(Inlines.silk_max(Gains_Q16[subfr], 1), 47);
            Inlines.OpusAssert(inv_gain_Q31 != 0);

            /* Calculate gain adjustment factor */
            if (Gains_Q16[subfr] != NSQ.prev_gain_Q16)
            {
                gain_adj_Q16 = Inlines.silk_DIV32_varQ(NSQ.prev_gain_Q16, Gains_Q16[subfr], 16);
            }
            else {
                gain_adj_Q16 = (int)1 << 16;
            }

            /* Scale input */
            inv_gain_Q23 = Inlines.silk_RSHIFT_ROUND(inv_gain_Q31, 8);
            for (i = 0; i < psEncC.subfr_length; i++)
            {
                x_sc_Q10[i] = Inlines.silk_SMULWW(x_Q3[i], inv_gain_Q23);
            }

            /* Save inverse gain */
            NSQ.prev_gain_Q16 = Gains_Q16[subfr];

            /* After rewhitening the LTP state is un-scaled, so scale with inv_gain_Q16 */
            if (NSQ.rewhite_flag != 0)
            {
                if (subfr == 0)
                {
                    /* Do LTP downscaling */
                    inv_gain_Q31 = Inlines.silk_LSHIFT(Inlines.silk_SMULWB(inv_gain_Q31, LTP_scale_Q14), 2);
                }
                for (i = NSQ.sLTP_buf_idx - lag - SilkConstants.LTP_ORDER / 2; i < NSQ.sLTP_buf_idx; i++)
                {
                    Inlines.OpusAssert(i < SilkConstants.MAX_FRAME_LENGTH);
                    sLTP_Q15[i] = Inlines.silk_SMULWB(inv_gain_Q31, sLTP[i]);
                }
            }

            /* Adjust for changing gain */
            if (gain_adj_Q16 != (int)1 << 16)
            {
                /* Scale long-term shaping state */
                for (i = NSQ.sLTP_shp_buf_idx - psEncC.ltp_mem_length; i < NSQ.sLTP_shp_buf_idx; i++)
                {
                    NSQ.sLTP_shp_Q14[i] = Inlines.silk_SMULWW(gain_adj_Q16, NSQ.sLTP_shp_Q14[i]);
                }

                /* Scale long-term prediction state */
                if (signal_type == SilkConstants.TYPE_VOICED && NSQ.rewhite_flag == 0)
                {
                    for (i = NSQ.sLTP_buf_idx - lag - SilkConstants.LTP_ORDER / 2; i < NSQ.sLTP_buf_idx; i++)
                    {
                        sLTP_Q15[i] = Inlines.silk_SMULWW(gain_adj_Q16, sLTP_Q15[i]);
                    }
                }

                NSQ.sLF_AR_shp_Q14 = Inlines.silk_SMULWW(gain_adj_Q16, NSQ.sLF_AR_shp_Q14);

                /* Scale short-term prediction and shaping states */
                for (i = 0; i < SilkConstants.NSQ_LPC_BUF_LENGTH; i++)
                {
                    NSQ.sLPC_Q14[i] = Inlines.silk_SMULWW(gain_adj_Q16, NSQ.sLPC_Q14[i]);
                }
                for (i = 0; i < SilkConstants.MAX_SHAPE_LPC_ORDER; i++)
                {
                    NSQ.sAR2_Q14[i] = Inlines.silk_SMULWW(gain_adj_Q16, NSQ.sAR2_Q14[i]);
                }
            }
        }

        public static void silk_NSQ_del_dec_c(
            silk_encoder_state psEncC,                                    /* I  Encoder State                   */
            silk_nsq_state NSQ,                                       /* I/O  NSQ state                       */
            SideInfoIndices psIndices,                                 /* I/O  Quantization Indices            */
            Pointer<int> x_Q3,                                     /* I    Prefiltered input signal        */
            Pointer<sbyte> pulses,                                   /* O    Quantized pulse signal          */
            Pointer<short> PredCoef_Q12,          /* I    Short term prediction coefs [2 * MAX_LPC_ORDER]    */
            Pointer<short> LTPCoef_Q14,    /* I    Long term prediction coefs LTP_ORDER * MAX_NB_SUBFR]     */
            Pointer<short> AR2_Q13, /* I Noise shaping coefs  [MAX_NB_SUBFR * MAX_SHAPE_LPC_ORDER]           */
            Pointer<int> HarmShapeGain_Q14,          /* I    Long term shaping coefs [MAX_NB_SUBFR]        */
            Pointer<int> Tilt_Q14,                   /* I    Spectral tilt [MAX_NB_SUBFR]                  */
            Pointer<int> LF_shp_Q14,                 /* I    Low frequency shaping coefs [MAX_NB_SUBFR]    */
            Pointer<int> Gains_Q16,                  /* I    Quantization step sizes [MAX_NB_SUBFR]        */
            Pointer<int> pitchL,                     /* I    Pitch lags  [MAX_NB_SUBFR]                    */
            int Lambda_Q10,                                 /* I    Rate/distortion tradeoff        */
            int LTP_scale_Q14                               /* I    LTP state scaling               */
        )
        {
            int i, k, lag, start_idx, LSF_interpolation_flag, Winner_ind, subfr;
            int last_smple_idx, smpl_buf_idx, decisionDelay;
            Pointer<short> A_Q12, B_Q14, AR_shp_Q13;
            Pointer<short> pxq;
            Pointer<int> sLTP_Q15;
            Pointer<short> sLTP;
            int HarmShapeFIRPacked_Q14;
            int offset_Q10;
            int RDmin_Q10, Gain_Q10;
            Pointer<int> x_sc_Q10;
            Pointer<int> delayedGain_Q10;
            NSQ_del_dec_struct[] psDelDec;
            NSQ_del_dec_struct psDD;

            /* Set unvoiced lag to the previous one, overwrite later for voiced */
            lag = NSQ.lagPrev;

            Inlines.OpusAssert(NSQ.prev_gain_Q16 != 0);

            /* Initialize delayed decision states */
            psDelDec = new NSQ_del_dec_struct[psEncC.nStatesDelayedDecision];
            // silk_memset(psDelDec, 0, psEncC.nStatesDelayedDecision * sizeof(NSQ_del_dec_struct));
            for (int c = 0; c < psEncC.nStatesDelayedDecision; c++)
            {
                psDelDec[c] = new NSQ_del_dec_struct();
                psDelDec[c].Reset();
            }

            for (k = 0; k < psEncC.nStatesDelayedDecision; k++)
            {
                psDD = psDelDec[k];
                psDD.Seed = (k + psIndices.Seed) & 3;
                psDD.SeedInit = psDD.Seed;
                psDD.RD_Q10 = 0;
                psDD.LF_AR_Q14 = NSQ.sLF_AR_shp_Q14;
                psDD.Shape_Q14[0] = NSQ.sLTP_shp_Q14[psEncC.ltp_mem_length - 1];
                NSQ.sLPC_Q14.MemCopyTo(psDD.sLPC_Q14, SilkConstants.NSQ_LPC_BUF_LENGTH);
                NSQ.sAR2_Q14.MemCopyTo(psDD.sAR2_Q14, SilkConstants.MAX_SHAPE_LPC_ORDER);
            }

            offset_Q10 = Tables.silk_Quantization_Offsets_Q10[psIndices.signalType >> 1][psIndices.quantOffsetType];
            smpl_buf_idx = 0; /* index of oldest samples */

            decisionDelay = Inlines.silk_min_int(SilkConstants.DECISION_DELAY, psEncC.subfr_length);

            /* For voiced frames limit the decision delay to lower than the pitch lag */
            if (psIndices.signalType == SilkConstants.TYPE_VOICED)
            {
                for (k = 0; k < psEncC.nb_subfr; k++)
                {
                    decisionDelay = Inlines.silk_min_int(decisionDelay, pitchL[k] - SilkConstants.LTP_ORDER / 2 - 1);
                }
            }
            else {
                if (lag > 0)
                {
                    decisionDelay = Inlines.silk_min_int(decisionDelay, lag - SilkConstants.LTP_ORDER / 2 - 1);
                }
            }

            if (psIndices.NLSFInterpCoef_Q2 == 4)
            {
                LSF_interpolation_flag = 0;
            }
            else {
                LSF_interpolation_flag = 1;
            }

            sLTP_Q15 = Pointer.Malloc<int>(psEncC.ltp_mem_length + psEncC.frame_length);
            sLTP = Pointer.Malloc<short>(psEncC.ltp_mem_length + psEncC.frame_length);
            x_sc_Q10 = Pointer.Malloc<int>(psEncC.subfr_length);
            delayedGain_Q10 = Pointer.Malloc<int>(SilkConstants.DECISION_DELAY);

            /* Set up pointers to start of sub frame */
            pxq = NSQ.xq.Point(psEncC.ltp_mem_length);
            NSQ.sLTP_shp_buf_idx = psEncC.ltp_mem_length;
            NSQ.sLTP_buf_idx = psEncC.ltp_mem_length;
            subfr = 0;
            for (k = 0; k < psEncC.nb_subfr; k++)
            {
                A_Q12 = PredCoef_Q12.Point(((k >> 1) | (1 - LSF_interpolation_flag)) * SilkConstants.MAX_LPC_ORDER);
                B_Q14 = LTPCoef_Q14.Point(k * SilkConstants.LTP_ORDER);
                AR_shp_Q13 = AR2_Q13.Point(k * SilkConstants.MAX_SHAPE_LPC_ORDER);

                /* Noise shape parameters */
                Inlines.OpusAssert(HarmShapeGain_Q14[k] >= 0);
                HarmShapeFIRPacked_Q14 = Inlines.silk_RSHIFT(HarmShapeGain_Q14[k], 2);
                HarmShapeFIRPacked_Q14 |= Inlines.silk_LSHIFT((int)Inlines.silk_RSHIFT(HarmShapeGain_Q14[k], 1), 16);

                NSQ.rewhite_flag = 0;
                if (psIndices.signalType == SilkConstants.TYPE_VOICED)
                {
                    /* Voiced */
                    lag = pitchL[k];

                    /* Re-whitening */
                    if ((k & (3 - Inlines.silk_LSHIFT(LSF_interpolation_flag, 1))) == 0)
                    {
                        if (k == 2)
                        {
                            /* RESET DELAYED DECISIONS */
                            /* Find winner */
                            RDmin_Q10 = psDelDec[0].RD_Q10;
                            Winner_ind = 0;
                            for (i = 1; i < psEncC.nStatesDelayedDecision; i++)
                            {
                                if (psDelDec[i].RD_Q10 < RDmin_Q10)
                                {
                                    RDmin_Q10 = psDelDec[i].RD_Q10;
                                    Winner_ind = i;
                                }
                            }
                            for (i = 0; i < psEncC.nStatesDelayedDecision; i++)
                            {
                                if (i != Winner_ind)
                                {
                                    psDelDec[i].RD_Q10 += (int.MaxValue >> 4);
                                    Inlines.OpusAssert(psDelDec[i].RD_Q10 >= 0);
                                }
                            }

                            /* Copy final part of signals from winner state to output and long-term filter states */
                            psDD = psDelDec[Winner_ind];
                            last_smple_idx = smpl_buf_idx + decisionDelay;
                            for (i = 0; i < decisionDelay; i++)
                            {
                                last_smple_idx = (last_smple_idx - 1) & SilkConstants.DECISION_DELAY_MASK;
                                pulses[i - decisionDelay] = (sbyte)Inlines.silk_RSHIFT_ROUND(psDD.Q_Q10[last_smple_idx], 10);
                                pxq[i - decisionDelay] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT_ROUND(
                                                            Inlines.silk_SMULWW(psDD.Xq_Q14[last_smple_idx], Gains_Q16[1]), 14));
                                NSQ.sLTP_shp_Q14[NSQ.sLTP_shp_buf_idx - decisionDelay + i] = psDD.Shape_Q14[last_smple_idx];
                            }

                            subfr = 0;
                        }

                        /* Rewhiten with new A coefs */
                        start_idx = psEncC.ltp_mem_length - lag - psEncC.predictLPCOrder - SilkConstants.LTP_ORDER / 2;
                        Inlines.OpusAssert(start_idx > 0);

                        Filters.silk_LPC_analysis_filter(sLTP.Point(start_idx), NSQ.xq.Point(start_idx + k * psEncC.subfr_length),
                            A_Q12, psEncC.ltp_mem_length - start_idx, psEncC.predictLPCOrder, psEncC.arch);

                        NSQ.sLTP_buf_idx = psEncC.ltp_mem_length;
                        NSQ.rewhite_flag = 1;
                    }
                }

                silk_nsq_del_dec_scale_states(psEncC, NSQ, psDelDec, x_Q3, x_sc_Q10, sLTP, sLTP_Q15, k,
                    psEncC.nStatesDelayedDecision, LTP_scale_Q14, Gains_Q16, pitchL, psIndices.signalType, decisionDelay);

                BoxedValue<int> smpl_buf_idx_boxed = new BoxedValue<int>(smpl_buf_idx);
                silk_noise_shape_quantizer_del_dec(NSQ, psDelDec, psIndices.signalType, x_sc_Q10, pulses, pxq, sLTP_Q15,
                    delayedGain_Q10, A_Q12, B_Q14, AR_shp_Q13, lag, HarmShapeFIRPacked_Q14, Tilt_Q14[k], LF_shp_Q14[k],
                    Gains_Q16[k], Lambda_Q10, offset_Q10, psEncC.subfr_length, subfr++, psEncC.shapingLPCOrder,
                    psEncC.predictLPCOrder, psEncC.warping_Q16, psEncC.nStatesDelayedDecision, smpl_buf_idx_boxed, decisionDelay);
                smpl_buf_idx = smpl_buf_idx_boxed.Val;

                x_Q3 = x_Q3.Point(psEncC.subfr_length);
                pulses = pulses.Point(psEncC.subfr_length);
                pxq = pxq.Point(psEncC.subfr_length);
            }

            /* Find winner */
            RDmin_Q10 = psDelDec[0].RD_Q10;
            Winner_ind = 0;
            for (k = 1; k < psEncC.nStatesDelayedDecision; k++)
            {
                if (psDelDec[k].RD_Q10 < RDmin_Q10)
                {
                    RDmin_Q10 = psDelDec[k].RD_Q10;
                    Winner_ind = k;
                }
            }

            /* Copy final part of signals from winner state to output and long-term filter states */
            psDD = psDelDec[Winner_ind];
            psIndices.Seed = Inlines.CHOP8(psDD.SeedInit);
            last_smple_idx = smpl_buf_idx + decisionDelay;
            Gain_Q10 = Inlines.silk_RSHIFT32(Gains_Q16[psEncC.nb_subfr - 1], 6);
            for (i = 0; i < decisionDelay; i++)
            {
                last_smple_idx = (last_smple_idx - 1) & SilkConstants.DECISION_DELAY_MASK;
                pulses[i - decisionDelay] = (sbyte)Inlines.silk_RSHIFT_ROUND(psDD.Q_Q10[last_smple_idx], 10);
                pxq[i - decisionDelay] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT_ROUND(
                            Inlines.silk_SMULWW(psDD.Xq_Q14[last_smple_idx], Gain_Q10), 8));
                NSQ.sLTP_shp_Q14[NSQ.sLTP_shp_buf_idx - decisionDelay + i] = psDD.Shape_Q14[last_smple_idx];
            }
            psDD.sLPC_Q14.Point(psEncC.subfr_length).MemCopyTo(NSQ.sLPC_Q14, SilkConstants.NSQ_LPC_BUF_LENGTH);
            psDD.sAR2_Q14.MemCopyTo(NSQ.sAR2_Q14, SilkConstants.MAX_SHAPE_LPC_ORDER);

            /* Update states */
            NSQ.sLF_AR_shp_Q14 = psDD.LF_AR_Q14;
            NSQ.lagPrev = pitchL[psEncC.nb_subfr - 1];

            /* Save quantized speech signal */
            /* DEBUG_STORE_DATA( enc.pcm, &NSQ.xq[psEncC.ltp_mem_length], psEncC.frame_length * sizeof( short ) ) */
            // silk_memmove(NSQ.xq, &NSQ.xq[psEncC.frame_length], psEncC.ltp_mem_length * sizeof(short));
            NSQ.xq.Point(psEncC.frame_length).MemMove(0 - psEncC.frame_length, psEncC.ltp_mem_length);
            // silk_memmove(NSQ.sLTP_shp_Q14, &NSQ.sLTP_shp_Q14[psEncC.frame_length], psEncC.ltp_mem_length * sizeof(int));
            NSQ.sLTP_shp_Q14.Point(psEncC.frame_length).MemMove(0 - psEncC.frame_length, psEncC.ltp_mem_length);
        }

        /******************************************/
        /* Noise shape quantizer for one subframe */
        /******************************************/
        private static void silk_noise_shape_quantizer_del_dec(
            silk_nsq_state NSQ,                   /* I/O  NSQ state                           */
            NSQ_del_dec_struct[] psDelDec,             /* I/O  Delayed decision states             */
            int signalType,             /* I    Signal type                         */
            Pointer<int> x_Q10,                /* I                                        */
            Pointer<sbyte> pulses,               /* O                                        */
            Pointer<short> xq,                   /* O                                        */
            Pointer<int> sLTP_Q15,             /* I/O  LTP filter state                    */
            Pointer<int> delayedGain_Q10,      /* I/O  Gain delay buffer                   */
            Pointer<short> a_Q12,                /* I    Short term prediction coefs         */
            Pointer<short> b_Q14,                /* I    Long term prediction coefs          */
            Pointer<short> AR_shp_Q13,           /* I    Noise shaping coefs                 */
            int lag,                    /* I    Pitch lag                           */
            int HarmShapeFIRPacked_Q14, /* I                                        */
            int Tilt_Q14,               /* I    Spectral tilt                       */
            int LF_shp_Q14,             /* I                                        */
            int Gain_Q16,               /* I                                        */
            int Lambda_Q10,             /* I                                        */
            int offset_Q10,             /* I                                        */
            int length,                 /* I    Input length                        */
            int subfr,                  /* I    Subframe number                     */
            int shapingLPCOrder,        /* I    Shaping LPC filter order            */
            int predictLPCOrder,        /* I    Prediction filter order             */
            int warping_Q16,            /* I                                        */
            int nStatesDelayedDecision, /* I    Number of states in decision tree   */
            BoxedValue<int> smpl_buf_idx,          /* I    Index to newest samples in buffers  */
            int decisionDelay           /* I                                        */
        )
        {
            int i, j, k, Winner_ind, RDmin_ind, RDmax_ind, last_smple_idx;
            int Winner_rand_state;
            int LTP_pred_Q14, LPC_pred_Q14, n_AR_Q14, n_LTP_Q14;
            int n_LF_Q14, r_Q10, rr_Q10, rd1_Q10, rd2_Q10, RDmin_Q10, RDmax_Q10;
            int q1_Q0, q1_Q10, q2_Q10, exc_Q14, LPC_exc_Q14, xq_Q14, Gain_Q10;
            int tmp1, tmp2, sLF_AR_shp_Q14;
            Pointer<int> pred_lag_ptr, shp_lag_ptr, psLPC_Q14;
            NSQ_sample_pair[] psSampleState;
            NSQ_del_dec_struct psDD;
            NSQ_sample_pair psSS;

            Inlines.OpusAssert(nStatesDelayedDecision > 0);
            psSampleState = new NSQ_sample_pair[nStatesDelayedDecision];
            // [porting note] structs must be initialized manually here
            for (int c = 0; c < nStatesDelayedDecision; c++)
            {
                psSampleState[c] = new NSQ_sample_pair();
            }

            shp_lag_ptr = NSQ.sLTP_shp_Q14.Point(NSQ.sLTP_shp_buf_idx - lag + SilkConstants.HARM_SHAPE_FIR_TAPS / 2);
            pred_lag_ptr = sLTP_Q15.Point(NSQ.sLTP_buf_idx - lag + SilkConstants.LTP_ORDER / 2);
            Gain_Q10 = Inlines.silk_RSHIFT(Gain_Q16, 6);

            for (i = 0; i < length; i++)
            {
                /* Perform common calculations used in all states */

                /* Long-term prediction */
                if (signalType == SilkConstants.TYPE_VOICED)
                {
                    /* Unrolled loop */
                    /* Avoids introducing a bias because Inlines.silk_SMLAWB() always rounds to -inf */
                    LTP_pred_Q14 = 2;
                    LTP_pred_Q14 = Inlines.silk_SMLAWB(LTP_pred_Q14, pred_lag_ptr[0], b_Q14[0]);
                    LTP_pred_Q14 = Inlines.silk_SMLAWB(LTP_pred_Q14, pred_lag_ptr[-1], b_Q14[1]);
                    LTP_pred_Q14 = Inlines.silk_SMLAWB(LTP_pred_Q14, pred_lag_ptr[-2], b_Q14[2]);
                    LTP_pred_Q14 = Inlines.silk_SMLAWB(LTP_pred_Q14, pred_lag_ptr[-3], b_Q14[3]);
                    LTP_pred_Q14 = Inlines.silk_SMLAWB(LTP_pred_Q14, pred_lag_ptr[-4], b_Q14[4]);
                    LTP_pred_Q14 = Inlines.silk_LSHIFT(LTP_pred_Q14, 1);                          /* Q13 . Q14 */
                    pred_lag_ptr = pred_lag_ptr.Point(1);
                }
                else {
                    LTP_pred_Q14 = 0;
                }

                /* Long-term shaping */
                if (lag > 0)
                {
                    /* Symmetric, packed FIR coefficients */
                    n_LTP_Q14 = Inlines.silk_SMULWB(Inlines.silk_ADD32(shp_lag_ptr[0], shp_lag_ptr[-2]), HarmShapeFIRPacked_Q14);
                    n_LTP_Q14 = Inlines.silk_SMLAWT(n_LTP_Q14, shp_lag_ptr[-1], HarmShapeFIRPacked_Q14);
                    n_LTP_Q14 = Inlines.silk_SUB_LSHIFT32(LTP_pred_Q14, n_LTP_Q14, 2);            /* Q12 . Q14 */
                    shp_lag_ptr = shp_lag_ptr.Point(1);
                }
                else {
                    n_LTP_Q14 = 0;
                }

                for (k = 0; k < nStatesDelayedDecision; k++)
                {
                    /* Delayed decision state */
                    psDD = psDelDec[k];

                    /* Sample state */
                    psSS = psSampleState[k];

                    /* Generate dither */
                    psDD.Seed = Inlines.silk_RAND(psDD.Seed);

                    /* Pointer used in short term prediction and shaping */
                    psLPC_Q14 = psDD.sLPC_Q14.Point(SilkConstants.NSQ_LPC_BUF_LENGTH - 1 + i);
                    /* Short-term prediction */
                    Inlines.OpusAssert(predictLPCOrder == 10 || predictLPCOrder == 16);
                    /* Avoids introducing a bias because Inlines.silk_SMLAWB() always rounds to -inf */
                    LPC_pred_Q14 = Inlines.silk_RSHIFT(predictLPCOrder, 1);
                    LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[0], a_Q12[0]);
                    LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-1], a_Q12[1]);
                    LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-2], a_Q12[2]);
                    LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-3], a_Q12[3]);
                    LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-4], a_Q12[4]);
                    LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-5], a_Q12[5]);
                    LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-6], a_Q12[6]);
                    LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-7], a_Q12[7]);
                    LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-8], a_Q12[8]);
                    LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-9], a_Q12[9]);
                    if (predictLPCOrder == 16)
                    {
                        LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-10], a_Q12[10]);
                        LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-11], a_Q12[11]);
                        LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-12], a_Q12[12]);
                        LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-13], a_Q12[13]);
                        LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-14], a_Q12[14]);
                        LPC_pred_Q14 = Inlines.silk_SMLAWB(LPC_pred_Q14, psLPC_Q14[-15], a_Q12[15]);
                    }
                    LPC_pred_Q14 = Inlines.silk_LSHIFT(LPC_pred_Q14, 4);                              /* Q10 . Q14 */
                    

                    /* Noise shape feedback */
                    Inlines.OpusAssert((shapingLPCOrder & 1) == 0);   /* check that order is even */
                                                                /* Output of lowpass section */
                    tmp2 = Inlines.silk_SMLAWB(psLPC_Q14[0], psDD.sAR2_Q14[0], warping_Q16);
                    /* Output of allpass section */
                    tmp1 = Inlines.silk_SMLAWB(psDD.sAR2_Q14[0], psDD.sAR2_Q14[1] - tmp2, warping_Q16);
                    psDD.sAR2_Q14[0] = tmp2;
                    n_AR_Q14 = Inlines.silk_RSHIFT(shapingLPCOrder, 1);
                    n_AR_Q14 = Inlines.silk_SMLAWB(n_AR_Q14, tmp2, AR_shp_Q13[0]);
                    /* Loop over allpass sections */
                    for (j = 2; j < shapingLPCOrder; j += 2)
                    {
                        /* Output of allpass section */
                        tmp2 = Inlines.silk_SMLAWB(psDD.sAR2_Q14[j - 1], psDD.sAR2_Q14[j + 0] - tmp1, warping_Q16);
                        psDD.sAR2_Q14[j - 1] = tmp1;
                        n_AR_Q14 = Inlines.silk_SMLAWB(n_AR_Q14, tmp1, AR_shp_Q13[j - 1]);
                        /* Output of allpass section */
                        tmp1 = Inlines.silk_SMLAWB(psDD.sAR2_Q14[j + 0], psDD.sAR2_Q14[j + 1] - tmp2, warping_Q16);
                        psDD.sAR2_Q14[j + 0] = tmp2;
                        n_AR_Q14 = Inlines.silk_SMLAWB(n_AR_Q14, tmp2, AR_shp_Q13[j]);
                    }
                    psDD.sAR2_Q14[shapingLPCOrder - 1] = tmp1;
                    n_AR_Q14 = Inlines.silk_SMLAWB(n_AR_Q14, tmp1, AR_shp_Q13[shapingLPCOrder - 1]);

                    n_AR_Q14 = Inlines.silk_LSHIFT(n_AR_Q14, 1);                                      /* Q11 . Q12 */
                    n_AR_Q14 = Inlines.silk_SMLAWB(n_AR_Q14, psDD.LF_AR_Q14, Tilt_Q14);              /* Q12 */
                    n_AR_Q14 = Inlines.silk_LSHIFT(n_AR_Q14, 2);                                      /* Q12 . Q14 */

                    n_LF_Q14 = Inlines.silk_SMULWB(psDD.Shape_Q14[smpl_buf_idx.Val], LF_shp_Q14);     /* Q12 */
                    n_LF_Q14 = Inlines.silk_SMLAWT(n_LF_Q14, psDD.LF_AR_Q14, LF_shp_Q14);            /* Q12 */
                    n_LF_Q14 = Inlines.silk_LSHIFT(n_LF_Q14, 2);                                      /* Q12 . Q14 */

                    /* Input minus prediction plus noise feedback                       */
                    /* r = x[ i ] - LTP_pred - LPC_pred + n_AR + n_Tilt + n_LF + n_LTP  */
                    tmp1 = Inlines.silk_ADD32(n_AR_Q14, n_LF_Q14);                                    /* Q14 */
                    tmp2 = Inlines.silk_ADD32(n_LTP_Q14, LPC_pred_Q14);                               /* Q13 */
                    tmp1 = Inlines.silk_SUB32(tmp2, tmp1);                                            /* Q13 */
                    tmp1 = Inlines.silk_RSHIFT_ROUND(tmp1, 4);                                        /* Q10 */

                    r_Q10 = Inlines.silk_SUB32(x_Q10[i], tmp1);                                     /* residual error Q10 */

                    /* Flip sign depending on dither */
                    if (psDD.Seed < 0)
                    {
                        r_Q10 = -r_Q10;
                    }
                    r_Q10 = Inlines.silk_LIMIT_32(r_Q10, -(31 << 10), 30 << 10);

                    /* Find two quantization level candidates and measure their rate-distortion */
                    q1_Q10 = Inlines.silk_SUB32(r_Q10, offset_Q10);
                    q1_Q0 = Inlines.silk_RSHIFT(q1_Q10, 10);
                    if (q1_Q0 > 0)
                    {
                        q1_Q10 = Inlines.silk_SUB32(Inlines.silk_LSHIFT(q1_Q0, 10), SilkConstants.QUANT_LEVEL_ADJUST_Q10);
                        q1_Q10 = Inlines.silk_ADD32(q1_Q10, offset_Q10);
                        q2_Q10 = Inlines.silk_ADD32(q1_Q10, 1024);
                        rd1_Q10 = Inlines.silk_SMULBB(q1_Q10, Lambda_Q10);
                        rd2_Q10 = Inlines.silk_SMULBB(q2_Q10, Lambda_Q10);
                    }
                    else if (q1_Q0 == 0)
                    {
                        q1_Q10 = offset_Q10;
                        q2_Q10 = Inlines.silk_ADD32(q1_Q10, 1024 - SilkConstants.QUANT_LEVEL_ADJUST_Q10);
                        rd1_Q10 = Inlines.silk_SMULBB(q1_Q10, Lambda_Q10);
                        rd2_Q10 = Inlines.silk_SMULBB(q2_Q10, Lambda_Q10);
                    }
                    else if (q1_Q0 == -1)
                    {
                        q2_Q10 = offset_Q10;
                        q1_Q10 = Inlines.silk_SUB32(q2_Q10, 1024 - SilkConstants.QUANT_LEVEL_ADJUST_Q10);
                        rd1_Q10 = Inlines.silk_SMULBB(-q1_Q10, Lambda_Q10);
                        rd2_Q10 = Inlines.silk_SMULBB(q2_Q10, Lambda_Q10);
                    }
                    else {            /* q1_Q0 < -1 */
                        q1_Q10 = Inlines.silk_ADD32(Inlines.silk_LSHIFT(q1_Q0, 10), SilkConstants.QUANT_LEVEL_ADJUST_Q10);
                        q1_Q10 = Inlines.silk_ADD32(q1_Q10, offset_Q10);
                        q2_Q10 = Inlines.silk_ADD32(q1_Q10, 1024);
                        rd1_Q10 = Inlines.silk_SMULBB(-q1_Q10, Lambda_Q10);
                        rd2_Q10 = Inlines.silk_SMULBB(-q2_Q10, Lambda_Q10);
                    }
                    rr_Q10 = Inlines.silk_SUB32(r_Q10, q1_Q10);
                    rd1_Q10 = Inlines.silk_RSHIFT(Inlines.silk_SMLABB(rd1_Q10, rr_Q10, rr_Q10), 10);
                    rr_Q10 = Inlines.silk_SUB32(r_Q10, q2_Q10);
                    rd2_Q10 = Inlines.silk_RSHIFT(Inlines.silk_SMLABB(rd2_Q10, rr_Q10, rr_Q10), 10);

                    if (rd1_Q10 < rd2_Q10)
                    {
                        psSS.left.RD_Q10 = Inlines.silk_ADD32(psDD.RD_Q10, rd1_Q10);
                        psSS.right.RD_Q10 = Inlines.silk_ADD32(psDD.RD_Q10, rd2_Q10);
                        psSS.left.Q_Q10 = q1_Q10;
                        psSS.right.Q_Q10 = q2_Q10;
                    }
                    else {
                        psSS.left.RD_Q10 = Inlines.silk_ADD32(psDD.RD_Q10, rd2_Q10);
                        psSS.right.RD_Q10 = Inlines.silk_ADD32(psDD.RD_Q10, rd1_Q10);
                        psSS.left.Q_Q10 = q2_Q10;
                        psSS.right.Q_Q10 = q1_Q10;
                    }

                    /* Update states for best quantization */

                    /* Quantized excitation */
                    exc_Q14 = Inlines.silk_LSHIFT32(psSS.left.Q_Q10, 4);
                    if (psDD.Seed < 0)
                    {
                        exc_Q14 = -exc_Q14;
                    }

                    /* Add predictions */
                    LPC_exc_Q14 = Inlines.silk_ADD32(exc_Q14, LTP_pred_Q14);
                    xq_Q14 = Inlines.silk_ADD32(LPC_exc_Q14, LPC_pred_Q14);

                    /* Update states */
                    sLF_AR_shp_Q14 = Inlines.silk_SUB32(xq_Q14, n_AR_Q14);
                    psSS.left.sLTP_shp_Q14 = Inlines.silk_SUB32(sLF_AR_shp_Q14, n_LF_Q14);
                    psSS.left.LF_AR_Q14 = sLF_AR_shp_Q14;
                    psSS.left.LPC_exc_Q14 = LPC_exc_Q14;
                    psSS.left.xq_Q14 = xq_Q14;

                    /* Update states for second best quantization */

                    /* Quantized excitation */
                    exc_Q14 = Inlines.silk_LSHIFT32(psSS.right.Q_Q10, 4);
                    if (psDD.Seed < 0)
                    {
                        exc_Q14 = -exc_Q14;
                    }


                    /* Add predictions */
                    LPC_exc_Q14 = Inlines.silk_ADD32(exc_Q14, LTP_pred_Q14);
                    xq_Q14 = Inlines.silk_ADD32(LPC_exc_Q14, LPC_pred_Q14);

                    /* Update states */
                    sLF_AR_shp_Q14 = Inlines.silk_SUB32(xq_Q14, n_AR_Q14);
                    psSS.right.sLTP_shp_Q14 = Inlines.silk_SUB32(sLF_AR_shp_Q14, n_LF_Q14);
                    psSS.right.LF_AR_Q14 = sLF_AR_shp_Q14;
                    psSS.right.LPC_exc_Q14 = LPC_exc_Q14;
                    psSS.right.xq_Q14 = xq_Q14;
                }

                smpl_buf_idx.Val = (smpl_buf_idx.Val - 1) & SilkConstants.DECISION_DELAY_MASK;                   /* Index to newest samples              */
                last_smple_idx = (smpl_buf_idx.Val + decisionDelay) & SilkConstants.DECISION_DELAY_MASK;       /* Index to decisionDelay old samples   */

                /* Find winner */
                RDmin_Q10 = psSampleState[0].left.RD_Q10;
                Winner_ind = 0;
                for (k = 1; k < nStatesDelayedDecision; k++)
                {
                    if (psSampleState[k].left.RD_Q10 < RDmin_Q10)
                    {
                        RDmin_Q10 = psSampleState[k].left.RD_Q10;
                        Winner_ind = k;
                    }
                }

                /* Increase RD values of expired states */
                Winner_rand_state = psDelDec[Winner_ind].RandState[last_smple_idx];
                for (k = 0; k < nStatesDelayedDecision; k++)
                {
                    if (psDelDec[k].RandState[last_smple_idx] != Winner_rand_state)
                    {
                        psSampleState[k].left.RD_Q10 = Inlines.silk_ADD32(psSampleState[k].left.RD_Q10, int.MaxValue >> 4);
                        psSampleState[k].right.RD_Q10 = Inlines.silk_ADD32(psSampleState[k].right.RD_Q10, int.MaxValue >> 4);
                        Inlines.OpusAssert(psSampleState[k].left.RD_Q10 >= 0);
                    }
                }

                /* Find worst in first set and best in second set */
                RDmax_Q10 = psSampleState[0].left.RD_Q10;
                RDmin_Q10 = psSampleState[0].right.RD_Q10;
                RDmax_ind = 0;
                RDmin_ind = 0;
                for (k = 1; k < nStatesDelayedDecision; k++)
                {
                    /* find worst in first set */
                    if (psSampleState[k].left.RD_Q10 > RDmax_Q10)
                    {
                        RDmax_Q10 = psSampleState[k].left.RD_Q10;
                        RDmax_ind = k;
                    }
                    /* find best in second set */
                    if (psSampleState[k].right.RD_Q10 < RDmin_Q10)
                    {
                        RDmin_Q10 = psSampleState[k].right.RD_Q10;
                        RDmin_ind = k;
                    }
                }

                /* Replace a state if best from second set outperforms worst in first set */
                if (RDmin_Q10 < RDmax_Q10)
                {
                    // FIXME this original code only copied the last portion of the struct over starting at (i * 4 bytes). Verify that still works
                    //silk_memcpy(((int*)&psDelDec[RDmax_ind]) + i, ((int*)&psDelDec[RDmin_ind]) + i, sizeof(NSQ_del_dec_struct) - i * sizeof(int));
                    psDelDec[RDmax_ind].PartialCopyFrom(psDelDec[RDmin_ind], i);

                    //silk_memcpy(&psSampleState[RDmax_ind][0], &psSampleState[RDmin_ind][1], sizeof(NSQ_sample_struct));
                    psSampleState[RDmax_ind].left.Assign(psSampleState[RDmin_ind].right);
                }

                /* Write samples from winner to output and long-term filter states */
                psDD = psDelDec[Winner_ind];
                if (subfr > 0 || i >= decisionDelay)
                {
                    pulses[i - decisionDelay] = (sbyte)Inlines.silk_RSHIFT_ROUND(psDD.Q_Q10[last_smple_idx], 10);
                    xq[i - decisionDelay] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT_ROUND(
                        Inlines.silk_SMULWW(psDD.Xq_Q14[last_smple_idx], delayedGain_Q10[last_smple_idx]), 8));
                    NSQ.sLTP_shp_Q14[NSQ.sLTP_shp_buf_idx - decisionDelay] = psDD.Shape_Q14[last_smple_idx];
                    sLTP_Q15[NSQ.sLTP_buf_idx - decisionDelay] = psDD.Pred_Q15[last_smple_idx];
                }
                NSQ.sLTP_shp_buf_idx++;
                NSQ.sLTP_buf_idx++;

                /* Update states */
                for (k = 0; k < nStatesDelayedDecision; k++)
                {
                    psDD = psDelDec[k];
                    psSS = psSampleState[k];
                    psDD.LF_AR_Q14 = psSS.left.LF_AR_Q14;
                    psDD.sLPC_Q14[SilkConstants.NSQ_LPC_BUF_LENGTH + i] = psSS.left.xq_Q14;
                    psDD.Xq_Q14[smpl_buf_idx.Val] = psSS.left.xq_Q14;
                    psDD.Q_Q10[smpl_buf_idx.Val] = psSS.left.Q_Q10;
                    psDD.Pred_Q15[smpl_buf_idx.Val] = Inlines.silk_LSHIFT32(psSS.left.LPC_exc_Q14, 1);
                    psDD.Shape_Q14[smpl_buf_idx.Val] = psSS.left.sLTP_shp_Q14;
                    psDD.Seed = Inlines.silk_ADD32_ovflw(psDD.Seed, Inlines.silk_RSHIFT_ROUND(psSS.left.Q_Q10, 10));
                    psDD.RandState[smpl_buf_idx.Val] = psDD.Seed;
                    psDD.RD_Q10 = psSS.left.RD_Q10;
                }
                delayedGain_Q10[smpl_buf_idx.Val] = Gain_Q10;
            }

            /* Update LPC states */
            for (k = 0; k < nStatesDelayedDecision; k++)
            {
                psDD = psDelDec[k];
                psDD.sLPC_Q14.Point(length).MemCopyTo(psDD.sLPC_Q14, SilkConstants.NSQ_LPC_BUF_LENGTH);
            }
        }

        private static void silk_nsq_del_dec_scale_states(
                silk_encoder_state psEncC,               /* I    Encoder State                       */
                silk_nsq_state NSQ,                       /* I/O  NSQ state                           */
                NSQ_del_dec_struct[] psDelDec,                 /* I/O  Delayed decision states             */
                Pointer<int> x_Q3,                     /* I    Input in Q3                         */
                Pointer<int> x_sc_Q10,                 /* O    Input scaled with 1/Gain in Q10     */
                Pointer<short> sLTP,                     /* I    Re-whitened LTP state in Q0         */
                Pointer<int> sLTP_Q15,                 /* O    LTP state matching scaled input     */
                int subfr,                      /* I    Subframe number                     */
                int nStatesDelayedDecision,     /* I    Number of del dec states            */
                int LTP_scale_Q14,              /* I    LTP state scaling                   */
                Pointer<int> Gains_Q16,  /* I [MAX_NB_SUBFR]                                       */
                Pointer<int> pitchL,     /* I    Pitch lag [MAX_NB_SUBFR]                          */
                int signal_type,                /* I    Signal type                         */
                int decisionDelay               /* I    Decision delay                      */
            )
        {
            int i, k, lag;
            int gain_adj_Q16, inv_gain_Q31, inv_gain_Q23;
            NSQ_del_dec_struct psDD;

            lag = pitchL[subfr];
            inv_gain_Q31 = Inlines.silk_INVERSE32_varQ(Inlines.silk_max(Gains_Q16[subfr], 1), 47);
            Inlines.OpusAssert(inv_gain_Q31 != 0);

            /* Calculate gain adjustment factor */
            if (Gains_Q16[subfr] != NSQ.prev_gain_Q16)
            {
                gain_adj_Q16 = Inlines.silk_DIV32_varQ(NSQ.prev_gain_Q16, Gains_Q16[subfr], 16);
            }
            else {
                gain_adj_Q16 = (int)1 << 16;
            }

            /* Scale input */
            inv_gain_Q23 = Inlines.silk_RSHIFT_ROUND(inv_gain_Q31, 8);
            for (i = 0; i < psEncC.subfr_length; i++)
            {
                x_sc_Q10[i] = Inlines.silk_SMULWW(x_Q3[i], inv_gain_Q23);
            }

            /* Save inverse gain */
            NSQ.prev_gain_Q16 = Gains_Q16[subfr];

            /* After rewhitening the LTP state is un-scaled, so scale with inv_gain_Q16 */
            if (NSQ.rewhite_flag != 0)
            {
                if (subfr == 0)
                {
                    /* Do LTP downscaling */
                    inv_gain_Q31 = Inlines.silk_LSHIFT(Inlines.silk_SMULWB(inv_gain_Q31, LTP_scale_Q14), 2);
                }
                for (i = NSQ.sLTP_buf_idx - lag - SilkConstants.LTP_ORDER / 2; i < NSQ.sLTP_buf_idx; i++)
                {
                    Inlines.OpusAssert(i < SilkConstants.MAX_FRAME_LENGTH);
                    sLTP_Q15[i] = Inlines.silk_SMULWB(inv_gain_Q31, sLTP[i]);
                }
            }

            /* Adjust for changing gain */
            if (gain_adj_Q16 != (int)1 << 16)
            {
                /* Scale long-term shaping state */
                for (i = NSQ.sLTP_shp_buf_idx - psEncC.ltp_mem_length; i < NSQ.sLTP_shp_buf_idx; i++)
                {
                    NSQ.sLTP_shp_Q14[i] = Inlines.silk_SMULWW(gain_adj_Q16, NSQ.sLTP_shp_Q14[i]);
                }

                /* Scale long-term prediction state */
                if (signal_type == SilkConstants.TYPE_VOICED && NSQ.rewhite_flag == 0)
                {
                    for (i = NSQ.sLTP_buf_idx - lag - SilkConstants.LTP_ORDER / 2; i < NSQ.sLTP_buf_idx - decisionDelay; i++)
                    {
                        sLTP_Q15[i] = Inlines.silk_SMULWW(gain_adj_Q16, sLTP_Q15[i]);
                    }
                }

                for (k = 0; k < nStatesDelayedDecision; k++)
                {
                    psDD = psDelDec[k];

                    /* Scale scalar states */
                    psDD.LF_AR_Q14 = Inlines.silk_SMULWW(gain_adj_Q16, psDD.LF_AR_Q14);

                    /* Scale short-term prediction and shaping states */
                    for (i = 0; i < SilkConstants.NSQ_LPC_BUF_LENGTH; i++)
                    {
                        psDD.sLPC_Q14[i] = Inlines.silk_SMULWW(gain_adj_Q16, psDD.sLPC_Q14[i]);
                    }
                    for (i = 0; i < SilkConstants.MAX_SHAPE_LPC_ORDER; i++)
                    {
                        psDD.sAR2_Q14[i] = Inlines.silk_SMULWW(gain_adj_Q16, psDD.sAR2_Q14[i]);
                    }
                    for (i = 0; i < SilkConstants.DECISION_DELAY; i++)
                    {
                        psDD.Pred_Q15[i] = Inlines.silk_SMULWW(gain_adj_Q16, psDD.Pred_Q15[i]);
                        psDD.Shape_Q14[i] = Inlines.silk_SMULWW(gain_adj_Q16, psDD.Shape_Q14[i]);
                    }
                }
            }
        }

        private class NSQ_del_dec_struct
        {
            public Pointer<int> sLPC_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_SUB_FRAME_LENGTH + SilkConstants.NSQ_LPC_BUF_LENGTH);
            public Pointer<int> RandState = Pointer.Malloc<int>(SilkConstants.DECISION_DELAY);
            public Pointer<int> Q_Q10 = Pointer.Malloc<int>(SilkConstants.DECISION_DELAY);
            public Pointer<int> Xq_Q14 = Pointer.Malloc<int>(SilkConstants.DECISION_DELAY);
            public Pointer<int> Pred_Q15 = Pointer.Malloc<int>(SilkConstants.DECISION_DELAY);
            public Pointer<int> Shape_Q14 = Pointer.Malloc<int>(SilkConstants.DECISION_DELAY);
            public Pointer<int> sAR2_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_SHAPE_LPC_ORDER);
            public int LF_AR_Q14;
            public int Seed;
            public int SeedInit;
            public int RD_Q10;

            public NSQ_del_dec_struct()
            {
                Reset();
            }

            public void Reset()
            {
                sLPC_Q14.MemSet(0, SilkConstants.MAX_SUB_FRAME_LENGTH + SilkConstants.NSQ_LPC_BUF_LENGTH);
                RandState.MemSet(0, SilkConstants.DECISION_DELAY);
                Q_Q10.MemSet(0, SilkConstants.DECISION_DELAY);
                Xq_Q14.MemSet(0, SilkConstants.DECISION_DELAY);
                Pred_Q15.MemSet(0, SilkConstants.DECISION_DELAY);
                Shape_Q14.MemSet(0, SilkConstants.DECISION_DELAY);
                sAR2_Q14.MemSet(0, SilkConstants.MAX_SHAPE_LPC_ORDER);
                LF_AR_Q14 = 0;
                Seed = 0;
                SeedInit = 0;
                RD_Q10 = 0;
            }

            public void PartialCopyFrom(NSQ_del_dec_struct other, int q14Offset)
            {
                other.sLPC_Q14.Point(q14Offset).MemCopyTo(sLPC_Q14.Point(q14Offset), SilkConstants.MAX_SUB_FRAME_LENGTH + SilkConstants.NSQ_LPC_BUF_LENGTH - q14Offset);
                other.RandState.MemCopyTo(RandState, SilkConstants.DECISION_DELAY);
                other.Q_Q10.MemCopyTo(Q_Q10, SilkConstants.DECISION_DELAY);
                other.Xq_Q14.MemCopyTo(Xq_Q14, SilkConstants.DECISION_DELAY);
                other.Pred_Q15.MemCopyTo(Pred_Q15, SilkConstants.DECISION_DELAY);
                other.Shape_Q14.MemCopyTo(Shape_Q14, SilkConstants.DECISION_DELAY);
                other.sAR2_Q14.MemCopyTo(sAR2_Q14, SilkConstants.MAX_SHAPE_LPC_ORDER);

                LF_AR_Q14 = other.LF_AR_Q14;
                Seed = other.Seed;
                SeedInit = other.SeedInit;
                RD_Q10 = other.RD_Q10;
            }

            public void Assign(NSQ_del_dec_struct other)
            {
                this.PartialCopyFrom(other, 0);
            }
        }

        private class NSQ_sample_struct
        {
            public int Q_Q10;
            public int RD_Q10;
            public int xq_Q14;
            public int LF_AR_Q14;
            public int sLTP_shp_Q14;
            public int LPC_exc_Q14;

            public NSQ_sample_struct()
            {
                Reset();
            }

            public void Reset()
            {
                Q_Q10 = 0;
                RD_Q10 = 0;
                xq_Q14 = 0;
                LF_AR_Q14 = 0;
                sLTP_shp_Q14 = 0;
                LPC_exc_Q14 = 0;
            }

            public void Assign(NSQ_sample_struct other)
            {
                this.Q_Q10 = other.Q_Q10;
                this.RD_Q10 = other.RD_Q10;
                this.xq_Q14 = other.xq_Q14;
                this.LF_AR_Q14 = other.LF_AR_Q14;
                this.sLTP_shp_Q14 = other.sLTP_shp_Q14;
                this.LPC_exc_Q14 = other.LPC_exc_Q14;
            }
        }

        private class NSQ_sample_pair
        {
            public NSQ_sample_struct left = new NSQ_sample_struct();
            public NSQ_sample_struct right = new NSQ_sample_struct();
        }
    }
}
