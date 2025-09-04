package opus
type AnalysisInfo struct {
	enabled         bool
	valid           int
	tonality        float32
	tonality_slope  float32
	noisiness       float32
	activity        float32
	music_prob      float32
	bandwidth       int
}

func (a *AnalysisInfo) Assign(other *AnalysisInfo) {
	a.valid = other.valid
	a.tonality = other.tonality
	a.tonality_slope = other.tonality_slope
	a.noisiness = other.noisiness
	a.activity = other.activity
	a.music_prob = other.music_prob
	a.bandwidth = other.bandwidth
}

func (a *AnalysisInfo) Reset() {
	a.valid = 0
	a.tonality = 0
	a.tonality_slope = 0
	a.noisiness = 0
	a.activity = 0
	a.music_prob = 0
	a.bandwidth = 0
}