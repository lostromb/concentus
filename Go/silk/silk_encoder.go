package silk

type SilkEncoder struct {
	State_Fxx                 []*SilkChannelEncoder
	SStereo                   *StereoEncodeState
	NBitsUsedLBRR             int
	NBitsExceeded             int
	NChannelsAPI              int
	NChannelsInternal         int
	NPrevChannelsInternal     int
	TimeSinceSwitchAllowed_ms int
	AllowBandwidthSwitch      int
	Prev_decode_only_middle   int
}

func NewSilkEncoder() SilkEncoder {
	enc := SilkEncoder{}
	enc.SStereo = NewStereoEncodeState()
	enc.State_Fxx = make([]*SilkChannelEncoder, ENCODER_NUM_CHANNELS)
	for i := 0; i < ENCODER_NUM_CHANNELS; i++ {
		enc.State_Fxx[i] = NewSilkChannelEncoder()
	}
	return enc
}

func (enc *SilkEncoder) Reset() {
	for c := 0; c < ENCODER_NUM_CHANNELS; c++ {
		enc.State_Fxx[c].Reset()
	}
	enc.SStereo.Reset()
	enc.NBitsUsedLBRR = 0
	enc.NBitsExceeded = 0
	enc.NChannelsAPI = 0
	enc.NChannelsInternal = 0
	enc.NPrevChannelsInternal = 0
	enc.TimeSinceSwitchAllowed_ms = 0
	enc.AllowBandwidthSwitch = 0
	enc.Prev_decode_only_middle = 0
}

func Silk_init_encoder(psEnc *SilkChannelEncoder) int {
	ret := 0
	psEnc.Reset()
	value := int(float64(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ)*float64(1<<16) + 0.5)
	logVal := inlines.Silk_lin2log(value)
	psEnc.Variable_HP_smth1_Q15 = int(inlines.Silk_LSHIFT(logVal-(16<<7), 8))
	psEnc.variable_HP_smth2_Q15 = psEnc.Variable_HP_smth1_Q15
	psEnc.First_frame_after_reset = 1
	ret += silk_VAD_Init(psEnc.sVAD)
	return ret
}
