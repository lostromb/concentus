package celt

type AnalysisInfo struct {
	Enabled        bool
	Valid          int
	Tonality       float32
	Tonality_slope float32
	Noisiness      float32
	Activity       float32
	Music_prob     float32
	Bandwidth      int
}

func (a *AnalysisInfo) Assign(other *AnalysisInfo) {
	a.Valid = other.Valid
	a.Tonality = other.Tonality
	a.Tonality_slope = other.Tonality_slope
	a.Noisiness = other.Noisiness
	a.Activity = other.Activity
	a.Music_prob = other.Music_prob
	a.Bandwidth = other.Bandwidth
}

func (a *AnalysisInfo) Reset() {
	a.Valid = 0
	a.Tonality = 0
	a.Tonality_slope = 0
	a.Noisiness = 0
	a.Activity = 0
	a.Music_prob = 0
	a.Bandwidth = 0
}
