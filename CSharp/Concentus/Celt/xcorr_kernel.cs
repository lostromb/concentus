﻿using Concentus.Common;
using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt
{
    public static class xcorr_kernel
    {
        /// <summary>
        /// OPT: This is the kernel you really want to optimize. It gets used a lot by the prefilter and by the PLC.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="sum"></param>
        /// <param name="len"></param>
        public static void xcorr_kernel_c(short[] x, int x_ptr, short[] y, int y_ptr, int[] sum, int len)
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

        public static void xcorr_kernel_c(Pointer<int> x, Pointer<int> y, Pointer<int> sum, int len)
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
    }
}
