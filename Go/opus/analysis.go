package opus

import (
	"math"

	"github.com/dosgo/concentus/go/celt"
	"github.com/dosgo/concentus/go/comm/opusConstants"
)

const (
	M_PI = 3.141592653
	cA   = 0.43157974
	cB   = 0.67848403
	cC   = 0.08595542
	cE   = float32(M_PI / 2)
)

func fast_atan2f(y float32, x float32) float32 {
	if inlines.ABS16Float(x)+inlines.ABS16Float(y) < 1e-9 {
		x *= 1e12
		y *= 1e12
	}
	x2 := x * x
	y2 := y * y
	if x2 < y2 {
		den := (y2 + cB*x2) * (y2 + cC*x2)
		if den != 0 {
			term := -x * y * (y2 + cA*x2) / den
			if y < 0 {
				return term - cE
			}
			return term + cE
		} else {
			if y < 0 {
				return -cE
			}
			return cE
		}
	} else {
		den := (x2 + cB*y2) * (x2 + cC*y2)
		if den != 0 {
			term := x * y * (x2 + cA*y2) / den
			if y < 0 {
				term -= cE
			} else {
				term += cE
			}
			if x*y < 0 {
				term += cE
			} else {
				term -= cE
			}
			return term
		} else {
			var term float32
			if y < 0 {
				term = -cE
			} else {
				term = cE
			}
			if x*y < 0 {
				term += cE
			} else {
				term -= cE
			}
			return term
		}
	}
}

func Tonality_analysis_init(tonal *TonalityAnalysisState) {
	tonal.Reset()
}

func tonality_get_info(tonal *TonalityAnalysisState, info_out *celt.AnalysisInfo, len int) {
	pos := tonal.read_pos
	curr_lookahead := tonal.write_pos - tonal.read_pos
	if curr_lookahead < 0 {
		curr_lookahead += opusConstants.DETECT_SIZE
	}

	if len > 480 && pos != tonal.write_pos {
		pos++
		if pos == opusConstants.DETECT_SIZE {
			pos = 0
		}
	}
	if pos == tonal.write_pos {
		pos--
	}
	if pos < 0 {
		pos = opusConstants.DETECT_SIZE - 1
	}

	info_out.Assign(tonal.info[pos])
	tonal.read_subframe += len / 120
	for tonal.read_subframe >= 4 {
		tonal.read_subframe -= 4
		tonal.read_pos++
	}
	if tonal.read_pos >= opusConstants.DETECT_SIZE {
		tonal.read_pos -= opusConstants.DETECT_SIZE
	}

	curr_lookahead = inlines.IMAX(curr_lookahead-10, 0)

	psum := float32(0)
	for i := 0; i < opusConstants.DETECT_SIZE-curr_lookahead; i++ {
		psum += tonal.pmusic[i]
	}
	for i := opusConstants.DETECT_SIZE - curr_lookahead; i < opusConstants.DETECT_SIZE; i++ {
		psum += tonal.pspeech[i]
	}
	psum = psum*tonal.music_confidence + (1-psum)*tonal.speech_confidence
	info_out.Music_prob = psum
}

func tonality_analysis(tonal *TonalityAnalysisState, celt_mode *celt.CeltMode, x []int16, x_ptr int, len int, offset int, c1 int, c2 int, C int, lsb_depth int) {
	const N = 480
	const N2 = 240
	pi4 := float32(M_PI * M_PI * M_PI * M_PI)
	var kfft *celt.FFTState
	input := make([]int, 960)
	output := make([]int, 960)
	tonality := make([]float32, 240)
	noisiness := make([]float32, 240)
	band_tonality := make([]float32, opusConstants.NB_TBANDS)
	logE := make([]float32, opusConstants.NB_TBANDS)
	BFCC := make([]float32, 8)
	features := make([]float32, 25)
	frame_tonality := float32(0)
	max_frame_tonality := float32(0)
	frame_noisiness := float32(0)
	slope := float32(0)
	frame_stationarity := float32(0)
	relativeE := float32(0)
	frame_probs := make([]float32, 2)
	frame_loudness := float32(0)
	bandwidth_mask := float32(0)
	bandwidth := 0
	maxE := float32(0)
	noise_floor := float32(0)
	var info *celt.AnalysisInfo

	tonal.last_transition++
	alpha := 1.0 / float32(inlines.IMIN(20, 1+tonal.count))
	alphaE := 1.0 / float32(inlines.IMIN(50, 1+tonal.count))
	alphaE2 := 1.0 / float32(inlines.IMIN(1000, 1+tonal.count))

	if tonal.count < 4 {
		tonal.music_prob = 0.5
	}
	kfft = celt_mode.Mdct.Kfft[0]
	if tonal.count == 0 {
		tonal.mem_fill = 240
	}

	downmix_int(x, x_ptr, tonal.inmem, tonal.mem_fill, inlines.IMIN(len, opusConstants.ANALYSIS_BUF_SIZE-tonal.mem_fill), offset, c1, c2, C)

	if tonal.mem_fill+len < opusConstants.ANALYSIS_BUF_SIZE {
		tonal.mem_fill += len
		return
	}

	info = tonal.info[tonal.write_pos]
	tonal.write_pos++
	if tonal.write_pos >= opusConstants.DETECT_SIZE {
		tonal.write_pos -= opusConstants.DETECT_SIZE
	}

	for i := 0; i < N2; i++ {
		w := OpusTables.Analysis_window[i]
		input[2*i] = int(w * float32(tonal.inmem[i]))
		input[2*i+1] = int(w * float32(tonal.inmem[N2+i]))
		input[2*(N-i-1)] = int(w * float32(tonal.inmem[N-i-1]))
		input[2*(N-i-1)+1] = int(w * float32(tonal.inmem[N+N2-i-1]))
	}
	//copy(tonal.inmem, tonal.inmem[ANALYSIS_BUF_SIZE-240:ANALYSIS_BUF_SIZE])
	MemMove(tonal.inmem, opusConstants.ANALYSIS_BUF_SIZE-240, 0, 240)
	remaining := len - (opusConstants.ANALYSIS_BUF_SIZE - tonal.mem_fill)
	downmix_int(x, x_ptr, tonal.inmem, 240, remaining, offset+opusConstants.ANALYSIS_BUF_SIZE-tonal.mem_fill, c1, c2, C)
	tonal.mem_fill = 240 + remaining

	celt.Opus_fft(kfft, input, output)

	for i := 1; i < N2; i++ {
		X1r := float32(output[2*i] + output[2*(N-i)])
		X1i := float32(output[2*i+1] - output[2*(N-i)+1])
		X2r := float32(output[2*i+1] + output[2*(N-i)+1])
		X2i := float32(output[2*(N-i)] - output[2*i])

		angle := 0.5 / float32(M_PI) * fast_atan2f(X1i, X1r)
		d_angle := angle - tonal.angle[i]
		d2_angle := d_angle - tonal.d_angle[i]

		angle2 := 0.5 / float32(M_PI) * fast_atan2f(X2i, X2r)
		d_angle2 := angle2 - angle
		d2_angle2 := d_angle2 - d_angle

		mod1 := d2_angle - float32(math.Trunc(float64(0.5+d2_angle)))
		noisiness[i] = inlines.ABS16Float(mod1)
		mod1 *= mod1
		mod1 *= mod1

		mod2 := d2_angle2 - float32(math.Trunc(float64(0.5+d2_angle2)))
		noisiness[i] += inlines.ABS16Float(mod2)
		mod2 *= mod2
		mod2 *= mod2

		avg_mod := 0.25 * (tonal.d2_angle[i] + 2.0*mod1 + mod2)
		tonality[i] = 1.0/(1.0+40.0*16.0*pi4*avg_mod) - 0.015

		tonal.angle[i] = angle2
		tonal.d_angle[i] = d_angle2
		tonal.d2_angle[i] = mod2
	}

	frame_tonality = 0
	max_frame_tonality = 0
	info.Activity = 0
	frame_noisiness = 0
	frame_stationarity = 0
	if tonal.count == 0 {
		for b := 0; b < opusConstants.NB_TBANDS; b++ {
			tonal.lowE[b] = 1e10
			tonal.highE[b] = -1e10
		}
	}
	relativeE = 0
	frame_loudness = 0
	for b := 0; b < opusConstants.NB_TBANDS; b++ {
		E := float32(0)
		tE := float32(0)
		nE := float32(0)
		var L1, L2 float32
		for i := OpusTables.Tbands[b]; i < OpusTables.Tbands[b+1]; i++ {
			binE := float32(output[2*i])*float32(output[2*i]) + float32(output[2*(N-i)])*float32(output[2*(N-i)]) +
				float32(output[2*i+1])*float32(output[2*i+1]) + float32(output[2*(N-i)+1])*float32(output[2*(N-i)+1])
			binE *= 5.55e-17
			E += binE
			tE += binE * tonality[i]
			nE += binE * 2.0 * (0.5 - noisiness[i])
		}

		tonal.E[tonal.E_count][b] = E
		frame_noisiness += nE / (1e-15 + E)

		frame_loudness += float32(math.Sqrt(float64(E + 1e-10)))
		logE[b] = float32(math.Log(float64(E + 1e-10)))
		tonal.lowE[b] = inlines.MIN32Float(logE[b], tonal.lowE[b]+0.01)
		tonal.highE[b] = inlines.MAX32Float(logE[b], tonal.highE[b]-0.1)
		if tonal.highE[b] < tonal.lowE[b]+1.0 {
			tonal.highE[b] += 0.5
			tonal.lowE[b] -= 0.5
		}
		relativeE += (logE[b] - tonal.lowE[b]) / (1e-15 + tonal.highE[b] - tonal.lowE[b])

		L1 = 0
		L2 = 0
		for i := 0; i < opusConstants.NB_FRAMES; i++ {
			L1 += float32(math.Sqrt(float64(tonal.E[i][b])))
			L2 += tonal.E[i][b]
		}

		stationarity := inlines.MIN16Float(0.99, L1/float32(math.Sqrt(1e-15+float64(opusConstants.NB_FRAMES)*float64(L2))))
		stationarity *= stationarity
		stationarity *= stationarity
		frame_stationarity += stationarity
		band_tonality[b] = inlines.MAX16Float(tE/(1e-15+E), stationarity*tonal.prev_band_tonality[b])
		frame_tonality += band_tonality[b]
		if b >= opusConstants.NB_TBANDS-opusConstants.NB_TONAL_SKIP_BANDS {
			frame_tonality -= band_tonality[b-opusConstants.NB_TBANDS+opusConstants.NB_TONAL_SKIP_BANDS]
		}
		max_frame_tonality = inlines.MAX16Float(max_frame_tonality, (1.0+0.03*float32(b-opusConstants.NB_TBANDS))*frame_tonality)
		slope += band_tonality[b] * float32(b-8)
		tonal.prev_band_tonality[b] = band_tonality[b]
	}

	bandwidth_mask = 0
	bandwidth = 0
	maxE = 0
	noise_floor = 5.7e-4 / float32(int(1)<<int(inlines.IMAX(0, lsb_depth-8)))
	noise_floor *= float32(int(1) << (15 + CeltConstants.SIG_SHIFT))
	noise_floor *= noise_floor
	for b := 0; b < opusConstants.NB_TOT_BANDS; b++ {
		E := float32(0)
		band_start := OpusTables.Extra_bands[b]
		band_end := OpusTables.Extra_bands[b+1]
		for i := band_start; i < band_end; i++ {
			binE := float32(output[2*i])*float32(output[2*i]) + float32(output[2*(N-i)])*float32(output[2*(N-i)]) +
				float32(output[2*i+1])*float32(output[2*i+1]) + float32(output[2*(N-i)+1])*float32(output[2*(N-i)+1])
			E += binE
		}
		maxE = inlines.MAX32Float(maxE, E)
		tonal.meanE[b] = inlines.MAX32Float((1-alphaE2)*tonal.meanE[b], E)
		E = inlines.MAX32Float(E, tonal.meanE[b])
		bandwidth_mask = inlines.MAX32Float(0.05*bandwidth_mask, E)
		if E > 0.1*bandwidth_mask && E*1e9 > maxE && E > noise_floor*float32(band_end-band_start) {
			bandwidth = b
		}
	}
	if tonal.count <= 2 {
		bandwidth = 20
	}
	frame_loudness = 20 * float32(math.Log10(float64(frame_loudness)))
	tonal.Etracker = inlines.MAX32Float(tonal.Etracker-0.03, frame_loudness)
	tonal.lowECount *= (1 - alphaE)
	if frame_loudness < tonal.Etracker-30 {
		tonal.lowECount += alphaE
	}

	for i := 0; i < 8; i++ {
		sum := float32(0)
		for b := 0; b < 16; b++ {
			sum += OpusTables.Dct_table[i*16+b] * logE[b]
		}
		BFCC[i] = sum
	}

	frame_stationarity /= opusConstants.NB_TBANDS
	relativeE /= opusConstants.NB_TBANDS
	if tonal.count < 10 {
		relativeE = 0.5
	}
	frame_noisiness /= opusConstants.NB_TBANDS
	info.Activity = frame_noisiness + (1-frame_noisiness)*relativeE
	frame_tonality = max_frame_tonality / float32(opusConstants.NB_TBANDS-opusConstants.NB_TONAL_SKIP_BANDS)
	frame_tonality = inlines.MAX16Float(frame_tonality, tonal.prev_tonality*0.8)
	tonal.prev_tonality = frame_tonality

	slope /= 8 * 8
	info.Tonality_slope = slope

	tonal.E_count = (tonal.E_count + 1) % opusConstants.NB_FRAMES
	tonal.count++
	info.Tonality = frame_tonality

	for i := 0; i < 4; i++ {
		features[i] = -0.12299*(BFCC[i]+tonal.mem[i+24]) + 0.49195*(tonal.mem[i]+tonal.mem[i+16]) + 0.69693*tonal.mem[i+8] - 1.4349*tonal.cmean[i]
	}

	for i := 0; i < 4; i++ {
		tonal.cmean[i] = (1-alpha)*tonal.cmean[i] + alpha*BFCC[i]
	}

	for i := 0; i < 4; i++ {
		features[4+i] = 0.63246*(BFCC[i]-tonal.mem[i+24]) + 0.31623*(tonal.mem[i]-tonal.mem[i+16])
	}
	for i := 0; i < 3; i++ {
		features[8+i] = 0.53452*(BFCC[i]+tonal.mem[i+24]) - 0.26726*(tonal.mem[i]+tonal.mem[i+16]) - 0.53452*tonal.mem[i+8]
	}

	if tonal.count > 5 {
		for i := 0; i < 9; i++ {
			tonal.std[i] = (1-alpha)*tonal.std[i] + alpha*features[i]*features[i]
		}
	}

	for i := 0; i < 8; i++ {
		tonal.mem[i+24] = tonal.mem[i+16]
		tonal.mem[i+16] = tonal.mem[i+8]
		tonal.mem[i+8] = tonal.mem[i]
		tonal.mem[i] = BFCC[i]
	}
	for i := 0; i < 9; i++ {
		features[11+i] = float32(math.Sqrt(float64(tonal.std[i])))
	}
	features[20] = info.Tonality
	features[21] = info.Activity
	features[22] = frame_stationarity
	features[23] = info.Tonality_slope
	features[24] = tonal.lowECount

	if info.Enabled {
		mlp_process(OpusTables.Net, features, frame_probs)
		frame_probs[0] = 0.5 * (frame_probs[0] + 1)
		frame_probs[0] = 0.01 + 1.21*frame_probs[0]*frame_probs[0] - 0.23*float32(math.Pow(float64(frame_probs[0]), 10))
		frame_probs[1] = 0.5*frame_probs[1] + 0.5
		frame_probs[0] = frame_probs[1]*frame_probs[0] + (1-frame_probs[1])*0.5

		{
			tau := 0.00005 * frame_probs[1]
			beta := float32(0.05)
			{
				p := inlines.MAX16Float(0.05, inlines.MIN16Float(0.95, frame_probs[0]))
				q := inlines.MAX16Float(0.05, inlines.MIN16Float(0.95, tonal.music_prob))
				beta = 0.01 + 0.05*inlines.ABS16Float(p-q)/(p*(1-q)+q*(1-p))
			}
			p0 := (1-tonal.music_prob)*(1-tau) + tonal.music_prob*tau
			p1 := tonal.music_prob*(1-tau) + (1-tonal.music_prob)*tau
			p0 *= float32(math.Pow(float64(1-frame_probs[0]), float64(beta)))
			p1 *= float32(math.Pow(float64(frame_probs[0]), float64(beta)))
			tonal.music_prob = p1 / (p0 + p1)
			info.Music_prob = tonal.music_prob

			psum := float32(1e-20)
			speech0 := float32(math.Pow(float64(1-frame_probs[0]), float64(beta)))
			music0 := float32(math.Pow(float64(frame_probs[0]), float64(beta)))
			if tonal.count == 1 {
				tonal.pspeech[0] = 0.5
				tonal.pmusic[0] = 0.5
			}
			s0 := tonal.pspeech[0] + tonal.pspeech[1]
			m0 := tonal.pmusic[0] + tonal.pmusic[1]
			tonal.pspeech[0] = s0 * (1 - tau) * speech0
			tonal.pmusic[0] = m0 * (1 - tau) * music0
			for i := 1; i < opusConstants.DETECT_SIZE-1; i++ {
				tonal.pspeech[i] = tonal.pspeech[i+1] * speech0
				tonal.pmusic[i] = tonal.pmusic[i+1] * music0
			}
			tonal.pspeech[opusConstants.DETECT_SIZE-1] = m0 * tau * speech0
			tonal.pmusic[opusConstants.DETECT_SIZE-1] = s0 * tau * music0

			for i := 0; i < opusConstants.DETECT_SIZE; i++ {
				psum += tonal.pspeech[i] + tonal.pmusic[i]
			}
			psum = 1.0 / psum
			for i := 0; i < opusConstants.DETECT_SIZE; i++ {
				tonal.pspeech[i] *= psum
				tonal.pmusic[i] *= psum
			}
			psum = tonal.pmusic[0]
			for i := 1; i < opusConstants.DETECT_SIZE; i++ {
				psum += tonal.pspeech[i]
			}

			if frame_probs[1] > 0.75 {
				if tonal.music_prob > 0.9 {
					adapt := 1.0 / float32(tonal.music_confidence_count+1)
					tonal.music_confidence_count = inlines.IMIN(tonal.music_confidence_count, 500)
					tonal.music_confidence += adapt * inlines.MAX16Float(-0.2, frame_probs[0]-tonal.music_confidence)
				}
				if tonal.music_prob < 0.1 {
					adapt := 1.0 / float32(tonal.speech_confidence_count+1)
					tonal.speech_confidence_count = inlines.IMIN(tonal.speech_confidence_count, 500)
					tonal.speech_confidence += adapt * inlines.MIN16Float(0.2, frame_probs[0]-tonal.speech_confidence)
				}
			} else {
				if tonal.music_confidence_count == 0 {
					tonal.music_confidence = 0.9
				}
				if tonal.speech_confidence_count == 0 {
					tonal.speech_confidence = 0.1
				}
			}
		}
		if tonal.last_music != 0 {
			if tonal.music_prob > 0.5 {
				tonal.last_music = 1
			} else {
				tonal.last_music = 0
			}
		} else {
			tonal.last_transition = 0
		}
		if tonal.music_prob > 0.5 {
			tonal.last_music = 1
		} else {
			tonal.last_music = 0
		}
	} else {
		info.Music_prob = 0
	}

	info.Bandwidth = bandwidth
	info.Noisiness = frame_noisiness
	info.Valid = 1
}

func run_analysis(analysis *TonalityAnalysisState, celt_mode *celt.CeltMode, analysis_pcm []int16, analysis_pcm_ptr int, analysis_frame_size int, frame_size int, c1 int, c2 int, C int, Fs int, lsb_depth int, analysis_info *celt.AnalysisInfo) {
	offset := 0
	pcm_len := 0

	if analysis_pcm != nil {
		analysis_frame_size = inlines.IMIN((opusConstants.DETECT_SIZE-5)*Fs/100, analysis_frame_size)

		pcm_len = analysis_frame_size - analysis.analysis_offset
		offset = analysis.analysis_offset
		for pcm_len > 0 {
			chunk := inlines.IMIN(480, pcm_len)
			tonality_analysis(analysis, celt_mode, analysis_pcm, analysis_pcm_ptr, chunk, offset, c1, c2, C, lsb_depth)
			offset += 480
			pcm_len -= 480
		}
		analysis.analysis_offset = analysis_frame_size
		analysis.analysis_offset -= frame_size
	}

	analysis_info.Valid = 0
	tonality_get_info(analysis, analysis_info, frame_size)
}
