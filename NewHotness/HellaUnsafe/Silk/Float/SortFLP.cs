using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class SortFLP
    {
        internal static unsafe void silk_insertion_sort_decreasing_FLP(
            float          *a,                 /* I/O  Unsorted / Sorted vector                                    */
            int            *idx,               /* O    Index vector for the sorted elements                        */
            in int      L,                  /* I    Vector length                                               */
            in int      K                   /* I    Number of correctly sorted positions                        */
        )
        {
            float value;
            int   i, j;

            /* Safety checks */
            celt_assert( K >  0 );
            celt_assert( L >  0 );
            celt_assert( L >= K );

            /* Write start indices in index vector */
            for( i = 0; i < K; i++ ) {
                idx[ i ] = i;
            }

            /* Sort vector elements by value, decreasing order */
            for( i = 1; i < K; i++ ) {
                value = a[ i ];
                for( j = i - 1; ( j >= 0 ) && ( value > a[ j ] ); j-- ) {
                    a[ j + 1 ]   = a[ j ];      /* Shift value */
                    idx[ j + 1 ] = idx[ j ];    /* Shift index */
                }
                a[ j + 1 ]   = value;   /* Write value */
                idx[ j + 1 ] = i;       /* Write index */
            }

            /* If less than L values are asked check the remaining values,      */
            /* but only spend CPU to ensure that the K first values are correct */
            for( i = K; i < L; i++ ) {
                value = a[ i ];
                if( value > a[ K - 1 ] ) {
                    for( j = K - 2; ( j >= 0 ) && ( value > a[ j ] ); j-- ) {
                        a[ j + 1 ]   = a[ j ];      /* Shift value */
                        idx[ j + 1 ] = idx[ j ];    /* Shift index */
                    }
                    a[ j + 1 ]   = value;   /* Write value */
                    idx[ j + 1 ] = i;       /* Write index */
                }
            }
        }
    }
}
