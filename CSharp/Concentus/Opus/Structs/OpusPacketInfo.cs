using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Opus.Structs
{
    public class OpusPacketInfo
    {
        public readonly byte TOC;
        public readonly IList<byte[]> Frames;
        public readonly int PayloadOffset;

        private OpusPacketInfo(byte toc, IList<byte[]> frames, int payloadOffset)
        {
            TOC = toc;
            Frames = frames;
            PayloadOffset = payloadOffset;
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
        public static OpusPacketInfo ParseOpusPacket(Pointer<byte> data, int len)
        {
            BoxedValue<int> payload_offset = new BoxedValue<int>();
            BoxedValue<byte> out_toc = new BoxedValue<byte>();
            Pointer<Pointer<byte>> frames = Pointer.Malloc<Pointer<byte>>(3);
            Pointer<short> size = Pointer.Malloc<short>(3);
            BoxedValue<int> packetOffset = new BoxedValue<int>();
            int error = OpusPacket.opus_packet_parse_impl(data, len, 0, out_toc, frames, size, payload_offset, packetOffset);
            if (error != OpusError.OPUS_OK)
            {
                throw new OpusException("An error occurred while parsing the packet");
            }

            return new OpusPacketInfo(out_toc.Val, null, payload_offset.Val);
        }
    }
}
