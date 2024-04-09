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

using Concentus.Enums;
using Concentus.Structs;

namespace Concentus
{
    public interface IOpusMultiStreamEncoder
    {
        int EncodeMultistream(float[] pcm, int pcm_offset, int frame_size, byte[] outputBuffer, int outputBuffer_offset, int max_data_bytes);
        int EncodeMultistream(short[] pcm, int pcm_offset, int frame_size, byte[] outputBuffer, int outputBuffer_offset, int max_data_bytes);
        OpusEncoder GetMultistreamEncoderState(int streamId);
        void ResetState();

        OpusApplication Application { get; set; }
        OpusBandwidth Bandwidth { get; set; }
        int Bitrate { get; set; }
        int Complexity { get; set; }
        OpusFramesize ExpertFrameDuration { get; set; }
        uint FinalRange { get; }
        int ForceChannels { get; set; }
        OpusMode ForceMode { get; set; }
        int Lookahead { get; }
        int LSBDepth { get; set; }
        OpusBandwidth MaxBandwidth { get; set; }
        int PacketLossPercent { get; set; }
        bool PredictionDisabled { get; set; }
        int SampleRate { get; }
        OpusSignal SignalType { get; set; }
        bool UseConstrainedVBR { get; set; }
        bool UseDTX { get; set; }
        bool UseInbandFEC { get; set; }
        bool UseVBR { get; set; }
    }
}