/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2009 Xiph.Org Foundation
   Written by Jean-Marc Valin */
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

using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Celt.Bands;
using static HellaUnsafe.Celt.MathOps;
using System;

namespace HellaUnsafe.Celt
{
    internal static unsafe class VQ
    {
        internal static unsafe void exp_rotation1(float* X, int len, int stride, float c, float s)
        {
            int i;
            float ms;
            float* Xptr;
            Xptr = X;
            ms = NEG16(s);
            for (i = 0; i < len - stride; i++)
            {
                float x1, x2;
                x1 = Xptr[0];
                x2 = Xptr[stride];
                Xptr[stride] = EXTRACT16(PSHR32(MAC16_16(MULT16_16(c, x2), s, x1), 15));
                *Xptr++ = EXTRACT16(PSHR32(MAC16_16(MULT16_16(c, x1), ms, x2), 15));
            }
            Xptr = &X[len - 2 * stride - 1];
            for (i = len - 2 * stride - 1; i >= 0; i--)
            {
                float x1, x2;
                x1 = Xptr[0];
                x2 = Xptr[stride];
                Xptr[stride] = EXTRACT16(PSHR32(MAC16_16(MULT16_16(c, x2), s, x1), 15));
                *Xptr-- = EXTRACT16(PSHR32(MAC16_16(MULT16_16(c, x1), ms, x2), 15));
            }
        }

        internal static unsafe void exp_rotation(float* X, int len, int dir, int stride, int K, int spread)
        {
            Span<int> SPREAD_FACTOR = [15, 10, 5];
            int i;
            float c, s;
            float gain, theta;
            int stride2 = 0;
            int factor;

            if (2 * K >= len || spread == SPREAD_NONE)
                return;
            factor = SPREAD_FACTOR[spread - 1];

            gain = celt_div((float)MULT16_16(Q15_ONE, len), (float)(len + factor * K));
            theta = HALF16(MULT16_16_Q15(gain, gain));

            c = celt_cos_norm(EXTEND32(theta));
            s = celt_cos_norm(EXTEND32(SUB16(Q15ONE, theta))); /*  sin(theta) */

            if (len >= 8 * stride)
            {
                stride2 = 1;
                /* This is just a simple (equivalent) way of computing sqrt(len/stride) with rounding.
                   It's basically incrementing long as (stride2+0.5)^2 < len/stride. */
                while ((stride2 * stride2 + stride2) * stride + (stride >> 2) < len)
                    stride2++;
            }
            /*NOTE: As a minor optimization, we could be passing around log2(B), not B, for both this and for
               extract_collapse_mask().*/
            len = celt_udiv(len, stride);
            for (i = 0; i < stride; i++)
            {
                if (dir < 0)
                {
                    if (stride2 != 0)
                        exp_rotation1(X + i * len, len, stride2, s, c);
                    exp_rotation1(X + i * len, len, 1, c, s);
                }
                else
                {
                    exp_rotation1(X + i * len, len, 1, c, -s);
                    if (stride2 != 0)
                        exp_rotation1(X + i * len, len, stride2, s, -c);
                }
            }
        }

        internal static void renormalise_vector(float* X, int N, float gain, int arch)
        {
            int i;
            float E;
            float g;
            float t;
            float* xptr;
            E = EPSILON + celt_inner_prod(X, X, N, arch);
            t = E;
            g = MULT16_16_P15(celt_rsqrt_norm(t), gain);

            xptr = X;
            for (i = 0; i < N; i++)
            {
                *xptr = MULT16_16(g, *xptr);
                xptr++;
            }
            /*return celt_sqrt(E);*/
        }
    }
}
