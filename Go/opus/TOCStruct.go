package opus

type TOCStruct struct {
	VADFlag       int
	VADFlags      []int
	inbandFECFlag int
}

func NewTOCStruct() *TOCStruct {
	obj := &TOCStruct{}
	obj.VADFlags = make([]int, SilkConstants.SILK_MAX_FRAMES_PER_PACKET)
	return obj
}

func (t *TOCStruct) Reset() {
	t.VADFlag = 0
	MemSetLen(t.VADFlags, 0, SilkConstants.SILK_MAX_FRAMES_PER_PACKET)
	t.inbandFECFlag = 0
}
