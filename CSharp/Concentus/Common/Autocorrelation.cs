﻿/* Copyright (c) 2006-2011 Skype Limited. All Rights Reserved
   Ported to C# by Logan Stromberg

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

namespace Concentus.Common
{
    using Concentus.Celt;
    using Concentus.Common.CPlusPlus;
    using System;

    internal static class Autocorrelation
    {
        /* Compute autocorrelation */
        internal static void silk_autocorr(
            int[] results,           /* O    Result (length correlationCount)                            */
            out int scale,             /* O    Scaling of the correlation vector                           */
            short[] inputData,         /* I    Input data to correlate                                     */
            int inputDataSize,      /* I    Length of input                                             */
            int correlationCount   /* I    Number of correlation taps to compute                       */
        )
        {
            int corrCount = Inlines.silk_min_int(inputDataSize, correlationCount);
            scale = Autocorrelation._celt_autocorr(inputData, results, corrCount - 1, inputDataSize);
        }

        internal static int _celt_autocorr(
                  short[] x,   /*  in: [0...n-1] samples x   */
                   int[] ac,  /* out: [0...lag-1] ac values */
                   int lag,
                   int n
                  )
        {
            int d;
            int i, k;
            int fastN = n - lag;
            int shift;
            short[] xptr;
            short[] xx = new short[n];
            Inlines.OpusAssert(n > 0);
            xptr = x;

            shift = 0;
            {
                int ac0;
                ac0 = 1 + (n << 7);
                if ((n & 1) != 0)
                {
                    ac0 += Inlines.SHR32(Inlines.MULT16_16(xptr[0], xptr[0]), 9);
                }
                for (i = (n & 1); i < n; i += 2)
                {
                    ac0 += Inlines.SHR32(Inlines.MULT16_16(xptr[i], xptr[i]), 9);
                    ac0 += Inlines.SHR32(Inlines.MULT16_16(xptr[i + 1], xptr[i + 1]), 9);
                }
                shift = Inlines.celt_ilog2(ac0) - 30 + 10;
                shift = (shift) / 2;
                if (shift > 0)
                {
                    for (i = 0; i < n; i++)
                    {
                        xx[i] = (short)(Inlines.PSHR32(xptr[i], shift));
                    }
                    xptr = xx;
                }
                else
                    shift = 0;
            }
            CeltPitchXCorr.pitch_xcorr(xptr, 0, xptr, 0, ac, fastN, lag + 1);
            for (k = 0; k <= lag; k++)
            {
                for (i = k + fastN, d = 0; i < n; i++)
                    d = Inlines.MAC16_16(d, xptr[i], xptr[i - k]);
                ac[k] += d;
            }
            shift = 2 * shift;
            if (shift <= 0)
                ac[0] += Inlines.SHL32((int)1, -shift);
            if (ac[0] < 268435456)
            {
                int shift2 = 29 - Inlines.EC_ILOG((uint)ac[0]);
                for (i = 0; i <= lag; i++)
                {
                    ac[i] = Inlines.SHL32(ac[i], shift2);
                }
                shift -= shift2;
            }
            else if (ac[0] >= 536870912)
            {
                int shift2 = 1;
                if (ac[0] >= 1073741824)
                    shift2++;
                for (i = 0; i <= lag; i++)
                {
                    ac[i] = Inlines.SHR32(ac[i], shift2);
                }
                shift += shift2;
            }

            return shift;
        }

        internal static int _celt_autocorr(
                           int[] x,   /*  in: [0...n-1] samples x   */
                           int[] ac,  /* out: [0...lag-1] ac values */
                           int[] window,
                           int overlap,
                           int lag,
                           int n)
        {
            int d;
            int i, k;
            int fastN = n - lag;
            int shift;
            int[] actualX;
            int[] xx = new int[n];

            Inlines.OpusAssert(n > 0);
            Inlines.OpusAssert(overlap >= 0);

            if (overlap == 0)
            {
                actualX = x;
            }
            else
            {
                for (i = 0; i < n; i++)
                    xx[i] = x[i];
                for (i = 0; i < overlap; i++)
                {
                    xx[i] = Inlines.MULT16_16_Q15(x[i], window[i]);
                    xx[n - i - 1] = Inlines.MULT16_16_Q15(x[n - i - 1], window[i]);
                }

                actualX = xx;
            }

            shift = 0;

            int ac0;
            ac0 = 1 + (n << 7);
            if ((n & 1) != 0)
                ac0 += Inlines.SHR32(Inlines.MULT16_16(actualX[0], actualX[0]), 9);

            for (i = (n & 1); i < n; i += 2)
            {
                ac0 += Inlines.SHR32(Inlines.MULT16_16(actualX[i], actualX[i]), 9);
                ac0 += Inlines.SHR32(Inlines.MULT16_16(actualX[i + 1], actualX[i + 1]), 9);
            }

            shift = Inlines.celt_ilog2(ac0) - 30 + 10;
            shift = (shift) / 2;
            if (shift > 0)
            {
                for (i = 0; i < n; i++)
                    xx[i] = (Inlines.PSHR32(actualX[i], shift));
                actualX = xx;
            }
            else
                shift = 0;

            CeltPitchXCorr.pitch_xcorr(actualX, 0, actualX, 0, ac, fastN, lag + 1);
            for (k = 0; k <= lag; k++)
            {
                for (i = k + fastN, d = 0; i < n; i++)
                    d = Inlines.MAC16_16(d, actualX[i], actualX[i - k]);
                ac[k] += d;
            }

            shift = 2 * shift;
            if (shift <= 0)
                ac[0] += Inlines.SHL32((int)1, -shift);
            if (ac[0] < 268435456)
            {
                int shift2 = 29 - Inlines.EC_ILOG((uint)ac[0]);
                for (i = 0; i <= lag; i++)
                    ac[i] = Inlines.SHL32(ac[i], shift2);
                shift -= shift2;
            }
            else if (ac[0] >= 536870912)
            {
                int shift2 = 1;
                if (ac[0] >= 1073741824)
                    shift2++;
                for (i = 0; i <= lag; i++)
                    ac[i] = Inlines.SHR32(ac[i], shift2);
                shift += shift2;
            }

            return shift;
        }

        private const int QC = 10;
        private const int QS = 14;

        /* Autocorrelations for a warped frequency axis */
        internal static void silk_warped_autocorrelation(
                  int[] corr,                                  /* O    Result [order + 1]                                                          */
                  out int scale,                                 /* O    Scaling of the correlation vector                                           */
                    short[] input,                                 /* I    Input data to correlate                                                     */
                    int warping_Q16,                            /* I    Warping coefficient                                                         */
                    int length,                                 /* I    Length of input                                                             */
                    int order                                   /* I    Correlation order (even)                                                    */
                )
        {
            int n, i, lsh;
            int tmp1_QS, tmp2_QS;
            Span<int> state_QS = new int[order + 1];// = { 0 };
            Span<long> corr_QC = new long[order + 1];// = { 0 };

            /* Order must be even */
            Inlines.OpusAssert((order & 1) == 0);
            Inlines.OpusAssert(2 * QS - QC >= 0);
            
            /* Loop over samples */
            for (n = 0; n < length; n++)
            {
                tmp1_QS = Inlines.silk_LSHIFT32((int)input[n], QS);
                /* Loop over allpass sections */
                for (i = 0; i < order; i += 2)
                {
                    /* Output of allpass section */
                    tmp2_QS = Inlines.silk_SMLAWB(state_QS[i], state_QS[i + 1] - tmp1_QS, warping_Q16);
                    state_QS[i] = tmp1_QS;
                    corr_QC[i] += Inlines.silk_RSHIFT64(Inlines.silk_SMULL(tmp1_QS, state_QS[0]), 2 * QS - QC);
                    /* Output of allpass section */
                    tmp1_QS = Inlines.silk_SMLAWB(state_QS[i + 1], state_QS[i + 2] - tmp2_QS, warping_Q16);
                    state_QS[i + 1] = tmp2_QS;
                    corr_QC[i + 1] += Inlines.silk_RSHIFT64(Inlines.silk_SMULL(tmp2_QS, state_QS[0]), 2 * QS - QC);
                }
                state_QS[order] = tmp1_QS;
                corr_QC[order] += Inlines.silk_RSHIFT64(Inlines.silk_SMULL(tmp1_QS, state_QS[0]), 2 * QS - QC);
            }

            lsh = Inlines.silk_CLZ64(corr_QC[0]) - 35;
            lsh = Inlines.silk_LIMIT(lsh, -12 - QC, 30 - QC);
            scale = -(QC + lsh);
            Inlines.OpusAssert(scale >= -30 && scale <= 12);
            if (lsh >= 0)
            {
                for (i = 0; i < order + 1; i++)
                {
                    corr[i] = (int)(Inlines.silk_LSHIFT64(corr_QC[i], lsh));
                }
            }
            else {
                for (i = 0; i < order + 1; i++)
                {
                    corr[i] = (int)(Inlines.silk_RSHIFT64(corr_QC[i], -lsh));
                }
            }
            Inlines.OpusAssert(corr_QC[0] >= 0); /* If breaking, decrease QC*/
        }
    }
}
