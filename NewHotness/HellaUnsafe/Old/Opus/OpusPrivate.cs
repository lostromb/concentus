/* Copyright (c) 2012 Xiph.Org Foundation
   Written by Jean-Marc Valin */
/*
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

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
using System.Runtime.CompilerServices;
using static HellaUnsafe.Old.Celt.KissFFT;

namespace HellaUnsafe.Old.Opus
{
    internal unsafe struct OpusRepacketizer
    {
        [InlineArray(48)]
        internal unsafe struct BytePointer48
        {
            nint element0;
        }

        internal byte toc;
        internal int nb_frames;
        internal /*const*/ BytePointer48 _frames_storage;
        internal byte* frames => (byte*)Unsafe.AsPointer(ref _frames_storage);
        internal fixed short len[48];
        internal int framesize;
        internal /*const*/ BytePointer48 _paddings_storage;
        internal byte* paddings => (byte*)Unsafe.AsPointer(ref _paddings_storage);
        internal fixed int padding_len[48];
    }

    internal unsafe struct opus_extension_data
    {
        internal int id;
        internal int frame;
        internal /*const*/ byte* data;
        internal int len;
    }

    internal unsafe struct ChannelLayout
    {
        internal int nb_channels;
        internal int nb_streams;
        internal int nb_coupled_streams;
        internal fixed byte mapping[256];
    }

    internal enum MappingType
    {
        MAPPING_TYPE_NONE,
        MAPPING_TYPE_SURROUND,
        MAPPING_TYPE_AMBISONICS
    }

    internal struct OpusMSEncoder
    {
        internal ChannelLayout layout;
        internal int lfe_stream;
        internal int application;
        internal int variable_duration;
        internal MappingType mapping_type;
        internal int bitrate_bps;
        /* Encoder states go here */
        /* then opus_val32 window_mem[channels*120]; */
        /* then opus_val32 preemph_mem[channels]; */
    }

    internal struct OpusMSDecoder
    {
        internal ChannelLayout layout;
        /* Decoder states go here */
    }
}
