package opus

type EncControlState struct {
	nChannelsAPI              int
	nChannelsInternal         int
	API_sampleRate            int
	maxInternalSampleRate     int
	minInternalSampleRate     int
	desiredInternalSampleRate int
	payloadSize_ms            int
	bitRate                   int
	packetLossPercentage      int
	complexity                int
	useInBandFEC              int
	useDTX                    int
	useCBR                    int
	maxBits                   int
	toMono                    int
	opusCanSwitch             int
	reducedDependency         int
	internalSampleRate        int
	allowBandwidthSwitch      int
	inWBmodeWithoutVariableLP int
	stereoWidth_Q14           int
	switchReady               int
}

func (s *EncControlState) Reset() {
	s.nChannelsAPI = 0
	s.nChannelsInternal = 0
	s.API_sampleRate = 0
	s.maxInternalSampleRate = 0
	s.minInternalSampleRate = 0
	s.desiredInternalSampleRate = 0
	s.payloadSize_ms = 0
	s.bitRate = 0
	s.packetLossPercentage = 0
	s.complexity = 0
	s.useInBandFEC = 0
	s.useDTX = 0
	s.useCBR = 0
	s.maxBits = 0
	s.toMono = 0
	s.opusCanSwitch = 0
	s.reducedDependency = 0
	s.internalSampleRate = 0
	s.allowBandwidthSwitch = 0
	s.inWBmodeWithoutVariableLP = 0
	s.stereoWidth_Q14 = 0
	s.switchReady = 0
}

func (s *EncControlState) check_control_input() int {
	if ((s.API_sampleRate != 8000) &&
		(s.API_sampleRate != 12000) &&
		(s.API_sampleRate != 16000) &&
		(s.API_sampleRate != 24000) &&
		(s.API_sampleRate != 32000) &&
		(s.API_sampleRate != 44100) &&
		(s.API_sampleRate != 48000)) ||
		((s.desiredInternalSampleRate != 8000) &&
			(s.desiredInternalSampleRate != 12000) &&
			(s.desiredInternalSampleRate != 16000)) ||
		((s.maxInternalSampleRate != 8000) &&
			(s.maxInternalSampleRate != 12000) &&
			(s.maxInternalSampleRate != 16000)) ||
		((s.minInternalSampleRate != 8000) &&
			(s.minInternalSampleRate != 12000) &&
			(s.minInternalSampleRate != 16000)) ||
		(s.minInternalSampleRate > s.desiredInternalSampleRate) ||
		(s.maxInternalSampleRate < s.desiredInternalSampleRate) ||
		(s.minInternalSampleRate > s.maxInternalSampleRate) {
		OpusAssert(false)
		return SilkError.SILK_ENC_FS_NOT_SUPPORTED
	}
	if s.payloadSize_ms != 10 &&
		s.payloadSize_ms != 20 &&
		s.payloadSize_ms != 40 &&
		s.payloadSize_ms != 60 {
		OpusAssert(false)
		return SilkError.SILK_ENC_PACKET_SIZE_NOT_SUPPORTED
	}
	if s.packetLossPercentage < 0 || s.packetLossPercentage > 100 {
		OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_LOSS_RATE
	}
	if s.useDTX < 0 || s.useDTX > 1 {
		OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_DTX_SETTING
	}
	if s.useCBR < 0 || s.useCBR > 1 {
		OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_CBR_SETTING
	}
	if s.useInBandFEC < 0 || s.useInBandFEC > 1 {
		OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_INBAND_FEC_SETTING
	}
	if s.nChannelsAPI < 1 || s.nChannelsAPI > SilkConstants.ENCODER_NUM_CHANNELS {
		OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_NUMBER_OF_CHANNELS_ERROR
	}
	if s.nChannelsInternal < 1 || s.nChannelsInternal > SilkConstants.ENCODER_NUM_CHANNELS {
		OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_NUMBER_OF_CHANNELS_ERROR
	}
	if s.nChannelsInternal > s.nChannelsAPI {
		OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_NUMBER_OF_CHANNELS_ERROR
	}
	if s.complexity < 0 || s.complexity > 10 {
		OpusAssert(false)
		return SilkError.SILK_ENC_INVALID_COMPLEXITY_SETTING
	}

	return SilkError.SILK_NO_ERROR
}
