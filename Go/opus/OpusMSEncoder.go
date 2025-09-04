package opus

import (
	"errors"
)

type OpusMSEncoder struct {
	layout            ChannelLayout
	lfe_stream        int
	application       OpusApplication
	variable_duration OpusFramesize
	surround          int
	bitrate_bps       int
	subframe_mem      [3]float32
	encoders          []*OpusEncoder
	window_mem        []int
	preemph_mem       []int
}

func NewOpusMSEncoder(nb_streams, nb_coupled_streams int) (*OpusMSEncoder, error) {
	if nb_streams < 1 || nb_coupled_streams > nb_streams || nb_coupled_streams < 0 {
		return nil, errors.New("Invalid channel count in MS encoder")
	}

	st := &OpusMSEncoder{
		encoders: make([]*OpusEncoder, nb_streams),
	}
	for c := 0; c < nb_streams; c++ {
		st.encoders[c] = &OpusEncoder{}
	}

	nb_channels := nb_coupled_streams*2 + (nb_streams - nb_coupled_streams)
	st.window_mem = make([]int, nb_channels*120)
	st.preemph_mem = make([]int, nb_channels)
	return st, nil
}

func (st *OpusMSEncoder) ResetState() {
	st.subframe_mem[0], st.subframe_mem[1], st.subframe_mem[2] = 0, 0, 0
	if st.surround != 0 {
		for i := range st.preemph_mem {
			st.preemph_mem[i] = 0
		}
		for i := range st.window_mem {
			st.window_mem[i] = 0
		}
	}
	encoder_ptr := 0
	for s := 0; s < st.layout.nb_streams; s++ {
		enc := st.encoders[encoder_ptr]
		encoder_ptr++
		enc.ResetState()
	}
}

func validate_encoder_layout(layout ChannelLayout) int {
	for s := 0; s < layout.nb_streams; s++ {
		if s < layout.nb_coupled_streams {
			if get_left_channel(layout, s, -1) == -1 {
				return 0
			}
			if get_right_channel(layout, s, -1) == -1 {
				return 0
			}
		} else if get_mono_channel(layout, s, -1) == -1 {
			return 0
		}
	}
	return 1
}

func channel_pos(channels int, pos *[8]int) {
	if channels == 4 {
		pos[0], pos[1], pos[2], pos[3] = 1, 3, 1, 3
	} else if channels == 3 || channels == 5 || channels == 6 {
		pos[0], pos[1], pos[2], pos[3], pos[4], pos[5] = 1, 2, 3, 1, 3, 0
	} else if channels == 7 {
		pos[0], pos[1], pos[2], pos[3], pos[4], pos[5], pos[6] = 1, 2, 3, 1, 3, 2, 0
	} else if channels == 8 {
		pos[0], pos[1], pos[2], pos[3], pos[4], pos[5], pos[6], pos[7] = 1, 2, 3, 1, 3, 1, 3, 0
	}
}

var diff_table = [17]int{
	int(0.5 + 0.5000000*float32(int(1)<<CeltConstants.DB_SHIFT)),
	int(0.5 + 0.2924813*float32(int(1)<<CeltConstants.DB_SHIFT)),
	int(0.5 + 0.1609640*float32(int(1)<<CeltConstants.DB_SHIFT)),
	int(0.5 + 0.0849625*float32(int(1)<<CeltConstants.DB_SHIFT)),
	int(0.5 + 0.0437314*float32(int(1)<<CeltConstants.DB_SHIFT)),
	int(0.5 + 0.0221971*float32(int(1)<<CeltConstants.DB_SHIFT)),
	int(0.5 + 0.0111839*float32(int(1)<<CeltConstants.DB_SHIFT)),
	int(0.5 + 0.0056136*float32(int(1)<<CeltConstants.DB_SHIFT)),
	int(0.5 + 0.0028123*float32(int(1)<<CeltConstants.DB_SHIFT)),
	0, 0, 0, 0, 0, 0, 0, 0,
}

func logSum(a, b int) int {
	var max, diff int
	if a > b {
		max = a
		diff = SUB32(EXTEND32Int(a), EXTEND32Int(b))
	} else {
		max = b
		diff = SUB32(EXTEND32Int(b), EXTEND32Int(a))
	}
	if diff >= int(QCONST16(8.0, CeltConstants.DB_SHIFT)) {
		return max
	}
	low := SHR32(diff, CeltConstants.DB_SHIFT-1)
	frac := SHL16Int(diff-SHL16Int(low, CeltConstants.DB_SHIFT-1), 16-CeltConstants.DB_SHIFT)
	return max + diff_table[low] + MULT16_16_Q15Int(frac, SUB16Int(diff_table[low+1], diff_table[low]))
}

func surround_analysis(celt_mode *CeltMode, pcm []int16, pcm_ptr int, bandLogE []int, mem, preemph_mem []int, len, overlap, channels, rate int) {
	var pos [8]int
	upsample := resampling_factor(rate)
	frame_size := len * upsample

	LM := 0
	for ; LM < celt_mode.maxLM; LM++ {
		if celt_mode.shortMdctSize<<LM == frame_size {
			break
		}
	}

	input := make([]int, frame_size+overlap)
	x := make([]int16, len)
	freq := make([][]int, 1)
	freq[0] = make([]int, frame_size)

	channel_pos(channels, &pos)

	maskLogE := make([][]int, 3)
	for i := range maskLogE {
		maskLogE[i] = make([]int, 21)
		for j := range maskLogE[i] {
			maskLogE[i][j] = -int(QCONST16(28.0, CeltConstants.DB_SHIFT))
		}
	}

	for c := 0; c < channels; c++ {
		copy(input[:overlap], mem[c*overlap:(c*overlap)+overlap])
		opus_copy_channel_in_short(x, 0, 1, pcm, pcm_ptr, channels, c, len)

		boxed_preemph := BoxedValueInt{preemph_mem[c]}
		//celt_preemphasis(x, input, overlap, frame_size, 1, upsample, celt_mode.preemph, &boxed_preemph, 0)
		//celt_preemphasis(x, input, overlap, frame_size, 1, upsample, celt_mode.preemph, boxed_preemph, 0)
		celt_preemphasis1(x, input, overlap, frame_size, 1, upsample, celt_mode.preemph, &boxed_preemph, 0)
		preemph_mem[c] = boxed_preemph.Val

		clt_mdct_forward(celt_mode.mdct, input, 0, freq[0], 0, celt_mode.window, overlap, celt_mode.maxLM-LM, 1)
		if upsample != 1 {
			bound := len
			for i := 0; i < bound; i++ {
				freq[0][i] *= int(upsample)
			}
			for i := bound; i < frame_size; i++ {
				freq[0][i] = 0
			}
		}

		bandE := make([][]int, 1)
		bandE[0] = make([]int, 21)
		compute_band_energies(celt_mode, freq, bandE, 21, 1, LM)
		amp2Log2Ptr(celt_mode, 21, 21, bandE[0], bandLogE, 21*c, 1)

		for i := 1; i < 21; i++ {
			bandLogE[21*c+i] = MAX16Int(bandLogE[21*c+i], bandLogE[21*c+i-1]-int(QCONST16(1.0, CeltConstants.DB_SHIFT)))
		}
		for i := 19; i >= 0; i-- {
			bandLogE[21*c+i] = MAX16Int(bandLogE[21*c+i], bandLogE[21*c+i+1]-int(QCONST16(2.0, CeltConstants.DB_SHIFT)))
		}
		if pos[c] == 1 {
			for i := 0; i < 21; i++ {
				maskLogE[0][i] = logSum(maskLogE[0][i], bandLogE[21*c+i])
			}
		} else if pos[c] == 3 {
			for i := 0; i < 21; i++ {
				maskLogE[2][i] = logSum(maskLogE[2][i], bandLogE[21*c+i])
			}
		} else if pos[c] == 2 {
			for i := 0; i < 21; i++ {
				maskLogE[0][i] = logSum(maskLogE[0][i], bandLogE[21*c+i]-int(QCONST16(0.5, CeltConstants.DB_SHIFT)))
				maskLogE[2][i] = logSum(maskLogE[2][i], bandLogE[21*c+i]-int(QCONST16(0.5, CeltConstants.DB_SHIFT)))
			}
		}
		copy(mem[c*overlap:(c*overlap)+overlap], input[frame_size:frame_size+overlap])
	}
	for i := 0; i < 21; i++ {
		maskLogE[1][i] = MIN32(maskLogE[0][i], maskLogE[2][i])
	}
	channel_offset := HALF16Int(celt_log2(int(QCONST32(2.0, 14)) / (channels - 1)))
	for c := 0; c < 3; c++ {
		for i := 0; i < 21; i++ {
			maskLogE[c][i] += channel_offset
		}
	}

	for c := 0; c < channels; c++ {
		if pos[c] != 0 {
			mask := maskLogE[pos[c]-1]
			for i := 0; i < 21; i++ {
				bandLogE[21*c+i] -= mask[i]
			}
		} else {
			for i := 0; i < 21; i++ {
				bandLogE[21*c+i] = 0
			}
		}
	}
}

func (st *OpusMSEncoder) opus_multistream_encoder_init(Fs, channels, streams, coupled_streams int, mapping []int16, application OpusApplication, surround int) int {
	if channels > 255 || channels < 1 || coupled_streams > streams || streams < 1 || coupled_streams < 0 || streams > 255-coupled_streams {
		return OpusError.OPUS_BAD_ARG
	}

	st.layout.nb_channels = channels
	st.layout.nb_streams = streams
	st.layout.nb_coupled_streams = coupled_streams
	st.subframe_mem[0], st.subframe_mem[1], st.subframe_mem[2] = 0, 0, 0
	if surround == 0 {
		st.lfe_stream = -1
	}
	st.bitrate_bps = OPUS_AUTO
	st.application = application
	st.variable_duration = OPUS_FRAMESIZE_ARG
	copy(st.layout.mapping[:], mapping)
	if validate_layout(st.layout) == 0 || validate_encoder_layout(st.layout) == 0 {
		return OpusError.OPUS_BAD_ARG
	}

	encoder_ptr := 0
	for i := 0; i < st.layout.nb_coupled_streams; i++ {
		ret := st.encoders[encoder_ptr].opus_init_encoder(Fs, 2, application)
		if ret != OpusError.OPUS_OK {
			return ret
		}
		if i == st.lfe_stream {
			st.encoders[encoder_ptr].SetIsLFE(true)
		}
		encoder_ptr++
	}
	for i := st.layout.nb_coupled_streams; i < st.layout.nb_streams; i++ {
		ret := st.encoders[encoder_ptr].opus_init_encoder(Fs, 1, application)
		if i == st.lfe_stream {
			st.encoders[encoder_ptr].SetIsLFE(true)
		}
		if ret != OpusError.OPUS_OK {
			return ret
		}
		encoder_ptr++
	}
	if surround != 0 {
		for i := range st.preemph_mem {
			st.preemph_mem[i] = 0
		}
		for i := range st.window_mem {
			st.window_mem[i] = 0
		}
	}
	st.surround = surround
	return OpusError.OPUS_OK
}

func (st *OpusMSEncoder) opus_multistream_surround_encoder_init(Fs, channels, mapping_family int, streams, coupled_streams *BoxedValueInt, mapping []int16, application OpusApplication) int {
	streams.Val = 0
	coupled_streams.Val = 0
	if channels > 255 || channels < 1 {
		return OpusError.OPUS_BAD_ARG
	}
	st.lfe_stream = -1
	if mapping_family == 0 {
		if channels == 1 {
			streams.Val = 1
			coupled_streams.Val = 0
			mapping[0] = 0
		} else if channels == 2 {
			streams.Val = 1
			coupled_streams.Val = 1
			mapping[0] = 0
			mapping[1] = 1
		} else {
			return OpusError.OPUS_UNIMPLEMENTED
		}
	} else if mapping_family == 1 && channels >= 1 && channels <= 8 {
		streams.Val = vorbis_mappings[channels-1].nb_streams
		coupled_streams.Val = vorbis_mappings[channels-1].nb_coupled_streams
		for i := 0; i < channels; i++ {
			mapping[i] = vorbis_mappings[channels-1].mapping[i]
		}
		if channels >= 6 {
			st.lfe_stream = streams.Val - 1
		}
	} else if mapping_family == 255 {
		for i := 0; i < channels; i++ {
			mapping[i] = int16(i)
		}
		streams.Val = channels
		coupled_streams.Val = 0
	} else {
		return OpusError.OPUS_UNIMPLEMENTED
	}
	return st.opus_multistream_encoder_init(Fs, channels, streams.Val, coupled_streams.Val, mapping, application, Ternary(channels > 2 && mapping_family == 1, 1, 0))
}

func CreateOpusMSEncoder(Fs, channels, streams, coupled_streams int, mapping []int16, application OpusApplication) (*OpusMSEncoder, error) {
	if channels > 255 || channels < 1 || coupled_streams > streams || streams < 1 || coupled_streams < 0 || streams > 255-coupled_streams {
		return nil, errors.New("Invalid channel / stream configuration")
	}
	st, err := NewOpusMSEncoder(streams, coupled_streams)
	if err != nil {
		return nil, err
	}
	ret := st.opus_multistream_encoder_init(Fs, channels, streams, coupled_streams, mapping, application, 0)
	if ret != OpusError.OPUS_OK {
		if ret == OpusError.OPUS_BAD_ARG {
			return nil, errors.New("OPUS_BAD_ARG when creating MS encoder")
		}
		return nil, errors.New("Error while initializing MS encoder")
	}
	return st, nil
}

func GetStreamCount(channels, mapping_family int, nb_streams, nb_coupled_streams *BoxedValueInt) error {
	if mapping_family == 0 {
		if channels == 1 {
			nb_streams.Val = 1
			nb_coupled_streams.Val = 0
		} else if channels == 2 {
			nb_streams.Val = 1
			nb_coupled_streams.Val = 1
		} else {
			return errors.New("More than 2 channels requires custom mappings")
		}
	} else if mapping_family == 1 && channels >= 1 && channels <= 8 {
		nb_streams.Val = vorbis_mappings[channels-1].nb_streams
		nb_coupled_streams.Val = vorbis_mappings[channels-1].nb_coupled_streams
	} else if mapping_family == 255 {
		nb_streams.Val = channels
		nb_coupled_streams.Val = 0
	} else {
		return errors.New("Invalid mapping family")
	}
	return nil
}

func CreateSurroundOpusMSEncoder(Fs, channels, mapping_family int, streams, coupled_streams *BoxedValueInt, mapping []int16, application OpusApplication) (*OpusMSEncoder, error) {
	if channels > 255 || channels < 1 || application == OPUS_APPLICATION_UNIMPLEMENTED {
		return nil, errors.New("Invalid channel count or application")
	}
	nb_streams := BoxedValueInt{0}
	nb_coupled_streams := BoxedValueInt{0}
	err := GetStreamCount(channels, mapping_family, &nb_streams, &nb_coupled_streams)
	if err != nil {
		return nil, err
	}

	st, err := NewOpusMSEncoder(nb_streams.Val, nb_coupled_streams.Val)
	if err != nil {
		return nil, err
	}
	ret := st.opus_multistream_surround_encoder_init(Fs, channels, mapping_family, streams, coupled_streams, mapping, application)
	if ret != OpusError.OPUS_OK {
		if ret == OpusError.OPUS_BAD_ARG {
			return nil, errors.New("Bad argument passed to CreateSurround")
		}
		return nil, errors.New("Error while initializing encoder")
	}
	return st, nil
}

func (st *OpusMSEncoder) surround_rate_allocation(out_rates []int, frame_size int) int {
	var channel_rate, Fs int
	ptr := st.encoders[0]
	Fs = ptr.GetSampleRate()

	var stream_offset int
	if st.bitrate_bps > st.layout.nb_channels*40000 {
		stream_offset = 20000
	} else {
		stream_offset = st.bitrate_bps / st.layout.nb_channels / 2
	}
	stream_offset += 60 * (Fs/frame_size - 50)
	lfe_offset := 3500 + 60*(Fs/frame_size-50)
	coupled_ratio := 512
	lfe_ratio := 32

	if st.bitrate_bps == OPUS_AUTO {
		channel_rate = Fs + 60*Fs/frame_size
	} else if st.bitrate_bps == OPUS_BITRATE_MAX {
		channel_rate = 300000
	} else {
		nb_lfe := Ternary(st.lfe_stream != -1, 1, 0)
		nb_coupled := st.layout.nb_coupled_streams
		nb_uncoupled := st.layout.nb_streams - nb_coupled - nb_lfe
		total := (nb_uncoupled << 8) + coupled_ratio*nb_coupled + nb_lfe*lfe_ratio
		channel_rate = 256 * (st.bitrate_bps - lfe_offset*nb_lfe - stream_offset*(nb_coupled+nb_uncoupled)) / total
	}

	rate_sum := 0
	for i := 0; i < st.layout.nb_streams; i++ {
		if i < st.layout.nb_coupled_streams {
			out_rates[i] = stream_offset + (channel_rate * coupled_ratio >> 8)
		} else if i != st.lfe_stream {
			out_rates[i] = stream_offset + channel_rate
		} else {
			out_rates[i] = lfe_offset + (channel_rate * lfe_ratio >> 8)
		}
		out_rates[i] = IMAX(out_rates[i], 500)
		rate_sum += out_rates[i]
	}
	return rate_sum
}

const MS_FRAME_TMP = 3*1275 + 7

func (st *OpusMSEncoder) opus_multistream_encode_native(pcm []int16, pcm_ptr, analysis_frame_size int, data []byte, data_ptr, max_data_bytes, lsb_depth, float_api int) int {
	var Fs, tot_size, frame_size, rate_sum, smallest_packet int
	var vbr int
	var celt_mode *CeltMode
	var bandLogE []int
	var mem, preemph_mem []int

	if st.surround != 0 {
		preemph_mem = st.preemph_mem
		mem = st.window_mem
	}

	encoder_ptr := 0
	Fs = st.encoders[encoder_ptr].GetSampleRate()
	vbr = Ternary(st.encoders[encoder_ptr].GetUseVBR(), 1, 0)
	celt_mode = st.encoders[encoder_ptr].GetCeltMode()

	delay_compensation := st.encoders[encoder_ptr].GetLookahead() - Fs/400
	frame_size = compute_frame_size(pcm, pcm_ptr, analysis_frame_size, st.variable_duration, st.layout.nb_channels, Fs, st.bitrate_bps, delay_compensation, st.subframe_mem[:], st.encoders[encoder_ptr].analysis.enabled)

	if 400*frame_size < Fs {
		return OpusError.OPUS_BAD_ARG
	}
	if 400*frame_size != Fs && 200*frame_size != Fs && 100*frame_size != Fs && 50*frame_size != Fs && 25*frame_size != Fs && 50*frame_size != 3*Fs {
		return OpusError.OPUS_BAD_ARG
	}

	smallest_packet = st.layout.nb_streams*2 - 1
	if max_data_bytes < smallest_packet {
		return OpusError.OPUS_BUFFER_TOO_SMALL
	}
	buf := make([]int16, 2*frame_size)

	bandSMR := make([]int, 21*st.layout.nb_channels)
	if st.surround != 0 {
		surround_analysis(celt_mode, pcm, pcm_ptr, bandSMR, mem, preemph_mem, frame_size, 120, st.layout.nb_channels, Fs)
	}

	bitrates := make([]int, 256)
	rate_sum = st.surround_rate_allocation(bitrates, frame_size)

	if vbr == 0 {
		if st.bitrate_bps == OPUS_AUTO {
			max_data_bytes = IMIN(max_data_bytes, 3*rate_sum/(3*8*Fs/frame_size))
		} else if st.bitrate_bps != OPUS_BITRATE_MAX {
			max_data_bytes = IMIN(max_data_bytes, IMAX(smallest_packet, 3*st.bitrate_bps/(3*8*Fs/frame_size)))
		}
	}

	encoder_ptr = 0
	for s := 0; s < st.layout.nb_streams; s++ {
		enc := st.encoders[encoder_ptr]
		encoder_ptr++
		enc.SetBitrate(bitrates[s])
		if st.surround != 0 {
			equiv_rate := st.bitrate_bps
			if frame_size*50 < Fs {
				equiv_rate -= 60 * (Fs/frame_size - 50) * st.layout.nb_channels
			}
			if equiv_rate > 10000*st.layout.nb_channels {
				enc.SetBandwidth(OPUS_BANDWIDTH_FULLBAND)
			} else if equiv_rate > 7000*st.layout.nb_channels {
				enc.SetBandwidth(OPUS_BANDWIDTH_SUPERWIDEBAND)
			} else if equiv_rate > 5000*st.layout.nb_channels {
				enc.SetBandwidth(OPUS_BANDWIDTH_WIDEBAND)
			} else {
				enc.SetBandwidth(OPUS_BANDWIDTH_NARROWBAND)
			}
			if s < st.layout.nb_coupled_streams {
				enc.SetForceMode(MODE_CELT_ONLY)
				enc.SetForceChannels(2)
			}
		}
	}

	encoder_ptr = 0
	tot_size = 0
	rp := NewOpusRepacketizer()
	tmp_data := make([]byte, MS_FRAME_TMP)
	for s := 0; s < st.layout.nb_streams; s++ {
		enc := st.encoders[encoder_ptr]
		var len, curr_max int
		var c1, c2 int

		rp.Reset()
		if s < st.layout.nb_coupled_streams {
			left := get_left_channel(st.layout, s, -1)
			right := get_right_channel(st.layout, s, -1)
			opus_copy_channel_in_short(buf, 0, 2, pcm, pcm_ptr, st.layout.nb_channels, left, frame_size)
			opus_copy_channel_in_short(buf, 1, 2, pcm, pcm_ptr, st.layout.nb_channels, right, frame_size)
			encoder_ptr++
			if st.surround != 0 {
				bandLogE = make([]int, 42)
				for i := 0; i < 21; i++ {
					bandLogE[i] = bandSMR[21*left+i]
					bandLogE[21+i] = bandSMR[21*right+i]
				}
				enc.SetEnergyMask(bandLogE)
			}
			c1, c2 = left, right
		} else {
			_chan := get_mono_channel(st.layout, s, -1)
			opus_copy_channel_in_short(buf, 0, 1, pcm, pcm_ptr, st.layout.nb_channels, _chan, frame_size)
			encoder_ptr++
			if st.surround != 0 {
				bandLogE = make([]int, 21)
				for i := 0; i < 21; i++ {
					bandLogE[i] = bandSMR[21*_chan+i]
				}
				enc.SetEnergyMask(bandLogE)
			}
			c1, c2 = _chan, -1
		}

		curr_max = max_data_bytes - tot_size
		curr_max -= IMAX(0, 2*(st.layout.nb_streams-s-1)-1)
		curr_max = IMIN(curr_max, MS_FRAME_TMP)
		if s != st.layout.nb_streams-1 {
			if curr_max > 253 {
				curr_max -= 2
			} else {
				curr_max -= 1
			}
		}
		if vbr == 0 && s == st.layout.nb_streams-1 {
			enc.SetBitrate(curr_max * (8 * Fs / frame_size))
		}
		len = enc.opus_encode_native(buf, 0, frame_size, tmp_data, 0, curr_max, lsb_depth, pcm, pcm_ptr, analysis_frame_size, c1, c2, st.layout.nb_channels, float_api)
		if len < 0 {
			return len
		}
		rp.addPacket(tmp_data, 0, len)

		len = rp.opus_repacketizer_out_range_impl(0, rp.getNumFrames(),
			data, data_ptr, max_data_bytes-tot_size, boolToInt(s != st.layout.nb_streams-1), boolToInt(vbr == 0 && s == st.layout.nb_streams-1))

		data_ptr += len
		tot_size += len
	}

	return tot_size
}

func opus_copy_channel_in_short(dst []int16, dst_ptr, dst_stride int, src []int16, src_ptr, src_stride, src_channel, frame_size int) {
	for i := 0; i < frame_size; i++ {
		dst[dst_ptr+i*dst_stride] = src[src_ptr+i*src_stride+src_channel]
	}
}

func (st *OpusMSEncoder) EncodeMultistream(pcm []int16, pcm_offset, frame_size int, outputBuffer []byte, outputBuffer_offset, max_data_bytes int) int {
	return st.opus_multistream_encode_native(pcm, pcm_offset, frame_size, outputBuffer, outputBuffer_offset, max_data_bytes, 16, 0)
}

func (st *OpusMSEncoder) GetBitrate() int {
	value := 0
	encoder_ptr := 0
	for s := 0; s < st.layout.nb_streams; s++ {
		enc := st.encoders[encoder_ptr]
		encoder_ptr++
		value += enc.GetBitrate()
	}
	return value
}

func (st *OpusMSEncoder) SetBitrate(value int) error {
	if value < 0 && value != OPUS_AUTO && value != OPUS_BITRATE_MAX {
		return errors.New("Invalid bitrate")
	}
	st.bitrate_bps = value
	return nil
}

func (st *OpusMSEncoder) GetApplication() OpusApplication {
	return st.encoders[0].GetApplication()
}

func (st *OpusMSEncoder) SetApplication(value OpusApplication) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetApplication(value)
	}
}

func (st *OpusMSEncoder) GetForceChannels() int {
	return st.encoders[0].GetForceChannels()
}

func (st *OpusMSEncoder) SetForceChannels(value int) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetForceChannels(value)
	}
}

func (st *OpusMSEncoder) GetMaxBandwidth() int {
	return st.encoders[0].GetMaxBandwidth()
}

func (st *OpusMSEncoder) SetMaxBandwidth(value int) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetMaxBandwidth(value)
	}
}

func (st *OpusMSEncoder) GetBandwidth() int {
	return st.encoders[0].GetBandwidth()
}

func (st *OpusMSEncoder) SetBandwidth(value int) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetBandwidth(value)
	}
}

func (st *OpusMSEncoder) GetUseDTX() bool {
	return st.encoders[0].GetUseDTX()
}

func (st *OpusMSEncoder) SetUseDTX(value bool) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetUseDTX(value)
	}
}

func (st *OpusMSEncoder) GetComplexity() int {
	return st.encoders[0].GetComplexity()
}

func (st *OpusMSEncoder) SetComplexity(value int) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetComplexity(value)
	}
}

func (st *OpusMSEncoder) GetForceMode() int {
	return st.encoders[0].GetForceMode()
}

func (st *OpusMSEncoder) SetForceMode(value int) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetForceMode(value)
	}
}

func (st *OpusMSEncoder) GetUseInbandFEC() bool {
	return st.encoders[0].GetUseInbandFEC()
}

func (st *OpusMSEncoder) SetUseInbandFEC(value bool) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetUseInbandFEC(value)
	}
}

func (st *OpusMSEncoder) GetPacketLossPercent() int {
	return st.encoders[0].GetPacketLossPercent()
}

func (st *OpusMSEncoder) SetPacketLossPercent(value int) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetPacketLossPercent(value)
	}
}

func (st *OpusMSEncoder) GetUseVBR() bool {
	return st.encoders[0].GetUseVBR()
}

func (st *OpusMSEncoder) SetUseVBR(value bool) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetUseVBR(value)
	}
}

func (st *OpusMSEncoder) GetUseConstrainedVBR() bool {
	return st.encoders[0].GetUseConstrainedVBR()
}

func (st *OpusMSEncoder) SetUseConstrainedVBR(value bool) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetUseConstrainedVBR(value)
	}
}

func (st *OpusMSEncoder) GetSignalType() OpusSignal {
	return st.encoders[0].GetSignalType()
}

func (st *OpusMSEncoder) SetSignalType(value OpusSignal) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetSignalType(value)
	}
}

func (st *OpusMSEncoder) GetLookahead() int {
	return st.encoders[0].GetLookahead()
}

func (st *OpusMSEncoder) GetSampleRate() int {
	return st.encoders[0].GetSampleRate()
}

func (st *OpusMSEncoder) GetFinalRange() int {
	value := 0
	encoder_ptr := 0
	for s := 0; s < st.layout.nb_streams; s++ {
		enc := st.encoders[encoder_ptr]
		encoder_ptr++
		value ^= enc.GetFinalRange()
	}
	return value
}

func (st *OpusMSEncoder) GetLSBDepth() int {
	return st.encoders[0].GetLSBDepth()
}

func (st *OpusMSEncoder) SetLSBDepth(value int) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetLSBDepth(value)
	}
}

func (st *OpusMSEncoder) GetPredictionDisabled() bool {
	return st.encoders[0].GetPredictionDisabled()
}

func (st *OpusMSEncoder) SetPredictionDisabled(value bool) {
	for i := 0; i < st.layout.nb_streams; i++ {
		st.encoders[i].SetPredictionDisabled(value)
	}
}

func (st *OpusMSEncoder) GetExpertFrameDuration() OpusFramesize {
	return st.variable_duration
}

func (st *OpusMSEncoder) SetExpertFrameDuration(value OpusFramesize) {
	st.variable_duration = value
}

func (st *OpusMSEncoder) GetMultistreamEncoderState(streamId int) (*OpusEncoder, error) {
	if streamId >= st.layout.nb_streams {
		return nil, errors.New("Requested stream doesn't exist")
	}
	return st.encoders[streamId], nil
}

func Ternary(condition bool, trueVal, falseVal int) int {
	if condition {
		return trueVal
	}
	return falseVal
}
