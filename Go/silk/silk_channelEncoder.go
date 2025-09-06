package silk

import (
	"math"

	"github.com/dosgo/concentus/go/comm"
	"github.com/dosgo/concentus/go/comm/arrayUtil"
)

type SilkChannelEncoder struct {
	In_HP_State                   [2]int
	Variable_HP_smth1_Q15         int
	variable_HP_smth2_Q15         int
	SLP                           *SilkLPState
	sVAD                          *SilkVADState
	SNSQ                          *SilkNSQState
	Prev_NLSFq_Q15                []int16
	Speech_activity_Q8            int
	Allow_bandwidth_switch        int
	LBRRprevLastGainIndex         byte
	PrevSignalType                byte
	PrevLag                       int
	pitch_LPC_win_length          int
	max_pitch_lag                 int
	API_fs_Hz                     int
	prev_API_fs_Hz                int
	MaxInternal_fs_Hz             int
	MinInternal_fs_Hz             int
	DesiredInternal_fs_Hz         int
	Fs_kHz                        int
	nb_subfr                      int
	Frame_length                  int
	subfr_length                  int
	ltp_mem_length                int
	la_pitch                      int
	la_shape                      int
	shapeWinLength                int
	TargetRate_bps                int
	PacketSize_ms                 int
	PacketLoss_perc               int
	frameCounter                  int
	Complexity                    int
	nStatesDelayedDecision        int
	useInterpolatedNLSFs          int
	shapingLPCOrder               int
	predictLPCOrder               int
	pitchEstimationComplexity     int
	pitchEstimationLPCOrder       int
	pitchEstimationThreshold_Q16  int
	LTPQuantLowComplexity         int
	mu_LTP_Q9                     int
	sum_log_gain_Q7               int
	NLSF_MSVQ_Survivors           int
	First_frame_after_reset       int
	Controlled_since_last_payload int
	warping_Q16                   int
	UseCBR                        int
	PrefillFlag                   int
	pitch_lag_low_bits_iCDF       []int16
	pitch_contour_iCDF            []int16
	psNLSF_CB                     *NLSFCodebook
	Input_quality_bands_Q15       [VAD_N_BANDS]int
	input_tilt_Q15                int
	SNR_dB_Q7                     int
	VAD_flags                     [MAX_FRAMES_PER_PACKET]byte
	LBRR_flag                     byte
	LBRR_flags                    [MAX_FRAMES_PER_PACKET]int
	indices                       *SideInfoIndices
	pulses                        []int8
	InputBuf                      []int16
	InputBufIx                    int
	NFramesPerPacket              int
	NFramesEncoded                int
	NChannelsAPI                  int
	NChannelsInternal             int
	channelNb                     int
	frames_since_onset            int
	ec_prevSignalType             int
	ec_prevLagIndex               int16
	Resampler_state               *SilkResamplerState
	UseDTX                        int
	InDTX                         int
	noSpeechCounter               int
	UseInBandFEC                  int
	LBRR_enabled                  int
	LBRR_GainIncreases            int
	Indices_LBRR                  []*SideInfoIndices
	Pulses_LBRR                   [MAX_FRAMES_PER_PACKET][]int8
	SShape                        *SilkShapeState
	SPrefilt                      *SilkPrefilterState
	x_buf                         [2*MAX_FRAME_LENGTH + LA_SHAPE_MAX]int16
	LTPCorr_Q15                   int
}

func NewSilkChannelEncoder() *SilkChannelEncoder {
	obj := &SilkChannelEncoder{}

	obj.SShape = &SilkShapeState{}
	obj.SPrefilt = &SilkPrefilterState{}
	/*
	   /* State of second smoother                                         */
	obj.SLP = NewSilkLPState()
	/* Low pass filter state                                            */
	obj.sVAD = NewSilkVADState()
	/* Voice activity detector state                                    */
	obj.SNSQ = NewSilkNSQState()
	/* Noise Shape Quantizer State                                      */
	obj.Prev_NLSFq_Q15 = make([]int16, SilkConstants.MAX_LPC_ORDER)
	/* Previously quantized NLSF vector                                 */

	/* Flag for ensuring codec_control only runs once per packet        */

	obj.indices = NewSideInfoIndices()

	obj.Indices_LBRR = make([]*SideInfoIndices, MAX_FRAMES_PER_PACKET)
	obj.Resampler_state = NewSilkResamplerState()
	obj.InputBuf = make([]int16, SilkConstants.MAX_FRAME_LENGTH+2)
	obj.pulses = make([]int8, SilkConstants.MAX_FRAME_LENGTH)

	for i := 0; i < MAX_FRAMES_PER_PACKET; i++ {
		obj.Indices_LBRR[i] = NewSideInfoIndices()
	}
	return obj
}
func (s *SilkChannelEncoder) Reset() {
	for i := range s.In_HP_State {
		s.In_HP_State[i] = 0
	}
	s.Variable_HP_smth1_Q15 = 0
	s.variable_HP_smth2_Q15 = 0
	s.SLP.Reset()
	s.sVAD.Reset()
	s.SNSQ.Reset()
	for i := range s.Prev_NLSFq_Q15 {
		s.Prev_NLSFq_Q15[i] = 0
	}
	s.Speech_activity_Q8 = 0
	s.Allow_bandwidth_switch = 0
	s.LBRRprevLastGainIndex = 0
	s.PrevSignalType = 0
	s.PrevLag = 0
	s.pitch_LPC_win_length = 0
	s.max_pitch_lag = 0
	s.API_fs_Hz = 0
	s.prev_API_fs_Hz = 0
	s.MaxInternal_fs_Hz = 0
	s.MinInternal_fs_Hz = 0
	s.DesiredInternal_fs_Hz = 0
	s.Fs_kHz = 0
	s.nb_subfr = 0
	s.Frame_length = 0
	s.subfr_length = 0
	s.ltp_mem_length = 0
	s.la_pitch = 0
	s.la_shape = 0
	s.shapeWinLength = 0
	s.TargetRate_bps = 0
	s.PacketSize_ms = 0
	s.PacketLoss_perc = 0
	s.frameCounter = 0
	s.Complexity = 0
	s.nStatesDelayedDecision = 0
	s.useInterpolatedNLSFs = 0
	s.shapingLPCOrder = 0
	s.predictLPCOrder = 0
	s.pitchEstimationComplexity = 0
	s.pitchEstimationLPCOrder = 0
	s.pitchEstimationThreshold_Q16 = 0
	s.LTPQuantLowComplexity = 0
	s.mu_LTP_Q9 = 0
	s.sum_log_gain_Q7 = 0
	s.NLSF_MSVQ_Survivors = 0
	s.First_frame_after_reset = 0
	s.Controlled_since_last_payload = 0
	s.warping_Q16 = 0
	s.UseCBR = 0
	s.PrefillFlag = 0
	s.pitch_lag_low_bits_iCDF = nil
	s.pitch_contour_iCDF = nil
	s.psNLSF_CB = nil
	for i := range s.Input_quality_bands_Q15 {
		s.Input_quality_bands_Q15[i] = 0
	}
	s.input_tilt_Q15 = 0
	s.SNR_dB_Q7 = 0
	for i := range s.VAD_flags {
		s.VAD_flags[i] = 0
	}
	s.LBRR_flag = 0
	for i := range s.LBRR_flags {
		s.LBRR_flags[i] = 0
	}
	s.indices.Reset()
	for i := range s.pulses {
		s.pulses[i] = 0
	}
	for i := range s.InputBuf {
		s.InputBuf[i] = 0
	}
	s.InputBufIx = 0
	s.NFramesPerPacket = 0
	s.NFramesEncoded = 0
	s.NChannelsAPI = 0
	s.NChannelsInternal = 0
	s.channelNb = 0
	s.frames_since_onset = 0
	s.ec_prevSignalType = 0
	s.ec_prevLagIndex = 0
	s.Resampler_state.Reset()
	s.UseDTX = 0
	s.InDTX = 0
	s.noSpeechCounter = 0
	s.UseInBandFEC = 0
	s.LBRR_enabled = 0
	s.LBRR_GainIncreases = 0
	for c := 0; c < SilkConstants.MAX_FRAMES_PER_PACKET; c++ {
		s.Indices_LBRR[c].Reset()
		for i := range s.Pulses_LBRR[c] {
			s.Pulses_LBRR[c][i] = 0
		}
	}
	s.SShape.Reset()
	s.SPrefilt.Reset()
	for i := range s.x_buf {
		s.x_buf[i] = 0
	}
	s.LTPCorr_Q15 = 0
}

func (s *SilkChannelEncoder) Silk_control_encoder(encControl *EncControlState, TargetRate_bps int, allow_bw_switch int, channelNb int, force_fs_kHz int) int {
	var fs_kHz int
	ret := SilkError.SILK_NO_ERROR

	s.UseDTX = encControl.UseDTX
	s.UseCBR = encControl.UseCBR
	s.API_fs_Hz = encControl.API_sampleRate
	s.MaxInternal_fs_Hz = encControl.MaxInternalSampleRate
	s.MinInternal_fs_Hz = encControl.MinInternalSampleRate
	s.DesiredInternal_fs_Hz = encControl.DesiredInternalSampleRate
	s.UseInBandFEC = encControl.UseInBandFEC
	s.NChannelsAPI = encControl.NChannelsAPI
	s.NChannelsInternal = encControl.NChannelsInternal
	s.Allow_bandwidth_switch = allow_bw_switch
	s.channelNb = channelNb

	if s.Controlled_since_last_payload != 0 && s.PrefillFlag == 0 {
		if s.API_fs_Hz != s.prev_API_fs_Hz && s.Fs_kHz > 0 {
			ret = s.silk_setup_resamplers(s.Fs_kHz)
		}
		return ret
	}

	fs_kHz = s.silk_control_audio_bandwidth(encControl)
	if force_fs_kHz != 0 {
		fs_kHz = force_fs_kHz
	}

	ret = s.silk_setup_resamplers(fs_kHz)
	ret = s.silk_setup_fs(fs_kHz, encControl.PayloadSize_ms)
	ret = s.silk_setup_complexity(encControl.Complexity)
	s.PacketLoss_perc = encControl.PacketLossPercentage
	ret = s.silk_setup_LBRR(TargetRate_bps)
	s.Controlled_since_last_payload = 1
	return ret
}

func (s *SilkChannelEncoder) silk_setup_resamplers(fs_kHz int) int {
	ret := int(0)
	if s.Fs_kHz != fs_kHz || s.prev_API_fs_Hz != s.API_fs_Hz {
		if s.Fs_kHz == 0 {
			ret += silk_resampler_init(s.Resampler_state, s.API_fs_Hz, fs_kHz*1000, 1)
		} else {
			var x_buf_API_fs_Hz []int16
			var temp_resampler_state SilkResamplerState
			api_buf_samples := int(0)
			old_buf_samples := int(0)
			buf_length_ms := int(0)

			buf_length_ms = inlines.Silk_LSHIFT(s.nb_subfr*5, 1) + SilkConstants.LA_SHAPE_MS
			old_buf_samples = buf_length_ms * s.Fs_kHz
			temp_resampler_state.Reset()
			ret += silk_resampler_init(&temp_resampler_state, inlines.Silk_SMULBB(s.Fs_kHz, 1000), s.API_fs_Hz, 0)
			api_buf_samples = buf_length_ms * inlines.Silk_DIV32_16(s.API_fs_Hz, 1000)
			x_buf_API_fs_Hz = make([]int16, api_buf_samples)
			ret += Silk_resampler(&temp_resampler_state, x_buf_API_fs_Hz, 0, s.x_buf[:], 0, old_buf_samples)
			ret += silk_resampler_init(s.Resampler_state, s.API_fs_Hz, inlines.Silk_SMULBB(fs_kHz, 1000), 1)
			ret += Silk_resampler(s.Resampler_state, s.x_buf[:], 0, x_buf_API_fs_Hz, 0, api_buf_samples)
		}
	}
	s.prev_API_fs_Hz = s.API_fs_Hz
	return ret
}

func (s *SilkChannelEncoder) silk_setup_fs(fs_kHz int, PacketSize_ms int) int {
	ret := SilkError.SILK_NO_ERROR

	/* Set packet size */
	if PacketSize_ms != s.PacketSize_ms {
		if (PacketSize_ms != 10) &&
			(PacketSize_ms != 20) &&
			(PacketSize_ms != 40) &&
			(PacketSize_ms != 60) {
			ret = SilkError.SILK_ENC_PACKET_SIZE_NOT_SUPPORTED
		}
		if PacketSize_ms <= 10 {
			s.NFramesPerPacket = 1
			if PacketSize_ms == 10 {
				s.nb_subfr = 2
			} else {
				s.nb_subfr = 1
			}

			s.Frame_length = inlines.Silk_SMULBB(PacketSize_ms, fs_kHz)
			s.pitch_LPC_win_length = inlines.Silk_SMULBB(SilkConstants.FIND_PITCH_LPC_WIN_MS_2_SF, fs_kHz)
			if s.Fs_kHz == 8 {
				s.pitch_contour_iCDF = silk_pitch_contour_10_ms_NB_iCDF
			} else {
				s.pitch_contour_iCDF = silk_pitch_contour_10_ms_iCDF
			}
		} else {
			s.NFramesPerPacket = inlines.Silk_DIV32_16(PacketSize_ms, SilkConstants.MAX_FRAME_LENGTH_MS)
			s.nb_subfr = SilkConstants.MAX_NB_SUBFR
			s.Frame_length = inlines.Silk_SMULBB(20, fs_kHz)
			s.pitch_LPC_win_length = inlines.Silk_SMULBB(SilkConstants.FIND_PITCH_LPC_WIN_MS, fs_kHz)
			if s.Fs_kHz == 8 {
				s.pitch_contour_iCDF = silk_pitch_contour_NB_iCDF
			} else {
				s.pitch_contour_iCDF = silk_pitch_contour_iCDF
			}
		}
		s.PacketSize_ms = PacketSize_ms
		s.TargetRate_bps = 0
		/* trigger new SNR computation */
	}

	/* Set sampling frequency */
	inlines.OpusAssert(fs_kHz == 8 || fs_kHz == 12 || fs_kHz == 16)
	inlines.OpusAssert(s.nb_subfr == 2 || s.nb_subfr == 4)
	if s.Fs_kHz != fs_kHz {
		/* reset part of the state */
		s.SShape.Reset()
		s.SPrefilt.Reset()
		s.SNSQ.Reset()
		arrayUtil.MemSetLen(s.Prev_NLSFq_Q15, 0, SilkConstants.MAX_LPC_ORDER)
		arrayUtil.MemSetLen(s.SLP.In_LP_State[:], 0, 2)
		s.InputBufIx = 0
		s.NFramesEncoded = 0
		s.TargetRate_bps = 0
		/* trigger new SNR computation */

		/* Initialize non-zero parameters */
		s.PrevLag = 100
		s.First_frame_after_reset = 1
		s.SPrefilt.lagPrev = 100
		s.SShape.LastGainIndex = 10
		s.SNSQ.LagPrev = 100
		s.SNSQ.Prev_gain_Q16 = 65536
		s.PrevSignalType = TYPE_NO_VOICE_ACTIVITY

		s.Fs_kHz = fs_kHz
		if s.Fs_kHz == 8 {
			if s.nb_subfr == SilkConstants.MAX_NB_SUBFR {
				s.pitch_contour_iCDF = silk_pitch_contour_NB_iCDF
			} else {
				s.pitch_contour_iCDF = silk_pitch_contour_10_ms_NB_iCDF
			}
		} else if s.nb_subfr == SilkConstants.MAX_NB_SUBFR {
			s.pitch_contour_iCDF = silk_pitch_contour_iCDF
		} else {
			s.pitch_contour_iCDF = silk_pitch_contour_10_ms_iCDF
		}

		if s.Fs_kHz == 8 || s.Fs_kHz == 12 {
			s.predictLPCOrder = SilkConstants.MIN_LPC_ORDER
			s.psNLSF_CB = silk_NLSF_CB_NB_MB
		} else {
			s.predictLPCOrder = SilkConstants.MAX_LPC_ORDER
			s.psNLSF_CB = silk_NLSF_CB_WB
		}

		s.subfr_length = SilkConstants.SUB_FRAME_LENGTH_MS * fs_kHz
		s.Frame_length = inlines.Silk_SMULBB(s.subfr_length, s.nb_subfr)
		s.ltp_mem_length = inlines.Silk_SMULBB(SilkConstants.LTP_MEM_LENGTH_MS, fs_kHz)
		s.la_pitch = inlines.Silk_SMULBB(SilkConstants.LA_PITCH_MS, fs_kHz)
		s.max_pitch_lag = inlines.Silk_SMULBB(18, fs_kHz)

		if s.nb_subfr == SilkConstants.MAX_NB_SUBFR {
			s.pitch_LPC_win_length = inlines.Silk_SMULBB(SilkConstants.FIND_PITCH_LPC_WIN_MS, fs_kHz)
		} else {
			s.pitch_LPC_win_length = inlines.Silk_SMULBB(SilkConstants.FIND_PITCH_LPC_WIN_MS_2_SF, fs_kHz)
		}

		if s.Fs_kHz == 16 {
			s.mu_LTP_Q9 = ((int)(float64(TuningParameters.MU_LTP_QUANT_WB)*float64(int64(1)<<(9)) + 0.5))
			s.pitch_lag_low_bits_iCDF = silk_uniform8_iCDF
		} else if s.Fs_kHz == 12 {
			s.mu_LTP_Q9 = ((int)(float64(TuningParameters.MU_LTP_QUANT_MB)*float64(int64(1)<<(9)) + 0.5))
			s.pitch_lag_low_bits_iCDF = silk_uniform6_iCDF
		} else {
			s.mu_LTP_Q9 = ((int)(float64(TuningParameters.MU_LTP_QUANT_NB)*float64(int64(1)<<(9)) + 0.5))
			s.pitch_lag_low_bits_iCDF = silk_uniform4_iCDF
		}
	}

	/* Check that settings are valid */
	inlines.OpusAssert((s.subfr_length * s.nb_subfr) == s.Frame_length)

	return ret
}

func (s *SilkChannelEncoder) silk_setup_complexity(Complexity int) int {
	ret := int(0)
	inlines.OpusAssert(Complexity >= 0 && Complexity <= 10)
	if Complexity < 2 {
		s.pitchEstimationComplexity = SilkConstants.SILK_PE_MIN_COMPLEX
		//s.pitchEstimationThreshold_Q16 = inlines.Silk_SMULWB(0.8, 1<<16)
		s.pitchEstimationThreshold_Q16 = int(math.Trunc(0.8*(1<<(16)) + 0.5)) /*Inlines.SILK_CONST(0.8f, 16)*/

		s.pitchEstimationLPCOrder = 6
		s.shapingLPCOrder = 8
		s.la_shape = 3 * s.Fs_kHz
		s.nStatesDelayedDecision = 1
		s.useInterpolatedNLSFs = 0
		s.LTPQuantLowComplexity = 1
		s.NLSF_MSVQ_Survivors = 2
		s.warping_Q16 = 0
	} else if Complexity < 4 {
		s.pitchEstimationComplexity = SilkConstants.SILK_PE_MID_COMPLEX
		//s.pitchEstimationThreshold_Q16 = inlines.Silk_SMULWB(0.76, 1<<16)
		s.pitchEstimationThreshold_Q16 = int(math.Trunc(0.76*(1<<(16)) + 0.5)) /*Inlines.SILK_CONST(0.76f, 16)*/

		s.pitchEstimationLPCOrder = 8
		s.shapingLPCOrder = 10
		s.la_shape = 5 * s.Fs_kHz
		s.nStatesDelayedDecision = 1
		s.useInterpolatedNLSFs = 0
		s.LTPQuantLowComplexity = 0
		s.NLSF_MSVQ_Survivors = 4
		s.warping_Q16 = 0
	} else if Complexity < 6 {
		s.pitchEstimationComplexity = SilkConstants.SILK_PE_MID_COMPLEX
		//s.pitchEstimationThreshold_Q16 = inlines.Silk_SMULWB(0.74, 1<<16)
		s.pitchEstimationThreshold_Q16 = int(math.Trunc(0.74*(1<<(16)) + 0.5)) /*Inlines.SILK_CONST(0.74f, 16)*/

		s.pitchEstimationLPCOrder = 10
		s.shapingLPCOrder = 12
		s.la_shape = 5 * s.Fs_kHz
		s.nStatesDelayedDecision = 2
		s.useInterpolatedNLSFs = 1
		s.LTPQuantLowComplexity = 0
		s.NLSF_MSVQ_Survivors = 8
		//s.warping_Q16 = s.fs_kHz * inlines.Silk_SMULWB(TuningParameters.WARPING_MULTIPLIER, 1<<16)
		s.warping_Q16 = s.Fs_kHz * int((TuningParameters.WARPING_MULTIPLIER)*(1<<(16))+0.5)
	} else if Complexity < 8 {
		s.pitchEstimationComplexity = SilkConstants.SILK_PE_MID_COMPLEX
		//s.pitchEstimationThreshold_Q16 = inlines.Silk_SMULWB(0.72, 1<<16)
		s.pitchEstimationThreshold_Q16 = int(math.Trunc((0.72)*(1<<(16)) + 0.5)) /*Inlines.SILK_CONST(0.72f, 16)*/
		s.pitchEstimationLPCOrder = 12
		s.shapingLPCOrder = 14
		s.la_shape = 5 * s.Fs_kHz
		s.nStatesDelayedDecision = 3
		s.useInterpolatedNLSFs = 1
		s.LTPQuantLowComplexity = 0
		s.NLSF_MSVQ_Survivors = 16
		//s.warping_Q16 = s.fs_kHz * inlines.Silk_SMULWB(TuningParameters.WARPING_MULTIPLIER, 1<<16)
		s.warping_Q16 = s.Fs_kHz * (int((TuningParameters.WARPING_MULTIPLIER)*(1<<(16)) + 0.5))
	} else {
		s.pitchEstimationComplexity = SilkConstants.SILK_PE_MAX_COMPLEX
		//s.pitchEstimationThreshold_Q16 = inlines.Silk_SMULWB(0.7, 1<<16)
		s.pitchEstimationThreshold_Q16 = int(math.Trunc(0.7*(1<<(16)) + 0.5))

		s.pitchEstimationLPCOrder = 16
		s.shapingLPCOrder = 16
		s.la_shape = 5 * s.Fs_kHz
		s.nStatesDelayedDecision = SilkConstants.MAX_DEL_DEC_STATES
		s.useInterpolatedNLSFs = 1
		s.LTPQuantLowComplexity = 0
		s.NLSF_MSVQ_Survivors = 32
		//s.warping_Q16 = s.fs_kHz * inlines.Silk_SMULWB(TuningParameters.WARPING_MULTIPLIER, 1<<16)
		s.warping_Q16 = s.Fs_kHz * int(((TuningParameters.WARPING_MULTIPLIER)*(1<<(16)) + 0.5))

	}
	s.pitchEstimationLPCOrder = inlines.Silk_min_int(s.pitchEstimationLPCOrder, s.predictLPCOrder)
	s.shapeWinLength = SilkConstants.SUB_FRAME_LENGTH_MS*s.Fs_kHz + 2*s.la_shape
	s.Complexity = Complexity
	inlines.OpusAssert(s.pitchEstimationLPCOrder <= SilkConstants.MAX_FIND_PITCH_LPC_ORDER)
	inlines.OpusAssert(s.shapingLPCOrder <= SilkConstants.MAX_SHAPE_LPC_ORDER)
	inlines.OpusAssert(s.nStatesDelayedDecision <= SilkConstants.MAX_DEL_DEC_STATES)
	inlines.OpusAssert(s.warping_Q16 <= 32767)
	inlines.OpusAssert(s.la_shape <= SilkConstants.LA_SHAPE_MAX)
	inlines.OpusAssert(s.shapeWinLength <= SilkConstants.SHAPE_LPC_WIN_MAX)
	inlines.OpusAssert(s.NLSF_MSVQ_Survivors <= SilkConstants.NLSF_VQ_MAX_SURVIVORS)
	return ret
}

func (s *SilkChannelEncoder) silk_setup_LBRR(TargetRate_bps int) int {
	LBRR_in_previous_packet := s.LBRR_enabled
	s.LBRR_enabled = 0
	if s.UseInBandFEC != 0 && s.PacketLoss_perc > 0 {
		var LBRR_rate_thres_bps int
		if s.Fs_kHz == 8 {
			LBRR_rate_thres_bps = SilkConstants.LBRR_NB_MIN_RATE_BPS
		} else if s.Fs_kHz == 12 {
			LBRR_rate_thres_bps = SilkConstants.LBRR_MB_MIN_RATE_BPS
		} else {
			LBRR_rate_thres_bps = SilkConstants.LBRR_WB_MIN_RATE_BPS
		}
		//	LBRR_rate_thres_bps = inlines.Silk_SMULWB(inlines.Silk_MUL(LBRR_rate_thres_bps, 125-inlines.Silk_min(s.PacketLoss_perc, 25)), inlines.Silk_SMULWB(0.01, 1<<16))

		LBRR_rate_thres_bps = inlines.Silk_SMULWB(inlines.Silk_MUL(LBRR_rate_thres_bps, 125-inlines.Silk_min(s.PacketLoss_perc, 25)), int(math.Trunc(0.01*(1<<(16))+0.5)))

		if TargetRate_bps > LBRR_rate_thres_bps {
			if LBRR_in_previous_packet == 0 {
				s.LBRR_GainIncreases = 7
			} else {
				s.LBRR_GainIncreases = inlines.Silk_max_int(7-inlines.Silk_SMULWB(s.PacketLoss_perc, int(math.Trunc((0.4)*(1<<(16))+0.5))), 2)

			}
			s.LBRR_enabled = 1
		}
	}
	return SilkError.SILK_NO_ERROR
}

func (s *SilkChannelEncoder) silk_control_audio_bandwidth(encControl *EncControlState) int {
	fs_kHz := s.Fs_kHz
	fs_Hz := inlines.Silk_SMULBB(fs_kHz, 1000)
	if fs_Hz == 0 {
		fs_Hz = inlines.Silk_min(s.DesiredInternal_fs_Hz, s.API_fs_Hz)
		fs_kHz = inlines.Silk_DIV32_16(fs_Hz, 1000)
	} else if fs_Hz > s.API_fs_Hz || fs_Hz > s.MaxInternal_fs_Hz || fs_Hz < s.MinInternal_fs_Hz {
		fs_Hz = s.API_fs_Hz
		fs_Hz = inlines.Silk_min(fs_Hz, s.MaxInternal_fs_Hz)
		fs_Hz = inlines.Silk_max(fs_Hz, s.MinInternal_fs_Hz)
		fs_kHz = inlines.Silk_DIV32_16(fs_Hz, 1000)
	} else {
		if s.SLP.transition_frame_no >= SilkConstants.TRANSITION_FRAMES {
			s.SLP.Mode = 0
		}
		if s.Allow_bandwidth_switch != 0 || encControl.OpusCanSwitch != 0 {
			if inlines.Silk_SMULBB(s.Fs_kHz, 1000) > s.DesiredInternal_fs_Hz {
				if s.SLP.Mode == 0 {
					s.SLP.transition_frame_no = SilkConstants.TRANSITION_FRAMES
					for i := range s.SLP.In_LP_State {
						s.SLP.In_LP_State[i] = 0
					}
				}
				if encControl.OpusCanSwitch != 0 {
					s.SLP.Mode = 0
					if s.Fs_kHz == 16 {
						fs_kHz = 12
					} else {
						fs_kHz = 8
					}
				} else if s.SLP.transition_frame_no <= 0 {
					encControl.SwitchReady = 1
					encControl.MaxBits -= encControl.MaxBits * 5 / (encControl.PayloadSize_ms + 5)
				} else {
					s.SLP.Mode = -2
				}
			} else if inlines.Silk_SMULBB(s.Fs_kHz, 1000) < s.DesiredInternal_fs_Hz {
				if encControl.OpusCanSwitch != 0 {
					if s.Fs_kHz == 8 {
						fs_kHz = 12
					} else {
						fs_kHz = 16
					}
					s.SLP.transition_frame_no = 0
					for i := range s.SLP.In_LP_State {
						s.SLP.In_LP_State[i] = 0
					}
					s.SLP.Mode = 1
				} else if s.SLP.Mode == 0 {
					encControl.SwitchReady = 1
					encControl.MaxBits -= encControl.MaxBits * 5 / (encControl.PayloadSize_ms + 5)
				} else {
					s.SLP.Mode = 1
				}
			} else if s.SLP.Mode < 0 {
				s.SLP.Mode = 1
			}
		}
	}
	return fs_kHz
}

func (s *SilkChannelEncoder) Silk_control_SNR(TargetRate_bps int) int {
	var k int
	ret := SilkError.SILK_NO_ERROR
	var frac_Q6 int
	var rateTable []int
	TargetRate_bps = inlines.Silk_LIMIT(TargetRate_bps, SilkConstants.MIN_TARGET_RATE_BPS, SilkConstants.MAX_TARGET_RATE_BPS)
	if TargetRate_bps != s.TargetRate_bps {
		s.TargetRate_bps = TargetRate_bps
		if s.Fs_kHz == 8 {
			rateTable = SilkTables.Silk_TargetRate_table_NB
		} else if s.Fs_kHz == 12 {
			rateTable = SilkTables.Silk_TargetRate_table_MB
		} else {
			rateTable = SilkTables.Silk_TargetRate_table_WB
		}
		if s.nb_subfr == 2 {
			TargetRate_bps -= TuningParameters.REDUCE_BITRATE_10_MS_BPS
		}
		for k = 1; k < SilkConstants.TARGET_RATE_TAB_SZ; k++ {
			if TargetRate_bps <= rateTable[k] {
				frac_Q6 = inlines.Silk_DIV32(inlines.Silk_LSHIFT(TargetRate_bps-rateTable[k-1], 6), rateTable[k]-rateTable[k-1])
				s.SNR_dB_Q7 = inlines.Silk_LSHIFT(int(SilkTables.Silk_SNR_table_Q1[k-1]), 6) + inlines.Silk_MUL(frac_Q6, int(SilkTables.Silk_SNR_table_Q1[k]-SilkTables.Silk_SNR_table_Q1[k-1]))
				break
			}
		}
	}
	return ret
}

func (s *SilkChannelEncoder) Silk_encode_do_VAD() {

	silk_VAD_GetSA_Q8(s, s.InputBuf[:], 1)

	if s.Speech_activity_Q8 < int((float64(TuningParameters.SPEECH_ACTIVITY_DTX_THRES)*float64(int64(1)<<(8)) + 0.5)) {

		s.indices.SignalType = byte(SilkConstants.TYPE_NO_VOICE_ACTIVITY)
		s.noSpeechCounter++
		if s.noSpeechCounter < SilkConstants.NB_SPEECH_FRAMES_BEFORE_DTX {
			s.InDTX = 0
		} else if s.noSpeechCounter > SilkConstants.MAX_CONSECUTIVE_DTX+SilkConstants.NB_SPEECH_FRAMES_BEFORE_DTX {
			s.noSpeechCounter = SilkConstants.NB_SPEECH_FRAMES_BEFORE_DTX
			s.InDTX = 0
		}
		s.VAD_flags[s.NFramesEncoded] = 0
	} else {

		s.noSpeechCounter = 0
		s.InDTX = 0
		s.indices.SignalType = byte(SilkConstants.TYPE_UNVOICED)
		s.VAD_flags[s.NFramesEncoded] = 1
	}
}

func (s *SilkChannelEncoder) Silk_encode_frame(pnBytesOut *comm.BoxedValueInt, psRangeEnc *comm.EntropyCoder, condCoding int, maxBits int, useCBR int) int {

	sEncCtrl := NewSilkEncoderControl()
	var iter, maxIter, found_upper, found_lower, ret int
	var x_frame int
	sRangeEnc_copy := &comm.EntropyCoder{}
	sRangeEnc_copy2 := &comm.EntropyCoder{}
	SNSQ_copy := &SilkNSQState{}
	SNSQ_copy2 := &SilkNSQState{}
	var nBits, nBits_lower, nBits_upper, gainMult_lower, gainMult_upper int
	var gainsID, gainsID_lower, gainsID_upper int
	var gainMult_Q8 int16
	var ec_prevLagIndex_copy int16
	var ec_prevSignalType_copy int
	var LastGainIndex_copy2 int8
	var seed_copy int8
	nBits_lower, nBits_upper, gainMult_lower, gainMult_upper = 0, 0, 0, 0
	s.indices.Seed = int8(s.frameCounter & 3)
	s.frameCounter++
	x_frame = s.ltp_mem_length

	s.SLP.silk_LP_variable_cutoff(s.InputBuf[:], 1, s.Frame_length)

	copy(s.x_buf[x_frame+SilkConstants.LA_SHAPE_MS*s.Fs_kHz:], s.InputBuf[1:1+s.Frame_length])

	if s.PrefillFlag == 0 {
		var xfw_Q3 []int
		var res_pitch []int16
		var ec_buf_copy []byte
		var res_pitch_frame int
		res_pitch = make([]int16, s.la_pitch+s.Frame_length+s.ltp_mem_length)
		res_pitch_frame = s.ltp_mem_length
		silk_find_pitch_lags(s, sEncCtrl, res_pitch, s.x_buf[:], x_frame)

		silk_noise_shape_analysis(s, sEncCtrl, res_pitch, res_pitch_frame, s.x_buf[:], x_frame)

		silk_find_pred_coefs(s, sEncCtrl, res_pitch, s.x_buf[:], x_frame, condCoding)
		silk_process_gains(s, sEncCtrl, condCoding)
		xfw_Q3 = make([]int, s.Frame_length)

		silk_prefilter(s, sEncCtrl, xfw_Q3, s.x_buf[:], x_frame)

		s.silk_LBRR_encode(sEncCtrl, xfw_Q3, condCoding)

		maxIter = 6
		gainMult_Q8 = int16(inlines.Silk_SMULWB(1, 1<<8))
		found_lower = 0
		found_upper = 0
		gainsID = silk_gains_ID(s.indices.GainsIndices[:], s.nb_subfr)
		gainsID_lower = -1
		gainsID_upper = -1
		sRangeEnc_copy.Assign(psRangeEnc)
		SNSQ_copy.Assign(s.SNSQ)
		seed_copy = s.indices.Seed
		ec_prevLagIndex_copy = s.ec_prevLagIndex
		ec_prevSignalType_copy = s.ec_prevSignalType
		ec_buf_copy = make([]byte, 1275)
		for iter = 0; ; iter++ {
			if gainsID == gainsID_lower {
				nBits = nBits_lower
			} else if gainsID == gainsID_upper {
				nBits = nBits_upper
			} else {
				if iter > 0 {
					psRangeEnc.Assign(sRangeEnc_copy)
					s.SNSQ.Assign(SNSQ_copy)
					s.indices.Seed = seed_copy
					s.ec_prevLagIndex = ec_prevLagIndex_copy
					s.ec_prevSignalType = ec_prevSignalType_copy
				}

				if s.nStatesDelayedDecision > 1 || s.warping_Q16 > 0 {
					s.SNSQ.silk_NSQ_del_dec(s, s.indices, xfw_Q3, s.pulses[:], sEncCtrl.PredCoef_Q12[:], sEncCtrl.LTPCoef_Q14[:], sEncCtrl.AR2_Q13[:], sEncCtrl.HarmShapeGain_Q14, sEncCtrl.Tilt_Q14, sEncCtrl.LF_shp_Q14, sEncCtrl.Gains_Q16[:], sEncCtrl.pitchL[:], sEncCtrl.Lambda_Q10, sEncCtrl.LTP_scale_Q14)

				} else {

					s.SNSQ.silk_NSQ(s, s.indices, xfw_Q3, s.pulses[:], sEncCtrl.PredCoef_Q12[:], sEncCtrl.LTPCoef_Q14[:], sEncCtrl.AR2_Q13[:], sEncCtrl.HarmShapeGain_Q14, sEncCtrl.Tilt_Q14, sEncCtrl.LF_shp_Q14, sEncCtrl.Gains_Q16[:], sEncCtrl.pitchL[:], sEncCtrl.Lambda_Q10, sEncCtrl.LTP_scale_Q14)
				}

				Silk_encode_indices(s, psRangeEnc, s.NFramesEncoded, 0, condCoding)

				Silk_encode_pulses(psRangeEnc, int(s.indices.SignalType), int(s.indices.QuantOffsetType), s.pulses, s.Frame_length)

				nBits = psRangeEnc.Tell()

				if useCBR == 0 && iter == 0 && nBits <= maxBits {
					break
				}
			}
			if iter == maxIter {
				if found_lower != 0 && (gainsID == gainsID_lower || nBits > maxBits) {
					psRangeEnc.Assign(sRangeEnc_copy2)
					inlines.OpusAssert(sRangeEnc_copy2.Offs <= 1275)
					psRangeEnc.Write_buffer(ec_buf_copy, 0, 0, sRangeEnc_copy2.Offs)
					s.SNSQ.Assign(SNSQ_copy2)
					s.SShape.LastGainIndex = LastGainIndex_copy2
				}
				break
			}
			if nBits > maxBits {
				if found_lower == 0 && iter >= 2 {
					sEncCtrl.Lambda_Q10 += sEncCtrl.Lambda_Q10 >> 1
					found_upper = 0
					gainsID_upper = -1
				} else {
					found_upper = 1
					nBits_upper = nBits
					gainMult_upper = int(gainMult_Q8)
					gainsID_upper = gainsID
				}
			} else if nBits < maxBits-5 {
				found_lower = 1
				nBits_lower = nBits
				gainMult_lower = int(gainMult_Q8)
				if gainsID != gainsID_lower {
					gainsID_lower = gainsID
					/* Copy part of the output state */
					sRangeEnc_copy2.Assign(psRangeEnc)
					inlines.OpusAssert(psRangeEnc.Offs <= 1275)

					copy(ec_buf_copy, psRangeEnc.Get_buffer()[:psRangeEnc.Offs])
					SNSQ_copy2.Assign(s.SNSQ)
					LastGainIndex_copy2 = s.SShape.LastGainIndex
				}
			} else {
				break
			}
			if (found_lower & found_upper) == 0 {

				gain_factor_Q16 := inlines.Silk_log2lin(inlines.Silk_LSHIFT(nBits-maxBits, 7)/s.Frame_length + int(math.Trunc(16)*(1<<(7))+0.5))
				gain_factor_Q16 = inlines.Silk_min_32(gain_factor_Q16, int(math.Trunc((2)*(1<<(16))+0.5)))
				if nBits > maxBits {
					gain_factor_Q16 = inlines.Silk_max_32(gain_factor_Q16, int(math.Trunc(1.3)*(1<<(16))+0.5))
				}
				gainMult_Q8 = int16(inlines.Silk_SMULWB(gain_factor_Q16, int(gainMult_Q8)))
			} else {
				gainMult_Q8 = int16(gainMult_lower + inlines.Silk_DIV32_16(inlines.Silk_MUL(gainMult_upper-gainMult_lower, maxBits-nBits_lower), nBits_upper-nBits_lower))
				if gainMult_Q8 > int16(gainMult_lower+(gainMult_upper-gainMult_lower)>>2) {
					gainMult_Q8 = int16(gainMult_lower + (gainMult_upper-gainMult_lower)>>2)
				} else if gainMult_Q8 < int16(gainMult_upper-(gainMult_upper-gainMult_lower)>>2) {
					gainMult_Q8 = int16(gainMult_upper - (gainMult_upper-gainMult_lower)>>2)
				}
			}
			for i := int(0); i < s.nb_subfr; i++ {
				sEncCtrl.Gains_Q16[i] = inlines.Silk_LSHIFT_SAT32(inlines.Silk_SMULWB(sEncCtrl.GainsUnq_Q16[i], int(gainMult_Q8)), 8)
			}

			s.SShape.LastGainIndex = sEncCtrl.lastGainIndexPrev
			boxed_gainIndex := &comm.BoxedValueByte{int8(s.SShape.LastGainIndex)}

			silk_gains_quant(s.indices.GainsIndices, sEncCtrl.Gains_Q16,
				boxed_gainIndex, comm.BoolToInt(condCoding == SilkConstants.CODE_CONDITIONALLY), s.nb_subfr)
			s.SShape.LastGainIndex = int8(boxed_gainIndex.Val)
			gainsID = silk_gains_ID(s.indices.GainsIndices[:], s.nb_subfr)
		}
	}
	copy(s.x_buf[:], s.x_buf[s.Frame_length:])
	if s.PrefillFlag != 0 {
		pnBytesOut.Val = 0
		return ret
	}
	s.PrevLag = sEncCtrl.pitchL[s.nb_subfr-1]
	s.PrevSignalType = s.indices.SignalType
	s.First_frame_after_reset = 0
	pnBytesOut.Val = int(uint(psRangeEnc.Tell()+7) >> 3)

	return ret
}

func (s *SilkChannelEncoder) silk_LBRR_encode(thisCtrl *SilkEncoderControl, xfw_Q3 []int, condCoding int) {
	SNSQ_LBRR := NewSilkNSQState()
	psIndices_LBRR := s.Indices_LBRR[s.NFramesEncoded]
	TempGains_Q16 := make([]int, s.nb_subfr)
	if s.LBRR_enabled != 0 && s.Speech_activity_Q8 > inlines.Silk_SMULWB(int(TuningParameters.LBRR_SPEECH_ACTIVITY_THRES), 1<<8) {
		s.LBRR_flags[s.NFramesEncoded] = 1

		SNSQ_LBRR.Assign(s.SNSQ)
		psIndices_LBRR.Assign(s.indices)
		copy(TempGains_Q16, thisCtrl.Gains_Q16[:])
		if s.NFramesEncoded == 0 || s.LBRR_flags[s.NFramesEncoded-1] == 0 {
			psIndices_LBRR.GainsIndices[0] = int8(inlines.Silk_min_int(int(psIndices_LBRR.GainsIndices[0])+s.LBRR_GainIncreases, SilkConstants.N_LEVELS_QGAIN-1))

		}

		boxed_gainIndex := comm.BoxedValueByte{int8(s.LBRRprevLastGainIndex)}
		silk_gains_dequant(thisCtrl.Gains_Q16, psIndices_LBRR.GainsIndices,
			&boxed_gainIndex, comm.BoolToInt(condCoding == SilkConstants.CODE_CONDITIONALLY), s.nb_subfr)
		s.LBRRprevLastGainIndex = byte(boxed_gainIndex.Val)
		if s.nStatesDelayedDecision > 1 || s.warping_Q16 > 0 {
			SNSQ_LBRR.silk_NSQ_del_dec(s, psIndices_LBRR, xfw_Q3, s.Pulses_LBRR[s.NFramesEncoded], thisCtrl.PredCoef_Q12[:], thisCtrl.LTPCoef_Q14[:], thisCtrl.AR2_Q13[:], thisCtrl.HarmShapeGain_Q14, thisCtrl.Tilt_Q14, thisCtrl.LF_shp_Q14, thisCtrl.Gains_Q16[:], thisCtrl.pitchL[:], thisCtrl.Lambda_Q10, thisCtrl.LTP_scale_Q14)
		} else {
			SNSQ_LBRR.silk_NSQ(s, psIndices_LBRR, xfw_Q3, s.Pulses_LBRR[s.NFramesEncoded], thisCtrl.PredCoef_Q12[:], thisCtrl.LTPCoef_Q14[:], thisCtrl.AR2_Q13[:], thisCtrl.HarmShapeGain_Q14, thisCtrl.Tilt_Q14, thisCtrl.LF_shp_Q14, thisCtrl.Gains_Q16[:], thisCtrl.pitchL[:], thisCtrl.Lambda_Q10, thisCtrl.LTP_scale_Q14)
		}
		copy(thisCtrl.Gains_Q16[:], TempGains_Q16)
	}
}
