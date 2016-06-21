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

    internal class CeltMode
    {
        internal int Fs = 0;
        internal int overlap = 0;

        internal int nbEBands = 0;
        internal int effEBands = 0;
        internal int[] preemph = { 0, 0, 0, 0 };

        /// <summary>
        /// Definition for each "pseudo-critical band"
        /// </summary>
        internal Pointer<short> eBands = null;

        internal int maxLM = 0;
        internal int nbShortMdcts = 0;
        internal int shortMdctSize = 0;

        /// <summary>
        /// Number of lines in allocVectors
        /// </summary>
        internal int nbAllocVectors = 0;

        /// <summary>
        /// Number of bits in each band for several rates
        /// </summary>
        internal Pointer<byte> allocVectors = null;
        internal Pointer<short> logN = null;

        internal Pointer<int> window = null;
        internal MDCTLookup mdct = new MDCTLookup();
        internal PulseCache cache = new PulseCache();

        internal CeltMode()
        {
        }

        internal void Reset()
        {
            Fs = 0;
            overlap = 0;
            nbEBands = 0;
            effEBands = 0;
            Arrays.MemSet<int>(preemph, 0);
            eBands = null;
            maxLM = 0;
            nbShortMdcts = 0;
            shortMdctSize = 0;
            nbAllocVectors = 0;
            allocVectors = null;
            logN = null;
            window = null;
            mdct.Reset();
            cache.Reset();
        }
    }
}
