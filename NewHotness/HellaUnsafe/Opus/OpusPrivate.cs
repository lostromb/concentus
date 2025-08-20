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

using System.Runtime.CompilerServices;

namespace HellaUnsafe.Opus
{
    public static unsafe class OpusPrivate
    {
        public const int MODE_SILK_ONLY = 1000;
        public const int MODE_HYBRID = 1001;
        public const int MODE_CELT_ONLY = 1002;

        public const int OPUS_SET_VOICE_RATIO_REQUEST = 11018;
        public const int OPUS_GET_VOICE_RATIO_REQUEST = 11019;

        public const int OPUS_SET_FORCE_MODE_REQUEST = 11002;

        internal unsafe struct OpusRepacketizer
        {
            internal byte toc;
            internal int nb_frames;
            private fixed ulong _frames[48]; // use ulong as a substitute for nint (pointer-width)
            internal byte** frames => (byte**)Unsafe.AsPointer(ref _frames[0]); // jank! Assumes the repacketizer is globally pinned!

            internal fixed short len[48];
            internal int framesize;
            private fixed ulong _paddings[48]; // same here
            internal byte** paddings => (byte**)Unsafe.AsPointer(ref _paddings[0]); // jank! Assumes the repacketizer is globally pinned!

            internal fixed int padding_len[48];
        }

        internal unsafe struct opus_extension_data
        {
            internal int id;
            internal int frame;
            internal byte* data;
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
            MAPPING_TYPE_NONE = 0,
            MAPPING_TYPE_SURROUND = 1,
            MAPPING_TYPE_AMBISONICS = 2
        }

        internal unsafe struct OpusMSEncoder
        {
            internal ChannelLayout layout;
            internal int lfe_stream;
            internal int application;
            internal int variable_duration;
            internal MappingType mapping_type;
            internal int bitrate_bps;
            /* Encoder states go here */
            /* then float window_mem[channels*120]; */
            /* then float preemph_mem[channels]; */
        }

        internal unsafe struct OpusMSDecoder
        {
            ChannelLayout layout;
            /* Decoder states go here */
        };

        /* Make sure everything is properly aligned. */
        internal static unsafe int align(int i)
        {
            // With C# constant type widths this calculation is easy
            // This may need to be revisited if it becomes important for, say, AVX2 vector alignment or
            // ARMv7 runtimes that disallow unaligned reads
            return i;

            //struct foo {char c; union { void* p; int i; float v; } u;};

            //unsigned int alignment = offsetof(struct foo, u);

            ///* Optimizing compilers should optimize div and multiply into and
            //   for all sensible alignment values. */
            //return ((i + alignment - 1) / alignment) * alignment;
        }

        //typedef void (* downmix_func) (const void*, opus_val32 *, int, int, int, int, int);
        internal delegate void downmix_func(in void* _x, float* sub, int subframe, int offset, int c1, int c2, int C);
    }
}
