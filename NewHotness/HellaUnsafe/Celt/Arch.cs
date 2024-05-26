/* Copyright (c) 2003-2008 Jean-Marc Valin
   Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2009 Xiph.Org Foundation
   Written by Jean-Marc Valin */
/**
   @file arch.h
   @brief Various architecture definitions for CELT
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

using System;
using System.Diagnostics;
using static System.Math;

namespace HellaUnsafe.Celt
{
    internal static class Arch
    {
        internal const float CELT_SIG_SCALE = 32768.0f;
        internal const float NORM_SCALING = 1.0f;
        internal const float Q15ONE = 1.0f;
        internal const float EPSILON = 1e-15f;
        internal const float VERY_SMALL = 1e-30f;
        internal const float VERY_LARGE16 = 1e15f;
        internal const float Q15_ONE = 1.0f;

        [Conditional("DEBUG")]
        internal static void ASSERT(bool condition)
        {
            if (!condition)
            {
                throw new Exception("Assertion failed");
            }
        }

        [Conditional("DEBUG")]
        internal static void ASSERT(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        internal static int IMUL32(int a, int b) { return a * b; }
        internal static uint IMUL32(uint a, uint b) { return a * b; }
        internal static float MIN16(float a, float b) { return Min(a, b); }
        internal static float MAX16(float a, float b) { return Max(a, b); }
        internal static short MIN16(short a, short b) { return Min(a, b); }
        internal static short MAX16(short a, short b) { return Max(a, b); }
        internal static int IMIN(int a, int b) { return Min(a, b); }
        internal static int IMAX(int a, int b) { return Max(a, b); }
        internal static int MIN32(int a, int b) { return Min(a, b); }
        internal static int MAX32(int a, int b) { return Max(a, b); }
        internal static float MIN32(float a, float b) { return Min(a, b); }
        internal static float MAX32(float a, float b) { return Max(a, b); }
        internal static uint UADD32(uint a, uint b) { return a + b; }
        internal static uint USUB32(uint a, uint b) { return a - b; }
        internal static float ABS16(float x) { return Abs(x); }
        internal static float ABS32(float x) { return Abs(x); }
        internal static float QCONST16(float x, int bits) { return x; }
        internal static float QCONST32(float x, int bits) { return x; }
        internal static float NEG16(float x) { return -x; }
        internal static float NEG32(float x) { return -x; }
        internal static float NEG32_ovflw(float x) { return -x; }
        internal static float EXTRACT16(float x) { return x; }
        internal static float EXTEND32(float x) { return x; }
        internal static float SHR16(float x, int shift) { return x; }
        internal static float SHL16(float x, int shift) { return x; }
        internal static float SHR32(float x, int shift) { return x; }
        internal static float SHL32(float x, int shift) { return x; }
        internal static float PSHR32(float x, int shift) { return x; }
        internal static float VSHR32(float x, int shift) { return x; }
        internal static float PSHR(float x, int shift) { return x; }
        internal static float SHR(float x, int shift) { return x; }
        internal static float SHL(float x, int shift) { return x; }
        internal static float SATURATE(float x, int a) { return x; }
        internal static float SATURATE16(float x) { return x; }
        internal static float ROUND16(float x, int a) { return x; }
        internal static float SROUND16(float x, int a) { return x; }
        internal static float HALF16(float x) { return 0.5f * x; }
        internal static float HALF32(float x) { return 0.5f * x; }
        internal static float ADD16(float a, float b) { return a + b; }
        internal static float SUB16(float a, float b) { return a - b; }
        internal static float ADD32(float a, float b) { return a + b; }
        internal static float SUB32(float a, float b) { return a - b; }
        internal static float ADD32_ovflw(float a, float b) { return a + b; }
        internal static float SUB32_ovflw(float a, float b) { return a - b; }
        internal static float MULT16_16_16(float a, float b) { return a * b; }
        internal static float MULT16_16(float a, float b) { return a * b; }
        internal static float MAC16_16(float c, float a, float b) { return c + (a * b); } // OPT can use FMA intrinsic if possible
        internal static float MULT16_32_Q15(float a, float b) { return a * b; }
        internal static float MULT16_32_Q16(float a, float b) { return a * b; }
        internal static float MULT32_32_Q31(float a, float b) { return a * b; }
        internal static float MAC16_32_Q15(float c, float a, float b) { return c + (a * b); } // OPT can use FMA intrinsic if possible
        internal static float MAC16_32_Q16(float c, float a, float b) { return c + (a * b); } // OPT can use FMA intrinsic if possible
        internal static float MULT16_16_Q11_32(float a, float b) { return a * b; }
        internal static float MULT16_16_Q11(float a, float b) { return a * b; }
        internal static float MULT16_16_Q13(float a, float b) { return a * b; }
        internal static float MULT16_16_Q14(float a, float b) { return a * b; }
        internal static float MULT16_16_Q15(float a, float b) { return a * b; }
        internal static float MULT16_16_P15(float a, float b) { return a * b; }
        internal static float MULT16_16_P13(float a, float b) { return a * b; }
        internal static float MULT16_16_P14(float a, float b) { return a * b; }
        internal static float MULT16_32_P16(float a, float b) { return a * b; }
        internal static float DIV32_16(float a, float b) { return a / b; }
        internal static float DIV32(float a, float b) { return a / b; }
        internal static float SCALEIN(float a) { return a * CELT_SIG_SCALE; }
        internal static float SCALEOUT(float a) { return a * (1 / CELT_SIG_SCALE); }
        internal static float SIG2WORD16(float x) { return x; }
    }
}
