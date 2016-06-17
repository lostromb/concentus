using Concentus.Common;
using Concentus.Common.CPlusPlus;

namespace Concentus.Celt
{
    internal static class Kernels
    {
        internal static void celt_fir(
             Pointer<short> _x,
             Pointer<short> num,
             Pointer<short> _y,
             int N,
             int ord,
             Pointer<short> mem
             )
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
                xcorr_kernel(rnum.Data, rnum.Offset, x.Data, x.Offset + i, sum, ord);
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

        internal static void celt_fir(
             Pointer<int> _x,
             Pointer<int> num,
             Pointer<int> _y,
             int N,
             int ord,
             Pointer<int> mem
             )
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
                xcorr_kernel(rnum, x.Point(i), sum.GetPointer(), ord);
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

        /// <summary>
        /// OPT: This is the kernel you really want to optimize. It gets used a lot by the prefilter and by the PLC.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="sum"></param>
        /// <param name="len"></param>
        internal static void xcorr_kernel(short[] x, int x_ptr, short[] y, int y_ptr, int[] sum, int len)
        {
            int j;
            short y_0, y_1, y_2, y_3;
            Inlines.OpusAssert(len >= 3);
            y_3 = 0; /* gcc doesn't realize that y_3 can't be used uninitialized */
            y_0 = y[y_ptr++];
            y_1 = y[y_ptr++];
            y_2 = y[y_ptr++];
            for (j = 0; j < len - 3; j += 4)
            {
                short tmp;
                tmp = x[x_ptr++];
                y_3 = y[y_ptr++];
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_0);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_1);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_2);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_3);
                tmp = x[x_ptr++];
                y_0 = y[y_ptr++];
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_1);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_2);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_3);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_0);
                tmp = x[x_ptr++];
                y_1 = y[y_ptr++];
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_2);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_3);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_0);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_1);
                tmp = x[x_ptr++];
                y_2 = y[y_ptr++];
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_3);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_0);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_1);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_2);
            }
            if (j++ < len)
            {
                short tmp;
                tmp = x[x_ptr++];
                y_3 = y[y_ptr++];
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_0);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_1);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_2);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_3);
            }
            if (j++ < len)
            {
                short tmp;
                tmp = x[x_ptr++];
                y_0 = y[y_ptr++];
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_1);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_2);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_3);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_0);
            }
            if (j < len)
            {
                short tmp;
                tmp = x[x_ptr++];
                y_1 = y[y_ptr++];
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_2);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_3);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_0);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_1);
            }
        }

        internal static void xcorr_kernel(Pointer<int> x, Pointer<int> y, Pointer<int> sum, int len)
        {
            int j;
            int y_0, y_1, y_2, y_3;
            Inlines.OpusAssert(len >= 3);
            y_3 = 0; /* gcc doesn't realize that y_3 can't be used uninitialized */
            y = y.Iterate(out y_0);
            y = y.Iterate(out y_1);
            y = y.Iterate(out y_2);
            for (j = 0; j < len - 3; j += 4)
            {
                int tmp;
                x = x.Iterate(out tmp);
                y = y.Iterate(out y_3);
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_0);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_1);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_2);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_3);
                x = x.Iterate(out tmp);
                y = y.Iterate(out y_0);
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_1);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_2);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_3);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_0);
                x = x.Iterate(out tmp);
                y = y.Iterate(out y_1);
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_2);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_3);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_0);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_1);
                x = x.Iterate(out tmp);
                y = y.Iterate(out y_2);
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_3);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_0);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_1);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_2);
            }
            if (j++ < len)
            {
                int tmp;
                x = x.Iterate(out tmp);
                y = y.Iterate(out y_3);
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_0);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_1);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_2);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_3);
            }
            if (j++ < len)
            {
                int tmp;
                x = x.Iterate(out tmp);
                y = y.Iterate(out y_0);
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_1);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_2);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_3);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_0);
            }
            if (j < len)
            {
                int tmp;
                x = x.Iterate(out tmp);
                y = y.Iterate(out y_1);
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_2);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_3);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_0);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_1);
            }
        }

        internal static int celt_inner_prod(Pointer<short> x, Pointer<short> y, int N)
        {
            int i;
            int xy = 0;
            for (i = 0; i < N; i++)
                xy = Inlines.MAC16_16(xy, x[i], y[i]);
            return xy;
        }

        internal static int celt_inner_prod(Pointer<int> x, Pointer<int> y, int N)
        {
            int i;
            int xy = 0;
            for (i = 0; i < N; i++)
                xy = Inlines.MAC16_16(xy, x[i], y[i]);
            return xy;
        }

        internal static void dual_inner_prod(Pointer<short> x, Pointer<short> y01, Pointer<short> y02, int N, BoxedValue<int> xy1, BoxedValue<int> xy2)
        {
            int i;
            int xy01 = 0;
            int xy02 = 0;
            for (i = 0; i < N; i++)
            {
                xy01 = Inlines.MAC16_16(xy01, x[i], y01[i]);
                xy02 = Inlines.MAC16_16(xy02, x[i], y02[i]);
            }
            xy1.Val = xy01;
            xy2.Val = xy02;
        }

        internal static void dual_inner_prod(Pointer<int> x, Pointer<int> y01, Pointer<int> y02, int N, BoxedValue<int> xy1, BoxedValue<int> xy2)
        {
            int i;
            int xy01 = 0;
            int xy02 = 0;
            for (i = 0; i < N; i++)
            {
                xy01 = Inlines.MAC16_16(xy01, x[i], y01[i]);
                xy02 = Inlines.MAC16_16(xy02, x[i], y02[i]);
            }
            xy1.Val = xy01;
            xy2.Val = xy02;
        }
    }
}
