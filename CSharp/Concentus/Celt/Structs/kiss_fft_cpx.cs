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

namespace Concentus.Celt.Structs
{
    using Concentus.Celt.Enums;
    using Concentus.Common;
    using Concentus.Common.CPlusPlus;
    using Concentus.Enums;
    using System;

    /// <summary>
    /// Complex numbers used in FFT calcs.
    /// TODO this should really really be a struct
    /// </summary>
    internal class kiss_fft_cpx
    {
        internal int r;
        internal int i;

        internal void Assign(kiss_fft_cpx other)
        {
            r = other.r;
            i = other.i;
        }

        /// <summary>
        /// Porting method that is needed because some parts of the code will arbitrarily cast int arrays into
        /// 2-element complex value arrays and vice versa. We should get rid of this as soon as possible because it's incredibly slow
        /// </summary>
        /// <param name="data"></param>
        /// <param name="numComplexValues"></param>
        /// <returns></returns>
        internal static kiss_fft_cpx[] ConvertInterleavedIntArray(Pointer<int> data, int numComplexValues)
        {
            kiss_fft_cpx[] returnVal = new kiss_fft_cpx[numComplexValues];
            for (int c = 0; c < numComplexValues; c++)
            {
                returnVal[c] = new kiss_fft_cpx()
                {
                    r = data[(2 * c)],
                    i = data[(2 * c) + 1],
                };
            }
            return returnVal;
        }

        /// <summary>
        /// does the reverse of the above function
        /// </summary>
        /// <param name="complex"></param>
        /// <param name="interleaved"></param>
        /// <param name="numComplexValues"></param>
        internal static void WriteComplexValuesToInterleavedIntArray(Pointer<kiss_fft_cpx> complex, Pointer<int> interleaved, int numComplexValues)
        {
            for (int c = 0; c < numComplexValues; c++)
            {
                interleaved[(2 * c)] = complex[c].r;
                interleaved[(2 * c) + 1] = complex[c].i;
            }
        }
    }
}
