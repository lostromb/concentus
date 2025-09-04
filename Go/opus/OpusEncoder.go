package opus

import (
	"encoding/json"
	"errors"
	"fmt"
	"math"
	"strconv"
	"strings"
)

type OpusEncoder struct {
	silk_mode               EncControlState
	application             OpusApplication
	channels                int
	delay_compensation      int
	force_channels          int
	signal_type             OpusSignal
	user_bandwidth          int
	max_bandwidth           int
	user_forced_mode        int
	voice_ratio             int
	Fs                      int
	use_vbr                 int
	vbr_constraint          int
	variable_duration       OpusFramesize
	bitrate_bps             int
	user_bitrate_bps        int
	lsb_depth               int
	encoder_buffer          int
	lfe                     int
	analysis                TonalityAnalysisState
	stream_channels         int
	hybrid_stereo_width_Q14 int16
	variable_HP_smth2_Q15   int
	prev_HB_gain            int
	hp_mem                  [4]int
	mode                    int
	prev_mode               int
	prev_channels           int
	prev_framesize          int
	bandwidth               int
	silk_bw_switch          int
	first                   int
	energy_masking          []int
	width_mem               StereoWidthState
	delay_buffer            [MAX_ENCODER_BUFFER * 2]int16
	detected_bandwidth      int
	rangeFinal              int
	SilkEncoder             SilkEncoder
	Celt_Encoder            CeltEncoder
}

func (st *OpusEncoder) reset() {
	st.silk_mode.Reset()
	st.application = OPUS_APPLICATION_UNIMPLEMENTED
	st.channels = 0
	st.delay_compensation = 0
	st.force_channels = 0
	st.signal_type = OPUS_SIGNAL_UNKNOWN
	st.user_bandwidth = OPUS_BANDWIDTH_UNKNOWN
	st.max_bandwidth = OPUS_BANDWIDTH_UNKNOWN
	st.user_forced_mode = MODE_UNKNOWN
	st.voice_ratio = 0
	st.Fs = 0
	st.use_vbr = 0
	st.vbr_constraint = 0
	st.variable_duration = OPUS_FRAMESIZE_UNKNOWN
	st.bitrate_bps = 0
	st.user_bitrate_bps = 0
	st.lsb_depth = 0
	st.encoder_buffer = 0
	st.lfe = 0
	st.analysis.Reset()
	st.PartialReset()
}

func (st *OpusEncoder) PartialReset() {
	st.stream_channels = 0
	st.hybrid_stereo_width_Q14 = 0
	st.variable_HP_smth2_Q15 = 0
	st.prev_HB_gain = CeltConstants.Q15ONE
	for i := range st.hp_mem {
		st.hp_mem[i] = 0
	}
	st.mode = MODE_UNKNOWN
	st.prev_mode = MODE_UNKNOWN
	st.prev_channels = 0
	st.prev_framesize = 0
	st.bandwidth = OPUS_BANDWIDTH_UNKNOWN
	st.silk_bw_switch = 0
	st.first = 0
	st.energy_masking = nil
	st.width_mem.Reset()
	for i := range st.delay_buffer {
		st.delay_buffer[i] = 0
	}
	st.detected_bandwidth = OPUS_BANDWIDTH_UNKNOWN
	st.rangeFinal = 0
}

func (st *OpusEncoder) ResetState() {
	dummy := EncControlState{}
	st.analysis.Reset()
	st.PartialReset()
	st.Celt_Encoder.ResetState()
	silk_InitEncoder(&st.SilkEncoder, &dummy)
	st.stream_channels = st.channels
	st.hybrid_stereo_width_Q14 = 1 << 14
	st.prev_HB_gain = CeltConstants.Q15ONE
	st.first = 1
	st.mode = MODE_HYBRID
	st.bandwidth = OPUS_BANDWIDTH_FULLBAND
	st.variable_HP_smth2_Q15 = silk_LSHIFT(silk_lin2log(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ), 8)
}

func NewOpusEncoder(Fs, channels int, application OpusApplication) (*OpusEncoder, error) {
	if Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000 {
		return nil, errors.New("Sample rate is invalid (must be 8/12/16/24/48 Khz)")
	}
	if channels != 1 && channels != 2 {
		return nil, errors.New("Number of channels must be 1 or 2")
	}
	st := &OpusEncoder{}

	st.SilkEncoder = NewSilkEncoder()

	st.Celt_Encoder = CeltEncoder{}
	st.analysis = NewTonalityAnalysisState()
	st.silk_mode = EncControlState{}
	ret := st.opus_init_encoder(Fs, channels, application)
	if ret != OpusError.OPUS_OK {
		if ret == OpusError.OPUS_BAD_ARG {
			return nil, errors.New("OPUS_BAD_ARG when creating encoder")
		}
		return nil, errors.New("Error while initializing encoder")
	}
	return st, nil
}

func (st *OpusEncoder) opus_init_encoder(Fs, channels int, application OpusApplication) int {
	if (Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000) || (channels != 1 && channels != 2) || application == OPUS_APPLICATION_UNIMPLEMENTED {
		return OpusError.OPUS_BAD_ARG
	}
	st.reset()
	st.stream_channels = channels
	st.channels = channels
	st.Fs = Fs

	ret := silk_InitEncoder(&st.SilkEncoder, &st.silk_mode)

	if ret != 0 {
		return OpusError.OPUS_INTERNAL_ERROR
	}
	st.silk_mode.nChannelsAPI = channels
	st.silk_mode.nChannelsInternal = channels
	st.silk_mode.API_sampleRate = Fs
	st.silk_mode.maxInternalSampleRate = 16000
	st.silk_mode.minInternalSampleRate = 8000
	st.silk_mode.desiredInternalSampleRate = 16000
	st.silk_mode.payloadSize_ms = 20
	st.silk_mode.bitRate = 25000
	st.silk_mode.packetLossPercentage = 0
	st.silk_mode.complexity = 9
	st.silk_mode.useInBandFEC = 0
	st.silk_mode.useDTX = 0
	st.silk_mode.useCBR = 0
	st.silk_mode.reducedDependency = 0
	err := st.Celt_Encoder.celt_encoder_init(Fs, channels)
	if err != OpusError.OPUS_OK {
		return OpusError.OPUS_INTERNAL_ERROR
	}
	st.Celt_Encoder.SetSignalling(0)
	st.Celt_Encoder.SetComplexity(st.silk_mode.complexity)
	st.use_vbr = 1
	st.vbr_constraint = 1
	st.user_bitrate_bps = OPUS_AUTO
	st.bitrate_bps = 3000 + Fs*channels
	st.application = application
	st.signal_type = OPUS_SIGNAL_AUTO
	st.user_bandwidth = OPUS_BANDWIDTH_AUTO
	st.max_bandwidth = OPUS_BANDWIDTH_FULLBAND
	st.force_channels = OPUS_AUTO
	st.user_forced_mode = MODE_AUTO
	st.voice_ratio = -1
	st.encoder_buffer = Fs / 100
	st.lsb_depth = 24
	st.variable_duration = OPUS_FRAMESIZE_ARG
	st.delay_compensation = Fs / 250
	st.hybrid_stereo_width_Q14 = 1 << 14
	st.prev_HB_gain = CeltConstants.Q15ONE
	st.variable_HP_smth2_Q15 = silk_LSHIFT(silk_lin2log(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ), 8)
	st.first = 1
	st.mode = MODE_HYBRID
	st.bandwidth = OPUS_BANDWIDTH_FULLBAND
	tonality_analysis_init(&st.analysis)
	return OpusError.OPUS_OK
}

func (st *OpusEncoder) user_bitrate_to_bitrate(frame_size, max_data_bytes int) int {
	if frame_size == 0 {
		frame_size = st.Fs / 400
	}
	if st.user_bitrate_bps == OPUS_AUTO {
		return 60*st.Fs/frame_size + st.Fs*st.channels
	} else if st.user_bitrate_bps == OPUS_BITRATE_MAX {
		return max_data_bytes * 8 * st.Fs / frame_size
	} else {
		return st.user_bitrate_bps
	}
}

func (st *OpusEncoder) opus_encode_native(pcm []int16, pcm_ptr, frame_size int, data []byte, data_ptr, out_data_bytes, lsb_depth int, analysis_pcm []int16, analysis_pcm_ptr, analysis_size, c1, c2, analysis_channels, float_api int) int {

	silk_enc := &st.SilkEncoder
	celt_enc := &st.Celt_Encoder

	var i int
	var ret int = 0
	var nBytes int

	enc := NewEntropyCoder()
	var bytes_target int
	var prefill int = 0
	var start_band int = 0
	var redundancy int = 0
	var redundancy_bytes int = 0
	/* Number of bytes to use for redundancy frame */
	var celt_to_silk int = 0

	var nb_compr_bytes int
	var to_celt int = 0
	var redundant_rng int = 0
	var cutoff_Hz, hp_freq_smth1 int
	var voice_est int
	/* Probability of voice in Q7 */
	var equiv_rate int
	var delay_compensation int
	var frame_rate int
	var max_rate int
	/* Max bitrate we're allowed to use */
	var curr_bandwidth int
	var HB_gain int
	var max_data_bytes int
	/* Max number of bytes we're allowed to use */
	var total_buffer int
	var stereo_width int

	var celt_mode *CeltMode

	var analysis_info = AnalysisInfo{} // porting note: stack var
	var analysis_read_pos_bak int = -1
	var analysis_read_subframe_bak int = -1

	max_data_bytes = IMIN(1276, out_data_bytes)

	st.rangeFinal = 0
	if (st.variable_duration == OPUS_FRAMESIZE_UNKNOWN && 400*frame_size != st.Fs && 200*frame_size != st.Fs && 100*frame_size != st.Fs &&
		50*frame_size != st.Fs && 25*frame_size != st.Fs && 50*frame_size != 3*st.Fs) ||
		(400*frame_size < st.Fs) ||
		max_data_bytes <= 0 {
		return OpusError.OPUS_BAD_ARG
	}

	if st.application == OPUS_APPLICATION_RESTRICTED_LOWDELAY {
		delay_compensation = 0
	} else {
		delay_compensation = st.delay_compensation
	}

	lsb_depth = IMIN(lsb_depth, st.lsb_depth)
	celt_mode = celt_enc.GetMode()
	st.voice_ratio = -1

	if st.analysis.enabled {
		analysis_info.valid = 0
		if st.silk_mode.complexity >= 7 && st.Fs == 48000 {
			analysis_read_pos_bak = st.analysis.read_pos
			analysis_read_subframe_bak = st.analysis.read_subframe
			run_analysis(&st.analysis,
				celt_mode,
				analysis_pcm,
				analysis_pcm_ptr,
				analysis_size,
				frame_size,
				c1,
				c2,
				analysis_channels,
				st.Fs,
				lsb_depth,
				&analysis_info)
		}

		st.detected_bandwidth = OPUS_BANDWIDTH_UNKNOWN
		if analysis_info.valid != 0 {
			var analysis_bandwidth int
			if st.signal_type == OPUS_SIGNAL_AUTO {
				st.voice_ratio = int(math.Trunc(.5 + 100*float64(1-analysis_info.music_prob)))
			}

			analysis_bandwidth = analysis_info.bandwidth
			if analysis_bandwidth <= 12 {
				st.detected_bandwidth = OPUS_BANDWIDTH_NARROWBAND
			} else if analysis_bandwidth <= 14 {
				st.detected_bandwidth = OPUS_BANDWIDTH_MEDIUMBAND
			} else if analysis_bandwidth <= 16 {
				st.detected_bandwidth = OPUS_BANDWIDTH_WIDEBAND
			} else if analysis_bandwidth <= 18 {
				st.detected_bandwidth = OPUS_BANDWIDTH_SUPERWIDEBAND
			} else {
				st.detected_bandwidth = OPUS_BANDWIDTH_FULLBAND
			}
		}
	}

	if st.channels == 2 && st.force_channels != 1 {
		stereo_width = compute_stereo_width(pcm, pcm_ptr, frame_size, st.Fs, &st.width_mem)
	} else {
		stereo_width = 0
	}
	total_buffer = delay_compensation
	st.bitrate_bps = st.user_bitrate_to_bitrate(frame_size, max_data_bytes)

	frame_rate = st.Fs / frame_size
	if st.use_vbr == 0 {
		var cbrBytes int
		/* Multiply by 3 to make sure the division is exact. */
		frame_rate3 := 3 * st.Fs / frame_size
		/* We need to make sure that "int" values always fit in 16 bits. */
		cbrBytes = IMIN((3*st.bitrate_bps/8+frame_rate3/2)/frame_rate3, max_data_bytes)
		st.bitrate_bps = cbrBytes * frame_rate3 * 8 / 3
		max_data_bytes = cbrBytes
	}
	if max_data_bytes < 3 || st.bitrate_bps < 3*frame_rate*8 ||
		(frame_rate < 50 && (max_data_bytes*frame_rate < 300 || st.bitrate_bps < 2400)) {
		/*If the space is too low to do something useful, emit 'PLC' frames.*/
		tocmode := st.mode
		bw := st.bandwidth
		if st.bandwidth == OPUS_BANDWIDTH_UNKNOWN {
			bw = OPUS_BANDWIDTH_NARROWBAND
		}

		if tocmode == MODE_UNKNOWN {
			tocmode = MODE_SILK_ONLY
		}
		if frame_rate > 100 {
			tocmode = MODE_CELT_ONLY
		}
		if frame_rate < 50 {
			tocmode = MODE_SILK_ONLY
		}
		if tocmode == MODE_SILK_ONLY && OpusBandwidthHelpers_GetOrdinal(bw) > OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_WIDEBAND) {
			bw = OPUS_BANDWIDTH_WIDEBAND
		} else if tocmode == MODE_CELT_ONLY && OpusBandwidthHelpers_GetOrdinal(bw) == OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_MEDIUMBAND) {
			bw = OPUS_BANDWIDTH_NARROWBAND
		} else if tocmode == MODE_HYBRID && OpusBandwidthHelpers_GetOrdinal(bw) <= OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_SUPERWIDEBAND) {
			bw = OPUS_BANDWIDTH_SUPERWIDEBAND
		}
		data[data_ptr] = gen_toc(tocmode, frame_rate, bw, st.stream_channels)
		ret = 1
		if st.use_vbr == 0 {
			ret = PadPacket(data, data_ptr, ret, max_data_bytes)
			if ret == OpusError.OPUS_OK {
				ret = max_data_bytes
			}
		}
		return ret
	}
	max_rate = frame_rate * max_data_bytes * 8

	/* Equivalent 20-ms rate for mode/channel/bandwidth decisions */
	equiv_rate = st.bitrate_bps - (40*st.channels+20)*(st.Fs/frame_size-50)

	if st.signal_type == OPUS_SIGNAL_VOICE {
		voice_est = 127
	} else if st.signal_type == OPUS_SIGNAL_MUSIC {
		voice_est = 0
	} else if st.voice_ratio >= 0 {
		voice_est = st.voice_ratio * 327 >> 8
		/* For AUDIO, never be more than 90% confident of having speech */
		if st.application == OPUS_APPLICATION_AUDIO {
			voice_est = IMIN(voice_est, 115)
		}
	} else if st.application == OPUS_APPLICATION_VOIP {
		voice_est = 115
	} else {
		voice_est = 48
	}

	if st.force_channels != OPUS_AUTO && st.channels == 2 {
		st.stream_channels = st.force_channels
	} else /* Rate-dependent mono-stereo decision */ if st.channels == 2 {
		var stereo_threshold int
		stereo_threshold = stereo_music_threshold + ((voice_est * voice_est * (stereo_voice_threshold - stereo_music_threshold)) >> 14)
		if st.stream_channels == 2 {
			stereo_threshold -= 1000
		} else {
			stereo_threshold += 1000
		}
		st.stream_channels = 1
		if equiv_rate > stereo_threshold {
			st.stream_channels = 2
		}
	} else {
		st.stream_channels = st.channels
	}
	equiv_rate = st.bitrate_bps - (40*st.stream_channels+20)*(st.Fs/frame_size-50)

	/* Mode selection depending on application and signal type */
	if st.application == OPUS_APPLICATION_RESTRICTED_LOWDELAY {
		st.mode = MODE_CELT_ONLY
	} else if st.user_forced_mode == MODE_AUTO {
		var mode_voice, mode_music int
		var threshold int

		/* Interpolate based on stereo width */
		mode_voice = MULT16_32_Q15Int(CeltConstants.Q15ONE-stereo_width, mode_thresholds[0][0]) +
			MULT16_32_Q15Int(stereo_width, mode_thresholds[1][0])
		mode_music = MULT16_32_Q15Int(CeltConstants.Q15ONE-stereo_width, mode_thresholds[1][1]) +
			MULT16_32_Q15Int(stereo_width, mode_thresholds[1][1])
		/* Interpolate based on speech/music probability */
		threshold = mode_music + ((voice_est * voice_est * (mode_voice - mode_music)) >> 14)
		/* Bias towards SILK for VoIP because of some useful features */
		if st.application == OPUS_APPLICATION_VOIP {
			threshold += 8000
		}

		/* Hysteresis */
		if st.prev_mode == MODE_CELT_ONLY {
			threshold -= 4000
		} else if st.prev_mode != MODE_AUTO && st.prev_mode != MODE_UNKNOWN {
			threshold += 4000
		}
		st.mode = MODE_SILK_ONLY
		if equiv_rate >= threshold {
			st.mode = MODE_CELT_ONLY
		}

		/* When FEC is enabled and there's enough packet loss, use SILK */
		if st.silk_mode.useInBandFEC != 0 && st.silk_mode.packetLossPercentage > (128-voice_est)>>4 {
			st.mode = MODE_SILK_ONLY
		}
		/* When encoding voice and DTX is enabled, set the encoder to SILK mode (at least for now) */
		if st.silk_mode.useDTX != 0 && voice_est > 100 {
			st.mode = MODE_SILK_ONLY
		}
	} else {
		st.mode = st.user_forced_mode
	}

	/* Override the chosen mode to make sure we meet the requested frame size */
	if st.mode != MODE_CELT_ONLY && frame_size < st.Fs/100 {
		st.mode = MODE_CELT_ONLY
	}
	if st.lfe != 0 {
		st.mode = MODE_CELT_ONLY
	}
	/* If max_data_bytes represents less than 8 kb/s, switch to CELT-only mode */
	frame_defult := 8000
	if frame_rate > 50 {
		frame_defult = 12000
	}

	if max_data_bytes < (frame_defult)*frame_size/(st.Fs*8) {
		st.mode = MODE_CELT_ONLY
	}

	if st.stream_channels == 1 && st.prev_channels == 2 && st.silk_mode.toMono == 0 &&
		st.mode != MODE_CELT_ONLY && st.prev_mode != MODE_CELT_ONLY {
		/* Delay stereo.mono transition by two frames so that SILK can do a smooth downmix */
		st.silk_mode.toMono = 1
		st.stream_channels = 2
	} else {
		st.silk_mode.toMono = 0
	}

	if (st.prev_mode != MODE_AUTO && st.prev_mode != MODE_UNKNOWN) &&
		((st.mode != MODE_CELT_ONLY && st.prev_mode == MODE_CELT_ONLY) ||
			(st.mode == MODE_CELT_ONLY && st.prev_mode != MODE_CELT_ONLY)) {
		redundancy = 1
		celt_to_silk = boolToInt(st.mode != MODE_CELT_ONLY)
		if celt_to_silk == 0 {
			/* Switch to SILK/hybrid if frame size is 10 ms or more*/
			if frame_size >= st.Fs/100 {
				st.mode = st.prev_mode
				to_celt = 1
			} else {
				redundancy = 0
			}
		}
	}
	/* For the first frame at a new SILK bandwidth */
	if st.silk_bw_switch != 0 {
		redundancy = 1
		celt_to_silk = 1
		st.silk_bw_switch = 0
		prefill = 1
	}

	if redundancy != 0 {
		/* Fair share of the max size allowed */
		redundancy_bytes = IMIN(257, max_data_bytes*(st.Fs/200)/(frame_size+st.Fs/200))
		/* For VBR, target the actual bitrate (subject to the limit above) */
		if st.use_vbr != 0 {
			redundancy_bytes = IMIN(redundancy_bytes, st.bitrate_bps/1600)
		}
	}

	if st.mode != MODE_CELT_ONLY && st.prev_mode == MODE_CELT_ONLY {
		//  EncControlState dummy = new EncControlState();
		dummy := &EncControlState{}
		silk_InitEncoder(silk_enc, dummy)
		prefill = 1
	}

	/* Automatic (rate-dependent) bandwidth selection */
	if st.mode == MODE_CELT_ONLY || st.first != 0 || st.silk_mode.allowBandwidthSwitch != 0 {
		var voice_bandwidth_thresholds []int
		var music_bandwidth_thresholds []int
		var bandwidth_thresholds = make([]int, 8)
		bandwidth := OPUS_BANDWIDTH_FULLBAND
		var equiv_rate2 int

		equiv_rate2 = equiv_rate
		if st.mode != MODE_CELT_ONLY {
			/* Adjust the threshold +/- 10% depending on complexity */
			equiv_rate2 = equiv_rate2 * (45 + st.silk_mode.complexity) / 50
			/* CBR is less efficient by ~1 kb/s */
			if st.use_vbr == 0 {
				equiv_rate2 -= 1000
			}
		}
		if st.channels == 2 && st.force_channels != 1 {
			voice_bandwidth_thresholds = STEREO_VOICE_BANDWIDTH_THRESHOLDS
			music_bandwidth_thresholds = STEREO_MUSIC_BANDWIDTH_THRESHOLDS
		} else {
			voice_bandwidth_thresholds = MONO_VOICE_BANDWIDTH_THRESHOLDS
			music_bandwidth_thresholds = MONO_MUSIC_BANDWIDTH_THRESHOLDS
		}
		/* Interpolate bandwidth thresholds depending on voice estimation */
		for i = 0; i < 8; i++ {
			bandwidth_thresholds[i] = music_bandwidth_thresholds[i] +
				((voice_est * voice_est * (voice_bandwidth_thresholds[i] - music_bandwidth_thresholds[i])) >> 14)
		}
		for {
			var threshold, hysteresis int
			threshold = bandwidth_thresholds[2*(OpusBandwidthHelpers_GetOrdinal(bandwidth)-OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_MEDIUMBAND))]
			hysteresis = bandwidth_thresholds[2*(OpusBandwidthHelpers_GetOrdinal(bandwidth)-OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_MEDIUMBAND))+1]
			if st.first == 0 {
				if OpusBandwidthHelpers_GetOrdinal(st.bandwidth) >= OpusBandwidthHelpers_GetOrdinal(bandwidth) {
					threshold -= hysteresis
				} else {
					threshold += hysteresis
				}
			}
			if equiv_rate2 >= threshold {
				break
			}

			bandwidth = SUBTRACT(bandwidth, 1)

			if OpusBandwidthHelpers_GetOrdinal(bandwidth) > OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_NARROWBAND) {
				continue
			} else {
				break
			}
		}

		st.bandwidth = bandwidth
		/* Prevents any transition to SWB/FB until the SILK layer has fully
		   switched to WB mode and turned the variable LP filter off */
		if st.first == 0 && st.mode != MODE_CELT_ONLY &&
			st.silk_mode.inWBmodeWithoutVariableLP == 0 &&
			OpusBandwidthHelpers_GetOrdinal(st.bandwidth) > OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_WIDEBAND) {
			st.bandwidth = OPUS_BANDWIDTH_WIDEBAND
		}
	}

	if OpusBandwidthHelpers_GetOrdinal(st.bandwidth) > OpusBandwidthHelpers_GetOrdinal(st.max_bandwidth) {
		st.bandwidth = st.max_bandwidth
	}

	if st.user_bandwidth != OPUS_BANDWIDTH_AUTO {
		st.bandwidth = st.user_bandwidth
	}

	/* This prevents us from using hybrid at unsafe CBR/max rates */
	if st.mode != MODE_CELT_ONLY && max_rate < 15000 {
		st.bandwidth = OpusBandwidthHelpers_MIN(st.bandwidth, OPUS_BANDWIDTH_WIDEBAND)
	}

	/* Prevents Opus from wasting bits on frequencies that are above
	   the Nyquist rate of the input signal */
	if st.Fs <= 24000 && OpusBandwidthHelpers_GetOrdinal(st.bandwidth) > OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_SUPERWIDEBAND) {
		st.bandwidth = OPUS_BANDWIDTH_SUPERWIDEBAND
	}
	if st.Fs <= 16000 && OpusBandwidthHelpers_GetOrdinal(st.bandwidth) > OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_WIDEBAND) {
		st.bandwidth = OPUS_BANDWIDTH_WIDEBAND
	}
	if st.Fs <= 12000 && OpusBandwidthHelpers_GetOrdinal(st.bandwidth) > OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_MEDIUMBAND) {
		st.bandwidth = OPUS_BANDWIDTH_MEDIUMBAND
	}
	if st.Fs <= 8000 && OpusBandwidthHelpers_GetOrdinal(st.bandwidth) > OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_NARROWBAND) {
		st.bandwidth = OPUS_BANDWIDTH_NARROWBAND
	}
	/* Use detected bandwidth to reduce the encoded bandwidth. */
	if st.detected_bandwidth != OPUS_BANDWIDTH_UNKNOWN && st.user_bandwidth == OPUS_BANDWIDTH_AUTO {
		var min_detected_bandwidth int
		/* Makes bandwidth detection more conservative just in case the detector
		   gets it wrong when we could have coded a high bandwidth transparently.
		   When operating in SILK/hybrid mode, we don't go below wideband to avoid
		   more complicated switches that require redundancy. */
		if equiv_rate <= 18000*st.stream_channels && st.mode == MODE_CELT_ONLY {
			min_detected_bandwidth = OPUS_BANDWIDTH_NARROWBAND
		} else if equiv_rate <= 24000*st.stream_channels && st.mode == MODE_CELT_ONLY {
			min_detected_bandwidth = OPUS_BANDWIDTH_MEDIUMBAND
		} else if equiv_rate <= 30000*st.stream_channels {
			min_detected_bandwidth = OPUS_BANDWIDTH_WIDEBAND
		} else if equiv_rate <= 44000*st.stream_channels {
			min_detected_bandwidth = OPUS_BANDWIDTH_SUPERWIDEBAND
		} else {
			min_detected_bandwidth = OPUS_BANDWIDTH_FULLBAND
		}

		st.detected_bandwidth = OpusBandwidthHelpers_MAX(st.detected_bandwidth, min_detected_bandwidth)
		st.bandwidth = OpusBandwidthHelpers_MIN(st.bandwidth, st.detected_bandwidth)

	}
	celt_enc.SetLSBDepth(lsb_depth)

	/* CELT mode doesn't support mediumband, use wideband instead */
	if st.mode == MODE_CELT_ONLY && st.bandwidth == OPUS_BANDWIDTH_MEDIUMBAND {
		st.bandwidth = OPUS_BANDWIDTH_WIDEBAND
	}
	if st.lfe != 0 {
		st.bandwidth = OPUS_BANDWIDTH_NARROWBAND
	}

	/* Can't support higher than wideband for >20 ms frames */
	if frame_size > st.Fs/50 && (st.mode == MODE_CELT_ONLY || OpusBandwidthHelpers_GetOrdinal(st.bandwidth) > OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_WIDEBAND)) {
		var tmp_data []byte
		var nb_frames int
		var bak_bandwidth int
		var bak_channels, bak_to_mono int
		var bak_mode int
		var rp *OpusRepacketizer
		var bytes_per_frame int
		var repacketize_len int

		if st.analysis.enabled && analysis_read_pos_bak != -1 {
			st.analysis.read_pos = analysis_read_pos_bak
			st.analysis.read_subframe = analysis_read_subframe_bak
		}
		nb_frames = 2
		if frame_size > st.Fs/25 {
			nb_frames = 3
		}

		bytes_per_frame = IMIN(1276, (out_data_bytes-3)/nb_frames)

		tmp_data = make([]byte, nb_frames*bytes_per_frame)

		rp = NewOpusRepacketizer()

		bak_mode = st.user_forced_mode
		bak_bandwidth = st.user_bandwidth
		bak_channels = st.force_channels

		st.user_forced_mode = st.mode
		st.user_bandwidth = st.bandwidth
		st.force_channels = st.stream_channels
		bak_to_mono = st.silk_mode.toMono

		if bak_to_mono != 0 {
			st.force_channels = 1
		} else {
			st.prev_channels = st.stream_channels
		}
		for i = 0; i < nb_frames; i++ {
			var tmp_len int
			st.silk_mode.toMono = 0
			/* When switching from SILK/Hybrid to CELT, only ask for a switch at the last frame */
			if to_celt != 0 && i == nb_frames-1 {
				st.user_forced_mode = MODE_CELT_ONLY
			}
			tmp_len = st.opus_encode_native(pcm, pcm_ptr+(i*(st.channels*st.Fs/50)), st.Fs/50,
				tmp_data, i*bytes_per_frame, bytes_per_frame, lsb_depth,
				nil, 0, 0, c1, c2, analysis_channels, float_api)
			if tmp_len < 0 {

				return OpusError.OPUS_INTERNAL_ERROR
			}
			if Debug {
				fmt.Printf("tmp_data:%+v\r\n", tmp_data)
			}
			ret = rp.addPacket(tmp_data, i*bytes_per_frame, tmp_len)
			if ret < 0 {

				return OpusError.OPUS_INTERNAL_ERROR
			}
		}
		if st.use_vbr != 0 {
			repacketize_len = out_data_bytes
		} else {
			repacketize_len = IMIN(3*st.bitrate_bps/(3*8*50/nb_frames), out_data_bytes)
		}
		if Debug {
			dataStr, _ := json.Marshal(ConvertByteToInt8(data))
			fmt.Printf("data-1:%s\r\n", dataStr)
		}
		ret = rp.opus_repacketizer_out_range_impl(0, nb_frames, data, data_ptr, repacketize_len, 0, boolToInt(st.use_vbr == 0))
		if Debug {
			dataStr, _ := json.Marshal(ConvertByteToInt8(data))
			fmt.Printf("data:%s\r\n", dataStr)
		}
		if ret < 0 {
			return OpusError.OPUS_INTERNAL_ERROR
		}
		st.user_forced_mode = bak_mode
		st.user_bandwidth = bak_bandwidth
		st.force_channels = bak_channels
		st.silk_mode.toMono = bak_to_mono

		return ret
	}
	curr_bandwidth = st.bandwidth

	/* Chooses the appropriate mode for speech
	 *NEVER* switch to/from CELT-only mode here as this will invalidate some assumptions */
	if st.mode == MODE_SILK_ONLY && OpusBandwidthHelpers_GetOrdinal(curr_bandwidth) > OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_WIDEBAND) {
		st.mode = MODE_HYBRID
	}
	if st.mode == MODE_HYBRID && OpusBandwidthHelpers_GetOrdinal(curr_bandwidth) <= OpusBandwidthHelpers_GetOrdinal(OPUS_BANDWIDTH_WIDEBAND) {
		st.mode = MODE_SILK_ONLY
	}

	/* printf("%d %d %d %d\n", st.bitrate_bps, st.stream_channels, st.mode, curr_bandwidth); */
	bytes_target = IMIN(max_data_bytes-redundancy_bytes, st.bitrate_bps*frame_size/(st.Fs*8)) - 1

	data_ptr += 1

	enc.enc_init(data, data_ptr, (max_data_bytes - 1))

	pcm_buf := make([]int16, (total_buffer+frame_size)*st.channels)
	//System.arraycopy(st.delay_buffer, ((st.encoder_buffer - total_buffer) * st.channels), pcm_buf, 0, total_buffer*st.channels)
	//copy(pcm_buf, st.delay_buffer[((st.encoder_buffer-total_buffer)*st.channels):total_buffer*st.channels])
	copy(pcm_buf[:total_buffer*st.channels], st.delay_buffer[(st.encoder_buffer-total_buffer)*st.channels:(st.encoder_buffer-total_buffer)*st.channels+total_buffer*st.channels])

	if st.mode == MODE_CELT_ONLY {
		hp_freq_smth1 = silk_LSHIFT(silk_lin2log(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ), 8)
	} else {
		hp_freq_smth1 = silk_enc.state_Fxx[0].variable_HP_smth1_Q15
	}

	st.variable_HP_smth2_Q15 = silk_SMLAWB(st.variable_HP_smth2_Q15,
		hp_freq_smth1-st.variable_HP_smth2_Q15, int(float64(TuningParameters.VARIABLE_HP_SMTH_COEF2)*float64(int64(1)<<(16))+0.5))

	/* convert from log scale to Hertz */
	cutoff_Hz = silk_log2lin(silk_RSHIFT(st.variable_HP_smth2_Q15, 8))

	if st.application == OPUS_APPLICATION_VOIP {
		hp_cutoff(pcm, pcm_ptr, cutoff_Hz, pcm_buf, (total_buffer * st.channels), st.hp_mem[:], frame_size, st.channels, st.Fs)
	} else {
		//  System.out.println("pcm-4:"+ java.util.Arrays.toString(pcm));
		dc_reject(pcm, pcm_ptr, 3, pcm_buf, total_buffer*st.channels, st.hp_mem[:], frame_size, st.channels, st.Fs)
	}

	/* SILK processing */
	HB_gain = CeltConstants.Q15ONE
	if st.mode != MODE_CELT_ONLY {
		var total_bitRate, celt_rate int
		pcm_silk := make([]int16, st.channels*frame_size)
		/* Distribute bits between SILK and CELT */
		total_bitRate = 8 * bytes_target * frame_rate

		if st.mode == MODE_HYBRID {
			var HB_gain_ref int
			/* Base rate for SILK */
			tmpBit := 0
			if st.Fs == 100*frame_size {
				tmpBit = 1000
			}
			st.silk_mode.bitRate = st.stream_channels * (5000 + tmpBit)
			if curr_bandwidth == OPUS_BANDWIDTH_SUPERWIDEBAND {
				/* SILK gets 2/3 of the remaining bits */
				st.silk_mode.bitRate += (total_bitRate - st.silk_mode.bitRate) * 2 / 3
			} else {
				/* FULLBAND */
				/* SILK gets 3/5 of the remaining bits */
				st.silk_mode.bitRate += (total_bitRate - st.silk_mode.bitRate) * 3 / 5
			}
			/* Don't let SILK use more than 80% */
			if st.silk_mode.bitRate > total_bitRate*4/5 {
				st.silk_mode.bitRate = total_bitRate * 4 / 5
			}
			if st.energy_masking == nil {
				/* Increasingly attenuate high band when it gets allocated fewer bits */
				celt_rate = total_bitRate - st.silk_mode.bitRate
				HB_gain_ref = 3600
				if curr_bandwidth == OPUS_BANDWIDTH_SUPERWIDEBAND {
					HB_gain_ref = 3000
				}

				HB_gain = SHL32(celt_rate, 9) / SHR32(celt_rate+st.stream_channels*HB_gain_ref, 6)
				if HB_gain < CeltConstants.Q15ONE*6/7 {
					HB_gain = HB_gain + CeltConstants.Q15ONE/7
				} else {
					HB_gain = CeltConstants.Q15ONE
				}

			}
		} else {
			/* SILK gets all bits */
			st.silk_mode.bitRate = total_bitRate
		}

		/* Surround masking for SILK */
		if st.energy_masking != nil && st.use_vbr != 0 && st.lfe == 0 {
			var mask_sum int = 0
			var masking_depth int
			var rate_offset int
			var c int
			var end int = 17
			var srate int16 = 16000
			if st.bandwidth == OPUS_BANDWIDTH_NARROWBAND {
				end = 13
				srate = 8000
			} else if st.bandwidth == OPUS_BANDWIDTH_MEDIUMBAND {
				end = 15
				srate = 12000
			}
			for c = 0; c < st.channels; c++ {
				for i = 0; i < end; i++ {
					var mask int
					mask = MAX16Int(MIN16Int(st.energy_masking[21*c+i], int(math.Trunc(0.5+(.5)*((1)<<(10))))), -int(math.Trunc(0.5+(2.0)*((1)<<(10)))))

					if mask > 0 {
						mask = HALF16Int(mask)
					}
					mask_sum += mask
				}
			}
			/* Conservative rate reduction, we cut the masking in half */
			masking_depth = mask_sum / end * st.channels
			masking_depth += int(int16(math.Trunc(0.5 + (.2)*float64((1)<<(10))))) /*Inlines.QCONST16(.2f, 10)*/
			rate_offset = PSHR32(MULT16_16(int(srate), masking_depth), 10)
			rate_offset = MAX32(rate_offset, -2*st.silk_mode.bitRate/3)
			/* Split the rate change between the SILK and CELT part for hybrid. */
			if st.bandwidth == OPUS_BANDWIDTH_SUPERWIDEBAND || st.bandwidth == OPUS_BANDWIDTH_FULLBAND {
				st.silk_mode.bitRate += 3 * rate_offset / 5
			} else {
				st.silk_mode.bitRate += rate_offset
			}
			bytes_target += rate_offset * frame_size / (8 * st.Fs)
		}

		st.silk_mode.payloadSize_ms = 1000 * frame_size / st.Fs
		st.silk_mode.nChannelsAPI = st.channels
		st.silk_mode.nChannelsInternal = st.stream_channels
		if curr_bandwidth == OPUS_BANDWIDTH_NARROWBAND {
			st.silk_mode.desiredInternalSampleRate = 8000
		} else if curr_bandwidth == OPUS_BANDWIDTH_MEDIUMBAND {
			st.silk_mode.desiredInternalSampleRate = 12000
		} else {
			OpusAssert(st.mode == MODE_HYBRID || curr_bandwidth == OPUS_BANDWIDTH_WIDEBAND)
			st.silk_mode.desiredInternalSampleRate = 16000
		}
		if st.mode == MODE_HYBRID {
			/* Don't allow bandwidth reduction at lowest bitrates in hybrid mode */
			st.silk_mode.minInternalSampleRate = 16000
		} else {
			st.silk_mode.minInternalSampleRate = 8000
		}

		if st.mode == MODE_SILK_ONLY {
			var effective_max_rate = max_rate
			st.silk_mode.maxInternalSampleRate = 16000
			if frame_rate > 50 {
				effective_max_rate = effective_max_rate * 2 / 3
			}
			if effective_max_rate < 13000 {
				st.silk_mode.maxInternalSampleRate = 12000
				st.silk_mode.desiredInternalSampleRate = IMIN(12000, st.silk_mode.desiredInternalSampleRate)
			}
			if effective_max_rate < 9600 {
				st.silk_mode.maxInternalSampleRate = 8000
				st.silk_mode.desiredInternalSampleRate = IMIN(8000, st.silk_mode.desiredInternalSampleRate)
			}
		} else {
			st.silk_mode.maxInternalSampleRate = 16000
		}

		st.silk_mode.useCBR = boolToInt(st.use_vbr == 0)

		/* Call SILK encoder for the low band */
		nBytes = IMIN(1275, max_data_bytes-1-redundancy_bytes)

		st.silk_mode.maxBits = nBytes * 8
		/* Only allow up to 90% of the bits for hybrid mode*/
		if st.mode == MODE_HYBRID {
			st.silk_mode.maxBits = st.silk_mode.maxBits * 9 / 10
		}
		if st.silk_mode.useCBR != 0 {
			st.silk_mode.maxBits = (st.silk_mode.bitRate * frame_size / (st.Fs * 8)) * 8
			/* Reduce the initial target to make it easier to reach the CBR rate */
			st.silk_mode.bitRate = IMAX(1, st.silk_mode.bitRate-2000)
		}

		if prefill != 0 {
			zero := &BoxedValueInt{0}
			var prefill_offset int

			/* Use a smooth onset for the SILK prefill to avoid the encoder trying to encode
			   a discontinuity. The exact location is what we need to avoid leaving any "gap"
			   in the audio when mixing with the redundant CELT frame. Here we can afford to
			   overwrite st.delay_buffer because the only thing that uses it before it gets
			   rewritten is tmp_prefill[] and even then only the part after the ramp really
			   gets used (rather than sent to the encoder and discarded) */
			prefill_offset = st.channels * (st.encoder_buffer - st.delay_compensation - st.Fs/400)
			gain_fade(st.delay_buffer[:], prefill_offset,
				0, CeltConstants.Q15ONE, celt_mode.overlap, st.Fs/400, st.channels, celt_mode.window, st.Fs)
			MemSetLen(st.delay_buffer[:], 0, prefill_offset)
			//System.arraycopy(st.delay_buffer, 0, pcm_silk, 0, st.encoder_buffer*st.channels)
			copy(pcm_silk, st.delay_buffer[:st.encoder_buffer*st.channels])
			silk_Encode(silk_enc, &st.silk_mode, pcm_silk, st.encoder_buffer, nil, zero, 1)
		}

		//System.arraycopy(pcm_buf, total_buffer*st.channels, pcm_silk, 0, frame_size*st.channels)
		copy(pcm_silk, pcm_buf[total_buffer*st.channels:total_buffer*st.channels+frame_size*st.channels])

		boxed_silkBytes := &BoxedValueInt{nBytes}
		ret = silk_Encode(silk_enc, &st.silk_mode, pcm_silk, frame_size, enc, boxed_silkBytes, 0)
		nBytes = boxed_silkBytes.Val

		if ret != 0 {
			/*fprintf (stderr, "SILK encode error: %d\n", ret);*/
			/* Handle error */

			return OpusError.OPUS_INTERNAL_ERROR
		}
		if nBytes == 0 {
			st.rangeFinal = 0
			data[data_ptr-1] = gen_toc(st.mode, st.Fs/frame_size, curr_bandwidth, st.stream_channels)

			return 1
		}
		/* Extract SILK public bandwidth for signaling in first byte */
		if st.mode == MODE_SILK_ONLY {
			if st.silk_mode.internalSampleRate == 8000 {
				curr_bandwidth = OPUS_BANDWIDTH_NARROWBAND
			} else if st.silk_mode.internalSampleRate == 12000 {
				curr_bandwidth = OPUS_BANDWIDTH_MEDIUMBAND
			} else if st.silk_mode.internalSampleRate == 16000 {
				curr_bandwidth = OPUS_BANDWIDTH_WIDEBAND
			}
		} else {
			OpusAssert(st.silk_mode.internalSampleRate == 16000)
		}

		st.silk_mode.opusCanSwitch = st.silk_mode.switchReady
		if st.silk_mode.opusCanSwitch != 0 {
			redundancy = 1
			celt_to_silk = 0
			st.silk_bw_switch = 1
		}
	}

	/* CELT processing */
	{
		var endband int = 21

		switch curr_bandwidth {
		case OPUS_BANDWIDTH_NARROWBAND:
			endband = 13
			break
		case OPUS_BANDWIDTH_MEDIUMBAND:
		case OPUS_BANDWIDTH_WIDEBAND:
			endband = 17
			break
		case OPUS_BANDWIDTH_SUPERWIDEBAND:
			endband = 19
			break
		case OPUS_BANDWIDTH_FULLBAND:
			endband = 21
			break
		}
		celt_enc.SetEndBand(endband)
		celt_enc.SetChannels(st.stream_channels)
	}
	celt_enc.SetBitrate(OPUS_BITRATE_MAX)
	if st.mode != MODE_SILK_ONLY {
		var celt_pred int = 2
		celt_enc.SetVBR(false)
		/* We may still decide to disable prediction later */
		if st.silk_mode.reducedDependency != 0 {
			celt_pred = 0
		}
		celt_enc.SetPrediction(celt_pred)

		if st.mode == MODE_HYBRID {
			var len int

			len = (enc.tell() + 7) >> 3

			if redundancy != 0 {
				if st.mode == MODE_HYBRID {
					len += 3
				} else {
					len += 1
				}
			}
			if st.use_vbr != 0 {
				nb_compr_bytes = len + bytes_target - (st.silk_mode.bitRate*frame_size)/(8*st.Fs)
			} else {
				/* check if SILK used up too much */
				nb_compr_bytes = bytes_target
				if len > bytes_target {
					nb_compr_bytes = len
				}

			}
		} else if st.use_vbr != 0 {
			var bonus int = 0
			if st.analysis.enabled && st.variable_duration == OPUS_FRAMESIZE_VARIABLE && frame_size != st.Fs/50 {
				bonus = (60*st.stream_channels + 40) * (st.Fs/frame_size - 50)
				if analysis_info.valid != 0 {
					bonus = (bonus * int(1.0+.5*analysis_info.tonality))
				}
			}
			celt_enc.SetVBR(true)
			celt_enc.SetVBRConstraint(st.vbr_constraint != 0)
			celt_enc.SetBitrate(st.bitrate_bps + bonus)

			nb_compr_bytes = max_data_bytes - 1 - redundancy_bytes
		} else {
			nb_compr_bytes = bytes_target
		}

	} else {
		nb_compr_bytes = 0
	}

	tmp_prefill := make([]int16, st.channels*st.Fs/400)
	if st.mode != MODE_SILK_ONLY && st.mode != st.prev_mode && (st.prev_mode != MODE_AUTO && st.prev_mode != MODE_UNKNOWN) {
		//System.arraycopy(st.delay_buffer, ((st.encoder_buffer - total_buffer - st.Fs/400) * st.channels), tmp_prefill, 0, st.channels*st.Fs/400)
		copy(tmp_prefill, st.delay_buffer[((st.encoder_buffer-total_buffer-st.Fs/400)*st.channels):((st.encoder_buffer-total_buffer-st.Fs/400)*st.channels)+st.channels*st.Fs/400])
	}

	if st.channels*(st.encoder_buffer-(frame_size+total_buffer)) > 0 {
		MemMove(st.delay_buffer[:], st.channels*frame_size, 0, st.channels*(st.encoder_buffer-frame_size-total_buffer))
		//	System.arraycopy(pcm_buf, 0, st.delay_buffer, (st.channels * (st.encoder_buffer - frame_size - total_buffer)), (frame_size+total_buffer)*st.channels)
		copy(st.delay_buffer[(st.channels*(st.encoder_buffer-frame_size-total_buffer)):], pcm_buf[:(frame_size+total_buffer)*st.channels])

	} else {
		//System.arraycopy(pcm_buf, (frame_size+total_buffer-st.encoder_buffer)*st.channels, st.delay_buffer, 0, st.encoder_buffer*st.channels)
		copy(st.delay_buffer[0:], pcm_buf[(frame_size+total_buffer-st.encoder_buffer)*st.channels:(frame_size+total_buffer-st.encoder_buffer)*st.channels+st.encoder_buffer*st.channels])
	}

	/* gain_fade() and stereo_fade() need to be after the buffer copying
	   because we don't want any of this to affect the SILK part */
	if st.prev_HB_gain < CeltConstants.Q15ONE || HB_gain < CeltConstants.Q15ONE {
		gain_fade(pcm_buf, 0,
			st.prev_HB_gain, HB_gain, celt_mode.overlap, frame_size, st.channels, celt_mode.window, st.Fs)
	}

	st.prev_HB_gain = HB_gain
	if st.mode != MODE_HYBRID || st.stream_channels == 1 {
		st.silk_mode.stereoWidth_Q14 = IMIN((1 << 14), 2*IMAX(0, equiv_rate-30000))
	}
	if st.energy_masking == nil && st.channels == 2 {
		/* Apply stereo width reduction (at low bitrates) */
		if st.hybrid_stereo_width_Q14 < (1<<14) || st.silk_mode.stereoWidth_Q14 < (1<<14) {
			var g1, g2 int
			g1 = int(st.hybrid_stereo_width_Q14)
			g2 = (st.silk_mode.stereoWidth_Q14)
			if g1 == 16384 {
				g1 = CeltConstants.Q15ONE
			} else {
				g1 = SHL16Int(g1, 1)
			}
			if g2 == 16384 {
				g2 = CeltConstants.Q15ONE
			} else {
				g2 = SHL16Int(g2, 1)
			}

			stereo_fade(pcm_buf, g1, g2, celt_mode.overlap,
				frame_size, st.channels, celt_mode.window, st.Fs)
			st.hybrid_stereo_width_Q14 = int16(st.silk_mode.stereoWidth_Q14)
		}
	}

	if st.mode != MODE_CELT_ONLY && enc.tell()+17+20*(boolToInt(st.mode == MODE_HYBRID)) <= 8*(max_data_bytes-1) {
		/* For SILK mode, the redundancy is inferred from the length */
		if st.mode == MODE_HYBRID && (redundancy != 0 || enc.tell()+37 <= 8*nb_compr_bytes) {
			enc.enc_bit_logp(redundancy, 12)
		}
		if redundancy != 0 {
			var max_redundancy int
			enc.enc_bit_logp(celt_to_silk, 1)
			if st.mode == MODE_HYBRID {
				max_redundancy = (max_data_bytes - 1) - nb_compr_bytes
			} else {
				max_redundancy = (max_data_bytes - 1) - ((enc.tell() + 7) >> 3)
			}
			/* Target the same bit-rate for redundancy as for the rest,
			   up to a max of 257 bytes */
			redundancy_bytes = IMIN(max_redundancy, st.bitrate_bps/1600)
			redundancy_bytes = IMIN(257, IMAX(2, redundancy_bytes))
			if st.mode == MODE_HYBRID {
				enc.enc_uint(int64(redundancy_bytes-2), 256)
			}
		}
	} else {
		redundancy = 0
	}

	if redundancy == 0 {
		st.silk_bw_switch = 0
		redundancy_bytes = 0
	}
	if st.mode != MODE_CELT_ONLY {
		start_band = 17
	}

	if st.mode == MODE_SILK_ONLY {
		ret = (enc.tell() + 7) >> 3
		enc.enc_done()
		nb_compr_bytes = ret
	} else {
		nb_compr_bytes = IMIN((max_data_bytes-1)-redundancy_bytes, nb_compr_bytes)
		enc.enc_shrink(nb_compr_bytes)
	}

	if st.analysis.enabled && redundancy != 0 || st.mode != MODE_SILK_ONLY {
		analysis_info.enabled = st.analysis.enabled
		celt_enc.SetAnalysis(&analysis_info)
	}
	/* 5 ms redundant frame for CELT->SILK */
	if redundancy != 0 && celt_to_silk != 0 {
		var err int
		celt_enc.SetStartBand(0)
		celt_enc.SetVBR(false)
		err = celt_enc.celt_encode_with_ec(pcm_buf, 0, st.Fs/200, data, data_ptr+nb_compr_bytes, redundancy_bytes, nil)

		if err < 0 {
			return OpusError.OPUS_INTERNAL_ERROR
		}
		redundant_rng = celt_enc.GetFinalRange()
		celt_enc.ResetState()
	}

	celt_enc.SetStartBand(start_band)

	if st.mode != MODE_SILK_ONLY {
		if st.mode != st.prev_mode && (st.prev_mode != MODE_AUTO && st.prev_mode != MODE_UNKNOWN) {
			dummy := make([]byte, 2)
			celt_enc.ResetState()

			/* Prefilling */
			celt_enc.celt_encode_with_ec(tmp_prefill, 0, st.Fs/400, dummy, 0, 2, nil)
			celt_enc.SetPrediction(0)
		}
		/* If false, we already busted the budget and we'll end up with a "PLC packet" */
		if enc.tell() <= 8*nb_compr_bytes {

			// Arrays.printObjectFields(this);
			ret = celt_enc.celt_encode_with_ec(pcm_buf, 0, frame_size, nil, 0, nb_compr_bytes, enc)

			if ret < 0 {
				return OpusError.OPUS_INTERNAL_ERROR
			}
		}
	}

	/* 5 ms redundant frame for SILK->CELT */
	if redundancy != 0 && celt_to_silk == 0 {
		var err int
		dummy := make([]byte, 2)
		var N2, N4 int
		N2 = st.Fs / 200
		N4 = st.Fs / 400

		celt_enc.ResetState()
		celt_enc.SetStartBand(0)
		celt_enc.SetPrediction(0)

		/* NOTE: We could speed this up slightly (at the expense of code size) by just adding a function that prefills the buffer */
		celt_enc.celt_encode_with_ec(pcm_buf, (st.channels * (frame_size - N2 - N4)), N4, dummy, 0, 2, nil)

		err = celt_enc.celt_encode_with_ec(pcm_buf, (st.channels * (frame_size - N2)), N2, data, data_ptr+nb_compr_bytes, redundancy_bytes, nil)
		if err < 0 {
			return OpusError.OPUS_INTERNAL_ERROR
		}
		redundant_rng = celt_enc.GetFinalRange()
	}

	/* Signalling the mode in the first byte */
	data_ptr -= 1
	data[data_ptr] = gen_toc(st.mode, st.Fs/frame_size, curr_bandwidth, st.stream_channels)

	st.rangeFinal = int(enc.rng) ^ redundant_rng

	if to_celt != 0 {
		st.prev_mode = MODE_CELT_ONLY
	} else {
		st.prev_mode = st.mode
	}
	st.prev_channels = st.stream_channels
	st.prev_framesize = frame_size

	st.first = 0

	/* In the unlikely case that the SILK encoder busted its target, tell
	   the decoder to call the PLC */
	if enc.tell() > (max_data_bytes-1)*8 {
		if max_data_bytes < 2 {
			return OpusError.OPUS_BUFFER_TOO_SMALL
		}
		data[data_ptr+1] = 0
		ret = 1
		st.rangeFinal = 0
	} else if st.mode == MODE_SILK_ONLY && redundancy == 0 {
		/*When in LPC only mode it's perfectly
		  reasonable to strip off trailing zero bytes as
		  the required range decoder behavior is to
		  fill these in. This can't be done when the MDCT
		  modes are used because the decoder needs to know
		  the actual length for allocation purposes.*/
		for ret > 2 && data[data_ptr+ret] == 0 {
			ret--
		}
	}
	/* Count ToC and redundancy */
	ret += 1 + redundancy_bytes
	if st.use_vbr == 0 {
		if PadPacket(data, data_ptr, ret, max_data_bytes) != OpusError.OPUS_OK {
			return OpusError.OPUS_INTERNAL_ERROR
		}
		ret = max_data_bytes
	}

	return ret
}

func formatSignedBytes(data []byte) string {
	var builder strings.Builder
	builder.WriteString("[")
	for i, b := range data {
		// 转换为有符号整数
		signed := int8(b)

		if i > 0 {
			builder.WriteString(", ")
		}
		builder.WriteString(strconv.Itoa(int(signed)))
	}
	builder.WriteString("]")
	return builder.String()
}
func (st *OpusEncoder) Encode(in_pcm []int16, pcm_offset, frame_size int, out_data []byte, out_data_offset, max_data_bytes int) (int, error) {

	if out_data_offset+max_data_bytes > len(out_data) {
		return 0, errors.New("Output buffer is too small")
	}
	delay_compensation := st.delay_compensation
	if st.application == OPUS_APPLICATION_RESTRICTED_LOWDELAY {
		delay_compensation = 0
	}
	internal_frame_size := compute_frame_size(in_pcm, pcm_offset, frame_size, st.variable_duration, st.channels, st.Fs, st.bitrate_bps, delay_compensation, st.analysis.subframe_mem, st.analysis.enabled)
	if pcm_offset+internal_frame_size > len(in_pcm) {
		return 0, errors.New("Not enough samples provided in input signal")
	}

	ret := st.opus_encode_native(in_pcm, pcm_offset, internal_frame_size, out_data, out_data_offset, max_data_bytes, 16, in_pcm, pcm_offset, frame_size, 0, -2, st.channels, 0)
	if ret < 0 {
		if ret == OpusError.OPUS_BAD_ARG {
			return 0, errors.New("OPUS_BAD_ARG while encoding")
		}
		return 0, errors.New("An error occurred during encoding")
	}
	return ret, nil
}

func (st *OpusEncoder) GetApplication() OpusApplication {
	return st.application
}

func (st *OpusEncoder) SetApplication(value OpusApplication) {
	if st.first == 0 && st.application != value {
		panic("Application cannot be changed after encoding has started")
	}
	st.application = value
}

func (st *OpusEncoder) GetBitrate() int {
	return st.user_bitrate_to_bitrate(st.prev_framesize, 1276)
}

func (st *OpusEncoder) SetBitrate(value int) {
	if value != OPUS_AUTO && value != OPUS_BITRATE_MAX {
		if value <= 0 {
			panic("Bitrate must be positive")
		} else if value <= 500 {
			value = 500
		} else if value > 300000*st.channels {
			value = 300000 * st.channels
		}
	}
	st.user_bitrate_bps = value
}

func (st *OpusEncoder) GetForceChannels() int {
	return st.force_channels
}

func (st *OpusEncoder) SetForceChannels(value int) {
	if (value < 1 || value > st.channels) && value != OPUS_AUTO {
		panic("Force channels must be <= num. of channels")
	}
	st.force_channels = value
}

func (st *OpusEncoder) GetMaxBandwidth() int {
	return st.max_bandwidth
}

func (st *OpusEncoder) SetMaxBandwidth(value int) {
	st.max_bandwidth = value
	if value == OPUS_BANDWIDTH_NARROWBAND {
		st.silk_mode.maxInternalSampleRate = 8000
	} else if value == OPUS_BANDWIDTH_MEDIUMBAND {
		st.silk_mode.maxInternalSampleRate = 12000
	} else {
		st.silk_mode.maxInternalSampleRate = 16000
	}
}

func (st *OpusEncoder) GetBandwidth() int {
	return st.bandwidth
}

func (st *OpusEncoder) SetBandwidth(value int) {
	st.user_bandwidth = value
	if value == OPUS_BANDWIDTH_NARROWBAND {
		st.silk_mode.maxInternalSampleRate = 8000
	} else if value == OPUS_BANDWIDTH_MEDIUMBAND {
		st.silk_mode.maxInternalSampleRate = 12000
	} else {
		st.silk_mode.maxInternalSampleRate = 16000
	}
}

func (st *OpusEncoder) GetUseDTX() bool {
	return st.silk_mode.useDTX != 0
}

func (st *OpusEncoder) SetUseDTX(value bool) {
	if value {
		st.silk_mode.useDTX = 1
	} else {
		st.silk_mode.useDTX = 0
	}
}

func (st *OpusEncoder) GetComplexity() int {
	return st.silk_mode.complexity
}

func (st *OpusEncoder) SetComplexity(value int) {
	if value < 0 || value > 10 {
		panic("Complexity must be between 0 and 10")
	}
	st.silk_mode.complexity = value
	st.Celt_Encoder.SetComplexity(value)
}

func (st *OpusEncoder) GetUseInbandFEC() bool {
	return st.silk_mode.useInBandFEC != 0
}

func (st *OpusEncoder) SetUseInbandFEC(value bool) {
	if value {
		st.silk_mode.useInBandFEC = 1
	} else {
		st.silk_mode.useInBandFEC = 0
	}
}

func (st *OpusEncoder) GetPacketLossPercent() int {
	return st.silk_mode.packetLossPercentage
}

func (st *OpusEncoder) SetPacketLossPercent(value int) {
	if value < 0 || value > 100 {
		panic("Packet loss must be between 0 and 100")
	}
	st.silk_mode.packetLossPercentage = value
	st.Celt_Encoder.SetPacketLossPercent(value)
}

func (st *OpusEncoder) GetUseVBR() bool {
	return st.use_vbr != 0
}

func (st *OpusEncoder) SetUseVBR(value bool) {
	if value {
		st.use_vbr = 1
		st.silk_mode.useCBR = 0
	} else {
		st.use_vbr = 0
		st.silk_mode.useCBR = 1
	}
}

func (st *OpusEncoder) GetUseConstrainedVBR() bool {
	return st.vbr_constraint != 0
}

func (st *OpusEncoder) SetUseConstrainedVBR(value bool) {
	if value {
		st.vbr_constraint = 1
	} else {
		st.vbr_constraint = 0
	}
}

func (st *OpusEncoder) GetSignalType() OpusSignal {
	return st.signal_type
}

func (st *OpusEncoder) SetSignalType(value OpusSignal) {
	st.signal_type = value
}

func (st *OpusEncoder) GetLookahead() int {
	returnVal := st.Fs / 400
	if st.application != OPUS_APPLICATION_RESTRICTED_LOWDELAY {
		returnVal += st.delay_compensation
	}
	return returnVal
}

func (st *OpusEncoder) GetSampleRate() int {
	return st.Fs
}

func (st *OpusEncoder) GetFinalRange() int {
	return st.rangeFinal
}

func (st *OpusEncoder) GetLSBDepth() int {
	return st.lsb_depth
}

func (st *OpusEncoder) SetLSBDepth(value int) {
	if value < 8 || value > 24 {
		panic("LSB depth must be between 8 and 24")
	}
	st.lsb_depth = value
}

func (st *OpusEncoder) GetExpertFrameDuration() OpusFramesize {
	return st.variable_duration
}

func (st *OpusEncoder) SetExpertFrameDuration(value OpusFramesize) {
	st.variable_duration = value
	st.Celt_Encoder.SetExpertFrameDuration(value)
}

func (st *OpusEncoder) GetForceMode() int {
	return st.user_forced_mode
}

func (st *OpusEncoder) SetForceMode(value int) {
	st.user_forced_mode = value
}

func (st *OpusEncoder) GetIsLFE() bool {
	return st.lfe != 0
}

func (st *OpusEncoder) SetIsLFE(value bool) {
	if value {
		st.lfe = 1
	} else {
		st.lfe = 0
	}
	st.Celt_Encoder.SetLFE(btol(value))
}

func (st *OpusEncoder) GetPredictionDisabled() bool {
	return st.silk_mode.reducedDependency != 0
}

func (st *OpusEncoder) SetPredictionDisabled(value bool) {
	if value {
		st.silk_mode.reducedDependency = 1
	} else {
		st.silk_mode.reducedDependency = 0
	}
}

func (st *OpusEncoder) GetEnableAnalysis() bool {
	return st.analysis.enabled
}

func (st *OpusEncoder) SetEnableAnalysis(value bool) {
	st.analysis.enabled = value
}

func (st *OpusEncoder) SetEnergyMask(value []int) {
	st.energy_masking = value
	st.Celt_Encoder.SetEnergyMask(value)
}

func (st *OpusEncoder) GetCeltMode() *CeltMode {
	return st.Celt_Encoder.GetMode()
}

func btol(b bool) int {
	if b {
		return 1
	}
	return 0
}

func imin(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func imax(a, b int) int {
	if a > b {
		return a
	}
	return b
}
