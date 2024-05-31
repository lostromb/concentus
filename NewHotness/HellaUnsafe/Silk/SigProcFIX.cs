using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace HellaUnsafe.Silk
{
    internal static class SigProcFIX
    {
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
            int m = (0 - rot);
            if (rot == 0)
            {
                return a32;
            }
            else if (rot < 0)
            {
                return ((a32 << m) | (a32 >> (32 - m)));
            }
            else
            {
                return ((a32 << (32 - rot)) | (a32 >> rot));
            }
        }

        internal static int silk_MUL(int a32, int b32)
        {
            int ret = a32 * b32;
#if DEBUG_MACROS
            long ret64 = (long)a32 * (long)b32;
            Inlines.OpusAssert((long)ret == ret64);
#endif
            return ret;
        }

        internal static float silk_MUL(float a32, float b32) { return a32 * b32; }
        
        internal static uint silk_MUL_uint(uint a32, uint b32)
        {
            uint ret = a32 * b32;
            return ret;
        }

        internal static int silk_MLA(int a32, int b32, int c32)
        {
            int ret = silk_ADD32((a32), ((b32) * (c32)));
            return ret;
        }


        internal static int silk_MLA_uint(uint a32, uint b32, uint c32)
        {
            uint ret = silk_ADD32((a32), ((b32) * (c32)));
            return (int)ret;
        }

        internal static short silk_ADD16(short a, short b)
        {
            return (short)(a + b);
        }

        internal static int silk_ADD32(int a, int b)
        {
            return a + b;
        }

        internal static uint silk_ADD32(uint a, uint b)
        {
            return a + b;
        }

        internal static long silk_ADD64(long a, long b)
        {
            return a + b;
        }

        internal static short silk_SUB16(short a, short b)
        {
            return (short)(a - b);
        }

        internal static int silk_SUB32(int a, int b)
        {
            return a - b;
        }

        internal static long silk_SUB64(long a, long b)
        {
            return a - b;
        }

        internal static byte silk_SAT8(int a)
        {
            return a > byte.MaxValue ? byte.MaxValue : ((a) < byte.MinValue ? byte.MinValue : (byte)(a));
        }

        internal static short silk_SAT16(int a)
        {
            return a > short.MaxValue ? short.MaxValue : ((a) < short.MinValue ? short.MinValue : (short)(a));
        }

        internal static int silk_SAT32(long a)
        {
            return a > int.MaxValue ? int.MaxValue : ((a) < int.MinValue ? int.MinValue : (int)(a));
        }
    }
}
