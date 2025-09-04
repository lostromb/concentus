package opus

type SilkEncoderControl struct {
	Gains_Q16          []int
	PredCoef_Q12       [][]int16
	LTPCoef_Q14        []int16
	LTP_scale_Q14      int
	pitchL             []int
	AR1_Q13            [MAX_NB_SUBFR * MAX_SHAPE_LPC_ORDER]int16
	AR2_Q13            [MAX_NB_SUBFR * MAX_SHAPE_LPC_ORDER]int16
	LF_shp_Q14         []int
	GainsPre_Q14       []int
	HarmBoost_Q14      []int
	Tilt_Q14           []int
	HarmShapeGain_Q14  []int
	Lambda_Q10         int
	input_quality_Q14  int
	coding_quality_Q14 int
	sparseness_Q8      int
	predGain_Q16       int
	LTPredCodGain_Q7   int
	ResNrg             [MAX_NB_SUBFR]int
	ResNrgQ            [MAX_NB_SUBFR]int
	GainsUnq_Q16       [MAX_NB_SUBFR]int
	lastGainIndexPrev  int8
}

func NewSilkEncoderControl() *SilkEncoderControl {
	s := &SilkEncoderControl{}
	s.Gains_Q16 = make([]int, SilkConstants.MAX_NB_SUBFR)
	s.GainsPre_Q14 = make([]int, SilkConstants.MAX_NB_SUBFR)
	s.LF_shp_Q14 = make([]int, SilkConstants.MAX_NB_SUBFR)
	s.LTPCoef_Q14 = make([]int16, SilkConstants.LTP_ORDER*SilkConstants.MAX_NB_SUBFR)
	s.HarmBoost_Q14 = make([]int, SilkConstants.MAX_NB_SUBFR)
	s.HarmShapeGain_Q14 = make([]int, SilkConstants.MAX_NB_SUBFR)
	s.Tilt_Q14 = make([]int, SilkConstants.MAX_NB_SUBFR)
	s.PredCoef_Q12 = InitTwoDimensionalArrayShort(2, SilkConstants.MAX_LPC_ORDER)
	s.pitchL = make([]int, SilkConstants.MAX_NB_SUBFR)
	return s
}

func (s *SilkEncoderControl) Reset() {
	MemSetLen(s.Gains_Q16, 0, SilkConstants.MAX_NB_SUBFR)
	MemSetLen(s.PredCoef_Q12[0], 0, SilkConstants.MAX_LPC_ORDER)
	MemSetLen(s.PredCoef_Q12[1], 0, SilkConstants.MAX_LPC_ORDER)
	MemSetLen(s.LTPCoef_Q14, 0, SilkConstants.LTP_ORDER*SilkConstants.MAX_NB_SUBFR)
	s.LTP_scale_Q14 = 0
	MemSetLen(s.pitchL, 0, SilkConstants.MAX_NB_SUBFR)
	//	MemSetLen(s.AR1_Q13, 0, SilkConstants.MAX_NB_SUBFR*SilkConstants.MAX_SHAPE_LPC_ORDER)
	s.AR1_Q13 = [MAX_NB_SUBFR * MAX_SHAPE_LPC_ORDER]int16{}
	//MemSetLen(s.AR2_Q13, 0, SilkConstants.MAX_NB_SUBFR*SilkConstants.MAX_SHAPE_LPC_ORDER)
	s.AR2_Q13 = [MAX_NB_SUBFR * MAX_SHAPE_LPC_ORDER]int16{}
	MemSetLen(s.LF_shp_Q14, 0, SilkConstants.MAX_NB_SUBFR)
	MemSetLen(s.GainsPre_Q14, 0, SilkConstants.MAX_NB_SUBFR)
	MemSetLen(s.HarmBoost_Q14, 0, SilkConstants.MAX_NB_SUBFR)
	MemSetLen(s.Tilt_Q14, 0, SilkConstants.MAX_NB_SUBFR)
	MemSetLen(s.HarmShapeGain_Q14, 0, SilkConstants.MAX_NB_SUBFR)
	s.Lambda_Q10 = 0
	s.input_quality_Q14 = 0
	s.coding_quality_Q14 = 0
	s.sparseness_Q8 = 0
	s.predGain_Q16 = 0
	s.LTPredCodGain_Q7 = 0
	//MemSetLen(s.ResNrg, 0, SilkConstants.MAX_NB_SUBFR)
	s.ResNrg = [MAX_NB_SUBFR]int{}
	s.ResNrgQ = [MAX_NB_SUBFR]int{}
	s.GainsUnq_Q16 = [MAX_NB_SUBFR]int{}
	//MemSetLen(s.ResNrgQ, 0, SilkConstants.MAX_NB_SUBFR)
	//MemSetLen(s.GainsUnq_Q16, 0, SilkConstants.MAX_NB_SUBFR)
	s.lastGainIndexPrev = 0
}
