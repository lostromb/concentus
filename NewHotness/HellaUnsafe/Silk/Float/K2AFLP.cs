namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class K2AFLP
    {
        /* step up function, converts reflection coefficients to prediction coefficients */
        internal static unsafe void silk_k2a_FLP(
            float          *A,                 /* O     prediction coefficients [order]                            */
            in float* rc,                /* I     reflection coefficients [order]                            */
            int          order               /* I     prediction order                                           */
        )
        {
            int k, n;
            float rck, tmp1, tmp2;

            for( k = 0; k < order; k++ ) {
                rck = rc[ k ];
                for( n = 0; n < (k + 1) >> 1; n++ ) {
                    tmp1 = A[ n ];
                    tmp2 = A[ k - n - 1 ];
                    A[ n ]         = tmp1 + tmp2 * rck;
                    A[ k - n - 1 ] = tmp2 + tmp1 * rck;
                }
                A[ k ] = -rck;
            }
        }
    }
}
