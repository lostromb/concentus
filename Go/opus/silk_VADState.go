package opus

type SilkVADState struct {
	AnaState        []int
	AnaState1       []int
	AnaState2       []int
	XnrgSubfr       [VAD_N_BANDS]int
	NrgRatioSmth_Q8 [VAD_N_BANDS]int
	HPstate         int16
	NL              [VAD_N_BANDS]int
	inv_NL          [VAD_N_BANDS]int
	NoiseLevelBias  [VAD_N_BANDS]int
	counter         int
}

func NewSilkVADState() *SilkVADState {
	obj := &SilkVADState{}
	obj.AnaState = make([]int, 2)
	obj.AnaState1 = make([]int, 2)
	obj.AnaState2 = make([]int, 2)
	return obj
}

func (s *SilkVADState) Reset() {
	MemSetLen(s.AnaState, 0, 2)
	//s.AnaState = [2]int{}
	MemSetLen(s.AnaState1, 0, 2)
	//s.AnaState1 = [2]int{}
	//	s.AnaState = [2]int{}
	MemSetLen(s.AnaState2, 0, 2)
	s.XnrgSubfr = [VAD_N_BANDS]int{}

	s.NrgRatioSmth_Q8 = [VAD_N_BANDS]int{}
	s.HPstate = 0
	s.NL = [VAD_N_BANDS]int{}
	s.inv_NL = [VAD_N_BANDS]int{}
	s.NoiseLevelBias = [VAD_N_BANDS]int{}
	s.counter = 0
}
