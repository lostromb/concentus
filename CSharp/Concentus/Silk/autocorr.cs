using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;
using Concentus.Celt;

namespace Concentus.Silk
{
    public static class autocorr
    {
        /* Compute autocorrelation */
        public static void silk_autocorr(
            Pointer<int> results,           /* O    Result (length correlationCount)                            */
            BoxedValue<int> scale,             /* O    Scaling of the correlation vector                           */
            Pointer<short> inputData,         /* I    Input data to correlate                                     */
            int inputDataSize,      /* I    Length of input                                             */
            int correlationCount,   /* I    Number of correlation taps to compute                       */
            int arch                /* I    Run-time architecture                                       */
        )
        {
            int corrCount = Inlines.silk_min_int(inputDataSize, correlationCount);
            scale.Val = celt_autocorr._celt_autocorr(inputData, results, null, 0, corrCount - 1, inputDataSize, arch);
        }
    }
}
