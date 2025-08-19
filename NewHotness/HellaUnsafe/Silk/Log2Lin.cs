using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk
{
    internal static unsafe class Log2Lin
    {
        /* Approximation of 2^() (very close inverse of silk_lin2log()) */
        /* Convert input to a linear scale    */
        internal static unsafe int silk_log2lin(
            in int            inLog_Q7            /* I  input on log scale                                            */
        )
        {
            int output, frac_Q7;

            if( inLog_Q7 < 0 ) {
                return 0;
            } else if ( inLog_Q7 >= 3967 ) {
                return silk_int32_MAX;
            }

            output = silk_LSHIFT( 1, silk_RSHIFT( inLog_Q7, 7 ) );
            frac_Q7 = inLog_Q7 & 0x7F;
            if( inLog_Q7 < 2048 ) {
                /* Piece-wise parabolic approximation */
                output = silk_ADD_RSHIFT32(output, silk_MUL(output, silk_SMLAWB( frac_Q7, silk_SMULBB( frac_Q7, 128 - frac_Q7 ), -174 ) ), 7 );
            } else {
                /* Piece-wise parabolic approximation */
                output = silk_MLA(output, silk_RSHIFT(output, 7 ), silk_SMLAWB( frac_Q7, silk_SMULBB( frac_Q7, 128 - frac_Q7 ), -174 ) );
            }

            return output;
        }
    }
}
