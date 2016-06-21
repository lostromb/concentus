using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus
{
    public static class opus_multistream_decoder
    {
        public static int opus_multistream_decoder_init(
      OpusMSDecoder st,
      int Fs,
      int channels,
      int streams,
      int coupled_streams,
      Pointer<byte> mapping
)
        {
            int i, ret;
            int decoder_ptr = 0;

            if ((channels > 255) || (channels < 1) || (coupled_streams > streams) ||
                (streams < 1) || (coupled_streams < 0) || (streams > 255 - coupled_streams))
                return OpusError.OPUS_BAD_ARG;

            st.layout.nb_channels = channels;
            st.layout.nb_streams = streams;
            st.layout.nb_coupled_streams = coupled_streams;

            for (i = 0; i < st.layout.nb_channels; i++)
                st.layout.mapping[i] = mapping[i];
            if (OpusMultistream.validate_layout(st.layout) == 0)
                return OpusError.OPUS_BAD_ARG;

            for (i = 0; i < st.layout.nb_coupled_streams; i++)
            {
                ret = st.decoders[decoder_ptr].opus_decoder_init(Fs, 2);
                if (ret != OpusError.OPUS_OK) return ret;
                decoder_ptr++;
            }
            for (; i < st.layout.nb_streams; i++)
            {
                ret = st.decoders[decoder_ptr].opus_decoder_init(Fs, 1);
                if (ret != OpusError.OPUS_OK) return ret;
                decoder_ptr++;
            }
            return OpusError.OPUS_OK;
        }

        public static OpusMSDecoder opus_multistream_decoder_create(
              int Fs,
              int channels,
              int streams,
              int coupled_streams,
      Pointer<byte> mapping,
      BoxedValue<int> error
)
        {
            int ret;
            OpusMSDecoder st;
            if ((channels > 255) || (channels < 1) || (coupled_streams > streams) ||
                (streams < 1) || (coupled_streams < 0) || (streams > 255 - coupled_streams))
            {
                if (error != null)
                    error.Val = OpusError.OPUS_BAD_ARG;
                return null;
            }
            st = new OpusMSDecoder(streams, coupled_streams);
            ret = opus_multistream_decoder_init(st, Fs, channels, streams, coupled_streams, mapping);
            if (error != null)
                error.Val = ret;
            if (ret != OpusError.OPUS_OK)
            {
                st = null;
            }
            return st;
        }

        public delegate void opus_copy_channel_out_func<T>(
          Pointer<T> dst,
          int dst_stride,
          int dst_channel,
          Pointer<short> src,
          int src_stride,
          int frame_size
        );

        internal static int opus_multistream_packet_validate(Pointer<byte> data,
            int len, int nb_streams, int Fs)
        {
            int s;
            int count;
            BoxedValue<byte> toc = new BoxedValue<byte>();
            short[] size = new short[48];
            int samples = 0;
            BoxedValue<int> packet_offset = new BoxedValue<int>();

            for (s = 0; s < nb_streams; s++)
            {
                int tmp_samples;
                if (len <= 0)
                    return OpusError.OPUS_INVALID_PACKET;

                count = OpusPacket.opus_packet_parse_impl(data, len, (s != nb_streams - 1) ? 1 : 0, toc, null,
                                               size.GetPointer(), null, packet_offset);
                if (count < 0)
                    return count;

                tmp_samples = OpusPacket.opus_packet_get_nb_samples(data, packet_offset.Val, Fs);
                if (s != 0 && samples != tmp_samples)
                    return OpusError.OPUS_INVALID_PACKET;
                samples = tmp_samples;
                data = data.Point(packet_offset.Val);
                len -= packet_offset.Val;
            }

            return samples;
        }

        internal static int opus_multistream_decode_native<T>(
              OpusMSDecoder st,
      Pointer<byte> data,
      int len,
      Pointer<T> pcm,
      opus_copy_channel_out_func<T> copy_channel_out,
      int frame_size,
      int decode_fec,
      int soft_clip
)
        {
            int Fs;
            int s, c;
            int decoder_ptr;
            int do_plc = 0;
            Pointer<short> buf;

            /* Limit frame_size to avoid excessive stack allocations. */
            Fs = st.GetSampleRate();
            frame_size = Inlines.IMIN(frame_size, Fs / 25 * 3);
            buf = Pointer.Malloc<short>(2 * frame_size);
            decoder_ptr = 0;

            if (len == 0)
                do_plc = 1;
            if (len < 0)
            {
                return OpusError.OPUS_BAD_ARG;
            }
            if (do_plc == 0 && len < 2 * st.layout.nb_streams - 1)
            {
                return OpusError.OPUS_INVALID_PACKET;
            }
            if (do_plc == 0)
            {
                int ret = opus_multistream_packet_validate(data, len, st.layout.nb_streams, Fs);
                if (ret < 0)
                {
                    return ret;
                }
                else if (ret > frame_size)
                {
                    return OpusError.OPUS_BUFFER_TOO_SMALL;
                }
            }
            for (s = 0; s < st.layout.nb_streams; s++)
            {
                OpusDecoder dec;
                int ret;

                dec = st.decoders[decoder_ptr++];

                if (do_plc == 0 && len <= 0)
                {
                    return OpusError.OPUS_INTERNAL_ERROR;
                }
                BoxedValue<int> packet_offset = new BoxedValue<int>(0);
                ret = dec.opus_decode_native(
                    data, len, buf, frame_size, decode_fec,
                    (s != st.layout.nb_streams - 1) ? 1 : 0, packet_offset, soft_clip);
                data = data.Point(packet_offset.Val);
                len -= packet_offset.Val;
                if (ret <= 0)
                {
                    return ret;
                }
                frame_size = ret;
                if (s < st.layout.nb_coupled_streams)
                {
                    int chan, prev;
                    prev = -1;
                    /* Copy "left" audio to the channel(s) where it belongs */
                    while ((chan = OpusMultistream.get_left_channel(st.layout, s, prev)) != -1)
                    {
                        copy_channel_out(pcm, st.layout.nb_channels, chan,
                           buf, 2, frame_size);
                        prev = chan;
                    }
                    prev = -1;
                    /* Copy "right" audio to the channel(s) where it belongs */
                    while ((chan = OpusMultistream.get_right_channel(st.layout, s, prev)) != -1)
                    {
                        copy_channel_out(pcm, st.layout.nb_channels, chan,
                           buf.Point(1), 2, frame_size);
                        prev = chan;
                    }
                }
                else {
                    int chan, prev;
                    prev = -1;
                    /* Copy audio to the channel(s) where it belongs */
                    while ((chan = OpusMultistream.get_mono_channel(st.layout, s, prev)) != -1)
                    {
                        copy_channel_out(pcm, st.layout.nb_channels, chan,
                           buf, 1, frame_size);
                        prev = chan;
                    }
                }
            }
            /* Handle muted channels */
            for (c = 0; c < st.layout.nb_channels; c++)
            {
                if (st.layout.mapping[c] == 255)
                {
                    copy_channel_out(pcm, st.layout.nb_channels, c,
                       null, 0, frame_size);
                }
            }

            return frame_size;
        }

        internal static void opus_copy_channel_out_float(
          Pointer<float> dst,
          int dst_stride,
          int dst_channel,
          Pointer<short> src,
          int src_stride,
          int frame_size
        )
        {
            int i;
            if (src != null)
            {
                for (i = 0; i < frame_size; i++)
                    dst[i * dst_stride + dst_channel] = (1 / 32768.0f) * src[i * src_stride];
            }
            else
            {
                for (i = 0; i < frame_size; i++)
                    dst[i * dst_stride + dst_channel] = 0;
            }
        }

        internal static void opus_copy_channel_out_short(
          Pointer<short> dst,
          int dst_stride,
          int dst_channel,
          Pointer<short> src,
          int src_stride,
          int frame_size
        )
        {
            int i;
            if (src != null)
            {
                // fixme: can use arraycopy here for speed
                for (i = 0; i < frame_size; i++)
                    dst[i * dst_stride + dst_channel] = src[i * src_stride];
            }
            else
            {
                for (i = 0; i < frame_size; i++)
                    dst[i * dst_stride + dst_channel] = 0;
            }
        }

        public static int opus_multistream_decode(
              OpusMSDecoder st,
              Pointer<byte> data,
              int len,
              Pointer<short> pcm,
              int frame_size,
              int decode_fec
        )
        {
            return opus_multistream_decode_native<short>(st, data, len,
                pcm, opus_copy_channel_out_short, frame_size, decode_fec, 0);
        }

        public static int opus_multistream_decode_float(OpusMSDecoder st, Pointer<byte> data,
          int len, Pointer<float> pcm, int frame_size, int decode_fec)
        {
            return opus_multistream_decode_native<float>(st, data, len,
                pcm, opus_copy_channel_out_float, frame_size, decode_fec, 0);
        }
    }
}
