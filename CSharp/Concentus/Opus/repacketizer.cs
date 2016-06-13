using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Opus.Enums;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus
{
    /** @defgroup opus_repacketizer Repacketizer
  * @{
  *
  * The repacketizer can be used to merge multiple Opus packets into a single
  * packet or alternatively to split Opus packets that have previously been
  * merged. Splitting valid Opus packets is always guaranteed to succeed,
  * whereas merging valid packets only succeeds if all frames have the same
  * mode, bandwidth, and frame size, and when the total duration of the merged
  * packet is no more than 120 ms. The 120 ms limit comes from the
  * specification and limits decoder memory requirements at a point where
  * framing overhead becomes negligible.
  *
  * The repacketizer currently only operates on elementary Opus
  * streams. It will not manipualte multistream packets successfully, except in
  * the degenerate case where they consist of data from a single stream.
  *
  * The repacketizing process starts with creating a repacketizer state, either
  * by calling opus_repacketizer_create() or by allocating the memory yourself,
  * e.g.,
  * @code
  * OpusRepacketizer *rp;
  * rp = (OpusRepacketizer*)malloc(opus_repacketizer_get_size());
  * if (rp != NULL)
  *     opus_repacketizer_init(rp);
  * @endcode
  *
  * Then the application should submit packets with opus_repacketizer_cat(),
  * extract new packets with opus_repacketizer_out() or
  * opus_repacketizer_out_range(), and then reset the state for the next set of
  * input packets via opus_repacketizer_init().
  *
  * An alternate way of merging packets is to simply call opus_repacketizer_cat()
  * unconditionally until it fails. At that point, the merged packet can be
  * obtained with opus_repacketizer_out() and the input packet for which
  * opus_repacketizer_cat() needs to be re-added to a newly reinitialized
  * repacketizer state.
  */
    public static class repacketizer
    {
        /** (Re)initializes a previously allocated repacketizer state.
  * The state must be at least the size returned by opus_repacketizer_get_size().
  * This can be used for applications which use their own allocator instead of
  * malloc().
  * It must also be called to reset the queue of packets waiting to be
  * repacketized, which is necessary if the maximum packet duration of 120 ms
  * is reached or if you wish to submit packets with a different Opus
  * configuration (coding mode, audio bandwidth, frame size, or channel count).
  * Failure to do so will prevent a new packet from being added with
  * opus_repacketizer_cat().
  * @see opus_repacketizer_create
  * @see opus_repacketizer_get_size
  * @see opus_repacketizer_cat
  * @param rp <tt>OpusRepacketizer*</tt>: The repacketizer state to
  *                                       (re)initialize.
  * @returns A pointer to the same repacketizer state that was passed in.
  */
        public static OpusRepacketizer opus_repacketizer_init(OpusRepacketizer rp)
        {
            rp.nb_frames = 0;
            return rp;
        }

        /** Allocates memory and initializes the new repacketizer with
 * opus_repacketizer_init().
  */
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

        /** Add a packet to the current repacketizer state.
  * This packet must match the configuration of any packets already submitted
  * for repacketization since the last call to opus_repacketizer_init().
  * This means that it must have the same coding mode, audio bandwidth, frame
  * size, and channel count.
  * This can be checked in advance by examining the top 6 bits of the first
  * byte of the packet, and ensuring they match the top 6 bits of the first
  * byte of any previously submitted packet.
  * The total duration of audio in the repacketizer state also must not exceed
  * 120 ms, the maximum duration of a single packet, after adding this packet.
  *
  * The contents of the current repacketizer state can be extracted into new
  * packets using opus_repacketizer_out() or opus_repacketizer_out_range().
  *
  * In order to add a packet with a different configuration or to add more
  * audio beyond 120 ms, you must clear the repacketizer state by calling
  * opus_repacketizer_init().
  * If a packet is too large to add to the current repacketizer state, no part
  * of it is added, even if it contains multiple frames, some of which might
  * fit.
  * If you wish to be able to add parts of such packets, you should first use
  * another repacketizer to split the packet into pieces and add them
  * individually.
  * @see opus_repacketizer_out_range
  * @see opus_repacketizer_out
  * @see opus_repacketizer_init
  * @param rp <tt>OpusRepacketizer*</tt>: The repacketizer state to which to
  *                                       add the packet.
  * @param[in] data <tt>const unsigned char*</tt>: The packet data.
  *                                                The application must ensure
  *                                                this pointer remains valid
  *                                                until the next call to
  *                                                opus_repacketizer_init() or
  *                                                opus_repacketizer_destroy().
  * @param len <tt>opus_int32</tt>: The number of bytes in the packet data.
  * @returns An error code indicating whether or not the operation succeeded.
  * @retval #OPUS_OK The packet's contents have been added to the repacketizer
  *                  state.
  * @retval #OPUS_INVALID_PACKET The packet did not have a valid TOC sequence,
  *                              the packet's TOC sequence was not compatible
  *                              with previously submitted packets (because
  *                              the coding mode, audio bandwidth, frame size,
  *                              or channel count did not match), or adding
  *                              this packet would increase the total amount of
  *                              audio stored in the repacketizer state to more
  *                              than 120 ms.
  */
        public static int opus_repacketizer_cat(OpusRepacketizer rp, Pointer<byte> data, int len)
        {
            return opus_repacketizer_cat_impl(rp, data, len, 0);
        }

        /** Return the total number of frames contained in packet data submitted to
  * the repacketizer state so far via opus_repacketizer_cat() since the last
  * call to opus_repacketizer_init() or opus_repacketizer_create().
  * This defines the valid range of packets that can be extracted with
  * opus_repacketizer_out_range() or opus_repacketizer_out().
  * @param rp <tt>OpusRepacketizer*</tt>: The repacketizer state containing the
  *                                       frames.
  * @returns The total number of frames contained in the packet data submitted
  *          to the repacketizer state.
  */
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
                //Inlines.OpusAssert(frames[i].Offset + len[i] <= data.Offset || ptr.Offset <= frames[i].Offset);
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

        /** Construct a new packet from data previously submitted to the repacketizer
  * state via opus_repacketizer_cat().
  * @param rp <tt>OpusRepacketizer*</tt>: The repacketizer state from which to
  *                                       construct the new packet.
  * @param begin <tt>int</tt>: The index of the first frame in the current
  *                            repacketizer state to include in the output.
  * @param end <tt>int</tt>: One past the index of the last frame in the
  *                          current repacketizer state to include in the
  *                          output.
  * @param[out] data <tt>const unsigned char*</tt>: The buffer in which to
  *                                                 store the output packet.
  * @param maxlen <tt>opus_int32</tt>: The maximum number of bytes to store in
  *                                    the output buffer. In order to guarantee
  *                                    success, this should be at least
  *                                    <code>1276</code> for a single frame,
  *                                    or for multiple frames,
  *                                    <code>1277*(end-begin)</code>.
  *                                    However, <code>1*(end-begin)</code> plus
  *                                    the size of all packet data submitted to
  *                                    the repacketizer since the last call to
  *                                    opus_repacketizer_init() or
  *                                    opus_repacketizer_create() is also
  *                                    sufficient, and possibly much smaller.
  * @returns The total size of the output packet on success, or an error code
  *          on failure.
  * @retval #OPUS_BAD_ARG <code>[begin,end)</code> was an invalid range of
  *                       frames (begin < 0, begin >= end, or end >
  *                       opus_repacketizer_get_nb_frames()).
  * @retval #OPUS_BUFFER_TOO_SMALL \a maxlen was insufficient to contain the
  *                                complete output packet.
  */
        public static int opus_repacketizer_out_range(OpusRepacketizer rp, int begin, int end, Pointer<byte> data, int maxlen)
        {
            return opus_repacketizer_out_range_impl(rp, begin, end, data, maxlen, 0, 0);
        }

        /** Construct a new packet from data previously submitted to the repacketizer
  * state via opus_repacketizer_cat().
  * This is a convenience routine that returns all the data submitted so far
  * in a single packet.
  * It is equivalent to calling
  * @code
  * opus_repacketizer_out_range(rp, 0, opus_repacketizer_get_nb_frames(rp),
  *                             data, maxlen)
  * @endcode
  * @param rp <tt>OpusRepacketizer*</tt>: The repacketizer state from which to
  *                                       construct the new packet.
  * @param[out] data <tt>const unsigned char*</tt>: The buffer in which to
  *                                                 store the output packet.
  * @param maxlen <tt>opus_int32</tt>: The maximum number of bytes to store in
  *                                    the output buffer. In order to guarantee
  *                                    success, this should be at least
  *                                    <code>1277*opus_repacketizer_get_nb_frames(rp)</code>.
  *                                    However,
  *                                    <code>1*opus_repacketizer_get_nb_frames(rp)</code>
  *                                    plus the size of all packet data
  *                                    submitted to the repacketizer since the
  *                                    last call to opus_repacketizer_init() or
  *                                    opus_repacketizer_create() is also
  *                                    sufficient, and possibly much smaller.
  * @returns The total size of the output packet on success, or an error code
  *          on failure.
  * @retval #OPUS_BUFFER_TOO_SMALL \a maxlen was insufficient to contain the
  *                                complete output packet.
  */
        public static int opus_repacketizer_out(OpusRepacketizer rp, Pointer<byte> data, int maxlen)
        {
            return opus_repacketizer_out_range_impl(rp, 0, rp.nb_frames, data, maxlen, 0, 0);
        }

        /** Pads a given Opus packet to a larger size (possibly changing the TOC sequence).
  * @param[in,out] data <tt>const unsigned char*</tt>: The buffer containing the
  *                                                   packet to pad.
  * @param len <tt>opus_int32</tt>: The size of the packet.
  *                                 This must be at least 1.
  * @param new_len <tt>opus_int32</tt>: The desired size of the packet after padding.
  *                                 This must be at least as large as len.
  * @returns an error code
  * @retval #OPUS_OK \a on success.
  * @retval #OPUS_BAD_ARG \a len was less than 1 or new_len was less than len.
  * @retval #OPUS_INVALID_PACKET \a data did not contain a valid Opus packet.
  */
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

        /** Remove all padding from a given Opus packet and rewrite the TOC sequence to
  * minimize space usage.
  * @param[in,out] data <tt>const unsigned char*</tt>: The buffer containing the
  *                                                   packet to strip.
  * @param len <tt>opus_int32</tt>: The size of the packet.
  *                                 This must be at least 1.
  * @returns The new size of the output packet on success, or an error code
  *          on failure.
  * @retval #OPUS_BAD_ARG \a len was less than 1.
  * @retval #OPUS_INVALID_PACKET \a data did not contain a valid Opus packet.
  */
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
            Inlines.OpusAssert(ret > 0 && ret <= len);

            return ret;
        }

        /** Pads a given Opus multi-stream packet to a larger size (possibly changing the TOC sequence).
  * @param[in,out] data <tt>const unsigned char*</tt>: The buffer containing the
  *                                                   packet to pad.
  * @param len <tt>opus_int32</tt>: The size of the packet.
  *                                 This must be at least 1.
  * @param new_len <tt>opus_int32</tt>: The desired size of the packet after padding.
  *                                 This must be at least 1.
  * @param nb_streams <tt>opus_int32</tt>: The number of streams (not channels) in the packet.
  *                                 This must be at least as large as len.
  * @returns an error code
  * @retval #OPUS_OK \a on success.
  * @retval #OPUS_BAD_ARG \a len was less than 1.
  * @retval #OPUS_INVALID_PACKET \a data did not contain a valid Opus packet.
  */
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

        /** Remove all padding from a given Opus multi-stream packet and rewrite the TOC sequence to
  * minimize space usage.
  * @param[in,out] data <tt>const unsigned char*</tt>: The buffer containing the
  *                                                   packet to strip.
  * @param len <tt>opus_int32</tt>: The size of the packet.
  *                                 This must be at least 1.
  * @param nb_streams <tt>opus_int32</tt>: The number of streams (not channels) in the packet.
  *                                 This must be at least 1.
  * @returns The new size of the output packet on success, or an error code
  *          on failure.
  * @retval #OPUS_BAD_ARG \a len was less than 1 or new_len was less than len.
  * @retval #OPUS_INVALID_PACKET \a data did not contain a valid Opus packet.
  */
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
