package celt

import (
	"encoding/json"
	"fmt"
	"math"

	"github.com/lostromb/concentus/go/comm"
	"github.com/lostromb/concentus/go/comm/arrayUtil"
)

type CeltDecoder struct {
	mode                  *CeltMode
	overlap               int
	channels              int
	stream_channels       int
	downsample            int
	start                 int
	end                   int
	signalling            int
	rng                   int
	error                 int
	last_pitch_index      int
	loss_count            int
	postfilter_period     int
	postfilter_period_old int
	postfilter_gain       int
	postfilter_gain_old   int
	postfilter_tapset     int
	postfilter_tapset_old int
	preemph_memD          []int
	decode_mem            [][]int
	lpc                   [][]int
	oldEBands             []int
	oldLogE               []int
	oldLogE2              []int
	backgroundLogE        []int
}

func (this *CeltDecoder) Reset() {
	this.mode = nil
	this.overlap = 0
	this.channels = 0
	this.stream_channels = 0
	this.downsample = 0
	this.start = 0
	this.end = 0
	this.signalling = 0
	this.PartialReset()
}

func (this *CeltDecoder) PartialReset() {
	this.rng = 0
	this.error = 0
	this.last_pitch_index = 0
	this.loss_count = 0
	this.postfilter_period = 0
	this.postfilter_period_old = 0
	this.postfilter_gain = 0
	this.postfilter_gain_old = 0
	this.postfilter_tapset = 0
	this.postfilter_tapset_old = 0
	this.preemph_memD = make([]int, 2)
	this.decode_mem = nil
	this.lpc = nil
	this.oldEBands = nil
	this.oldLogE = nil
	this.oldLogE2 = nil
	this.backgroundLogE = nil
}

func (this *CeltDecoder) ResetState() {
	this.PartialReset()

	if this.channels > 0 && this.mode != nil {
		this.decode_mem = make([][]int, this.channels)
		this.lpc = make([][]int, this.channels)
		for c := 0; c < this.channels; c++ {
			this.decode_mem[c] = make([]int, CeltConstants.DECODE_BUFFER_SIZE+this.mode.Overlap)
			this.lpc[c] = make([]int, CeltConstants.LPC_ORDER)
		}
		nbEBands := this.mode.nbEBands
		this.oldEBands = make([]int, 2*nbEBands)
		this.oldLogE = make([]int, 2*nbEBands)
		this.oldLogE2 = make([]int, 2*nbEBands)
		this.backgroundLogE = make([]int, 2*nbEBands)

		q28 := int(inlines.QCONST16(28.0, CeltConstants.DB_SHIFT))
		for i := 0; i < 2*nbEBands; i++ {
			this.oldLogE[i] = -q28
			this.oldLogE2[i] = -q28
		}
	}
}

func (this *CeltDecoder) Celt_decoder_init(sampling_rate int, channels int) int {
	ret := this.opus_custom_decoder_init(mode48000_960_120, channels)
	if ret != OpusError.OPUS_OK {
		return ret
	}
	this.downsample = Resampling_factor(sampling_rate)
	if this.downsample == 0 {
		return OpusError.OPUS_BAD_ARG
	}
	return OpusError.OPUS_OK
}

func (this *CeltDecoder) opus_custom_decoder_init(mode *CeltMode, channels int) int {
	if channels < 0 || channels > 2 {
		return OpusError.OPUS_BAD_ARG
	}
	if this == nil {
		return OpusError.OPUS_ALLOC_FAIL
	}
	this.Reset()
	this.mode = mode
	this.overlap = mode.Overlap
	this.stream_channels = channels
	this.channels = channels
	this.downsample = 1
	this.start = 0
	this.end = this.mode.effEBands
	this.signalling = 1
	this.loss_count = 0
	this.ResetState()
	return OpusError.OPUS_OK
}

func (this *CeltDecoder) celt_decode_lost(N int, LM int) {
	C := this.channels
	out_syn := make([][]int, 2)
	out_syn_ptrs := make([]int, 2)
	mode := this.mode
	nbEBands := mode.nbEBands
	overlap := mode.Overlap
	eBands := mode.eBands

	for c := 0; c < C; c++ {
		out_syn[c] = this.decode_mem[c]
		out_syn_ptrs[c] = CeltConstants.DECODE_BUFFER_SIZE - N
	}

	noise_based := 0
	if this.loss_count >= 5 || this.start != 0 {
		noise_based = 1
	}
	if noise_based != 0 {
		end := this.end
		effEnd := inlines.IMAX(this.start, inlines.IMIN(end, mode.effEBands))

		X := make([][]int, C)
		for c := range X {
			X[c] = make([]int, N)
		}

		decay := inlines.QCONST16(0.5, CeltConstants.DB_SHIFT)
		if this.loss_count == 0 {
			decay = inlines.QCONST16(1.5, CeltConstants.DB_SHIFT)
		}
		for c := 0; c < C; c++ {
			for i := this.start; i < end; i++ {
				idx := c*nbEBands + i
				this.oldEBands[idx] = inlines.MIN16Int(this.backgroundLogE[idx], this.oldEBands[idx]-int(decay))
			}
		}
		seed := this.rng
		for c := 0; c < C; c++ {
			for i := this.start; i < effEnd; i++ {
				boffs := int(eBands[i]) << LM
				blen := int(eBands[i+1]-eBands[i]) << LM
				for j := 0; j < blen; j++ {
					seed = celt_lcg_rand(seed)
					X[c][boffs+j] = int(seed) >> 20
				}
				renormalise_vector(X[c], boffs, blen, CeltConstants.Q15ONE)
			}
		}
		this.rng = seed

		for c := 0; c < C; c++ {
			//copy(this.decode_mem[c][:CeltConstants.DECODE_BUFFER_SIZE-N+(overlap>>1)], this.decode_mem[c][N:])
			arrayUtil.MemMove(this.decode_mem[c], N, 0, CeltConstants.DECODE_BUFFER_SIZE-N+(overlap>>1))
		}

		celt_synthesis(mode, X, out_syn, out_syn_ptrs, this.oldEBands, this.start, effEnd, C, C, 0, LM, this.downsample, 0)
	} else {
		fade := CeltConstants.Q15ONE
		pitch_index := 0
		if this.loss_count == 0 {
			this.last_pitch_index = celt_plc_pitch_search(this.decode_mem, C)
			pitch_index = this.last_pitch_index
		} else {
			pitch_index = this.last_pitch_index
			fade = int(math.Trunc(0.5 + (8)*((1)<<(15))))
		}

		etmp := make([]int, overlap)
		exc := make([]int, CeltConstants.MAX_PERIOD)
		window := mode.Window
		for c := 0; c < C; c++ {
			buf := this.decode_mem[c]
			for i := 0; i < CeltConstants.MAX_PERIOD; i++ {
				exc[i] = inlines.ROUND16Int(buf[CeltConstants.DECODE_BUFFER_SIZE-CeltConstants.MAX_PERIOD+i], CeltConstants.SIG_SHIFT)
			}

			if this.loss_count == 0 {
				ac := make([]int, CeltConstants.LPC_ORDER+1)
				comm.Celt_autocorr_with_window(exc, ac, window, overlap, CeltConstants.LPC_ORDER, CeltConstants.MAX_PERIOD)
				ac[0] += inlines.SHR32(ac[0], 13)
				for i := 1; i <= CeltConstants.LPC_ORDER; i++ {
					ac[i] -= inlines.MULT16_32_Q15Int(2*i*i, ac[i])
				}
				celt_lpc(this.lpc[c], ac, CeltConstants.LPC_ORDER)
			}

			exc_length := inlines.IMIN(2*pitch_index, CeltConstants.MAX_PERIOD)
			lpc_mem := make([]int, CeltConstants.LPC_ORDER)
			for i := 0; i < CeltConstants.LPC_ORDER; i++ {
				lpc_mem[i] = inlines.ROUND16Int(buf[CeltConstants.DECODE_BUFFER_SIZE-exc_length-1-i], CeltConstants.SIG_SHIFT)
			}
			kernels.Celt_fir_int(exc, CeltConstants.MAX_PERIOD-exc_length, this.lpc[c], 0, exc, CeltConstants.MAX_PERIOD-exc_length, exc_length, CeltConstants.LPC_ORDER, lpc_mem)

			shift := inlines.IMAX(0, 2*inlines.Celt_zlog2(inlines.Celt_maxabs16(exc, CeltConstants.MAX_PERIOD-exc_length, exc_length))-20)
			decay_length := exc_length >> 1
			E1 := 1
			E2 := 1
			for i := 0; i < decay_length; i++ {
				e := exc[CeltConstants.MAX_PERIOD-decay_length+i]
				E1 += inlines.SHR32(inlines.MULT16_16(e, e), shift)
				e = exc[CeltConstants.MAX_PERIOD-2*decay_length+i]
				E2 += inlines.SHR32(inlines.MULT16_16(e, e), shift)
			}
			E1 = inlines.MIN32(E1, E2)
			decay := inlines.Celt_sqrt(inlines.Frac_div32(inlines.SHR32(E1, 1), E2))

			//copy(buf[:CeltConstants.DECODE_BUFFER_SIZE-N], buf[N:])
			arrayUtil.MemMove(buf, N, 0, CeltConstants.DECODE_BUFFER_SIZE-N)
			extrapolation_offset := CeltConstants.MAX_PERIOD - pitch_index
			extrapolation_len := N + overlap
			attenuation := inlines.MULT16_16_Q15Int(fade, decay)
			S1 := 0
			j := 0
			for i := 0; i < extrapolation_len; i++ {
				if j >= pitch_index {
					j -= pitch_index
					attenuation = inlines.MULT16_16_Q15Int(attenuation, decay)
				}
				val := inlines.MULT16_16_Q15Int(attenuation, exc[extrapolation_offset+j])
				buf[CeltConstants.DECODE_BUFFER_SIZE-N+i] = inlines.SHL32(val, CeltConstants.SIG_SHIFT)
				tmp := inlines.ROUND16Int(buf[CeltConstants.DECODE_BUFFER_SIZE-CeltConstants.MAX_PERIOD-N+extrapolation_offset+j], CeltConstants.SIG_SHIFT)
				S1 += inlines.SHR32(inlines.MULT16_16(tmp, tmp), 8)
				j++
			}

			lpc_mem = make([]int, CeltConstants.LPC_ORDER)
			for i := 0; i < CeltConstants.LPC_ORDER; i++ {
				lpc_mem[i] = inlines.ROUND16Int(buf[CeltConstants.DECODE_BUFFER_SIZE-N-1-i], CeltConstants.SIG_SHIFT)
			}
			celt_iir(buf, CeltConstants.DECODE_BUFFER_SIZE-N, this.lpc[c], buf, CeltConstants.DECODE_BUFFER_SIZE-N, extrapolation_len, CeltConstants.LPC_ORDER, lpc_mem)

			S2 := 0
			for i := 0; i < extrapolation_len; i++ {
				tmp := inlines.ROUND16Int(buf[CeltConstants.DECODE_BUFFER_SIZE-N+i], CeltConstants.SIG_SHIFT)
				S2 += inlines.SHR32(inlines.MULT16_16(tmp, tmp), 8)
			}
			if !(S1 > inlines.SHR32(S2, 2)) {
				for i := 0; i < extrapolation_len; i++ {
					buf[CeltConstants.DECODE_BUFFER_SIZE-N+i] = 0
				}
			} else if S1 < S2 {
				ratio := inlines.Celt_sqrt(inlines.Frac_div32(inlines.SHR32(S1, 1)+1, S2+1))
				for i := 0; i < overlap; i++ {
					tmp_g := CeltConstants.Q15ONE - inlines.MULT16_16_Q15Int(window[i], CeltConstants.Q15ONE-ratio)
					buf[CeltConstants.DECODE_BUFFER_SIZE-N+i] = inlines.MULT16_32_Q15Int(tmp_g, buf[CeltConstants.DECODE_BUFFER_SIZE-N+i])
				}
				for i := overlap; i < extrapolation_len; i++ {
					buf[CeltConstants.DECODE_BUFFER_SIZE-N+i] = inlines.MULT16_32_Q15Int(ratio, buf[CeltConstants.DECODE_BUFFER_SIZE-N+i])
				}
			}

			comb_filter(etmp, 0, buf, CeltConstants.DECODE_BUFFER_SIZE, this.postfilter_period_old, this.postfilter_period, overlap, -this.postfilter_gain_old, -this.postfilter_gain, this.postfilter_tapset_old, this.postfilter_tapset, nil, 0)

			for i := 0; i < overlap/2; i++ {
				buf[CeltConstants.DECODE_BUFFER_SIZE+i] = inlines.MULT16_32_Q15Int(window[i], etmp[overlap-1-i]) + inlines.MULT16_32_Q15Int(window[overlap-i-1], etmp[i])
			}
		}
	}
	this.loss_count++
}

func (ed *CeltDecoder) Celt_decode_with_ec(data []byte, data_ptr int, length int, pcm []int16, pcm_ptr int, frame_size int, dec *comm.EntropyCoder, accum int) int {
	var c, i, N int
	var spread_decision, bits int
	var X [][]int
	var fine_quant, pulses, cap, offsets, fine_priority, tf_res []int
	var collapse_masks []int16
	out_syn := make([][]int, 2)
	out_syn_ptrs := make([]int, 2)
	var oldBandE, oldLogE, oldLogE2, backgroundLogE []int

	var shortBlocks, isTransient, intra_ener int
	CC := ed.channels
	var LM, M int
	var start, end int

	var effEnd, codedBands, alloc_trim, postfilter_pitch, postfilter_gain int
	intensity := 0
	dual_stereo := 0
	var total_bits, balance, tell, dynalloc_logp, postfilter_tapset int
	anti_collapse_rsv := 0
	anti_collapse_on := 0
	silence := 0
	C := ed.stream_channels
	var mode *CeltMode
	nbEBands := 0
	overlap := 0
	var eBands []int16

	mode = ed.mode
	nbEBands = mode.nbEBands
	overlap = mode.Overlap
	eBands = mode.eBands
	start = ed.start
	end = ed.end
	frame_size *= ed.downsample

	oldBandE = ed.oldEBands
	oldLogE = ed.oldLogE
	oldLogE2 = ed.oldLogE2
	backgroundLogE = ed.backgroundLogE

	{
		for LM = 0; LM <= mode.MaxLM; LM++ {
			if mode.ShortMdctSize<<LM == frame_size {
				break
			}
		}
		if LM > mode.MaxLM {
			return OpusError.OPUS_BAD_ARG
		}
	}
	M = 1 << LM

	if length < 0 || length > 1275 || pcm == nil {
		return OpusError.OPUS_BAD_ARG
	}

	N = M * mode.ShortMdctSize
	c = 0
	for {
		out_syn[c] = ed.decode_mem[c]
		out_syn_ptrs[c] = CeltConstants.DECODE_BUFFER_SIZE - N
		c++
		if !(c < CC) {
			break
		}
	}

	effEnd = end
	if effEnd > mode.effEBands {
		effEnd = mode.effEBands
	}

	if data == nil || length <= 1 {
		ed.celt_decode_lost(N, LM)
		deemphasis(out_syn, out_syn_ptrs, pcm, pcm_ptr, N, CC, ed.downsample, mode.Preemph, ed.preemph_memD, accum)
		return frame_size / ed.downsample
	}

	if dec == nil {
		dec = comm.NewEntropyCoder()
		dec.Dec_init(data, data_ptr, length)
	}

	if C == 1 {
		for i = 0; i < nbEBands; i++ {
			oldBandE[i] = inlines.MAX16Int(oldBandE[i], oldBandE[nbEBands+i])
		}
	}

	total_bits = length * 8
	tell = dec.Tell()

	if tell >= total_bits {
		silence = 1
	} else if tell == 1 {
		silence = dec.Dec_bit_logp(15)
	} else {
		silence = 0
	}

	if silence != 0 {
		tell = length * 8
		dec.Nbits_total += tell - dec.Tell()
	}

	postfilter_gain = 0
	postfilter_pitch = 0
	postfilter_tapset = 0
	if start == 0 && tell+16 <= total_bits {
		if dec.Dec_bit_logp(1) != 0 {
			var qg, octave int
			octave = int(dec.Dec_uint(6))
			postfilter_pitch = (16 << octave) + dec.Dec_bits(4+int(octave)) - 1
			qg = dec.Dec_bits(3)
			if dec.Tell()+2 <= total_bits {
				postfilter_tapset = dec.Dec_icdf(tapset_icdf, 2)
			}
			postfilter_gain = int(int16(math.Trunc(0.5+(0.09375)*float64(int32(1)<<(15))))) * (qg + 1)
			//System.out.println("postfilter_pitch: " + postfilter_pitch)
		}
		tell = dec.Tell()
	}

	if LM > 0 && tell+3 <= total_bits {
		isTransient = dec.Dec_bit_logp(3)
		tell = dec.Tell()
	} else {
		isTransient = 0
	}

	if isTransient != 0 {
		shortBlocks = M
	} else {
		shortBlocks = 0
	}

	intra_ener = 0
	if tell+3 <= total_bits {
		intra_ener = dec.Dec_bit_logp(3)
	}

	unquant_coarse_energy(mode, start, end, oldBandE, intra_ener, dec, C, LM)

	tf_res = make([]int, nbEBands)
	tf_decode(start, end, isTransient, tf_res, LM, dec)

	tell = dec.Tell()
	spread_decision = Spread.SPREAD_NORMAL
	if tell+4 <= total_bits {
		spread_decision = dec.Dec_icdf(spread_icdf[:], 5)
	}

	cap = make([]int, nbEBands)
	init_caps(mode, cap, LM, C)

	offsets = make([]int, nbEBands)
	dynalloc_logp = 6
	total_bits <<= BITRES
	tell = dec.Tell_frac()
	for i = start; i < end; i++ {
		var width, quanta int
		dynalloc_loop_logp := dynalloc_logp
		boost := 0
		width = C * (int(eBands[i+1]) - int(eBands[i])) << LM
		quanta = inlines.IMIN(width<<BITRES, inlines.IMAX(6<<BITRES, width))
		for tell+(dynalloc_loop_logp<<BITRES) < total_bits && boost < cap[i] {
			flag := dec.Dec_bit_logp(int64(dynalloc_loop_logp))
			tell = dec.Tell_frac()
			if flag == 0 {
				break
			}
			boost += quanta
			total_bits -= quanta
			dynalloc_loop_logp = 1
		}
		offsets[i] = boost
		if boost > 0 {
			dynalloc_logp = inlines.IMAX(2, dynalloc_logp-1)
		}
	}

	fine_quant = make([]int, nbEBands)
	alloc_trim = 5
	if tell+(6<<BITRES) <= total_bits {
		alloc_trim = dec.Dec_icdf(trim_icdf[:], 7)
	}

	bits = (length*8)<<BITRES - dec.Tell_frac() - 1
	if isTransient != 0 && LM >= 2 && bits >= ((LM+2)<<BITRES) {
		anti_collapse_rsv = 1 << BITRES
	} else {
		anti_collapse_rsv = 0
	}
	bits -= anti_collapse_rsv

	pulses = make([]int, nbEBands)
	fine_priority = make([]int, nbEBands)

	boxed_intensity := &comm.BoxedValueInt{Val: intensity}
	boxed_dual_stereo := &comm.BoxedValueInt{Val: dual_stereo}
	boxed_balance := &comm.BoxedValueInt{Val: 0}
	codedBands = compute_allocation(mode, start, end, offsets, cap, alloc_trim, boxed_intensity, boxed_dual_stereo, bits, boxed_balance, pulses, fine_quant, fine_priority, C, LM, dec, 0, 0, 0)
	intensity = boxed_intensity.Val
	dual_stereo = boxed_dual_stereo.Val
	balance = boxed_balance.Val

	unquant_fine_energy(mode, start, end, oldBandE, fine_quant, dec, C)
	c = 0
	for {
		//	copy(ed.decode_mem[c][0:], ed.decode_mem[c][N:CeltConstants.DECODE_BUFFER_SIZE-N+overlap/2])
		arrayUtil.MemMove(ed.decode_mem[c], N, 0, CeltConstants.DECODE_BUFFER_SIZE-N+overlap/2)
		c++
		if !(c < CC) {
			break
		}
	}

	collapse_masks = make([]int16, C*nbEBands)
	X = arrayUtil.InitTwoDimensionalArrayInt(C, N)

	boxed_rng := &comm.BoxedValueInt{Val: ed.rng}
	var Y_ []int
	if C == 2 {
		Y_ = X[1]
	}

	quant_all_bands(0, mode, start, end, X[0], Y_, collapse_masks, nil, pulses, shortBlocks, spread_decision, dual_stereo, intensity, tf_res, length*(8<<BITRES)-anti_collapse_rsv, balance, dec, LM, codedBands, boxed_rng)

	ed.rng = boxed_rng.Val

	if anti_collapse_rsv > 0 {
		anti_collapse_on = dec.Dec_bits(1)
	}

	unquant_energy_finalise(mode, start, end, oldBandE, fine_quant, fine_priority, length*8-dec.Tell(), dec, C)

	if anti_collapse_on != 0 {
		anti_collapse(mode, X, collapse_masks, LM, C, N, start, end, oldBandE, oldLogE, oldLogE2, pulses, ed.rng)
	}

	if silence != 0 {
		for i = 0; i < C*nbEBands; i++ {
			oldBandE[i] = -int(0.5 + 28.0*float64(int(1)<<CeltConstants.DB_SHIFT))
		}
	}

	celt_synthesis(mode, X, out_syn, out_syn_ptrs, oldBandE, start, effEnd, C, CC, isTransient, LM, ed.downsample, silence)

	c = 0

	for {
		ed.postfilter_period = inlines.IMAX(ed.postfilter_period, CeltConstants.COMBFILTER_MINPERIOD)
		ed.postfilter_period_old = inlines.IMAX(ed.postfilter_period_old, CeltConstants.COMBFILTER_MINPERIOD)

		comb_filter(out_syn[c], out_syn_ptrs[c], out_syn[c], out_syn_ptrs[c], ed.postfilter_period_old, ed.postfilter_period, mode.ShortMdctSize, ed.postfilter_gain_old, ed.postfilter_gain, ed.postfilter_tapset_old, ed.postfilter_tapset, mode.Window, overlap)
		if LM != 0 {
			comb_filter(out_syn[c], out_syn_ptrs[c]+mode.ShortMdctSize, out_syn[c], out_syn_ptrs[c]+mode.ShortMdctSize, ed.postfilter_period, postfilter_pitch, N-mode.ShortMdctSize, ed.postfilter_gain, postfilter_gain, ed.postfilter_tapset, postfilter_tapset, mode.Window, overlap)
		}
		c++
		if !(c < CC) {
			break
		}
	}
	ed.postfilter_period_old = ed.postfilter_period
	ed.postfilter_gain_old = ed.postfilter_gain
	ed.postfilter_tapset_old = ed.postfilter_tapset
	ed.postfilter_period = postfilter_pitch
	ed.postfilter_gain = postfilter_gain
	ed.postfilter_tapset = postfilter_tapset
	if LM != 0 {
		ed.postfilter_period_old = ed.postfilter_period
		ed.postfilter_gain_old = ed.postfilter_gain
		ed.postfilter_tapset_old = ed.postfilter_tapset
	}

	if C == 1 {
		copy(oldBandE[nbEBands:], oldBandE[:nbEBands])
	}

	if isTransient == 0 {
		var max_background_increase int
		copy(oldLogE2, oldLogE)
		copy(oldLogE, oldBandE)
		if ed.loss_count < 10 {
			max_background_increase = int(0.5 + 0.001*float64(int(1)<<CeltConstants.DB_SHIFT))
		} else {
			max_background_increase = int(0.5 + 1.0*float64(int(1)<<CeltConstants.DB_SHIFT))
		}
		for i = 0; i < 2*nbEBands; i++ {
			backgroundLogE[i] = inlines.MIN16Int(backgroundLogE[i]+max_background_increase, oldBandE[i])
		}
	} else {
		for i = 0; i < 2*nbEBands; i++ {
			oldLogE[i] = inlines.MIN16Int(oldLogE[i], oldBandE[i])
		}
	}
	c = 0
	for {
		for i = 0; i < start; i++ {
			oldBandE[c*nbEBands+i] = 0
			oldLogE[c*nbEBands+i] = -int(0.5 + 28.0*float64(int32(1)<<CeltConstants.DB_SHIFT))
			oldLogE2[c*nbEBands+i] = -int(0.5 + 28.0*float64(int32(1)<<CeltConstants.DB_SHIFT))
		}
		for i = end; i < nbEBands; i++ {
			oldBandE[c*nbEBands+i] = 0
			oldLogE[c*nbEBands+i] = -int(0.5 + 28.0*float64(int32(1)<<CeltConstants.DB_SHIFT))
			oldLogE2[c*nbEBands+i] = -int(0.5 + 28.0*float64(int32(1)<<CeltConstants.DB_SHIFT))
		}
		c++
		if !(c < 2) {
			break
		}
	}
	ed.rng = int(dec.Rng)
	if comm.Debug {
		Xstr, _ := json.Marshal(out_syn)
		fmt.Printf("out_syn1:%s\r\n", Xstr)
	}
	deemphasis(out_syn, out_syn_ptrs, pcm, pcm_ptr, N, CC, ed.downsample, mode.Preemph, ed.preemph_memD, accum)
	ed.loss_count = 0

	if dec.Tell() > 8*length {
		return OpusError.OPUS_INTERNAL_ERROR
	}
	if dec.Get_error() != 0 {
		ed.error = 1
	}
	return frame_size / ed.downsample
}

func (this *CeltDecoder) SetStartBand(value int) {
	if value < 0 || value >= this.mode.nbEBands {
		panic("Start band above max number of ebands (or negative)")
	}
	this.start = value
}

func (this *CeltDecoder) SetEndBand(value int) {
	if value < 1 || value > this.mode.nbEBands {
		panic("End band above max number of ebands (or less than 1)")
	}
	this.end = value
}

func (this *CeltDecoder) SetChannels(value int) {
	if value < 1 || value > 2 {
		panic("Channel count must be 1 or 2")
	}
	this.stream_channels = value
}

func (this *CeltDecoder) GetAndClearError() int {
	returnVal := this.error
	this.error = 0
	return returnVal
}

func (this *CeltDecoder) GetLookahead() int {
	return this.overlap / this.downsample
}

func (this *CeltDecoder) GetPitch() int {
	return this.postfilter_period
}

func (this *CeltDecoder) GetMode() *CeltMode {
	return this.mode
}

func (this *CeltDecoder) SetSignalling(value int) {
	this.signalling = value
}

func (this *CeltDecoder) GetFinalRange() int {
	return this.rng
}
