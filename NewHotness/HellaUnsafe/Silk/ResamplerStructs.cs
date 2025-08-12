using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HellaUnsafe.Silk
{
    internal static unsafe class ResamplerStructs
    {
        internal const int SILK_RESAMPLER_MAX_FIR_ORDER = 36;
        internal const int SILK_RESAMPLER_MAX_IIR_ORDER = 6;

        internal unsafe struct silk_resampler_state_struct
        {
            internal fixed int sIIR[SILK_RESAMPLER_MAX_IIR_ORDER]; /* this must be the first element of this struct */

            //union{
            //    opus_int32 i32;
            //    opus_int16 i16[SILK_RESAMPLER_MAX_FIR_ORDER];
            //}
            //sFIR;

            // Representing a union field with a common storage area and access methods for each union type
            private fixed int _sFIR[SILK_RESAMPLER_MAX_FIR_ORDER];
            internal int* sFIR_i32 => (int*)Unsafe.AsPointer(ref _sFIR[0]);
            internal short* sFIR_i16 => (short*)Unsafe.AsPointer(ref _sFIR[0]);

            internal fixed short delayBuf[48];
            internal int resampler_function;
            internal int batchSize;
            internal int invRatio_Q16;
            internal int FIR_Order;
            internal int FIR_Fracs;
            internal int Fs_in_kHz;
            internal int Fs_out_kHz;
            internal int inputDelay;
            internal short* Coefs;
        }
    }
}
