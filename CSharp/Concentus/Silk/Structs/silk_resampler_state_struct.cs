using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    public class silk_resampler_state_struct
    {
        public readonly Pointer<int> sIIR = Pointer.Malloc<int>(SilkConstants.SILK_RESAMPLER_MAX_IIR_ORDER); /* this must be the first element of this struct FIXME why? */
        public readonly Pointer<int> sFIR_i32 = Pointer.Malloc<int>(SilkConstants.SILK_RESAMPLER_MAX_FIR_ORDER);
        public readonly Pointer<short> sFIR_i16 = Pointer.Malloc<short>(SilkConstants.SILK_RESAMPLER_MAX_FIR_ORDER);

        public readonly Pointer<short> delayBuf = Pointer.Malloc<short>(48);
        public int resampler_function = 0;
        public int batchSize = 0;
        public int invRatio_Q16 = 0;
        public int FIR_Order = 0;
        public int FIR_Fracs = 0;
        public int Fs_in_kHz = 0;
        public int Fs_out_kHz = 0;
        public int inputDelay = 0;

        /// <summary>
        /// POINTER
        /// </summary>
        public Pointer<short> Coefs = null;

        public void Reset()
        {
            sIIR.MemSet(0, SilkConstants.SILK_RESAMPLER_MAX_IIR_ORDER);
            sFIR_i32.MemSet(0, SilkConstants.SILK_RESAMPLER_MAX_FIR_ORDER);
            sFIR_i16.MemSet(0, SilkConstants.SILK_RESAMPLER_MAX_FIR_ORDER);
            delayBuf.MemSet(0, 48);
            resampler_function = 0;
            batchSize = 0;
            invRatio_Q16 = 0;
            FIR_Order = 0;
            FIR_Fracs = 0;
            Fs_in_kHz = 0;
            Fs_out_kHz = 0;
            inputDelay = 0;
            Coefs = null;
        }

        public void Assign(silk_resampler_state_struct other)
        {
            resampler_function = other.resampler_function;
            batchSize = other.batchSize;
            invRatio_Q16 = other.invRatio_Q16;
            FIR_Order = other.FIR_Order;
            FIR_Fracs = other.FIR_Fracs;
            Fs_in_kHz = other.Fs_in_kHz;
            Fs_out_kHz = other.Fs_out_kHz;
            inputDelay = other.inputDelay;
            Coefs = other.Coefs;
            other.sIIR.MemCopyTo(this.sIIR, SilkConstants.SILK_RESAMPLER_MAX_IIR_ORDER);
            other.sFIR_i32.MemCopyTo(this.sFIR_i32, SilkConstants.SILK_RESAMPLER_MAX_FIR_ORDER);
            other.sFIR_i16.MemCopyTo(this.sFIR_i16, SilkConstants.SILK_RESAMPLER_MAX_FIR_ORDER);
            other.delayBuf.MemCopyTo(this.delayBuf, 48);
        }
    }
}
