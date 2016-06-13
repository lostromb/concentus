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
    public static class celt_pitch_xcorr
    {
        public static int pitch_xcorr(
            Pointer<int> _x,
            Pointer<int> _y,
            Pointer<int> xcorr,
            int len,
            int max_pitch)
        {
            int i;
            int maxcorr = 1;
            Inlines.OpusAssert(max_pitch > 0);
            // Inlines.OpusAssert((((byte*)_x - (byte*)NULL)&3)== 0); // fixme this checks alignment, right?
            for (i = 0; i < max_pitch - 3; i += 4)
            {
                int[] sum = { 0, 0, 0, 0 };

                xcorr_kernel.xcorr_kernel_c(_x, _y.Point(i), sum.GetPointer(), len);
                xcorr[i] = sum[0];
                xcorr[i + 1] = sum[1];
                xcorr[i + 2] = sum[2];
                xcorr[i + 3] = sum[3];
                sum[0] = Inlines.MAX32(sum[0], sum[1]);
                sum[2] = Inlines.MAX32(sum[2], sum[3]);
                sum[0] = Inlines.MAX32(sum[0], sum[2]);
                maxcorr = Inlines.MAX32(maxcorr, sum[0]);
            }
            /* In case max_pitch isn't a multiple of 4, do non-unrolled version. */
            for (; i < max_pitch; i++)
            {
                int sum = celt_inner_prod.celt_inner_prod_c(_x, _y.Point(i), len);
                xcorr[i] = sum;
                maxcorr = Inlines.MAX32(maxcorr, sum);
            }
            return maxcorr;
        }

        public static int pitch_xcorr(
            Pointer<short> _x,
            Pointer<short> _y,
            Pointer<int> xcorr,
            int len,
            int max_pitch)
        {
            int i;
            int maxcorr = 1;
            Inlines.OpusAssert(max_pitch > 0);
            // Inlines.OpusAssert((((byte*)_x - (byte*)NULL)&3)== 0); // fixme this checks alignment, right?
            for (i = 0; i < max_pitch - 3; i += 4)
            {
                int[] sum = { 0, 0, 0, 0 };

                xcorr_kernel.xcorr_kernel_c(_x, _y.Point(i), sum.GetPointer(), len);

                xcorr[i] = sum[0];
                xcorr[i + 1] = sum[1];
                xcorr[i + 2] = sum[2];
                xcorr[i + 3] = sum[3];
                sum[0] = Inlines.MAX32(sum[0], sum[1]);
                sum[2] = Inlines.MAX32(sum[2], sum[3]);
                sum[0] = Inlines.MAX32(sum[0], sum[2]);
                maxcorr = Inlines.MAX32(maxcorr, sum[0]);
            }
            /* In case max_pitch isn't a multiple of 4, do non-unrolled version. */
            for (; i < max_pitch; i++)
            {
                int sum = celt_inner_prod.celt_inner_prod_c(_x, _y.Point(i), len);
                xcorr[i] = sum;
                maxcorr = Inlines.MAX32(maxcorr, sum);
            }
            return maxcorr;
        }
    }
}
