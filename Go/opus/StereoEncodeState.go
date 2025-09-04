package opus

type StereoEncodeState struct {
	pred_prev_Q13   [2]int16
	sMid            [2]int16
	sSide           [2]int16
	mid_side_amp_Q0 [4]int
	smth_width_Q14  int16
	width_prev_Q14  int16
	silent_side_len int16
	predIx          [][][]byte
	mid_only_flags  [3]byte
}

func NewStereoEncodeState() *StereoEncodeState {
	obj := &StereoEncodeState{}
	obj.predIx = InitThreeDimensionalArrayByte(SilkConstants.MAX_FRAMES_PER_PACKET, 2, 3)
	//obj.mid_only_flags = make([]byte, SilkConstants.MAX_FRAMES_PER_PACKET)
	return obj
}

func (s *StereoEncodeState) Reset() {
	s.pred_prev_Q13 = [2]int16{}
	s.sMid = [2]int16{}
	s.sSide = [2]int16{}
	s.mid_side_amp_Q0 = [4]int{}
	s.smth_width_Q14 = 0
	s.width_prev_Q14 = 0
	s.silent_side_len = 0
	s.predIx = InitThreeDimensionalArrayByte(SilkConstants.MAX_FRAMES_PER_PACKET, 2, 3)
	s.mid_only_flags = [3]byte{}
}
