using Concentus.Common.CPlusPlus;
using Concentus.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    public static class SumSqrShift
    {
        /// <summary>
        /// Compute number of bits to right shift the sum of squares of a vector
        /// of int16s to make it fit in an int32
        /// </summary>
        /// <param name="energy">O   Energy of x, after shifting to the right</param>
        /// <param name="shift">O   Number of bits right shift applied to energy</param>
        /// <param name="x">I   Input vector</param>
        /// <param name="len">I   Length of input vector</param>
        public static void silk_sum_sqr_shift(
            BoxedValue<int> energy,
            BoxedValue<int> shift,
            Pointer<short> x,
            int len)
        {
            int i, shft;
            int nrg_tmp, nrg;

            nrg = 0;
            shft = 0;
            len--;

            for (i = 0; i < len; i += 2)
            {
                nrg = Inlines.silk_SMLABB_ovflw(nrg, x[i], x[i]);
                nrg = Inlines.silk_SMLABB_ovflw(nrg, x[i + 1], x[i + 1]);
                if (nrg < 0)
                {
                    /* Scale down */
                    nrg = unchecked((int)Inlines.silk_RSHIFT_uint((uint)nrg, 2));
                    shft = 2;
                    i += 2;
                    break;
                }
            }

            for (; i < len; i += 2)
            {
                nrg_tmp = Inlines.silk_SMULBB(x[i], x[i]);
                nrg_tmp = Inlines.silk_SMLABB_ovflw(nrg_tmp, x[i + 1], x[i + 1]);
                nrg = (int)Inlines.silk_ADD_RSHIFT_uint((uint)nrg, (uint)nrg_tmp, shft);
                if (nrg < 0)
                {
                    /* Scale down */
                    nrg = (int)Inlines.silk_RSHIFT_uint((uint)nrg, 2);
                    shft += 2;
                }
            }

            if (i == len)
            {
                /* One sample left to process */
                nrg_tmp = Inlines.silk_SMULBB(x[i], x[i]);
                nrg = (int)Inlines.silk_ADD_RSHIFT_uint((uint)nrg, (uint)nrg_tmp, shft);
            }

            /* Make sure to have at least one extra leading zero (two leading zeros in total) */
            if ((nrg & 0xC0000000) != 0)
            {
                nrg = (int)Inlines.silk_RSHIFT_uint((uint)nrg, 2);
                shft += 2;
            }

            /* Output arguments */
            shift.Val = shft;
            energy.Val = nrg;
        }
    }
}
