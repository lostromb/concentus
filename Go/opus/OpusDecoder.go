package opus

import (
	"errors"

	"github.com/dosgo/concentus/go/celt"
	"github.com/dosgo/concentus/go/comm"
	"github.com/dosgo/concentus/go/silk"
)

type OpusDecoder struct {
	channels             int
	Fs                   int
	DecControl           DecControlState
	decode_gain          int
	stream_channels      int
	bandwidth            int
	mode                 int
	prev_mode            int
	frame_size           int
	prev_redundancy      int
	last_packet_duration int
	rangeFinal           int
	SilkDecoder          silk.SilkDecoder
	Celt_Decoder         celt.CeltDecoder
}

func (this *OpusDecoder) reset() {
	this.channels = 0
	this.Fs = 0
	this.DecControl.Reset()
	this.decode_gain = 0
	this.partialReset()
}

func (this *OpusDecoder) partialReset() {
	this.stream_channels = 0
	this.bandwidth = OPUS_BANDWIDTH_UNKNOWN
	this.mode = MODE_UNKNOWN
	this.prev_mode = MODE_UNKNOWN
	this.frame_size = 0
	this.prev_redundancy = 0
	this.last_packet_duration = 0
	this.rangeFinal = 0
}

func (this *OpusDecoder) opus_decoder_init(Fs int, channels int) int {
	var ret int

	if (Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000) ||
		(channels != 1 && channels != 2) {
		return OpusError.OPUS_BAD_ARG
	}
	this.reset()

	silk_dec := &this.SilkDecoder
	celt_dec := &this.Celt_Decoder
	this.stream_channels = channels
	this.channels = channels

	this.Fs = Fs
	this.DecControl.API_sampleRate = this.Fs
	this.DecControl.nChannelsAPI = this.channels

	ret = silk_InitDecoder(silk_dec)
	if ret != 0 {
		return OpusError.OPUS_INTERNAL_ERROR
	}

	ret = celt_dec.Celt_decoder_init(Fs, channels)
	if ret != OpusError.OPUS_OK {
		return OpusError.OPUS_INTERNAL_ERROR
	}

	celt_dec.SetSignalling(0)

	this.prev_mode = MODE_UNKNOWN
	this.frame_size = Fs / 400
	return OpusError.OPUS_OK
}

func NewOpusDecoder(Fs int, channels int) (*OpusDecoder, error) {
	this := &OpusDecoder{}
	var ret int
	if Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000 {
		return nil, errors.New("Sample rate is invalid (must be 8/12/16/24/48 Khz)")
	}
	if channels != 1 && channels != 2 {
		return nil, errors.New("Number of channels must be 1 or 2")
	}
	this.SilkDecoder = silk.NewSilkDecoder()
	this.Celt_Decoder = celt.CeltDecoder{}

	ret = this.opus_decoder_init(Fs, channels)
	if ret != OpusError.OPUS_OK {
		if ret == OpusError.OPUS_BAD_ARG {
			return nil, errors.New("OPUS_BAD_ARG when creating decoder")
		}
		return nil, errors.New("eeee")
	}
	return this, nil
}

var SILENCE = []byte{0xFF, 0xFF}

func (this *OpusDecoder) opus_decode_frame(data []byte, data_ptr int, len int, pcm []int16, pcm_ptr int, frame_size int, decode_fec int) int {

	var i, silk_ret, celt_ret int
	dec := comm.EntropyCoder{}
	var silk_frame_size int
	var pcm_silk []int16
	var pcm_transition_silk []int16
	var pcm_transition_celt []int16
	var pcm_transition []int16
	var redundant_audio []int16

	var audiosize int
	var mode int
	transition := 0
	start_band := 0
	redundancy := 0
	redundancy_bytes := 0
	celt_to_silk := 0
	var c int
	F20 := this.Fs / 50
	F10 := F20 >> 1
	F5 := F10 >> 1
	F2_5 := F5 >> 1
	if frame_size < F2_5 {
		return OpusError.OPUS_BUFFER_TOO_SMALL
	}
	frame_size = inlines.IMIN(frame_size, this.Fs/25*3)
	if len <= 1 {
		data = nil
		frame_size = inlines.IMIN(frame_size, this.frame_size)
	}
	if data != nil {
		audiosize = this.frame_size
		mode = this.mode
		dec.Dec_init(data, data_ptr, len)
	} else {
		audiosize = frame_size
		mode = this.prev_mode

		if mode == MODE_UNKNOWN {
			for i = pcm_ptr; i < pcm_ptr+(audiosize*this.channels); i++ {
				pcm[i] = 0
			}
			return audiosize
		}

		if audiosize > F20 {
			for audiosize > 0 {

				ret := this.opus_decode_frame(nil, 0, 0, pcm, pcm_ptr, inlines.IMIN(audiosize, F20), 0)
				if ret < 0 {
					return ret
				}
				pcm_ptr += ret * this.channels
				audiosize -= ret
			}
			return frame_size
		} else if audiosize < F20 {
			if audiosize > F10 {
				audiosize = F10
			} else if mode != MODE_SILK_ONLY && audiosize > F5 && audiosize < F10 {
				audiosize = F5
			}
		}
	}
	celt_accum := 0
	if mode != MODE_CELT_ONLY && frame_size >= F10 {
		celt_accum = 1
	}

	pcm_transition_silk_size := 0
	pcm_transition_celt_size := 0
	if data != nil && (this.prev_mode != MODE_UNKNOWN && this.prev_mode != MODE_AUTO) &&
		((mode == MODE_CELT_ONLY && this.prev_mode != MODE_CELT_ONLY && this.prev_redundancy == 0) ||
			(mode != MODE_CELT_ONLY && this.prev_mode == MODE_CELT_ONLY)) {
		transition = 1
		if mode == MODE_CELT_ONLY {
			pcm_transition_celt_size = F5 * this.channels
		} else {
			pcm_transition_silk_size = F5 * this.channels
		}
	}
	pcm_transition_celt = make([]int16, pcm_transition_celt_size)
	if transition != 0 && mode == MODE_CELT_ONLY {
		pcm_transition = pcm_transition_celt
		this.opus_decode_frame(nil, 0, 0, pcm_transition, 0, inlines.IMIN(F5, audiosize), 0)
	}
	if audiosize > frame_size {
		return OpusError.OPUS_BAD_ARG
	} else {
		frame_size = audiosize
	}

	pcm_silk_size := 0
	if mode != MODE_CELT_ONLY && celt_accum == 0 {
		pcm_silk_size = inlines.IMAX(F10, frame_size) * this.channels
	}
	pcm_silk = make([]int16, pcm_silk_size)

	if mode != MODE_CELT_ONLY {
		var lost_flag, decoded_samples int
		var pcm_ptr2 []int16
		var pcm_ptr2_ptr = 0

		if celt_accum != 0 {
			pcm_ptr2 = pcm
			pcm_ptr2_ptr = pcm_ptr
		} else {
			pcm_ptr2 = pcm_silk
			pcm_ptr2_ptr = 0
		}

		if this.prev_mode == MODE_CELT_ONLY {
			silk_InitDecoder(&this.SilkDecoder)
		}

		/* The SILK PLC cannot produce frames of less than 10 ms */
		this.DecControl.payloadSize_ms = inlines.IMAX(10, 1000*audiosize/this.Fs)

		if data != nil {
			this.DecControl.nChannelsInternal = this.stream_channels
			if mode == MODE_SILK_ONLY {
				if this.bandwidth == OPUS_BANDWIDTH_NARROWBAND {
					this.DecControl.internalSampleRate = 8000
				} else if this.bandwidth == OPUS_BANDWIDTH_MEDIUMBAND {
					this.DecControl.internalSampleRate = 12000
				} else if this.bandwidth == OPUS_BANDWIDTH_WIDEBAND {
					this.DecControl.internalSampleRate = 16000
				} else {
					this.DecControl.internalSampleRate = 16000
					inlines.OpusAssert(false)
				}
			} else {
				/* Hybrid mode */
				this.DecControl.internalSampleRate = 16000
			}
		}

		lost_flag = 2 * decode_fec
		if data == nil {
			lost_flag = 1
		}
		decoded_samples = 0
		for {
			/* Call SILK decoder */
			first_frame := boolToInt(decoded_samples == 0)
			boxed_silk_frame_size := &comm.BoxedValueInt{0}
			silk_ret = silk_Decode(&this.SilkDecoder, &this.DecControl,
				lost_flag, first_frame, &dec, pcm_ptr2, pcm_ptr2_ptr, boxed_silk_frame_size)
			silk_frame_size = boxed_silk_frame_size.Val

			if silk_ret != 0 {
				if lost_flag != 0 {
					/* PLC failure should not be fatal */
					silk_frame_size = frame_size
					MemSetWithOffset(pcm_ptr2, 0, pcm_ptr2_ptr, frame_size*this.channels)
				} else {

					return OpusError.OPUS_INTERNAL_ERROR
				}
			}
			pcm_ptr2_ptr += (silk_frame_size * this.channels)
			decoded_samples += silk_frame_size
			if decoded_samples < frame_size {
				continue
			}
			break

		}
	}

	start_band = 0
	if decode_fec == 0 && mode != MODE_CELT_ONLY && data != nil &&
		dec.Tell()+17+20*boolToInt(this.mode == MODE_HYBRID) <= 8*len {
		if mode == MODE_HYBRID {
			redundancy = dec.Dec_bit_logp(12)
		} else {
			redundancy = 1
		}
		if redundancy != 0 {
			celt_to_silk = dec.Dec_bit_logp(1)
			if mode == MODE_HYBRID {
				redundancy_bytes = int(dec.Dec_uint(256)) + 2
			} else {
				redundancy_bytes = len - ((dec.Tell() + 7) >> 3)
			}
			len -= redundancy_bytes
			if len*8 < dec.Tell() {
				len = 0
				redundancy_bytes = 0
				redundancy = 0
			}
			dec.Storage = dec.Storage - redundancy_bytes
		}
	}

	if mode != MODE_CELT_ONLY {
		start_band = 17
	}

	endband := 21
	switch this.bandwidth {
	case OPUS_BANDWIDTH_NARROWBAND:
		endband = 13
	case OPUS_BANDWIDTH_MEDIUMBAND, OPUS_BANDWIDTH_WIDEBAND:
		endband = 17
	case OPUS_BANDWIDTH_SUPERWIDEBAND:
		endband = 19
	case OPUS_BANDWIDTH_FULLBAND:
		endband = 21
	}
	this.Celt_Decoder.SetEndBand(endband)
	this.Celt_Decoder.SetChannels(this.stream_channels)

	if redundancy != 0 {
		transition = 0
		pcm_transition_silk_size = 0
	}

	pcm_transition_silk = make([]int16, pcm_transition_silk_size)

	if transition != 0 && mode != MODE_CELT_ONLY {
		pcm_transition = pcm_transition_silk
		this.opus_decode_frame(nil, 0, 0, pcm_transition, 0, inlines.IMIN(F5, audiosize), 0)
	}

	redundant_audio_size := 0
	if redundancy != 0 {
		redundant_audio_size = F5 * this.channels
	}
	redundant_audio = make([]int16, redundant_audio_size)

	if redundancy != 0 && celt_to_silk != 0 {
		this.Celt_Decoder.SetStartBand(0)
		this.Celt_Decoder.Celt_decode_with_ec(data, data_ptr+len, redundancy_bytes, redundant_audio, 0, F5, nil, 0)
		redundant_rng := this.Celt_Decoder.GetFinalRange()
		_ = redundant_rng
	}

	this.Celt_Decoder.SetStartBand(start_band)
	if mode != MODE_SILK_ONLY {

		celt_frame_size := inlines.IMIN(F20, frame_size)
		if mode != this.prev_mode && (this.prev_mode != MODE_AUTO && this.prev_mode != MODE_UNKNOWN) && this.prev_redundancy == 0 {
			this.Celt_Decoder.ResetState()
		}
		decode_data := data
		if decode_fec != 0 {
			decode_data = nil
		}
		celt_ret = this.Celt_Decoder.Celt_decode_with_ec(decode_data, data_ptr, len, pcm, pcm_ptr, celt_frame_size, &dec, celt_accum)
	} else {
		if celt_accum == 0 {
			for i = pcm_ptr; i < pcm_ptr+(frame_size*this.channels); i++ {
				pcm[i] = 0
			}
		}
		if this.prev_mode == MODE_HYBRID && !(redundancy != 0 && celt_to_silk != 0 && this.prev_redundancy != 0) {
			this.Celt_Decoder.SetStartBand(0)
			this.Celt_Decoder.Celt_decode_with_ec(SILENCE, 0, 2, pcm, pcm_ptr, F2_5, nil, celt_accum)
		}
	}

	if mode != MODE_CELT_ONLY && celt_accum == 0 {
		for i = 0; i < frame_size*this.channels; i++ {
			pcm[pcm_ptr+i] = inlines.SAT16(int(pcm[pcm_ptr+i]) + int(pcm_silk[i]))
		}
	}
	window := this.Celt_Decoder.GetMode().Window
	var redundant_rng = 0
	if redundancy != 0 && celt_to_silk == 0 {
		this.Celt_Decoder.ResetState()
		this.Celt_Decoder.SetStartBand(0)

		this.Celt_Decoder.Celt_decode_with_ec(data, data_ptr+len, redundancy_bytes, redundant_audio, 0, F5, nil, 0)
		redundant_rng = this.Celt_Decoder.GetFinalRange()

		smooth_fade(pcm, pcm_ptr+this.channels*(frame_size-F2_5), redundant_audio, this.channels*F2_5,
			pcm, (pcm_ptr + this.channels*(frame_size-F2_5)), F2_5, this.channels, window, this.Fs)
	}

	if redundancy != 0 && celt_to_silk != 0 {
		for c = 0; c < this.channels; c++ {
			for i = 0; i < F2_5; i++ {
				pcm[this.channels*i+c+pcm_ptr] = redundant_audio[this.channels*i+c]
			}
		}

		smooth_fade(redundant_audio, this.channels*F2_5, pcm, pcm_ptr+this.channels*F2_5,
			pcm, pcm_ptr+this.channels*F2_5, F2_5, this.channels, window, this.Fs)
	}
	if transition != 0 {

		if audiosize >= F5 {
			for i = 0; i < this.channels*F2_5; i++ {
				pcm[pcm_ptr+i] = pcm_transition[i]
			}
			smooth_fade(pcm_transition, this.channels*F2_5, pcm, pcm_ptr+this.channels*F2_5,
				pcm, pcm_ptr+this.channels*F2_5, F2_5, this.channels, window, this.Fs)
		} else {
			smooth_fade(pcm_transition, 0, pcm, pcm_ptr,
				pcm, pcm_ptr, F2_5, this.channels, window, this.Fs)
		}
	}

	if this.decode_gain != 0 {

		gain := inlines.Celt_exp2(int(inlines.MULT16_16_P15(inlines.QCONST16(6.48814081e-4, 25), int16(this.decode_gain))))
		for i = pcm_ptr; i < pcm_ptr+(frame_size*this.channels); i++ {
			x := inlines.MULT16_32_P16(pcm[i], gain)
			pcm[i] = int16(inlines.SATURATE(x, 32767))
		}
	}
	if len <= 1 {
		this.rangeFinal = 0
	} else {
		this.rangeFinal = int(dec.Rng) ^ redundant_rng
	}

	this.prev_mode = mode
	if redundancy != 0 && celt_to_silk == 0 {
		this.prev_redundancy = 1
	} else {
		this.prev_redundancy = 0
	}

	if celt_ret < 0 {
		return celt_ret
	}
	return audiosize
}

func (this *OpusDecoder) opus_decode_native(data []byte, data_ptr int, len int, pcm_out []int16, pcm_out_ptr int, frame_size int, decode_fec int, self_delimited int, packet_offset *comm.BoxedValueInt, soft_clip int) int {
	var i, nb_samples int
	var count, offset int
	var packet_frame_size, packet_stream_channels int
	packet_offset.Val = 0
	var packet_bandwidth int
	var packet_mode int
	size := make([]int16, 48)
	if decode_fec < 0 || decode_fec > 1 {
		return OpusError.OPUS_BAD_ARG
	}
	if (decode_fec != 0 || len == 0 || data == nil) && frame_size%(this.Fs/400) != 0 {
		return OpusError.OPUS_BAD_ARG
	}
	if len == 0 || data == nil {
		pcm_count := 0
		for pcm_count < frame_size {
			ret := this.opus_decode_frame(nil, 0, 0, pcm_out, pcm_out_ptr+(pcm_count*this.channels), frame_size-pcm_count, 0)
			if ret < 0 {
				return ret
			}
			pcm_count += ret
		}
		inlines.OpusAssert(pcm_count == frame_size)
		this.last_packet_duration = pcm_count
		return pcm_count
	} else if len < 0 {
		return OpusError.OPUS_BAD_ARG
	}

	packet_mode = GetEncoderMode(data, data_ptr)
	packet_bandwidth = GetBandwidth(data, data_ptr)
	packet_frame_size = getNumSamplesPerFrame(data, data_ptr, this.Fs)

	packet_stream_channels = GetNumEncodedChannels(data, data_ptr)

	var toc comm.BoxedValueByte = comm.BoxedValueByte{0}
	boxed_offset := comm.BoxedValueInt{0}
	//count = opus_packet_parse_impl(data, data_ptr, len, self_delimited, &toc, nil, 0, size, 0, offset, packet_offset)
	count = opus_packet_parse_impl(data, data_ptr, len, self_delimited, &toc, nil, 0,
		size, 0, &boxed_offset, packet_offset)
	offset = boxed_offset.Val
	if count < 0 {
		return count
	}

	data_ptr += offset

	if decode_fec != 0 {

		dummy := comm.BoxedValueInt{0}
		duration_copy := this.last_packet_duration
		var ret int
		if frame_size < packet_frame_size || packet_mode == MODE_CELT_ONLY || this.mode == MODE_CELT_ONLY {
			return this.opus_decode_native(nil, 0, 0, pcm_out, pcm_out_ptr, frame_size, 0, 0, &dummy, soft_clip)
		}
		if frame_size-packet_frame_size != 0 {
			ret = this.opus_decode_native(nil, 0, 0, pcm_out, pcm_out_ptr, frame_size-packet_frame_size, 0, 0, &dummy, soft_clip)
			if ret < 0 {
				this.last_packet_duration = duration_copy
				return ret
			}
			inlines.OpusAssert(ret == frame_size-packet_frame_size)
		}
		this.mode = packet_mode
		this.bandwidth = packet_bandwidth

		this.frame_size = packet_frame_size
		this.stream_channels = packet_stream_channels
		ret = this.opus_decode_frame(data, data_ptr, int(size[0]), pcm_out, pcm_out_ptr+(this.channels*(frame_size-packet_frame_size)), packet_frame_size, 1)
		if ret < 0 {
			return ret
		} else {
			this.last_packet_duration = frame_size
			return frame_size
		}
	}

	if count*packet_frame_size > frame_size {
		return OpusError.OPUS_BUFFER_TOO_SMALL
	}

	this.mode = packet_mode
	this.bandwidth = packet_bandwidth
	this.frame_size = packet_frame_size
	this.stream_channels = packet_stream_channels

	nb_samples = 0
	for i = 0; i < count; i++ {
		ret := this.opus_decode_frame(data, data_ptr, int(size[i]), pcm_out, pcm_out_ptr+(nb_samples*this.channels), frame_size-nb_samples, 0)
		if ret < 0 {
			return ret
		}
		inlines.OpusAssert(ret == packet_frame_size)
		data_ptr += int(size[i])
		nb_samples += ret
	}
	this.last_packet_duration = nb_samples

	return nb_samples
}

func (this *OpusDecoder) Decode(in_data []byte, in_data_offset int, len int, out_pcm []int16, out_pcm_offset int, frame_size int, decode_fec bool) (int, error) {
	if frame_size <= 0 {
		return 0, errors.New("Frame size must be > 0")
	}

	dummy := comm.BoxedValueInt{0}
	decode_fec_int := 0
	if decode_fec {
		decode_fec_int = 1
	}
	ret := this.opus_decode_native(in_data, in_data_offset, len, out_pcm, out_pcm_offset, frame_size, decode_fec_int, 0, &dummy, 0)

	if ret < 0 {
		if ret == OpusError.OPUS_BAD_ARG {
			return 0, errors.New("OPUS_BAD_ARG while decoding")
		}
		return 0, errors.New("An error occurred during decoding")
	}

	return ret, nil
}

func (this *OpusDecoder) DecodeBytes(in_data []byte, in_data_offset int, len int, out_pcm []byte, out_pcm_offset int, frame_size int, decode_fec bool) (int, error) {
	maxSamples := inlines.IMIN(frame_size, 5760)
	spcm := make([]int16, maxSamples*this.channels)
	decSamples, err := this.Decode(in_data, in_data_offset, len, spcm, 0, frame_size, decode_fec)
	if err != nil {
		return 0, err
	}
	idx := out_pcm_offset
	for _, s := range spcm[:decSamples*this.channels] {
		out_pcm[idx] = byte(s)
		out_pcm[idx+1] = byte(s >> 8)
		idx += 2
	}
	return decSamples, nil
}

func (this *OpusDecoder) GetBandwidth() int {
	return this.bandwidth
}

func (this *OpusDecoder) GetFinalRange() int {
	return this.rangeFinal
}

func (this *OpusDecoder) GetSampleRate() int {
	return this.Fs
}

func (this *OpusDecoder) GetPitch() int {
	if this.prev_mode == MODE_CELT_ONLY {
		return this.Celt_Decoder.GetPitch()
	} else {
		return this.DecControl.prevPitchLag
	}
}

func (this *OpusDecoder) GetGain() int {
	return this.decode_gain
}

func (this *OpusDecoder) SetGain(value int) error {
	if value < -32768 || value > 32767 {
		return errors.New("Gain must be within the range of a signed int16")
	}
	this.decode_gain = value
	return nil
}

func (this *OpusDecoder) GetLastPacketDuration() int {
	return this.last_packet_duration
}

func (this *OpusDecoder) ResetState() {
	this.partialReset()
	this.Celt_Decoder.ResetState()
	silk_InitDecoder(&this.SilkDecoder)
	this.stream_channels = this.channels
	this.frame_size = this.Fs / 400
}
