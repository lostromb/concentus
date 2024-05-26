/* Copyright (c) 2024 Logan Stromberg

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

using System;

namespace HellaUnsafe.Common
{
    internal static class CRuntime
    {
        internal static unsafe void OPUS_CLEAR(byte* dst, int elements)
        {
            new Span<byte>(dst, elements).Fill(0);
        }

        internal static unsafe void OPUS_CLEAR(float* dst, int elements)
        {
            new Span<float>(dst, elements).Fill(0);
        }

        internal static unsafe void OPUS_CLEAR(byte* dst, uint elements)
        {
            new Span<byte>(dst, (int)elements).Fill(0);
        }

        internal static unsafe void OPUS_COPY(byte* dst, byte* src, int elements)
        {
            new Span<byte>(src, elements).CopyTo(new Span<byte>(dst, elements));
        }

        internal static unsafe void OPUS_COPY(float* dst, float* src, int elements)
        {
            new Span<float>(src, elements).CopyTo(new Span<float>(dst, elements));
        }

        internal static unsafe void OPUS_COPY(float* dst, Span<float> src, int elements)
        {
            src.Slice(0, elements).CopyTo(new Span<float>(dst, elements));
        }

        internal static unsafe void OPUS_COPY(byte* dst, byte* src, uint elements)
        {
            new Span<byte>(src, (int)elements).CopyTo(new Span<byte>(dst, (int)elements));
        }

        internal static unsafe void OPUS_MOVE(byte* dst, byte* src, int elements)
        {
            new Span<byte>(src, elements).CopyTo(new Span<byte>(dst, elements));
        }

        internal static unsafe void OPUS_MOVE(byte* dst, byte* src, uint elements)
        {
            new Span<byte>(src, (int)elements).CopyTo(new Span<byte>(dst, (int)elements));
        }
    }
}
