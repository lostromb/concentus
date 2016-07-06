using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Structs
{
    public class OpusMSDecoder
    {
        internal ChannelLayout layout = new ChannelLayout();
        internal OpusDecoder[] decoders = null;

        private OpusMSDecoder(int nb_streams, int nb_coupled_streams)
        {
            decoders = new OpusDecoder[nb_streams];
            for (int c = 0; c < nb_streams; c++)
                decoders[c] = new OpusDecoder();
        }

        #region API functions

        internal int opus_multistream_decoder_init(
      int Fs,
      int channels,
      int streams,
      int coupled_streams,
      byte[] mapping
)
        {
            int i, ret;
            int decoder_ptr = 0;

            if ((channels > 255) || (channels < 1) || (coupled_streams > streams) ||
                (streams < 1) || (coupled_streams < 0) || (streams > 255 - coupled_streams))
                throw new ArgumentException("Invalid channel or coupled stream count");

            this.layout.nb_channels = channels;
            this.layout.nb_streams = streams;
            this.layout.nb_coupled_streams = coupled_streams;

            for (i = 0; i < this.layout.nb_channels; i++)
                this.layout.mapping[i] = mapping[i];
            if (OpusMultistream.validate_layout(this.layout) == 0)
                throw new ArgumentException("Invalid surround channel layout");

            for (i = 0; i < this.layout.nb_coupled_streams; i++)
            {
                ret = this.decoders[decoder_ptr].opus_decoder_init(Fs, 2);
                if (ret != OpusError.OPUS_OK) return ret;
                decoder_ptr++;
            }
            for (; i < this.layout.nb_streams; i++)
            {
                ret = this.decoders[decoder_ptr].opus_decoder_init(Fs, 1);
                if (ret != OpusError.OPUS_OK) return ret;
                decoder_ptr++;
            }
            return OpusError.OPUS_OK;
        }

        /// <summary>
        /// Creates a new MS decoder
        /// </summary>
        /// <param name="Fs"></param>
        /// <param name="channels"></param>
        /// <param name="streams"></param>
        /// <param name="coupled_streams"></param>
        /// <param name="mapping">A mapping family (just use { 0, 1, 255 })</param>
        /// <returns></returns>
        public static OpusMSDecoder Create(
              int Fs,
              int channels,
              int streams,
              int coupled_streams,
              byte[] mapping)
        {
            int ret;
            OpusMSDecoder st;
            if ((channels > 255) || (channels < 1) || (coupled_streams > streams) ||
                (streams < 1) || (coupled_streams < 0) || (streams > 255 - coupled_streams))
            {
                throw new ArgumentException("Invalid channel / stream configuration");
            }
            st = new OpusMSDecoder(streams, coupled_streams);
            ret = st.opus_multistream_decoder_init(Fs, channels, streams, coupled_streams, mapping);
            if (ret != OpusError.OPUS_OK)
            {
                if (ret == OpusError.OPUS_BAD_ARG)
                    throw new ArgumentException("Bad argument while creating MS decoder");
                throw new OpusException("Could not create MS decoder", ret);
            }
            return st;
        }

        internal delegate void opus_copy_channel_out_func<T>(
          T[] dst,
          int dst_ptr,
          int dst_stride,
          int dst_channel,
          short[] src,
          int src_ptr,
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

                count = OpusPacketInfo.opus_packet_parse_impl(data, len, (s != nb_streams - 1) ? 1 : 0, toc, null,
                                               size.GetPointer(), null, packet_offset);
                if (count < 0)
                    return count;

                tmp_samples = OpusPacketInfo.GetNumSamples(data.Data, data.Offset, packet_offset.Val, Fs);
                if (s != 0 && samples != tmp_samples)
                    return OpusError.OPUS_INVALID_PACKET;
                samples = tmp_samples;
                data = data.Point(packet_offset.Val);
                len -= packet_offset.Val;
            }

            return samples;
        }

        internal int opus_multistream_decode_native<T>(
      byte[] data,
      int data_ptr,
      int len,
      T[] pcm,
      int pcm_ptr,
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
            short[] buf;

            /* Limit frame_size to avoid excessive stack allocations. */
            Fs = this.SampleRate;
            frame_size = Inlines.IMIN(frame_size, Fs / 25 * 3);
            buf = new short[2 * frame_size];
            decoder_ptr = 0;

            if (len == 0)
                do_plc = 1;
            if (len < 0)
            {
                return OpusError.OPUS_BAD_ARG;
            }
            if (do_plc == 0 && len < 2 * this.layout.nb_streams - 1)
            {
                return OpusError.OPUS_INVALID_PACKET;
            }
            if (do_plc == 0)
            {
                int ret = opus_multistream_packet_validate(data.GetPointer(data_ptr), len, this.layout.nb_streams, Fs);
                if (ret < 0)
                {
                    return ret;
                }
                else if (ret > frame_size)
                {
                    return OpusError.OPUS_BUFFER_TOO_SMALL;
                }
            }
            for (s = 0; s < this.layout.nb_streams; s++)
            {
                OpusDecoder dec;
                int ret;

                dec = this.decoders[decoder_ptr++];

                if (do_plc == 0 && len <= 0)
                {
                    return OpusError.OPUS_INTERNAL_ERROR;
                }
                BoxedValue<int> packet_offset = new BoxedValue<int>(0);
                ret = dec.opus_decode_native(
                    data.GetPointer(data_ptr), len, buf.GetPointer(), frame_size, decode_fec,
                    (s != this.layout.nb_streams - 1) ? 1 : 0, packet_offset, soft_clip);
                data_ptr += packet_offset.Val;
                len -= packet_offset.Val;
                if (ret <= 0)
                {
                    return ret;
                }
                frame_size = ret;
                if (s < this.layout.nb_coupled_streams)
                {
                    int chan, prev;
                    prev = -1;
                    /* Copy "left" audio to the channel(s) where it belongs */
                    while ((chan = OpusMultistream.get_left_channel(this.layout, s, prev)) != -1)
                    {
                        copy_channel_out(pcm, pcm_ptr, this.layout.nb_channels, chan,
                           buf, 0, 2, frame_size);
                        prev = chan;
                    }
                    prev = -1;
                    /* Copy "right" audio to the channel(s) where it belongs */
                    while ((chan = OpusMultistream.get_right_channel(this.layout, s, prev)) != -1)
                    {
                        copy_channel_out(pcm, pcm_ptr, this.layout.nb_channels, chan,
                           buf, 1, 2, frame_size);
                        prev = chan;
                    }
                }
                else {
                    int chan, prev;
                    prev = -1;
                    /* Copy audio to the channel(s) where it belongs */
                    while ((chan = OpusMultistream.get_mono_channel(this.layout, s, prev)) != -1)
                    {
                        copy_channel_out(pcm, pcm_ptr, this.layout.nb_channels, chan,
                           buf, 0, 1, frame_size);
                        prev = chan;
                    }
                }
            }
            /* Handle muted channels */
            for (c = 0; c < this.layout.nb_channels; c++)
            {
                if (this.layout.mapping[c] == 255)
                {
                    copy_channel_out(pcm, pcm_ptr, this.layout.nb_channels, c,
                       null, 0, 0, frame_size);
                }
            }

            return frame_size;
        }

        internal static void opus_copy_channel_out_float(
          float[] dst,
          int dst_ptr,
          int dst_stride,
          int dst_channel,
          short[] src,
          int src_ptr,
          int src_stride,
          int frame_size
        )
        {
            int i;
            if (src != null)
            {
                for (i = 0; i < frame_size; i++)
                    dst[i * dst_stride + dst_channel + dst_ptr] = (1 / 32768.0f) * src[i * src_stride + src_ptr];
            }
            else
            {
                for (i = 0; i < frame_size; i++)
                    dst[i * dst_stride + dst_channel + dst_ptr] = 0;
            }
        }

        internal static void opus_copy_channel_out_short(
          short[] dst,
          int dst_ptr,
          int dst_stride,
          int dst_channel,
          short[] src,
          int src_ptr,
          int src_stride,
          int frame_size
        )
        {
            int i;
            if (src != null)
            {
                for (i = 0; i < frame_size; i++)
                    dst[i * dst_stride + dst_channel + dst_ptr] = src[i * src_stride + src_ptr];
            }
            else
            {
                for (i = 0; i < frame_size; i++)
                    dst[i * dst_stride + dst_channel + dst_ptr] = 0;
            }
        }

        public int DecodeMultistream(
              byte[] data,
              int data_offset,
              int len,
              short[] out_pcm,
              int out_pcm_offset,
              int frame_size,
              int decode_fec
        )
        {
            return opus_multistream_decode_native<short>(data, data_offset, len,
                out_pcm, out_pcm_offset, opus_copy_channel_out_short, frame_size, decode_fec, 0);
        }

        public int DecodeMultistream(byte[] data, int data_offset,
          int len, float[] out_pcm, int out_pcm_offset, int frame_size, int decode_fec)
        {
            return opus_multistream_decode_native<float>(data, data_offset, len,
                out_pcm, out_pcm_offset, opus_copy_channel_out_float, frame_size, decode_fec, 0);
        }

        #endregion

        #region Getters and setters

        public OpusBandwidth Bandwidth
        {
            get
            {
                if (decoders == null || decoders.Length == 0)
                    throw new InvalidOperationException("Decoder not initialized");
                return decoders[0].Bandwidth;
            }
        }

        public int SampleRate
        {
            get
            {
                if (decoders == null || decoders.Length == 0)
                    throw new InvalidOperationException("Decoder not initialized");
                return decoders[0].SampleRate;
            }
        }

        public int Gain
        {
            get
            {
                if (decoders == null || decoders.Length == 0)
                    return OpusError.OPUS_INVALID_STATE;
                return decoders[0].Gain;
            }
            set
            {
                for (int s = 0; s < layout.nb_streams; s++)
                {
                    decoders[s].Gain = value;
                }
            }
        }

        public int LastPacketDuration
        {
            get
            {
                if (decoders == null || decoders.Length == 0)
                    return OpusError.OPUS_INVALID_STATE;
                return decoders[0].LastPacketDuration;
            }
        }

        public uint FinalRange
        {
            get
            {
                uint value = 0;
                for (int s = 0; s < layout.nb_streams; s++)
                {
                    value ^= decoders[s].FinalRange;
                }
                return value;
            }
        }

        public void ResetState()
        {
            for (int s = 0; s < layout.nb_streams; s++)
            {
                decoders[s].ResetState();
            }
        }

        public OpusDecoder GetMultistreamDecoderState(int streamId)
        {
            return decoders[streamId];
        }
        
        #endregion
    }
}