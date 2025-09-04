package opus

type SilkLPState struct {
	In_LP_State         [2]int
	transition_frame_no int
	mode                int
}

func NewSilkLPState() *SilkLPState {
	obj := &SilkLPState{}
	return obj
}
func (s *SilkLPState) Reset() {
	s.In_LP_State[0] = 0
	s.In_LP_State[1] = 0
	s.transition_frame_no = 0
	s.mode = 0
}

func (s *SilkLPState) silk_LP_variable_cutoff(frame []int16, frame_ptr int, frame_length int) {
	var B_Q28 []int
	var A_Q28 []int
	fac_Q16 := 0
	ind := 0

	OpusAssert(s.transition_frame_no >= 0 && s.transition_frame_no <= TRANSITION_FRAMES)

	if s.mode != 0 {
		fac_Q16 = silk_LSHIFT(TRANSITION_FRAMES-s.transition_frame_no, 16-6)
		ind = silk_RSHIFT(fac_Q16, 16)
		fac_Q16 -= silk_LSHIFT(ind, 16)

		OpusAssert(ind >= 0)
		OpusAssert(ind < TRANSITION_INT_NUM)

		B_Q28 = make([]int, TRANSITION_NB)
		A_Q28 = make([]int, TRANSITION_NA)
		silk_LP_interpolate_filter_taps(B_Q28, A_Q28, ind, fac_Q16)

		s.transition_frame_no = silk_LIMIT(s.transition_frame_no+s.mode, 0, TRANSITION_FRAMES)

		OpusAssert(TRANSITION_NB == 3 && TRANSITION_NA == 2)
		silk_biquad_alt(frame, frame_ptr, B_Q28, A_Q28, s.In_LP_State[:], frame, frame_ptr, frame_length, 1)
	}
}
