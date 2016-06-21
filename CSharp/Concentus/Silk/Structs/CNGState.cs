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
    using Concentus.Common.CPlusPlus;

    /// <summary>
    /// Struct for CNG
    /// </summary>
    internal class CNGState
    {
        internal readonly Pointer<int> CNG_exc_buf_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_FRAME_LENGTH);
        internal readonly Pointer<short> CNG_smth_NLSF_Q15 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);
        internal readonly Pointer<int> CNG_synth_state = Pointer.Malloc<int>(SilkConstants.MAX_LPC_ORDER);
        internal int CNG_smth_Gain_Q16 = 0;
        internal int rand_seed = 0;
        internal int fs_kHz = 0;

        internal void Reset()
        {
            CNG_exc_buf_Q14.MemSet(0, SilkConstants.MAX_FRAME_LENGTH);
            CNG_smth_NLSF_Q15.MemSet(0, SilkConstants.MAX_LPC_ORDER);
            CNG_synth_state.MemSet(0, SilkConstants.MAX_LPC_ORDER);
            CNG_smth_Gain_Q16 = 0;
            rand_seed = 0;
            fs_kHz = 0;
        }
    }
}
