/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2009 Xiph.Org Foundation
   Written by Jean-Marc Valin */
/**
   @file pitch.c
   @brief Pitch analysis
 */

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

using System.Numerics;
using static HellaUnsafe.Celt.Arch;

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
                xcorr_kernel_c(x, y, sum, len);
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
            ASSERT(len >= 3);
            y_3 = 0; /* gcc doesn't realize that y_3 can't be used uninitialized */
            y_0 = *y++;
            y_1 = *y++;
            y_2 = *y++;
            for (j = 0; j < len - 3; j += 4)
            {
                float tmp;
                tmp = *x++;
                y_3 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_0);
                sum[1] = MAC16_16(sum[1], tmp, y_1);
                sum[2] = MAC16_16(sum[2], tmp, y_2);
                sum[3] = MAC16_16(sum[3], tmp, y_3);
                tmp = *x++;
                y_0 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_1);
                sum[1] = MAC16_16(sum[1], tmp, y_2);
                sum[2] = MAC16_16(sum[2], tmp, y_3);
                sum[3] = MAC16_16(sum[3], tmp, y_0);
                tmp = *x++;
                y_1 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_2);
                sum[1] = MAC16_16(sum[1], tmp, y_3);
                sum[2] = MAC16_16(sum[2], tmp, y_0);
                sum[3] = MAC16_16(sum[3], tmp, y_1);
                tmp = *x++;
                y_2 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_3);
                sum[1] = MAC16_16(sum[1], tmp, y_0);
                sum[2] = MAC16_16(sum[2], tmp, y_1);
                sum[3] = MAC16_16(sum[3], tmp, y_2);
            }
            if (j++ < len)
            {
                float tmp = *x++;
                y_3 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_0);
                sum[1] = MAC16_16(sum[1], tmp, y_1);
                sum[2] = MAC16_16(sum[2], tmp, y_2);
                sum[3] = MAC16_16(sum[3], tmp, y_3);
            }
            if (j++ < len)
            {
                float tmp = *x++;
                y_0 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_1);
                sum[1] = MAC16_16(sum[1], tmp, y_2);
                sum[2] = MAC16_16(sum[2], tmp, y_3);
                sum[3] = MAC16_16(sum[3], tmp, y_0);
            }
            if (j < len)
            {
                float tmp = *x++;
                y_1 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_2);
                sum[1] = MAC16_16(sum[1], tmp, y_3);
                sum[2] = MAC16_16(sum[2], tmp, y_0);
                sum[3] = MAC16_16(sum[3], tmp, y_1);
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
                xy01 = MAC16_16(xy01, x[i], y01[i]);
                xy02 = MAC16_16(xy02, x[i], y02[i]);
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
                xy = MAC16_16(xy, x[i], y[i]);
            }

            return xy;
        }
    }
}
