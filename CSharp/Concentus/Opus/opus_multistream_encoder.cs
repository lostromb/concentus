/* Copyright (c) 2011 Xiph.Org Foundation
   Written by Jean-Marc Valin 
   Ported to C# by Logan Stromberg */
/*
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

using Concentus.Celt;
using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using Concentus.Structs;
using System;
using static Concentus.Downmix;

namespace Concentus
{
    public static class opus_multistream_encoder
    {
        private class VorbisLayout
        {
            public VorbisLayout(int streams, int coupled_streams, byte[] map)
            {
                nb_streams = streams;
                nb_coupled_streams = coupled_streams;
                mapping = map;
            }

            public int nb_streams;
            public int nb_coupled_streams;
            public byte[] mapping;
        }

        /* Index is nb_channel-1*/
        private static readonly VorbisLayout[] vorbis_mappings = {
              new VorbisLayout(1, 0, new byte[] {0}),                      /* 1: mono */
              new VorbisLayout(1, 1, new byte[] {0, 1}),                   /* 2: stereo */
              new VorbisLayout(2, 1, new byte[] {0, 2, 1}),                /* 3: 1-d surround */
              new VorbisLayout(2, 2, new byte[] {0, 1, 2, 3}),             /* 4: quadraphonic surround */
              new VorbisLayout(3, 2, new byte[] {0, 4, 1, 2, 3}),          /* 5: 5-channel surround */
              new VorbisLayout(4, 2, new byte[] {0, 4, 1, 2, 3, 5}),       /* 6: 5.1 surround */
              new VorbisLayout(4, 3, new byte[] {0, 4, 1, 2, 3, 5, 6}),    /* 7: 6.1 surround */
              new VorbisLayout(5, 3, new byte[] {0, 6, 1, 2, 3, 4, 5, 7}), /* 8: 7.1 surround */
        };

        public delegate void opus_copy_channel_in_func<T>(
            Pointer<short> dst, int dst_stride, Pointer<T> src, int src_stride, int src_channel, int frame_size);
        
        internal static int validate_encoder_layout(ChannelLayout layout)
        {
            int s;
            for (s = 0; s < layout.nb_streams; s++)
            {
                if (s < layout.nb_coupled_streams)
                {
                    if (OpusMultistream.get_left_channel(layout, s, -1) == -1)
                        return 0;
                    if (OpusMultistream.get_right_channel(layout, s, -1) == -1)
                        return 0;
                }
                else {
                    if (OpusMultistream.get_mono_channel(layout, s, -1) == -1)
                        return 0;
                }
            }
            return 1;
        }

        internal static void channel_pos(int channels, int[] pos/*[8]*/)
        {
            /* Position in the mix: 0 don't mix, 1: left, 2: center, 3:right */
            if (channels == 4)
            {
                pos[0] = 1;
                pos[1] = 3;
                pos[2] = 1;
                pos[3] = 3;
            }
            else if (channels == 3 || channels == 5 || channels == 6)
            {
                pos[0] = 1;
                pos[1] = 2;
                pos[2] = 3;
                pos[3] = 1;
                pos[4] = 3;
                pos[5] = 0;
            }
            else if (channels == 7)
            {
                pos[0] = 1;
                pos[1] = 2;
                pos[2] = 3;
                pos[3] = 1;
                pos[4] = 3;
                pos[5] = 2;
                pos[6] = 0;
            }
            else if (channels == 8)
            {
                pos[0] = 1;
                pos[1] = 2;
                pos[2] = 3;
                pos[3] = 1;
                pos[4] = 3;
                pos[5] = 1;
                pos[6] = 3;
                pos[7] = 0;
            }
        }

        private static readonly int[] diff_table/*[17]*/ = {
             Inlines.QCONST16(0.5000000f, CeltConstants.DB_SHIFT), Inlines.QCONST16(0.2924813f, CeltConstants.DB_SHIFT), Inlines.QCONST16(0.1609640f, CeltConstants.DB_SHIFT), Inlines.QCONST16(0.0849625f, CeltConstants.DB_SHIFT),
             Inlines.QCONST16(0.0437314f, CeltConstants.DB_SHIFT), Inlines.QCONST16(0.0221971f, CeltConstants.DB_SHIFT), Inlines.QCONST16(0.0111839f, CeltConstants.DB_SHIFT), Inlines.QCONST16(0.0056136f, CeltConstants.DB_SHIFT),
             Inlines.QCONST16(0.0028123f, CeltConstants.DB_SHIFT)
       };

        /* Computes a rough approximation of log2(2^a + 2^b) */
        internal static int logSum(int a, int b)
        {
            int max;
            int diff;
            int frac;

            int low;
            if (a > b)
            {
                max = a;
                diff = Inlines.SUB32(Inlines.EXTEND32(a), Inlines.EXTEND32(b));
            }
            else {
                max = b;
                diff = Inlines.SUB32(Inlines.EXTEND32(b), Inlines.EXTEND32(a));
            }
            if (!(diff < Inlines.QCONST16(8.0f, CeltConstants.DB_SHIFT)))  /* inverted to catch NaNs */
                return max;
            low = Inlines.SHR32(diff, CeltConstants.DB_SHIFT - 1);
            frac = Inlines.SHL16(diff - Inlines.SHL16(low, CeltConstants.DB_SHIFT - 1), 16 - CeltConstants.DB_SHIFT);
            return max + diff_table[low] + Inlines.MULT16_16_Q15(frac, Inlines.SUB16(diff_table[low + 1], diff_table[low]));
        }

        // fixme: test the perf of this alternate implementation
        //int logSum(int a, int b)
        //{
        //    return log2(pow(4, a) + pow(4, b)) / 2;
        //}

        internal static void surround_analysis<T>(CeltMode celt_mode, Pointer<T> pcm,
            Pointer<int> bandLogE, Pointer<int> mem, Pointer<int> preemph_mem,
          int len, int overlap, int channels, int rate, opus_copy_channel_in_func<T> copy_channel_in
    )
        {
            int c;
            int i;
            int LM;
            int[] pos = { 0, 0, 0, 0, 0, 0, 0, 0 };
            int upsample;
            int frame_size;
            int channel_offset;
            int[] bandE = new int[21];
            int[][] maskLogE = Arrays.InitTwoDimensionalArray<int>(3, 21);
            Pointer<int> input;
            Pointer<short> x;
            Pointer<int> freq;

            upsample = CeltCommon.resampling_factor(rate);
            frame_size = len * upsample;

            for (LM = 0; LM < celt_mode.maxLM; LM++)
                if (celt_mode.shortMdctSize << LM == frame_size)
                    break;

            input = Pointer.Malloc<int>(frame_size + overlap);
            x = Pointer.Malloc<short>(len);
            freq = Pointer.Malloc<int>(frame_size);

            channel_pos(channels, pos);

            for (c = 0; c < 3; c++)
                for (i = 0; i < 21; i++)
                    maskLogE[c][i] = -Inlines.QCONST16(28.0f, CeltConstants.DB_SHIFT);

            for (c = 0; c < channels; c++)
            {
                mem.Point(c * overlap).MemCopyTo(input, overlap);
                copy_channel_in(x, 1, pcm, channels, c, len);
                BoxedValue<int> boxed_preemph = new BoxedValue<int>(preemph_mem[c]);
                CeltCommon.celt_preemphasis(x, input.Point(overlap), frame_size, 1, upsample, celt_mode.preemph.GetPointer(), boxed_preemph, 0);
                preemph_mem[c] = boxed_preemph.Val;

                MDCT.clt_mdct_forward(celt_mode.mdct, input, freq, celt_mode.window,
                      overlap, celt_mode.maxLM - LM, 1);
                if (upsample != 1)
                {
                    int bound = len;
                    for (i = 0; i < bound; i++)
                        freq[i] *= upsample;
                    for (; i < frame_size; i++)
                        freq[i] = 0;
                }

                Bands.compute_band_energies(celt_mode, freq, bandE.GetPointer(), 21, 1, LM);
                QuantizeBands.amp2Log2(celt_mode, 21, 21, bandE.GetPointer(), bandLogE.Point(21 * c), 1);
                /* Apply spreading function with -6 dB/band going up and -12 dB/band going down. */
                for (i = 1; i < 21; i++)
                    bandLogE[21 * c + i] = Inlines.MAX16(bandLogE[21 * c + i], bandLogE[21 * c + i - 1] - Inlines.QCONST16(1.0f, CeltConstants.DB_SHIFT));
                for (i = 19; i >= 0; i--)
                    bandLogE[21 * c + i] = Inlines.MAX16(bandLogE[21 * c + i], bandLogE[21 * c + i + 1] - Inlines.QCONST16(2.0f, CeltConstants.DB_SHIFT));
                if (pos[c] == 1)
                {
                    for (i = 0; i < 21; i++)
                        maskLogE[0][i] = logSum(maskLogE[0][i], bandLogE[21 * c + i]);
                }
                else if (pos[c] == 3)
                {
                    for (i = 0; i < 21; i++)
                        maskLogE[2][i] = logSum(maskLogE[2][i], bandLogE[21 * c + i]);
                }
                else if (pos[c] == 2)
                {
                    for (i = 0; i < 21; i++)
                    {
                        maskLogE[0][i] = logSum(maskLogE[0][i], bandLogE[21 * c + i] - Inlines.QCONST16(.5f, CeltConstants.DB_SHIFT));
                        maskLogE[2][i] = logSum(maskLogE[2][i], bandLogE[21 * c + i] - Inlines.QCONST16(.5f, CeltConstants.DB_SHIFT));
                    }
                }

                input.Point(frame_size).MemCopyTo(mem.Point(c * overlap), overlap);
            }
            for (i = 0; i < 21; i++)
                maskLogE[1][i] = Inlines.MIN32(maskLogE[0][i], maskLogE[2][i]);
            channel_offset = Inlines.HALF16(Inlines.celt_log2(Inlines.QCONST32(2.0f, 14) / (channels - 1)));
            for (c = 0; c < 3; c++)
                for (i = 0; i < 21; i++)
                    maskLogE[c][i] += channel_offset;

            for (c = 0; c < channels; c++)
            {
                Pointer<int> mask;
                if (pos[c] != 0)
                {
                    // fixme: I think this 2-d array needs to be linearized
                    mask = maskLogE[pos[c] - 1].GetPointer(0);
                    for (i = 0; i < 21; i++)
                        bandLogE[21 * c + i] = bandLogE[21 * c + i] - mask[i];
                }
                else {
                    for (i = 0; i < 21; i++)
                        bandLogE[21 * c + i] = 0;
                }
            }
        }

        public static int opus_multistream_encoder_init(
              OpusMSEncoder st,
              int Fs,
              int channels,
              int streams,
              int coupled_streams,
              Pointer<byte> mapping,
              OpusApplication application,
              int surround
        )
        {
            int i, ret;
            int encoder_ptr;

            if ((channels > 255) || (channels < 1) || (coupled_streams > streams) ||
                (streams < 1) || (coupled_streams < 0) || (streams > 255 - coupled_streams))
                return OpusError.OPUS_BAD_ARG;
            
            st.layout.nb_channels = channels;
            st.layout.nb_streams = streams;
            st.layout.nb_coupled_streams = coupled_streams;
            st.subframe_mem[0] = st.subframe_mem[1] = st.subframe_mem[2] = 0;
            if (surround == 0)
                st.lfe_stream = -1;
            st.bitrate_bps = OpusConstants.OPUS_AUTO;
            st.application = application;
            st.variable_duration = OpusFramesize.OPUS_FRAMESIZE_ARG;
            for (i = 0; i < st.layout.nb_channels; i++)
                st.layout.mapping[i] = mapping[i];
            if (OpusMultistream.validate_layout(st.layout) == 0 || validate_encoder_layout(st.layout) == 0)
                return OpusError.OPUS_BAD_ARG;

            encoder_ptr = 0;

            for (i = 0; i < st.layout.nb_coupled_streams; i++)
            {
                ret = st.encoders[encoder_ptr].opus_init_encoder(Fs, 2, application);
                if (ret != OpusError.OPUS_OK) return ret;
                if (i == st.lfe_stream)
                    st.encoders[encoder_ptr].SetLFE(1);
                encoder_ptr += 1;
            }
            for (; i < st.layout.nb_streams; i++)
            {
                ret = st.encoders[encoder_ptr].opus_init_encoder(Fs, 1, application);
                if (i == st.lfe_stream)
                    st.encoders[encoder_ptr].SetLFE(1);
                if (ret != OpusError.OPUS_OK) return ret;
                encoder_ptr += 1;
            }
            if (surround != 0)
            {
                st.preemph_mem.GetPointer().MemSet(0, channels);
                st.window_mem.GetPointer().MemSet(0, channels * 120);
            }
            st.surround = surround;
            return OpusError.OPUS_OK;
        }

        public static int opus_multistream_surround_encoder_init(
              OpusMSEncoder st,
              int Fs,
              int channels,
              int mapping_family,
              BoxedValue<int> streams,
              BoxedValue<int> coupled_streams,
              Pointer<byte> mapping,
              OpusApplication application
        )
        {
            if ((channels > 255) || (channels < 1))
                return OpusError.OPUS_BAD_ARG;
            st.lfe_stream = -1;
            if (mapping_family == 0)
            {
                if (channels == 1)
                {
                    streams.Val = 1;
                    coupled_streams.Val = 0;
                    mapping[0] = 0;
                }
                else if (channels == 2)
                {
                    streams.Val = 1;
                    coupled_streams.Val = 1;
                    mapping[0] = 0;
                    mapping[1] = 1;
                }
                else
                    return OpusError.OPUS_UNIMPLEMENTED;
            }
            else if (mapping_family == 1 && channels <= 8 && channels >= 1)
            {
                int i;
                streams.Val = vorbis_mappings[channels - 1].nb_streams;
                coupled_streams.Val = vorbis_mappings[channels - 1].nb_coupled_streams;
                for (i = 0; i < channels; i++)
                    mapping[i] = vorbis_mappings[channels - 1].mapping[i];
                if (channels >= 6)
                    st.lfe_stream = streams.Val - 1;
            }
            else if (mapping_family == 255)
            {
                byte i;
                streams.Val = channels;
                coupled_streams.Val = 0;
                for (i = 0; i < channels; i++)
                    mapping[i] = i;
            }
            else
                return OpusError.OPUS_UNIMPLEMENTED;
            return opus_multistream_encoder_init(st, Fs, channels, streams.Val, coupled_streams.Val,
                  mapping, application, (channels > 2 && mapping_family == 1) ? 1 : 0);
        }

        public static OpusMSEncoder opus_multistream_encoder_create(
              int Fs,
              int channels,
              int streams,
              int coupled_streams,
              Pointer<byte> mapping,
              OpusApplication application,
              BoxedValue<int> error
        )
        {
            int ret;
            OpusMSEncoder st;
            if ((channels > 255) || (channels < 1) || (coupled_streams > streams) ||
                (streams < 1) || (coupled_streams < 0) || (streams > 255 - coupled_streams))
            {
                if (error != null)
                    error.Val = OpusError.OPUS_BAD_ARG;
                return null;
            }
            st = new OpusMSEncoder(streams, coupled_streams);
            ret = opus_multistream_encoder_init(st, Fs, channels, streams, coupled_streams, mapping, application, 0);
            if (ret != OpusError.OPUS_OK)
            {
                st = null;
            }
            if (error != null)
                error.Val = ret;
            return st;
        }

        internal static void GetStreamCount(int channels, int mapping_family, BoxedValue<int> nb_streams, BoxedValue<int> nb_coupled_streams)
        {
            if (mapping_family == 0)
            {
                if (channels == 1)
                {
                    nb_streams.Val = 1;
                    nb_coupled_streams.Val = 0;
                }
                else if (channels == 2)
                {
                    nb_streams.Val = 1;
                    nb_coupled_streams.Val = 1;
                }
                else
                    throw new ArgumentException("More than 2 channels requires custom mappings");
            }
            else if (mapping_family == 1 && channels <= 8 && channels >= 1)
            {
                nb_streams.Val = vorbis_mappings[channels - 1].nb_streams;
                nb_coupled_streams.Val = vorbis_mappings[channels - 1].nb_coupled_streams;
            }
            else if (mapping_family == 255)
            {
                nb_streams.Val = channels;
                nb_coupled_streams.Val = 0;
            }
            else
                throw new ArgumentException("Invalid mapping family");
        }

        public static OpusMSEncoder opus_multistream_surround_encoder_create(
              int Fs,
              int channels,
              int mapping_family,
              BoxedValue<int> streams,
              BoxedValue<int> coupled_streams,
              Pointer<byte> mapping,
              OpusApplication application,
              BoxedValue<int> error
        )
        {
            int ret;
            OpusMSEncoder st;
            if ((channels > 255) || (channels < 1) || application == OpusApplication.OPUS_APPLICATION_UNIMPLEMENTED)
            {
                if (error != null)
                    error.Val = OpusError.OPUS_BAD_ARG;
                return null;
            }
            BoxedValue<int> nb_streams = new BoxedValue<int>();
            BoxedValue<int> nb_coupled_streams = new BoxedValue<int>();
            GetStreamCount(channels, mapping_family, nb_streams, nb_coupled_streams);

            st = new OpusMSEncoder(nb_streams.Val, nb_coupled_streams.Val);
            if (st == null)
            {
                if (error != null)
                    error.Val = OpusError.OPUS_ALLOC_FAIL;
                return null;
            }
            ret = opus_multistream_surround_encoder_init(st, Fs, channels, mapping_family, streams, coupled_streams, mapping, application);
            if (ret != OpusError.OPUS_OK)
            {
                st = null;
            }
            if (error != null)
                error.Val = ret;
            return st;
        }

        internal static int surround_rate_allocation(
              OpusMSEncoder st,
              Pointer<int> rate,
              int frame_size
              )
        {
            int i;
            int channel_rate;
            int Fs;
            OpusEncoder ptr;
            int stream_offset;
            int lfe_offset;
            int coupled_ratio; /* Q8 */
            int lfe_ratio;     /* Q8 */
            int rate_sum = 0;

            ptr = st.encoders[0];// (char*)st + align(sizeof(OpusMSEncoder));
            Fs = ptr.GetSampleRate();

            if (st.bitrate_bps > st.layout.nb_channels * 40000)
                stream_offset = 20000;
            else
                stream_offset = st.bitrate_bps / st.layout.nb_channels / 2;
            stream_offset += 60 * (Fs / frame_size - 50);
            /* We start by giving each stream (coupled or uncoupled) the same bitrate.
               This models the main saving of coupled channels over uncoupled. */
            /* The LFE stream is an exception to the above and gets fewer bits. */
            lfe_offset = 3500 + 60 * (Fs / frame_size - 50);
            /* Coupled streams get twice the mono rate after the first 20 kb/s. */
            coupled_ratio = 512;
            /* Should depend on the bitrate, for now we assume LFE gets 1/8 the bits of mono */
            lfe_ratio = 32;

            /* Compute bitrate allocation between streams */
            if (st.bitrate_bps == OpusConstants.OPUS_AUTO)
            {
                channel_rate = Fs + 60 * Fs / frame_size;
            }
            else if (st.bitrate_bps == OpusConstants.OPUS_BITRATE_MAX)
            {
                channel_rate = 300000;
            }
            else {
                int nb_lfe;
                int nb_uncoupled;
                int nb_coupled;
                int total;
                nb_lfe = (st.lfe_stream != -1) ? 1 : 0;
                nb_coupled = st.layout.nb_coupled_streams;
                nb_uncoupled = st.layout.nb_streams - nb_coupled - nb_lfe;
                total = (nb_uncoupled << 8)         /* mono */
                      + coupled_ratio * nb_coupled /* stereo */
                      + nb_lfe * lfe_ratio;
                channel_rate = 256 * (st.bitrate_bps - lfe_offset * nb_lfe - stream_offset * (nb_coupled + nb_uncoupled)) / total;
            }

            for (i = 0; i < st.layout.nb_streams; i++)
            {
                if (i < st.layout.nb_coupled_streams)
                    rate[i] = stream_offset + (channel_rate * coupled_ratio >> 8);
                else if (i != st.lfe_stream)
                    rate[i] = stream_offset + channel_rate;
                else
                    rate[i] = lfe_offset + (channel_rate * lfe_ratio >> 8);
                rate[i] = Inlines.IMAX(rate[i], 500);
                rate_sum += rate[i];
            }
            return rate_sum;
        }

        /* Max size in case the encoder decides to return three frames */
        private const int MS_FRAME_TMP = (3 * 1275 + 7);

        internal static int opus_multistream_encode_native<T>
        (
            OpusMSEncoder st,
            opus_copy_channel_in_func<T> copy_channel_in,
            Pointer<T> pcm,
            int analysis_frame_size,
            Pointer<byte> data,
            int max_data_bytes,
            int lsb_depth,
            downmix_func<T> downmix,
            int float_api
        )
        {
            int Fs;
            int s;
            int encoder_ptr;
            int tot_size;
            Pointer<short> buf;
            Pointer<int> bandSMR;
            Pointer<byte> tmp_data = Pointer.Malloc<byte>(MS_FRAME_TMP);
            OpusRepacketizer rp = new OpusRepacketizer();
            int vbr;
            CeltMode celt_mode;
            Pointer<int> bitrates = Pointer.Malloc<int>(256);
            Pointer<int> bandLogE = Pointer.Malloc<int>(42);
            Pointer<int> mem = null;
            Pointer<int> preemph_mem = null;
            int frame_size;
            int rate_sum;
            int smallest_packet;

            if (st.surround != 0)
            {
                preemph_mem = st.preemph_mem.GetPointer();
                mem = st.window_mem.GetPointer();
            }

            encoder_ptr = 0;
            Fs = st.encoders[encoder_ptr].GetSampleRate();
            vbr = st.encoders[encoder_ptr].GetVBR() ? 1 : 0;
            celt_mode = st.encoders[encoder_ptr].GetCeltMode();

            {
                int delay_compensation;
                int channels;

                channels = st.layout.nb_streams + st.layout.nb_coupled_streams;
                delay_compensation = st.encoders[encoder_ptr].GetLookahead();
                delay_compensation -= Fs / 400;
                frame_size = CodecHelpers.compute_frame_size(pcm, analysis_frame_size,
                      st.variable_duration, channels, Fs, st.bitrate_bps,
                      delay_compensation, downmix
#if ENABLE_ANALYSIS
            , st.subframe_mem.GetPointer()
#endif
            );
            }

            if (400 * frame_size < Fs)
            {
                return OpusError.OPUS_BAD_ARG;
            }
            /* Validate frame_size before using it to allocate stack space.
               This mirrors the checks in opus_encode[_float](). */
            if (400 * frame_size != Fs && 200 * frame_size != Fs &&
                100 * frame_size != Fs && 50 * frame_size != Fs &&
                 25 * frame_size != Fs && 50 * frame_size != 3 * Fs)
            {
                return OpusError.OPUS_BAD_ARG;
            }

            /* Smallest packet the encoder can produce. */
            smallest_packet = st.layout.nb_streams * 2 - 1;
            if (max_data_bytes < smallest_packet)
            {
                return OpusError.OPUS_BUFFER_TOO_SMALL;
            }
            buf = Pointer.Malloc<short>(2 * frame_size);

            bandSMR = Pointer.Malloc<int>(21 * st.layout.nb_channels);
            if (st.surround != 0)
            {
                surround_analysis(celt_mode, pcm, bandSMR, mem, preemph_mem, frame_size, 120, st.layout.nb_channels, Fs, copy_channel_in);
            }

            /* Compute bitrate allocation between streams (this could be a lot better) */
            rate_sum = surround_rate_allocation(st, bitrates, frame_size);

            if (vbr == 0)
            {
                if (st.bitrate_bps == OpusConstants.OPUS_AUTO)
                {
                    max_data_bytes = Inlines.IMIN(max_data_bytes, 3 * rate_sum / (3 * 8 * Fs / frame_size));
                }
                else if (st.bitrate_bps != OpusConstants.OPUS_BITRATE_MAX)
                {
                    max_data_bytes = Inlines.IMIN(max_data_bytes, Inlines.IMAX(smallest_packet,
                                     3 * st.bitrate_bps / (3 * 8 * Fs / frame_size)));
                }
            }

            for (s = 0; s < st.layout.nb_streams; s++)
            {
                OpusEncoder enc = st.encoders[encoder_ptr];
                encoder_ptr += 1;
                enc.SetBitrate(bitrates[s]);
                if (st.surround != 0)
                {
                    int equiv_rate;
                    equiv_rate = st.bitrate_bps;
                    if (frame_size * 50 < Fs)
                        equiv_rate -= 60 * (Fs / frame_size - 50) * st.layout.nb_channels;
                    if (equiv_rate > 10000 * st.layout.nb_channels)
                        enc.SetBandwidth(OpusBandwidth.OPUS_BANDWIDTH_FULLBAND);
                    else if (equiv_rate > 7000 * st.layout.nb_channels)
                        enc.SetBandwidth(OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND);
                    else if (equiv_rate > 5000 * st.layout.nb_channels)
                        enc.SetBandwidth(OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND);
                    else
                        enc.SetBandwidth(OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND);
                    if (s < st.layout.nb_coupled_streams)
                    {
                        /* To preserve the spatial image, force stereo CELT on coupled streams */
                        enc.SetForceMode(OpusMode.MODE_CELT_ONLY);
                        enc.SetForceChannels(2);
                    }
                }
            }

            encoder_ptr = 0;
            /* Counting ToC */
            tot_size = 0;
            for (s = 0; s < st.layout.nb_streams; s++)
            {
                OpusEncoder enc;
                int len;
                int curr_max;
                int c1, c2;

                Repacketizer.opus_repacketizer_init(rp);
                enc = st.encoders[encoder_ptr];
                if (s < st.layout.nb_coupled_streams)
                {
                    int i;
                    int left, right;
                    left = OpusMultistream.get_left_channel(st.layout, s, -1);
                    right = OpusMultistream.get_right_channel(st.layout, s, -1);
                    copy_channel_in(buf, 2,
                       pcm, st.layout.nb_channels, left, frame_size);
                    copy_channel_in(buf.Point(1), 2,
                       pcm, st.layout.nb_channels, right, frame_size);
                    encoder_ptr += 1;
                    if (st.surround != 0)
                    {
                        for (i = 0; i < 21; i++)
                        {
                            bandLogE[i] = bandSMR[21 * left + i];
                            bandLogE[21 + i] = bandSMR[21 * right + i];
                        }
                    }
                    c1 = left;
                    c2 = right;
                }
                else {
                    int i;
                    int chan = OpusMultistream.get_mono_channel(st.layout, s, -1);
                    copy_channel_in(buf, 1,
                       pcm, st.layout.nb_channels, chan, frame_size);
                    encoder_ptr += 1;
                    if (st.surround != 0)
                    {
                        for (i = 0; i < 21; i++)
                            bandLogE[i] = bandSMR[21 * chan + i];
                    }
                    c1 = chan;
                    c2 = -1;
                }
                if (st.surround != 0)
                    enc.SetEnergyMask(bandLogE);

                /* number of bytes left (+Toc) */
                curr_max = max_data_bytes - tot_size;
                /* Reserve one byte for the last stream and two for the others */
                curr_max -= Inlines.IMAX(0, 2 * (st.layout.nb_streams - s - 1) - 1);
                curr_max = Inlines.IMIN(curr_max, MS_FRAME_TMP);
                /* Repacketizer will add one or two bytes for self-delimited frames */
                if (s != st.layout.nb_streams - 1) curr_max -= curr_max > 253 ? 2 : 1;
                if (vbr == 0 && s == st.layout.nb_streams - 1)
                    enc.SetBitrate(curr_max * (8 * Fs / frame_size));
                len = enc.opus_encode_native(buf.Data, buf.Offset, frame_size, tmp_data.Data, tmp_data.Offset, curr_max, lsb_depth,
                      pcm.Data, pcm.Offset, analysis_frame_size, c1, c2, st.layout.nb_channels, downmix, float_api);
                if (len < 0)
                {
                    return len;
                }
                /* We need to use the repacketizer to add the self-delimiting lengths
                   while taking into account the fact that the encoder can now return
                   more than one frame at a time (e.g. 60 ms CELT-only) */
                Repacketizer.opus_repacketizer_cat(rp, tmp_data, len);
                len = Repacketizer.opus_repacketizer_out_range_impl(rp, 0, Repacketizer.opus_repacketizer_get_nb_frames(rp),
                                                  data, max_data_bytes - tot_size, (s != st.layout.nb_streams - 1) ? 1 : 0, (vbr == 0 && s == st.layout.nb_streams - 1) ? 1 : 0);
                data = data.Point(len);
                tot_size += len;
            }

            return tot_size;
        }

        internal static void opus_copy_channel_in_float(
          Pointer<short> dst,
          int dst_stride,
          Pointer<float> src,
          int src_stride,
          int src_channel,
          int frame_size
        )
        {
            int i;
            for (i = 0; i < frame_size; i++)
                dst[i * dst_stride] = Inlines.FLOAT2INT16(src[i * src_stride + src_channel]);
        }

        internal static void opus_copy_channel_in_short(
          Pointer<short> dst,
          int dst_stride,
          Pointer<short> src,
          int src_stride,
          int src_channel,
          int frame_size
        )
        {
            int i;
            for (i = 0; i < frame_size; i++)
                dst[i * dst_stride] = src[i * src_stride + src_channel];
        }

        public static int opus_multistream_encode(
            OpusMSEncoder st,
            Pointer<short> pcm,
            int frame_size,
            Pointer<byte> data,
            int max_data_bytes
        )
        {
            return opus_multistream_encode_native<short>(st, opus_copy_channel_in_short,
               pcm, frame_size, data, max_data_bytes, 16, Downmix.downmix_int, 0);
        }

        public static int opus_multistream_encode_float(
            OpusMSEncoder st,
            Pointer<float> pcm,
            int frame_size,
            Pointer<byte> data,
            int max_data_bytes
        )
        {
            return opus_multistream_encode_native<float>(st, opus_copy_channel_in_float,
               pcm, frame_size, data, max_data_bytes, 16, Downmix.downmix_float, 1);
        }
    }
}
