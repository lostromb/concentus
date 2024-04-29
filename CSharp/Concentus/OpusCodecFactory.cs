using Concentus.Enums;
using Concentus.Structs;
using System;
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

using System.Collections.Generic;
using System.Text;

namespace Concentus
{
    /// <summary>
    /// Central factory class for creating Opus encoder / decoder structs.
    /// Using these methods allows the runtime to decide the most appropriate
    /// implementation for your platform based on what is available (generally,
    /// this means using a P/Invoke native adapter if native libopus is present)
    /// </summary>
    public static class OpusCodecFactory
    {
        /// <summary>
        /// Creates an IOpusEncoder appropriate for the current platform.
        /// This could potentially involve a native code layer.
        /// </summary>
        public static IOpusEncoder CreateEncoder(int Fs, int channels, OpusApplication application)
        {
            return new OpusEncoder(Fs, channels, application);
        }

        /// <summary>
        /// Creates an IOpusEncoder appropriate for the current platform.
        /// This could potentially involve a native code layer.
        /// </summary>
        public static IOpusDecoder CreateDecoder(int Fs, int channels)
        {
            return new OpusDecoder(Fs, channels);
        }

        /// <summary>
        /// Creates a multichannel Opus encoder using the "new API". This constructor allows you to use predefined Vorbis channel mappings, or specify your own.
        /// </summary>
        /// <param name="Fs">The samples rate of the input</param>
        /// <param name="channels">The total number of channels to encode (1 - 255)</param>
        /// <param name="mapping_family">The mapping family to use. 0 = mono/stereo, 1 = use Vorbis mappings, 255 = use raw channel mapping</param>
        /// <param name="streams">The number of streams to encode</param>
        /// <param name="coupled_streams">The number of coupled streams</param>
        /// <param name="mapping">A raw mapping of input/output channels</param>
        /// <param name="application">The application to use for the encoders</param>
        public static IOpusMultiStreamEncoder CreateMultiStreamEncoder(
            int Fs,
            int channels,
            int mapping_family,
            out int streams,
            out int coupled_streams,
            byte[] mapping,
            OpusApplication application)
        {
            return OpusMSEncoder.CreateSurround(Fs, channels, mapping_family, out streams, out coupled_streams, mapping, application);
        }

        /// <summary>
        /// Creates a new multichannel decoder
        /// </summary>
        /// <param name="Fs">The sample rate to decode to.</param>
        /// <param name="channels">The total number of channels being decoded.</param>
        /// <param name="streams">The number of streams being decoded.</param>
        /// <param name="coupled_streams">The number of coupled streams being decoded.</param>
        /// <param name="mapping">A raw mapping of input/output channels.</param>
        /// <returns></returns>
        public static IOpusMultiStreamDecoder CreateMultiStreamDecoder(
            int Fs,
            int channels,
            int streams,
            int coupled_streams,
            byte[] mapping)
        {
            return new OpusMSDecoder(Fs, channels, streams, coupled_streams, mapping);
        }
    }
}
