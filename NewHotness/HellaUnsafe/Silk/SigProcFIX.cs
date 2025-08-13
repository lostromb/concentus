using System.Diagnostics;
using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Silk
{
    internal static class SigProcFIX
    {
        internal const int SILK_MAX_ORDER_LPC = 24;

        internal static void silk_assert(bool condition)
        {
            Debug.Assert(condition);
        }

        /// <summary>
        /// Rotate a32 right by 'rot' bits. Negative rot values result in rotating
        /// left. Output is 32bit int.
        /// </summary>
        /// <param name="a32"></param>
        /// <param name="rot"></param>
        /// <returns></returns>
        internal static int silk_ROR32(int a32, int rot)
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
        internal static uint silk_ROR32(uint a32, int rot)
        {
            int m = 0 - rot;
            if (rot == 0)
            {
                return a32;
            }
            else if (rot < 0)
            {
                return (a32 << m) | (a32 >> (32 - m));
            }
            else
            {
                return (a32 << (32 - rot)) | (a32 >> rot);
            }
        }

        /* (a32 * b32) output have to be 32bit int */
        internal static int silk_MUL(int a32, int b32)
        {
            return ((a32) * (b32));
        }

        /* (a32 * b32) output have to be 32bit uint */
        internal static uint silk_MUL_uint(uint a32, uint b32)
        {
            return ((a32) * (b32));
        }

        /// <summary>
        /// ((a32 >> 16)  * (b32 >> 16))
        /// </summary>
        /// <param name="a32"></param>
        /// <param name="b32"></param>
        /// <returns></returns>
        internal static int silk_SMULTT(int a32, int b32)
        {
            return ((a32 >> 16) * (b32 >> 16));
        }


        internal static int silk_SMLATT(int a32, int b32, int c32)
        {
            return silk_ADD32((a32), ((b32) >> 16) * ((c32) >> 16));
        }


        internal static long silk_SMLALBB(long a64, short b16, short c16)
        {
            return silk_ADD64((a64), (long)((int)(b16) * (int)(c16)));
        }


        internal static long silk_SMULL(int a32, int b32)
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
        internal static int silk_ADD32_ovflw(int a, int b)
        {
            return unchecked((int)((uint)a + (uint)b));
        }

        internal static int silk_ADD32_ovflw(uint a, uint b)
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
        internal static int silk_SUB32_ovflw(int a, int b)
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
        internal static int silk_MLA_ovflw(int a32, int b32, int c32)
        {
            return unchecked(silk_ADD32_ovflw((uint)(a32), (uint)(b32) * (uint)(c32)));
        }


        internal static int silk_SMLABB_ovflw(int a32, int b32, int c32)
        {
            return unchecked((silk_ADD32_ovflw((a32), ((int)((short)(b32))) * (int)((short)(c32)))));
        }


        internal static int silk_SMULBB(int a32, int b32)
        {
            return ((int)unchecked((short)a32) * (int)unchecked((short)b32));
        }

        /// <summary>
        /// (a32 * (int)((short)(b32))) >> 16 output have to be 32bit int
        /// </summary>
        /// <param name="a32"></param>
        /// <param name="b32"></param>
        /// <returns></returns>
        internal static int silk_SMULWB(int a32, int b32)
        {
#if DEBUG_MACROS
            int ret;
            ret = ((a32 >> 16) * (int)((short)b32) + (((a32 & 0x0000FFFF) * (int)((short)b32)) >> 16));
            if ((long)ret != ((long)a32 * (short)b32) >> 16)
            {
                silk_assert(false);
            }
            return ret;
#else
            return unchecked((int)(unchecked(unchecked(a32 * (long)(unchecked((short)b32))) >> 16)));
#endif
        }

        internal static int silk_SMLABB(int a32, int b32, int c32)
        {
            return ((a32) + ((int)unchecked((short)b32)) * (int)unchecked((short)c32));
        }

        internal static int silk_DIV32_16(int a32, int b32)
        {
#if DEBUG_MACROS
            bool fail = false;
            fail |= b32 == 0;
            fail |= b32 > short.MaxValue;
            fail |= b32 < short.MinValue;
            silk_assert(!fail);
#endif
            return a32 / b32;
        }

        internal static int silk_DIV32(int a32, int b32)
        {
            return a32 / b32;
        }

        internal static short silk_ADD16(short a, short b)
        {
            short ret = (short)(a + b);
#if DEBUG_MACROS
            if (ret != silk_ADD_SAT16(a, b))
            {
                silk_assert(false);
            }
#endif
            return ret;
        }

        internal static int silk_ADD32(int a, int b)
        {
            int ret = a + b;
#if DEBUG_MACROS
            if (ret != silk_ADD_SAT32(a, b))
            {
                silk_assert(false);
            }
#endif
            return ret;
        }

        internal static uint silk_ADD32(uint a, uint b)
        {
            uint ret = a + b;
            return ret;
        }

        internal static long silk_ADD64(long a, long b)
        {
            long ret = a + b;
            ASSERT(ret == silk_ADD_SAT64(a, b));
            return ret;
        }


        internal static short silk_SUB16(short a, short b)
        {
            short ret = (short)(a - b);
            ASSERT(ret == silk_SUB_SAT16(a, b));
            return ret;
        }

        internal static int silk_SUB32(int a, int b)
        {
            int ret = a - b;
            ASSERT(ret == silk_SUB_SAT32(a, b));
            return ret;
        }

        internal static long silk_SUB64(long a, long b)
        {
            long ret = a - b;
            ASSERT(ret == silk_SUB_SAT64(a, b));
            return ret;
        }

        internal static int silk_SAT8(int a)
        {
            return a > byte.MaxValue ? byte.MaxValue : ((a) < byte.MinValue ? byte.MinValue : (a));
        }

        internal static short silk_SAT16(int a)
        {
            return a > short.MaxValue ? short.MaxValue : ((short)(a) < short.MinValue ? short.MinValue : (short)(a));
        }

        internal static int silk_SAT32(long a)
        {
            return a > int.MaxValue ? int.MaxValue : ((a) < int.MinValue ? int.MinValue : (int)(a));
        }

        internal static short silk_ADD_SAT16(short a16, short b16)
        {
            short res = (short)silk_SAT16(silk_ADD32((int)(a16), (b16)));
            ASSERT(res == silk_SAT16((int)a16 + (int)b16));
            return res;
        }

        internal static int silk_ADD_SAT32(int a32, int b32)
        {
            int res = (unchecked(((uint)(a32) + (uint)(b32)) & 0x80000000) == 0 ?
                ((((a32) & (b32)) & 0x80000000) != 0 ? int.MinValue : (a32) + (b32)) :
                ((((a32) | (b32)) & 0x80000000) == 0 ? int.MaxValue : (a32) + (b32)));
            ASSERT(res == silk_SAT32((long)a32 + (long)b32));
            return res;
        }

        internal static long silk_ADD_SAT64(long a64, long b64)
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
            ASSERT(!fail);
#endif
            return res;
        }

        internal static short silk_SUB_SAT16(short a16, short b16)
        {
            short res = (short)silk_SAT16(silk_SUB32((int)(a16), (b16)));
            ASSERT(res == silk_SAT16((int)a16 - (int)b16));
            return res;
        }

        internal static int silk_SUB_SAT32(int a32, int b32)
        {
            int res = (unchecked(((uint)(a32) - (uint)(b32)) & 0x80000000) == 0 ?
                (((a32) & ((b32) ^ 0x80000000) & 0x80000000) != 0 ? int.MinValue : (a32) - (b32)) :
                ((((a32) ^ 0x80000000) & (b32) & 0x80000000) != 0 ? int.MaxValue : (a32) - (b32)));
            ASSERT(res == silk_SAT32((long)a32 - (long)b32));
            return res;
        }

        internal static long silk_SUB_SAT64(long a64, long b64)
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
            silk_assert(!fail);
#endif
            return res;
        }

        //* Saturation for positive input values */
        //#define silk_POS_SAT32(a)                   ((a) > int_MAX ? int_MAX : (a))

        /// <summary>
        /// Add with saturation for positive input values
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        internal static sbyte silk_ADD_POS_SAT8(sbyte a, sbyte b)
        {
            return (sbyte)((((a + b) & 0x80) != 0) ? sbyte.MaxValue : (a + b));
        }

        /// <summary>
        /// Add with saturation for positive input values
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        internal static short silk_ADD_POS_SAT16(short a, short b)
        {
            return (short)(unchecked(((a + b) & 0x8000) != 0) ? short.MaxValue : (a + b));
        }

        /// <summary>
        /// Add with saturation for positive input values
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        internal static int silk_ADD_POS_SAT32(int a, int b)
        {
            return (unchecked(((a + b) & 0x80000000) != 0) ? int.MaxValue : (a + b));
        }

        /// <summary>
        /// Add with saturation for positive input values
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        internal static long silk_ADD_POS_SAT64(long a, long b)
        {
            return ((unchecked((ulong)(a + b) & 0x8000000000000000L) != 0) ? long.MaxValue : (a + b));
        }

        internal static sbyte silk_LSHIFT8(sbyte a, int shift)
        {
            sbyte ret = (sbyte)(a << shift);
#if DEBUG_MACROS
            bool fail = false;
            fail |= shift < 0;
            fail |= shift >= 8;
            fail |= (long)ret != ((long)a) << shift;
            silk_assert(!fail);
#endif
            return ret;
        }

        internal static short silk_LSHIFT16(short a, int shift)
        {
            short ret = (short)(a << shift);
#if DEBUG_MACROS
            bool fail = false;
            fail |= shift < 0;
            fail |= shift >= 16;
            fail |= (long)ret != ((long)a) << shift;
            silk_assert(!fail);
#endif
            return ret;
        }

        internal static int silk_LSHIFT32(int a, int shift)
        {
            int ret = a << shift;
#if DEBUG_MACROS
            bool fail = false;
            fail |= shift < 0;
            fail |= shift >= 32;
            fail |= (long)ret != ((long)a) << shift;
            silk_assert(!fail);
#endif
            return ret;
        }

        internal static long silk_LSHIFT64(long a, int shift)
        {
            long ret = a << shift;
#if DEBUG_MACROS
            bool fail = false;
            fail |= shift < 0;
            fail |= shift >= 64;
            fail |= (ret >> shift) != ((long)a);
            silk_assert(!fail);
#endif
            return ret;
        }

        internal static int silk_LSHIFT(int a, int shift)
        {
            int ret = a << shift;
#if DEBUG_MACROS
            bool fail = false;
            fail |= shift < 0;
            fail |= shift >= 32;
            fail |= (long)ret != ((long)a) << shift;
            silk_assert(!fail);
#endif
            return ret;
        }

        internal static int silk_LSHIFT_ovflw(int a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 32)) /* no check for overflow */
            {
                silk_assert(false);
            }
#endif
            return a << shift;
        }

        internal static uint silk_LSHIFT_uint(uint a, int shift)
        {
            uint ret = a << shift;
#if DEBUG_MACROS
            if ((shift < 0) || ((long)ret != ((long)a) << shift))
            {
                silk_assert(false);
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
        internal static int silk_LSHIFT_SAT32(int a, int shift)
        {
            return (silk_LSHIFT32(silk_LIMIT((a), silk_RSHIFT32(int.MinValue, (shift)), silk_RSHIFT32(int.MaxValue, (shift))), (shift)));
        }

        internal static sbyte silk_RSHIFT8(sbyte a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 8))
            {
                silk_assert(false);
            }
#endif
            return (sbyte)(a >> shift);
        }

        internal static short silk_RSHIFT16(short a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 16))
            {
                silk_assert(false);
            }
#endif
            return (short)(a >> shift);
        }

        internal static int silk_RSHIFT32(int a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 32))
            {
                silk_assert(false);
            }
#endif
            return a >> shift;
        }

        internal static int silk_RSHIFT(int a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 32))
            {
                silk_assert(false);
            }
#endif
            return a >> shift;
        }

        internal static long silk_RSHIFT64(long a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift >= 64))
            {
                silk_assert(false);
            }
#endif
            return a >> shift;
        }

        internal static uint silk_RSHIFT_uint(uint a, int shift)
        {
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 32))
            {
                silk_assert(false);
            }
#endif
            return a >> shift;
        }

        internal static int silk_ADD_LSHIFT(int a, int b, int shift)
        {
            int ret = a + (b << shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a + (((long)b) << shift)))
            {
                silk_assert(false);
            }
#endif
            return ret;                /* shift >= 0 */
        }

        internal static int silk_ADD_LSHIFT32(int a, int b, int shift)
        {
            int ret = a + (b << shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a + (((long)b) << shift)))
            {
                silk_assert(false);
            }
#endif
            return ret;                /* shift >= 0 */
        }

        internal static uint silk_ADD_LSHIFT_uint(uint a, uint b, int shift)
        {
            uint ret;
            ret = a + (b << shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 32) || ((long)ret != (long)a + (((long)b) << shift)))
            {
                silk_assert(false);
            }
#endif
            return ret;                /* shift >= 0 */
        }

        internal static int silk_ADD_RSHIFT(int a, int b, int shift)
        {
            int ret = a + (b >> shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a + (((long)b) >> shift)))
            {
                silk_assert(false);
            }
#endif
            return ret;                /* shift  > 0 */
        }

        internal static int silk_ADD_RSHIFT32(int a, int b, int shift)
        {
            int ret = a + (b >> shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a + (((long)b) >> shift)))
            {
                silk_assert(false);
            }
#endif
            return ret;                /* shift  > 0 */
        }

        internal static uint silk_ADD_RSHIFT_uint(uint a, uint b, int shift)
        {
            uint ret;
            ret = a + (b >> shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 32) || ((long)ret != (long)a + (((long)b) >> shift)))
            {
                silk_assert(false);
            }
#endif
            return ret;                /* shift  > 0 */
        }

        internal static int silk_SUB_LSHIFT32(int a, int b, int shift)
        {
            int ret;
            ret = a - (b << shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a - (((long)b) << shift)))
            {
                silk_assert(false);
            }
#endif
            return ret;                /* shift >= 0 */
        }

        internal static int silk_SUB_RSHIFT32(int a, int b, int shift)
        {
            int ret;
            ret = a - (b >> shift);
#if DEBUG_MACROS
            if ((shift < 0) || (shift > 31) || ((long)ret != (long)a - (((long)b) >> shift)))
            {
                silk_assert(false);
            }
#endif
            return ret;                /* shift  > 0 */
        }

        internal static int silk_RSHIFT_ROUND(int a, int shift)
        {
            int ret;
            ret = shift == 1 ? (a >> 1) + (a & 1) : ((a >> (shift - 1)) + 1) >> 1;
#if DEBUG_MACROS
            /* the marco definition can't handle a shift of zero */
            if ((shift <= 0) || (shift > 31) || ((long)ret != ((long)a + ((long)1 << (shift - 1))) >> shift))
            {
                silk_assert(false);
            }
#endif
            return ret;
        }

        internal static long silk_RSHIFT_ROUND64(long a, int shift)
        {
            long ret;
#if DEBUG_MACROS
            /* the macro definition can't handle a shift of zero */
            if ((shift <= 0) || (shift >= 64))
            {
                silk_assert(false);
            }
#endif
            ret = shift == 1 ? (a >> 1) + (a & 1) : ((a >> (shift - 1)) + 1) >> 1;
            return ret;
        }

        internal static int silk_min(int a, int b)
        {
            return ((a) < (b)) ? (a) : (b);
        }

        internal static int silk_max(int a, int b)
        {
            return ((a) > (b)) ? (a) : (b);
        }

        /* silk_min() versions with typecast in the function call */
        internal static int silk_min_int(int a, int b)
        {
            return (((a) < (b)) ? (a) : (b));
        }

        internal static short silk_min_16(short a, short b)
        {
            return (((a) < (b)) ? (a) : (b));
        }

        internal static int silk_min_32(int a, int b)
        {
            return (((a) < (b)) ? (a) : (b));
        }

        internal static long silk_min_64(long a, long b)
        {
            return (((a) < (b)) ? (a) : (b));
        }

        /* silk_min() versions with typecast in the function call */
        internal static int silk_max_int(int a, int b)
        {
            return (((a) > (b)) ? (a) : (b));
        }

        internal static short silk_max_16(short a, short b)
        {
            return (((a) > (b)) ? (a) : (b));
        }

        internal static int silk_max_32(int a, int b)
        {
            return (((a) > (b)) ? (a) : (b));
        }

        internal static long silk_max_64(long a, long b)
        {
            return (((a) > (b)) ? (a) : (b));
        }

        internal static float silk_LIMIT(float a, float limit1, float limit2)
        {
            return ((limit1) > (limit2) ? ((a) > (limit1) ? (limit1) : ((a) < (limit2) ? (limit2) : (a))) : ((a) > (limit2) ? (limit2) : ((a) < (limit1) ? (limit1) : (a))));
        }

        internal static int silk_LIMIT(int a, int limit1, int limit2)
        {
            return silk_LIMIT_32(a, limit1, limit2);
        }

        internal static int silk_LIMIT_int(int a, int limit1, int limit2)
        {
            return silk_LIMIT_32(a, limit1, limit2);
        }

        internal static short silk_LIMIT_16(short a, short limit1, short limit2)
        {
            return ((limit1) > (limit2) ? ((a) > (limit1) ? (limit1) : ((a) < (limit2) ? (limit2) : (a))) : ((a) > (limit2) ? (limit2) : ((a) < (limit1) ? (limit1) : (a))));
        }

        internal static int silk_LIMIT_32(int a, int limit1, int limit2)
        {
            return ((limit1) > (limit2) ? ((a) > (limit1) ? (limit1) : ((a) < (limit2) ? (limit2) : (a))) : ((a) > (limit2) ? (limit2) : ((a) < (limit1) ? (limit1) : (a))));
        }

        internal static int silk_abs(int a)
        {
            // Be careful, silk_abs returns wrong when input equals to silk_intXX_MIN
            return ((a) > 0) ? (a) : -(a);
        }

        internal static int silk_abs_int16(int a)
        {
            return (a ^ (a >> 15)) - (a >> 15);
        }

        internal static int silk_abs_int32(int a)
        {
            return (a ^ (a >> 31)) - (a >> 31);
        }

        internal static long silk_abs_int64(long a)
        {
            return ((a) > 0) ? (a) : -(a);
        }

        internal static long silk_sign(int a)
        {
            return (a) > 0 ? 1 : ((a) < 0 ? -1 : 0);
        }

        /* PSEUDO-RANDOM GENERATOR                                                          */
        /* Make sure to store the result as the seed for the next call (also in between     */
        /* frames), otherwise result won't be random at all. When only using some of the    */
        /* bits, take the most significant bits by right-shifting.                          */
        internal const int RAND_MULTIPLIER = 196314165;
        internal const int RAND_INCREMENT = 907633515;

        internal static int silk_RAND(int seed)
        {
            return (silk_MLA_ovflw((RAND_INCREMENT), (seed), (RAND_MULTIPLIER)));
        }

        /*    silk_SMMUL: Signed top word multiply.
          ARMv6        2 instruction cycles.
          ARMv3M+      3 instruction cycles. use SMULL and ignore LSB registers.(except xM)*/
        /*#define silk_SMMUL(a32, b32)                (opus_int32)silk_RSHIFT(silk_SMLAL(silk_SMULWB((a32), (b32)), (a32), silk_RSHIFT_ROUND((b32), 16)), 16)*/
        /* the following seems faster on x86 */
        internal static int silk_SMMUL(int a32, int b32)
        {
            return (int)silk_RSHIFT64(silk_SMULL(a32, b32), 32);
        }

        /* Macro to convert floating-point constants to fixed-point */
        //#define SILK_FIX_CONST( C, Q )        ((opus_int32)((C) * ((opus_int64)1 << (Q)) + 0.5))       
        internal static int SILK_FIX_CONST(double C, int Q)
        {
            return (int)(C * ((long)1 << Q) + 0.5);
        }
    }
}
