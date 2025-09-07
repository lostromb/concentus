package celt

import (
	"math"

	"github.com/lostromb/concentus/go/comm"
	"github.com/lostromb/concentus/go/comm/arrayUtil"
	"github.com/lostromb/concentus/go/comm/opusConstants"
)

type CeltEncoder struct {
	mode              *CeltMode
	channels          int
	stream_channels   int
	force_intra       int
	clip              int
	disable_pf        int
	complexity        int
	upsample          int
	start             int
	end               int
	bitrate           int
	vbr               int
	signalling        int
	constrained_vbr   int
	loss_rate         int
	lsb_depth         int
	variable_duration int
	lfe               int
	rng               int
	spread_decision   int
	delayedIntra      int
	tonal_average     int
	lastCodedBands    int
	hf_average        int
	tapset_decision   int
	prefilter_period  int
	prefilter_gain    int
	prefilter_tapset  int
	consec_transient  int
	analysis          AnalysisInfo
	preemph_memE      [2]int
	preemph_memD      [2]int
	vbr_reservoir     int
	vbr_drift         int
	vbr_offset        int
	vbr_count         int
	overlap_max       int
	stereo_saving     int
	intensity         int
	energy_mask       []int
	spec_avg          int
	in_mem            [][]int
	prefilter_mem     [][]int
	oldBandE          [][]int
	oldLogE           [][]int
	oldLogE2          [][]int
}

func (this *CeltEncoder) Reset() {
	this.mode = nil
	this.channels = 0
	this.stream_channels = 0
	this.force_intra = 0
	this.clip = 0
	this.disable_pf = 0
	this.complexity = 0
	this.upsample = 0
	this.start = 0
	this.end = 0
	this.bitrate = 0
	this.vbr = 0
	this.signalling = 0
	this.constrained_vbr = 0
	this.loss_rate = 0
	this.lsb_depth = 0
	this.variable_duration = OPUS_FRAMESIZE_UNKNOWN
	this.lfe = 0
	this.PartialReset()
}

func (this *CeltEncoder) PartialReset() {
	this.rng = 0
	this.spread_decision = 0
	this.delayedIntra = 0
	this.tonal_average = 0
	this.lastCodedBands = 0
	this.hf_average = 0
	this.tapset_decision = 0
	this.prefilter_period = 0
	this.prefilter_gain = 0
	this.prefilter_tapset = 0
	this.consec_transient = 0
	this.analysis.Reset()
	this.preemph_memE[0] = 0
	this.preemph_memE[1] = 0
	this.preemph_memD[0] = 0
	this.preemph_memD[1] = 0
	this.vbr_reservoir = 0
	this.vbr_drift = 0
	this.vbr_offset = 0
	this.vbr_count = 0
	this.overlap_max = 0
	this.stereo_saving = 0
	this.intensity = 0
	this.energy_mask = nil
	this.spec_avg = 0
	this.in_mem = nil
	this.prefilter_mem = nil
	this.oldBandE = nil
	this.oldLogE = nil
	this.oldLogE2 = nil
}

func (this *CeltEncoder) ResetState() {
	this.PartialReset()

	this.in_mem = arrayUtil.InitTwoDimensionalArrayInt(this.channels, this.mode.Overlap)
	this.prefilter_mem = arrayUtil.InitTwoDimensionalArrayInt(this.channels, CeltConstants.COMBFILTER_MAXPERIOD)
	this.oldBandE = arrayUtil.InitTwoDimensionalArrayInt(this.channels, this.mode.nbEBands)
	this.oldLogE = arrayUtil.InitTwoDimensionalArrayInt(this.channels, this.mode.nbEBands)
	this.oldLogE2 = arrayUtil.InitTwoDimensionalArrayInt(this.channels, this.mode.nbEBands)

	for i := 0; i < this.mode.nbEBands; i++ {
		val := -int(math.Trunc(0.5 + 28.0 + float64(int(1<<CeltConstants.DB_SHIFT))))
		this.oldLogE[0][i] = val
		this.oldLogE2[0][i] = val
	}
	if this.channels == 2 {
		for i := 0; i < this.mode.nbEBands; i++ {
			val := -int(math.Trunc(0.5 + 28.0 + float64(int(1<<CeltConstants.DB_SHIFT))))
			this.oldLogE[1][i] = val
			this.oldLogE2[1][i] = val
		}
	}
	this.vbr_offset = 0
	this.delayedIntra = 1
	this.spread_decision = SPREAD_NORMAL
	this.tonal_average = 256
	this.hf_average = 0
	this.tapset_decision = 0
}

func (this *CeltEncoder) opus_custom_encoder_init_arch(mode *CeltMode, channels int) int {
	if channels < 0 || channels > 2 {
		return OpusError.OPUS_BAD_ARG
	}
	if this == nil || mode == nil {
		return OpusError.OPUS_ALLOC_FAIL
	}
	this.Reset()
	this.mode = mode
	this.stream_channels = channels
	this.channels = channels
	this.upsample = 1
	this.start = 0
	this.end = this.mode.effEBands
	this.signalling = 1
	this.constrained_vbr = 1
	this.clip = 1
	this.bitrate = opusConstants.OPUS_BITRATE_MAX
	this.vbr = 0
	this.force_intra = 0
	this.complexity = 5
	this.lsb_depth = 24
	this.ResetState()
	return OpusError.OPUS_OK
}

func (this *CeltEncoder) Celt_encoder_init(sampling_rate int, channels int) int {
	ret := this.opus_custom_encoder_init_arch(mode48000_960_120, channels)
	if ret != OpusError.OPUS_OK {
		return ret
	}
	this.upsample = Resampling_factor(sampling_rate)
	return OpusError.OPUS_OK
}

func (this *CeltEncoder) run_prefilter(input [][]int, prefilter_mem [][]int, CC int, N int, prefilter_tapset int, pitch *comm.BoxedValueInt, gain *comm.BoxedValueInt, qgain *comm.BoxedValueInt, enabled int, nbAvailableBytes int) int {
	mode := this.mode
	overlap := mode.Overlap
	pre := make([][]int, CC)
	for z := range pre {
		pre[z] = make([]int, N+CeltConstants.COMBFILTER_MAXPERIOD)
	}

	for c := 0; c < CC; c++ {
		copy(pre[c][:CeltConstants.COMBFILTER_MAXPERIOD], prefilter_mem[c])
		copy(pre[c][CeltConstants.COMBFILTER_MAXPERIOD:], input[c][overlap:overlap+N])
	}
	pitch_index := comm.BoxedValueInt{0}
	var gain1 int
	if enabled != 0 {
		pitch_buf := make([]int, (CeltConstants.COMBFILTER_MAXPERIOD+N)>>1)
		pitch_downsample(pre, pitch_buf, CeltConstants.COMBFILTER_MAXPERIOD+N, CC)

		pitch_search(pitch_buf, CeltConstants.COMBFILTER_MAXPERIOD>>1, pitch_buf, N, CeltConstants.COMBFILTER_MAXPERIOD-3*CeltConstants.COMBFILTER_MINPERIOD, &pitch_index)
		pitch_index.Val = CeltConstants.COMBFILTER_MAXPERIOD - pitch_index.Val
		gain1 = remove_doubling(pitch_buf, CeltConstants.COMBFILTER_MAXPERIOD, CeltConstants.COMBFILTER_MINPERIOD, N, &pitch_index, this.prefilter_period, this.prefilter_gain)
		if pitch_index.Val > CeltConstants.COMBFILTER_MAXPERIOD-2 {
			pitch_index.Val = CeltConstants.COMBFILTER_MAXPERIOD - 2
		}
		gain1 = int(inlines.MULT16_16_Q15(int16(math.Trunc(0.5+0.7*float64(int(1<<15)))), int16(gain1)))
		if this.loss_rate > 2 {
			gain1 = inlines.HALF32(gain1)
		}
		if this.loss_rate > 4 {
			gain1 = inlines.HALF32(gain1)
		}
		if this.loss_rate > 8 {
			gain1 = 0
		}
	} else {
		gain1 = 0
		pitch_index := CeltConstants.COMBFILTER_MINPERIOD
		pitch.Val = pitch_index
	}

	pf_threshold := int16(math.Trunc(0.5 + 0.2*(1<<15)))
	if inlines.Abs(pitch.Val-this.prefilter_period)*10 > pitch.Val {
		pf_threshold += int16(math.Trunc(0.5 + 0.2*(1<<15)))
	}
	if nbAvailableBytes < 25 {
		pf_threshold += int16(math.Trunc(0.5 + 0.1*(1<<15)))
	}
	if nbAvailableBytes < 35 {
		pf_threshold += int16(math.Trunc(0.5 + 0.1*(1<<15)))
	}
	if this.prefilter_gain > int(math.Trunc(0.5+0.4*(1<<15))) {
		pf_threshold += int16(math.Trunc(0.5 + 0.1*(1<<15)))
	}
	if this.prefilter_gain > int(math.Trunc(0.5+0.55*(1<<15))) {
		pf_threshold += int16(math.Trunc(0.5 + 0.1*(1<<15)))
	}
	pf_threshold = inlines.MAX16(pf_threshold, int16(math.Trunc(0.5+0.2*(1<<15))))

	pf_on := 0
	qg := 0
	if gain1 < int(pf_threshold) {
		gain1 = 0
	} else {
		if inlines.ABS32(gain1-this.prefilter_gain) < int(math.Trunc(0.5+0.1*float64(1<<15))) {
			gain1 = this.prefilter_gain
		}
		qg = ((gain1 + 1536) >> 10) / 3
		if qg < 0 {
			qg = 0
		} else if qg > 7 {
			qg = 7
		}
		gain1 = int(math.Trunc(0.5 + 0.09375*(1<<15)*float64(qg+1)))
		pf_on = 1
	}

	gain.Val = gain1
	pitch.Val = pitch_index.Val
	qgain.Val = qg

	for c := 0; c < CC; c++ {
		offset := mode.ShortMdctSize - overlap
		if this.prefilter_period < CeltConstants.COMBFILTER_MINPERIOD {
			this.prefilter_period = CeltConstants.COMBFILTER_MINPERIOD
		}
		copy(input[c][:overlap], this.in_mem[c])
		if offset != 0 {
			comb_filter(input[c][:overlap], overlap, pre[c][:CeltConstants.COMBFILTER_MAXPERIOD], CeltConstants.COMBFILTER_MAXPERIOD, this.prefilter_period, this.prefilter_period, offset, -this.prefilter_gain, -this.prefilter_gain, this.prefilter_tapset, this.prefilter_tapset, nil, 0)
		}
		comb_filter(input[c][overlap:overlap+offset], overlap+offset, pre[c][CeltConstants.COMBFILTER_MAXPERIOD+offset:], CeltConstants.COMBFILTER_MAXPERIOD+offset, this.prefilter_period, pitch.Val, N-offset, -this.prefilter_gain, -gain1, this.prefilter_tapset, prefilter_tapset, mode.Window, overlap)
		copy(this.in_mem[c], input[c][N:N+overlap])
		if N > CeltConstants.COMBFILTER_MAXPERIOD {
			copy(prefilter_mem[c], pre[c][N:N+CeltConstants.COMBFILTER_MAXPERIOD])
		} else {
			copy(prefilter_mem[c][:CeltConstants.COMBFILTER_MAXPERIOD-N], prefilter_mem[c][N:])
			copy(prefilter_mem[c][CeltConstants.COMBFILTER_MAXPERIOD-N:], pre[c][CeltConstants.COMBFILTER_MAXPERIOD:CeltConstants.COMBFILTER_MAXPERIOD+N])
		}
	}

	return pf_on
}

func (this *CeltEncoder) Celt_encode_with_ec(pcm []int16, pcm_ptr int, frame_size int, compressed []byte, compressed_ptr int, nbCompressedBytes int, enc *comm.EntropyCoder) int {
	//PrintFuncArgs(pcm, pcm_ptr, frame_size, compressed, compressed_ptr, nbCompressedBytes, enc)

	var i, c, N int
	var bits int
	var input [][]int
	var freq [][]int
	var X [][]int
	var bandE [][]int
	var bandLogE [][]int
	var bandLogE2 [][]int
	var fine_quant []int
	var error [][]int
	var pulses []int
	var cap []int
	var offsets []int
	var fine_priority []int
	var tf_res []int
	var collapse_masks []int16
	var shortBlocks = 0
	var isTransient = 0
	var CC = this.channels
	var C = this.stream_channels
	var LM, M int
	var tf_select int
	var nbFilledBytes, nbAvailableBytes int
	var start int
	var end int
	var effEnd int
	var codedBands int
	//var tf_sum int
	var alloc_trim int
	var pitch_index = CeltConstants.COMBFILTER_MINPERIOD
	var gain1 = 0
	var dual_stereo = 0
	var effectiveBytes int
	var dynalloc_logp int
	var vbr_rate int
	var total_bits int
	var total_boost int
	var balance int
	var tell int
	var prefilter_tapset = 0
	var pf_on int
	var anti_collapse_rsv int
	var anti_collapse_on = 0
	var silence = 0
	var tf_chan = 0
	var tf_estimate int
	var pitch_change = 0
	var tot_boost int
	var sample_max int
	var maxDepth int
	var mode *CeltMode
	var nbEBands int
	var overlap int
	var eBands []int16
	var secondMdct int
	var signalBandwidth int
	var transient_got_disabled = 0
	var surround_masking = 0
	var temporal_vbr = 0
	var surround_trim = 0
	var equiv_rate = 510000
	var surround_dynalloc []int

	mode = this.mode
	nbEBands = mode.nbEBands
	overlap = mode.Overlap
	eBands = mode.eBands
	start = this.start
	end = this.end
	tf_estimate = 0
	if nbCompressedBytes < 2 || pcm == nil {
		return OpusError.OPUS_BAD_ARG
	}

	frame_size *= this.upsample
	for LM = 0; LM <= mode.MaxLM; LM++ {
		if mode.ShortMdctSize<<LM == frame_size {
			break
		}
	}
	if LM > mode.MaxLM {
		return OpusError.OPUS_BAD_ARG
	}
	M = 1 << LM
	N = M * mode.ShortMdctSize

	if enc == nil {
		tell = 1
		nbFilledBytes = 0
	} else {
		tell = enc.Tell()
		nbFilledBytes = (tell + 4) >> 3
	}

	inlines.OpusAssert(this.signalling == 0)

	/* Can't produce more than 1275 output bytes */
	nbCompressedBytes = inlines.IMIN(nbCompressedBytes, 1275)
	nbAvailableBytes = nbCompressedBytes - nbFilledBytes

	if this.vbr != 0 && this.bitrate != opusConstants.OPUS_BITRATE_MAX {
		den := mode.Fs >> BITRES
		vbr_rate = (this.bitrate*frame_size + (den >> 1)) / den
		effectiveBytes = vbr_rate >> (3 + BITRES)
	} else {
		var tmp int
		vbr_rate = 0
		tmp = this.bitrate * frame_size
		if tell > 1 {
			tmp += tell
		}
		if this.bitrate != opusConstants.OPUS_BITRATE_MAX {
			nbCompressedBytes = inlines.IMAX(2, inlines.IMIN(nbCompressedBytes, (tmp+4*mode.Fs)/(8*mode.Fs)-comm.BoolToInt(this.signalling != 0)))
		}
		effectiveBytes = nbCompressedBytes
	}
	if this.bitrate != opusConstants.OPUS_BITRATE_MAX {
		equiv_rate = this.bitrate - (40*C+20)*((400>>LM)-50)
	}

	if enc == nil {
		enc = comm.NewEntropyCoder()
		enc.Enc_init(compressed, compressed_ptr, nbCompressedBytes)
	}

	if vbr_rate > 0 {
		/* Computes the max bit-rate allowed in VBR mode to avoid violating the
		    target rate and buffering.
		   We must do this up front so that bust-prevention logic triggers
		    correctly if we don't have enough bits. */
		if this.constrained_vbr != 0 {
			var vbr_bound int
			var max_allowed int
			/* We could use any multiple of vbr_rate as bound (depending on the
			    delay).
			   This is clamped to ensure we use at least two bytes if the encoder
			    was entirely empty, but to allow 0 in hybrid mode. */
			vbr_bound = vbr_rate
			tmp := 0
			if tell == 1 {
				tmp = 2
			}
			max_allowed = inlines.IMIN(inlines.IMAX(tmp, (vbr_rate+vbr_bound-this.vbr_reservoir)>>(BITRES+3)), nbAvailableBytes)
			if max_allowed < nbAvailableBytes {
				nbCompressedBytes = nbFilledBytes + max_allowed
				nbAvailableBytes = max_allowed
				enc.Enc_shrink(nbCompressedBytes)
			}
		}
	}
	total_bits = nbCompressedBytes * 8

	effEnd = end
	if effEnd > mode.effEBands {
		effEnd = mode.effEBands
	}

	input = arrayUtil.InitTwoDimensionalArrayInt(CC, N+overlap)

	sample_max = inlines.MAX32(this.overlap_max, int(inlines.Celt_maxabs32Short(pcm, pcm_ptr, C*(N-overlap)/this.upsample)))
	this.overlap_max = int(inlines.Celt_maxabs32Short(pcm, pcm_ptr+(C*(N-overlap)/this.upsample), C*overlap/this.upsample))
	sample_max = inlines.MAX32(sample_max, this.overlap_max)
	silence = comm.BoolToInt(sample_max == 0)
	if tell == 1 {
		enc.Enc_bit_logp(silence, 15)
	} else {
		silence = 0
	}
	if silence != 0 {
		/*In VBR mode there is no need to send more than the minimum. */
		if vbr_rate > 0 {
			effectiveBytes = inlines.IMIN(nbCompressedBytes, nbFilledBytes+2)
			nbCompressedBytes = effectiveBytes
			total_bits = nbCompressedBytes * 8
			nbAvailableBytes = 2
			enc.Enc_shrink(nbCompressedBytes)
		}
		/* Pretend we've filled all the remaining bits with zeros
		   (that's what the initialiser did anyway) */
		tell = nbCompressedBytes * 8
		enc.Nbits_total += tell - enc.Tell()
	}
	c = 0
	boxed_memE := comm.BoxedValueInt{0}
	for {
		var need_clip int = 0
		boxed_memE.Val = this.preemph_memE[c]
		celt_preemphasis(pcm, pcm_ptr+c, input[c], overlap, N, CC, this.upsample,
			mode.Preemph, &boxed_memE, need_clip)
		this.preemph_memE[c] = boxed_memE.Val
		c++
		if c < CC {
			continue
		}
		break
	}

	/* Find pitch period and gain */

	var enabled int
	var qg int
	enabled = comm.BoolToInt(((this.lfe != 0 && nbAvailableBytes > 3) || nbAvailableBytes > 12*C) && start == 0 && silence == 0 && this.disable_pf == 0 && this.complexity >= 5 && !(this.consec_transient != 0 && LM != 3 && this.variable_duration == OPUS_FRAMESIZE_VARIABLE))

	prefilter_tapset = this.tapset_decision
	boxed_pitch_index := comm.BoxedValueInt{0}
	boxed_gain1 := comm.BoxedValueInt{0}
	boxed_qg := comm.BoxedValueInt{0}
	pf_on = this.run_prefilter(input, this.prefilter_mem, CC, N, prefilter_tapset, &boxed_pitch_index, &boxed_gain1, &boxed_qg, enabled, nbAvailableBytes)
	pitch_index = boxed_pitch_index.Val
	gain1 = boxed_gain1.Val
	qg = boxed_qg.Val

	if (gain1 > int(math.Trunc(0.5+(.4)*float64(1<<(15)))) || this.prefilter_gain > int(math.Trunc(0.5+(.4)*float64(1<<(15))))) && (this.analysis.Valid == 0 || this.analysis.Tonality > .3) && (pitch_index > int(1.26*float64(this.prefilter_period)) || pitch_index < int(0.79*float64(this.prefilter_period))) {
		pitch_change = 1
	}

	if pf_on == 0 {
		if start == 0 && tell+16 <= total_bits {
			enc.Enc_bit_logp(0, 1)
		}
	} else {
		/*This block is not gated by a total bits check only because
		of the nbAvailableBytes check above.*/
		var octave int
		enc.Enc_bit_logp(1, 1)
		pitch_index += 1
		octave = inlines.EC_ILOG(int64(pitch_index)) - 5
		enc.Enc_uint(int64(octave), 6)
		enc.Enc_bits(int64(pitch_index-(16<<octave)), 4+octave)
		pitch_index -= 1
		enc.Enc_bits(int64(qg), 3)
		enc.Enc_icdf(prefilter_tapset, tapset_icdf, 2)
	}

	isTransient = 0
	shortBlocks = 0
	if this.complexity >= 1 && this.lfe == 0 {
		boxed_tf_estimate := comm.BoxedValueInt{0}
		boxed_tf_chan := comm.BoxedValueInt{0}
		isTransient = transient_analysis(input, N+overlap, CC,
			&boxed_tf_estimate, &boxed_tf_chan)
		tf_estimate = boxed_tf_estimate.Val
		tf_chan = boxed_tf_chan.Val
	}

	if LM > 0 && enc.Tell()+3 <= total_bits {
		if isTransient != 0 {
			shortBlocks = M
		}
	} else {
		isTransient = 0
		transient_got_disabled = 1
	}

	freq = arrayUtil.InitTwoDimensionalArrayInt(CC, N)
	/**
	 * < Interleaved signal MDCTs
	 */
	bandE = arrayUtil.InitTwoDimensionalArrayInt(CC, nbEBands)
	bandLogE = arrayUtil.InitTwoDimensionalArrayInt(CC, nbEBands)

	secondMdct = comm.BoolToInt(shortBlocks != 0 && this.complexity >= 8)
	bandLogE2 = arrayUtil.InitTwoDimensionalArrayInt(CC, nbEBands)

	//Arrays.MemSet(bandLogE2, 0, C * nbEBands); // not explicitly needed
	if secondMdct != 0 {
		compute_mdcts(mode, 0, input, freq, C, CC, LM, this.upsample)
		Compute_band_energies(mode, freq, bandE, effEnd, C, LM)
		amp2Log2(mode, effEnd, end, bandE, bandLogE2, C)
		for i = 0; i < nbEBands; i++ {
			bandLogE2[0][i] += inlines.HALF16Int(inlines.SHL16Int(LM, CeltConstants.DB_SHIFT))
		}
		if C == 2 {
			for i = 0; i < nbEBands; i++ {
				bandLogE2[1][i] += inlines.HALF16Int(inlines.SHL16Int(LM, CeltConstants.DB_SHIFT))
			}
		}
	}

	compute_mdcts(mode, shortBlocks, input, freq, C, CC, LM, this.upsample)
	if CC == 2 && C == 1 {
		tf_chan = 0
	}
	Compute_band_energies(mode, freq, bandE, effEnd, C, LM)

	if this.lfe != 0 {
		for i = 2; i < end; i++ {
			bandE[0][i] = inlines.IMIN(bandE[0][i], inlines.MULT16_32_Q15Int(int(math.Trunc(0.5+(1e-4)*float64((1)<<(15)))), bandE[0][0]))
			bandE[0][i] = inlines.MAX32(bandE[0][i], CeltConstants.EPSILON)
		}
	}
	amp2Log2(mode, effEnd, end, bandE, bandLogE, C)

	surround_dynalloc = make([]int, C*nbEBands)
	//Arrays.MemSet(surround_dynalloc, 0, end); // not strictly needed
	/* This computes how much masking takes place between surround channels */
	if start == 0 && this.energy_mask != nil && this.lfe == 0 {
		var mask_end int
		var midband int
		var count_dynalloc int
		var mask_avg = 0
		var diff = 0
		var count = 0
		mask_end = inlines.IMAX(2, this.lastCodedBands)
		for c = 0; c < C; c++ {
			for i = 0; i < mask_end; i++ {
				var mask int
				//mask = inlines.MAX16Int(inlines.MIN16Int(this.energy_mask[nbEBands*c+i], int(0.5+(.25)*float64(int(1)<<(CeltConstants.DB_SHIFT)
				mask = inlines.MAX16Int(inlines.MIN16Int(this.energy_mask[nbEBands*c+i], int(0.5+(.25)*float64(int(1)<<(CeltConstants.DB_SHIFT)))), -int(0.5+(2.0)*float64(int(1)<<(CeltConstants.DB_SHIFT))))

				if mask > 0 {
					mask = inlines.HALF16Int(mask)
				}
				mask_avg += inlines.MULT16_16(mask, int(eBands[i+1]-eBands[i]))
				count += int(eBands[i+1] - eBands[i])
				diff += inlines.MULT16_16(mask, 1+2*i-mask_end)
			}
		}
		inlines.OpusAssert(count > 0)
		mask_avg = inlines.DIV32_16Int(mask_avg, count)
		mask_avg += int(0.5 + (.2)*float64(int(1)<<(CeltConstants.DB_SHIFT)))
		diff = diff * 6 / (C * (mask_end - 1) * (mask_end + 1) * mask_end)
		/* Again, being conservative */
		diff = inlines.HALF32(diff)
		diff = inlines.MAX32(inlines.MIN32(diff, int(0.5+(.031)*float64(int(1)<<(CeltConstants.DB_SHIFT)))), 0-int(0.5+(.031)*float64(int(1)<<(CeltConstants.DB_SHIFT))))
		/* Find the band that's in the middle of the coded spectrum */
		//for (midband = 0; eBands[midband + 1] < eBands[mask_end] / 2; midband++) ;
		midband = 0
		for eBands[midband+1] < eBands[mask_end]/2 {
			midband++
		}
		count_dynalloc = 0
		for i = 0; i < mask_end; i++ {
			var lin int
			var unmask int
			lin = mask_avg + diff*(i-midband)
			if C == 2 {
				unmask = inlines.MAX16Int(this.energy_mask[i], this.energy_mask[nbEBands+i])
			} else {
				unmask = this.energy_mask[i]
			}
			unmask = inlines.MIN16Int(unmask, int(0.5+(.0)*float64(int(1)<<(CeltConstants.DB_SHIFT))))
			unmask -= lin
			if unmask > int(0.5+(.25)*float64(int(1)<<(CeltConstants.DB_SHIFT))) {
				surround_dynalloc[i] = unmask - int(0.5+.25*float64(int(1)<<(CeltConstants.DB_SHIFT)))
				count_dynalloc++
			}
		}
		if count_dynalloc >= 3 {
			/* If we need dynalloc in many bands, it's probably because our
			   initial masking rate was too low. */
			mask_avg += int(0.5 + (.25)*float64(int(1)<<(CeltConstants.DB_SHIFT)))
			if mask_avg > 0 {
				/* Something went really wrong in the original calculations,
				   disabling masking. */
				mask_avg = 0
				diff = 0
				arrayUtil.MemSetLen(surround_dynalloc, 0, mask_end)
			} else {
				for i = 0; i < mask_end; i++ {
					surround_dynalloc[i] = inlines.MAX16Int(0, surround_dynalloc[i]-int(0.5+(.25)*float64(int(1)<<(CeltConstants.DB_SHIFT))))
				}
			}
		}
		mask_avg += int(0.5 + 0.2*float64(int(1)<<(CeltConstants.DB_SHIFT)))
		/* Convert to 1/64th units used for the trim */
		surround_trim = 64 * diff
		/*printf("%d %d ", mask_avg, surround_trim);*/
		surround_masking = mask_avg
	}
	/* Temporal VBR (but not for LFE) */
	if this.lfe == 0 {
		follow := -int(0.5 + (10.0)*float64(int(1)<<(CeltConstants.DB_SHIFT)))
		var frame_avg int = 0
		var offset int = 0
		if shortBlocks != 0 {
			offset = inlines.HALF16Int(inlines.SHL16Int(LM, CeltConstants.DB_SHIFT))
		}
		for i = start; i < end; i++ {
			follow = inlines.MAX16Int(follow-int(0.5+(1.0)*float64(int(1)<<(CeltConstants.DB_SHIFT))), bandLogE[0][i]-offset)
			if C == 2 {
				follow = inlines.MAX16Int(follow, bandLogE[1][i]-offset)
			}
			frame_avg += follow
		}
		frame_avg /= (end - start)
		temporal_vbr = inlines.SUB16Int(frame_avg, this.spec_avg)
		temporal_vbr = inlines.MIN16Int(int(0.5+(3.0)*float64(int(1)<<(CeltConstants.DB_SHIFT))), inlines.MAX16Int(-int(0.5+(1.5)*float64(int(1)<<(CeltConstants.DB_SHIFT))), temporal_vbr))
		this.spec_avg += (inlines.MULT16_16_Q15Int(int(math.Trunc(0.5+(.02)*float64((1)<<(15)))), temporal_vbr))
	}

	if secondMdct == 0 {
		copy(bandLogE2[0], bandLogE[0])

		if C == 2 {
			//System.arraycopy(bandLogE[1], 0, bandLogE2[1], 0, nbEBands)
			copy(bandLogE2[1], bandLogE[1])
		}
	}

	/* Last chance to catch any transient we might have missed in the
	   time-domain analysis */
	if LM > 0 && enc.Tell()+3 <= total_bits && isTransient == 0 && this.complexity >= 5 && this.lfe == 0 {
		if patch_transient_decision(bandLogE, this.oldBandE, nbEBands, start, end, C) != 0 {
			isTransient = 1
			shortBlocks = M
			compute_mdcts(mode, shortBlocks, input, freq, C, CC, LM, this.upsample)
			Compute_band_energies(mode, freq, bandE, effEnd, C, LM)
			amp2Log2(mode, effEnd, end, bandE, bandLogE, C)
			/* Compensate for the scaling of short vs long mdcts */
			for i = 0; i < nbEBands; i++ {
				bandLogE2[0][i] += inlines.HALF16Int(inlines.SHL16Int(LM, CeltConstants.DB_SHIFT))
			}
			if C == 2 {
				for i = 0; i < nbEBands; i++ {
					bandLogE2[1][i] += inlines.HALF16Int(inlines.SHL16Int(LM, CeltConstants.DB_SHIFT))
				}
			}
			tf_estimate = int(math.Trunc(0.5 + (.2)*float64((1)<<(14))))
		}
	}

	if LM > 0 && enc.Tell()+3 <= total_bits {
		enc.Enc_bit_logp(isTransient, 3)
	}

	X = arrayUtil.InitTwoDimensionalArrayInt(C, N)
	/**
	 * < Interleaved normalised MDCTs
	 */

	/* Band normalisation */
	normalise_bands(mode, freq, X, bandE, effEnd, C, M)

	tf_res = make([]int, nbEBands)
	/* Disable variable tf resolution for hybrid and at very low bitrate */
	if effectiveBytes >= 15*C && start == 0 && this.complexity >= 2 && this.lfe == 0 {
		var lambda int
		if effectiveBytes < 40 {
			lambda = 12
		} else if effectiveBytes < 60 {
			lambda = 6
		} else if effectiveBytes < 100 {
			lambda = 4
		} else {
			lambda = 3
		}
		lambda *= 2
		boxed_tf_sum := comm.BoxedValueInt{0}
		tf_select = tf_analysis(mode, effEnd, isTransient, tf_res, lambda, X, N, LM, &boxed_tf_sum, tf_estimate, tf_chan)
		//tf_sum = boxed_tf_sum.Val

		for i = effEnd; i < end; i++ {
			tf_res[i] = tf_res[effEnd-1]
		}
	} else {
		//tf_sum = 0
		for i = 0; i < end; i++ {
			tf_res[i] = isTransient
		}
		tf_select = 0
	}

	error = arrayUtil.InitTwoDimensionalArrayInt(C, nbEBands)
	boxed_delayedintra := comm.BoxedValueInt{this.delayedIntra}

	quant_coarse_energy(mode, start, end, effEnd, bandLogE,
		this.oldBandE, total_bits, error, enc,
		C, LM, nbAvailableBytes, this.force_intra,
		&boxed_delayedintra, comm.BoolToInt(this.complexity >= 4), this.loss_rate, this.lfe)
	this.delayedIntra = boxed_delayedintra.Val

	tf_encode(start, end, isTransient, tf_res, LM, tf_select, enc)

	if enc.Tell()+4 <= total_bits {
		if this.lfe != 0 {
			this.tapset_decision = 0
			this.spread_decision = Spread.SPREAD_NORMAL
		} else if shortBlocks != 0 || this.complexity < 3 || nbAvailableBytes < 10*C || start != 0 {
			if this.complexity == 0 {
				this.spread_decision = Spread.SPREAD_NONE
			} else {
				this.spread_decision = Spread.SPREAD_NORMAL
			}
		} else {
			boxed_tonal_average := comm.BoxedValueInt{this.tonal_average}
			boxed_tapset_decision := comm.BoxedValueInt{this.tapset_decision}
			boxed_hf_average := comm.BoxedValueInt{this.hf_average}
			this.spread_decision = spreading_decision(mode, X,
				&boxed_tonal_average, this.spread_decision, &boxed_hf_average,
				&boxed_tapset_decision, comm.BoolToInt(pf_on != 0 && shortBlocks == 0), effEnd, C, M)
			this.tonal_average = boxed_tonal_average.Val
			this.tapset_decision = boxed_tapset_decision.Val
			this.hf_average = boxed_hf_average.Val

		}
		enc.Enc_icdf(this.spread_decision, spread_icdf, 5)
	}

	offsets = make([]int, nbEBands)

	boxed_tot_boost := comm.BoxedValueInt{0}
	maxDepth = dynalloc_analysis(bandLogE, bandLogE2, nbEBands, start, end, C, offsets,
		this.lsb_depth, mode.logN, isTransient, this.vbr, this.constrained_vbr,
		eBands, LM, effectiveBytes, &boxed_tot_boost, this.lfe, surround_dynalloc)
	tot_boost = boxed_tot_boost.Val

	/* For LFE, everything interesting is in the first band */
	if this.lfe != 0 {
		offsets[0] = inlines.IMIN(8, effectiveBytes/3)
	}
	cap = make([]int, nbEBands)
	init_caps(mode, cap, LM, C)

	dynalloc_logp = 6
	total_bits <<= BITRES
	total_boost = 0
	tell = enc.Tell_frac()
	for i = start; i < end; i++ {
		var width, quanta int
		var dynalloc_loop_logp int
		var boost int
		var j int
		width = C * int(eBands[i+1]-eBands[i]) << LM
		/* quanta is 6 bits, but no more than 1 bit/sample
		   and no less than 1/8 bit/sample */
		quanta = inlines.IMIN(width<<BITRES, inlines.IMAX(6<<BITRES, width))
		dynalloc_loop_logp = dynalloc_logp
		boost = 0

		for j = 0; tell+(dynalloc_loop_logp<<BITRES) < total_bits-total_boost && boost < cap[i]; j++ {
			var flag int
			flag = comm.BoolToInt(j < offsets[i])

			enc.Enc_bit_logp(flag, dynalloc_loop_logp)
			tell = enc.Tell_frac()
			if flag == 0 {
				break
			}
			boost += quanta
			total_boost += quanta
			dynalloc_loop_logp = 1
		}
		/* Making dynalloc more likely */
		if j != 0 {
			dynalloc_logp = inlines.IMAX(2, dynalloc_logp-1)
		}
		offsets[i] = boost
	}

	if C == 2 {
		/* Always use MS for 2.5 ms frames until we can do a better analysis */
		if LM != 0 {
			dual_stereo = stereo_analysis(mode, X, LM)
		}

		this.intensity = hysteresis_decision((equiv_rate / 1000),
			intensity_thresholds, intensity_histeresis, 21, this.intensity)
		this.intensity = inlines.IMIN(end, inlines.IMAX(start, this.intensity))
	}

	alloc_trim = 5
	if tell+(6<<BITRES) <= total_bits-total_boost {
		if this.lfe != 0 {
			alloc_trim = 5
		} else {
			boxed_stereo_saving := comm.BoxedValueInt{this.stereo_saving}
			alloc_trim = alloc_trim_analysis(mode, X, bandLogE,
				end, LM, C, &this.analysis, &boxed_stereo_saving, tf_estimate,
				this.intensity, surround_trim)
			this.stereo_saving = boxed_stereo_saving.Val
		}
		enc.Enc_icdf(alloc_trim, trim_icdf, 7)
		tell = enc.Tell_frac()
	}

	/* Variable bitrate */
	if vbr_rate > 0 {
		var alpha int
		var delta int
		/* The target rate in 8th bits per frame */
		var target, base_target int
		var min_allowed int
		var lm_diff = mode.MaxLM - LM

		/* Don't attempt to use more than 510 kb/s, even for frames smaller than 20 ms.
		   The CELT allocator will just not be able to use more than that anyway. */
		nbCompressedBytes = inlines.IMIN(nbCompressedBytes, 1275>>(3-LM))
		base_target = vbr_rate - ((40*C + 20) << BITRES)

		if this.constrained_vbr != 0 {
			base_target += (this.vbr_offset >> lm_diff)
		}

		target = compute_vbr(mode, &this.analysis, base_target, LM, equiv_rate,
			this.lastCodedBands, C, this.intensity, this.constrained_vbr,
			this.stereo_saving, tot_boost, tf_estimate, pitch_change, maxDepth,
			this.variable_duration, this.lfe, comm.BoolToInt(this.energy_mask != nil), surround_masking,
			temporal_vbr)

		/* The current offset is removed from the target and the space used
		   so far is added*/
		target = target + tell
		/* In VBR mode the frame size must not be reduced so much that it would
		    result in the encoder running out of bits.
		   The margin of 2 bytes ensures that none of the bust-prevention logic
		    in the decoder will have triggered so far. */
		min_allowed = ((tell + total_boost + (1 << (BITRES + 3)) - 1) >> (BITRES + 3)) + 2 - nbFilledBytes

		nbAvailableBytes = (target + (1 << (BITRES + 2))) >> (BITRES + 3)
		nbAvailableBytes = inlines.IMAX(min_allowed, nbAvailableBytes)
		nbAvailableBytes = inlines.IMIN(nbCompressedBytes, nbAvailableBytes+nbFilledBytes) - nbFilledBytes

		/* By how much did we "miss" the target on that frame */
		delta = target - vbr_rate

		target = nbAvailableBytes << (BITRES + 3)

		/*If the frame is silent we don't adjust our drift, otherwise
		  the encoder will shoot to very high rates after hitting a
		  span of silence, but we do allow the EntropyCoder.BITRES to refill.
		  This means that we'll undershoot our target in CVBR/VBR modes
		  on files with lots of silence. */
		if silence != 0 {
			nbAvailableBytes = 2
			target = 2 * 8 << BITRES
			delta = 0
		}

		if this.vbr_count < 970 {
			this.vbr_count++
			alpha = inlines.Celt_rcp(inlines.SHL32((this.vbr_count + 20), 16))
		} else {
			alpha = int(math.Trunc(0.5 + 0.001*float64(1<<(15))))
		}
		/* How many bits have we used in excess of what we're allowed */
		if this.constrained_vbr != 0 {
			this.vbr_reservoir += target - vbr_rate
		}
		/*printf ("%d\n", st.vbr_reservoir);*/

		/* Compute the offset we need to apply in order to reach the target */
		if this.constrained_vbr != 0 {
			this.vbr_drift += inlines.MULT16_32_Q15Int(alpha, (delta*(1<<lm_diff))-this.vbr_offset-this.vbr_drift)
			this.vbr_offset = -this.vbr_drift
		}
		/*printf ("%d\n", st.vbr_drift);*/

		if this.constrained_vbr != 0 && this.vbr_reservoir < 0 {
			/* We're under the min value -- increase rate */
			adjust := (-this.vbr_reservoir) / (8 << BITRES)
			/* Unless we're just coding silence */
			if silence == 0 {
				nbAvailableBytes += adjust
			}
			// nbAvailableBytes += silence != 0 ? 0 : adjust;
			this.vbr_reservoir = 0
			/*printf ("+%d\n", adjust);*/
		}
		nbCompressedBytes = inlines.IMIN(nbCompressedBytes, nbAvailableBytes+nbFilledBytes)
		/*printf("%d\n", nbCompressedBytes*50*8);*/
		/* This moves the raw bits to take into account the new compressed size */
		enc.Enc_shrink(nbCompressedBytes)
	}

	/* Bit allocation */
	fine_quant = make([]int, nbEBands)
	pulses = make([]int, nbEBands)
	fine_priority = make([]int, nbEBands)

	/* bits =    packet size                                     - where we are                        - safety*/
	bits = ((nbCompressedBytes * 8) << BITRES) - enc.Tell_frac() - 1
	anti_collapse_rsv = 0
	if isTransient != 0 && LM >= 2 && bits >= ((LM+2)<<BITRES) {
		anti_collapse_rsv = (1 << BITRES)
	}
	//   anti_collapse_rsv = isTransient != 0 && LM >= 2 && bits >= ((LM + 2) << EntropyCoder.BITRES) ? (1 << EntropyCoder.BITRES) : 0;
	bits -= anti_collapse_rsv
	signalBandwidth = end - 1

	if this.analysis.Enabled && this.analysis.Valid != 0 {
		var min_bandwidth int
		if equiv_rate < 32000*C {
			min_bandwidth = 13
		} else if equiv_rate < 48000*C {
			min_bandwidth = 16
		} else if equiv_rate < 60000*C {
			min_bandwidth = 18
		} else if equiv_rate < 80000*C {
			min_bandwidth = 19
		} else {
			min_bandwidth = 20
		}
		signalBandwidth = inlines.IMAX(this.analysis.Bandwidth, min_bandwidth)
	}

	if this.lfe != 0 {
		signalBandwidth = 1
	}

	boxed_intensity := comm.BoxedValueInt{this.intensity}
	boxed_balance := comm.BoxedValueInt{0}
	boxed_dual_stereo := comm.BoxedValueInt{dual_stereo}
	codedBands = compute_allocation(mode, start, end, offsets, cap,
		alloc_trim, &boxed_intensity, &boxed_dual_stereo, bits, &boxed_balance, pulses,
		fine_quant, fine_priority, C, LM, enc, 1, this.lastCodedBands, signalBandwidth)
	this.intensity = boxed_intensity.Val
	balance = boxed_balance.Val
	dual_stereo = boxed_dual_stereo.Val

	if this.lastCodedBands != 0 {
		this.lastCodedBands = inlines.IMIN(this.lastCodedBands+1, inlines.IMAX(this.lastCodedBands-1, codedBands))
	} else {
		this.lastCodedBands = codedBands
	}

	quant_fine_energy(mode, start, end, this.oldBandE, error, fine_quant, enc, C)

	/* Residual quantisation */
	collapse_masks = make([]int16, C*nbEBands)
	boxed_rng := comm.BoxedValueInt{this.rng}
	var temp1 []int
	if C == 2 {
		temp1 = X[1]
	}
	quant_all_bands(1, mode, start, end, X[0], temp1, collapse_masks,
		bandE, pulses, shortBlocks, this.spread_decision,
		dual_stereo, this.intensity, tf_res, nbCompressedBytes*(8<<BITRES)-anti_collapse_rsv,
		balance, enc, LM, codedBands, &boxed_rng)
	this.rng = boxed_rng.Val

	if anti_collapse_rsv > 0 {
		anti_collapse_on = comm.BoolToInt(this.consec_transient < 2)
		enc.Enc_bits(int64(anti_collapse_on), 1)
	}

	quant_energy_finalise(mode, start, end, this.oldBandE, error, fine_quant, fine_priority, nbCompressedBytes*8-enc.Tell(), enc, C)

	if silence != 0 {
		for i = 0; i < nbEBands; i++ {
			this.oldBandE[0][i] = -int(0.5 + (28.0)*float64(int(1)<<(CeltConstants.DB_SHIFT)))
		}
		if C == 2 {
			for i = 0; i < nbEBands; i++ {
				this.oldBandE[1][i] = -int(0.5 + (28.0)*float64(int(1)<<(CeltConstants.DB_SHIFT)))
			}
		}
	}

	this.prefilter_period = pitch_index
	this.prefilter_gain = gain1
	this.prefilter_tapset = prefilter_tapset

	if CC == 2 && C == 1 {
		//	System.arraycopy(oldBandE[0], 0, oldBandE[1], 0, nbEBands)
		copy(this.oldBandE[1], this.oldBandE[0])
	}

	if isTransient == 0 {
		//System.arraycopy(this.oldLogE[0], 0, this.oldLogE2[0], 0, nbEBands)
		copy(this.oldLogE2[0], this.oldLogE[0])
		//System.arraycopy(this.oldBandE[0], 0, this.oldLogE[0], 0, nbEBands)
		copy(this.oldLogE[0], this.oldBandE[0])
		if CC == 2 {
			//System.arraycopy(this.oldLogE[1], 0, this.oldLogE2[1], 0, nbEBands)
			copy(this.oldLogE2[1], this.oldLogE[1])
			//System.arraycopy(this.oldBandE[1], 0, this.oldLogE[1], 0, nbEBands)
			copy(this.oldLogE[1], this.oldBandE[1])
		}
	} else {
		for i = 0; i < nbEBands; i++ {
			this.oldLogE[0][i] = inlines.MIN16Int(this.oldLogE[0][i], this.oldBandE[0][i])
		}
		if CC == 2 {
			for i = 0; i < nbEBands; i++ {
				this.oldLogE[1][i] = inlines.MIN16Int(this.oldLogE[1][i], this.oldBandE[1][i])
			}
		}
	}

	/* In case start or end were to change */
	c = 0
	for {
		for i = 0; i < start; i++ {
			this.oldBandE[c][i] = 0
			this.oldLogE[c][i] = -int(0.5 + (28.0)*float64(int(1)<<(CeltConstants.DB_SHIFT)))
			this.oldLogE2[c][i] = this.oldLogE[c][i]
		}
		for i = end; i < nbEBands; i++ {
			this.oldBandE[c][i] = 0
			this.oldLogE[c][i] = -int(0.5 + (28.0)*float64(int(1)<<(CeltConstants.DB_SHIFT)))
			this.oldLogE2[c][i] = this.oldLogE[c][i]
		}
		c++
		if c < CC {
			continue
		}
		break
	}

	if isTransient != 0 || transient_got_disabled != 0 {
		this.consec_transient++
	} else {
		this.consec_transient = 0
	}
	this.rng = int(enc.Rng)

	/* If there's any room left (can only happen for very high rates),
	   it's already filled with zeros */
	enc.Enc_done()

	if enc.Get_error() != 0 {
		return OpusError.OPUS_INTERNAL_ERROR
	} else {
		return nbCompressedBytes
	}
}

func boolToSlice(cond bool, slice []int) []int {
	if cond {
		return slice
	}
	return nil
}

func (this *CeltEncoder) SetComplexity(value int) {
	if value < 0 || value > 10 {
		panic("Complexity must be between 0 and 10 inclusive")
	}
	this.complexity = value
}

func (this *CeltEncoder) SetStartBand(value int) {
	if value < 0 || value >= this.mode.nbEBands {
		panic("Start band above max number of ebands (or negative)")
	}
	this.start = value
}

func (this *CeltEncoder) SetEndBand(value int) {
	if value < 1 || value > this.mode.nbEBands {
		panic("End band above max number of ebands (or less than 1)")
	}
	this.end = value
}

func (this *CeltEncoder) SetPacketLossPercent(value int) {
	if value < 0 || value > 100 {
		panic("Packet loss must be between 0 and 100")
	}
	this.loss_rate = value
}

func (this *CeltEncoder) SetPrediction(value int) {
	if value < 0 || value > 2 {
		panic("CELT prediction mode must be 0, 1, or 2")
	}
	if value <= 1 {
		this.disable_pf = 1
	} else {
		this.disable_pf = 0
	}
	if value == 0 {
		this.force_intra = 1
	} else {
		this.force_intra = 0
	}
}

func (this *CeltEncoder) SetVBRConstraint(value bool) {
	if value {
		this.constrained_vbr = 1
	} else {
		this.constrained_vbr = 0
	}
}

func (this *CeltEncoder) SetVBR(value bool) {
	if value {
		this.vbr = 1
	} else {
		this.vbr = 0
	}
}

func (this *CeltEncoder) SetBitrate(value int) {
	if value <= 500 && value != opusConstants.OPUS_BITRATE_MAX {
		panic("Bitrate out of range")
	}
	if value > 260000*this.channels {
		value = 260000 * this.channels
	}
	this.bitrate = value
}

func (this *CeltEncoder) SetChannels(value int) {
	if value < 1 || value > 2 {
		panic("Channel count must be 1 or 2")
	}
	this.stream_channels = value
}

func (this *CeltEncoder) SetLSBDepth(value int) {
	if value < 8 || value > 24 {
		panic("Bit depth must be between 8 and 24")
	}
	this.lsb_depth = value
}

func (this *CeltEncoder) GetLSBDepth() int {
	return this.lsb_depth
}

func (this *CeltEncoder) SetExpertFrameDuration(value int) {
	this.variable_duration = value
}

func (this *CeltEncoder) SetSignalling(value int) {
	this.signalling = value
}

func (this *CeltEncoder) SetAnalysis(value *AnalysisInfo) {
	if value == nil {
		panic("AnalysisInfo")
	}
	this.analysis = *value
}

func (this *CeltEncoder) GetMode() *CeltMode {
	return this.mode
}

func (this *CeltEncoder) GetFinalRange() int {
	return this.rng
}

func (this *CeltEncoder) SetLFE(value int) {
	this.lfe = value
}

func (this *CeltEncoder) SetEnergyMask(value []int) {
	this.energy_mask = value
}

// Additional helper functions (like inlines.MULT16_16_Q15, ABS32, etc.) would be defined elsewhere
