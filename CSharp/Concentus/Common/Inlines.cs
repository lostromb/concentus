/* Copyright (c) 2006-2011 Skype Limited. 
   Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2010 Xiph.Org Foundation
   All Rights Reserved
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

namespace Concentus.Common
{
    using Concentus.Celt;
    using Concentus.Common.CPlusPlus;
    using System;
    using System.Diagnostics;
    public static class Inlines
    {
        public static void OpusAssert(bool condition, string message = "Unknown error")
        {
#if DEBUG
            Debug.Assert(condition, message);
#endif
#if DEBUG_MACROS
            if (!condition) throw new ArithmeticException("Debug macro failed validation");
#endif
        }

#region CELT

        // CELT-SPECIFIC INLINES

        //        /** Multiply a 16-bit signed value by a 16-bit unsigned value. The result is a 32-bit signed value */
        //#define MULT16_16SU(a,b) ((opus_val32)(opus_val16)(a)*(opus_val32)(opus_uint16)(b))
        public static int MULT16_16SU(int a, int b)
        {
            return ((int)(short)(a) * (int)(ushort)(b));
        }

        public static int MULT16_16SU(short a, ushort b)
        {
            return ((int)(short)(a) * (int)(ushort)(b));
        }

        public static int MULT16_16SU(int a, uint b)
        {
            return ((a) * (int)(b));
        }

        //        /** 16x32 multiplication, followed by a 16-bit shift right. Results fits in 32 bits */
        //#define MULT16_32_Q16(a,b) ADD32(MULT16_16((a),SHR((b),16)), SHR(MULT16_16SU((a),((b)&0x0000ffff)),16))
        public static int MULT16_32_Q16(short a, int b)
        {
            return ADD32(MULT16_16((a), SHR((b), 16)), SHR(MULT16_16SU((a), ((b) & 0x0000ffff)), 16));
        }

        public static int MULT16_32_Q16(int a, int b)
        {
            return ADD32(MULT16_16((a), SHR((b), 16)), SHR(MULT16_16SU((a), ((b) & 0x0000ffff)), 16));
        }

        //        /** 16x32 multiplication, followed by a 16-bit shift right (round-to-nearest). Results fits in 32 bits */
        //#define MULT16_32_P16(a,b) ADD32(MULT16_16((a),SHR((b),16)), PSHR(MULT16_16SU((a),((b)&0x0000ffff)),16))
        public static int MULT16_32_P16(short a, int b)
        {
            return ADD32(MULT16_16((a), SHR((b), 16)), PSHR(MULT16_16SU((a), ((b) & 0x0000ffff)), 16));
        }

        public static int MULT16_32_P16(int a, int b)
        {
            return ADD32(MULT16_16((a), SHR((b), 16)), PSHR(MULT16_16SU((a), ((b) & 0x0000ffff)), 16));
        }

        //        /** 16x32 multiplication, followed by a 15-bit shift right. Results fits in 32 bits */
        public static int MULT16_32_Q15(short a, int b)
        {
            return ((a * (b >> 16)) << 1) + ((a * (b & 0xFFFF)) >> 15);
            //return ADD32(SHL(MULT16_16((a), SHR((b), 16)), 1), SHR(MULT16_16SU((a), (ushort)((b) & 0x0000ffff)), 15));
        }

        public static int MULT16_32_Q15(int a, int b)
        {
            return ((a * (b >> 16)) << 1) + ((a * (b & 0xFFFF)) >> 15);
            //return ADD32(SHL(MULT16_16((a), SHR((b), 16)), 1), SHR(MULT16_16SU((a), (uint)((b) & 0x0000ffff)), 15));
        }

        //        /** 32x32 multiplication, followed by a 31-bit shift right. Results fits in 32 bits */
        //#define MULT32_32_Q31(a,b) ADD32(ADD32(SHL(MULT16_16(SHR((a),16),SHR((b),16)),1), SHR(MULT16_16SU(SHR((a),16),((b)&0x0000ffff)),15)), SHR(MULT16_16SU(SHR((b),16),((a)&0x0000ffff)),15))
        public static int MULT32_32_Q31(int a, int b)
        {
            return ADD32(ADD32(SHL(MULT16_16(SHR((a), 16), SHR((b), 16)), 1), SHR(MULT16_16SU(SHR((a), 16), ((b) & 0x0000ffff)), 15)), SHR(MULT16_16SU(SHR((b), 16), ((a) & 0x0000ffff)), 15));
        }

        //        /** Compile-time conversion of float constant to 16-bit value */
        public static short QCONST16(float x, int bits)
        {
            return ((short)(0.5 + (x) * (((int)1) << (bits))));
        }

        //        /** Compile-time conversion of float constant to 32-bit value */
        public static int QCONST32(float x, int bits)
        {
            return ((int)(0.5 + (x) * (((int)1) << (bits))));
        }

        //        /** Negate a 16-bit value */
        public static short NEG16(short x)
        {
            return (short)(0 - x);
        }

        public static int NEG16(int x)
        {
            return 0 - x;
        }

        //        /** Negate a 32-bit value */
        public static int NEG32(int x)
        {
            return 0 - x;
        }

        //        /** Change a 32-bit value into a 16-bit value. The value is assumed to fit in 16-bit, otherwise the result is undefined */
        public static short EXTRACT16(int x)
        {
            return unchecked((short)x);
        }

        //        /** Change a 16-bit value into a 32-bit value */
        public static int EXTEND32(short x)
        {
            return (int)x;
        }

        public static int EXTEND32(int x)
        {
            return x;
        }

        //        /** Arithmetic shift-right of a 16-bit value */
        public static short SHR16(short a, int shift)
        {
            return (short)((a) >> (shift));
        }

        public static int SHR16(int a, int shift)
        {
            return ((a) >> (shift));
        }

        //        /** Arithmetic shift-left of a 16-bit value */
        public static short SHL16(short a, int shift)
        {
            return unchecked((short)(unchecked((ushort)a) << shift));
        }

        public static int SHL16(int a, int shift)
        {
            return unchecked(((int)(unchecked((unchecked((uint)(a)) << (shift))))));
        }

        //        /** Arithmetic shift-right of a 32-bit value */
        public static int SHR32(int a, int shift)
        {
            return a >> shift;
        }

        //        /** Arithmetic shift-left of a 32-bit value */
        public static int SHL32(int a, int shift)
        {
            return unchecked(((int)(unchecked((unchecked((uint)(a)) << (shift))))));
        }

        //        /** 32-bit arithmetic shift right with rounding-to-nearest instead of rounding down */
        public static int PSHR32(int a, int shift)
        {
            return (SHR32((a) + ((EXTEND32(1) << ((shift)) >> 1)), shift));
        }

        public static short PSHR16(short a, int shift)
        {
            return SHR16((short)(a + (1 << (shift) >> 1)), shift);
        }

        public static int PSHR16(int a, int shift)
        {
            return SHR32((a + (1 << (shift) >> 1)), shift);
        }

        //        /** 32-bit arithmetic shift right where the argument can be negative */
        public static int VSHR32(int a, int shift)
        {
            return (((shift) > 0) ? SHR32(a, shift) : SHL32(a, -(shift)));
        }

        //        /** "RAW" macros, should not be used outside of this header file */
        private static int SHR(int a, int shift)
        {
            return ((a) >> (shift));
        }

        private static int SHL(int a, int shift)
        {
            return SHL32(a, shift);
        }

        private static int SHR(short a, int shift)
        {
            return ((a) >> (shift));
        }

        private static int SHL(short a, int shift)
        {
            return SHL32(a, shift);
        }

        private static int PSHR(int a, int shift)
        {
            return (SHR((a) + ((EXTEND32(1) << ((shift)) >> 1)), shift));
        }

        public static int SATURATE(int x, int a)
        {
            return (((x) > (a) ? (a) : (x) < -(a) ? -(a) : (x)));
        }

        public static short SATURATE16(int x)
        {
            return (EXTRACT16((x) > 32767 ? 32767 : (x) < -32768 ? -32768 : (x)));
        }

        //        /** Shift by a and round-to-neareast 32-bit value. Result is a 16-bit value */
        public static short ROUND16(short x, short a)
        {
            return (EXTRACT16(PSHR32((x), (a))));
        }

        public static int ROUND16(int x, int a)
        {
            return PSHR32((x), (a));
        }

        //        /** Divide by two */
        public static short HALF16(short x)
        {
            return (SHR16(x, 1));
        }

        public static int HALF16(int x)
        {
            return (SHR32(x, 1));
        }

        public static int HALF32(int x)
        {
            return (SHR32(x, 1));
        }

        //        /** Add two 16-bit values */
        public static short ADD16(short a, short b)
        {
            return ((short)((short)(a) + (short)(b)));
        }

        public static int ADD16(int a, int b)
        {
            return (a + b);
        }

        //        /** Subtract two 16-bit values */
        public static short SUB16(short a, short b)
        {
            return ((short)((short)(a) - (short)(b)));
        }

        public static int SUB16(int a, int b)
        {
            return (a - b);
        }

        //        /** Add two 32-bit values */
        public static int ADD32(int a, int b)
        {
            return ((int)(a) + (int)(b));
        }

        //        /** Subtract two 32-bit values */
        public static int SUB32(int a, int b)
        {
            return ((int)(a) - (int)(b));
        }

        //        /** 16x16 multiplication where the result fits in 16 bits */
        //#define MULT16_16_16(a,b)     ((((opus_val16)(a))*((opus_val16)(b))))
        public static short MULT16_16_16(short a, short b)
        {
            return CHOP16(((((short)(a)) * ((short)(b)))));
        }

        public static int MULT16_16_16(int a, int b)
        {
            return (a * b);
        }

        //        /* (opus_val32)(opus_val16) gives TI compiler a hint that it's 16x16->32 multiply */
        //        /** 16x16 multiplication where the result fits in 32 bits */
        //#define MULT16_16(a,b)     (((opus_val32)(opus_val16)(a))*((opus_val32)(opus_val16)(b)))
        public static int MULT16_16(int a, int b)
        {
            return a * b;
        }

        public static int MULT16_16(short a, short b)
        {
            return a * b;
        }

        //        /** 16x16 multiply-add where the result fits in 32 bits */
        //#define MAC16_16(c,a,b) (ADD32((c),MULT16_16((a),(b))))
        public static int MAC16_16(short c, short a, short b)
        {
            return c + (a * b);
        }

        public static int MAC16_16(int c, short a, short b)
        {
            return c + (a * b);
        }

        public static int MAC16_16(int c, int a, int b)
        {
            return c + (a * b);
        }

        //        /** 16x32 multiply, followed by a 15-bit shift right and 32-bit add.
        //            b must fit in 31 bits.
        //            Result fits in 32 bits. */
        //#define MAC16_32_Q15(c,a,b) ADD32((c),ADD32(MULT16_16((a),SHR((b),15)), SHR(MULT16_16((a),((b)&0x00007fff)),15)))
        public static int MAC16_32_Q15(int c, short a, short b)
        {
            return ADD32((c), ADD32(MULT16_16((a), SHR((b), 15)), SHR(MULT16_16((a), ((b) & 0x00007fff)), 15)));
        }

        public static int MAC16_32_Q15(int c, int a, int b)
        {
            return ADD32((c), ADD32(MULT16_16((a), SHR((b), 15)), SHR(MULT16_16((a), ((b) & 0x00007fff)), 15)));
        }

        //        /** 16x32 multiplication, followed by a 16-bit shift right and 32-bit add.
        //            Results fits in 32 bits */
        //#define MAC16_32_Q16(c,a,b) ADD32((c),ADD32(MULT16_16((a),SHR((b),16)), SHR(MULT16_16SU((a),((b)&0x0000ffff)),16)))
        public static int MAC16_32_Q16(int c, short a, short b)
        {
            return ADD32((c), ADD32(MULT16_16((a), SHR((b), 16)), SHR(MULT16_16SU((a), ((b) & 0x0000ffff)), 16)));
        }

        public static int MAC16_32_Q16(int c, int a, int b)
        {
            return ADD32((c), ADD32(MULT16_16((a), SHR((b), 16)), SHR(MULT16_16SU((a), ((b) & 0x0000ffff)), 16)));
        }

        //#define MULT16_16_Q11_32(a,b) (SHR(MULT16_16((a),(b)),11))
        public static int MULT16_16_Q11_32(short a, short b)
        {
            return (SHR(MULT16_16((a), (b)), 11));
        }

        public static int MULT16_16_Q11_32(int a, int b)
        {
            return (SHR(MULT16_16((a), (b)), 11));
        }

        //#define MULT16_16_Q11(a,b) (SHR(MULT16_16((a),(b)),11))
        public static short MULT16_16_Q11(short a, short b)
        {
            return CHOP16((SHR(MULT16_16((a), (b)), 11)));
        }

        public static int MULT16_16_Q11(int a, int b)
        {
            return (SHR(MULT16_16((a), (b)), 11));
        }

        //#define MULT16_16_Q13(a,b) (SHR(MULT16_16((a),(b)),13))
        public static short MULT16_16_Q13(short a, short b)
        {
            return CHOP16((SHR(MULT16_16((a), (b)), 13)));
        }

        public static int MULT16_16_Q13(int a, int b)
        {
            return (SHR(MULT16_16((a), (b)), 13));
        }

        //#define MULT16_16_Q14(a,b) (SHR(MULT16_16((a),(b)),14))
        public static short MULT16_16_Q14(short a, short b)
        {
            return CHOP16((SHR(MULT16_16((a), (b)), 14)));
        }

        public static int MULT16_16_Q14(int a, int b)
        {
            return (SHR(MULT16_16((a), (b)), 14));
        }

        //#define MULT16_16_Q15(a,b) (SHR(MULT16_16((a),(b)),15))
        public static short MULT16_16_Q15(short a, short b)
        {
            return CHOP16((SHR(MULT16_16((a), (b)), 15)));
        }

        public static int MULT16_16_Q15(int a, int b)
        {
            return (SHR(MULT16_16((a), (b)), 15));
        }

        //#define MULT16_16_P13(a,b) (SHR(ADD32(4096,MULT16_16((a),(b))),13))
        public static short MULT16_16_P13(short a, short b)
        {
            return CHOP16((SHR(ADD32(4096, MULT16_16((a), (b))), 13)));
        }

        public static int MULT16_16_P13(int a, int b)
        {
            return (SHR(ADD32(4096, MULT16_16((a), (b))), 13));
        }

        //#define MULT16_16_P14(a,b) (SHR(ADD32(8192,MULT16_16((a),(b))),14))
        public static short MULT16_16_P14(short a, short b)
        {
            return CHOP16((SHR(ADD32(8192, MULT16_16((a), (b))), 14)));
        }

        public static int MULT16_16_P14(int a, int b)
        {
            return (SHR(ADD32(8192, MULT16_16((a), (b))), 14));
        }

        //#define MULT16_16_P15(a,b) (SHR(ADD32(16384,MULT16_16((a),(b))),15))
        public static short MULT16_16_P15(short a, short b)
        {
            return CHOP16((SHR(ADD32(16384, MULT16_16((a), (b))), 15)));
        }

        public static int MULT16_16_P15(int a, int b)
        {
            return (SHR(ADD32(16384, MULT16_16((a), (b))), 15));
        }

        //        /** Divide a 32-bit value by a 16-bit value. Result fits in 16 bits */
        //#define DIV32_16(a,b) ((opus_val16)(((opus_val32)(a))/((opus_val16)(b))))
        public static short DIV32_16(int a, short b)
        {
            return CHOP16(((short)(((int)(a)) / ((short)(b)))));
        }

        public static int DIV32_16(int a, int b)
        {
            return a / b;
        }

        //        /** Divide a 32-bit value by a 32-bit value. Result fits in 32 bits */
        //#define DIV32(a,b) (((opus_val32)(a))/((opus_val32)(b)))
        public static int DIV32(int a, int b)
        {
            return a / b;
        }

        // identical to silk_SAT16 - saturate operation
        public static short SAT16(int x)
        {
            return (short)(x > 32767 ? 32767 : x < -32768 ? -32768 : (short)x);
        }

        public static short SIG2WORD16(int x)
        {
            x = PSHR32(x, 12);
            x = MAX32(x, -32768);
            x = MIN32(x, 32767);
            return EXTRACT16(x);
        }

        public static short MIN(short a, short b)
        {
            return ((a) < (b) ? (a) : (b));
        }

        public static short MAX(short a, short b)
        {
            return ((a) > (b) ? (a) : (b));
        }

        public static short MIN16(short a, short b)
        {
            return ((a) < (b) ? (a) : (b));
        }

        public static short MAX16(short a, short b)
        {
            return ((a) > (b) ? (a) : (b));
        }

        public static int MIN16(int a, int b)
        {
            return ((a) < (b) ? (a) : (b));
        }

        public static int MAX16(int a, int b)
        {
            return ((a) > (b) ? (a) : (b));
        }

        public static float MIN16(float a, float b)
        {
            return ((a) < (b) ? (a) : (b));
        }

        public static float MAX16(float a, float b)
        {
            return ((a) > (b) ? (a) : (b));
        }

        public static int MIN(int a, int b)
        {
            return ((a) < (b) ? (a) : (b));
        }

        public static int MAX(int a, int b)
        {
            return ((a) > (b) ? (a) : (b));
        }

        public static int IMIN(int a, int b)
        {
            return ((a) < (b) ? (a) : (b));
        }

        public static uint IMIN(uint a, uint b)
        {
            return ((a) < (b) ? (a) : (b));
        }

        public static int IMAX(int a, int b)
        {
            return ((a) > (b) ? (a) : (b));
        }

        public static int MIN32(int a, int b)
        {
            return ((a) < (b) ? (a) : (b));
        }

        public static int MAX32(int a, int b)
        {
            return ((a) > (b) ? (a) : (b));
        }

        public static float MIN32(float a, float b)
        {
            return ((a) < (b) ? (a) : (b));
        }

        public static float MAX32(float a, float b)
        {
            return ((a) > (b) ? (a) : (b));
        }

        public static int ABS16(int x)
        {
            return ((x) < 0 ? (-(x)) : (x));
        }

        public static float ABS16(float x)
        {
            return ((x) < 0 ? (-(x)) : (x));
        }

        public static short ABS16(short x)
        {
            return CHOP16(((x) < 0 ? (-(x)) : (x)));
        }

        public static int ABS32(int x)
        {
            return ((x) < 0 ? (-(x)) : (x));
        }

        public static uint celt_udiv(uint n, uint d)
        {
            Inlines.OpusAssert(d > 0);
            return n / d;
        }

        public static int celt_udiv(int n, int d)
        {
            Inlines.OpusAssert(d > 0);
            return n / d;
        }

        public static int celt_sudiv(int n, int d)
        {
            Inlines.OpusAssert(d > 0);
            return n / d;
        }

        //#define celt_div(a,b) MULT32_32_Q31((opus_val32)(a),celt_rcp(b))
        public static int celt_div(int a, int b)
        {
            return MULT32_32_Q31((int)(a), celt_rcp(b));
        }

        /** Integer log in base2. Undefined for zero and negative numbers */
        public static int celt_ilog2(int x)
        {
            Inlines.OpusAssert(x > 0, "celt_ilog2() only defined for strictly positive numbers");
#if DEBUG_MACROS
            if (x <= 0)
                throw new ArgumentException("celt_ilog2() only defined for strictly positive numbers");
#endif
            return (EC_ILOG((uint)x) - 1);
        }

        /** Integer log in base2. Defined for zero, but not for negative numbers */
        public static int celt_zlog2(int x)
        {
            return x <= 0 ? 0 : celt_ilog2(x);
        }

        public static int celt_maxabs16(Pointer<int> x, int len)
        {
            int i;
            int maxval = 0;
            int minval = 0;
            for (i = 0; i < len; i++)
            {
                maxval = MAX32(maxval, x[i]);
                minval = MIN32(minval, x[i]);
            }
            return MAX32(EXTEND32(maxval), -EXTEND32(minval));
        }

        public static int celt_maxabs32(Pointer<int> x, int len)
        {
            int i;
            int maxval = 0;
            int minval = 0;
            for (i = 0; i < len; i++)
            {
                maxval = MAX32(maxval, x[i]);
                minval = MIN32(minval, x[i]);
            }
            return MAX32(maxval, -minval);
        }

        /// <summary>
        /// Multiplies two 16-bit fractional values. Bit-exactness of this macro is important
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static int FRAC_MUL16(int a, int b)
        {
            return ((16384 + ((int)((short)a * (short)b))) >> 15);
        }

        /// <summary>
        /// Compute floor(sqrt(_val)) with exact arithmetic.
        /// This has been tested on all possible 32-bit inputs.
        /// </summary>
        /// <param name="_val"></param>
        /// <returns></returns>
        public static uint isqrt32(uint _val)
        {
#if PARITY
            uint b;
            uint g;
            int bshift;
            /*Uses the second method from
               http://www.azillionmonkeys.com/qed/sqroot.html
              The main idea is to search for the largest binary digit b such that
               (g+b)*(g+b) <= _val, and add it to the solution g.*/
            g = 0;
            bshift = (EC_ILOG(_val) - 1) >> 1;
            b = 1U << bshift;
            do
            {
                uint t;
                t = (((uint)g << 1) + b) << bshift;
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
#else
            // This is 100x faster
            return (uint)Math.Sqrt(_val);
#endif
        }

        private static readonly short[] sqrt_C = { 23175, 11561, -3011, 1699, -664 };

        /** Sqrt approximation (QX input, QX/2 output) */
        public static int celt_sqrt(int x)
        {
#if PARITY
            int k;
            short n;
            int rt;

            if (x == 0)
                return 0;
            else if (x >= 1073741824)
                return 32767;
            k = (celt_ilog2(x) >> 1) - 7;
            x = VSHR32(x, 2 * k);
            n = CHOP16(x - 32768);
            rt = ADD16(sqrt_C[0], MULT16_16_Q15(n, ADD16(sqrt_C[1], MULT16_16_Q15(n, ADD16(sqrt_C[2],
                       MULT16_16_Q15(n, ADD16(sqrt_C[3], MULT16_16_Q15(n, (sqrt_C[4])))))))));
            rt = VSHR32(rt, 7 - k);
            return rt;
#else
            // This is 100x faster
            return (int)Math.Sqrt(x);
#endif
        }

        /** Reciprocal approximation (Q15 input, Q16 output) */
        public static int celt_rcp(int x)
        {
#if PARITY
            int i;
            int n;
            int r;
            Inlines.OpusAssert(x > 0, "celt_rcp() only defined for positive values");
            i = celt_ilog2(x);
            /* n is Q15 with range [0,1). */
            n = VSHR32(x, i - 15) - 32768;
            /* Start with a linear approximation:
               r = 1.8823529411764706-0.9411764705882353*n.
               The coefficients and the result are Q14 in the range [15420,30840].*/
            r = ADD16(30840, MULT16_16_Q15(-15420, n));
            /* Perform two Newton iterations:
               r -= r*((r*n)-1.Q15)
                  = r*((r*n)+(r-1.Q15)). */
            r = SUB16(r, MULT16_16_Q15(r,
                      ADD16(MULT16_16_Q15(r, n), ADD16(r, -32768))));
            /* We subtract an extra 1 in the second iteration to avoid overflow; it also
                neatly compensates for truncation error in the rest of the process. */
            r = SUB16(r, ADD16(1, MULT16_16_Q15(r,
                      ADD16(MULT16_16_Q15(r, n), ADD16(r, -32768)))));
            /* r is now the Q15 solution to 2/(n+1), with a maximum relative error
                of 7.05346E-5, a (relative) RMSE of 2.14418E-5, and a peak absolute
                error of 1.24665/32768. */
            return VSHR32(EXTEND32(r), i - 16);
#else
            // 50x faster
            return (int)(((float)(1 << 16) * (float)(1 << 15)) / ((float)x));
#endif
        }

        /** Reciprocal sqrt approximation in the range [0.25,1) (Q16 in, Q14 out) */
        public static int celt_rsqrt_norm(int x)
        {
            int n;
            int r;
            int r2;
            int y;
            /* Range of n is [-16384,32767] ([-0.5,1) in Q15). */
            n = x - 32768;
            /* Get a rough initial guess for the root.
               The optimal minimax quadratic approximation (using relative error) is
                r = 1.437799046117536+n*(-0.823394375837328+n*0.4096419668459485).
               Coefficients here, and the final result r, are Q14.*/
            r = ADD16(23557, MULT16_16_Q15(n, ADD16(-13490, MULT16_16_Q15(n, 6713))));
            /* We want y = x*r*r-1 in Q15, but x is 32-bit Q16 and r is Q14.
               We can compute the result from n and r using Q15 multiplies with some
                adjustment, carefully done to avoid overflow.
               Range of y is [-1564,1594]. */
            r2 = MULT16_16_Q15(r, r);
            y = SHL16(SUB16(ADD16(MULT16_16_Q15(r2, n), r2), 16384), 1);
            /* Apply a 2nd-order Householder iteration: r += r*y*(y*0.375-0.5).
               This yields the Q14 reciprocal square root of the Q16 x, with a maximum
                relative error of 1.04956E-4, a (relative) RMSE of 2.80979E-5, and a
                peak absolute error of 2.26591/16384. */
            return ADD16(r, MULT16_16_Q15(r, MULT16_16_Q15(y,
                       SUB16(MULT16_16_Q15(y, 12288), 16384))));
        }

        public static int frac_div32(int a, int b)
        {
            int rcp;
            int result, rem;
            int shift = celt_ilog2(b) - 29;
            a = VSHR32(a, shift);
            b = VSHR32(b, shift);
            /* 16-bit reciprocal */
            rcp = ROUND16(celt_rcp(ROUND16(b, 16)), 3);
            result = MULT16_32_Q15(rcp, a);
            rem = PSHR32(a, 2) - MULT32_32_Q31(result, b);
            result = ADD32(result, SHL32(MULT16_32_Q15(rcp, rem), 2));
            if (result >= 536870912)       /*  2^29 */
                return 2147483647;          /*  2^31 - 1 */
            else if (result <= -536870912) /* -2^29 */
                return -2147483647;         /* -2^31 */
            else
                return SHL32(result, 2);
        }

        private static readonly short[] log2_C =
            { -6801 + (1 << (3)), 15746, -5217, 2545, -1401 };

        /** Base-2 logarithm approximation (log2(x)). (Q14 input, Q10 output) */
        public static int celt_log2(int x)
        {
#if PARITY
            int i;
            int n, frac;
            /* -0.41509302963303146, 0.9609890551383969, -0.31836011537636605,
                0.15530808010959576, -0.08556153059057618 */
            if (x == 0)
                return -32767;
            i = celt_ilog2(x);
            n = VSHR32(x, i - 15) - 32768 - 16384;
            frac = ADD16(log2_C[0], MULT16_16_Q15(n, ADD16(log2_C[1], MULT16_16_Q15(n, ADD16(log2_C[2], MULT16_16_Q15(n, ADD16(log2_C[3], MULT16_16_Q15(n, log2_C[4]))))))));
            return SHL16(CHOP16(i - 13), 10) + SHR16(frac, 4);
#else
            return (int)((float)(1 << 10) * (float)Math.Log10(x / (float)(1 << 14)) / (float)Math.Log10(2));
#endif
        }

        /*
         K0 = 1
         K1 = log(2)
         K2 = 3-4*log(2)
         K3 = 3*log(2) - 2
        */
        private const int D0 = 16383;
        private const int D1 = 22804;
        private const int D2 = 14819;
        private const int D3 = 10204;

        public static int celt_exp2_frac(int x)
        {
            int frac;
            frac = SHL16(x, 4);
            return ADD16(D0, MULT16_16_Q15(frac, ADD16(D1, MULT16_16_Q15(frac, ADD16(D2, MULT16_16_Q15(D3, frac))))));
        }

        /** Base-2 exponential approximation (2^x). (Q10 input, Q16 output) */
        public static int celt_exp2(int x)
        {
            int integer;
            int frac;
            integer = SHR16(x, 10);
            if (integer > 14)
                return 0x7f000000;
            else if (integer < -15)
                return 0;
            frac = CHOP16(celt_exp2_frac(CHOP16(x - SHL16(CHOP16(integer), 10))));
            return VSHR32(EXTEND32(frac), -integer - 2);
        }

        private const int M1 = 32767;
        private const int M2 = -21;
        private const int M3 = -11943;
        private const int M4 = 4936;

        /* Atan approximation using a 4th order polynomial. Input is in Q15 format
           and normalized by pi/4. Output is in Q15 format */
        public static int celt_atan01(int x)
        {
            return MULT16_16_P15(x, ADD32(M1, MULT16_16_P15(x, ADD32(M2, MULT16_16_P15(x, ADD32(M3, MULT16_16_P15(M4, x)))))));
        }

        /* atan2() approximation valid for positive input values */
        public static int celt_atan2p(int y, int x)
        {
            if (y < x)
            {
                int arg;
                arg = celt_div(SHL32(EXTEND32(y), 15), x);
                if (arg >= 32767)
                    arg = 32767;
                return SHR32(celt_atan01(EXTRACT16(arg)), 1);
            }
            else {
                int arg;
                arg = celt_div(SHL32(EXTEND32(x), 15), y);
                if (arg >= 32767)
                    arg = 32767;
                return 25736 - SHR16(celt_atan01(EXTRACT16(arg)), 1);
            }
        }

        public static int celt_cos_norm(int x)
        {
            x = x & 0x0001ffff;
            if (x > SHL32(EXTEND32(1), 16))
                x = SUB32(SHL32(EXTEND32(1), 17), x);
            if ((x & 0x00007fff) != 0)
            {
                if (x < SHL32(EXTEND32(1), 15))
                {
                    return _celt_cos_pi_2(EXTRACT16(x));
                }
                else {
                    return NEG32(_celt_cos_pi_2(EXTRACT16(65536 - x))); // opus bug: should be neg32?
                }
            }
            else {
                if ((x & 0x0000ffff) != 0)
                    return 0;
                else if ((x & 0x0001ffff) != 0)
                    return -32767;
                else
                    return 32767;
            }
        }

        public static int L1 = 32767;
        public static int L2 = -7651;
        public static int L3 = 8277;
        public static int L4 = -626;

        public static int _celt_cos_pi_2(int x)
        {
            int x2;

            x2 = MULT16_16_P15(x, x);
            return ADD32(1, MIN32(32766, ADD32(SUB16(L1, x2), MULT16_16_P15(x2, ADD32(L2, MULT16_16_P15(x2, ADD32(L3, MULT16_16_P15(L4, x2
                                                                                         ))))))));
        }

        public static short FLOAT2INT16(float x)
        {
            x = x * CeltConstants.CELT_SIG_SCALE;
            if (x < short.MinValue)
                x = short.MinValue;
            if (x > short.MaxValue)
                x = short.MaxValue;
            return (short)x;
        }

#endregion

#region SILK

        // SILK-SPECIFIC INLINES

        /// <summary>
        /// Rotate a32 right by 'rot' bits. Negative rot values result in rotating
        /// left. Output is 32bit int.
        /// </summary>
        /// <param name="a32"></param>
        /// <param name="rot"></param>
        /// <returns></returns>
        public static int silk_ROR32(int a32, int rot)
        {
            return unchecked((int)silk_ROR32(unchecked((uint)a32), rot));
        }

        /// <summary>
        /// Rotate a32 right by 'rot' bits. Negative rot values result in rotating
        /// left. Output is 32bit uint.
        /// </summary>
        /// <param name="a32"></param>
        /// <param name="rot"></param>
        /// <returns></returns>
        public static uint silk_ROR32(uint a32, int rot)
        {
            int m = (0 - rot);
            if (rot == 0)
            {
                return a32;
            }
            else if (rot < 0)
            {
                return ((a32 << m) | (a32 >> (32 - m)));
            }
            else {
                return ((a32 << (32 - rot)) | (a32 >> rot));
            }
        }

        public static int silk_MUL(int a32, int b32)
        {
            int ret = a32 * b32;
#if DEBUG_MACROS
            long ret64 = (long)a32 * (long)b32;
            Inlines.OpusAssert((long)ret == ret64);
#endif
            return ret;
        }

        public static uint silk_MUL_uint(uint a32, uint b32)
        {
            uint ret = a32 * b32;
            Inlines.OpusAssert((ulong)ret == (ulong)a32 * (ulong)b32);
            return ret;
        }

        public static int silk_MLA(int a32, int b32, int c32)
        {
            int ret = silk_ADD32((a32), ((b32) * (c32)));
            Inlines.OpusAssert((long)ret == (long)a32 + (long)b32 * (long)c32);
            return ret;
        }


        public static int silk_MLA_uint(uint a32, uint b32, uint c32)
        {
            uint ret = silk_ADD32((a32), ((b32) * (c32)));
            Inlines.OpusAssert((long)ret == (long)a32 + (long)b32 * (long)c32);
            return (int)ret;
        }

        /// <summary>
        /// ((a32 >> 16)  * (b32 >> 16))
        /// </summary>
        /// <param name="a32"></param>
        /// <param name="b32"></param>
        /// <returns></returns>

        public static int silk_SMULTT(int a32, int b32)
        {
            return ((a32 >> 16) * (b32 >> 16));
        }


        public static int silk_SMLATT(int a32, int b32, int c32)
        {
            return silk_ADD32((a32), ((b32) >> 16) * ((c32) >> 16));
        }


        public static long silk_SMLALBB(long a64, short b16, short c16)
        {
            return silk_ADD64((a64), (long)((int)(b16) * (int)(c16)));
        }


        public static long silk_SMULL(int a32, int b32)
        {
            return (long)a32 * (long)b32;
        }

        /// <summary>
        /// Adds two signed 32-bit values in a way that can overflow, while not relying on undefined behaviour
        /// (just standard two's complement implementation-specific behaviour)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>

        public static int silk_ADD32_ovflw(int a, int b)
        {
            return unchecked((int)((uint)a + (uint)b));
        }

        public static int silk_ADD32_ovflw(uint a, uint b)
        {
            return unchecked((int)(a + b));
        }

        /// <summary>
        /// Subtracts two signed 32-bit values in a way that can overflow, while not relying on undefined behaviour
        /// (just standard two's complement implementation-specific behaviour)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>

        public static int silk_SUB32_ovflw(int a, int b)
        {
            return unchecked((int)((uint)a - (uint)b));
        }

        /// <summary>
        /// Multiply-accumulate macros that allow overflow in the addition (ie, no asserts in debug mode)
        /// </summary>
        /// <param name="a32"></param>
        /// <param name="b32"></param>
        /// <param name="c32"></param>
        /// <returns></returns>

        public static int silk_MLA_ovflw(int a32, int b32, int c32)
        {
            return unchecked(silk_ADD32_ovflw((uint)(a32), (uint)(b32) * (uint)(c32)));
        }


        public static int silk_SMLABB_ovflw(int a32, int b32, int c32)
        {
            return unchecked((silk_ADD32_ovflw((a32), ((int)((short)(b32))) * (int)((short)(c32)))));
        }


        public static int silk_SMULBB(int a32, int b32)
        {
            return ((int)unchecked((short)a32) * (int)unchecked((short)b32));
        }

        /// <summary>
        /// (a32 * (int)((short)(b32))) >> 16 output have to be 32bit int
        /// </summary>
        /// <param name="a32"></param>
        /// <param name="b32"></param>
        /// <returns></returns>

        public static int silk_SMULWB(int a32, int b32)
        {
#if DEBUG_MACROS
            int ret;
            ret = ((a32 >> 16) * (int)((short)b32) + (((a32 & 0x0000FFFF) * (int)((short)b32)) >> 16));
            if ((long)ret != ((long)a32 * (short)b32) >> 16)
            {
                Inlines.OpusAssert(false);
            }
            return ret;
#else
            return unchecked((int)(unchecked(unchecked(a32 * (long)(unchecked((short)b32))) >> 16)));
#endif
        }


        public static int silk_SMLABB(int a32, int b32, int c32)
        {
            return ((a32) + ((int)unchecked((short)b32)) * (int)unchecked((short)c32));
        }

        public static int silk_DIV32_16(int a32, int b32)
        {
#if DEBUG_MACROS
            bool fail = false;
            fail |= b32 == 0;
            fail |= b32 > short.MaxValue;
            fail |= b32 < short.MinValue;
            Inlines.OpusAssert(!fail);
#endif
            return a32 / b32;
        }

        public static int silk_DIV32(int a32, int b32)
        {
            return a32 / b32;
        }


        public static short silk_ADD16(short a, short b)
        {
            short ret = (short)(a + b);
#if DEBUG_MACROS
            if (ret != silk_ADD_SAT16(a, b))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;
        }


        public static int silk_ADD32(int a, int b)
        {
            int ret = a + b;
#if DEBUG_MACROS
            if (ret != silk_ADD_SAT32(a, b))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;
        }

        public static uint silk_ADD32(uint a, uint b)
        {
            uint ret = a + b;
            return ret;
        }

        public static long silk_ADD64(long a, long b)
        {
            long ret = a + b;
            Inlines.OpusAssert(ret == silk_ADD_SAT64(a, b));
            return ret;
        }


        public static short silk_SUB16(short a, short b)
        {
            short ret = (short)(a - b);
            Inlines.OpusAssert(ret == silk_SUB_SAT16(a, b));
            return ret;
        }


        public static int silk_SUB32(int a, int b)
        {
            int ret = a - b;
            Inlines.OpusAssert(ret == silk_SUB_SAT32(a, b));
            return ret;
        }


        public static long silk_SUB64(long a, long b)
        {
            long ret = a - b;
            Inlines.OpusAssert(ret == silk_SUB_SAT64(a, b));
            return ret;
        }


        public static int silk_SAT8(int a)
        {
            return a > byte.MaxValue ? byte.MaxValue : ((a) < byte.MinValue ? byte.MinValue : (a));
        }


        public static int silk_SAT16(int a)
        {
            return a > short.MaxValue ? short.MaxValue : ((a) < short.MinValue ? short.MinValue : (a));
        }
        
        public static int silk_SAT32(long a)
        {
            return a > int.MaxValue ? int.MaxValue : ((a) < int.MinValue ? int.MinValue : (int)(a));
        }

        // Truncation macros defined for safety while porting //

        public static sbyte CHOP8(int a)
        {
            //if (a > sbyte.MaxValue)
            //    a = sbyte.MaxValue;
            //else if (a < sbyte.MinValue)
            //    a = sbyte.MinValue;
            return checked((sbyte)a);
        }

        public static byte CHOP8U(int a)
        {
            //if (a > byte.MaxValue)
            //    a = byte.MaxValue;
            //else if (a < byte.MinValue)
            //    a = byte.MinValue;
            return checked((byte)a);
        }

        public static sbyte CHOP8(long a)
        {
            //if (a > sbyte.MaxValue)
            //    a = sbyte.MaxValue;
            //else if (a < sbyte.MinValue)
            //    a = sbyte.MinValue;
            return checked((sbyte)a);
        }

        public static short CHOP16(int a)
        {
            //if (a > short.MaxValue)
            //    a = short.MaxValue;
            //else if (a < short.MinValue)
            //    a = short.MinValue;
            return checked((short)a);
        }

        public static short CHOP16(long a)
        {
            //if (a > short.MaxValue)
            //    a = short.MaxValue;
            //else if (a < short.MinValue)
            //    a = short.MinValue;
            return checked((short)a);
        }

        public static int CHOP32(long a)
        {
            //if (a > int.MaxValue)
            //    a = int.MaxValue;
            //else if (a < int.MinValue)
            //    a = int.MinValue;
            return checked((int)a);
        }

        public static uint CHOP32U(long a)
        {
            //if (a > uint.MaxValue)
            //    a = uint.MaxValue;
            //else if (a < uint.MinValue)
            //    a = uint.MinValue;
            return checked((uint)a);
        }

        /// <summary>
        /// //////////////////
        /// </summary>
        /// <param name="a16"></param>
        /// <param name="b16"></param>
        /// <returns></returns>

        public static short silk_ADD_SAT16(short a16, short b16)
        {
            short res = (short)silk_SAT16(silk_ADD32((int)(a16), (b16)));
            Inlines.OpusAssert(res == silk_SAT16((int)a16 + (int)b16));
            return res;
        }

        public static int silk_ADD_SAT32(int a32, int b32)
        {
            int res = (unchecked(((uint)(a32) + (uint)(b32)) & 0x80000000) == 0 ?
                ((((a32) & (b32)) & 0x80000000) != 0 ? int.MinValue : (a32) + (b32)) :
                ((((a32) | (b32)) & 0x80000000) == 0 ? int.MaxValue : (a32) + (b32)));
            Inlines.OpusAssert(res == silk_SAT32((long)a32 + (long)b32));
            return res;
        }

        public static long silk_ADD_SAT64(long a64, long b64)
        {
            long res;
            res = (unchecked((ulong)(a64 + b64) & 0x8000000000000000UL) == 0 ?
                (unchecked((ulong)(a64 & b64) & 0x8000000000000000UL) != 0 ? long.MinValue : a64 + b64) :
                (unchecked((ulong)(a64 | b64) & 0x8000000000000000UL) == 0 ? long.MaxValue : a64 + b64));
#if DEBUG_MACROS
            bool fail = false;
            if (res != a64 + b64)
            {
                /* Check that we saturated to the correct extreme value */
                if (!((res == long.MaxValue && ((a64 >> 1) + (b64 >> 1) > (long.MaxValue >> 3))) ||
                       (res == long.MinValue && ((a64 >> 1) + (b64 >> 1) < (long.MinValue >> 3)))))
                {
                    fail = true;
                }
            }
            else
            {
                /* Saturation not necessary */
                fail = res != a64 + b64;
            }
            Inlines.OpusAssert(!fail);
#endif
            return res;
        }

        public static short silk_SUB_SAT16(short a16, short b16)
        {
            short res = (short)silk_SAT16(silk_SUB32((int)(a16), (b16)));
            Inlines.OpusAssert(res == silk_SAT16((int)a16 - (int)b16));
            return res;
        }

        public static int silk_SUB_SAT32(int a32, int b32)
        {
            int res = (unchecked(((uint)(a32) - (uint)(b32)) & 0x80000000) == 0 ?
                (((a32) & ((b32) ^ 0x80000000) & 0x80000000) != 0 ? int.MinValue : (a32) - (b32)) :
                ((((a32) ^ 0x80000000) & (b32) & 0x80000000) != 0 ? int.MaxValue : (a32) - (b32)));
            Inlines.OpusAssert(res == silk_SAT32((long)a32 - (long)b32));
            return res;
        }

        public static long silk_SUB_SAT64(long a64, long b64)
        {
            long res;
            res = (unchecked((ulong)((a64) - (b64)) & 0x8000000000000000UL) == 0 ?
                (((ulong)(a64) & ((ulong)(b64) ^ 0x8000000000000000UL) & 0x8000000000000000UL) != 0 ? long.MinValue : (a64) - (b64)) :
                ((((ulong)(a64) ^ 0x8000000000000000UL) & (ulong)(b64) & 0x8000000000000000UL) != 0 ? long.MaxValue : (a64) - (b64)));
#if DEBUG_MACROS
            bool fail = false;
            if (res != a64 - b64)
            {
                /* Check that we saturated to the correct extreme value */
                if (!((res == long.MaxValue && ((a64 >> 1) + (b64 >> 1) > (long.MaxValue >> 3))) ||
                      (res == long.MinValue && ((a64 >> 1) + (b64 >> 1) < (long.MinValue >> 3)))))
                {
                    fail = true;
                }
            }
            else
            {
                /* Saturation not necessary */
                fail = res != a64 - b64;
            }
            Inlines.OpusAssert(!fail);
#endif
            return res;
        }

        ///* Saturation for positive input values */
        //#define silk_POS_SAT32(a)                   ((a) > int_MAX ? int_MAX : (a))

        /// <summary>
        /// Add with saturation for positive input values
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>

        public static sbyte silk_ADD_POS_SAT8(sbyte a, sbyte b)
        {
            return (sbyte)((((a + b) & 0x80) != 0) ? sbyte.MaxValue : (a + b));
        }

        /// <summary>
        /// Add with saturation for positive input values
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>

        public static short silk_ADD_POS_SAT16(short a, short b)
        {
            return (short)(unchecked(((a + b) & 0x8000) != 0) ? short.MaxValue : (a + b));
        }

        /// <summary>
        /// Add with saturation for positive input values
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>

        public static int silk_ADD_POS_SAT32(int a, int b)
        {
            return (unchecked(((a + b) & 0x80000000) != 0) ? int.MaxValue : (a + b));
        }

        /// <summary>
        /// Add with saturation for positive input values
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>

        public static long silk_ADD_POS_SAT64(long a, long b)
        {
            return ((unchecked((ulong)(a + b) & 0x8000000000000000L) != 0) ? long.MaxValue : (a + b));
        }

        public static sbyte silk_LSHIFT8(sbyte a, int shift)
        {
            sbyte ret = (sbyte)(a << shift);
#if DEBUG_MACROS
            bool fail = false;
            fail |= shift < 0;
            fail |= shift >= 8;
            fail |= (long)ret != ((long)a) << shift;
            Inlines.OpusAssert(!fail);
#endif
            return ret;
        }

        public static short silk_LSHIFT16(short a, int shift)
        {
            short ret = (short)(a << shift);
#if DEBUG_MACROS
            bool fail = false;
            fail |= shift < 0;
            fail |= shift >= 16;
            fail |= (long)ret != ((long)a) << shift;
            Inlines.OpusAssert(!fail);
#endif
            return ret;
        }

        public static int silk_LSHIFT32(int a, int shift)
        {
            int ret = a << shift;
#if DEBUG_MACROS
            bool fail = false;
            fail |= shift < 0;
            fail |= shift >= 32;
            fail |= (long)ret != ((long)a) << shift;
            Inlines.OpusAssert(!fail);
#endif
            return ret;
        }

        public static long silk_LSHIFT64(long a, int shift)
        {
            long ret = a << shift;
#if DEBUG_MACROS
            bool fail = false;
            fail |= shift < 0;
            fail |= shift >= 64;
            fail |= (ret >> shift) != ((long)a);
            Inlines.OpusAssert(!fail);
#endif
            return ret;
        }

        public static int silk_LSHIFT(int a, int shift)
        {
            int ret = a << shift;
#if DEBUG_MACROS
            bool fail = false;
            fail |= shift < 0;
            fail |= shift >= 32;
            fail |= (long)ret != ((long)a) << shift;
            Inlines.OpusAssert(!fail);
#endif
            return ret;
        }

        public static int silk_LSHIFT_ovflw(int a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 32)) /* no check for overflow */
            {
                Inlines.OpusAssert(false);
            }
#endif
            return a << shift;
        }

        public static uint silk_LSHIFT_uint(uint a, int shift)
        {
            uint ret = a << shift;
#if DEBUG_MACROS
            if ((shift < 0) || ((long)ret != ((long)a) << shift))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;
        }

        /// <summary>
        /// saturates before shifting
        /// </summary>
        /// <param name="a"></param>
        /// <param name="shift"></param>
        /// <returns></returns>
        public static int silk_LSHIFT_SAT32(int a, int shift)
        {
            return (silk_LSHIFT32(silk_LIMIT((a), silk_RSHIFT32(int.MinValue, (shift)), silk_RSHIFT32(int.MaxValue, (shift))), (shift)));
        }

        public static sbyte silk_RSHIFT8(sbyte a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 8))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return (sbyte)(a >> shift);
        }

        public static short silk_RSHIFT16(short a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 16))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return (short)(a >> shift);
        }

        public static int silk_RSHIFT32(int a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 32))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return a >> shift;
        }

        public static int silk_RSHIFT(int a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 32))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return a >> shift;
        }

        public static long silk_RSHIFT64(long a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 64))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return a >> shift;
        }

        public static uint silk_RSHIFT_uint(uint a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 32))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return a >> shift;
        }

        public static int silk_ADD_LSHIFT(int a, int b, int shift)
        {
            int ret = a + (b << shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a + (((long)b) << shift)))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;                /* shift >= 0 */
        }

        public static int silk_ADD_LSHIFT32(int a, int b, int shift)
        {
            int ret = a + (b << shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a + (((long)b) << shift)))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;                /* shift >= 0 */
        }

        public static uint silk_ADD_LSHIFT_uint(uint a, uint b, int shift)
        {
            uint ret;
            ret = a + (b << shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 32) || ((long)ret != (long)a + (((long)b) << shift)))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;                /* shift >= 0 */
        }

        public static int silk_ADD_RSHIFT(int a, int b, int shift)
        {
            int ret = a + (b >> shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a + (((long)b) >> shift)))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;                /* shift  > 0 */
        }

        public static int silk_ADD_RSHIFT32(int a, int b, int shift)
        {
            int ret = a + (b >> shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a + (((long)b) >> shift)))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;                /* shift  > 0 */
        }

        public static uint silk_ADD_RSHIFT_uint(uint a, uint b, int shift)
        {
            uint ret;
            ret = a + (b >> shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 32) || ((long)ret != (long)a + (((long)b) >> shift)))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;                /* shift  > 0 */
        }

        public static int silk_SUB_LSHIFT32(int a, int b, int shift)
        {
            int ret;
            ret = a - (b << shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a - (((long)b) << shift)))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;                /* shift >= 0 */
        }

        public static int silk_SUB_RSHIFT32(int a, int b, int shift)
        {
            int ret;
            ret = a - (b >> shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a - (((long)b) >> shift)))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;                /* shift  > 0 */
        }

        public static int silk_RSHIFT_ROUND(int a, int shift)
        {
            int ret;
            ret = shift == 1 ? (a >> 1) + (a & 1) : ((a >> (shift - 1)) + 1) >> 1;
#if DEBUG_MACROS
            /* the marco definition can't handle a shift of zero */
            if ((shift <= 0) || (shift > 31) || ((long)ret != ((long)a + ((long)1 << (shift - 1))) >> shift))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;
        }

        public static long silk_RSHIFT_ROUND64(long a, int shift)
        {
            long ret;
#if DEBUG_MACROS
            /* the macro definition can't handle a shift of zero */
            if ((shift <= 0) || (shift >= 64))
            {
                Inlines.OpusAssert(false);
            }
#endif
            ret = shift == 1 ? (a >> 1) + (a & 1) : ((a >> (shift - 1)) + 1) >> 1;
            return ret;
        }

        ///* Number of rightshift required to fit the multiplication */
        //#define silk_NSHIFT_MUL_32_32(a, b)         ( -(31- (32-silk_CLZ32(silk_abs(a)) + (32-silk_CLZ32(silk_abs(b))))) )
        //#define silk_NSHIFT_MUL_16_16(a, b)         ( -(15- (16-silk_CLZ16(silk_abs(a)) + (16-silk_CLZ16(silk_abs(b))))) )


        public static int silk_min(int a, int b)
        {
            return ((a) < (b)) ? (a) : (b);
        }


        public static int silk_max(int a, int b)
        {
            return ((a) > (b)) ? (a) : (b);
        }

        public static float silk_min(float a, float b)
        {
            return ((a) < (b)) ? (a) : (b);
        }


        public static float silk_max(float a, float b)
        {
            return ((a) > (b)) ? (a) : (b);
        }

        /// <summary>
        /// Macro to convert floating-point constants to fixed-point by applying a scalar factor
        /// </summary>

        public static int SILK_CONST(float number, int scale)
        {
            return ((int)((number) * ((long)1 << (scale)) + 0.5));
        }

        /* silk_min() versions with typecast in the function call */

        public static int silk_min_int(int a, int b)
        {
            return (((a) < (b)) ? (a) : (b));
        }


        public static short silk_min_16(short a, short b)
        {
            return (((a) < (b)) ? (a) : (b));
        }


        public static int silk_min_32(int a, int b)
        {
            return (((a) < (b)) ? (a) : (b));
        }


        public static long silk_min_64(long a, long b)
        {
            return (((a) < (b)) ? (a) : (b));
        }

        /* silk_min() versions with typecast in the function call */

        public static int silk_max_int(int a, int b)
        {
            return (((a) > (b)) ? (a) : (b));
        }


        public static short silk_max_16(short a, short b)
        {
            return (((a) > (b)) ? (a) : (b));
        }


        public static int silk_max_32(int a, int b)
        {
            return (((a) > (b)) ? (a) : (b));
        }


        public static long silk_max_64(long a, long b)
        {
            return (((a) > (b)) ? (a) : (b));
        }

        public static float silk_LIMIT(float a, float limit1, float limit2)
        {
            return ((limit1) > (limit2) ? ((a) > (limit1) ? (limit1) : ((a) < (limit2) ? (limit2) : (a))) : ((a) > (limit2) ? (limit2) : ((a) < (limit1) ? (limit1) : (a))));
        }

        public static int silk_LIMIT(int a, int limit1, int limit2)
        {
            return silk_LIMIT_32(a, limit1, limit2);
        }


        public static int silk_LIMIT_int(int a, int limit1, int limit2)
        {
            return silk_LIMIT_32(a, limit1, limit2);
        }


        public static short silk_LIMIT_16(short a, short limit1, short limit2)
        {
            return ((limit1) > (limit2) ? ((a) > (limit1) ? (limit1) : ((a) < (limit2) ? (limit2) : (a))) : ((a) > (limit2) ? (limit2) : ((a) < (limit1) ? (limit1) : (a))));
        }


        public static int silk_LIMIT_32(int a, int limit1, int limit2)
        {
            return ((limit1) > (limit2) ? ((a) > (limit1) ? (limit1) : ((a) < (limit2) ? (limit2) : (a))) : ((a) > (limit2) ? (limit2) : ((a) < (limit1) ? (limit1) : (a))));
        }


        public static int silk_abs(int a)
        {
            // Be careful, silk_abs returns wrong when input equals to silk_intXX_MIN
            return ((a) > 0) ? (a) : -(a);
        }


        public static int silk_abs_int16(int a)
        {
            return (a ^ (a >> 15)) - (a >> 15);
        }


        public static int silk_abs_int32(int a)
        {

            return (a ^ (a >> 31)) - (a >> 31);
        }


        public static long silk_abs_int64(long a)
        {
            return ((a) > 0) ? (a) : -(a);
        }


        public static long silk_sign(int a)
        {
            return (a) > 0 ? 1 : ((a) < 0 ? -1 : 0);
        }

        /// <summary>
        /// PSEUDO-RANDOM GENERATOR
        /// Make sure to store the result as the seed for the next call (also in between
        /// frames), otherwise result won't be random at all. When only using some of the
        /// bits, take the most significant bits by right-shifting.
        /// </summary>

        public static int silk_RAND(int seed)
        {
            return silk_MLA_ovflw(907633515, seed, 196314165);
        }

        /// <summary>
        /// silk_SMMUL: Signed top word multiply.
        /// </summary>
        /// <param name="a32"></param>
        /// <param name="b32"></param>
        /// <returns></returns>

        public static int silk_SMMUL(int a32, int b32)
        {
            return (int)silk_RSHIFT64(silk_SMULL((a32), (b32)), 32);
        }

        /* a32 + (b32 * (c32 >> 16)) >> 16 */
        public static int silk_SMLAWT(int a32, int b32, int c32)
        {
            int ret = a32 + ((b32 >> 16) * (c32 >> 16)) + (((b32 & 0x0000FFFF) * ((c32 >> 16)) >> 16));
#if DEBUG_MACROS
            if ((long)ret != (long)a32 + (((long)b32 * (c32 >> 16)) >> 16))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;
        }

        /// <summary>
        /// Divide two int32 values and return result as int32 in a given Q-domain
        /// </summary>
        /// <param name="a32">I    numerator (Q0)</param>
        /// <param name="b32">I    denominator (Q0)</param>
        /// <param name="Qres">I    Q-domain of result (>= 0)</param>
        /// <returns>O    returns a good approximation of "(a32 << Qres) / b32"</returns>
        public static int silk_DIV32_varQ(int a32, int b32, int Qres)
        {
            int a_headrm, b_headrm, lshift;
            int b32_inv, a32_nrm, b32_nrm, result;

            Inlines.OpusAssert(b32 != 0);
            Inlines.OpusAssert(Qres >= 0);

            /* Compute number of bits head room and normalize inputs */
            a_headrm = silk_CLZ32(silk_abs(a32)) - 1;
            a32_nrm = silk_LSHIFT(a32, a_headrm);                                       /* Q: a_headrm                  */
            b_headrm = silk_CLZ32(silk_abs(b32)) - 1;
            b32_nrm = silk_LSHIFT(b32, b_headrm);                                       /* Q: b_headrm                  */

            /* Inverse of b32, with 14 bits of precision */
            b32_inv = silk_DIV32_16(int.MaxValue >> 2, silk_RSHIFT(b32_nrm, 16));   /* Q: 29 + 16 - b_headrm        */

            /* First approximation */
            result = silk_SMULWB(a32_nrm, b32_inv);                                     /* Q: 29 + a_headrm - b_headrm  */

            /* Compute residual by subtracting product of denominator and first approximation */
            /* It's OK to overflow because the final value of a32_nrm should always be small */
            a32_nrm = silk_SUB32_ovflw(a32_nrm, silk_LSHIFT_ovflw(silk_SMMUL(b32_nrm, result), 3));  /* Q: a_headrm   */

            /* Refinement */
            result = silk_SMLAWB(result, a32_nrm, b32_inv);                             /* Q: 29 + a_headrm - b_headrm  */

            /* Convert to Qres domain */
            lshift = 29 + a_headrm - b_headrm - Qres;
            if (lshift < 0)
            {
                return silk_LSHIFT_SAT32(result, -lshift);
            }
            else
            {
                if (lshift < 32)
                {
                    return silk_RSHIFT(result, lshift);
                }
                else
                {
                    /* Avoid undefined result */
                    return 0;
                }
            }
        }

        /// <summary>
        /// Invert int32 value and return result as int32 in a given Q-domain
        /// </summary>
        /// <param name="b32">I    denominator (Q0)</param>
        /// <param name="Qres">I    Q-domain of result (> 0)</param>
        /// <returns>a good approximation of "(1 << Qres) / b32"</returns>

        public static int silk_INVERSE32_varQ(int b32, int Qres)
        {
            int b_headrm, lshift;
            int b32_inv, b32_nrm, err_Q32, result;

            Inlines.OpusAssert(b32 != 0);
            Inlines.OpusAssert(Qres > 0);

            /* Compute number of bits head room and normalize input */
            b_headrm = silk_CLZ32(silk_abs(b32)) - 1;
            b32_nrm = silk_LSHIFT(b32, b_headrm);                                       /* Q: b_headrm                */

            /* Inverse of b32, with 14 bits of precision */
            b32_inv = silk_DIV32_16(int.MaxValue >> 2, CHOP16(silk_RSHIFT(b32_nrm, 16)));   /* Q: 29 + 16 - b_headrm    */

            /* First approximation */
            result = silk_LSHIFT(b32_inv, 16);                                          /* Q: 61 - b_headrm            */

            /* Compute residual by subtracting product of denominator and first approximation from one */
            err_Q32 = silk_LSHIFT(((int)1 << 29) - silk_SMULWB(b32_nrm, b32_inv), 3);        /* Q32                        */

            /* Refinement */
            result = silk_SMLAWW(result, err_Q32, b32_inv);                             /* Q: 61 - b_headrm            */

            /* Convert to Qres domain */
            lshift = 61 - b_headrm - Qres;
            if (lshift <= 0)
            {
                return silk_LSHIFT_SAT32(result, -lshift);
            }
            else
            {
                if (lshift < 32)
                {
                    return silk_RSHIFT(result, lshift);
                }
                else
                {
                    /* Avoid undefined result */
                    return 0;
                }
            }
        }

        //////////////////////// from macros.h /////////////////////////////////////////////

        /// <summary>
        /// a32 + (b32 * (int)((short)(c32))) >> 16 output have to be 32bit int
        /// </summary>

        public static int silk_SMLAWB(int a32, int b32, int c32)
        {
            //return (int)(a32 + ((b32 * (long)((short)c32)) >> 16));
            int ret;
            ret = silk_ADD32(a32, silk_SMULWB(b32, c32));
#if DEBUG_MACROS
            if (silk_ADD32(a32, silk_SMULWB(b32, c32)) != silk_ADD_SAT32(a32, silk_SMULWB(b32, c32)))
            {
                Inlines.OpusAssert(false);
            }
#endif
            return ret;
        }

        ///* (a32 * (b32 >> 16)) >> 16 */
        public static int silk_SMULWT(int a32, int b32)
        {
            return (((a32) >> 16) * ((b32) >> 16) + ((((a32) & 0x0000FFFF) * ((b32) >> 16)) >> 16));
        }

        ///* (int)((short)(a32)) * (b32 >> 16) */
        public static int silk_SMULBT(int a32, int b32)
        {
            return ((int)((short)(a32)) * ((b32) >> 16));
        }

        ///* a32 + (int)((short)(b32)) * (c32 >> 16) */
        public static int silk_SMLABT(int a32, int b32, int c32)
        {
            return ((a32) + ((int)((short)(b32))) * ((c32) >> 16));
        }

        ///* a64 + (b32 * c32) */
        public static long silk_SMLAL(long a64, int b32, int c32)
        {
            return (silk_ADD64((a64), ((long)(b32) * (long)(c32))));
        }

        public static T matrix_ptr<T>(T[] Matrix_base_adr, int row, int column, int N)
        {
            return Matrix_base_adr[((row) * (N)) + (column)];
        }

        public static Pointer<T> matrix_adr<T>(T[] Matrix_base_adr, int row, int column, int N)
        {
            return Matrix_base_adr.GetPointer(((row) * (N)) + (column));
        }

        public static T matrix_c_ptr<T>(T[] Matrix_base_adr, int row, int column, int M)
        {
            return Matrix_base_adr[(row) + ((M) * (column))];
        }

        public static T matrix_ptr<T>(Pointer<T> Matrix_base_adr, int row, int column, int N)
        {
            return Matrix_base_adr[((row) * (N)) + (column)];
        }

        // FIXME this should be replaced by a "SetMatrix" macro or something
        public static Pointer<T> matrix_adr<T>(Pointer<T> Matrix_base_adr, int row, int column, int N)
        {
            return Matrix_base_adr.Point(((row) * (N)) + (column));
        }

        public static T matrix_c_ptr<T>(Pointer<T> Matrix_base_adr, int row, int column, int M)
        {
            return Matrix_base_adr[(row) + ((M) * (column))];
        }

        public static Pointer<T> matrix_c_adr<T>(Pointer<T> Matrix_base_adr, int row, int column, int M)
        {
            return Matrix_base_adr.Point((row) + ((M) * (column)));
        }

        /// <summary>
        /// (a32 * b32) >> 16
        /// </summary>
        public static int silk_SMULWW(int a32, int b32)
        {
#if DEBUG_MACROS
            int ret, tmp1, tmp2;
            long ret64;
            bool fail = false;

            ret = silk_SMULWB(a32, b32);
            tmp1 = silk_RSHIFT_ROUND(b32, 16);
            tmp2 = silk_MUL(a32, tmp1);

            fail |= (long)tmp2 != (long)a32 * (long)tmp1;

            tmp1 = ret;
            ret = silk_ADD32(tmp1, tmp2);
            fail |= silk_ADD32(tmp1, tmp2) != silk_ADD_SAT32(tmp1, tmp2);

            ret64 = silk_RSHIFT64(silk_SMULL(a32, b32), 16);
            fail |= (long)ret != ret64;

            if (fail)
            {
                Inlines.OpusAssert(false);
            }

            return ret;
#else
            //return CHOP32(((long)(a32) * (b32)) >> 16);
            return silk_MLA(silk_SMULWB((a32), (b32)), (a32), silk_RSHIFT_ROUND((b32), 16));
#endif
        }

        /// <summary>
        /// a32 + ((b32 * c32) >> 16)
        /// </summary>
        public static int silk_SMLAWW(int a32, int b32, int c32)
        {
#if DEBUG_MACROS
            int ret, tmp;

            tmp = silk_SMULWW(b32, c32);
            ret = silk_ADD32(a32, tmp);
            if (ret != silk_ADD_SAT32(a32, tmp))
            {
                Inlines.OpusAssert(false);
            }
            return ret;
#else
            //return CHOP32(((a32) + (((long)(b32) * (c32)) >> 16)));
            return silk_MLA(silk_SMLAWB((a32), (b32), (c32)), (b32), silk_RSHIFT_ROUND((c32), 16));
#endif
        }

        /* count leading zeros of opus_int64 */
        public static int silk_CLZ64(long input)
        {
            int in_upper;

            in_upper = (int)silk_RSHIFT64(input, 32);
            if (in_upper == 0)
            {
                /* Search in the lower 32 bits */
                return 32 + silk_CLZ32(unchecked((int)input));
            }
            else {
                /* Search in the upper 32 bits */
                return silk_CLZ32(in_upper);
            }
        }

        public static int silk_CLZ32(int in32)
        {
            return in32 == 0 ? 32 : 32 - EC_ILOG(unchecked((uint)in32));
        }

        /// <summary>
        /// Get number of leading zeros and fractional part (the bits right after the leading one)
        /// </summary>
        /// <param name="input">input</param>
        /// <param name="lz">number of leading zeros</param>
        /// <param name="frac_Q7">the 7 bits right after the leading one</param>

        public static void silk_CLZ_FRAC(int input, out int lz, out int frac_Q7)
        {
            int lzeros = silk_CLZ32(input);

            lz = lzeros;
            frac_Q7 = silk_ROR32(input, 24 - lzeros) & 0x7f;
        }

        /// <summary>
        /// Approximation of square root.
        /// Accuracy: +/- 10%  for output values > 15
        ///           +/- 2.5% for output values > 120
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>

        public static int silk_SQRT_APPROX(int x)
        {
#if PARITY
            int y, lz, frac_Q7;

            if (x <= 0)
            {
                return 0;
            }

            silk_CLZ_FRAC(x, out lz, out frac_Q7);

            if ((lz & 1) != 0)
            {
                y = 32768;
            }
            else {
                y = 46214;        // 46214 = sqrt(2) * 32768
            }

            // get scaling right
            y >>= silk_RSHIFT(lz, 1);

            // increment using fractional part of input
            y = silk_SMLAWB(y, y, silk_SMULBB(213, frac_Q7));

            return y;
#else
            // This is 10x faster
            return (int)(Math.Sqrt(x));
#endif
        }

        public static int MUL32_FRAC_Q(int a32, int b32, int Q)
        {
            return ((int)(silk_RSHIFT_ROUND64(silk_SMULL(a32, b32), Q)));
        }

        /// <summary>
        /// Approximation of 128 * log2() (very close inverse of silk_log2lin())
        /// Convert input to a log scale
        /// </summary>
        /// <param name="inLin">(I) input in linear scale</param>
        /// <returns></returns>

        public static int silk_lin2log(int inLin)
        {
            int lz, frac_Q7;

            silk_CLZ_FRAC(inLin, out lz, out frac_Q7);

            // Piece-wise parabolic approximation
            return silk_LSHIFT(31 - lz, 7) + silk_SMLAWB(frac_Q7, silk_MUL(frac_Q7, 128 - frac_Q7), 179);
        }

        /// <summary>
        /// Approximation of 2^() (very close inverse of silk_lin2log())
        /// Convert input to a linear scale
        /// </summary>
        /// <param name="inLog_Q7">input on log scale</param>
        /// <returns>Linearized value</returns>
        public static int silk_log2lin(int inLog_Q7)
        {
            int output, frac_Q7;

            if (inLog_Q7 < 0)
            {
                return 0;
            }
            else if (inLog_Q7 >= 3967)
            {
                return int.MaxValue;
            }

            output = silk_LSHIFT(1, silk_RSHIFT(inLog_Q7, 7));
            frac_Q7 = inLog_Q7 & 0x7F;

            if (inLog_Q7 < 2048)
            {
                /* Piece-wise parabolic approximation */
                output = silk_ADD_RSHIFT32(output, silk_MUL(output, silk_SMLAWB(frac_Q7, silk_SMULBB(frac_Q7, 128 - frac_Q7), -174)), 7);
            }
            else
            {
                /* Piece-wise parabolic approximation */
                output = silk_MLA(output, silk_RSHIFT(output, 7), silk_SMLAWB(frac_Q7, silk_SMULBB(frac_Q7, 128 - frac_Q7), -174));
            }

            return output;
        }

        /// <summary>
        /// Interpolate two vectors
        /// </summary>
        /// <param name="xi">(O) interpolated vector [MAX_LPC_ORDER]</param>
        /// <param name="x0">(I) first vector [MAX_LPC_ORDER]</param>
        /// <param name="x1">(I) second vector [MAX_LPC_ORDER]</param>
        /// <param name="ifact_Q2">(I) interp. factor, weight on 2nd vector</param>
        /// <param name="d">(I) number of parameters</param>

        public static void silk_interpolate(
            Pointer<short> xi,
            Pointer<short> x0,
            Pointer<short> x1,
            int ifact_Q2,
            int d)
        {
            int i;

            Inlines.OpusAssert(ifact_Q2 >= 0);
            Inlines.OpusAssert(ifact_Q2 <= 4);

            for (i = 0; i < d; i++)
            {
                xi[i] = (short)silk_ADD_RSHIFT(x0[i], silk_SMULBB(x1[i] - x0[i], ifact_Q2), 2);
            }
        }

        /// <summary>
        /// Inner product with bit-shift
        /// </summary>
        /// <param name="inVec1">I input vector 1</param>
        /// <param name="inVec2">I input vector 2</param>
        /// <param name="scale">I number of bits to shift</param>
        /// <param name="len">I vector lengths</param>
        /// <returns></returns>

        public static int silk_inner_prod_aligned_scale(
            Pointer<short> inVec1,
            Pointer<short> inVec2,
            int scale,
            int len)
        {
            int i, sum = 0;
            for (i = 0; i < len; i++)
            {
                sum = silk_ADD_RSHIFT32(sum, silk_SMULBB(inVec1[i], inVec2[i]), scale);
            }

            return sum;
        }

        /* Copy and multiply a vector by a constant */
        public static void silk_scale_copy_vector16(
            Pointer<short> data_out,
        Pointer<short> data_in,
        int gain_Q16,           /* I    Gain in Q16                                                 */
        int dataSize            /* I    Length                                                      */
    )
        {
            int i;
            int tmp32;

            for (i = 0; i < dataSize; i++)
            {
                tmp32 = silk_SMULWB(gain_Q16, data_in[i]);
                data_out[i] = CHOP16(tmp32);
            }
        }

        /* Multiply a vector by a constant */
        public static void silk_scale_vector32_Q26_lshift_18(
            Pointer<int> data1,             /* I/O  Q0/Q18                                                      */
            int gain_Q26,           /* I    Q26                                                         */
            int dataSize            /* I    length                                                      */
        )
        {
            int i;

            for (i = 0; i < dataSize; i++)
            {
                data1[i] = CHOP32(silk_RSHIFT64(silk_SMULL(data1[i], gain_Q26), 8));    /* OUTPUT: Q18 */
            }
        }

        /* sum = for(i=0;i<len;i++)inVec1[i]*inVec2[i];      ---        inner product   */
        public static int silk_inner_prod_aligned(
            Pointer<short> inVec1,             /*    I input vector 1                                              */
            Pointer<short> inVec2,             /*    I input vector 2                                              */
            int len                /*    I vector lengths                                              */
        )
        {
            return Kernels.celt_inner_prod(inVec1.Data, inVec1.Offset, inVec2.Data, inVec2.Offset, len);
        }

        public static long silk_inner_prod16_aligned_64(
            Pointer<short> inVec1,            /*    I input vector 1                                              */
            Pointer<short> inVec2,            /*    I input vector 2                                              */
            int len                 /*    I vector lengths                                              */
        )
        {
            int i;
            long sum = 0;
            for (i = 0; i < len; i++)
            {
                sum = silk_SMLALBB(sum, inVec1[i], inVec2[i]);
            }
            return sum;
        }


#endregion

#region EntropyCoder helper functions, common to both projects
        
        /// <summary>
        /// returns the value that has fewer higher-order bits, ignoring sign bit (? I think?)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static uint EC_MINI(uint a, uint b)
        {
            return unchecked(a + ((b - a) & ((b < a) ? 0xFFFFFFFFU : 0)));
        }

        /// <summary>
        /// Counts leading zeroes
        /// </summary>
        /// <param name="_x"></param>
        /// <returns></returns>
        public static int EC_CLZ(uint _x)
        {
            if (_x == 0)
                return 0;
            return clz_fast(_x) - 31;
        }

        public static int clz_fast(uint x)
        {
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            uint y = x - ((x >> 1) & 0x55555555);
            y = (((y >> 2) & 0x33333333) + (y & 0x33333333));
            y = (((y >> 4) + y) & 0x0f0f0f0f);
            y += (y >> 8);
            y += (y >> 16);
            y = (y & 0x0000003f);
            return (int)(32 - y);
        }

        /// <summary>
        /// returns inverse base-2 log of a value
        /// </summary>
        /// <param name="_x"></param>
        /// <returns></returns>
        public static int EC_ILOG(uint _x)
        {
#if PARITY
            return 1 - EC_CLZ(_x);
#else
            // On a Pentium M, this branchless version tested as the fastest on
            // 1,000,000,000 random 32-bit integers, edging out a similar version with
            // branches, and a 256-entry LUT version.
            int ret;
            int m;
            ret = _x == 0 ? 0 : 1;
            m = ((_x & 0xFFFF0000U) == 0 ? 0 : 1) << 4;
            _x >>= m;
            ret |= m;
            m = ((_x & 0xFF00U) == 0 ? 0 : 1) << 3;
            _x >>= m;
            ret |= m;
            m = ((_x & 0xF0U) == 0 ? 0 : 1) << 2;
            _x >>= m;
            ret |= m;
            m = ((_x & 0xCU) == 0 ? 0 : 1) << 1;
            _x >>= m;
            ret |= m;
            ret += (_x & 0x2U) == 0 ? 0 : 1;
            return ret;
#endif
        }

#endregion

#region C++ Math
        
        public static int abs(int a)
        {
            if (a < 0)
                return 0 - a;
            return a;
        }

#endregion
    }
}
