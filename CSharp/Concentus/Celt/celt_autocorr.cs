using Concentus.Common.CPlusPlus;
using Concentus.Common;
using System.Diagnostics;

namespace Concentus.Celt
{
    public static class celt_autocorr
    {
        public static int _celt_autocorr(
                  Pointer<short> x,   /*  in: [0...n-1] samples x   */
                   Pointer<int> ac,  /* out: [0...lag-1] ac values */
                   Pointer<short> window,
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
            Pointer<short> xptr;
            Pointer<short> xx = Pointer.Malloc<short>(n);
            Inlines.OpusAssert(n > 0);
            Inlines.OpusAssert(overlap >= 0);
            if (overlap == 0)
            {
                xptr = x;
            }
            else {
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
                        xx[i] = Inlines.PSHR16(xptr[i], shift); // opus bug: this was originally PSHR32
                    xptr = xx;
                }
                else
                    shift = 0;
            }
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
