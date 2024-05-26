using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace HellaUnsafe.Celt
{
    internal static class Pitch
    {
        internal static unsafe void xcorr_kernel(
            float* x,
            float* y,
            float* sum /* [4] */,
            int len)
        {
            if (Vector.IsHardwareAccelerated)
            {
                // TODO Vectorized loop here
                //Inlines.ASSERT(max_pitch > 0);
                //int i = 0;
                //for (i = 0; i < max_pitch - 7; i += Vector<float>.Count)
                //{
                //    
                //}
                //for (; i < max_pitch; i++)
                //{
                //    xcorr[i] = celt_inner_prod(_x, _y + i, len);
                //}
            }
            else
            {
                xcorr_kernel_c(x, y, sum, len);
            }
        }

        /* OPT: This is the kernel you really want to optimize. It gets used a lot
            by the prefilter and by the PLC. */
        internal static unsafe void xcorr_kernel_c(
            float* x,
            float* y,
            float* sum /* [4] */,
            int len)
        {

            int j;
            float y_0, y_1, y_2, y_3;
            Inlines.ASSERT(len >= 3);
            y_3 = 0; /* gcc doesn't realize that y_3 can't be used uninitialized */
            y_0 = *y++;
            y_1 = *y++;
            y_2 = *y++;
            for (j = 0; j < len - 3; j += 4)
            {
                float tmp;
                tmp = *x++;
                y_3 = *y++;
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_0);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_1);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_2);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_3);
                tmp = *x++;
                y_0 = *y++;
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_1);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_2);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_3);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_0);
                tmp = *x++;
                y_1 = *y++;
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_2);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_3);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_0);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_1);
                tmp = *x++;
                y_2 = *y++;
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_3);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_0);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_1);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_2);
            }
            if (j++ < len)
            {
                float tmp = *x++;
                y_3 = *y++;
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_0);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_1);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_2);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_3);
            }
            if (j++ < len)
            {
                float tmp = *x++;
                y_0 = *y++;
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_1);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_2);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_3);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_0);
            }
            if (j < len)
            {
                float tmp = *x++;
                y_1 = *y++;
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_2);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_3);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_0);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_1);
            }
        }

        internal static unsafe void dual_inner_prod(in float* x, in float* y01, in float* y02,
            int N, in float* xy1, in float* xy2)
        {
            dual_inner_prod_c(x, y01, y02, N, xy1, xy2);
        }

        internal static unsafe void dual_inner_prod_c(in float* x, in float* y01, in float* y02,
            int N, in float* xy1, in float* xy2)
        {
            int i;
            float xy01 = 0;
            float xy02 = 0;
            for (i = 0; i < N; i++)
            {
                xy01 = Inlines.MAC16_16(xy01, x[i], y01[i]);
                xy02 = Inlines.MAC16_16(xy02, x[i], y02[i]);
            }
            *xy1 = xy01;
            *xy2 = xy02;
        }

        internal static unsafe float celt_inner_prod(
            in float* x,
            in float* y,
            int N)
        {
            return celt_inner_prod(x, y, N);
        }

        internal static unsafe float celt_inner_prod_c(
            in float* x,
            in float* y,
            int N)
        {
            int i;
            float xy = 0;
            for (i = 0; i < N; i++)
            {
                // TODO yep, more vectors!
                xy = Inlines.MAC16_16(xy, x[i], y[i]);
            }

            return xy;
        }
    }
}
