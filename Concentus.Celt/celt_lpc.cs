using Concentus.Celt.Enums;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Celt
{
    public static class celt_lpc
    {
        public static void _celt_lpc(
            Pointer<int> _lpc, /* out: [0...p-1] LPC coefficients      */
            Pointer<int> ac,  /* in:  [0...p] autocorrelation values  */
            int p)
        {
            int i, j;
            int r;
            int error = ac[0];
            Pointer<int> lpc = Pointer.Malloc<int>(CeltConstants.LPC_ORDER);

            // FIXME this is just MemSet(0)
            // opus bug: why does original code not use opus_clear?
            for (i = 0; i < p; i++)
            {
                lpc[i] = 0;
            }

            if (ac[0] != 0)
            {
                for (i = 0; i < p; i++)
                {
                    /* Sum up this iteration's reflection coefficient */
                    int rr = 0;
                    for (j = 0; j < i; j++)
                        rr += Inlines.MULT32_32_Q31(lpc[j], ac[i - j]);
                    rr += Inlines.SHR32(ac[i + 1], 3);
                    r = 0 - Inlines.frac_div32(Inlines.SHL32(rr, 3), error);
                    /*  Update LPC coefficients and total error */
                    lpc[i] = Inlines.SHR32(r, 3);

                    for (j = 0; j < (i + 1) >> 1; j++)
                    {
                        int tmp1, tmp2;
                        tmp1 = lpc[j];
                        tmp2 = lpc[i - 1 - j];
                        lpc[j] = tmp1 + Inlines.MULT32_32_Q31(r, tmp2);
                        lpc[i - 1 - j] = tmp2 + Inlines.MULT32_32_Q31(r, tmp1);
                    }

                    error = error - Inlines.MULT32_32_Q31(Inlines.MULT32_32_Q31(r, r), error);

                    /* Bail out once we get 30 dB gain */
                    if (error < Inlines.SHR32(ac[0], 10))
                    {
                        break;
                    }
                }
            }

            for (i = 0; i < p; i++)
            {
                _lpc[i] = Inlines.ROUND16((lpc[i]), 16);
            }
        }
        
        public static void celt_fir_c(
                 Pointer<int> _x,
                 Pointer<int> num,
                 Pointer<int> _y,
                 int N,
                 int ord,
                 Pointer<int> mem,
                 int arch)
        {
            int i, j;
            Pointer<int> rnum = Pointer.Malloc<int>(ord);
            Pointer<int> x = Pointer.Malloc<int>(N + ord);

            for (i = 0; i < ord; i++)
                rnum[i] = num[ord - i - 1];
            for (i = 0; i < ord; i++)
                x[i] = mem[ord - i - 1];
            for (i = 0; i < N; i++)
                x[i + ord] = _x[i];
            for (i = 0; i < ord; i++)
                mem[i] = _x[N - i - 1];
            for (i = 0; i < N - 3; i += 4)
            {
                int[] sum = { 0, 0, 0, 0 };
                xcorr_kernel.xcorr_kernel_c(rnum, x.Point(i), sum.GetPointer(), ord);
                _y[i] = Inlines.SATURATE16(Inlines.CHOP16(Inlines.ADD32(Inlines.EXTEND32(_x[i]), Inlines.PSHR32(sum[0], CeltConstants.SIG_SHIFT))));
                _y[i + 1] = Inlines.SATURATE16(Inlines.CHOP16(Inlines.ADD32(Inlines.EXTEND32(_x[i + 1]), Inlines.PSHR32(sum[1], CeltConstants.SIG_SHIFT))));
                _y[i + 2] = Inlines.SATURATE16(Inlines.CHOP16(Inlines.ADD32(Inlines.EXTEND32(_x[i + 2]), Inlines.PSHR32(sum[2], CeltConstants.SIG_SHIFT))));
                _y[i + 3] = Inlines.SATURATE16(Inlines.CHOP16(Inlines.ADD32(Inlines.EXTEND32(_x[i + 3]), Inlines.PSHR32(sum[3], CeltConstants.SIG_SHIFT))));
            }
            for (; i < N; i++)
            {
                int sum = 0;
                for (j = 0; j < ord; j++)
                    sum = Inlines.MAC16_16(sum, rnum[j], x[i + j]);
                _y[i] = Inlines.SATURATE16(Inlines.CHOP16(Inlines.ADD32(Inlines.EXTEND32(_x[i]), Inlines.PSHR32(sum, CeltConstants.SIG_SHIFT))));
            }
        }

        public static void celt_iir(Pointer<int> _x,
                 Pointer<int> den,
                 Pointer<int> _y,
                 int N,
                 int ord,
                 Pointer<int> mem,
                 int arch)
        {
            int i, j;
            Pointer<int> rden = Pointer.Malloc<int>(ord);
            Pointer<int> y = Pointer.Malloc<int>(N + ord);
            Debug.Assert((ord & 3) == 0);

            for (i = 0; i < ord; i++)
                rden[i] = den[ord - i - 1];
            for (i = 0; i < ord; i++)
                y[i] = (0 - mem[ord - i - 1]);
            for (; i < N + ord; i++)
                y[i] = 0;
            for (i = 0; i < N - 3; i += 4)
            {
                /* Unroll by 4 as if it were an FIR filter */
                int[] sum = new int[4];
                sum[0] = _x[i];
                sum[1] = _x[i + 1];
                sum[2] = _x[i + 2];
                sum[3] = _x[i + 3];
                xcorr_kernel.xcorr_kernel_c(rden, y.Point(i), sum.GetPointer(), ord);

                /* Patch up the result to compensate for the fact that this is an IIR */
                y[i + ord] = (0 - Inlines.ROUND16((sum[0]), CeltConstants.SIG_SHIFT));
                _y[i] = sum[0];
                sum[1] = Inlines.MAC16_16(sum[1], y[i + ord], den[0]);
                y[i + ord + 1] = (0 - Inlines.ROUND16((sum[1]), CeltConstants.SIG_SHIFT));
                _y[i + 1] = sum[1];
                sum[2] = Inlines.MAC16_16(sum[2], y[i + ord + 1], den[0]);
                sum[2] = Inlines.MAC16_16(sum[2], y[i + ord], den[1]);
                y[i + ord + 2] = (0 - Inlines.ROUND16((sum[2]), CeltConstants.SIG_SHIFT));
                _y[i + 2] = sum[2];

                sum[3] = Inlines.MAC16_16(sum[3], y[i + ord + 2], den[0]);
                sum[3] = Inlines.MAC16_16(sum[3], y[i + ord + 1], den[1]);
                sum[3] = Inlines.MAC16_16(sum[3], y[i + ord], den[2]);
                y[i + ord + 3] = (0 - Inlines.ROUND16((sum[3]), CeltConstants.SIG_SHIFT));
                _y[i + 3] = sum[3];
            }
            for (; i < N; i++)
            {
                int sum = _x[i];
                for (j = 0; j < ord; j++)
                    sum -= Inlines.MULT16_16(rden[j], y[i + j]);
                y[i + ord] = Inlines.ROUND16((sum), CeltConstants.SIG_SHIFT);
                _y[i] = sum;
            }
            for (i = 0; i < ord; i++)
                mem[i] = (_y[N - i - 1]);

        }

        public static int _celt_autocorr(
                           Pointer<int> x,   /*  in: [0...n-1] samples x   */
                           Pointer<int> ac,  /* out: [0...lag-1] ac values */
                           Pointer<int> window,
                           int overlap,
                           int lag,
                           int n,
                           int arch
                          )
        {
            int d;
            int i, k;
            int fastN = n - lag;
            int shift;
            Pointer<int> xptr;
            Pointer<int> xx = Pointer.Malloc<int>(n);

            Debug.Assert(n > 0);
            Debug.Assert(overlap >= 0);

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
                    xx[i] = Inlines.MULT16_16_Q15(x[i], window[i]);
                    xx[n - i - 1] = Inlines.MULT16_16_Q15(x[n - i - 1], window[i]);
                }
                xptr = xx;
            }

            shift = 0;
            
            int ac0;
            ac0 = 1 + (n << 7);
            if ((n & 1) != 0)
                ac0 += Inlines.SHR32(Inlines.MULT16_16(xptr[0], xptr[0]), 9);

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
                    xx[i] = (Inlines.PSHR32(xptr[i], shift));
                xptr = xx;
            }
            else
                shift = 0;

            celt_pitch_xcorr.pitch_xcorr(xptr, xptr, ac, fastN, lag + 1);
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
    }
}
