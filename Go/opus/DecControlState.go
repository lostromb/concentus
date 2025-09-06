package opus

type DecControlState struct {
    nChannelsAPI       int
    nChannelsInternal  int
    API_sampleRate     int
    internalSampleRate int
    payloadSize_ms     int
    prevPitchLag       int
}

func (d *DecControlState) Reset() {
    d.nChannelsAPI = 0
    d.nChannelsInternal = 0
    d.API_sampleRate = 0
    d.internalSampleRate = 0
    d.payloadSize_ms = 0
    d.prevPitchLag = 0
}