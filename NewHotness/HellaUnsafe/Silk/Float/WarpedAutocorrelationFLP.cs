using System;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class WarpedAutocorrelationFLP
    {
        /* Autocorrelations for a warped frequency axis */
        internal static unsafe void silk_warped_autocorrelation_FLP(
            float                      *corr,                              /* O    Result [order + 1]                          */
            in float                *input,                             /* I    Input data to correlate                     */
            in float                warping,                            /* I    Warping coefficient                         */
            in int                  length,                             /* I    Length of input                             */
            in int                  order                               /* I    Correlation order (even)                    */
        )
        {
            int    n, i;
            double      tmp1, tmp2;
            double* state = stackalloc double[ MAX_SHAPE_LPC_ORDER + 1 ];
            double* C = stackalloc double[     MAX_SHAPE_LPC_ORDER + 1 ];
            new Span<double>(state, MAX_SHAPE_LPC_ORDER + 1).Fill(0);
            new Span<double>(C, MAX_SHAPE_LPC_ORDER + 1).Fill(0);

            /* Order must be even */
            celt_assert( ( order & 1 ) == 0 );

            /* Loop over samples */
            for( n = 0; n < length; n++ ) {
                tmp1 = input[ n ];
                /* Loop over allpass sections */
                for( i = 0; i < order; i += 2 ) {
                    /* Output of allpass section */
                    /* We voluntarily use two multiples instead of factoring the expression to
                       reduce the length of the dependency chain (tmp1->tmp2->tmp1... ). */
                    tmp2 = state[ i ] + warping * state[ i + 1 ] - warping * tmp1;
                    state[ i ] = tmp1;
                    C[ i ] += state[ 0 ] * tmp1;
                    /* Output of allpass section */
                    tmp1 = state[ i + 1 ] + warping * state[ i + 2 ] - warping * tmp2;
                    state[ i + 1 ] = tmp2;
                    C[ i + 1 ] += state[ 0 ] * tmp2;
                }
                state[ order ] = tmp1;
                C[ order ] += state[ 0 ] * tmp1;
            }

            /* Copy correlations in float output format */
            for( i = 0; i < order + 1; i++ ) {
                corr[ i ] = ( float )C[ i ];
            }
        }
    }
}
