package opus

type StereoWidthState struct {
	XX             int
	XY             int
	YY             int
	smoothed_width int
	max_follower   int
}

func (s *StereoWidthState) Reset() {
	s.XX = 0
	s.XY = 0
	s.YY = 0
	s.smoothed_width = 0
	s.max_follower = 0
}
