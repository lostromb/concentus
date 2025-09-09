package opus

type PulseCache struct {
    size  int
    index []int16
    bits  []int16
    caps  []int16
}

func (p *PulseCache) Reset() {
    p.size = 0
    p.index = nil
    p.bits = nil
    p.caps = nil
}