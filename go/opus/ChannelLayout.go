package opus
type ChannelLayout struct {
    nb_channels        int
    nb_streams         int
    nb_coupled_streams int
    mapping            [256]int16
}

func (c *ChannelLayout) Reset() {
    c.nb_channels = 0
    c.nb_streams = 0
    c.nb_coupled_streams = 0
    c.mapping = [256]int16{}
}