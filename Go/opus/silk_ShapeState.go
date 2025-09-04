package opus

type SilkShapeState struct {
	LastGainIndex          int8
	HarmBoost_smth_Q16     int
	HarmShapeGain_smth_Q16 int
	Tilt_smth_Q16          int
}

func (s *SilkShapeState) Reset() {
	s.LastGainIndex = 0
	s.HarmBoost_smth_Q16 = 0
	s.HarmShapeGain_smth_Q16 = 0
	s.Tilt_smth_Q16 = 0
}
