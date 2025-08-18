using HellaUnsafe.Common;
using static HellaUnsafe.Silk.Float.SigProcFLP;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class SchurFLP
    {
        internal static unsafe float silk_schur_FLP(                  /* O    returns residual energy                                     */
            float*          refl_coef,        /* O    reflection coefficients (length order)                      */
            in float*    auto_corr,        /* I    autocorrelation sequence (length order+1)                   */
            int            order               /* I    order                                                       */
        )
        {
            int   k, n;
            double* C_data = stackalloc double[ (SILK_MAX_ORDER_LPC + 1) *  2 ];
            Native2DArray<double> C = new Native2DArray<double>(SILK_MAX_ORDER_LPC + 1, 2, C_data);
            double Ctmp1, Ctmp2, rc_tmp;

            celt_assert( order >= 0 && order <= SILK_MAX_ORDER_LPC );

            /* Copy correlations */
            k = 0;
            do {
                C[ k ][ 0 ] = C[ k ][ 1 ] = auto_corr[ k ];
            } while( ++k <= order );

            for( k = 0; k < order; k++ ) {
                /* Get reflection coefficient */
                rc_tmp = -C[ k + 1 ][ 0 ] / silk_max_float( C[ 0 ][ 1 ], 1e-9 );

                /* Save the output */
                refl_coef[ k ] = (float)rc_tmp;

                /* Update correlations */
                for( n = 0; n < order - k; n++ ) {
                    Ctmp1 = C[ n + k + 1 ][ 0 ];
                    Ctmp2 = C[ n ][ 1 ];
                    C[ n + k + 1 ][ 0 ] = Ctmp1 + Ctmp2 * rc_tmp;
                    C[ n ][ 1 ]         = Ctmp2 + Ctmp1 * rc_tmp;
                }
            }

            /* Return residual energy */
            return (float)C[ 0 ][ 1 ];
        }
    }
}
