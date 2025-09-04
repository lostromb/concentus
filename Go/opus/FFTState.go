package opus

type FFTState struct {
	nfft        int
	scale       int16
	scale_shift int
	shift       int
	factors     []int16
	bitrev      []int16
	twiddles    []int16
}
