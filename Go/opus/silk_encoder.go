package opus

type SilkEncoder struct {
	state_Fxx                 []*SilkChannelEncoder
	sStereo                   *StereoEncodeState
	nBitsUsedLBRR             int
	nBitsExceeded             int
	nChannelsAPI              int
	nChannelsInternal         int
	nPrevChannelsInternal     int
	timeSinceSwitchAllowed_ms int
	allowBandwidthSwitch      int
	prev_decode_only_middle   int
}

func NewSilkEncoder() SilkEncoder {
	enc := SilkEncoder{}
	enc.sStereo = NewStereoEncodeState()
	enc.state_Fxx = make([]*SilkChannelEncoder, ENCODER_NUM_CHANNELS)
	for i := 0; i < ENCODER_NUM_CHANNELS; i++ {
		enc.state_Fxx[i] = NewSilkChannelEncoder()
	}
	return enc
}

func (enc *SilkEncoder) Reset() {
	for c := 0; c < ENCODER_NUM_CHANNELS; c++ {
		enc.state_Fxx[c].Reset()
	}
	enc.sStereo.Reset()
	enc.nBitsUsedLBRR = 0
	enc.nBitsExceeded = 0
	enc.nChannelsAPI = 0
	enc.nChannelsInternal = 0
	enc.nPrevChannelsInternal = 0
	enc.timeSinceSwitchAllowed_ms = 0
	enc.allowBandwidthSwitch = 0
	enc.prev_decode_only_middle = 0
}

func silk_init_encoder(psEnc *SilkChannelEncoder) int {
	ret := 0
	psEnc.Reset()
	value := int(float64(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ)*float64(1<<16) + 0.5)
	logVal := silk_lin2log(value)
	psEnc.variable_HP_smth1_Q15 = int(silk_LSHIFT(logVal-(16<<7), 8))
	psEnc.variable_HP_smth2_Q15 = psEnc.variable_HP_smth1_Q15
	psEnc.first_frame_after_reset = 1
	ret += silk_VAD_Init(psEnc.sVAD)
	return ret
}
