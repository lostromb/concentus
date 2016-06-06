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
    public static class Filters
    {
        /// <summary>
        /// Second order ARMA filter, alternative implementation
        /// </summary>
        /// <param name="input">I     input signal</param>
        /// <param name="B_Q28">I     MA coefficients [3]</param>
        /// <param name="A_Q28">I     AR coefficients [2]</param>
        /// <param name="S">I/O   State vector [2]</param>
        /// <param name="output">O     output signal</param>
        /// <param name="len">I     signal length (must be even)</param>
        /// <param name="stride">I     Operate on interleaved signal if > 1</param>
        public static void silk_biquad_alt(
            Pointer<short> input,
            Pointer<int> B_Q28,
            Pointer<int> A_Q28,
            Pointer<int> S,
            Pointer<short> output,
            int len,
            int stride)
        {
            /* DIRECT FORM II TRANSPOSED (uses 2 element state vector) */
            int k;
            int inval, A0_U_Q28, A0_L_Q28, A1_U_Q28, A1_L_Q28, out32_Q14;

            /* Negate A_Q28 values and split in two parts */
            A0_L_Q28 = (-A_Q28[0]) & 0x00003FFF;        /* lower part */
            A0_U_Q28 = Inlines.silk_RSHIFT(-A_Q28[0], 14);      /* upper part */
            A1_L_Q28 = (-A_Q28[1]) & 0x00003FFF;        /* lower part */
            A1_U_Q28 = Inlines.silk_RSHIFT(-A_Q28[1], 14);      /* upper part */

            for (k = 0; k < len; k++)
            {
                /* S[ 0 ], S[ 1 ]: Q12 */
                inval = input[k * stride];
                out32_Q14 = Inlines.silk_LSHIFT(Inlines.silk_SMLAWB(S[0], B_Q28[0], inval), 2);

                S[0] = S[1] + Inlines.silk_RSHIFT_ROUND(Inlines.silk_SMULWB(out32_Q14, A0_L_Q28), 14);
                S[0] = Inlines.silk_SMLAWB(S[0], out32_Q14, A0_U_Q28);
                S[0] = Inlines.silk_SMLAWB(S[0], B_Q28[1], inval);

                S[1] = Inlines.silk_RSHIFT_ROUND(Inlines.silk_SMULWB(out32_Q14, A1_L_Q28), 14);
                S[1] = Inlines.silk_SMLAWB(S[1], out32_Q14, A1_U_Q28);
                S[1] = Inlines.silk_SMLAWB(S[1], B_Q28[2], inval);

                /* Scale back to Q0 and saturate */
                output[k * stride] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT(out32_Q14 + (1 << 14) - 1, 14));
            }
        }

        /// <summary>
        /// FIXME: TEMPORARY
        /// </summary>
        public static void silk_biquad_alt(
            Pointer<int> input,
            Pointer<int> B_Q28,
            Pointer<int> A_Q28,
            Pointer<int> S,
            Pointer<int> output,
            int len,
            int stride)
        {
            /* DIRECT FORM II TRANSPOSED (uses 2 element state vector) */
            int k;
            int inval, A0_U_Q28, A0_L_Q28, A1_U_Q28, A1_L_Q28, out32_Q14;

            /* Negate A_Q28 values and split in two parts */
            A0_L_Q28 = (-A_Q28[0]) & 0x00003FFF;        /* lower part */
            A0_U_Q28 = Inlines.silk_RSHIFT(-A_Q28[0], 14);      /* upper part */
            A1_L_Q28 = (-A_Q28[1]) & 0x00003FFF;        /* lower part */
            A1_U_Q28 = Inlines.silk_RSHIFT(-A_Q28[1], 14);      /* upper part */

            for (k = 0; k < len; k++)
            {
                /* S[ 0 ], S[ 1 ]: Q12 */
                inval = input[k * stride];
                out32_Q14 = Inlines.silk_LSHIFT(Inlines.silk_SMLAWB(S[0], B_Q28[0], inval), 2);

                S[0] = S[1] + Inlines.silk_RSHIFT_ROUND(Inlines.silk_SMULWB(out32_Q14, A0_L_Q28), 14);
                S[0] = Inlines.silk_SMLAWB(S[0], out32_Q14, A0_U_Q28);
                S[0] = Inlines.silk_SMLAWB(S[0], B_Q28[1], inval);

                S[1] = Inlines.silk_RSHIFT_ROUND(Inlines.silk_SMULWB(out32_Q14, A1_L_Q28), 14);
                S[1] = Inlines.silk_SMLAWB(S[1], out32_Q14, A1_U_Q28);
                S[1] = Inlines.silk_SMLAWB(S[1], B_Q28[2], inval);

                /* Scale back to Q0 and saturate */
                output[k * stride] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT(out32_Q14 + (1 << 14) - 1, 14));
            }
        }

        /* Coefficients for 2-band filter bank based on first-order allpass filters */
        private readonly static short A_fb1_20 = 5394 << 1;
        private readonly static short A_fb1_21 = -24290; /* (opus_int16)(20623 << 1) */

        /// <summary>
        /// Split signal into two decimated bands using first-order allpass filters
        /// </summary>
        /// <param name="input">I    Input signal [N]</param>
        /// <param name="S">I/O  State vector [2]</param>
        /// <param name="outL">O    Low band [N/2]</param>
        /// <param name="outH">O    High band [N/2]</param>
        /// <param name="N">I    Number of input samples</param>
        public static void silk_ana_filt_bank_1(
            Pointer<short> input,
            Pointer<int> S,
            Pointer<short> outL,
            Pointer<short> outH,
            int N)
        {
            int k, N2 = Inlines.silk_RSHIFT(N, 1);
            int in32, X, Y, out_1, out_2;

            /* Internal variables and state are in Q10 format */
            for (k = 0; k < N2; k++)
            {
                /* Convert to Q10 */
                in32 = Inlines.silk_LSHIFT((int)input[2 * k], 10);

                /* All-pass section for even input sample */
                Y = Inlines.silk_SUB32(in32, S[0]);
                X = Inlines.silk_SMLAWB(Y, Y, A_fb1_21);
                out_1 = Inlines.silk_ADD32(S[0], X);
                S[0] = Inlines.silk_ADD32(in32, X);

                /* Convert to Q10 */
                in32 = Inlines.silk_LSHIFT((int)input[2 * k + 1], 10);

                /* All-pass section for odd input sample, and add to output of previous section */
                Y = Inlines.silk_SUB32(in32, S[1]);
                X = Inlines.silk_SMULWB(Y, A_fb1_20);
                out_2 = Inlines.silk_ADD32(S[1], X);
                S[1] = Inlines.silk_ADD32(in32, X);

                /* Add/subtract, convert back to int16 and store to output */
                outL[k] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT_ROUND(Inlines.silk_ADD32(out_2, out_1), 11));
                outH[k] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT_ROUND(Inlines.silk_SUB32(out_2, out_1), 11));
            }
        }

        /// <summary>
        /// Chirp (bandwidth expand) LP AR filter
        /// </summary>
        /// <param name="ar">I/O  AR filter to be expanded (without leading 1)</param>
        /// <param name="d">I    Length of ar</param>
        /// <param name="chirp_Q16">I    Chirp factor (typically in the range 0 to 1) FIXME Should this be an int?</param>
        public static void silk_bwexpander(
            Pointer<short> ar,
            int d,
            int chirp_Q16)
        {
            int i;
            int chirp_minus_one_Q16 = chirp_Q16 - 65536;

            /* NB: Dont use silk_SMULWB, instead of silk_RSHIFT_ROUND( silk_MUL(), 16 ), below.  */
            /* Bias in silk_SMULWB can lead to unstable filters                                */
            for (i = 0; i < d - 1; i++)
            {
                ar[i] = (short)Inlines.silk_RSHIFT_ROUND(Inlines.silk_MUL(chirp_Q16, ar[i]), 16);
                chirp_Q16 += Inlines.silk_RSHIFT_ROUND(Inlines.silk_MUL(chirp_Q16, chirp_minus_one_Q16), 16);
            }

            ar[d - 1] = (short)Inlines.silk_RSHIFT_ROUND(Inlines.silk_MUL(chirp_Q16, ar[d - 1]), 16);
        }

        /// <summary>
        /// Chirp (bandwidth expand) LP AR filter
        /// </summary>
        /// <param name="ar">I/O  AR filter to be expanded (without leading 1)</param>
        /// <param name="d">I    Length of ar</param>
        /// <param name="chirp_Q16">I    Chirp factor in Q16</param>
        public static void silk_bwexpander_32(Pointer<int> ar, int d, int chirp_Q16)
        {
            int i;
            int chirp_minus_one_Q16 = chirp_Q16 - 65536;

            for (i = 0; i < d - 1; i++)
            {
                ar[i] = Inlines.silk_SMULWW(chirp_Q16, ar[i]);
                chirp_Q16 += Inlines.silk_RSHIFT_ROUND(Inlines.silk_MUL(chirp_Q16, chirp_minus_one_Q16), 16);
            }

            ar[d - 1] = Inlines.silk_SMULWW(chirp_Q16, ar[d - 1]);
        }

        /// <summary>
        /// Elliptic/Cauer filters designed with 0.1 dB passband ripple,
        /// 80 dB minimum stopband attenuation, and
        /// [0.95 : 0.15 : 0.35] normalized cut off frequencies.
        /// Helper function, interpolates the filter taps
        /// </summary>
        /// <param name="B_Q28">order [TRANSITION_NB]</param>
        /// <param name="A_Q28">order [TRANSITION_NA]</param>
        /// <param name="ind"></param>
        /// <param name="fac_Q16"></param>
        public static void silk_LP_interpolate_filter_taps(
            Pointer<int> B_Q28,
            Pointer<int> A_Q28,
            int ind,
            int fac_Q16)
        {
            int nb, na;

            if (ind < SilkConstants.TRANSITION_INT_NUM - 1)
            {
                if (fac_Q16 > 0)
                {
                    if (fac_Q16 < 32768)
                    {
                        /* fac_Q16 is in range of a 16-bit int */
                        /* Piece-wise linear interpolation of B and A */
                        for (nb = 0; nb < SilkConstants.TRANSITION_NB; nb++)
                        {
                            B_Q28[nb] = Inlines.silk_SMLAWB(
                                Tables.silk_Transition_LP_B_Q28[ind][nb],
                                Tables.silk_Transition_LP_B_Q28[ind + 1][nb] -
                                    Tables.silk_Transition_LP_B_Q28[ind][nb],
                                fac_Q16);
                        }

                        for (na = 0; na < SilkConstants.TRANSITION_NA; na++)
                        {
                            A_Q28[na] = Inlines.silk_SMLAWB(
                                Tables.silk_Transition_LP_A_Q28[ind][na],
                                Tables.silk_Transition_LP_A_Q28[ind + 1][na] -
                                    Tables.silk_Transition_LP_A_Q28[ind][na],
                                fac_Q16);
                        }
                    }
                    else
                    {
                        /* ( fac_Q16 - ( 1 << 16 ) ) is in range of a 16-bit int */
                        Inlines.OpusAssert(fac_Q16 - (1 << 16) == Inlines.silk_SAT16(fac_Q16 - (1 << 16)));

                        /* Piece-wise linear interpolation of B and A */

                        for (nb = 0; nb < SilkConstants.TRANSITION_NB; nb++)
                        {
                            B_Q28[nb] = Inlines.silk_SMLAWB(
                                Tables.silk_Transition_LP_B_Q28[ind + 1][nb],
                                Tables.silk_Transition_LP_B_Q28[ind + 1][nb] -
                                    Tables.silk_Transition_LP_B_Q28[ind][nb],
                                fac_Q16 - ((int)1 << 16));
                        }

                        for (na = 0; na < SilkConstants.TRANSITION_NA; na++)
                        {
                            A_Q28[na] = Inlines.silk_SMLAWB(
                                Tables.silk_Transition_LP_A_Q28[ind + 1][na],
                                Tables.silk_Transition_LP_A_Q28[ind + 1][na] -
                                    Tables.silk_Transition_LP_A_Q28[ind][na],
                                fac_Q16 - ((int)1 << 16));
                        }
                    }
                }
                else
                {
                    B_Q28.MemCopyFrom(Tables.silk_Transition_LP_B_Q28[ind], 0, SilkConstants.TRANSITION_NB);
                    A_Q28.MemCopyFrom(Tables.silk_Transition_LP_A_Q28[ind], 0, SilkConstants.TRANSITION_NA);
                }
            }
            else
            {
                B_Q28.MemCopyFrom(Tables.silk_Transition_LP_B_Q28[SilkConstants.TRANSITION_INT_NUM - 1], 0, SilkConstants.TRANSITION_NB);
                A_Q28.MemCopyFrom(Tables.silk_Transition_LP_A_Q28[SilkConstants.TRANSITION_INT_NUM - 1], 0, SilkConstants.TRANSITION_NA);
            }
        }

        /* Low-pass filter with variable cutoff frequency based on  */
        /* piece-wise linear interpolation between elliptic filters */
        /* Start by setting psEncC.mode <> 0;                      */
        /* Deactivate by setting psEncC.mode = 0;                  */
        public static void silk_LP_variable_cutoff(
            silk_LP_state psLP,                          /* I/O  LP filter state                             */
            Pointer<short> frame,                         /* I/O  Low-pass filtered output signal             */
            int frame_length                    /* I    Frame length                                */
            )
        {
            Pointer<int> B_Q28 = Pointer.Malloc<int>(SilkConstants.TRANSITION_NB);
            Pointer<int> A_Q28 = Pointer.Malloc<int>(SilkConstants.TRANSITION_NA);
            int fac_Q16 = 0;
            int ind = 0;

            Inlines.OpusAssert(psLP.transition_frame_no >= 0 && psLP.transition_frame_no <= SilkConstants.TRANSITION_FRAMES);

            /* Run filter if needed */
            if (psLP.mode != 0)
            {
                /* Calculate index and interpolation factor for interpolation */
                fac_Q16 = Inlines.silk_LSHIFT(SilkConstants.TRANSITION_FRAMES - psLP.transition_frame_no, 16 - 6);

                ind = Inlines.silk_RSHIFT(fac_Q16, 16);
                fac_Q16 -= Inlines.silk_LSHIFT(ind, 16);

                Inlines.OpusAssert(ind >= 0);
                Inlines.OpusAssert(ind < SilkConstants.TRANSITION_INT_NUM);

                /* Interpolate filter coefficients */
                silk_LP_interpolate_filter_taps(B_Q28, A_Q28, ind, fac_Q16);

                /* Update transition frame number for next frame */
                psLP.transition_frame_no = Inlines.silk_LIMIT(psLP.transition_frame_no + psLP.mode, 0, SilkConstants.TRANSITION_FRAMES);

                /* ARMA low-pass filtering */
                Inlines.OpusAssert(SilkConstants.TRANSITION_NB == 3 && SilkConstants.TRANSITION_NA == 2);
                silk_biquad_alt(frame, B_Q28, A_Q28, psLP.In_LP_State, frame, frame_length, 1);
            }
        }

        /// <summary>
        /// LPC analysis filter
        /// NB! State is kept internally and the
        /// filter always starts with zero state
        /// first d output samples are set to zero
        /// </summary>
        /// <param name="output">O    Output signal</param>
        /// <param name="input">I    Input signal</param>
        /// <param name="B">I    MA prediction coefficients, Q12 [order]</param>
        /// <param name="len">I    Signal length</param>
        /// <param name="d">I    Filter order</param>
        /// <param name="arch">I    Run-time architecture</param>
        public static void silk_LPC_analysis_filter(
                    Pointer<short> output,
                    Pointer<short> input,
                    Pointer<short> B,
                    int len,
                    int d,
                    int arch)
        {
            int j;

            Inlines.OpusAssert(d >= 6);
            Inlines.OpusAssert((d & 1) == 0);
            Inlines.OpusAssert(d <= len);

            int ix;
            int out32_Q12, out32;
            Pointer<short> in_ptr;

            for (ix = d; ix < len; ix++)
            {
                in_ptr = input.Point(ix - 1);

                out32_Q12 = Inlines.silk_SMULBB(in_ptr[0], B[0]);
                /* Allowing wrap around so that two wraps can cancel each other. The rare
                   cases where the result wraps around can only be triggered by invalid streams*/
                out32_Q12 = Inlines.silk_SMLABB_ovflw(out32_Q12, in_ptr[-1], B[1]);
                out32_Q12 = Inlines.silk_SMLABB_ovflw(out32_Q12, in_ptr[-2], B[2]);
                out32_Q12 = Inlines.silk_SMLABB_ovflw(out32_Q12, in_ptr[-3], B[3]);
                out32_Q12 = Inlines.silk_SMLABB_ovflw(out32_Q12, in_ptr[-4], B[4]);
                out32_Q12 = Inlines.silk_SMLABB_ovflw(out32_Q12, in_ptr[-5], B[5]);

                for (j = 6; j < d; j += 2)
                {
                    out32_Q12 = Inlines.silk_SMLABB_ovflw(out32_Q12, in_ptr[-j], B[j]);
                    out32_Q12 = Inlines.silk_SMLABB_ovflw(out32_Q12, in_ptr[-j - 1], B[j + 1]);
                }

                /* Subtract prediction */
                out32_Q12 = Inlines.silk_SUB32_ovflw(Inlines.silk_LSHIFT((int)in_ptr[1], 12), out32_Q12);

                /* Scale to Q0 */
                out32 = Inlines.silk_RSHIFT_ROUND(out32_Q12, 12);

                /* Saturate output */
                output[ix] = (short)Inlines.silk_SAT16(out32);
            }

            /* Set first d output samples to zero */
            output.MemSet(0, d);
        }

        private const int QA = 24;
        private static readonly int A_LIMIT = Inlines.SILK_FIX_CONST(0.99975f, QA);

        /// <summary>
        /// Compute inverse of LPC prediction gain, and
        /// test if LPC coefficients are stable (all poles within unit circle)
        /// </summary>
        /// <param name="A_QA">Prediction coefficients, order [2][SILK_MAX_ORDER_LPC]</param>
        /// <param name="order">Prediction order</param>
        /// <returns>inverse prediction gain in energy domain, Q30</returns>
        public static int LPC_inverse_pred_gain_QA(
            Pointer<Pointer<int>> A_QA,
            int order)
        {
            int k, n, mult2Q;
            int invGain_Q30, rc_Q31, rc_mult1_Q30, rc_mult2, tmp_QA;
            Pointer<int> Aold_QA, Anew_QA;

            Anew_QA = A_QA[order & 1];

            invGain_Q30 = (int)1 << 30;
            for (k = order - 1; k > 0; k--)
            {
                /* Check for stability */
                if ((Anew_QA[k] > A_LIMIT) || (Anew_QA[k] < -A_LIMIT))
                {
                    return 0;
                }

                /* Set RC equal to negated AR coef */
                rc_Q31 = 0 - Inlines.silk_LSHIFT(Anew_QA[k], 31 - QA);

                /* rc_mult1_Q30 range: [ 1 : 2^30 ] */
                rc_mult1_Q30 = ((int)1 << 30) - Inlines.silk_SMMUL(rc_Q31, rc_Q31);
                Inlines.OpusAssert(rc_mult1_Q30 > (1 << 15));                   /* reduce A_LIMIT if fails */
                Inlines.OpusAssert(rc_mult1_Q30 <= (1 << 30));

                /* rc_mult2 range: [ 2^30 : silk_int32_MAX ] */
                mult2Q = 32 - Inlines.silk_CLZ32(Inlines.silk_abs(rc_mult1_Q30));
                rc_mult2 = Inlines.silk_INVERSE32_varQ(rc_mult1_Q30, mult2Q + 30);

                /* Update inverse gain */
                /* invGain_Q30 range: [ 0 : 2^30 ] */
                invGain_Q30 = Inlines.silk_LSHIFT(Inlines.silk_SMMUL(invGain_Q30, rc_mult1_Q30), 2);
                Inlines.OpusAssert(invGain_Q30 >= 0);
                Inlines.OpusAssert(invGain_Q30 <= (1 << 30));

                /* Swap pointers */
                Aold_QA = Anew_QA;
                Anew_QA = A_QA[k & 1];

                /* Update AR coefficient */
                for (n = 0; n < k; n++)
                {
                    tmp_QA = Aold_QA[n] - Inlines.MUL32_FRAC_Q(Aold_QA[k - n - 1], rc_Q31, 31);
                    Anew_QA[n] = Inlines.MUL32_FRAC_Q(tmp_QA, rc_mult2, mult2Q);
                }
            }

            /* Check for stability */
            if ((Anew_QA[0] > A_LIMIT) || (Anew_QA[0] < -A_LIMIT))
            {
                return 0;
            }

            /* Set RC equal to negated AR coef */
            rc_Q31 = 0 - Inlines.silk_LSHIFT(Anew_QA[0], 31 - QA);

            /* Range: [ 1 : 2^30 ] */
            rc_mult1_Q30 = ((int)1 << 30) - Inlines.silk_SMMUL(rc_Q31, rc_Q31);

            /* Update inverse gain */
            /* Range: [ 0 : 2^30 ] */
            invGain_Q30 = Inlines.silk_LSHIFT(Inlines.silk_SMMUL(invGain_Q30, rc_mult1_Q30), 2);
            Inlines.OpusAssert(invGain_Q30 >= 0);
            Inlines.OpusAssert(invGain_Q30 <= 1 << 30);

            return invGain_Q30;
        }

        /// <summary>
        /// For input in Q12 domain
        /// </summary>
        /// <param name="A_Q12">Prediction coefficients, Q12 [order]</param>
        /// <param name="order">I   Prediction order</param>
        /// <returns>inverse prediction gain in energy domain, Q30</returns>
        public static int silk_LPC_inverse_pred_gain(Pointer<short> A_Q12, int order)
        {
            int k;
            Pointer<Pointer<int>> Atmp_QA = Pointer.Malloc<Pointer<int>>(2);
            Atmp_QA[0] = Pointer.Malloc<int>(SilkConstants.SILK_MAX_ORDER_LPC);
            Atmp_QA[1] = Pointer.Malloc<int>(SilkConstants.SILK_MAX_ORDER_LPC);
            Pointer<int> Anew_QA;
            int DC_resp = 0;

            Anew_QA = Atmp_QA[order & 1];

            /* Increase Q domain of the AR coefficients */
            for (k = 0; k < order; k++)
            {
                DC_resp += (int)A_Q12[k];
                Anew_QA[k] = Inlines.silk_LSHIFT32((int)A_Q12[k], QA - 12);
            }

            /* If the DC is unstable, we don't even need to do the full calculations */
            if (DC_resp >= 4096)
            {
                return 0;
            }

            return LPC_inverse_pred_gain_QA(Atmp_QA, order);
        }
    }
}
