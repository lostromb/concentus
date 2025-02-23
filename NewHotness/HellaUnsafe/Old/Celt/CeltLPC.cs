/* Copyright (c) 2009-2010 Xiph.Org Foundation
   Written by Jean-Marc Valin */
/*
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

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

using static HellaUnsafe.Old.Celt.Arch;
using static HellaUnsafe.Old.Celt.MathOps;
using static HellaUnsafe.Old.Celt.Pitch;
using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Old.Celt
{
    internal static class CeltLPC
    {
        internal const int CELT_LPC_ORDER = 24;

        internal static unsafe void _celt_lpc(
            float* _lpc, /* out: [0...p-1] LPC coefficients      */
            in float* ac,  /* in:  [0...p] autocorrelation values  */
            int p)
        {
            int i, j;
            float r;
            float error = ac[0];
            float* lpc = _lpc;

            OPUS_CLEAR(lpc, p);
            if (ac[0] > 1e-10f)
            {
                for (i = 0; i < p; i++)
                {
                    /* Sum up this iteration's reflection coefficient */
                    float rr = 0;
                    for (j = 0; j < i; j++)
                        rr += MULT32_32_Q31(lpc[j], ac[i - j]);
                    rr += SHR32(ac[i + 1], 6);
                    r = -frac_div32(SHL32(rr, 6), error);
                    /*  Update LPC coefficients and total error */
                    lpc[i] = SHR32(r, 6);
                    for (j = 0; j < i + 1 >> 1; j++)
                    {
                        float tmp1, tmp2;
                        tmp1 = lpc[j];
                        tmp2 = lpc[i - 1 - j];
                        lpc[j] = tmp1 + MULT32_32_Q31(r, tmp2);
                        lpc[i - 1 - j] = tmp2 + MULT32_32_Q31(r, tmp1);
                    }

                    error = error - MULT32_32_Q31(MULT32_32_Q31(r, r), error);
                    /* Bail out once we get 30 dB gain */
                    if (error <= .001f * ac[0])
                        break;
                }
            }
        }

        private const int SIG_SHIFT = 0;

        internal static unsafe void celt_fir(
            in float* x,
            in float* num,
            float* y,
            int N,
            int ord)
        {
            celt_fir_c(x, num, y, N, ord);
        }

        internal static unsafe void celt_fir_c(
            in float* x,
            in float* num,
            float* y,
            int N,
            int ord)
        {
            int i, j;
            ASSERT(x != y);
            float[] scratch_buf = new float[ord + 4];
            fixed (float* scratch_ptr = scratch_buf)
            {
                float* rnum = scratch_ptr;
                float* sums = scratch_ptr + ord;
                for (i = 0; i < ord; i++)
                    rnum[i] = num[ord - i - 1];
                for (i = 0; i < N - 3; i += 4)
                {
                    sums[0] = SHL32(EXTEND32(x[i]), SIG_SHIFT);
                    sums[1] = SHL32(EXTEND32(x[i + 1]), SIG_SHIFT);
                    sums[2] = SHL32(EXTEND32(x[i + 2]), SIG_SHIFT);
                    sums[3] = SHL32(EXTEND32(x[i + 3]), SIG_SHIFT);
                    xcorr_kernel(rnum, x + i - ord, sums, ord);
                    y[i] = SROUND16(sums[0], SIG_SHIFT);
                    y[i + 1] = SROUND16(sums[1], SIG_SHIFT);
                    y[i + 2] = SROUND16(sums[2], SIG_SHIFT);
                    y[i + 3] = SROUND16(sums[3], SIG_SHIFT);
                }
                for (; i < N; i++)
                {
                    float sum = SHL32(EXTEND32(x[i]), SIG_SHIFT);
                    for (j = 0; j < ord; j++)
                        sum = MAC16_16(sum, rnum[j], x[i + j - ord]);
                    y[i] = SROUND16(sum, SIG_SHIFT);
                }
            }
        }

        internal static unsafe void celt_iir(in float* _x,
            in float* den,
            float* _y,
            int N,
            int ord,
            float* mem)
        {
            int i, j;
            float[] scratch_buf = new float[4 + (N + 1) * ord];
            fixed (float* scratch_ptr = scratch_buf)
            {
                ASSERT((ord & 3) == 0);
                float* sums = scratch_ptr;
                float* rden = sums + 4;
                float* y = rden + ord;
                for (i = 0; i < ord; i++)
                    rden[i] = den[ord - i - 1];
                for (i = 0; i < ord; i++)
                    y[i] = -mem[ord - i - 1];
                for (; i < N + ord; i++)
                    y[i] = 0;
                for (i = 0; i < N - 3; i += 4)
                {
                    /* Unroll by 4 as if it were an FIR filter */
                    sums[0] = _x[i];
                    sums[1] = _x[i + 1];
                    sums[2] = _x[i + 2];
                    sums[3] = _x[i + 3];
                    xcorr_kernel(rden, y + i, sums, ord);
                    /* Patch up the result to compensate for the fact that this is an IIR */
                    y[i + ord] = -SROUND16(sums[0], SIG_SHIFT);
                    _y[i] = sums[0];
                    sums[1] = MAC16_16(sums[1], y[i + ord], den[0]);
                    y[i + ord + 1] = -SROUND16(sums[1], SIG_SHIFT);
                    _y[i + 1] = sums[1];
                    sums[2] = MAC16_16(sums[2], y[i + ord + 1], den[0]);
                    sums[2] = MAC16_16(sums[2], y[i + ord], den[1]);
                    y[i + ord + 2] = -SROUND16(sums[2], SIG_SHIFT);
                    _y[i + 2] = sums[2];

                    sums[3] = MAC16_16(sums[3], y[i + ord + 2], den[0]);
                    sums[3] = MAC16_16(sums[3], y[i + ord + 1], den[1]);
                    sums[3] = MAC16_16(sums[3], y[i + ord], den[2]);
                    y[i + ord + 3] = -SROUND16(sums[3], SIG_SHIFT);
                    _y[i + 3] = sums[3];
                }
                for (; i < N; i++)
                {
                    float sum = _x[i];
                    for (j = 0; j < ord; j++)
                        sum -= MULT16_16(rden[j], y[i + j]);
                    y[i + ord] = SROUND16(sum, SIG_SHIFT);
                    _y[i] = sum;
                }
                for (i = 0; i < ord; i++)
                    mem[i] = _y[N - i - 1];
            }
        }

        internal static unsafe int _celt_autocorr(
            in float* x,   /*  in: [0...n-1] samples x   */
            float* ac,  /* out: [0...lag-1] ac values */
            in float* window,
            int overlap,
            int lag,
            int n)
        {
            float d;
            int i, k;
            int fastN = n - lag;
            int shift;
            float* xptr;
            float[] xx_buf = new float[n];
            fixed (float* xx = xx_buf)
            {
                ASSERT(n > 0);
                ASSERT(overlap >= 0);
                if (overlap == 0)
                {
                    xptr = x;
                }
                else
                {
                    for (i = 0; i < n; i++)
                        xx[i] = x[i];
                    for (i = 0; i < overlap; i++)
                    {
                        xx[i] = MULT16_16_Q15(x[i], window[i]);
                        xx[n - i - 1] = MULT16_16_Q15(x[n - i - 1], window[i]);
                    }
                    xptr = xx;
                }
                shift = 0;

                celt_pitch_xcorr(xptr, xptr, ac, fastN, lag + 1);
                for (k = 0; k <= lag; k++)
                {
                    for (i = k + fastN, d = 0; i < n; i++)
                        d = MAC16_16(d, xptr[i], xptr[i - k]);
                    ac[k] += d;
                }

                return shift;
            }
        }
    }
}
