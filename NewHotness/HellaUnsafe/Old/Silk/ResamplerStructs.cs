/***********************************************************************
Copyright (c) 2006-2011, Skype Limited. All rights reserved.
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
- Redistributions of source code must retain the above copyright notice,
this list of conditions and the following disclaimer.
- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.
- Neither the name of Internet Society, IETF or IETF Trust, nor the
names of specific contributors, may be used to endorse or promote
products derived from this software without specific prior written
permission.
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
***********************************************************************/


using HellaUnsafe.Old.Silk;
using static HellaUnsafe.Old.Celt.KissFFT;
using System.Runtime.CompilerServices;

namespace HellaUnsafe.Old.Silk
{
    internal static unsafe class ResamplerStructs
    {
        internal const int SILK_RESAMPLER_MAX_FIR_ORDER = 36;
        internal const int SILK_RESAMPLER_MAX_IIR_ORDER = 6;

        internal unsafe struct silk_resampler_state_struct
        {
            internal fixed int sIIR[SILK_RESAMPLER_MAX_IIR_ORDER]; /* this must be the first element of this struct */
            internal fixed int _sFIR_storage[SILK_RESAMPLER_MAX_FIR_ORDER];
            // Representing a union field with a common storage area and access methods for each union type
            internal int* sFIR_i32 => (int*)Unsafe.AsPointer(ref _sFIR_storage[0]);
            internal short* sFIR_i16 => (short*)Unsafe.AsPointer(ref _sFIR_storage[0]);
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
