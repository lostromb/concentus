using HellaUnsafe.Common;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Float.EnergyFLP;
using static HellaUnsafe.Silk.Float.LPCAnalysisFilterFLP;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class ResidualEnergyFLP
    {
        private const int MAX_ITERATIONS_RESIDUAL_NRG = 10;
        private const float REGULARIZATION_FACTOR = 1e-8f;

        /* Residual energy: nrg = wxx - 2 * wXx * c + c' * wXX * c */
        internal static unsafe float silk_residual_energy_covar_FLP(                              /* O    Weighted residual energy                    */
            in float                *c,                                 /* I    Filter coefficients                         */
            float                      *wXX,                               /* I/O  Weighted correlation matrix, reg. out       */
            in float                *wXx,                               /* I    Weighted correlation vector                 */
            in float                wxx,                                /* I    Weighted correlation value                  */
            in int                  D                                   /* I    Dimension                                   */
        )
        {
            int   i, j, k;
            float tmp, nrg = 0.0f, regularization;

            /* Safety checks */
            celt_assert( D >= 0 );

            regularization = REGULARIZATION_FACTOR * ( wXX[ 0 ] + wXX[ D * D - 1 ] );
            for( k = 0; k < MAX_ITERATIONS_RESIDUAL_NRG; k++ ) {
                nrg = wxx;

                tmp = 0.0f;
                for( i = 0; i < D; i++ ) {
                    tmp += wXx[ i ] * c[ i ];
                }
                nrg -= 2.0f * tmp;

                /* compute c' * wXX * c, assuming wXX is symmetric */
                for( i = 0; i < D; i++ ) {
                    tmp = 0.0f;
                    for( j = i + 1; j < D; j++ ) {
                        tmp += matrix_c_ptr( wXX, i, j, D ) * c[ j ];
                    }
                    nrg += c[ i ] * ( 2.0f * tmp + matrix_c_ptr( wXX, i, i, D ) * c[ i ] );
                }
                if( nrg > 0 ) {
                    break;
                } else {
                    /* Add white noise */
                    for( i = 0; i < D; i++ ) {
                        matrix_c_ptr( wXX, i, i, D ) +=  regularization;
                    }
                    /* Increase noise for next run */
                    regularization *= 2.0f;
                }
            }
            if( k == MAX_ITERATIONS_RESIDUAL_NRG ) {
                silk_assert( nrg == 0 );
                nrg = 1.0f;
            }

            return nrg;
        }

        /* Calculates residual energies of input subframes where all subframes have LPC_order   */
        /* of preceding samples                                                                 */
        internal static unsafe void silk_residual_energy_FLP(
            float*                      nrgs/*[ MAX_NB_SUBFR ]*/,               /* O    Residual energy per subframe                */
            in float*                x,                                /* I    Input signal                                */
            Native2DArray<float>                      a/*[ 2 ][ MAX_LPC_ORDER ]*/,            /* I    AR coefs for each frame half                */
            in float*                gains,                            /* I    Quantization gains                          */
            in int                  subfr_length,                       /* I    Subframe length                             */
            in int                  nb_subfr,                           /* I    number of subframes                         */
            in int                  LPC_order                           /* I    LPC order                                   */
        )
        {
            int     shift;
            float* LPC_res_ptr;
            float* LPC_res = stackalloc float[ ( MAX_FRAME_LENGTH + MAX_NB_SUBFR * MAX_LPC_ORDER ) / 2 ];

            LPC_res_ptr = LPC_res + LPC_order;
            shift = LPC_order + subfr_length;

            /* Filter input to create the LPC residual for each frame half, and measure subframe energies */
            silk_LPC_analysis_filter_FLP( LPC_res, a[ 0 ], x + 0 * shift, 2 * shift, LPC_order );
            nrgs[ 0 ] = ( float )( gains[ 0 ] * gains[ 0 ] * silk_energy_FLP( LPC_res_ptr + 0 * shift, subfr_length ) );
            nrgs[ 1 ] = ( float )( gains[ 1 ] * gains[ 1 ] * silk_energy_FLP( LPC_res_ptr + 1 * shift, subfr_length ) );

            if( nb_subfr == MAX_NB_SUBFR ) {
                silk_LPC_analysis_filter_FLP( LPC_res, a[ 1 ], x + 2 * shift, 2 * shift, LPC_order );
                nrgs[ 2 ] = ( float )( gains[ 2 ] * gains[ 2 ] * silk_energy_FLP( LPC_res_ptr + 0 * shift, subfr_length ) );
                nrgs[ 3 ] = ( float )( gains[ 3 ] * gains[ 3 ] * silk_energy_FLP( LPC_res_ptr + 1 * shift, subfr_length ) );
            }
        }
    }
}
