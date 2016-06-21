using Concentus.Common;
using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    internal static class BWExpander
    {
        /// <summary>
        /// Chirp (bw expand) LP AR filter (Fixed point implementation)
        /// </summary>
        /// <param name="ar">I/O  AR filter to be expanded (without leading 1)</param>
        /// <param name="d">I length of ar</param>
        /// <param name="chirp">I    chirp factor (typically in range (0..1) )</param>
        internal static void silk_bwexpander_32(
            Pointer<int> ar,                /* I/O  AR filter to be expanded (without leading 1)                */
    int d,                  /* I    Length of ar                                                */
    int chirp_Q16           /* I    Chirp factor in Q16                                         */
)
        {
            int i;
            int chirp_minus_one_Q16 = chirp_Q16 - 65536;

            for (i = 0; i < d - 1; i++)
            {
                ar[i] = Inlines.silk_SMULWW(chirp_Q16, ar[i]);
                chirp_Q16 += Inlines.silk_RSHIFT_ROUND(Inlines.silk_MUL(chirp_Q16, chirp_minus_one_Q16), 16);
            }
            ar[d - 1] = Inlines.silk_SMULWW(chirp_Q16, ar[d - 1]);
        }

        /// <summary>
        /// Chirp (bw expand) LP AR filter (Fixed point implementation)
        /// </summary>
        /// <param name="ar">I/O  AR filter to be expanded (without leading 1)</param>
        /// <param name="d">I length of ar</param>
        /// <param name="chirp">I    chirp factor (typically in range (0..1) )</param>
        internal static void silk_bwexpander(
                    Pointer<short> ar,
                    int d,
                    int chirp_Q16)
        {
            int i;
            int chirp_minus_one_Q16 = chirp_Q16 - 65536;

            /* NB: Dont use silk_SMULWB, instead of silk_RSHIFT_ROUND( silk_MUL(), 16 ), below.  */
            /* Bias in silk_SMULWB can lead to unstable filters                                */
            for (i = 0; i < d - 1; i++)
            {
                ar[i] = (short)Inlines.silk_RSHIFT_ROUND(Inlines.silk_MUL(chirp_Q16, ar[i]), 16);
                chirp_Q16 += Inlines.silk_RSHIFT_ROUND(Inlines.silk_MUL(chirp_Q16, chirp_minus_one_Q16), 16);
            }
            ar[d - 1] = (short)Inlines.silk_RSHIFT_ROUND(Inlines.silk_MUL(chirp_Q16, ar[d - 1]), 16);
        }
    }
}
