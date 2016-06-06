using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class find_LTP
    {
        /* Head room for correlations */
        private const int LTP_CORRS_HEAD_ROOM = 2;

        /// <summary>
        /// Finds linear prediction coeffecients and weights
        /// </summary>
        /// <param name="b_Q14"></param>
        /// <param name="WLTP"></param>
        /// <param name="LTPredCodGain_Q7"></param>
        /// <param name="r_lpc"></param>
        /// <param name="lag"></param>
        /// <param name="Wght_Q15"></param>
        /// <param name="subfr_length"></param>
        /// <param name="nb_subfr"></param>
        /// <param name="mem_offset"></param>
        /// <param name="corr_rshifts"></param>
        /// <param name="arch"></param>
        public static void silk_find_LTP_FIX(
             Pointer<short> b_Q14,      /* O    LTP coefs [SilkConstants.MAX_NB_SUBFR * SilkConstants.LTP_ORDER]                                                                  */
             Pointer<int> WLTP, /* O    Weight for LTP quantization [SilkConstants.MAX_NB_SUBFR * SilkConstants.LTP_ORDER * SilkConstants.LTP_ORDER]                                          */
             BoxedValue<int> LTPredCodGain_Q7,                      /* O    LTP coding gain                                                             */
             Pointer<short> r_lpc,                                /* I    residual signal after LPC signal + state for first 10 ms                    */
              Pointer<int> lag,                    /* I    LTP lags   [SilkConstants.MAX_NB_SUBFR]                                                                 */
              Pointer<int> Wght_Q15,               /* I    weights [SilkConstants.MAX_NB_SUBFR]                                                                    */
             int subfr_length,                           /* I    subframe length                                                             */
             int nb_subfr,                               /* I    number of subframes                                                         */
             int mem_offset,                             /* I    number of samples in LTP memory                                             */
              Pointer<int> corr_rshifts,           /* O    right shifts applied to correlations  [SilkConstants.MAX_NB_SUBFR]                                      */
             int arch                                    /* I    Run-time architecture                                                       */
         )
        {
            int i, k, lshift;
            Pointer<short> r_ptr, lag_ptr;
            Pointer<short> b_Q14_ptr;

            int regu;
            Pointer<int> WLTP_ptr;
            Pointer<int> b_Q16 = Pointer.Malloc<int>(SilkConstants.LTP_ORDER);
            Pointer<int> delta_b_Q14 = Pointer.Malloc<int>(SilkConstants.LTP_ORDER);
            Pointer<int> d_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
            Pointer<int> nrg = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
            int g_Q26;
            Pointer<int> w = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
            int WLTP_max, max_abs_d_Q14, max_w_bits;

            int temp32, denom32;
            int extra_shifts;
            int rr_shifts, maxRshifts, maxRshifts_wxtra, LZs;
            int LPC_res_nrg, LPC_LTP_res_nrg, div_Q16;
            Pointer<int> Rr = Pointer.Malloc<int>(SilkConstants.LTP_ORDER);
            Pointer<int> rr = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
            int wd, m_Q12;

            b_Q14_ptr = b_Q14;
            WLTP_ptr = WLTP;
            r_ptr = r_lpc.Point(mem_offset);
            for (k = 0; k < nb_subfr; k++)
            {
                lag_ptr = r_ptr.Point(0 - (lag[k] + SilkConstants.LTP_ORDER / 2));

                BoxedValue<int> boxed_energy = new BoxedValue<int>(rr[k]); // fixme: this var may not be initialized
                BoxedValue<int> boxed_shifts = new BoxedValue<int>();
                SumSqrShift.silk_sum_sqr_shift(boxed_energy, boxed_shifts, r_ptr, subfr_length); /* rr[ k ] in Q( -rr_shifts ) */
                rr[k] = boxed_energy.Val;
                rr_shifts = boxed_shifts.Val;

                /* Assure headroom */
                LZs = Inlines.silk_CLZ32(rr[k]);
                if (LZs < LTP_CORRS_HEAD_ROOM)
                {
                    rr[k] = Inlines.silk_RSHIFT_ROUND(rr[k], LTP_CORRS_HEAD_ROOM - LZs);
                    rr_shifts += (LTP_CORRS_HEAD_ROOM - LZs);
                }
                corr_rshifts[k] = rr_shifts;
                boxed_shifts.Val = corr_rshifts[k];
                corrMatrix.silk_corrMatrix_FIX(lag_ptr, subfr_length, SilkConstants.LTP_ORDER, LTP_CORRS_HEAD_ROOM, WLTP_ptr, boxed_shifts, arch);  /* WLTP_fix_ptr in Q( -corr_rshifts[ k ] ) */
                corr_rshifts[k] = boxed_shifts.Val;

                /* The correlation vector always has lower max abs value than rr and/or RR so head room is assured */
                corrMatrix.silk_corrVector_FIX(lag_ptr, r_ptr, subfr_length, SilkConstants.LTP_ORDER, Rr, corr_rshifts[k], arch);  /* Rr_fix_ptr   in Q( -corr_rshifts[ k ] ) */
                if (corr_rshifts[k] > rr_shifts)
                {
                    rr[k] = Inlines.silk_RSHIFT(rr[k], corr_rshifts[k] - rr_shifts); /* rr[ k ] in Q( -corr_rshifts[ k ] ) */
                }
                Debug.Assert(rr[k] >= 0);

                regu = 1;
                regu = Inlines.silk_SMLAWB(regu, rr[k], Inlines.SILK_FIX_CONST(TuningParameters.LTP_DAMPING / 3, 16));
                regu = Inlines.silk_SMLAWB(regu, Inlines.matrix_ptr(WLTP_ptr, 0, 0, SilkConstants.LTP_ORDER), Inlines.SILK_FIX_CONST(TuningParameters.LTP_DAMPING / 3, 16));
                regu = Inlines.silk_SMLAWB(regu, Inlines.matrix_ptr(WLTP_ptr, SilkConstants.LTP_ORDER - 1, SilkConstants.LTP_ORDER - 1, SilkConstants.LTP_ORDER), Inlines.SILK_FIX_CONST(TuningParameters.LTP_DAMPING / 3, 16));
                regularize_correlations.silk_regularize_correlations_FIX(WLTP_ptr, rr.Point(k), regu, SilkConstants.LTP_ORDER);

                solve_LS.silk_solve_LDL_FIX(WLTP_ptr, SilkConstants.LTP_ORDER, Rr, b_Q16); /* WLTP_fix_ptr and Rr_fix_ptr both in Q(-corr_rshifts[k]) */

                /* Limit and store in Q14 */
                silk_fit_LTP(b_Q16, b_Q14_ptr);

                /* Calculate residual energy */
                nrg[k] = residual_energy.silk_residual_energy16_covar_FIX(b_Q14_ptr, WLTP_ptr, Rr, rr[k], SilkConstants.LTP_ORDER, 14); /* nrg_fix in Q( -corr_rshifts[ k ] ) */

                /* temp = Wght[ k ] / ( nrg[ k ] * Wght[ k ] + 0.01f * subfr_length ); */
                extra_shifts = Inlines.silk_min_int(corr_rshifts[k], LTP_CORRS_HEAD_ROOM);
                denom32 = Inlines.silk_LSHIFT_SAT32(Inlines.silk_SMULWB(nrg[k], Wght_Q15[k]), 1 + extra_shifts) + /* Q( -corr_rshifts[ k ] + extra_shifts ) */
                            Inlines.silk_RSHIFT(Inlines.silk_SMULWB((int)subfr_length, 655), corr_rshifts[k] - extra_shifts);    /* Q( -corr_rshifts[ k ] + extra_shifts ) */
                denom32 = Inlines.silk_max(denom32, 1);
                Debug.Assert(((long)Wght_Q15[k] << 16) < int.MaxValue);                       /* Wght always < 0.5 in Q0 */
                temp32 = Inlines.silk_DIV32(Inlines.silk_LSHIFT((int)Wght_Q15[k], 16), denom32);             /* Q( 15 + 16 + corr_rshifts[k] - extra_shifts ) */
                temp32 = Inlines.silk_RSHIFT(temp32, 31 + corr_rshifts[k] - extra_shifts - 26);               /* Q26 */

                /* Limit temp such that the below scaling never wraps around */
                WLTP_max = 0;
                for (i = 0; i < SilkConstants.LTP_ORDER * SilkConstants.LTP_ORDER; i++)
                {
                    WLTP_max = Inlines.silk_max(WLTP_ptr[i], WLTP_max);
                }
                lshift = Inlines.silk_CLZ32(WLTP_max) - 1 - 3; /* keep 3 bits free for vq_nearest_neighbor_fix */
                Debug.Assert(26 - 18 + lshift >= 0);
                if (26 - 18 + lshift < 31)
                {
                    temp32 = Inlines.silk_min_32(temp32, Inlines.silk_LSHIFT((int)1, 26 - 18 + lshift));
                }

                Inlines.silk_scale_vector32_Q26_lshift_18(WLTP_ptr, temp32, SilkConstants.LTP_ORDER * SilkConstants.LTP_ORDER); /* WLTP_ptr in Q( 18 - corr_rshifts[ k ] ) */

                w[k] = Inlines.matrix_ptr(WLTP_ptr, SilkConstants.LTP_ORDER / 2, SilkConstants.LTP_ORDER / 2, SilkConstants.LTP_ORDER); /* w in Q( 18 - corr_rshifts[ k ] ) */
                Debug.Assert(w[k] >= 0);

                r_ptr = r_ptr.Point(subfr_length);
                b_Q14_ptr = b_Q14_ptr.Point(SilkConstants.LTP_ORDER);
                WLTP_ptr = WLTP_ptr.Point(SilkConstants.LTP_ORDER * SilkConstants.LTP_ORDER);
            }

            maxRshifts = 0;
            for (k = 0; k < nb_subfr; k++)
            {
                maxRshifts = Inlines.silk_max_int(corr_rshifts[k], maxRshifts);
            }

            /* Compute LTP coding gain */
            if (LTPredCodGain_Q7 != null)
            {
                LPC_LTP_res_nrg = 0;
                LPC_res_nrg = 0;
                Debug.Assert(LTP_CORRS_HEAD_ROOM >= 2); /* Check that no overflow will happen when adding */
                for (k = 0; k < nb_subfr; k++)
                {
                    LPC_res_nrg = Inlines.silk_ADD32(LPC_res_nrg, Inlines.silk_RSHIFT(Inlines.silk_ADD32(Inlines.silk_SMULWB(rr[k], Wght_Q15[k]), 1), 1 + (maxRshifts - corr_rshifts[k]))); /* Q( -maxRshifts ) */
                    LPC_LTP_res_nrg = Inlines.silk_ADD32(LPC_LTP_res_nrg, Inlines.silk_RSHIFT(Inlines.silk_ADD32(Inlines.silk_SMULWB(nrg[k], Wght_Q15[k]), 1), 1 + (maxRshifts - corr_rshifts[k]))); /* Q( -maxRshifts ) */
                }
                LPC_LTP_res_nrg = Inlines.silk_max(LPC_LTP_res_nrg, 1); /* avoid division by zero */

                div_Q16 = Inlines.silk_DIV32_varQ(LPC_res_nrg, LPC_LTP_res_nrg, 16);
                LTPredCodGain_Q7.Val = (int)Inlines.silk_SMULBB(3, Inlines.silk_lin2log(div_Q16) - (16 << 7));

                Debug.Assert(LTPredCodGain_Q7.Val == (int)Inlines.silk_SAT16(Inlines.silk_MUL(3, Inlines.silk_lin2log(div_Q16) - (16 << 7))));
            }

            /* smoothing */
            /* d = sum( B, 1 ); */
            b_Q14_ptr = b_Q14;
            for (k = 0; k < nb_subfr; k++)
            {
                d_Q14[k] = 0;
                for (i = 0; i < SilkConstants.LTP_ORDER; i++)
                {
                    d_Q14[k] += b_Q14_ptr[i];
                }
                b_Q14_ptr = b_Q14_ptr.Point(SilkConstants.LTP_ORDER);
            }

            /* m = ( w * d' ) / ( sum( w ) + 1e-3 ); */

            /* Find maximum absolute value of d_Q14 and the bits used by w in Q0 */
            max_abs_d_Q14 = 0;
            max_w_bits = 0;
            for (k = 0; k < nb_subfr; k++)
            {
                max_abs_d_Q14 = Inlines.silk_max_32(max_abs_d_Q14, Inlines.silk_abs(d_Q14[k]));
                /* w[ k ] is in Q( 18 - corr_rshifts[ k ] ) */
                /* Find bits needed in Q( 18 - maxRshifts ) */
                max_w_bits = Inlines.silk_max_32(max_w_bits, 32 - Inlines.silk_CLZ32(w[k]) + corr_rshifts[k] - maxRshifts);
            }

            /* max_abs_d_Q14 = (5 << 15); worst case, i.e. SilkConstants.LTP_ORDER * -silk_int16_MIN */
            Debug.Assert(max_abs_d_Q14 <= (5 << 15));

            /* How many bits is needed for w*d' in Q( 18 - maxRshifts ) in the worst case, of all d_Q14's being equal to max_abs_d_Q14 */
            extra_shifts = max_w_bits + 32 - Inlines.silk_CLZ32(max_abs_d_Q14) - 14;

            /* Subtract what we got available; bits in output var plus maxRshifts */
            extra_shifts -= (32 - 1 - 2 + maxRshifts); /* Keep sign bit free as well as 2 bits for accumulation */
            extra_shifts = Inlines.silk_max_int(extra_shifts, 0);

            maxRshifts_wxtra = maxRshifts + extra_shifts;

            temp32 = Inlines.silk_RSHIFT(262, maxRshifts + extra_shifts) + 1; /* 1e-3f in Q( 18 - (maxRshifts + extra_shifts) ) */
            wd = 0;
            for (k = 0; k < nb_subfr; k++)
            {
                /* w has at least 2 bits of headroom so no overflow should happen */
                temp32 = Inlines.silk_ADD32(temp32, Inlines.silk_RSHIFT(w[k], maxRshifts_wxtra - corr_rshifts[k]));                      /* Q( 18 - maxRshifts_wxtra ) */
                wd = Inlines.silk_ADD32(wd, Inlines.silk_LSHIFT(Inlines.silk_SMULWW(Inlines.silk_RSHIFT(w[k], maxRshifts_wxtra - corr_rshifts[k]), d_Q14[k]), 2)); /* Q( 18 - maxRshifts_wxtra ) */
            }
            m_Q12 = Inlines.silk_DIV32_varQ(wd, temp32, 12);

            b_Q14_ptr = b_Q14;
            for (k = 0; k < nb_subfr; k++)
            {
                /* w_fix[ k ] from Q( 18 - corr_rshifts[ k ] ) to Q( 16 ) */
                if (2 - corr_rshifts[k] > 0)
                {
                    temp32 = Inlines.silk_RSHIFT(w[k], 2 - corr_rshifts[k]);
                }
                else {
                    temp32 = Inlines.silk_LSHIFT_SAT32(w[k], corr_rshifts[k] - 2);
                }

                g_Q26 = Inlines.silk_MUL(
                    Inlines.silk_DIV32(
                        Inlines.SILK_FIX_CONST(TuningParameters.LTP_SMOOTHING, 26),
                        Inlines.silk_RSHIFT(Inlines.SILK_FIX_CONST(TuningParameters.LTP_SMOOTHING, 26), 10) + temp32),                          /* Q10 */
                    Inlines.silk_LSHIFT_SAT32(Inlines.silk_SUB_SAT32((int)m_Q12, Inlines.silk_RSHIFT(d_Q14[k], 2)), 4));    /* Q16 */

                temp32 = 0;
                for (i = 0; i < SilkConstants.LTP_ORDER; i++)
                {
                    delta_b_Q14[i] = Inlines.silk_max_16(b_Q14_ptr[i], 1638);     /* 1638_Q14 = 0.1_Q0 */
                    temp32 += delta_b_Q14[i];                                 /* Q14 */
                }
                temp32 = Inlines.silk_DIV32(g_Q26, temp32);                           /* Q14 . Q12 */
                for (i = 0; i < SilkConstants.LTP_ORDER; i++)
                {
                    b_Q14_ptr[i] = Inlines.CHOP16(Inlines.silk_LIMIT_32((int)b_Q14_ptr[i] + Inlines.silk_SMULWB(Inlines.silk_LSHIFT_SAT32(temp32, 4), delta_b_Q14[i]), -16000, 28000));
                }
                b_Q14_ptr = b_Q14_ptr.Point(SilkConstants.LTP_ORDER);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="LTP_coefs_Q16">[SilkConstants.LTP_ORDER]</param>
        /// <param name="LTP_coefs_Q14">[SilkConstants.LTP_ORDER]</param>
        /// <param name=""></param>
        public static void silk_fit_LTP(
            Pointer<int> LTP_coefs_Q16,
            Pointer<short> LTP_coefs_Q14
)
        {
            int i;

            for (i = 0; i < SilkConstants.LTP_ORDER; i++)
            {
                LTP_coefs_Q14[i] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT_ROUND(LTP_coefs_Q16[i], 2));
            }
        }

    }
}
