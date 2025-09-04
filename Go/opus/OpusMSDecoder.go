package opus

import "errors"

type OpusMSDecoder struct {
	layout   ChannelLayout
	decoders []*OpusDecoder
}

func newOpusMSDecoder(nb_streams int, nb_coupled_streams int) *OpusMSDecoder {
	decoders := make([]*OpusDecoder, nb_streams)
	for c := 0; c < nb_streams; c++ {
		decoders[c] = new(OpusDecoder)
	}
	return &OpusMSDecoder{
		layout:   ChannelLayout{},
		decoders: decoders,
	}
}

func (this *OpusMSDecoder) opus_multistream_decoder_init(Fs int, channels int, streams int, coupled_streams int, mapping []int16) int {
	if channels > 255 || channels < 1 || coupled_streams > streams || streams < 1 || coupled_streams < 0 || streams > 255-coupled_streams {
		return OpusError.OPUS_BAD_ARG
	}

	this.layout.nb_channels = channels
	this.layout.nb_streams = streams
	this.layout.nb_coupled_streams = coupled_streams

	for i := 0; i < this.layout.nb_channels; i++ {
		this.layout.mapping[i] = mapping[i]
	}
	if validate_layout(this.layout) == 0 {
		return OpusError.OPUS_BAD_ARG
	}

	decoder_ptr := 0
	for i := 0; i < this.layout.nb_coupled_streams; i++ {
		ret := this.decoders[decoder_ptr].opus_decoder_init(Fs, 2)
		if ret != OpusError.OPUS_OK {
			return ret
		}
		decoder_ptr++
	}
	for i := this.layout.nb_coupled_streams; i < this.layout.nb_streams; i++ {
		ret := this.decoders[decoder_ptr].opus_decoder_init(Fs, 1)
		if ret != OpusError.OPUS_OK {
			return ret
		}
		decoder_ptr++
	}
	return OpusError.OPUS_OK
}

func OpusMSDecoder_create(Fs int, channels int, streams int, coupled_streams int, mapping []int16) (*OpusMSDecoder, error) {
	if channels > 255 || channels < 1 || coupled_streams > streams || streams < 1 || coupled_streams < 0 || streams > 255-coupled_streams {
		return nil, errors.New("Invalid channel / stream configuration")
	}
	st := newOpusMSDecoder(streams, coupled_streams)
	ret := st.opus_multistream_decoder_init(Fs, channels, streams, coupled_streams, mapping)
	if ret != OpusError.OPUS_OK {
		if ret == OpusError.OPUS_BAD_ARG {
			return nil, errors.New("Bad argument while creating MS decoder")
		}
		return nil, errors.New("Could not create MS decoder")
	}
	return st, nil
}

func opus_multistream_packet_validate(data []byte, data_ptr int, len int, nb_streams int, Fs int) int {
	toc := BoxedValueByte{Val: 0}
	size := make([]int16, 48)
	samples := 0
	packet_offset := BoxedValueInt{Val: 0}
	dummy := BoxedValueInt{Val: 0}

	for s := 0; s < nb_streams; s++ {
		if len <= 0 {
			return OpusError.OPUS_INVALID_PACKET
		}

		count := opus_packet_parse_impl(data, data_ptr, len, boolToInt(s != nb_streams-1), &toc, nil, 0,
			size, 0, &dummy, &packet_offset)
		if count < 0 {
			return count
		}

		tmp_samples := GetNumSamples(data, data_ptr, packet_offset.Val, Fs)
		if s != 0 && samples != tmp_samples {
			return OpusError.OPUS_INVALID_PACKET
		}
		samples = tmp_samples
		data_ptr += packet_offset.Val
		len -= packet_offset.Val
	}
	return samples
}

func (this *OpusMSDecoder) opus_multistream_decode_native(data []byte, data_ptr int, len int, pcm []int16, pcm_ptr int, frame_size int, decode_fec int, soft_clip int) int {
	Fs := this.getSampleRate()
	frame_size = IMIN(frame_size, Fs/25*3)
	buf := make([]int16, 2*frame_size)
	decoder_ptr := 0
	do_plc := 0

	if len == 0 {
		do_plc = 1
	}
	if len < 0 {
		return OpusError.OPUS_BAD_ARG
	}
	if do_plc == 0 && len < 2*this.layout.nb_streams-1 {
		return OpusError.OPUS_INVALID_PACKET
	}
	if do_plc == 0 {
		ret := opus_multistream_packet_validate(data, data_ptr, len, this.layout.nb_streams, Fs)
		if ret < 0 {
			return ret
		} else if ret > frame_size {
			return OpusError.OPUS_BUFFER_TOO_SMALL
		}
	}

	for s := 0; s < this.layout.nb_streams; s++ {
		dec := this.decoders[decoder_ptr]
		decoder_ptr++

		if do_plc == 0 && len <= 0 {
			return OpusError.OPUS_INTERNAL_ERROR
		}

		packet_offset := BoxedValueInt{Val: 0}
		ret := dec.opus_decode_native(data, data_ptr, len, buf, 0, frame_size, decode_fec, boolToInt(s != this.layout.nb_streams-1), &packet_offset, soft_clip)
		data_ptr += packet_offset.Val
		len -= packet_offset.Val
		if ret <= 0 {
			return ret
		}
		frame_size = ret

		if s < this.layout.nb_coupled_streams {
			prev := -1
			for {
				_chan := get_left_channel(this.layout, s, prev)
				if _chan == -1 {
					break
				}
				opus_copy_channel_out_short(pcm, pcm_ptr, this.layout.nb_channels, _chan, buf, 0, 2, frame_size)
				prev = _chan
			}
			prev = -1
			for {
				_chan := get_right_channel(this.layout, s, prev)
				if _chan == -1 {
					break
				}
				opus_copy_channel_out_short(pcm, pcm_ptr, this.layout.nb_channels, _chan, buf, 1, 2, frame_size)
				prev = _chan
			}
		} else {
			prev := -1
			for {
				_chan := get_mono_channel(this.layout, s, prev)
				if _chan == -1 {
					break
				}
				opus_copy_channel_out_short(pcm, pcm_ptr, this.layout.nb_channels, _chan, buf, 0, 1, frame_size)
				prev = _chan
			}
		}
	}

	for c := 0; c < this.layout.nb_channels; c++ {
		if this.layout.mapping[c] == 255 {
			opus_copy_channel_out_short(pcm, pcm_ptr, this.layout.nb_channels, c, nil, 0, 0, frame_size)
		}
	}
	return frame_size
}

func opus_copy_channel_out_short(dst []int16, dst_ptr int, dst_stride int, dst_channel int, src []int16, src_ptr int, src_stride int, frame_size int) {
	if src != nil {
		for i := 0; i < frame_size; i++ {
			dst[i*dst_stride+dst_channel+dst_ptr] = src[i*src_stride+src_ptr]
		}
	} else {
		for i := 0; i < frame_size; i++ {
			dst[i*dst_stride+dst_channel+dst_ptr] = 0
		}
	}
}

func (this *OpusMSDecoder) decodeMultistream(data []byte, data_offset int, len int, out_pcm []int16, out_pcm_offset int, frame_size int, decode_fec int) int {
	return this.opus_multistream_decode_native(data, data_offset, len, out_pcm, out_pcm_offset, frame_size, decode_fec, 0)
}

func (this *OpusMSDecoder) getBandwidth() int {
	if this.decoders == nil || len(this.decoders) == 0 {
		panic("Decoder not initialized")
	}
	return this.decoders[0].GetBandwidth()
}

func (this *OpusMSDecoder) getSampleRate() int {
	if this.decoders == nil || len(this.decoders) == 0 {
		panic("Decoder not initialized")
	}
	return this.decoders[0].GetSampleRate()
}

func (this *OpusMSDecoder) getGain() int {
	if this.decoders == nil || len(this.decoders) == 0 {
		panic("Decoder not initialized")
	}
	return this.decoders[0].GetGain()
}

func (this *OpusMSDecoder) setGain(value int) {
	for s := 0; s < this.layout.nb_streams; s++ {
		this.decoders[s].SetGain(value)
	}
}

func (this *OpusMSDecoder) getLastPacketDuration() int {
	if this.decoders == nil || len(this.decoders) == 0 {
		return OpusError.OPUS_INVALID_STATE
	}
	return this.decoders[0].GetLastPacketDuration()
}

func (this *OpusMSDecoder) getFinalRange() int {
	value := 0
	for s := 0; s < this.layout.nb_streams; s++ {
		value ^= this.decoders[s].GetFinalRange()
	}
	return value
}

func (this *OpusMSDecoder) ResetState() {
	for s := 0; s < this.layout.nb_streams; s++ {
		this.decoders[s].ResetState()
	}
}

func (this *OpusMSDecoder) GetMultistreamDecoderState(streamId int) *OpusDecoder {
	return this.decoders[streamId]
}

func boolToInt(b bool) int {
	if b {
		return 1
	}
	return 0
}
