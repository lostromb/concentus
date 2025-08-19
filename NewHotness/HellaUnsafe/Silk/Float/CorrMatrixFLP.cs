using static HellaUnsafe.Silk.Float.EnergyFLP;
using static HellaUnsafe.Silk.Float.InnerProductFLP;
using static HellaUnsafe.Silk.Macros;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class CorrMatrixFLP
    {
        /* Calculates correlation vector X'*t */
        internal static unsafe void silk_corrVector_FLP(
            in float                *x,                                 /* I    x vector [L+order-1] used to create X       */
            in float                *t,                                 /* I    Target vector [L]                           */
            in int                  L,                                  /* I    Length of vecors                            */
            in int                  Order,                              /* I    Max lag for correlation                     */
            float                      *Xt                                /* O    X'*t correlation vector [order]             */
        )
        {
            int lag;
            float *ptr1;

            ptr1 = &x[ Order - 1 ];                     /* Points to first sample of column 0 of X: X[:,0] */
            for( lag = 0; lag < Order; lag++ ) {
                /* Calculate X[:,lag]'*t */
                Xt[ lag ] = (float)silk_inner_product_FLP( ptr1, t, L );
                ptr1--;                                 /* Next column of X */
            }
        }

        /* Calculates correlation matrix X'*X */
        internal static unsafe void silk_corrMatrix_FLP(
            in float                *x,                                 /* I    x vector [ L+order-1 ] used to create X     */
            in int                  L,                                  /* I    Length of vectors                           */
            in int                  Order,                              /* I    Max lag for correlation                     */
            float                      *XX                                /* O    X'*X correlation matrix [order x order]     */
        )
        {
            int j, lag;
            double  energy;
            float *ptr1, ptr2;

            ptr1 = &x[ Order - 1 ];                     /* First sample of column 0 of X */
            energy = silk_energy_FLP( ptr1, L );  /* X[:,0]'*X[:,0] */
            matrix_ptr( XX, 0, 0, Order ) = ( float )energy;
            for( j = 1; j < Order; j++ ) {
                /* Calculate X[:,j]'*X[:,j] */
                energy += ptr1[ -j ] * ptr1[ -j ] - ptr1[ L - j ] * ptr1[ L - j ];
                matrix_ptr( XX, j, j, Order ) = ( float )energy;
            }

            ptr2 = &x[ Order - 2 ];                     /* First sample of column 1 of X */
            for( lag = 1; lag < Order; lag++ ) {
                /* Calculate X[:,0]'*X[:,lag] */
                energy = silk_inner_product_FLP( ptr1, ptr2, L );
                matrix_ptr( XX, lag, 0, Order ) = ( float )energy;
                matrix_ptr( XX, 0, lag, Order ) = ( float )energy;
                /* Calculate X[:,j]'*X[:,j + lag] */
                for( j = 1; j < ( Order - lag ); j++ ) {
                    energy += ptr1[ -j ] * ptr2[ -j ] - ptr1[ L - j ] * ptr2[ L - j ];
                    matrix_ptr( XX, lag + j, j, Order ) = ( float )energy;
                    matrix_ptr( XX, j, lag + j, Order ) = ( float )energy;
                }
                ptr2--;                                 /* Next column of X */
            }
        }
    }
}
