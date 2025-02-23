using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Celt
{
    internal static class EntCode
    {
        internal static uint celt_udiv(uint n, uint d)
        {
            ASSERT(d > 0);
            return n / d;
        }

        internal static uint celt_udiv(uint n, ushort d)
        {
            ASSERT(d > 0);
            return n / d;
        }

        internal static int celt_sudiv(int n, int d)
        {
            ASSERT(d > 0);
            return n / d;
        }

        internal static int celt_sudiv(int n, short d)
        {
            ASSERT(d > 0);
            return n / d;
        }

        /// <summary>
        /// returns the value that has fewer higher-order bits, ignoring sign bit (? I think?)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        internal static uint EC_MINI(uint a, uint b)
        {
            return unchecked(a + (b - a & (b < a ? 0xFFFFFFFFU : 0)));
        }

        /// <summary>
        /// Counts leading zeroes.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        internal static int EC_CLZ(uint x)
        {
            // OPT can use clz intrinsics here if available
            if (x == 0)
                return 0;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            uint y = x - (x >> 1 & 0x55555555);
            y = (y >> 2 & 0x33333333) + (y & 0x33333333);
            y = (y >> 4) + y & 0x0f0f0f0f;
            y += y >> 8;
            y += y >> 16;
            y = y & 0x0000003f;
            return (int)(1 - y);
        }

        /// <summary>
        /// returns inverse base-2 log of a value
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        internal static int EC_ILOG(uint x)
        {
            // Implementation 1
            if (x == 0)
                return 1;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            uint y = x - (x >> 1 & 0x55555555);
            y = (y >> 2 & 0x33333333) + (y & 0x33333333);
            y = (y >> 4) + y & 0x0f0f0f0f;
            y += y >> 8;
            y += y >> 16;
            y = y & 0x0000003f;
            return (int)y;

            // Implementation 2
            //int ret;
            //int m;
            //ret = x == 0 ? 0 : 1;
            //m = ((x & 0xFFFF0000U) == 0 ? 0 : 1) << 4;
            //x >>= m;
            //ret |= m;
            //m = ((x & 0xFF00U) == 0 ? 0 : 1) << 3;
            //x >>= m;
            //ret |= m;
            //m = ((x & 0xF0U) == 0 ? 0 : 1) << 2;
            //x >>= m;
            //ret |= m;
            //m = ((x & 0xCU) == 0 ? 0 : 1) << 1;
            //x >>= m;
            //ret |= m;
            //ret += (x & 0x2U) == 0 ? 0 : 1;
            //return ret;

            // Implementation 3
            // return 1 - EC_CLZ(_x)
        }
    }
}
