namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class ScaleVectorFLP
    {
        /* multiply a vector by a constant */
        // OPT this is begging to be vectorized!
        internal static unsafe void silk_scale_vector_FLP(
            float* data1,
            float gain,
            int dataSize
        )
        {
            int i, dataSize4;

            /* 4x unrolled loop */
            dataSize4 = dataSize & 0xFFFC;
            for (i = 0; i < dataSize4; i += 4)
            {
                data1[i + 0] *= gain;
                data1[i + 1] *= gain;
                data1[i + 2] *= gain;
                data1[i + 3] *= gain;
            }

            /* any remaining elements */
            for (; i < dataSize; i++)
            {
                data1[i] *= gain;
            }
        }
    }
}
