package opus

import (
	"bytes"

	"github.com/lostromb/concentus/go/comm"
)

type OpusRepacketizer struct {
	toc       byte
	nb_frames int
	frames    [48][]byte
	len       [48]int16
	framesize int
}

func (this *OpusRepacketizer) Reset() {
	this.nb_frames = 0
}

func NewOpusRepacketizer() *OpusRepacketizer {
	rp := &OpusRepacketizer{}
	rp.Reset()
	return rp
}

func (this *OpusRepacketizer) opus_repacketizer_cat_impl(data []byte, data_ptr int, len_val int, self_delimited int) int {
	dummy_toc := comm.BoxedValueByte{0}
	dummy_offset := comm.BoxedValueInt{0}
	if len_val < 1 {
		return OpusError.OPUS_INVALID_PACKET
	}

	if this.nb_frames == 0 {
		this.toc = (data[data_ptr])
		this.framesize = getNumSamplesPerFrame(data, data_ptr, 8000)
	} else if (this.toc & 0xFC) != (data[data_ptr] & 0xFC) {
		return OpusError.OPUS_INVALID_PACKET
	}

	curr_nb_frames := getNumFrames(data, data_ptr, len_val)
	if curr_nb_frames < 1 {
		return OpusError.OPUS_INVALID_PACKET
	}

	if (curr_nb_frames+this.nb_frames)*this.framesize > 960 {
		return OpusError.OPUS_INVALID_PACKET
	}

	//ret := opus_packet_parse_impl(data, data_ptr, len_val, self_delimited, &dummy_toc, this.frames[:], this.nb_frames, this.len[:], this.nb_frames, dummy_offset, dummy_offset)
	ret := opus_packet_parse_impl(data, data_ptr, len_val, self_delimited, &dummy_toc, this.frames[:], this.nb_frames, this.len[:], this.nb_frames, &dummy_offset, &dummy_offset)

	if ret < 1 {
		return ret
	}

	this.nb_frames += curr_nb_frames
	return OpusError.OPUS_OK
}

func (this *OpusRepacketizer) addPacket(data []byte, data_offset int, len_val int) int {
	return this.opus_repacketizer_cat_impl(data, data_offset, len_val, 0)
}

func (this *OpusRepacketizer) getNumFrames() int {
	return this.nb_frames
}

func (this *OpusRepacketizer) opus_repacketizer_out_range_impl(begin int, end int, data []byte, data_ptr int, maxlen int, self_delimited int, pad int) int {
	var i, count int
	var tot_size int
	var ptr int

	if begin < 0 || begin >= end || end > this.nb_frames {
		/*fprintf(stderr, "%d %d %d\n", begin, end, rp.nb_frames);*/
		return OpusError.OPUS_BAD_ARG
	}
	count = end - begin

	if self_delimited != 0 {
		tot_size = 1 + boolToInt(this.len[count-1] >= 252)
	} else {
		tot_size = 0
	}

	ptr = data_ptr
	if count == 1 {
		/* Code 0 */
		tot_size += int(this.len[0]) + 1
		if tot_size > maxlen {
			return OpusError.OPUS_BUFFER_TOO_SMALL
		}
		data[ptr] = (byte)(this.toc & 0xFC)
		ptr++
	} else if count == 2 {
		if this.len[1] == this.len[0] {
			/* Code 1 */
			tot_size += 2*int(this.len[0]) + 1
			if tot_size > maxlen {
				return OpusError.OPUS_BUFFER_TOO_SMALL
			}
			data[ptr] = (byte)((this.toc & 0xFC) | 0x1)
			ptr++
		} else {
			/* Code 2 */
			tot_size += int(this.len[0]) + int(this.len[1]) + 2 + boolToInt(this.len[0] >= 252)
			if tot_size > maxlen {
				return OpusError.OPUS_BUFFER_TOO_SMALL
			}
			data[ptr] = (byte)((this.toc & 0xFC) | 0x2)
			ptr++
			ptr += encode_size(int(this.len[0]), data, ptr)
		}
	}
	if count > 2 || (pad != 0 && tot_size < maxlen) {
		/* Code 3 */
		var vbr int
		var pad_amount = 0

		/* Restart the process for the padding case */
		ptr = data_ptr
		if self_delimited != 0 {
			tot_size = 1 + boolToInt(this.len[count-1] >= 252)
		} else {
			tot_size = 0
		}
		vbr = 0
		for i = 1; i < count; i++ {
			if this.len[i] != this.len[0] {
				vbr = 1
				break
			}
		}
		if vbr != 0 {
			tot_size += 2
			for i = 0; i < count-1; i++ {
				tot_size += 1 + boolToInt(this.len[i] >= 252) + int(this.len[i])
			}
			tot_size += int(this.len[count-1])

			if tot_size > maxlen {
				return OpusError.OPUS_BUFFER_TOO_SMALL
			}
			data[ptr] = (byte)((this.toc & 0xFC) | 0x3)
			ptr++
			data[ptr] = (byte)(count | 0x80)
			ptr++
		} else {
			tot_size += count*int(this.len[0]) + 2
			if tot_size > maxlen {
				return OpusError.OPUS_BUFFER_TOO_SMALL
			}
			data[ptr] = (byte)((this.toc & 0xFC) | 0x3)
			ptr++
			data[ptr] = (byte)(count)
			ptr++
		}
		if pad != 0 {
			pad_amount = (maxlen - tot_size)
		} else {
			pad_amount = 0
		}
		// pad_amount = pad != 0 ? (maxlen - tot_size) : 0;

		if pad_amount != 0 {
			var nb_255s int
			data[data_ptr+1] = (byte)(data[data_ptr+1] | 0x40)
			nb_255s = (pad_amount - 1) / 255
			for i = 0; i < nb_255s; i++ {
				data[ptr] = 255
				ptr++
			}

			data[ptr] = (byte)(pad_amount - 255*nb_255s - 1)
			ptr++
			tot_size += pad_amount
		}

		if vbr != 0 {
			for i = 0; i < count-1; i++ {
				ptr += (encode_size(int(this.len[i]), data, ptr))
			}
		}
	}

	if self_delimited != 0 {
		sdlen := encode_size(int(this.len[count-1]), data, ptr)
		ptr += (sdlen)
	}

	/* Copy the actual data */
	for i = begin; i < count+begin; i++ {

		if bytes.Equal(data, this.frames[i]) {
			/* Using OPUS_MOVE() instead of OPUS_COPY() in case we're doing in-place
			   padding from opus_packet_pad or opus_packet_unpad(). */
			MemMove(data, 0, ptr, int(this.len[i]))
		} else {
			//System.arraycopy(this.frames[i], 0, data, ptr, this.len[i])
			copy(data[ptr:], this.frames[i][0:this.len[i]])
		}
		ptr += int(this.len[i])
	}

	if pad != 0 {
		/* Fill padding with zeros. */
		MemSetWithOffset(data, 0, ptr, data_ptr+maxlen-ptr)
	}

	return tot_size
}

func (this *OpusRepacketizer) createPacket(begin int, end int, data []byte, data_offset int, maxlen int) int {
	return this.opus_repacketizer_out_range_impl(begin, end, data, data_offset, maxlen, 0, 0)
}

func (this *OpusRepacketizer) createPacketOut(data []byte, data_offset int, maxlen int) int {
	return this.opus_repacketizer_out_range_impl(0, this.nb_frames, data, data_offset, maxlen, 0, 0)
}

func PadPacket(data []byte, data_offset int, len_val int, new_len int) int {
	if len_val < 1 {
		return OpusError.OPUS_BAD_ARG
	}
	if len_val == new_len {
		return OpusError.OPUS_OK
	} else if len_val > new_len {
		return OpusError.OPUS_BAD_ARG
	}

	rp := NewOpusRepacketizer()
	copy(data[data_offset+new_len-len_val:], data[data_offset:data_offset+len_val])
	rp.addPacket(data, data_offset+new_len-len_val, len_val)
	ret := rp.opus_repacketizer_out_range_impl(0, rp.nb_frames, data, data_offset, new_len, 0, 1)
	if ret > 0 {
		return OpusError.OPUS_OK
	}
	return ret
}

func UnpadPacket(data []byte, data_offset int, len_val int) int {
	if len_val < 1 {
		return OpusError.OPUS_BAD_ARG
	}

	rp := NewOpusRepacketizer()
	ret := rp.addPacket(data, data_offset, len_val)
	if ret < 0 {
		return ret
	}
	ret = rp.opus_repacketizer_out_range_impl(0, rp.nb_frames, data, data_offset, len_val, 0, 0)
	return ret
}

func PadMultistreamPacket(data []byte, data_offset int, len_val int, new_len int, nb_streams int) int {
	if len_val < 1 {
		return OpusError.OPUS_BAD_ARG
	}
	if len_val == new_len {
		return OpusError.OPUS_OK
	} else if len_val > new_len {
		return OpusError.OPUS_BAD_ARG
	}

	amount := new_len - len_val
	dummy_toc := comm.BoxedValueByte{0}
	size := []int16{}
	packet_offset := comm.BoxedValueInt{0}
	dummy_offset := comm.BoxedValueInt{0}

	for s := 0; s < nb_streams-1; s++ {
		if len_val <= 0 {
			return OpusError.OPUS_INVALID_PACKET
		}
		count := opus_packet_parse_impl(data, data_offset, len_val, 1, &dummy_toc, nil, 0, size, 0, &dummy_offset, &packet_offset)
		if count < 0 {
			return count
		}
		data_offset += int(packet_offset.Val)
		len_val -= int(packet_offset.Val)
	}
	return PadPacket(data, data_offset, len_val, len_val+amount)
}

func UnpadMultistreamPacket(data []byte, data_offset int, len_val int, nb_streams int) int {
	if len_val < 1 {
		return OpusError.OPUS_BAD_ARG
	}

	dst := data_offset
	dst_len := 0
	dummy_toc := comm.BoxedValueByte{0}
	size := []int16{}
	packet_offset := comm.BoxedValueInt{0}
	dummy_offset := comm.BoxedValueInt{0}

	for s := 0; s < nb_streams; s++ {
		self_delimited := 0
		if s != nb_streams-1 {
			self_delimited = 1
		}
		if len_val <= 0 {
			return OpusError.OPUS_INVALID_PACKET
		}
		rp := NewOpusRepacketizer()
		count := opus_packet_parse_impl(data, data_offset, len_val, self_delimited, &dummy_toc, nil, 0, size, 0, &dummy_offset, &packet_offset)
		if count < 0 {
			return count
		}
		ret := rp.opus_repacketizer_cat_impl(data, data_offset, int(packet_offset.Val), self_delimited)
		if ret < 0 {
			return ret
		}
		ret = rp.opus_repacketizer_out_range_impl(0, rp.nb_frames, data, dst, len_val, self_delimited, 0)
		if ret < 0 {
			return ret
		}
		dst_len += ret
		dst += ret
		data_offset += int(packet_offset.Val)
		len_val -= int(packet_offset.Val)
	}
	return dst_len
}

func getNumSamplesPerFrame(packet []byte, packet_offset int, Fs int) int {
	var audiosize int
	if (packet[packet_offset] & 0x80) != 0 {
		audiosize = int((packet[packet_offset] >> 3) & 0x3)
		audiosize = (Fs << audiosize) / 400
	} else if (packet[packet_offset] & 0x60) == 0x60 {
		if (packet[packet_offset] & 0x08) != 0 {
			audiosize = Fs / 50
		} else {
			audiosize = Fs / 100
		}

	} else {
		audiosize = int((packet[packet_offset] >> 3) & 0x3)
		if audiosize == 3 {
			audiosize = Fs * 60 / 1000
		} else {
			audiosize = (Fs << audiosize) / 100
		}
	}
	return audiosize
}

func getNumFrames(data []byte, data_ptr int, len_val int) int {
	var count int
	if len_val < 1 {
		return OpusError.OPUS_BAD_ARG
	}
	count = int(data[data_ptr] & 0x3)
	if count == 0 {
		return 1
	} else if count != 3 {
		return 2
	} else if len_val < 2 {
		return OpusError.OPUS_INVALID_PACKET
	} else {
		return int(data[data_ptr+1] & 0x3F)
	}
}
