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
    public static class OpusPacket
    {
        
        internal static int encode_size(int size, Pointer<byte> data)
        {
            if (size < 252)
            {
                data[0] = (byte)size;
                return 1;
            }
            else {
                data[0] = (byte)(252 + (size & 0x3));
                data[1] = (byte)((size - (int)data[0]) >> 2);
                return 2;
            }
        }

        internal static int parse_size(Pointer<byte> data, int len, BoxedValue<short> size)
        {
            if (len < 1)
            {
                size.Val = -1;
                return -1;
            }
            else if (data[0] < 252)
            {
                size.Val = data[0];
                return 1;
            }
            else if (len < 2)
            {
                size.Val = -1;
                return -1;
            }
            else {
                size.Val = Inlines.CHOP16(4 * data[1] + data[0]);
                return 2;
            }
        }

        /** Gets the number of samples per frame from an Opus packet.
  * @param [in] data <tt>char*</tt>: Opus packet.
  *                                  This must contain at least one byte of
  *                                  data.
  * @param [in] Fs <tt>opus_int32</tt>: Sampling rate in Hz.
  *                                     This must be a multiple of 400, or
  *                                     inaccurate results will be returned.
  * @returns Number of samples per frame.
  */
        internal static int opus_packet_get_samples_per_frame(Pointer<byte> data,
              int Fs)
        {
            int audiosize;
            if ((data[0] & 0x80) != 0)
            {
                audiosize = ((data[0] >> 3) & 0x3);
                audiosize = (Fs << audiosize) / 400;
            }
            else if ((data[0] & 0x60) == 0x60)
            {
                audiosize = ((data[0] & 0x08) != 0) ? Fs / 50 : Fs / 100;
            }
            else {
                audiosize = ((data[0] >> 3) & 0x3);
                if (audiosize == 3)
                    audiosize = Fs * 60 / 1000;
                else
                    audiosize = (Fs << audiosize) / 100;
            }
            return audiosize;
        }
        
        internal static int opus_packet_parse_impl(Pointer<byte> data, int len,
              int self_delimited, BoxedValue<byte> out_toc,
              Pointer<Pointer<byte>> frames, Pointer<short> size,
              BoxedValue<int> payload_offset, BoxedValue<int> packet_offset)
        {
            int i, bytes;
            int count;
            int cbr;
            byte ch, toc;
            int framesize;
            int last_size;
            int pad = 0;
            Pointer<byte> data0 = data;

            if (size == null)
                return OpusError.OPUS_BAD_ARG;

            framesize = opus_packet_get_samples_per_frame(data, 48000);

            cbr = 0;
            toc = data[0];
            data = data.Point(1);
            len--;
            last_size = len;
            switch (toc & 0x3)
            {
                /* One frame */
                case 0:
                    count = 1;
                    break;
                /* Two CBR frames */
                case 1:
                    count = 2;
                    cbr = 1;
                    if (self_delimited == 0)
                    {
                        if ((len & 0x1) != 0)
                            return OpusError.OPUS_INVALID_PACKET;
                        last_size = len / 2;
                        /* If last_size doesn't fit in size[0], we'll catch it later */
                        size[0] = (short)last_size;
                    }
                    break;
                /* Two VBR frames */
                case 2:
                    count = 2;
                    BoxedValue<short> boxed_size = new BoxedValue<short>(size[0]);
                    bytes = parse_size(data, len, boxed_size);
                    size[0] = boxed_size.Val;
                    len -= bytes;
                    if (size[0] < 0 || size[0] > len)
                        return OpusError.OPUS_INVALID_PACKET;
                    data = data.Point(bytes);
                    last_size = len - size[0];
                    break;
                /* Multiple CBR/VBR frames (from 0 to 120 ms) */
                default: /*case 3:*/
                    if (len < 1)
                        return OpusError.OPUS_INVALID_PACKET;
                    /* Number of frames encoded in bits 0 to 5 */
                    ch = data[0];
                    data = data.Point(1);
                    count = ch & 0x3F;
                    if (count <= 0 || framesize * count > 5760)
                        return OpusError.OPUS_INVALID_PACKET;
                    len--;
                    /* Padding flag is bit 6 */
                    if ((ch & 0x40) != 0)
                    {
                        int p;
                        do
                        {
                            int tmp;
                            if (len <= 0)
                                return OpusError.OPUS_INVALID_PACKET;
                            p = data[0];
                            data = data.Point(1);
                            len--;
                            tmp = p == 255 ? 254 : p;
                            len -= tmp;
                            pad += tmp;
                        } while (p == 255);
                    }
                    if (len < 0)
                        return OpusError.OPUS_INVALID_PACKET;
                    /* VBR flag is bit 7 */
                    cbr = (ch & 0x80) != 0 ? 0 : 1;
                    if (cbr == 0)
                    {
                        /* VBR case */
                        last_size = len;
                        for (i = 0; i < count - 1; i++)
                        {
                            boxed_size = new BoxedValue<short>(size[i]);
                            bytes = parse_size(data, len, boxed_size);
                            size[i] = boxed_size.Val;
                            len -= bytes;
                            if (size[i] < 0 || size[i] > len)
                                return OpusError.OPUS_INVALID_PACKET;
                            data = data.Point(bytes);
                            last_size -= bytes + size[i];
                        }
                        if (last_size < 0)
                            return OpusError.OPUS_INVALID_PACKET;
                    }
                    else if (self_delimited == 0)
                    {
                        /* CBR case */
                        last_size = len / count;
                        if (last_size * count != len)
                            return OpusError.OPUS_INVALID_PACKET;
                        for (i = 0; i < count - 1; i++)
                            size[i] = (short)last_size;
                    }
                    break;
            }

            /* Self-delimited framing has an extra size for the last frame. */
            if (self_delimited != 0)
            {
                BoxedValue<short> boxed_size = new BoxedValue<short>(size[count - 1]);
                bytes = parse_size(data, len, boxed_size);
                size[count - 1] = boxed_size.Val;
                len -= bytes;
                if (size[count - 1] < 0 || size[count - 1] > len)
                    return OpusError.OPUS_INVALID_PACKET;
                data = data.Point(bytes);
                /* For CBR packets, apply the size to all the frames. */
                if (cbr != 0)
                {
                    if (size[count - 1] * count > len)
                        return OpusError.OPUS_INVALID_PACKET;
                    for (i = 0; i < count - 1; i++)
                        size[i] = size[count - 1];
                }
                else if (bytes + size[count - 1] > last_size)
                    return OpusError.OPUS_INVALID_PACKET;
            }
            else
            {
                /* Because it's not encoded explicitly, it's possible the size of the
                   last packet (or all the packets, for the CBR case) is larger than
                   1275. Reject them here.*/
                if (last_size > 1275)
                    return OpusError.OPUS_INVALID_PACKET;
                size[count - 1] = (short)last_size;
            }

            if (payload_offset != null)
                payload_offset.Val = (int)(data.Offset - data0.Offset);

            for (i = 0; i < count; i++)
            {
                if (frames != null)
                    frames[i] = data;
                data = data.Point(size[i]);
            }

            if (packet_offset != null)
                packet_offset.Val = pad + (int)(data.Offset - data0.Offset);

            if (out_toc != null)
               out_toc.Val = toc;

            return count;
        }

        /** Parse an opus packet into one or more frames.
  * Opus_decode will perform this operation internally so most applications do
  * not need to use this function.
  * This function does not copy the frames, the returned pointers are pointers into
  * the input packet.
  * @param [in] data <tt>char*</tt>: Opus packet to be parsed
  * @param [in] len <tt>opus_int32</tt>: size of data
  * @param [out] out_toc <tt>char*</tt>: TOC pointer
  * @param [out] frames <tt>char*[48]</tt> encapsulated frames
  * @param [out] size <tt>opus_int16[48]</tt> sizes of the encapsulated frames
  * @param [out] payload_offset <tt>int*</tt>: returns the position of the payload within the packet (in bytes)
  * @returns number of frames
  */
        internal static int opus_packet_parse(Pointer<byte> data, int len,
              BoxedValue<byte> out_toc, Pointer<Pointer<byte>> frames,
              Pointer<short> size, BoxedValue<int> payload_offset)
        {
            return opus_packet_parse_impl(data, len, 0, out_toc,
                                          frames, size, payload_offset, null);
        }

        /** Gets the bandwidth of an Opus packet.
        * @param [in] data <tt>char*</tt>: Opus packet
        * @retval OPUS_BANDWIDTH_NARROWBAND Narrowband (4kHz bandpass)
        * @retval OPUS_BANDWIDTH_MEDIUMBAND Mediumband (6kHz bandpass)
        * @retval OPUS_BANDWIDTH_WIDEBAND Wideband (8kHz bandpass)
        * @retval OPUS_BANDWIDTH_SUPERWIDEBAND Superwideband (12kHz bandpass)
        * @retval OPUS_BANDWIDTH_FULLBAND Fullband (20kHz bandpass)
        * @retval OPUS_INVALID_PACKET The compressed data passed is corrupted or of an unsupported type
*/
        public static int opus_packet_get_bandwidth(Pointer<byte> data)
        {
            int bandwidth;
            if ((data[0] & 0x80) != 0)
            {
                bandwidth = OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND + ((data[0] >> 5) & 0x3);
                if (bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
                    bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
            }
            else if ((data[0] & 0x60) == 0x60)
            {
                bandwidth = ((data[0] & 0x10) != 0) ? OpusBandwidth.OPUS_BANDWIDTH_FULLBAND :
                                             OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
            }
            else {
                bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND + ((data[0] >> 5) & 0x3);
            }
            return bandwidth;
        }

        /** Gets the number of channels from an Opus packet.
        * @param [in] data <tt>char*</tt>: Opus packet
        * @returns Number of channels
        * @retval OPUS_INVALID_PACKET The compressed data passed is corrupted or of an unsupported type
*/
        public static int opus_packet_get_nb_channels(Pointer<byte> data)
        {
            return ((data[0] & 0x4) != 0) ? 2 : 1;
        }

        /** Gets the number of frames in an Opus packet.
        * @param [in] packet <tt>char*</tt>: Opus packet
        * @param [in] len <tt>opus_int32</tt>: Length of packet
        * @returns Number of frames
        * @retval OPUS_BAD_ARG Insufficient data was passed to the function
        * @retval OPUS_INVALID_PACKET The compressed data passed is corrupted or of an unsupported type
*/
        public static int opus_packet_get_nb_frames(Pointer<byte> packet, int len)
        {
            int count;
            if (len < 1)
                return OpusError.OPUS_BAD_ARG;
            count = packet[0] & 0x3;
            if (count == 0)
                return 1;
            else if (count != 3)
                return 2;
            else if (len < 2)
                return OpusError.OPUS_INVALID_PACKET;
            else
                return packet[1] & 0x3F;
        }

        /** Gets the number of samples of an Opus packet.
        * @param [in] packet <tt>char*</tt>: Opus packet
        * @param [in] len <tt>opus_int32</tt>: Length of packet
        * @param [in] Fs <tt>opus_int32</tt>: Sampling rate in Hz.
        *                                     This must be a multiple of 400, or
        *                                     inaccurate results will be returned.
        * @returns Number of samples
        * @retval OPUS_BAD_ARG Insufficient data was passed to the function
        * @retval OPUS_INVALID_PACKET The compressed data passed is corrupted or of an unsupported type
*/
        public static int opus_packet_get_nb_samples(Pointer<byte> packet, int len,
              int Fs)
        {
            int samples;
            int count = opus_packet_get_nb_frames(packet, len);

            if (count < 0)
                return count;

            samples = count * OpusPacket.opus_packet_get_samples_per_frame(packet, Fs);
            /* Can't have more than 120 ms */
            if (samples * 25 > Fs * 3)
                return OpusError.OPUS_INVALID_PACKET;
            else
                return samples;
        }

        /** Gets the number of samples of an Opus packet.
        * @param [in] dec <tt>OpusDecoder*</tt>: Decoder state
        * @param [in] packet <tt>char*</tt>: Opus packet
        * @param [in] len <tt>opus_int32</tt>: Length of packet
        * @returns Number of samples
        * @retval OPUS_BAD_ARG Insufficient data was passed to the function
        * @retval OPUS_INVALID_PACKET The compressed data passed is corrupted or of an unsupported type
*/
        public static int opus_decoder_get_nb_samples(OpusDecoder dec,
              Pointer<byte> packet, int len)
        {
            return opus_packet_get_nb_samples(packet, len, dec.Fs);
        }

        public static OpusMode opus_packet_get_mode(Pointer<byte> data)
        {
            OpusMode mode;
            if ((data[0] & 0x80) != 0)
            {
                mode = OpusMode.MODE_CELT_ONLY;
            }
            else if ((data[0] & 0x60) == 0x60)
            {
                mode = OpusMode.MODE_HYBRID;
            }
            else {
                mode = OpusMode.MODE_SILK_ONLY;
            }
            return mode;
        }

        // fixme: move these somewhere

        internal static void opus_pcm_soft_clip(Pointer<float> _x, int N, int C, Pointer<float> declip_mem)
        {
            int c;
            int i;
            Pointer<float> x;

            if (C < 1 || N < 1 || _x == null || declip_mem == null) return;

            /* First thing: saturate everything to +/- 2 which is the highest level our
               non-linearity can handle. At the point where the signal reaches +/-2,
               the derivative will be zero anyway, so this doesn't introduce any
               discontinuity in the derivative. */
            for (i = 0; i < N * C; i++)
                _x[i] = Inlines.MAX16(-2.0f, Inlines.MIN16(2.0f, _x[i]));
            for (c = 0; c < C; c++)
            {
                float a;
                float x0;
                int curr;

                x = _x.Point(c);
                a = declip_mem[c];
                /* Continue applying the non-linearity from the previous frame to avoid
                   any discontinuity. */
                for (i = 0; i < N; i++)
                {
                    if (x[i * C] * a >= 0)
                        break;
                    x[i * C] = x[i * C] + a * x[i * C] * x[i * C];
                }

                curr = 0;
                x0 = x[0];

                while (true)
                {
                    int start, end;
                    float maxval;
                    int special = 0;
                    int peak_pos;
                    for (i = curr; i < N; i++)
                    {
                        if (x[i * C] > 1 || x[i * C] < -1)
                            break;
                    }
                    if (i == N)
                    {
                        a = 0;
                        break;
                    }
                    peak_pos = i;
                    start = end = i;
                    maxval = Inlines.ABS16(x[i * C]);
                    /* Look for first zero crossing before clipping */
                    while (start > 0 && x[i * C] * x[(start - 1) * C] >= 0)
                        start--;
                    /* Look for first zero crossing after clipping */
                    while (end < N && x[i * C] * x[end * C] >= 0)
                    {
                        /* Look for other peaks until the next zero-crossing. */
                        if (Inlines.ABS16(x[end * C]) > maxval)
                        {
                            maxval = Inlines.ABS16(x[end * C]);
                            peak_pos = end;
                        }
                        end++;
                    }
                    /* Detect the special case where we clip before the first zero crossing */
                    special = (start == 0 && x[i * C] * x[0] >= 0) ? 1 : 0;

                    /* Compute a such that maxval + a*maxval^2 = 1 */
                    a = (maxval - 1) / (maxval * maxval);
                    if (x[i * C] > 0)
                        a = -a;
                    /* Apply soft clipping */
                    for (i = start; i < end; i++)
                        x[i * C] = x[i * C] + a * x[i * C] * x[i * C];

                    if (special != 0 && peak_pos >= 2)
                    {
                        /* Add a linear ramp from the first sample to the signal peak.
                           This avoids a discontinuity at the beginning of the frame. */
                        float delta;
                        float offset = x0 - x[0];
                        delta = offset / peak_pos;
                        for (i = curr; i < peak_pos; i++)
                        {
                            offset -= delta;
                            x[i * C] += offset;
                            x[i * C] = Inlines.MAX16(-1.0f, Inlines.MIN16(1.0f, x[i * C]));
                        }
                    }
                    curr = end;
                    if (curr == N)
                    {
                        break;
                    }
                }

                declip_mem[c] = a;
            }
        }


        public static string opus_strerror(int error)
        {
            string[] error_strings = {
              "success",
              "invalid argument",
              "buffer too small",
              "internal error",
              "corrupted stream",
              "request not implemented",
              "invalid state",
              "memory allocation failed"
           };
            if (error > 0 || error < -7)
                return "unknown error";
            else
                return error_strings[-error];
        }

        public static string opus_get_version_string()
        {
            return "concentus 1.0-fixed"
#if FUZZING
          + "-fuzzing"
#endif
          ;
        }
    }
}
