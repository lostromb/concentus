/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2010 Xiph.Org Foundation
   Copyright (c) 2008 Gregory Maxwell
   Originally written by Jean-Marc Valin, Gregory Maxwell, and the Opus open-source contributors
   Ported to C# by Logan Stromberg

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

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

namespace Concentus.Celt
{
    using Concentus.Celt.Enums;
    using Concentus.Celt.Structs;
    using Concentus.Common;
    using Concentus.Common.CPlusPlus;
    using System.Diagnostics;

    internal static class Kernels
    {
        internal static void celt_fir(
             short[] x,
             int x_ptr,
             short[] num,
             short[] y,
             int y_ptr,
             int N,
             int ord,
             short[] mem
             )
        {
            int i, j;
            short[] rnum = new short[ord];
            short[] local_x = new short[N + ord];

            for (i = 0; i < ord; i++)
            {
                rnum[i] = num[ord - i - 1];
            }

            for (i = 0; i < ord; i++)
            {
                local_x[i] = mem[ord - i - 1];
            }

            for (i = 0; i < N; i++)
            {
                local_x[i + ord] = x[x_ptr + i];
            }

            for (i = 0; i < ord; i++)
            {
                mem[i] = x[x_ptr + N - i - 1];
            }

            for (i = 0; i < N - 3; i += 4)
            {
                int[] sum = { 0, 0, 0, 0 };
                xcorr_kernel(rnum, 0, local_x, i, sum, ord);
                y[y_ptr + i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i]), Inlines.PSHR32(sum[0], CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 1] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 1]), Inlines.PSHR32(sum[1], CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 2] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 2]), Inlines.PSHR32(sum[2], CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 3] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 3]), Inlines.PSHR32(sum[3], CeltConstants.SIG_SHIFT))));
            }

            for (; i < N; i++)
            {
                int sum = 0;

                for (j = 0; j < ord; j++)
                {
                    sum = Inlines.MAC16_16(sum, rnum[j], local_x[i + j]);
                }

                y[y_ptr + i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i]), Inlines.PSHR32(sum, CeltConstants.SIG_SHIFT))));
            }
        }

        internal static void celt_fir(
             int[] x,
             int x_ptr,
             int[] num,
             int num_ptr,
             int[] y,
             int y_ptr,
             int N,
             int ord,
             int[] mem
             )
        {
            int i, j;
            int[] rnum = new int[ord];
            int[] local_x = new int[N + ord];

            for (i = 0; i < ord; i++)
            {
                rnum[i] = num[num_ptr + ord - i - 1];
            }

            for (i = 0; i < ord; i++)
            {
                local_x[i] = mem[ord - i - 1];
            }

            for (i = 0; i < N; i++)
            {
                local_x[i + ord] = x[x_ptr + i];
            }

            for (i = 0; i < ord; i++)
            {
                mem[i] = x[x_ptr + N - i - 1];
            }

            for (i = 0; i < N - 3; i += 4)
            {
                int[] sum = { 0, 0, 0, 0 };
                xcorr_kernel(rnum, 0, local_x, i, sum, ord);
                y[y_ptr + i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i]), Inlines.PSHR32(sum[0], CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 1] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 1]), Inlines.PSHR32(sum[1], CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 2] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 2]), Inlines.PSHR32(sum[2], CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 3] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 3]), Inlines.PSHR32(sum[3], CeltConstants.SIG_SHIFT))));
            }

            for (; i < N; i++)
            {
                int sum = 0;

                for (j = 0; j < ord; j++)
                {
                    sum = Inlines.MAC16_16(sum, rnum[j], local_x[i + j]);
                }

                y[y_ptr + i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i]), Inlines.PSHR32(sum, CeltConstants.SIG_SHIFT))));
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

        internal static void xcorr_kernel(int[] x, int x_ptr, int[] y, int y_ptr, int[] sum, int len)
        {
            int j;
            int y_0, y_1, y_2, y_3;
            Inlines.OpusAssert(len >= 3);
            y_3 = 0; /* gcc doesn't realize that y_3 can't be used uninitialized */
            y_0 = y[y_ptr++];
            y_1 = y[y_ptr++];
            y_2 = y[y_ptr++];
            for (j = 0; j < len - 3; j += 4)
            {
                int tmp;
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
                int tmp;
                tmp = x[x_ptr++];
                y_3 = y[y_ptr++];
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_0);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_1);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_2);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_3);
            }
            if (j++ < len)
            {
                int tmp;
                tmp = x[x_ptr++];
                y_0 = y[y_ptr++];
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_1);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_2);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_3);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_0);
            }
            if (j < len)
            {
                int tmp;
                tmp = x[x_ptr++];
                y_1 = y[y_ptr++];
                sum[0] = Inlines.MAC16_16(sum[0], tmp, y_2);
                sum[1] = Inlines.MAC16_16(sum[1], tmp, y_3);
                sum[2] = Inlines.MAC16_16(sum[2], tmp, y_0);
                sum[3] = Inlines.MAC16_16(sum[3], tmp, y_1);
            }
        }

        internal static int celt_inner_prod(short[] x, int x_ptr, short[] y, int y_ptr, int N)
        {
            int i;
            int xy = 0;
            for (i = 0; i < N; i++)
                xy = Inlines.MAC16_16(xy, x[x_ptr + i], y[y_ptr + i]);
            return xy;
        }

        internal static int celt_inner_prod(int[] x, int x_ptr, int[] y, int y_ptr, int N)
        {
            int i;
            int xy = 0;
            for (i = 0; i < N; i++)
                xy = Inlines.MAC16_16(xy, x[x_ptr + i], y[y_ptr + i]);
            return xy;
        }
        
        internal static void dual_inner_prod(int[] x, int x_ptr, int[] y01,int y01_ptr, int[] y02, int y02_ptr, int N, BoxedValue<int> xy1, BoxedValue<int> xy2)
        {
            int i;
            int xy01 = 0;
            int xy02 = 0;
            for (i = 0; i < N; i++)
            {
                xy01 = Inlines.MAC16_16(xy01, x[x_ptr + i], y01[y01_ptr + i]);
                xy02 = Inlines.MAC16_16(xy02, x[x_ptr + i], y02[y02_ptr + i]);
            }
            xy1.Val = xy01;
            xy2.Val = xy02;
        }
    }
}
