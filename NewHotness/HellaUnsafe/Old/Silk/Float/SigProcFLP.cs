/***********************************************************************
Copyright (c) 2006-2011, Skype Limited. All rights reserved.
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
- Redistributions of source code must retain the above copyright notice,
this list of conditions and the following disclaimer.
- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.
- Neither the name of Internet Society, IETF or IETF Trust, nor the
names of specific contributors, may be used to endorse or promote
products derived from this software without specific prior written
permission.
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
***********************************************************************/

using static System.Math;
using static HellaUnsafe.Old.Silk.SigProcFIX;
using static HellaUnsafe.Old.Silk.Float.FloatCast;

namespace HellaUnsafe.Old.Silk.Float
{
    internal static class SigProcFLP
    {
        internal static float silk_min_float(float a, float b)
        {
            return a < b ? a : b;
        }

        internal static float silk_max_float(float a, float b)
        {
            return a > b ? a : b;
        }

        internal static float silk_abs_float(float a)
        {
            return Abs(a);
        }

        /* sigmoid function */
        internal static float silk_sigmoid(float x)
        {
            return (float)(1.0 / (1.0 + Exp(-x)));
        }

        /* floating-point to integer conversion (rounding) */
        internal static int silk_float2int(float x)
        {
            return float2int(x);
        }

        /* floating-point to integer conversion (rounding) */
        internal static unsafe void silk_float2short_array(
            short* output,
            in float* input,
            int length
        )
        {
            // OPT see possibly faster vectorized function in FloatCast
            int k;
            for (k = length - 1; k >= 0; k--)
            {
                output[k] = silk_SAT16(float2int(input[k]));
            }
        }

        /* integer to floating-point conversion */
        internal static unsafe void silk_short2float_array(
            float* output,
            in short* input,
            int length
        )
        {
            // OPT see possibly faster vectorized function in FloatCast
            int k;
            for (k = length - 1; k >= 0; k--)
            {
                output[k] = input[k];
            }
        }

        /* using log2() helps the fixed-point conversion */
        internal static float silk_log2(double x)
        {
            return (float)(3.32192809488736 * Log10(x));
        }
    }
}
