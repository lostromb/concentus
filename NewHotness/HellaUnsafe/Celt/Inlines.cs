using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace HellaUnsafe.Celt
{
    internal static class Inlines
    {
        internal const float CELT_SIG_SCALE = 32768.0f;
        internal const float Q15ONE = 1.0f;
        internal const float EPSILON = 1e-15f;
        internal const float VERY_SMALL = 1e-30f;
        internal const float VERY_LARGE = 1e15f;
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
        internal static float MIN16(float a, float b) { return Math.Min(a, b); }
        internal static float MAX16(float a, float b) { return Math.Max(a, b); }
        internal static short MIN16(short a, short b) { return Math.Min(a, b); }
        internal static short MAX16(short a, short b) { return Math.Max(a, b); }
        internal static int MIN32(int a, int b) { return Math.Min(a, b); }
        internal static int MAX32(int a, int b) { return Math.Max(a, b); }
        internal static float MIN32(float a, float b) { return Math.Min(a, b); }
        internal static float MAX32(float a, float b) { return Math.Max(a, b); }
        internal static uint UADD32(uint a, uint b) { return a + b; }
        internal static uint USUB32(uint a, uint b) { return a - b; }
        internal static float ABS16(float x) { return Math.Abs(x); }
        internal static float ABS32(float x) { return Math.Abs(x); }
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

        internal static float celt_sqrt(float x) { return (float)Math.Sqrt(x); }
        internal static float celt_rsqrt(float x) { return 1.0f / (float)Math.Sqrt(x); }
        internal static float celt_rsqrt_norm(float x) { return 1.0f / (float)Math.Sqrt(x); }
        internal static float celt_cos_norm(float x) { return (float)Math.Cos(0.5f * Math.PI * x); }
        internal static float celt_rcp(float x) { return 1.0f / x; }
        internal static float celt_div(float a, float b) { Inlines.ASSERT(b > 0); return a / b; }
        internal static float frac_div32(float a, float b) { Inlines.ASSERT(b > 0); return a / b; }
        internal static uint celt_udiv(uint a, uint b) { Inlines.ASSERT(b > 0); return a / b; }
        internal static int celt_sudiv(int a, int b) { Inlines.ASSERT(b > 0); return a / b; }
        internal static float celt_log2(float x) { return (float)(1.442695040888963387 * Math.Log(x)); }
        internal static float celt_exp2(float x) { return (float)Math.Exp(0.6931471805599453094 * (x)); }
        internal static float fast_atan2f(float a, float b) { return (float)Math.Atan2(a, b); }

    }
}
