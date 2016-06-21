/* Copyright (c) 2006-2011 Skype Limited. All Rights Reserved
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

namespace Concentus.Silk.Structs
{
    using Concentus.Common;
    using Concentus.Common.CPlusPlus;
    using Concentus.Silk.Enums;

    internal class SideInfoIndices
    {
        internal readonly Pointer<sbyte> GainsIndices = Pointer.Malloc<sbyte>(SilkConstants.MAX_NB_SUBFR);
        internal readonly Pointer<sbyte> LTPIndex = Pointer.Malloc<sbyte>(SilkConstants.MAX_NB_SUBFR);
        internal readonly Pointer<sbyte> NLSFIndices = Pointer.Malloc<sbyte>(SilkConstants.MAX_LPC_ORDER + 1);
        internal short lagIndex = 0;
        internal sbyte contourIndex = 0;
        internal sbyte signalType = 0;
        internal sbyte quantOffsetType = 0;
        internal sbyte NLSFInterpCoef_Q2 = 0;
        internal sbyte PERIndex = 0;
        internal sbyte LTP_scaleIndex = 0;
        internal sbyte Seed = 0;

        internal void Reset()
        {
            GainsIndices.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            LTPIndex.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            NLSFIndices.MemSet(0, SilkConstants.MAX_LPC_ORDER + 1);
            lagIndex = 0;
            contourIndex = 0;
            signalType = 0;
            quantOffsetType = 0;
            NLSFInterpCoef_Q2 = 0;
            PERIndex = 0;
            LTP_scaleIndex = 0;
            Seed = 0;
        }

        /// <summary>
        /// Overwrites this struct with values from another one. Equivalent to C struct assignment this = other
        /// </summary>
        /// <param name="other"></param>
        internal void Assign(SideInfoIndices other)
        {
            other.GainsIndices.MemCopyTo(this.GainsIndices, SilkConstants.MAX_NB_SUBFR);
            other.LTPIndex.MemCopyTo(this.LTPIndex, SilkConstants.MAX_NB_SUBFR);
            other.NLSFIndices.MemCopyTo(this.NLSFIndices, SilkConstants.MAX_LPC_ORDER + 1);
            this.lagIndex = other.lagIndex;
            this.contourIndex = other.contourIndex;
            this.signalType = other.signalType;
            this.quantOffsetType = other.quantOffsetType;
            this.NLSFInterpCoef_Q2 = other.NLSFInterpCoef_Q2;
            this.PERIndex = other.PERIndex;
            this.LTP_scaleIndex = other.LTP_scaleIndex;
            this.Seed = other.Seed;
        }
    }
}
