using static HellaUnsafe.Silk.Float.InnerProductFLP;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class AutocorrelationFLP
    {
        /* compute autocorrelation */
        internal static unsafe void silk_autocorrelation_FLP(
            float          *results,           /* O    result (length correlationCount)                            */
            in float* inputData,         /* I    input data to correlate                                     */
            int            inputDataSize,      /* I    length of input                                             */
            int            correlationCount    /* I    number of correlation taps to compute                       */
        )
        {
            int i;

            if( correlationCount > inputDataSize ) {
                correlationCount = inputDataSize;
            }

            for( i = 0; i < correlationCount; i++ ) {
                results[ i ] =  (float)silk_inner_product_FLP( inputData, inputData + i, inputDataSize - i );
            }
        }
    }
}
