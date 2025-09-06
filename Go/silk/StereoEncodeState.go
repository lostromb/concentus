package silk

import "github.com/dosgo/concentus/go/comm/arrayUtil"

type StereoEncodeState struct {
	Pred_prev_Q13   [2]int16
	SMid            [2]int16
	SSide           [2]int16
	Mid_side_amp_Q0 [4]int
	Smth_width_Q14  int16
	Width_prev_Q14  int16
	silent_side_len int16
	PredIx          [][][]byte
	Mid_only_flags  [3]byte
}

func NewStereoEncodeState() *StereoEncodeState {
	obj := &StereoEncodeState{}
	obj.PredIx = arrayUtil.InitThreeDimensionalArrayByte(SilkConstants.MAX_FRAMES_PER_PACKET, 2, 3)
	//obj.mid_only_flags = make([]byte, SilkConstants.MAX_FRAMES_PER_PACKET)
	return obj
}

func (s *StereoEncodeState) Reset() {
	s.Pred_prev_Q13 = [2]int16{}
	s.SMid = [2]int16{}
	s.SSide = [2]int16{}
	s.Mid_side_amp_Q0 = [4]int{}
	s.Smth_width_Q14 = 0
	s.Width_prev_Q14 = 0
	s.silent_side_len = 0
	s.PredIx = arrayUtil.InitThreeDimensionalArrayByte(SilkConstants.MAX_FRAMES_PER_PACKET, 2, 3)
	s.Mid_only_flags = [3]byte{}
}
