package opus

type SideInfoIndices struct {
	GainsIndices      []int8
	LTPIndex          []int8
	NLSFIndices       []int8
	lagIndex          int16
	contourIndex      int8
	signalType        byte
	quantOffsetType   byte
	NLSFInterpCoef_Q2 byte
	PERIndex          int8
	LTP_scaleIndex    int8
	Seed              int8
}

func NewSideInfoIndices() *SideInfoIndices {
	obj := &SideInfoIndices{}
	obj.GainsIndices = make([]int8, SilkConstants.MAX_NB_SUBFR)
	obj.LTPIndex = make([]int8, SilkConstants.MAX_NB_SUBFR)
	obj.NLSFIndices = make([]int8, SilkConstants.MAX_LPC_ORDER+1)
	return obj
}
func (si *SideInfoIndices) Reset() {
	for i := range si.GainsIndices {
		si.GainsIndices[i] = 0
	}

	for i := range si.LTPIndex {
		si.LTPIndex[i] = 0
	}
	for i := range si.NLSFIndices {
		si.NLSFIndices[i] = 0
	}
	si.lagIndex = 0
	si.contourIndex = 0
	si.signalType = 0
	si.quantOffsetType = 0
	si.NLSFInterpCoef_Q2 = 0
	si.PERIndex = 0
	si.LTP_scaleIndex = 0
	si.Seed = 0
}

func (si *SideInfoIndices) Assign(other *SideInfoIndices) {

	copy(si.GainsIndices, other.GainsIndices)
	copy(si.LTPIndex, other.LTPIndex)
	copy(si.NLSFIndices, other.NLSFIndices)
	si.lagIndex = other.lagIndex
	si.contourIndex = other.contourIndex
	si.signalType = other.signalType
	si.quantOffsetType = other.quantOffsetType
	si.NLSFInterpCoef_Q2 = other.NLSFInterpCoef_Q2
	si.PERIndex = other.PERIndex
	si.LTP_scaleIndex = other.LTP_scaleIndex
	si.Seed = other.Seed
}
