using static HellaUnsafe.Silk.Inlines;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk
{
    internal static unsafe class Lin2Log
    {
        /* Approximation of 128 * log2() (very close inverse of silk_log2lin()) */
        /* Convert input to a log scale    */
        internal static unsafe int silk_lin2log(
            in int            inLin               /* I  input in linear scale                                         */
        )
        {
            int lz, frac_Q7;

            silk_CLZ_FRAC( inLin, &lz, &frac_Q7 );

            /* Piece-wise parabolic approximation */
            return silk_ADD_LSHIFT32( silk_SMLAWB( frac_Q7, silk_MUL( frac_Q7, 128 - frac_Q7 ), 179 ), 31 - lz, 7 );
        }
    }
}
