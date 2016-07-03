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
    using System.Threading;

    internal static class CeltPitchXCorr
    {
        internal static int pitch_xcorr(
            int[] _x,
            int[] _y,
            int[] xcorr,
            int len,
            int max_pitch)
        {
            int[] sum = new int[4];
            int i;
            int maxcorr = 1;
            Inlines.OpusAssert(max_pitch > 0);
            for (i = 0; i < max_pitch - 3; i += 4)
            {
                sum[0] = 0;
                sum[1] = 0;
                sum[2] = 0;
                sum[3] = 0;
                Kernels.xcorr_kernel(_x, _y, i, sum, len);
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
                int inner_sum = Kernels.celt_inner_prod(_x, 0, _y, i, len);
                xcorr[i] = inner_sum;
                maxcorr = Inlines.MAX32(maxcorr, inner_sum);
            }
            return maxcorr;
        }

        internal static int pitch_xcorr(
            Pointer<short> _x,
            Pointer<short> _y,
            Pointer<int> xcorr,
            int len,
            int max_pitch)
        {
            int[] sum = new int[4];
            int i;
            int maxcorr = 1;
            Inlines.OpusAssert(max_pitch > 0);
            for (i = 0; i < max_pitch - 3; i += 4)
            {
                sum[0] = 0;
                sum[1] = 0;
                sum[2] = 0;
                sum[3] = 0;
                Kernels.xcorr_kernel(_x.Data, _x.Offset, _y.Data, _y.Offset + i, sum, len);

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
                int inner_sum = Kernels.celt_inner_prod(_x.Data, _x.Offset, _y.Data, _y.Offset + i, len);
                xcorr[i] = inner_sum;
                maxcorr = Inlines.MAX32(maxcorr, inner_sum);
            }
            return maxcorr;
        }

        internal static int pitch_xcorr(
            short[] _x,
            short[] _y,
            int[] xcorr,
            int len,
            int max_pitch)
        {
            int[] sum = new int[4];
            int i;
            int maxcorr = 1;
            Inlines.OpusAssert(max_pitch > 0);
            for (i = 0; i < max_pitch - 3; i += 4)
            {
                sum[0] = 0;
                sum[1] = 0;
                sum[2] = 0;
                sum[3] = 0;
                Kernels.xcorr_kernel(_x, 0, _y, i, sum, len);

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
                int inner_sum = Kernels.celt_inner_prod(_x, _y, i, len);
                xcorr[i] = inner_sum;
                maxcorr = Inlines.MAX32(maxcorr, inner_sum);
            }
            return maxcorr;
        }
    }
}
