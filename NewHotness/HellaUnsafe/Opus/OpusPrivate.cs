using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HellaUnsafe.Opus
{
    internal static unsafe class OpusPrivate
    {
        internal const int MODE_SILK_ONLY = 1000;
        internal const int MODE_HYBRID = 1001;
        internal const int MODE_CELT_ONLY = 1002;

        internal const int OPUS_SET_VOICE_RATIO_REQUEST = 11018;
        internal const int OPUS_GET_VOICE_RATIO_REQUEST = 11019;

        internal const int OPUS_SET_FORCE_MODE_REQUEST = 11002;

        internal unsafe struct OpusRepacketizer
        {
            internal byte toc;
            internal int nb_frames;
            private fixed ulong _frames[48]; // use ulong as a substitute for nint (pointer-width)
            internal byte** frames => (byte**)Unsafe.AsPointer(ref _frames[0]);

            internal fixed short len[48];
            internal int framesize;
            private fixed ulong _paddings[48]; // same here
            internal byte** paddings => (byte**)Unsafe.AsPointer(ref _paddings[0]);

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
            /* then opus_val32 window_mem[channels*120]; */
            /* then opus_val32 preemph_mem[channels]; */
        }

        internal unsafe struct OpusMSDecoder
        {
            ChannelLayout layout;
            /* Decoder states go here */
        };
    }
}
