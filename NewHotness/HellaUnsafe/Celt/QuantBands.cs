using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Celt
{
    internal static unsafe class QuantBands
    {
        /* Mean energy in each band quantized in Q4 and converted back to float */
        internal static readonly float* eMeans = AllocateGlobalArray(new float[25] {
              6.437500f, 6.250000f, 5.750000f, 5.312500f, 5.062500f,
              4.812500f, 4.500000f, 4.375000f, 4.875000f, 4.687500f,
              4.562500f, 4.437500f, 4.875000f, 4.625000f, 4.312500f,
              4.500000f, 4.375000f, 4.625000f, 4.750000f, 4.437500f,
              3.750000f, 3.750000f, 3.750000f, 3.750000f, 3.750000f
        });
    }
}
