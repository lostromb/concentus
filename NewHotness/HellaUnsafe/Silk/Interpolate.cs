using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.NLSF2A;
using static HellaUnsafe.Silk.NLSFEncode;
using static HellaUnsafe.Silk.NLSFVQWeightsLaroia;
using static HellaUnsafe.Silk.Interpolate;
using static HellaUnsafe.Silk.Macros;
using HellaUnsafe.Common;

namespace HellaUnsafe.Silk
{
    internal static unsafe class Interpolate
    {
        /* Interpolate two vectors */
        internal static unsafe void silk_interpolate(
            short*                  xi/*[ MAX_LPC_ORDER ]*/,            /* O    interpolated vector                         */
            in short*            x0/*[ MAX_LPC_ORDER ]*/,            /* I    first vector                                */
            in short*            x1/*[ MAX_LPC_ORDER ]*/,            /* I    second vector                               */
            in int              ifact_Q2,                       /* I    interp. factor, weight on 2nd vector        */
            in int              d                               /* I    number of parameters                        */
        )
        {
            int i;

            celt_assert( ifact_Q2 >= 0 );
            celt_assert( ifact_Q2 <= 4 );

            for( i = 0; i < d; i++ ) {
                xi[ i ] = (short)silk_ADD_RSHIFT( x0[ i ], silk_SMULBB( x1[ i ] - x0[ i ], ifact_Q2 ), 2 );
            }
        }
    }
}
