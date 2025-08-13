﻿/***********************************************************************
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
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Inlines;

namespace HellaUnsafe.Silk
{
    internal static unsafe class LPCInvPredGain
    {
        private const int QA = 24;
        private static readonly int A_LIMIT = SILK_FIX_CONST(0.99975, QA);

        private static int MUL32_FRAC_Q(int a32, int b32, int Q)
        {
            return ((int)(silk_RSHIFT_ROUND64(silk_SMULL(a32, b32), Q)));
        }

        /* Compute inverse of LPC prediction gain, and                          */
        /* test if LPC coefficients are stable (all poles within unit circle)   */
        internal static unsafe int LPC_inverse_pred_gain_QA(               /* O   Returns inverse prediction gain in energy domain, Q30    */
            int* A_QA/*[SILK_MAX_ORDER_LPC]*/,        /* I   Prediction coefficients                                  */
            in int order                              /* I   Prediction order                                         */
        )
        {
            int k, n, mult2Q;
            int invGain_Q30, rc_Q31, rc_mult1_Q30, rc_mult2, tmp1, tmp2;

            invGain_Q30 = SILK_FIX_CONST(1, 30);
            for (k = order - 1; k > 0; k--)
            {
                /* Check for stability */
                if ((A_QA[k] > A_LIMIT) || (A_QA[k] < -A_LIMIT))
                {
                    return 0;
                }

                /* Set RC equal to negated AR coef */
                rc_Q31 = -silk_LSHIFT(A_QA[k], 31 - QA);

                /* rc_mult1_Q30 range: [ 1 : 2^30 ] */
                rc_mult1_Q30 = silk_SUB32(SILK_FIX_CONST(1, 30), silk_SMMUL(rc_Q31, rc_Q31));
                silk_assert(rc_mult1_Q30 > (1 << 15));                   /* reduce A_LIMIT if fails */
                silk_assert(rc_mult1_Q30 <= (1 << 30));

                /* Update inverse gain */
                /* invGain_Q30 range: [ 0 : 2^30 ] */
                invGain_Q30 = silk_LSHIFT(silk_SMMUL(invGain_Q30, rc_mult1_Q30), 2);
                silk_assert(invGain_Q30 >= 0);
                silk_assert(invGain_Q30 <= (1 << 30));
                if (invGain_Q30 < SILK_FIX_CONST(1.0f / MAX_PREDICTION_POWER_GAIN, 30))
                {
                    return 0;
                }

                /* rc_mult2 range: [ 2^30 : silk_int32_MAX ] */
                mult2Q = 32 - silk_CLZ32(silk_abs(rc_mult1_Q30));
                rc_mult2 = silk_INVERSE32_varQ(rc_mult1_Q30, mult2Q + 30);

                /* Update AR coefficient */
                for (n = 0; n < (k + 1) >> 1; n++)
                {
                    long tmp64;
                    tmp1 = A_QA[n];
                    tmp2 = A_QA[k - n - 1];
                    tmp64 = silk_RSHIFT_ROUND64(silk_SMULL(silk_SUB_SAT32(tmp1,
                          MUL32_FRAC_Q(tmp2, rc_Q31, 31)), rc_mult2), mult2Q);
                    if (tmp64 > silk_int32_MAX || tmp64 < silk_int32_MIN)
                    {
                        return 0;
                    }
                    A_QA[n] = (int)tmp64;
                    tmp64 = silk_RSHIFT_ROUND64(silk_SMULL(silk_SUB_SAT32(tmp2,
                          MUL32_FRAC_Q(tmp1, rc_Q31, 31)), rc_mult2), mult2Q);
                    if (tmp64 > silk_int32_MAX || tmp64 < silk_int32_MIN)
                    {
                        return 0;
                    }
                    A_QA[k - n - 1] = (int)tmp64;
                }
            }

            /* Check for stability */
            if ((A_QA[k] > A_LIMIT) || (A_QA[k] < -A_LIMIT))
            {
                return 0;
            }

            /* Set RC equal to negated AR coef */
            rc_Q31 = -silk_LSHIFT(A_QA[0], 31 - QA);

            /* Range: [ 1 : 2^30 ] */
            rc_mult1_Q30 = silk_SUB32(SILK_FIX_CONST(1, 30), silk_SMMUL(rc_Q31, rc_Q31));

            /* Update inverse gain */
            /* Range: [ 0 : 2^30 ] */
            invGain_Q30 = silk_LSHIFT(silk_SMMUL(invGain_Q30, rc_mult1_Q30), 2);
            silk_assert(invGain_Q30 >= 0);
            silk_assert(invGain_Q30 <= (1 << 30));
            if (invGain_Q30 < SILK_FIX_CONST(1.0f / MAX_PREDICTION_POWER_GAIN, 30))
            {
                return 0;
            }

            return invGain_Q30;
        }

        /* For input in Q12 domain */
        internal static unsafe int silk_LPC_inverse_pred_gain(            /* O   Returns inverse prediction gain in energy domain, Q30        */
            in short* A_Q12,             /* I   Prediction coefficients, Q12 [order]                         */
            in int order               /* I   Prediction order                                             */
        )
        {
            int k;
            int* Atmp_QA = stackalloc int[SILK_MAX_ORDER_LPC];
            int DC_resp = 0;

            /* Increase Q domain of the AR coefficients */
            for (k = 0; k < order; k++)
            {
                DC_resp += (int)A_Q12[k];
                Atmp_QA[k] = silk_LSHIFT32((int)A_Q12[k], QA - 12);
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
