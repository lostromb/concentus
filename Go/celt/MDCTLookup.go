package celt

type MDCTLookup struct {
	n        int
	maxshift int
	Kfft     [4]*FFTState
	trig     []int16
}
