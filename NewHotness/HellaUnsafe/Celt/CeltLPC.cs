using System;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Common.CRuntime;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HellaUnsafe.Celt
{
    internal static unsafe class CeltLPC
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
                    for (j = 0; j < (i + 1) >> 1; j++)
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

        internal static unsafe void celt_fir(
            in float* x,
            in float* num,
            float* y,
            int N,
            int ord)
        {
            int i, j;
            ASSERT(x != y);
            float* rnum = stackalloc float[ord];
            for (i = 0; i < ord; i++)
                rnum[i] = num[ord - i - 1];
            float* sum = stackalloc float[4];
            for (i = 0; i < N - 3; i += 4)
            {
                sum[0] = SHL32(EXTEND32(x[i]), 0);
                sum[1] = SHL32(EXTEND32(x[i + 1]), 0);
                sum[2] = SHL32(EXTEND32(x[i + 2]), 0);
                sum[3] = SHL32(EXTEND32(x[i + 3]), 0);

                xcorr_kernel(rnum, x + i - ord, sum, ord);

                y[i] = SROUND16(sum[0], 0);
                y[i + 1] = SROUND16(sum[1], 0);
                y[i + 2] = SROUND16(sum[2], 0);
                y[i + 3] = SROUND16(sum[3], 0);
            }
            for (; i < N; i++)
            {
                float sum2 = SHL32(EXTEND32(x[i]), 0);
                for (j = 0; j < ord; j++)
                    sum2 = MAC16_16(sum2, rnum[j], x[i + j - ord]);
                y[i] = SROUND16(sum2, 0);
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
            ASSERT((ord & 3) == 0);
            float* rden = stackalloc float[ord];
            float* y = stackalloc float[N + ord];
            for (i = 0; i < ord; i++)
                rden[i] = den[ord - i - 1];
            for (i = 0; i < ord; i++)
                y[i] = -mem[ord - i - 1];
            for (; i < N + ord; i++)
                y[i] = 0;

            float* sum = stackalloc float[4];
            for (i = 0; i < N - 3; i += 4)
            {
                /* Unroll by 4 as if it were an FIR filter */
                sum[0] = _x[i];
                sum[1] = _x[i + 1];
                sum[2] = _x[i + 2];
                sum[3] = _x[i + 3];
                xcorr_kernel(rden, y + i, sum, ord);
                /* Patch up the result to compensate for the fact that this is an IIR */
                y[i + ord] = -SROUND16(sum[0], 0);
                _y[i] = sum[0];
                sum[1] = MAC16_16(sum[1], y[i + ord], den[0]);
                y[i + ord + 1] = -SROUND16(sum[1], 0);
                _y[i + 1] = sum[1];
                sum[2] = MAC16_16(sum[2], y[i + ord + 1], den[0]);
                sum[2] = MAC16_16(sum[2], y[i + ord], den[1]);
                y[i + ord + 2] = -SROUND16(sum[2], 0);
                _y[i + 2] = sum[2];

                sum[3] = MAC16_16(sum[3], y[i + ord + 2], den[0]);
                sum[3] = MAC16_16(sum[3], y[i + ord + 1], den[1]);
                sum[3] = MAC16_16(sum[3], y[i + ord], den[2]);
                y[i + ord + 3] = -SROUND16(sum[3], 0);
                _y[i + 3] = sum[3];
            }
            for (; i < N; i++)
            {
                float sum2 = _x[i];
                for (j = 0; j < ord; j++)
                    sum2 -= MULT16_16(rden[j], y[i + j]);
                y[i + ord] = SROUND16(sum2, 0);
                _y[i] = sum2;
            }
            for (i = 0; i < ord; i++)
                mem[i] = _y[N - i - 1];
        }

        internal static unsafe int _celt_autocorr(
                           in float* x,   /*  in: [0...n-1] samples x   */
                           float* ac,  /* out: [0...lag-1] ac values */
                           in float* window,
                           int overlap,
                           int lag,
                           int n
                          )
        {
            float d;
            int i, k;
            int fastN = n - lag;
            int shift;
            float* xptr;
            float* xx = stackalloc float[n];
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
