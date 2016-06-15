using Concentus.Common;
using Concentus.Common.CPlusPlus;

namespace Concentus.Celt
{
    public static class celt_fir
    {
        public static void celt_fir_c(
             Pointer<short> _x,
             Pointer<short> num,
             Pointer<short> _y,
             int N,
             int ord,
             Pointer<short> mem,
             int arch)
        {
            int i, j;
            Pointer<short> rnum = Pointer.Malloc<short>(ord);
            Pointer<short> x = Pointer.Malloc<short>(N + ord);

            for (i = 0; i < ord; i++)
            {
                rnum[i] = num[ord - i - 1];
            }

            for (i = 0; i < ord; i++)
            {
                x[i] = mem[ord - i - 1];
            }

            for (i = 0; i < N; i++)
            {
                x[i + ord] = _x[i];
            }

            for (i = 0; i < ord; i++)
            {
                mem[i] = _x[N - i - 1];
            }

            for (i = 0; i < N - 3; i += 4)
            {
                int[] sum = { 0, 0, 0, 0 };
                xcorr_kernel.xcorr_kernel_c(rnum.Data, rnum.Offset, x.Data, x.Offset + i, sum, ord);
                _y[i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(_x[i]), Inlines.PSHR32(sum[0], CeltConstants.SIG_SHIFT))));
                _y[i + 1] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(_x[i + 1]), Inlines.PSHR32(sum[1], CeltConstants.SIG_SHIFT))));
                _y[i + 2] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(_x[i + 2]), Inlines.PSHR32(sum[2], CeltConstants.SIG_SHIFT))));
                _y[i + 3] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(_x[i + 3]), Inlines.PSHR32(sum[3], CeltConstants.SIG_SHIFT))));
            }

            for (; i < N; i++)
            {
                int sum = 0;

                for (j = 0; j < ord; j++)
                {
                    sum = Inlines.MAC16_16(sum, rnum[j], x[i + j]);
                }

                _y[i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(_x[i]), Inlines.PSHR32(sum, CeltConstants.SIG_SHIFT))));
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
            {
                rnum[i] = num[ord - i - 1];
            }

            for (i = 0; i < ord; i++)
            {
                x[i] = mem[ord - i - 1];
            }

            for (i = 0; i < N; i++)
            {
                x[i + ord] = _x[i];
            }

            for (i = 0; i < ord; i++)
            {
                mem[i] = _x[N - i - 1];
            }

            for (i = 0; i < N - 3; i += 4)
            {
                int[] sum = { 0, 0, 0, 0 };
                xcorr_kernel.xcorr_kernel_c(rnum, x.Point(i), sum.GetPointer(), ord);
                _y[i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(_x[i]), Inlines.PSHR32(sum[0], CeltConstants.SIG_SHIFT))));
                _y[i + 1] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(_x[i + 1]), Inlines.PSHR32(sum[1], CeltConstants.SIG_SHIFT))));
                _y[i + 2] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(_x[i + 2]), Inlines.PSHR32(sum[2], CeltConstants.SIG_SHIFT))));
                _y[i + 3] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(_x[i + 3]), Inlines.PSHR32(sum[3], CeltConstants.SIG_SHIFT))));
            }

            for (; i < N; i++)
            {
                int sum = 0;

                for (j = 0; j < ord; j++)
                {
                    sum = Inlines.MAC16_16(sum, rnum[j], x[i + j]);
                }

                _y[i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(_x[i]), Inlines.PSHR32(sum, CeltConstants.SIG_SHIFT))));
            }
        }
    }
}
