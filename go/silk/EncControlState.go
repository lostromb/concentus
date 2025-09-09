package silk

type EncControlState struct {
	NChannelsAPI              int
	NChannelsInternal         int
	API_sampleRate            int
	MaxInternalSampleRate     int
	MinInternalSampleRate     int
	DesiredInternalSampleRate int
	PayloadSize_ms            int
	BitRate                   int
	PacketLossPercentage      int
	Complexity                int
	UseInBandFEC              int
	UseDTX                    int
	UseCBR                    int
	MaxBits                   int
	ToMono                    int
	OpusCanSwitch             int
	ReducedDependency         int
	InternalSampleRate        int
	AllowBandwidthSwitch      int
	InWBmodeWithoutVariableLP int
	StereoWidth_Q14           int
	SwitchReady               int
}

func (s *EncControlState) Reset() {
	s.NChannelsAPI = 0
	s.NChannelsInternal = 0
	s.API_sampleRate = 0
	s.MaxInternalSampleRate = 0
	s.MinInternalSampleRate = 0
	s.DesiredInternalSampleRate = 0
	s.PayloadSize_ms = 0
	s.BitRate = 0
	s.PacketLossPercentage = 0
	s.Complexity = 0
	s.UseInBandFEC = 0
	s.UseDTX = 0
	s.UseCBR = 0
	s.MaxBits = 0
	s.ToMono = 0
	s.OpusCanSwitch = 0
	s.ReducedDependency = 0
	s.InternalSampleRate = 0
	s.AllowBandwidthSwitch = 0
	s.InWBmodeWithoutVariableLP = 0
	s.StereoWidth_Q14 = 0
	s.SwitchReady = 0
}

func (s *EncControlState) Check_control_input() int {
	if ((s.API_sampleRate != 8000) &&
		(s.API_sampleRate != 12000) &&
		(s.API_sampleRate != 16000) &&
		(s.API_sampleRate != 24000) &&
		(s.API_sampleRate != 32000) &&
		(s.API_sampleRate != 44100) &&
		(s.API_sampleRate != 48000)) ||
		((s.DesiredInternalSampleRate != 8000) &&
			(s.DesiredInternalSampleRate != 12000) &&
			(s.DesiredInternalSampleRate != 16000)) ||
		((s.MaxInternalSampleRate != 8000) &&
			(s.MaxInternalSampleRate != 12000) &&
			(s.MaxInternalSampleRate != 16000)) ||
		((s.MinInternalSampleRate != 8000) &&
			(s.MinInternalSampleRate != 12000) &&
			(s.MinInternalSampleRate != 16000)) ||
		(s.MinInternalSampleRate > s.DesiredInternalSampleRate) ||
		(s.MaxInternalSampleRate < s.DesiredInternalSampleRate) ||
		(s.MinInternalSampleRate > s.MaxInternalSampleRate) {
		inlines.OpusAssert(false)
		return SilkError.SILK_ENC_FS_NOT_SUPPORTED
	}
	if s.PayloadSize_ms != 10 &&
		s.PayloadSize_ms != 20 &&
		s.PayloadSize_ms != 40 &&
		s.PayloadSize_ms != 60 {
		inlines.OpusAssert(false)
		return SilkError.SILK_ENC_PACKET_SIZE_NOT_SUPPORTED
	}
	if s.PacketLossPercentage < 0 || s.PacketLossPercentage > 100 {
		inlines.OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_LOSS_RATE
	}
	if s.UseDTX < 0 || s.UseDTX > 1 {
		inlines.OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_DTX_SETTING
	}
	if s.UseCBR < 0 || s.UseCBR > 1 {
		inlines.OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_CBR_SETTING
	}
	if s.UseInBandFEC < 0 || s.UseInBandFEC > 1 {
		inlines.OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_INBAND_FEC_SETTING
	}
	if s.NChannelsAPI < 1 || s.NChannelsAPI > SilkConstants.ENCODER_NUM_CHANNELS {
		inlines.OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_NUMBER_OF_CHANNELS_ERROR
	}
	if s.NChannelsInternal < 1 || s.NChannelsInternal > SilkConstants.ENCODER_NUM_CHANNELS {
		inlines.OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_NUMBER_OF_CHANNELS_ERROR
	}
	if s.NChannelsInternal > s.NChannelsAPI {
		inlines.OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_NUMBER_OF_CHANNELS_ERROR
	}
	if s.Complexity < 0 || s.Complexity > 10 {
		inlines.OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_COMPLEXITY_SETTING
	}

	return SilkError.SILK_NO_ERROR
}
