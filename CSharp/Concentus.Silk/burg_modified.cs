using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class burg_modified
    {
        /* subfr_length * nb_subfr = ( 0.005 * 16000 + 16 ) * 4 = 384 */
        private const int MAX_FRAME_SIZE = 384;
        private const int QA = 25;
        private const int N_BITS_HEAD_ROOM = 2;
        private const int MIN_RSHIFTS = -16;
        private const int MAX_RSHIFTS = (32 - QA);

        /* Compute reflection coefficients from input signal */
        public static void silk_burg_modified_c(
            BoxedValue<int> res_nrg,           /* O    Residual energy                                             */
            BoxedValue<int> res_nrg_Q,         /* O    Residual energy Q value                                     */
            Pointer<int> A_Q16,            /* O    Prediction coefficients (length order)                      */
            Pointer<short> x,                /* I    Input signal, length: nb_subfr * ( D + subfr_length )       */
            int minInvGain_Q30,     /* I    Inverse of max prediction gain                              */
            int subfr_length,       /* I    Input signal subframe length (incl. D preceding samples)    */
            int nb_subfr,           /* I    Number of subframes stacked in x                            */
            int D,                  /* I    Order                                                       */
            int arch                /* I    Run-time architecture                                       */
        )
        {
            int k, n, s, lz, rshifts, reached_max_gain;
            int C0, num, nrg, rc_Q31, invGain_Q30, Atmp_QA, Atmp1, tmp1, tmp2, x1, x2;
            Pointer<short> x_ptr;
            Pointer<int> C_first_row = Pointer.Malloc<int>(SilkConstants.SILK_MAX_ORDER_LPC);
            Pointer<int> C_last_row = Pointer.Malloc<int>(SilkConstants.SILK_MAX_ORDER_LPC);
            Pointer<int> Af_QA = Pointer.Malloc<int>(SilkConstants.SILK_MAX_ORDER_LPC);
            Pointer<int> CAf = Pointer.Malloc<int>(SilkConstants.SILK_MAX_ORDER_LPC + 1);
            Pointer<int> CAb = Pointer.Malloc<int>(SilkConstants.SILK_MAX_ORDER_LPC + 1);
            Pointer<int> xcorr = Pointer.Malloc<int>(SilkConstants.SILK_MAX_ORDER_LPC);
            long C0_64;

            Debug.Assert(subfr_length * nb_subfr <= MAX_FRAME_SIZE);

            /* Compute autocorrelations, added over subframes */
            C0_64 = Inlines.silk_inner_prod16_aligned_64_c(x, x, subfr_length * nb_subfr);
            lz = Inlines.silk_CLZ64(C0_64);
            rshifts = 32 + 1 + N_BITS_HEAD_ROOM - lz;
            if (rshifts > MAX_RSHIFTS) rshifts = MAX_RSHIFTS;
            if (rshifts < MIN_RSHIFTS) rshifts = MIN_RSHIFTS;

            if (rshifts > 0)
            {
                C0 = (int)Inlines.silk_RSHIFT64(C0_64, rshifts);
            }
            else {
                C0 = Inlines.silk_LSHIFT32((int)C0_64, -rshifts);
            }

            CAb[0] = CAf[0] = C0 + Inlines.silk_SMMUL(Inlines.SILK_FIX_CONST(TuningParameters.FIND_LPC_COND_FAC, 32), C0) + 1;                                /* Q(-rshifts) */
            C_first_row.MemSet(0, SilkConstants.SILK_MAX_ORDER_LPC);
            if (rshifts > 0)
            {
                for (s = 0; s < nb_subfr; s++)
                {
                    x_ptr = x.Point(s * subfr_length);
                    for (n = 1; n < D + 1; n++)
                    {
                        C_first_row[n - 1] += (int)Inlines.silk_RSHIFT64(
                          Inlines.silk_inner_prod16_aligned_64_c(x_ptr, x_ptr.Point(n), subfr_length - n), rshifts);
                    }
                }
            }
            else {
                for (s = 0; s < nb_subfr; s++)
                {
                    int i;
                    int d;
                    x_ptr = x.Point(s * subfr_length);
                    celt_pitch_xcorr.pitch_xcorr(x_ptr, x_ptr.Point(1), xcorr, subfr_length - D, D);
                    for (n = 1; n < D + 1; n++)
                    {
                        for (i = n + subfr_length - D, d = 0; i < subfr_length; i++)
                            d = Inlines.MAC16_16(d, x_ptr[i], x_ptr[i - n]);
                        xcorr[n - 1] += d;
                    }
                    for (n = 1; n < D + 1; n++)
                    {
                        C_first_row[n - 1] += Inlines.silk_LSHIFT32(xcorr[n - 1], -rshifts);
                    }
                }
            }
            C_first_row.MemCopyTo(C_last_row, SilkConstants.SILK_MAX_ORDER_LPC);

            /* Initialize */
            CAb[0] = CAf[0] = C0 + Inlines.silk_SMMUL(Inlines.SILK_FIX_CONST(TuningParameters.FIND_LPC_COND_FAC, 32), C0) + 1;                                /* Q(-rshifts) */

            invGain_Q30 = (int)1 << 30;
            reached_max_gain = 0;
            for (n = 0; n < D; n++)
            {
                /* Update first row of correlation matrix (without first element) */
                /* Update last row of correlation matrix (without last element, stored in reversed order) */
                /* Update C * Af */
                /* Update C * flipud(Af) (stored in reversed order) */
                if (rshifts > -2)
                {
                    for (s = 0; s < nb_subfr; s++)
                    {
                        x_ptr = x.Point(s * subfr_length);
                        x1 = -Inlines.silk_LSHIFT32((int)x_ptr[n], 16 - rshifts);        /* Q(16-rshifts) */
                        x2 = -Inlines.silk_LSHIFT32((int)x_ptr[subfr_length - n - 1], 16 - rshifts);        /* Q(16-rshifts) */
                        tmp1 = Inlines.silk_LSHIFT32((int)x_ptr[n], QA - 16);             /* Q(QA-16) */
                        tmp2 = Inlines.silk_LSHIFT32((int)x_ptr[subfr_length - n - 1], QA - 16);             /* Q(QA-16) */
                        for (k = 0; k < n; k++)
                        {
                            C_first_row[k] = Inlines.silk_SMLAWB(C_first_row[k], x1, x_ptr[n - k - 1]); /* Q( -rshifts ) */
                            C_last_row[k] = Inlines.silk_SMLAWB(C_last_row[k], x2, x_ptr[subfr_length - n + k]); /* Q( -rshifts ) */
                            Atmp_QA = Af_QA[k];
                            tmp1 = Inlines.silk_SMLAWB(tmp1, Atmp_QA, x_ptr[n - k - 1]);                 /* Q(QA-16) */
                            tmp2 = Inlines.silk_SMLAWB(tmp2, Atmp_QA, x_ptr[subfr_length - n + k]);                 /* Q(QA-16) */
                        }
                        tmp1 = Inlines.silk_LSHIFT32(-tmp1, 32 - QA - rshifts);                                       /* Q(16-rshifts) */
                        tmp2 = Inlines.silk_LSHIFT32(-tmp2, 32 - QA - rshifts);                                       /* Q(16-rshifts) */
                        for (k = 0; k <= n; k++)
                        {
                            CAf[k] = Inlines.silk_SMLAWB(CAf[k], tmp1, x_ptr[n - k]);        /* Q( -rshift ) */
                            CAb[k] = Inlines.silk_SMLAWB(CAb[k], tmp2, x_ptr[subfr_length - n + k - 1]);        /* Q( -rshift ) */
                        }
                    }
                }
                else {
                    for (s = 0; s < nb_subfr; s++)
                    {
                        x_ptr = x.Point(s * subfr_length);
                        x1 = -Inlines.silk_LSHIFT32((int)x_ptr[n], -rshifts);            /* Q( -rshifts ) */
                        x2 = -Inlines.silk_LSHIFT32((int)x_ptr[subfr_length - n - 1], -rshifts);            /* Q( -rshifts ) */
                        tmp1 = Inlines.silk_LSHIFT32((int)x_ptr[n], 17);                  /* Q17 */
                        tmp2 = Inlines.silk_LSHIFT32((int)x_ptr[subfr_length - n - 1], 17);                  /* Q17 */
                        for (k = 0; k < n; k++)
                        {
                            C_first_row[k] = Inlines.silk_MLA(C_first_row[k], x1, x_ptr[n - k - 1]); /* Q( -rshifts ) */
                            C_last_row[k] = Inlines.silk_MLA(C_last_row[k], x2, x_ptr[subfr_length - n + k]); /* Q( -rshifts ) */
                            Atmp1 = Inlines.silk_RSHIFT_ROUND(Af_QA[k], QA - 17);                                   /* Q17 */
                            tmp1 = Inlines.silk_MLA(tmp1, x_ptr[n - k - 1], Atmp1);                      /* Q17 */
                            tmp2 = Inlines.silk_MLA(tmp2, x_ptr[subfr_length - n + k], Atmp1);                      /* Q17 */
                        }
                        tmp1 = -tmp1;                                                                           /* Q17 */
                        tmp2 = -tmp2;                                                                           /* Q17 */
                        for (k = 0; k <= n; k++)
                        {
                            CAf[k] = Inlines.silk_SMLAWW(CAf[k], tmp1,
                              Inlines.silk_LSHIFT32((int)x_ptr[n - k], -rshifts - 1));                    /* Q( -rshift ) */
                            CAb[k] = Inlines.silk_SMLAWW(CAb[k], tmp2,
                              Inlines.silk_LSHIFT32((int)x_ptr[subfr_length - n + k - 1], -rshifts - 1)); /* Q( -rshift ) */
                        }
                    }
                }

                /* Calculate nominator and denominator for the next order reflection (parcor) coefficient */
                tmp1 = C_first_row[n];                                                                        /* Q( -rshifts ) */
                tmp2 = C_last_row[n];                                                                         /* Q( -rshifts ) */
                num = 0;                                                                                       /* Q( -rshifts ) */
                nrg = Inlines.silk_ADD32(CAb[0], CAf[0]);                                                        /* Q( 1-rshifts ) */
                for (k = 0; k < n; k++)
                {
                    Atmp_QA = Af_QA[k];
                    lz = Inlines.silk_CLZ32(Inlines.silk_abs(Atmp_QA)) - 1;
                    lz = Inlines.silk_min(32 - QA, lz);
                    Atmp1 = Inlines.silk_LSHIFT32(Atmp_QA, lz);                                                       /* Q( QA + lz ) */

                    tmp1 = Inlines.silk_ADD_LSHIFT32(tmp1, Inlines.silk_SMMUL(C_last_row[n - k - 1], Atmp1), 32 - QA - lz);  /* Q( -rshifts ) */
                    tmp2 = Inlines.silk_ADD_LSHIFT32(tmp2, Inlines.silk_SMMUL(C_first_row[n - k - 1], Atmp1), 32 - QA - lz);  /* Q( -rshifts ) */
                    num = Inlines.silk_ADD_LSHIFT32(num, Inlines.silk_SMMUL(CAb[n - k], Atmp1), 32 - QA - lz);  /* Q( -rshifts ) */
                    nrg = Inlines.silk_ADD_LSHIFT32(nrg, Inlines.silk_SMMUL(Inlines.silk_ADD32(CAb[k + 1], CAf[k + 1]),
                                                                                        Atmp1), 32 - QA - lz);    /* Q( 1-rshifts ) */
                }
                CAf[n + 1] = tmp1;                                                                            /* Q( -rshifts ) */
                CAb[n + 1] = tmp2;                                                                            /* Q( -rshifts ) */
                num = Inlines.silk_ADD32(num, tmp2);                                                                  /* Q( -rshifts ) */
                num = Inlines.silk_LSHIFT32(-num, 1);                                                                 /* Q( 1-rshifts ) */

                /* Calculate the next order reflection (parcor) coefficient */
                if (Inlines.silk_abs(num) < nrg)
                {
                    rc_Q31 = Inlines.silk_DIV32_varQ(num, nrg, 31);
                }
                else {
                    rc_Q31 = (num > 0) ? int.MaxValue : int.MinValue;
                }

                /* Update inverse prediction gain */
                tmp1 = ((int)1 << 30) - Inlines.silk_SMMUL(rc_Q31, rc_Q31);
                tmp1 = Inlines.silk_LSHIFT(Inlines.silk_SMMUL(invGain_Q30, tmp1), 2);
                if (tmp1 <= minInvGain_Q30)
                {
                    /* Max prediction gain exceeded; set reflection coefficient such that max prediction gain is exactly hit */
                    tmp2 = ((int)1 << 30) - Inlines.silk_DIV32_varQ(minInvGain_Q30, invGain_Q30, 30);            /* Q30 */
                    rc_Q31 = Inlines.silk_SQRT_APPROX(tmp2);                                                  /* Q15 */
                                                                                                      /* Newton-Raphson iteration */
                    rc_Q31 = Inlines.silk_RSHIFT32(rc_Q31 + Inlines.silk_DIV32(tmp2, rc_Q31), 1);                   /* Q15 */
                    rc_Q31 = Inlines.silk_LSHIFT32(rc_Q31, 16);                                               /* Q31 */
                    if (num < 0)
                    {
                        /* Ensure adjusted reflection coefficients has the original sign */
                        rc_Q31 = -rc_Q31;
                    }
                    invGain_Q30 = minInvGain_Q30;
                    reached_max_gain = 1;
                }
                else {
                    invGain_Q30 = tmp1;
                }

                /* Update the AR coefficients */
                for (k = 0; k < (n + 1) >> 1; k++)
                {
                    tmp1 = Af_QA[k];                                                                  /* QA */
                    tmp2 = Af_QA[n - k - 1];                                                          /* QA */
                    Af_QA[k] = Inlines.silk_ADD_LSHIFT32(tmp1, Inlines.silk_SMMUL(tmp2, rc_Q31), 1);      /* QA */
                    Af_QA[n - k - 1] = Inlines.silk_ADD_LSHIFT32(tmp2, Inlines.silk_SMMUL(tmp1, rc_Q31), 1);      /* QA */
                }
                Af_QA[n] = Inlines.silk_RSHIFT32(rc_Q31, 31 - QA);                                          /* QA */

                if (reached_max_gain != 0)
                {
                    /* Reached max prediction gain; set remaining coefficients to zero and exit loop */
                    for (k = n + 1; k < D; k++)
                    {
                        Af_QA[k] = 0;
                    }
                    break;
                }

                /* Update C * Af and C * Ab */
                for (k = 0; k <= n + 1; k++)
                {
                    tmp1 = CAf[k];                                                                    /* Q( -rshifts ) */
                    tmp2 = CAb[n - k + 1];                                                            /* Q( -rshifts ) */
                    CAf[k] = Inlines.silk_ADD_LSHIFT32(tmp1, Inlines.silk_SMMUL(tmp2, rc_Q31), 1);        /* Q( -rshifts ) */
                    CAb[n - k + 1] = Inlines.silk_ADD_LSHIFT32(tmp2, Inlines.silk_SMMUL(tmp1, rc_Q31), 1);        /* Q( -rshifts ) */
                }
            }

            if (reached_max_gain != 0)
            {
                for (k = 0; k < D; k++)
                {
                    /* Scale coefficients */
                    A_Q16[k] = -Inlines.silk_RSHIFT_ROUND(Af_QA[k], QA - 16);
                }
                /* Subtract energy of preceding samples from C0 */
                if (rshifts > 0)
                {
                    for (s = 0; s < nb_subfr; s++)
                    {
                        x_ptr = x.Point(s * subfr_length);
                        C0 -= (int)Inlines.silk_RSHIFT64(Inlines.silk_inner_prod16_aligned_64_c(x_ptr, x_ptr, D), rshifts);
                    }
                }
                else {
                    for (s = 0; s < nb_subfr; s++)
                    {
                        x_ptr = x.Point(s * subfr_length);
                        C0 -= Inlines.silk_LSHIFT32(Inlines.silk_inner_prod_aligned(x_ptr, x_ptr, D, arch), -rshifts);
                    }
                }
                /* Approximate residual energy */
                res_nrg.Val = Inlines.silk_LSHIFT(Inlines.silk_SMMUL(invGain_Q30, C0), 2);
                res_nrg_Q.Val = 0 - rshifts;
            }
            else {
                /* Return residual energy */
                nrg = CAf[0];                                                                            /* Q( -rshifts ) */
                tmp1 = (int)1 << 16;                                                                             /* Q16 */
                for (k = 0; k < D; k++)
                {
                    Atmp1 = Inlines.silk_RSHIFT_ROUND(Af_QA[k], QA - 16);                                       /* Q16 */
                    nrg = Inlines.silk_SMLAWW(nrg, CAf[k + 1], Atmp1);                                         /* Q( -rshifts ) */
                    tmp1 = Inlines.silk_SMLAWW(tmp1, Atmp1, Atmp1);                                               /* Q16 */
                    A_Q16[k] = -Atmp1;
                }
                res_nrg.Val = Inlines.silk_SMLAWW(nrg, Inlines.silk_SMMUL(Inlines.SILK_FIX_CONST(TuningParameters.FIND_LPC_COND_FAC, 32), C0), -tmp1);/* Q( -rshifts ) */
                res_nrg_Q.Val = -rshifts;
            }
        }

    }
}
