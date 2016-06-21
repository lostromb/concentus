/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2010 Xiph.Org Foundation
   Originally written by Jean-Marc Valin, Gregory Maxwell, and the Opus open-source contributors
   Ported to C# by Logan Stromberg

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

namespace Concentus.Celt
{
    using Concentus.Celt.Enums;
    using Concentus.Celt.Structs;
    using Concentus.Common;
    using Concentus.Common.CPlusPlus;
    using Concentus.Enums;
    using System.Diagnostics;

    internal static class Modes
    {
        internal static readonly CeltMode mode48000_960_120 = new CeltMode
        {
            Fs = 48000,
            overlap = 120,
            nbEBands = 21,
            effEBands = 21,
            preemph = new int[] { 27853, 0, 4096, 8192 },
            eBands = Tables.eband5ms.GetPointer(),
            maxLM = 3,
            nbShortMdcts = 8,
            shortMdctSize = 120,
            nbAllocVectors = 11,
            allocVectors = Tables.band_allocation.GetPointer(),
            logN = Tables.logN400.GetPointer(),
            window = Tables.window120.GetPointer(),
            mdct = new MDCTLookup()
            {
                n = 1920,
                maxshift = 3,
                kfft = new FFTState[]
                {
                    Tables.fft_state48000_960_0,
                    Tables.fft_state48000_960_1,
                    Tables.fft_state48000_960_2,
                    Tables.fft_state48000_960_3,
                },
                trig = Tables.mdct_twiddles960.GetPointer()
            },
            cache = new PulseCache()
            {
                size = 392,
                index = Tables.cache_index50.GetPointer(),
                bits = Tables.cache_bits50.GetPointer(),
                caps = Tables.cache_caps50.GetPointer(),
            }
        };

        private static readonly CeltMode[] static_mode_list = new CeltMode[] {
            mode48000_960_120,
        };

        internal static CeltMode opus_custom_mode_create(int Fs, int frame_size, BoxedValue<int> error)
        {
            int i;

            for (i = 0; i < CeltConstants.TOTAL_MODES; i++)
            {
                int j;
                for (j = 0; j < 4; j++)
                {
                    if (Fs == static_mode_list[i].Fs &&
                          (frame_size << j) == static_mode_list[i].shortMdctSize * static_mode_list[i].nbShortMdcts)
                    {
                        if (error != null)
                            error.Val = OpusError.OPUS_OK;
                        return static_mode_list[i];
                    }
                }
            }

            if (error != null)
                error.Val = OpusError.OPUS_BAD_ARG;

            return null;
        }
    }
}
