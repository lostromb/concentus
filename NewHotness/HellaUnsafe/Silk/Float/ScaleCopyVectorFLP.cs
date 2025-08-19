namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class ScaleCopyVectorFLP
    {
        /* copy and multiply a vector by a constant */
        internal static unsafe void silk_scale_copy_vector_FLP(
            float          *data_out,
            in float    *data_in,
            float          gain,
            int            dataSize
        )
        {
            int i, dataSize4;

            /* 4x unrolled loop */
            dataSize4 = dataSize & 0xFFFC;
            for( i = 0; i < dataSize4; i += 4 ) {
                data_out[ i + 0 ] = gain * data_in[ i + 0 ];
                data_out[ i + 1 ] = gain * data_in[ i + 1 ];
                data_out[ i + 2 ] = gain * data_in[ i + 2 ];
                data_out[ i + 3 ] = gain * data_in[ i + 3 ];
            }

            /* any remaining elements */
            for( ; i < dataSize; i++ ) {
                data_out[ i ] = gain * data_in[ i ];
            }
        }
    }
}
