package opus

type SilkResamplerState struct {
	sIIR               []int
	sFIR_i32           []int
	sFIR_i16           []int16
	delayBuf           []int16
	resampler_function int
	batchSize          int
	invRatio_Q16       int
	FIR_Order          int
	FIR_Fracs          int
	Fs_in_kHz          int
	Fs_out_kHz         int
	inputDelay         int
	Coefs              []int16
}

func NewSilkResamplerState() *SilkResamplerState {
	obj := &SilkResamplerState{}
	obj.sIIR = make([]int, SilkConstants.SILK_RESAMPLER_MAX_IIR_ORDER)
	obj.sFIR_i32 = make([]int, SilkConstants.SILK_RESAMPLER_MAX_FIR_ORDER)
	obj.sFIR_i16 = make([]int16, SilkConstants.SILK_RESAMPLER_MAX_FIR_ORDER)
	obj.delayBuf = make([]int16, 48)
	return obj
}

func (s *SilkResamplerState) Reset() {

	MemSetLen(s.sIIR, 0, SilkConstants.SILK_RESAMPLER_MAX_IIR_ORDER)
	MemSetLen(s.sFIR_i32, 0, SilkConstants.SILK_RESAMPLER_MAX_FIR_ORDER)
	MemSetLen(s.sFIR_i16, 0, SilkConstants.SILK_RESAMPLER_MAX_FIR_ORDER)
	MemSetLen(s.delayBuf, 0, 48)
	s.resampler_function = 0
	s.batchSize = 0
	s.invRatio_Q16 = 0
	s.FIR_Order = 0
	s.FIR_Fracs = 0
	s.Fs_in_kHz = 0
	s.Fs_out_kHz = 0
	s.inputDelay = 0
	s.Coefs = nil
}

func (s *SilkResamplerState) Assign(other *SilkResamplerState) {
	*s = *other
}
