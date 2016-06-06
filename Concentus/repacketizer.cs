using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Common.Enums;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus
{
    public static class repacketizer
    {
        public static OpusRepacketizer opus_repacketizer_init(OpusRepacketizer rp)
        {
            rp.nb_frames = 0;
            return rp;
        }

        public static OpusRepacketizer opus_repacketizer_create()
        {
            OpusRepacketizer rp = new OpusRepacketizer();
            return opus_repacketizer_init(rp);
        }

        public static int opus_repacketizer_cat_impl(OpusRepacketizer rp, Pointer<byte> data, int len, int self_delimited)
        {
            BoxedValue<byte> tmp_toc = new BoxedValue<byte>();
            int curr_nb_frames, ret;
            /* Set of check ToC */
            if (len < 1)
            {
                return OpusError.OPUS_INVALID_PACKET;
            }

            if (rp.nb_frames == 0)
            {
                rp.toc = data[0];
                rp.framesize = opus.opus_packet_get_samples_per_frame(data, 8000);
            }
            else if ((rp.toc & 0xFC) != (data[0] & 0xFC))
            {
                /*fprintf(stderr, "toc mismatch: 0x%x vs 0x%x\n", rp.toc, data[0]);*/
                return OpusError.OPUS_INVALID_PACKET;
            }
            curr_nb_frames = opus_decoder.opus_packet_get_nb_frames(data, len);
            if (curr_nb_frames < 1)
                return OpusError.OPUS_INVALID_PACKET;

            /* Check the 120 ms maximum packet size */
            if ((curr_nb_frames + rp.nb_frames) * rp.framesize > 960)
            {
                return OpusError.OPUS_INVALID_PACKET;
            }

            ret = opus.opus_packet_parse_impl(data, len, self_delimited, tmp_toc, rp.frames.Point(rp.nb_frames), rp.len.Point(rp.nb_frames), null, null);
            if (ret < 1) return ret;

            rp.nb_frames += curr_nb_frames;
            return OpusError.OPUS_OK;
        }

        public static int opus_repacketizer_cat(OpusRepacketizer rp, Pointer<byte> data, int len)
        {
            return opus_repacketizer_cat_impl(rp, data, len, 0);
        }

        public static int opus_repacketizer_get_nb_frames(OpusRepacketizer rp)
        {
            return rp.nb_frames;
        }

        public static int opus_repacketizer_out_range_impl(OpusRepacketizer rp, int begin, int end,
              Pointer<byte> data, int maxlen, int self_delimited, int pad)
        {
            int i, count;
            int tot_size;
            Pointer<short> len;
            Pointer<Pointer<byte>> frames;
            Pointer<byte> ptr;

            if (begin < 0 || begin >= end || end > rp.nb_frames)
            {
                /*fprintf(stderr, "%d %d %d\n", begin, end, rp.nb_frames);*/
                return OpusError.OPUS_BAD_ARG;
            }
            count = end - begin;

            len = rp.len.Point(begin);
            frames = rp.frames.Point(begin);

            if (self_delimited != 0)
                tot_size = 1 + (len[count - 1] >= 252 ? 1 : 0);
            else
                tot_size = 0;

            ptr = data;
            if (count == 1)
            {
                /* Code 0 */
                tot_size += len[0] + 1;
                if (tot_size > maxlen)
                    return OpusError.OPUS_BUFFER_TOO_SMALL;
                ptr[0] = (byte)(rp.toc & 0xFC);
                ptr = ptr.Point(1);
            }
            else if (count == 2)
            {
                if (len[1] == len[0])
                {
                    /* Code 1 */
                    tot_size += 2 * len[0] + 1;
                    if (tot_size > maxlen)
                        return OpusError.OPUS_BUFFER_TOO_SMALL;
                    ptr[0] = (byte)((rp.toc & 0xFC) | 0x1);
                    ptr = ptr.Point(1);
                }
                else {
                    /* Code 2 */
                    tot_size += len[0] + len[1] + 2 + (len[0] >= 252 ? 1 : 0);
                    if (tot_size > maxlen)
                        return OpusError.OPUS_BUFFER_TOO_SMALL;
                    ptr[0] = (byte)((rp.toc & 0xFC) | 0x2);
                    ptr = ptr.Point(1);
                    ptr = ptr.Point(opus.encode_size(len[0], ptr));
                }
            }
            if (count > 2 || (pad != 0 && tot_size < maxlen))
            {
                /* Code 3 */
                int vbr;
                int pad_amount = 0;

                /* Restart the process for the padding case */
                ptr = data;
                if (self_delimited != 0)
                    tot_size = 1 + (len[count - 1] >= 252 ? 1 : 0);
                else
                    tot_size = 0;
                vbr = 0;
                for (i = 1; i < count; i++)
                {
                    if (len[i] != len[0])
                    {
                        vbr = 1;
                        break;
                    }
                }
                if (vbr != 0)
                {
                    tot_size += 2;
                    for (i = 0; i < count - 1; i++)
                        tot_size += 1 + (len[i] >= 252 ? 1 : 0) + len[i];
                    tot_size += len[count - 1];

                    if (tot_size > maxlen)
                        return OpusError.OPUS_BUFFER_TOO_SMALL;
                    ptr[0] = (byte)((rp.toc & 0xFC) | 0x3);
                    ptr = ptr.Point(1);
                    ptr[0] = Inlines.CHOP8U(count | 0x80);
                    ptr = ptr.Point(1);
                }
                else
                {
                    tot_size += count * len[0] + 2;
                    if (tot_size > maxlen)
                        return OpusError.OPUS_BUFFER_TOO_SMALL;
                    ptr[0] = (byte)((rp.toc & 0xFC) | 0x3);
                    ptr = ptr.Point(1);
                    ptr[0] = Inlines.CHOP8U(count);
                    ptr = ptr.Point(1);
                }

                pad_amount = pad != 0 ? (maxlen - tot_size) : 0;

                if (pad_amount != 0)
                {
                    int nb_255s;
                    data[1] |= 0x40;
                    nb_255s = (pad_amount - 1) / 255;
                    for (i = 0; i < nb_255s; i++)
                    {
                        ptr[0] = 255;
                        ptr = ptr.Point(1);
                    }

                    ptr[0] = (byte)(pad_amount - 255 * nb_255s - 1);
                    ptr = ptr.Point(1);
                    tot_size += pad_amount;
                }

                if (vbr != 0)
                {
                    for (i = 0; i < count - 1; i++)
                        ptr = ptr.Point(opus.encode_size(len[i], ptr));
                }
            }

            if (self_delimited != 0)
            {
                int sdlen = opus.encode_size(len[count - 1], ptr);
                ptr = ptr.Point(sdlen);
            }

            /* Copy the actual data */
            for (i = 0; i < count; i++)
            {
                /* Using OPUS_MOVE() instead of OPUS_COPY() in case we're doing in-place
                   padding from opus_packet_pad or opus_packet_unpad(). */
                   // fixme: what is this?
                //Debug.Assert(frames[i].Offset + len[i] <= data.Offset || ptr.Offset <= frames[i].Offset);
                // OPUS_MOVE(ptr, frames[i], len[i]);
                frames[i].MemMoveTo(ptr, len[i]);
                ptr = ptr.Point(len[i]);
            }

            if (pad != 0)
            {
                /* Fill padding with zeros. */
                // FIXME why did they not just use a MemSet(0) here?
                while (ptr.Offset < data.Offset + maxlen)
                {
                    ptr[0] = 0;
                    ptr = ptr.Point(1);
                }
            }

            return tot_size;
        }

        public static int opus_repacketizer_out_range(OpusRepacketizer rp, int begin, int end, Pointer<byte> data, int maxlen)
        {
            return opus_repacketizer_out_range_impl(rp, begin, end, data, maxlen, 0, 0);
        }

        public static int opus_repacketizer_out(OpusRepacketizer rp, Pointer<byte> data, int maxlen)
        {
            return opus_repacketizer_out_range_impl(rp, 0, rp.nb_frames, data, maxlen, 0, 0);
        }

        public static int opus_packet_pad(Pointer<byte> data, int len, int new_len)
        {
            OpusRepacketizer rp = new OpusRepacketizer();
            int ret;
            if (len < 1)
                return OpusError.OPUS_BAD_ARG;
            if (len == new_len)
                return OpusError.OPUS_OK;
            else if (len > new_len)
                return OpusError.OPUS_BAD_ARG;
            opus_repacketizer_init(rp);
            /* Moving payload to the end of the packet so we can do in-place padding */
            //OPUS_MOVE(data + new_len - len, data, len);
            data.MemMoveTo(data.Point(new_len - len), len);
            opus_repacketizer_cat(rp, data.Point(new_len - len), len);
            ret = opus_repacketizer_out_range_impl(rp, 0, rp.nb_frames, data, new_len, 0, 1);
            if (ret > 0)
                return OpusError.OPUS_OK;
            else
                return ret;
        }

        public static int opus_packet_unpad(Pointer<byte> data, int len)
        {
            int ret;
            if (len < 1)
                return OpusError.OPUS_BAD_ARG;

            OpusRepacketizer rp = new OpusRepacketizer();
            opus_repacketizer_init(rp);
            ret = opus_repacketizer_cat(rp, data, len);
            if (ret < 0)
                return ret;
            ret = opus_repacketizer_out_range_impl(rp, 0, rp.nb_frames, data, len, 0, 0);
            Debug.Assert(ret > 0 && ret <= len);

            return ret;
        }

        public static int opus_multistream_packet_pad(Pointer<byte> data, int len, int new_len, int nb_streams)
        {
            int s;
            int count;
            BoxedValue<byte> toc = new BoxedValue<byte>();
            short[] size = new short[48];
            BoxedValue<int> packet_offset = new BoxedValue<int>();
            int amount;

            if (len < 1)
                return OpusError.OPUS_BAD_ARG;
            if (len == new_len)
                return OpusError.OPUS_OK;
            else if (len > new_len)
                return OpusError.OPUS_BAD_ARG;
            amount = new_len - len;
            /* Seek to last stream */
            for (s = 0; s < nb_streams - 1; s++)
            {
                if (len <= 0)
                    return OpusError.OPUS_INVALID_PACKET;
                count = opus.opus_packet_parse_impl(data, len, 1, toc, null,
                                               size.GetPointer(), null, packet_offset);
                if (count < 0)
                    return count;
                data = data.Point(packet_offset.Val);
                len -= packet_offset.Val;
            }
            return opus_packet_pad(data, len, len + amount);
        }

        public static int opus_multistream_packet_unpad(Pointer<byte> data, int len, int nb_streams)
        {
            int s;
            BoxedValue<byte> toc = new BoxedValue<byte>();
            short[] size = new short[48];
            BoxedValue<int> packet_offset = new BoxedValue<int>();
            OpusRepacketizer rp = new OpusRepacketizer();
            Pointer<byte> dst;
            int dst_len;

            if (len < 1)
                return OpusError.OPUS_BAD_ARG;
            dst = data;
            dst_len = 0;
            /* Unpad all frames */
            for (s = 0; s < nb_streams; s++)
            {
                int ret;
                int self_delimited = ((s != nb_streams) ? 1 : 0) - 1;
                if (len <= 0)
                    return OpusError.OPUS_INVALID_PACKET;
                opus_repacketizer_init(rp);
                ret = opus.opus_packet_parse_impl(data, len, self_delimited, toc, null,
                                               size.GetPointer(), null, packet_offset);
                if (ret < 0)
                    return ret;
                ret = opus_repacketizer_cat_impl(rp, data, packet_offset.Val, self_delimited);
                if (ret < 0)
                    return ret;
                ret = opus_repacketizer_out_range_impl(rp, 0, rp.nb_frames, dst, len, self_delimited, 0);
                if (ret < 0)
                    return ret;
                else
                    dst_len += ret;
                dst = dst.Point(ret);
                data = data.Point(packet_offset.Val);
                len -= packet_offset.Val;
            }
            return dst_len;
        }

    }
}
