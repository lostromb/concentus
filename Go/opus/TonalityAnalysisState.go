package opus

type TonalityAnalysisState struct {
	enabled                 bool
	angle                   [240]float32
	d_angle                 [240]float32
	d2_angle                [240]float32
	inmem                   []int
	mem_fill                int
	prev_band_tonality      [NB_TBANDS]float32
	prev_tonality           float32
	E                       [NB_FRAMES][NB_TBANDS]float32
	lowE                    [NB_TBANDS]float32
	highE                   [NB_TBANDS]float32
	meanE                   [NB_TOT_BANDS]float32
	mem                     [32]float32
	cmean                   [8]float32
	std                     [9]float32
	music_prob              float32
	Etracker                float32
	lowECount               float32
	E_count                 int
	last_music              int
	last_transition         int
	count                   int
	subframe_mem            []float32
	analysis_offset         int
	pspeech                 [DETECT_SIZE]float32
	pmusic                  [DETECT_SIZE]float32
	speech_confidence       float32
	music_confidence        float32
	speech_confidence_count int
	music_confidence_count  int
	write_pos               int
	read_pos                int
	read_subframe           int
	info                    [DETECT_SIZE]*AnalysisInfo
}

func NewTonalityAnalysisState() TonalityAnalysisState {
	t := TonalityAnalysisState{}
	for i := 0; i < DETECT_SIZE; i++ {
		t.info[i] = &AnalysisInfo{}
	}
	return t
}

func (t *TonalityAnalysisState) Reset() {
	for i := range t.angle {
		t.angle[i] = 0
	}
	for i := range t.d_angle {
		t.d_angle[i] = 0
	}
	for i := range t.d2_angle {
		t.d2_angle[i] = 0
	}
	for i := range t.inmem {
		t.inmem[i] = 0
	}
	t.mem_fill = 0
	for i := range t.prev_band_tonality {
		t.prev_band_tonality[i] = 0
	}
	t.prev_tonality = 0
	for i := range t.E {
		for j := range t.E[i] {
			t.E[i][j] = 0
		}
	}
	for i := range t.lowE {
		t.lowE[i] = 0
	}
	for i := range t.highE {
		t.highE[i] = 0
	}
	for i := range t.meanE {
		t.meanE[i] = 0
	}
	for i := range t.mem {
		t.mem[i] = 0
	}
	for i := range t.cmean {
		t.cmean[i] = 0
	}
	for i := range t.std {
		t.std[i] = 0
	}
	t.music_prob = 0
	t.Etracker = 0
	t.lowECount = 0
	t.E_count = 0
	t.last_music = 0
	t.last_transition = 0
	t.count = 0
	for i := range t.subframe_mem {
		t.subframe_mem[i] = 0
	}
	t.analysis_offset = 0
	for i := range t.pspeech {
		t.pspeech[i] = 0
	}
	for i := range t.pmusic {
		t.pmusic[i] = 0
	}
	t.speech_confidence = 0
	t.music_confidence = 0
	t.speech_confidence_count = 0
	t.music_confidence_count = 0
	t.write_pos = 0
	t.read_pos = 0
	t.read_subframe = 0
	for i := 0; i < DETECT_SIZE; i++ {
		t.info[i].Reset()
	}
}
