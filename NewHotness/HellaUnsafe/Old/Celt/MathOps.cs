/* Copyright (c) 2002-2008 Jean-Marc Valin
   Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2009 Xiph.Org Foundation
   Written by Jean-Marc Valin */
/**
   @file mathops.h
   @brief Various math functions
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

using static System.Math;
using static HellaUnsafe.Old.Celt.Arch;
using static HellaUnsafe.Old.Celt.EntCode;
using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Old.Celt
{
    internal static class MathOps
    {
        /* Multiplies two 16-bit fractional values. Bit-exactness of this macro is important */
        internal static short FRAC_MUL16(short a, short b) { return (short)(16384 + a * b >> 15); }
        internal static int FRAC_MUL16(int a, int b) { return 16384 + (short)a * (short)b >> 15; }

        internal static float celt_sqrt(float x) { return (float)Sqrt(x); }
        internal static float celt_rsqrt(float x) { return 1.0f / (float)Sqrt(x); }
        internal static float celt_rsqrt_norm(float x) { return 1.0f / (float)Sqrt(x); }
        internal static float celt_cos_norm(float x) { return (float)Cos(0.5f * PI * x); }
        internal static float celt_rcp(float x) { return 1.0f / x; }
        internal static float celt_div(float a, float b) { ASSERT(b > 0); return a / b; }
        internal static float frac_div32(float a, float b) { ASSERT(b > 0); return a / b; }
        internal static float celt_log2(float x) { return (float)(1.442695040888963387 * Log(x)); }
        internal static float celt_exp2(float x) { return (float)Exp(0.6931471805599453094 * x); }
        internal static float fast_atan2f(float a, float b) { return (float)Atan2(a, b); }

        /*Compute floor(sqrt(_val)) with exact arithmetic.
          _val must be greater than 0.
          This has been tested on all possible 32-bit inputs greater than 0.*/
        internal static uint isqrt32(uint _val)
        {
            uint b;
            uint g;
            int bshift;
            /*Uses the second method from
               http://www.azillionmonkeys.com/qed/sqroot.html
              The main idea is to search for the largest binary digit b such that
               (g+b)*(g+b) <= _val, and add it to the solution g.*/
            g = 0;
            bshift = EC_ILOG(_val) - 1 >> 1;
            b = 1U << bshift;
            do
            {
                uint t;
                t = (g << 1) + b << bshift;
                if (t <= _val)
                {
                    g += b;
                    _val -= t;
                }
                b >>= 1;
                bshift--;
            }
            while (bshift >= 0);
            return g;
        }

        internal static unsafe float celt_maxabs16(in float* x, int len)
        {
            int i;
            float maxval = 0;
            float minval = 0;
            for (i = 0; i < len; i++)
            {
                maxval = MAX16(maxval, x[i]);
                minval = MIN16(minval, x[i]);
            }

            return MAX32(EXTEND32(maxval), -EXTEND32(minval));
        }
    }
}
