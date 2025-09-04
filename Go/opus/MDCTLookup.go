package opus

type MDCTLookup struct {
	n        int
	maxshift int
	kfft     [4]*FFTState
	trig     []int16
}
