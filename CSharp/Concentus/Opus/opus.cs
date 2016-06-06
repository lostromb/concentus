using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Opus.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus
{
    public static class opus
    {
        //public static void opus_pcm_soft_clip(Pointer<float> _x, int N, int C, Pointer<float> declip_mem)
        //{
        //    int c;
        //    int i;
        //    Pointer<float> x;

        //    if (C < 1 || N < 1 || _x == null || declip_mem == null) return;

        //    /* First thing: saturate everything to +/- 2 which is the highest level our
        //       non-linearity can handle. At the point where the signal reaches +/-2,
        //       the derivative will be zero anyway, so this doesn't introduce any
        //       discontinuity in the derivative. */
        //    for (i = 0; i < N * C; i++)
        //        _x[i] = Inlines.MAX16(-2.0f, Inlines.MIN16(2.0f, _x[i]));
        //    for (c = 0; c < C; c++)
        //    {
        //        float a;
        //        float x0;
        //        int curr;

        //        x = _x.Point(c);
        //        a = declip_mem[c];
        //        /* Continue applying the non-linearity from the previous frame to avoid
        //           any discontinuity. */
        //        for (i = 0; i < N; i++)
        //        {
        //            if (x[i * C] * a >= 0)
        //                break;
        //            x[i * C] = x[i * C] + a * x[i * C] * x[i * C];
        //        }

        //        curr = 0;
        //        x0 = x[0];

        //        while (true)
        //        {
        //            int start, end;
        //            float maxval;
        //            int special = 0;
        //            int peak_pos;
        //            for (i = curr; i < N; i++)
        //            {
        //                if (x[i * C] > 1 || x[i * C] < -1)
        //                    break;
        //            }
        //            if (i == N)
        //            {
        //                a = 0;
        //                break;
        //            }
        //            peak_pos = i;
        //            start = end = i;
        //            maxval = Inlines.ABS16(x[i * C]);
        //            /* Look for first zero crossing before clipping */
        //            while (start > 0 && x[i * C] * x[(start - 1) * C] >= 0)
        //                start--;
        //            /* Look for first zero crossing after clipping */
        //            while (end < N && x[i * C] * x[end * C] >= 0)
        //            {
        //                /* Look for other peaks until the next zero-crossing. */
        //                if (Inlines.ABS16(x[end * C]) > maxval)
        //                {
        //                    maxval = Inlines.ABS16(x[end * C]);
        //                    peak_pos = end;
        //                }
        //                end++;
        //            }
        //            /* Detect the special case where we clip before the first zero crossing */
        //            special = (start == 0 && x[i * C] * x[0] >= 0) ? 1 : 0;

        //            /* Compute a such that maxval + a*maxval^2 = 1 */
        //            a = (maxval - 1) / (maxval * maxval);
        //            if (x[i * C] > 0)
        //                a = -a;
        //            /* Apply soft clipping */
        //            for (i = start; i < end; i++)
        //                x[i * C] = x[i * C] + a * x[i * C] * x[i * C];

        //            if (special != 0 && peak_pos >= 2)
        //            {
        //                /* Add a linear ramp from the first sample to the signal peak.
        //                   This avoids a discontinuity at the beginning of the frame. */
        //                float delta;
        //                float offset = x0 - x[0];
        //                delta = offset / peak_pos;
        //                for (i = curr; i < peak_pos; i++)
        //                {
        //                    offset -= delta;
        //                    x[i * C] += offset;
        //                    x[i * C] = Inlines.MAX16(-1.0f, Inlines.MIN16(1.0f, x[i * C]));
        //                }
        //            }
        //            curr = end;
        //            if (curr == N)
        //            {
        //                break;
        //            }
        //        }

        //        declip_mem[c] = a;
        //    }
        //}

        public static int encode_size(int size, Pointer<byte> data)
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

        public static int parse_size(Pointer<byte> data, int len, BoxedValue<short> size)
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

        public static int opus_packet_get_samples_per_frame(Pointer<byte> data,
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

        public static int opus_packet_parse_impl(Pointer<byte> data, int len,
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

        public static int opus_packet_parse(Pointer<byte> data, int len,
              BoxedValue<byte> out_toc, Pointer<Pointer<byte>> frames,
              Pointer<short> size, BoxedValue<int> payload_offset)
        {
            return opus_packet_parse_impl(data, len, 0, out_toc,
                                          frames, size, payload_offset, null);
        }
    }
}
