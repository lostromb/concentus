package opus

import (
	"errors"

	"github.com/dosgo/concentus/go/comm"
)

type OpusPacketInfo struct {
	TOCByte       byte
	Frames        [][]byte
	PayloadOffset int
}

func NewOpusPacketInfo(toc byte, frames [][]byte, payloadOffset int) *OpusPacketInfo {
	return &OpusPacketInfo{
		TOCByte:       toc,
		Frames:        frames,
		PayloadOffset: payloadOffset,
	}
}

func ParseOpusPacket(packet []byte, packet_offset, _len int) (*OpusPacketInfo, error) {
	numFrames := GetNumFrames(packet, packet_offset, _len)
	if numFrames < 0 {
		return nil, errors.New("opus_packet_parse_impl failed")
	}

	var out_toc = comm.BoxedValueByte{0}
	var payload_offset = comm.BoxedValueInt{0}

	frames := make([][]byte, numFrames)
	sizes := make([]int16, numFrames)
	var packet_offset_out = comm.BoxedValueInt{0}
	errCode := opus_packet_parse_impl(packet, packet_offset, _len, 0, &out_toc, frames, 0, sizes, 0, &payload_offset, &packet_offset_out)
	if errCode < 0 {
		return nil, errors.New("opus_packet_parse_impl failed")
	}

	copiedFrames := make([][]byte, len(frames))
	for i := range frames {
		copiedFrames[i] = make([]byte, len(frames[i]))
		copy(copiedFrames[i], frames[i])
	}

	return NewOpusPacketInfo(byte(out_toc.Val), copiedFrames, payload_offset.Val), nil
}

func GetNumSamplesPerFrame(packet []byte, packet_offset, Fs int) int {
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

func GetNumEncodedChannels(packet []byte, packet_offset int) int {
	if (packet[packet_offset] & 0x4) != 0 {
		return 2
	}
	return 1
}

func GetNumFrames(packet []byte, packet_offset, len int) int {
	if len < 1 {
		return OpusError.OPUS_BAD_ARG
	}
	count := packet[packet_offset] & 0x3
	if count == 0 {
		return 1
	} else if count != 3 {
		return 2
	} else if len < 2 {
		return OpusError.OPUS_INVALID_PACKET
	} else {
		return int(packet[packet_offset+1] & 0x3F)
	}
}

func GetNumSamples(packet []byte, packet_offset, len, Fs int) int {
	count := GetNumFrames(packet, packet_offset, len)
	if count < 0 {
		return count
	}

	samples := count * GetNumSamplesPerFrame(packet, packet_offset, Fs)
	if samples*25 > Fs*3 {
		return OpusError.OPUS_INVALID_PACKET
	}
	return samples
}

func GetNumSamplesDecoder(dec *OpusDecoder, packet []byte, packet_offset, len int) int {
	return GetNumSamples(packet, packet_offset, len, dec.Fs)
}

func GetEncoderMode(packet []byte, packet_offset int) int {
	if (packet[packet_offset] & 0x80) != 0 {
		return MODE_CELT_ONLY
	} else if (packet[packet_offset] & 0x60) == 0x60 {
		return MODE_HYBRID
	}
	return MODE_SILK_ONLY
}

func GetBandwidth(packet []byte, packet_offset int) int {
	var bandwidth int
	if (packet[packet_offset] & 0x80) != 0 {
		bandwidth = OpusBandwidthHelpers_GetBandwidth(OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_MEDIUMBAND) + (int(packet[packet_offset]>>5) & 0x3))
		if bandwidth == OPUS_BANDWIDTH_MEDIUMBAND {
			bandwidth = OPUS_BANDWIDTH_NARROWBAND
		}
	} else if (packet[packet_offset] & 0x60) == 0x60 {
		bandwidth = OPUS_BANDWIDTH_SUPERWIDEBAND
		if (packet[packet_offset] & 0x10) != 0 {
			bandwidth = OPUS_BANDWIDTH_FULLBAND
		}

	} else {
		bandwidth = OpusBandwidthHelpers_GetBandwidth(OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_NARROWBAND) + (int(packet[packet_offset]>>5) & 0x3))
	}
	return bandwidth
}
func encode_size(size int, data []byte, data_ptr int) int {
	if size < 252 {
		data[data_ptr] = byte(size)
		return 1
	} else {
		dp1 := 252 + (size & 0x3)
		data[data_ptr] = byte(dp1)
		data[data_ptr+1] = byte((size - dp1) >> 2)
		return 2
	}
}

func parse_size(data []byte, data_ptr, len int, size *comm.BoxedValueShort) int {
	if len < 1 {
		size.Val = -1
		return -1
	} else if int(data[data_ptr]) < 252 {
		size.Val = int16(data[data_ptr])
		return 1
	} else if len < 2 {
		size.Val = -1
		return -1
	} else {
		size.Val = int16(4*int(data[data_ptr+1]) + int(data[data_ptr]))
		return 2
	}
}
func opus_packet_parse_impl(data []byte, data_ptr, len_val, self_delimited int, out_toc *comm.BoxedValueByte,
	frames [][]byte, frames_ptr int, sizes []int16, sizes_ptr int,
	payload_offset, packet_offset *comm.BoxedValueInt) int {
	var i, bytes int
	var count int
	var cbr int
	var toc int8
	var ch int
	var framesize int
	var last_size int
	var pad = 0
	var data0 = data_ptr
	out_toc.Val = 0
	payload_offset.Val = 0
	packet_offset.Val = 0

	if sizes == nil || len_val < 0 {
		return OpusError.OPUS_BAD_ARG
	}
	if len_val == 0 {
		return OpusError.OPUS_INVALID_PACKET
	}

	framesize = getNumSamplesPerFrame(data, data_ptr, 48000)
	cbr = 0

	toc = int8(data[data_ptr])
	data_ptr++
	len_val--
	last_size = len_val
	switch toc & 0x3 {
	/* One frame */
	case 0:
		count = 1
		break
	/* Two CBR frames */
	case 1:
		count = 2
		cbr = 1
		if self_delimited == 0 {
			if (len_val & 0x1) != 0 {
				return OpusError.OPUS_INVALID_PACKET
			}
			last_size = len_val / 2
			/* If last_size doesn't fit in size[0], we'll catch it later */
			sizes[sizes_ptr] = int16(last_size)
		}
		break
	/* Two VBR frames */
	case 2:
		count = 2
		boxed_size := &comm.BoxedValueShort{sizes[sizes_ptr]}
		bytes = parse_size(data, data_ptr, len_val, boxed_size)
		sizes[sizes_ptr] = boxed_size.Val
		len_val -= bytes
		if sizes[sizes_ptr] < 0 || int(sizes[sizes_ptr]) > len_val {
			return OpusError.OPUS_INVALID_PACKET
		}
		data_ptr += bytes
		last_size = len_val - int(sizes[sizes_ptr])
		break
	/* Multiple CBR/VBR frames (from 0 to 120 ms) */
	default:
		/*case 3:*/
		if len_val < 1 {
			return OpusError.OPUS_INVALID_PACKET
		}
		/* Number of frames encoded in bits 0 to 5 */

		ch = inlines.SignedByteToUnsignedInt(int8(data[data_ptr]))
		data_ptr++
		count = ch & 0x3F

		if count <= 0 || framesize*count > 5760 {
			return OpusError.OPUS_INVALID_PACKET
		}
		len_val--
		/* Padding flag is bit 6 */
		if (ch & 0x40) != 0 {
			var p int
			for {
				var tmp int
				if len_val <= 0 {
					return OpusError.OPUS_INVALID_PACKET
				}
				p = inlines.SignedByteToUnsignedInt(int8(data[data_ptr]))
				data_ptr++
				len_val--
				if p == 255 {
					tmp = 264
				} else {
					tmp = p
				}
				// tmp = p == 255 ? 254 : p;
				len_val -= tmp
				pad += tmp
				if p == 255 {
					continue
				}
				break
			}
		}
		if len_val < 0 {
			return OpusError.OPUS_INVALID_PACKET
		}
		/* VBR flag is bit 7 */
		if (ch & 0x80) != 0 {
			cbr = 0
		} else {
			cbr = 1
		}
		// cbr = (ch & 0x80) != 0 ? 0 : 1;
		if cbr == 0 {
			/* VBR case */
			last_size = len_val
			for i = 0; i < count-1; i++ {
				boxed_size := &comm.BoxedValueShort{sizes[sizes_ptr+i]}
				bytes = parse_size(data, data_ptr, len_val, boxed_size)
				sizes[sizes_ptr+i] = boxed_size.Val
				len_val -= bytes
				if sizes[sizes_ptr+i] < 0 || int(sizes[sizes_ptr+i]) > len_val {
					return OpusError.OPUS_INVALID_PACKET
				}
				data_ptr += bytes
				last_size -= bytes + int(sizes[sizes_ptr+i])
			}
			if last_size < 0 {
				return OpusError.OPUS_INVALID_PACKET
			}
		} else if self_delimited == 0 {
			/* CBR case */
			last_size = len_val / count
			if last_size*count != len_val {
				return OpusError.OPUS_INVALID_PACKET
			}
			for i = 0; i < count-1; i++ {
				sizes[sizes_ptr+i] = int16(last_size)
			}
		}
		break
	}

	/* Self-delimited framing has an extra size for the last frame. */
	if self_delimited != 0 {
		boxed_size := &comm.BoxedValueShort{sizes[sizes_ptr+count-1]}
		bytes = parse_size(data, data_ptr, len_val, boxed_size)
		sizes[sizes_ptr+count-1] = boxed_size.Val
		len_val -= bytes
		if sizes[sizes_ptr+count-1] < 0 || int(sizes[sizes_ptr+count-1]) > len_val {
			return OpusError.OPUS_INVALID_PACKET
		}
		data_ptr += bytes
		/* For CBR packets, apply the size to all the frames. */
		if cbr != 0 {
			if int(sizes[sizes_ptr+count-1])*count > len_val {
				return OpusError.OPUS_INVALID_PACKET
			}
			for i = 0; i < count-1; i++ {
				sizes[sizes_ptr+i] = sizes[sizes_ptr+count-1]
			}
		} else if bytes+int(sizes[sizes_ptr+count-1]) > last_size {
			return OpusError.OPUS_INVALID_PACKET
		}
	} else {
		/* Because it's not encoded explicitly, it's possible the size of the
		   last packet (or all the packets, for the CBR case) is larger than
		   1275. Reject them here.*/
		if last_size > 1275 {
			return OpusError.OPUS_INVALID_PACKET
		}
		sizes[sizes_ptr+count-1] = int16(last_size)
	}

	payload_offset.Val = (int)(data_ptr - data0)

	for i = 0; i < count; i++ {
		if frames != nil {
			// The old code returned pointers to the single data array, but that can cause unwanted side effects.
			// So I have replaced it with this code that creates a new copy of each frame. Slower, but more robust
			frames[frames_ptr+i] = make([]byte, len(data)-data_ptr)
			//System.arraycopy(data, data_ptr, frames[frames_ptr+i], 0, data.length-data_ptr)
			copy(frames[frames_ptr+i][0:], data[data_ptr:data_ptr+len(data)-data_ptr])
		}
		data_ptr += int(sizes[sizes_ptr+i])
	}

	packet_offset.Val = pad + (int)(data_ptr-data0)

	out_toc.Val = toc

	return count
}
