/* Copyright (c) 2011 Xiph.Org Foundation
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
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.Extensions;
using static HellaUnsafe.Opus.Opus;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Opus.OpusPrivate;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Opus
{
    internal static unsafe class Repacketizer
    {
        internal static unsafe int opus_repacketizer_get_size()
        {
            return sizeof(OpusRepacketizer);
        }

        internal static unsafe OpusRepacketizer* opus_repacketizer_init(OpusRepacketizer* rp)
        {
            rp->nb_frames = 0;
            return rp;
        }

        internal static unsafe OpusRepacketizer* opus_repacketizer_create()
        {
            OpusRepacketizer* rp;
            rp = (OpusRepacketizer*)opus_alloc(opus_repacketizer_get_size());
            if (rp == null) return null;
            return opus_repacketizer_init(rp);
        }

        internal static unsafe void opus_repacketizer_destroy(OpusRepacketizer* rp)
        {
            opus_free(rp);
        }

        internal static unsafe int opus_repacketizer_cat_impl(OpusRepacketizer* rp, in byte* data, int len, int self_delimited)
        {
            byte tmp_toc;
            int curr_nb_frames, ret;
            /* Set of check ToC */
            if (len < 1) return OPUS_INVALID_PACKET;
            if (rp->nb_frames == 0)
            {
                rp->toc = data[0];
                rp->framesize = opus_packet_get_samples_per_frame(data, 8000);
            }
            else if ((rp->toc & 0xFC) != (data[0] & 0xFC))
            {
                /*fprintf(stderr, "toc mismatch: 0x%x vs 0x%x\n", rp->toc, data[0]);*/
                return OPUS_INVALID_PACKET;
            }
            curr_nb_frames = opus_packet_get_nb_frames(data, len);
            if (curr_nb_frames < 1) return OPUS_INVALID_PACKET;

            /* Check the 120 ms maximum packet size */
            if ((curr_nb_frames + rp->nb_frames) * rp->framesize > 960)
            {
                return OPUS_INVALID_PACKET;
            }

            ret = opus_packet_parse_impl(data, len, self_delimited, &tmp_toc, &rp->frames[rp->nb_frames], &rp->len[rp->nb_frames],
                null, null, &rp->paddings[rp->nb_frames], &rp->padding_len[rp->nb_frames]);
            if (ret < 1) return ret;

            /* set padding length to zero for all but the first frame */
            while (curr_nb_frames > 1)
            {
                rp->nb_frames++;
                rp->padding_len[rp->nb_frames] = 0;
                rp->paddings[rp->nb_frames] = null;
                curr_nb_frames--;
            }
            rp->nb_frames++;
            return OPUS_OK;
        }

        internal static unsafe int opus_repacketizer_cat(OpusRepacketizer* rp, in byte* data, int len)
        {
            return opus_repacketizer_cat_impl(rp, data, len, 0);
        }

        internal static unsafe int opus_repacketizer_get_nb_frames(OpusRepacketizer* rp)
        {
            return rp->nb_frames;
        }

        internal static unsafe int opus_repacketizer_out_range_impl(OpusRepacketizer* rp, int begin, int end,
              byte* data, int maxlen, int self_delimited, int pad, in opus_extension_data* extensions, int nb_extensions)
        {
            int i, count;
            int tot_size;
            short* len;
            byte** frames;
            byte* ptr;
            int ones_begin = 0, ones_end = 0;
            int ext_begin = 0, ext_len = 0;
            int ext_count, total_ext_count;

            if (begin < 0 || begin >= end || end > rp->nb_frames)
            {
                /*fprintf(stderr, "%d %d %d\n", begin, end, rp->nb_frames);*/
                return OPUS_BAD_ARG;
            }
            count = end - begin;

            len = rp->len + begin;
            frames = rp->frames + begin;
            if (self_delimited != 0)
                tot_size = 1 + BOOL2INT(len[count - 1] >= 252);
            else
                tot_size = 0;

            /* figure out total number of extensions */
            total_ext_count = nb_extensions;
            for (i = begin; i < end; i++)
            {
                int n = opus_packet_extensions_count(rp->paddings[i], rp->padding_len[i]);
                if (n > 0) total_ext_count += n;
            }
            opus_extension_data[] all_extensions_data = total_ext_count == 0 ?
                Array.Empty<opus_extension_data>() :
                new opus_extension_data[total_ext_count];
            fixed (opus_extension_data* all_extensions = all_extensions_data)
            {
                /* copy over any extensions that were passed in */
                for (ext_count = 0; ext_count < nb_extensions; ext_count++)
                {
                    all_extensions[ext_count] = extensions[ext_count];
                }

                /* incorporate any extensions from the repacketizer padding */
                for (i = begin; i < end; i++)
                {
                    int j;
                    int frame_ext_count;
                    frame_ext_count = total_ext_count - ext_count;
                    int ret = opus_packet_extensions_parse(rp->paddings[i], rp->padding_len[i],
                       &all_extensions[ext_count], &frame_ext_count);
                    if (ret < 0)
                    {
                        return OPUS_INTERNAL_ERROR;
                    }
                    /* renumber the extension frame numbers */
                    for (j = 0; j < frame_ext_count; j++)
                    {
                        all_extensions[ext_count + j].frame += i - begin;
                    }
                    ext_count += frame_ext_count;
                }

                ptr = data;
                if (count == 1)
                {
                    /* Code 0 */
                    tot_size += len[0] + 1;
                    if (tot_size > maxlen)
                    {
                        return OPUS_BUFFER_TOO_SMALL;
                    }
                    *ptr++ = (byte)(rp->toc & 0xFC);
                }
                else if (count == 2)
                {
                    if (len[1] == len[0])
                    {
                        /* Code 1 */
                        tot_size += 2 * len[0] + 1;
                        if (tot_size > maxlen)
                        {
                            return OPUS_BUFFER_TOO_SMALL;
                        }
                        *ptr++ = (byte)((rp->toc & 0xFC) | 0x1);
                    }
                    else
                    {
                        /* Code 2 */
                        tot_size += len[0] + len[1] + 2 + BOOL2INT(len[0] >= 252);
                        if (tot_size > maxlen)
                        {
                            return OPUS_BUFFER_TOO_SMALL;
                        }
                        *ptr++ = (byte)((rp->toc & 0xFC) | 0x2);
                        ptr += encode_size(len[0], ptr);
                    }
                }
                if (count > 2 || (pad != 0 && tot_size < maxlen) || ext_count > 0)
                {
                    /* Code 3 */
                    int vbr;
                    int pad_amount = 0;

                    /* Restart the process for the padding case */
                    ptr = data;
                    if (self_delimited != 0)
                        tot_size = 1 + BOOL2INT(len[count - 1] >= 252);
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
                            tot_size += 1 + BOOL2INT(len[i] >= 252) + len[i];
                        tot_size += len[count - 1];

                        if (tot_size > maxlen)
                        {
                            return OPUS_BUFFER_TOO_SMALL;
                        }
                        *ptr++ = (byte)((rp->toc & 0xFC) | 0x3);
                        *ptr++ = (byte)(count | 0x80);
                    }
                    else
                    {
                        tot_size += count * len[0] + 2;
                        if (tot_size > maxlen)
                        {
                            return OPUS_BUFFER_TOO_SMALL;
                        }
                        *ptr++ = (byte)((rp->toc & 0xFC) | 0x3);
                        *ptr++ = (byte)(count);
                    }
                    pad_amount = pad != 0 ? (maxlen - tot_size) : 0;
                    if (ext_count > 0)
                    {
                        /* figure out how much space we need for the extensions */
                        ext_len = opus_packet_extensions_generate(null, maxlen - tot_size, all_extensions, ext_count, 0);
                        if (ext_len < 0) return ext_len;
                        if (pad == 0)
                            pad_amount = ext_len + ext_len / 254 + 1;
                    }
                    if (pad_amount != 0)
                    {
                        int nb_255s;
                        data[1] |= 0x40;
                        nb_255s = (pad_amount - 1) / 255;
                        if (tot_size + ext_len + nb_255s + 1 > maxlen)
                        {
                            return OPUS_BUFFER_TOO_SMALL;
                        }
                        ext_begin = tot_size + pad_amount - ext_len;
                        /* Prepend 0x01 padding */
                        ones_begin = tot_size + nb_255s + 1;
                        ones_end = tot_size + pad_amount - ext_len;
                        for (i = 0; i < nb_255s; i++)
                            *ptr++ = 255;
                        *ptr++ = (byte)(pad_amount - 255 * nb_255s - 1);
                        tot_size += pad_amount;
                    }
                    if (vbr != 0)
                    {
                        for (i = 0; i < count - 1; i++)
                            ptr += encode_size(len[i], ptr);
                    }
                }
                if (self_delimited != 0)
                {
                    int sdlen = encode_size(len[count - 1], ptr);
                    ptr += sdlen;
                }
                /* Copy the actual data */
                for (i = 0; i < count; i++)
                {
                    /* Using OPUS_MOVE() instead of OPUS_COPY() in case we're doing in-place
                       padding from opus_packet_pad or opus_packet_unpad(). */
                    /* assert disabled because it's not valid in C. */
                    /* celt_assert(frames[i] + len[i] <= data || ptr <= frames[i]); */
                    OPUS_MOVE(ptr, frames[i], len[i]);
                    ptr += len[i];
                }
                if (ext_len > 0)
                {
                    int ret = opus_packet_extensions_generate(&data[ext_begin], ext_len, all_extensions, ext_count, 0);
                    celt_assert(ret == ext_len);
                }
                for (i = ones_begin; i < ones_end; i++)
                    data[i] = 0x01;
                if (pad != 0 && ext_count == 0)
                {
                    /* Fill padding with zeros. */
                    while (ptr < data + maxlen)
                        *ptr++ = 0;
                }
                return tot_size;
            }
        }

        internal static unsafe int opus_repacketizer_out_range(OpusRepacketizer* rp, int begin, int end, byte* data, int maxlen)
        {
            return opus_repacketizer_out_range_impl(rp, begin, end, data, maxlen, 0, 0, null, 0);
        }

        internal static unsafe int opus_repacketizer_out(OpusRepacketizer* rp, byte* data, int maxlen)
        {
            return opus_repacketizer_out_range_impl(rp, 0, rp->nb_frames, data, maxlen, 0, 0, null, 0);
        }

        internal static unsafe int opus_packet_pad_impl(byte* data, int len, int new_len, int pad, in opus_extension_data* extensions, int nb_extensions)
        {
            OpusRepacketizer rp;
            int ret;
            if (len < 1)
                return OPUS_BAD_ARG;
            if (len == new_len)
                return OPUS_OK;
            else if (len > new_len)
                return OPUS_BAD_ARG;
            byte[] copy_data = new byte[len];
            fixed (byte* copy = copy_data)
            {
                opus_repacketizer_init(&rp);
                /* Moving payload to the end of the packet so we can do in-place padding */
                OPUS_COPY(copy, data, len);
                ret = opus_repacketizer_cat(&rp, copy, len);
                if (ret != OPUS_OK)
                    return ret;
                ret = opus_repacketizer_out_range_impl(&rp, 0, rp.nb_frames, data, new_len, 0, pad, extensions, nb_extensions);
                return ret;
            }
        }

        internal static unsafe int opus_packet_pad(byte* data, int len, int new_len)
        {
            int ret;
            ret = opus_packet_pad_impl(data, len, new_len, 1, null, 0);
            if (ret > 0)
                return OPUS_OK;
            else
                return ret;
        }

        internal static unsafe int opus_packet_unpad(byte* data, int len)
        {
            OpusRepacketizer rp;
            int ret;
            int i;
            if (len < 1)
                return OPUS_BAD_ARG;
            opus_repacketizer_init(&rp);
            ret = opus_repacketizer_cat(&rp, data, len);
            if (ret < 0)
                return ret;
            /* Discard all padding and extensions. */
            for (i = 0; i < rp.nb_frames; i++)
            {
                rp.padding_len[i] = 0;
                rp.paddings[i] = null;
            }
            ret = opus_repacketizer_out_range_impl(&rp, 0, rp.nb_frames, data, len, 0, 0, null, 0);
            celt_assert(ret > 0 && ret <= len);
            return ret;
        }

        internal static unsafe int opus_multistream_packet_pad(byte* data, int len, int new_len, int nb_streams)
        {
            int s;
            int count;
            byte toc;
            short* size = stackalloc short[48];
            int packet_offset;
            int amount;

            if (len < 1)
                return OPUS_BAD_ARG;
            if (len == new_len)
                return OPUS_OK;
            else if (len > new_len)
                return OPUS_BAD_ARG;
            amount = new_len - len;
            /* Seek to last stream */
            for (s = 0; s < nb_streams - 1; s++)
            {
                if (len <= 0)
                    return OPUS_INVALID_PACKET;
                count = opus_packet_parse_impl(data, len, 1, &toc, null,
                                               size, null, &packet_offset, null, null);
                if (count < 0)
                    return count;
                data += packet_offset;
                len -= packet_offset;
            }
            return opus_packet_pad(data, len, len + amount);
        }

        internal static unsafe int opus_multistream_packet_unpad(byte* data, int len, int nb_streams)
        {
            int s;
            byte toc;
            short* size = stackalloc short[48];
            int packet_offset;
            OpusRepacketizer rp;
            byte* dst;
            int dst_len;

            if (len < 1)
                return OPUS_BAD_ARG;
            dst = data;
            dst_len = 0;
            /* Unpad all frames */
            for (s = 0; s < nb_streams; s++)
            {
                int ret;
                int i;
                int self_delimited = BOOL2INT(s != nb_streams - 1);
                if (len <= 0)
                    return OPUS_INVALID_PACKET;
                opus_repacketizer_init(&rp);
                ret = opus_packet_parse_impl(data, len, self_delimited, &toc, null,
                                               size, null, &packet_offset, null, null);
                if (ret < 0)
                    return ret;
                ret = opus_repacketizer_cat_impl(&rp, data, packet_offset, self_delimited);
                if (ret < 0)
                    return ret;
                /* Discard all padding and extensions. */
                for (i = 0; i < rp.nb_frames; i++)
                {
                    rp.padding_len[i] = 0;
                    rp.paddings[i] = null;
                }
                ret = opus_repacketizer_out_range_impl(&rp, 0, rp.nb_frames, dst, len, self_delimited, 0, null, 0);
                if (ret < 0)
                    return ret;
                else
                    dst_len += ret;
                dst += ret;
                data += packet_offset;
                len -= packet_offset;
            }
            return dst_len;
        }
    }
}
