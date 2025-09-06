package celt

import (
	"math"

	"github.com/dosgo/concentus/go/comm"
	"github.com/dosgo/concentus/go/comm/arrayUtil"
)

var inv_table = []int16{
	255, 255, 156, 110, 86, 70, 59, 51, 45, 40, 37, 33, 31, 28, 26, 25,
	23, 22, 21, 20, 19, 18, 17, 16, 16, 15, 15, 14, 13, 13, 12, 12,
	12, 12, 11, 11, 11, 10, 10, 10, 9, 9, 9, 9, 9, 9, 8, 8,
	8, 8, 8, 7, 7, 7, 7, 7, 7, 6, 6, 6, 6, 6, 6, 6,
	6, 6, 6, 6, 6, 6, 6, 6, 6, 5, 5, 5, 5, 5, 5, 5,
	5, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
	4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 3, 3,
	3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2,
}

func compute_vbr(mode *CeltMode, analysis *AnalysisInfo, base_target int, LM int, bitrate int, lastCodedBands int, C int, intensity int, constrained_vbr int, stereo_saving int, tot_boost int, tf_estimate int, pitch_change int, maxDepth int, variable_duration int, lfe int, has_surround_mask int, surround_masking int, temporal_vbr int) int {
	var target int
	var coded_bins int
	var coded_bands int
	var tf_calibration int
	var nbEBands int
	var eBands []int16

	nbEBands = mode.nbEBands
	eBands = mode.eBands

	if lastCodedBands != 0 {
		coded_bands = lastCodedBands
	} else {
		coded_bands = nbEBands
	}
	coded_bins = int(eBands[coded_bands]) << LM
	if C == 2 {
		intensity_bands := inlines.IMIN(intensity, coded_bands)
		coded_bins += int(eBands[intensity_bands]) << LM
	}

	target = base_target
	if analysis.Enabled && analysis.Valid != 0 && analysis.Activity < 0.4 {
		target -= int(float32(coded_bins<<BITRES) * (0.4 - analysis.Activity))
	}

	if C == 2 {
		var coded_stereo_bands int
		var coded_stereo_dof int
		var max_frac int
		coded_stereo_bands = inlines.IMIN(intensity, coded_bands)
		coded_stereo_dof = int(eBands[coded_stereo_bands])<<LM - coded_stereo_bands
		max_frac = inlines.DIV32_16Int(inlines.MULT16_16(int(math.Trunc(0.5+(0.8)*((1)<<(15)))), coded_stereo_dof), coded_bins)

		//stereo_saving_val := MIN16(int16(stereo_saving), int16(0.5+1.0*float32(1<<8)))
		stereo_saving = inlines.MIN16Int(stereo_saving, int(math.Trunc(0.5+(1.0)*((1)<<(8)))))
		target -= int(inlines.MIN32(inlines.MULT16_32_Q15Int(max_frac, int(target)), inlines.SHR32(inlines.MULT16_16(int(stereo_saving)-int(math.Trunc(0.5+0.1*float64(1<<8))), int(coded_stereo_dof<<BITRES)), 8)))
	}

	target += tot_boost - (16 << LM)
	if variable_duration == OPUS_FRAMESIZE_VARIABLE {
		tf_calibration = int(math.Trunc(0.5 + 0.02*float64(1<<14)))
	} else {
		tf_calibration = int(math.Trunc(0.5 + 0.04*float64(1<<14)))
	}
	target += int(inlines.SHL32(inlines.MULT16_32_Q15(int16(tf_estimate)-int16(tf_calibration), int(target)), 1))

	if analysis.Enabled && analysis.Valid != 0 && lfe == 0 {
		var tonal_target int
		var tonal float32
		tonal = inlines.MAX16Float(0, analysis.Tonality-0.15) - 0.09
		tonal_target = target + int(float32(coded_bins<<BITRES)*1.2*tonal)
		if pitch_change != 0 {
			tonal_target += int(float32(coded_bins<<BITRES) * 0.8)
		}
		target = tonal_target
	}

	if has_surround_mask != 0 && lfe == 0 {
		surround_target := target + int(inlines.SHR32(inlines.MULT16_16(int(surround_masking), int(coded_bins<<BITRES)), CeltConstants.DB_SHIFT))
		if target/4 > surround_target {
			target = target / 4
		} else {
			target = surround_target
		}
	}

	{
		var floor_depth int
		bins := int(eBands[nbEBands-2]) << LM
		floor_depth = int(inlines.SHR32(inlines.MULT16_16(int(C*bins<<BITRES), int(maxDepth)), CeltConstants.DB_SHIFT))
		if target>>2 > floor_depth {
			floor_depth = target >> 2
		}
		if floor_depth < target {
			target = floor_depth
		}
	}

	if (has_surround_mask == 0 || lfe != 0) && (constrained_vbr != 0 || bitrate < 64000) {
		rate_factor := inlines.MAX16Int(0, int(bitrate-32000))
		if constrained_vbr != 0 {
			rate_factor = inlines.MIN16Int(rate_factor, int(math.Trunc(0.5+0.67*float64(1<<15))))
		}
		target = base_target + int(inlines.MULT16_32_Q15Int(rate_factor, int(target-base_target)))
	}

	if has_surround_mask == 0 && tf_estimate < int(math.Trunc(0.5+0.2*float64(1<<14))) {
		amount := inlines.MULT16_16_Q15Int(int(math.Trunc(0.5+0.0000031*float64(1<<30))), int(inlines.IMAX(0, inlines.IMIN(32000, 96000-bitrate))))
		tvbr_factor := int(inlines.SHR32(inlines.MULT16_16(int(temporal_vbr), amount), CeltConstants.DB_SHIFT))
		target += int(inlines.MULT16_32_Q15Int(int(tvbr_factor), int(target)))
	}

	if 2*base_target < target {
		target = 2 * base_target
	}

	return target
}

func transient_analysis(input [][]int, len int, C int, tf_estimate *comm.BoxedValueInt, tf_chan *comm.BoxedValueInt) int {
	tmp := make([]int, len)
	is_transient := 0
	mask_metric := 0
	tf_chan.Val = 0
	len2 := len / 2

	for c := 0; c < C; c++ {
		unmask := 0
		mem0 := 0
		mem1 := 0
		for i := 0; i < len; i++ {
			x := inlines.SHR32(input[c][i], CeltConstants.SIG_SHIFT)
			y := inlines.ADD32(mem0, x)
			mem0 = mem1 + y - inlines.SHL32(x, 1)
			mem1 = x - inlines.SHR32(y, 1)
			tmp[i] = int(inlines.EXTRACT16(inlines.SHR32(y, 2)))
		}
		for i := 0; i < 12; i++ {
			tmp[i] = 0
		}

		shift := 14 - inlines.Celt_ilog2(1+inlines.Celt_maxabs32(tmp, 0, len))
		if shift != 0 {
			for i := 0; i < len; i++ {
				tmp[i] = inlines.SHL16Int(tmp[i], shift)
			}
		}

		mean := 0
		mem0 = 0
		for i := 0; i < len2; i++ {
			x2 := inlines.PSHR32(inlines.ADD32(inlines.MULT16_16(tmp[2*i], tmp[2*i]), inlines.MULT16_16(tmp[2*i+1], tmp[2*i+1])), 16)
			mean += x2
			tmp[i] = mem0 + inlines.PSHR32(x2-mem0, 4)
			mem0 = tmp[i]
		}

		mem0 = 0
		maxE := 0
		for i := len2 - 1; i >= 0; i-- {
			tmp[i] = mem0 + inlines.PSHR32(tmp[i]-mem0, 3)
			mem0 = tmp[i]
			if mem0 > maxE {
				maxE = mem0
			}
		}

		mean = inlines.MULT16_16(inlines.Celt_sqrt(mean), inlines.Celt_sqrt(inlines.MULT16_16(maxE, len2>>1)))
		norm := (len2 << (6 + 14)) / (CeltConstants.EPSILON + inlines.SHR32(mean, 1))

		for i := 12; i < len2-5; i += 4 {
			id := inlines.MAX32(0, inlines.MIN32(127, inlines.MULT16_32_Q15(int16(tmp[i]+CeltConstants.EPSILON), int(norm))))
			unmask += int(inv_table[id])
		}
		unmask = 64 * unmask * 4 / (6 * (len2 - 17))
		if unmask > mask_metric {
			tf_chan.Val = c
			mask_metric = unmask
		}
	}

	if mask_metric > 200 {
		is_transient = 1
	}

	tf_max := inlines.MAX16Int(0, (inlines.Celt_sqrt(27*mask_metric) - 42))

	tf_estimate.Val = (inlines.Celt_sqrt(inlines.MAX32(0, inlines.SHL32(inlines.MULT16_16(int(math.Trunc(0.5+(0.0069)*((1)<<(14)))), inlines.MAX16Int(163, tf_max)), 14)-int(math.Trunc(0.5+(0.139)*((1)<<(28)))))))
	return is_transient
}

func patch_transient_decision(newE [][]int, oldE [][]int, nbEBands int, start int, end int, C int) int {
	mean_diff := 0
	spread_old := make([]int, 26)

	if C == 1 {
		spread_old[start] = oldE[0][start]
		for i := start + 1; i < end; i++ {
			spread_old[i] = inlines.MAX16Int(spread_old[i-1]-int(1.0*float32(int(1)<<CeltConstants.DB_SHIFT)), oldE[0][i])
		}
	} else {
		spread_old[start] = inlines.MAX16Int(oldE[0][start], oldE[1][start])
		for i := start + 1; i < end; i++ {
			spread_old[i] = inlines.MAX16Int(spread_old[i-1]-int(1.0*float32(int(1)<<CeltConstants.DB_SHIFT)), inlines.MAX16Int(oldE[0][i], oldE[1][i]))
		}
	}

	for i := end - 2; i >= start; i-- {
		spread_old[i] = inlines.MAX16Int(spread_old[i], spread_old[i+1]-int(1.0*float32(int(1)<<CeltConstants.DB_SHIFT)))
	}

	for c := 0; c < C; c++ {
		for i := inlines.IMAX(2, start); i < end-1; i++ {
			x1 := inlines.MAX16Int(0, newE[c][i])
			x2 := inlines.MAX16Int(0, spread_old[i])
			diff := inlines.MAX16Int(0, x1-x2)
			mean_diff += int(diff)
		}
	}

	mean_diff /= C * (end - 1 - inlines.IMAX(2, start))
	if mean_diff > int(1.0*float32(int(1)<<CeltConstants.DB_SHIFT)) {
		return 1
	}
	return 0
}

func compute_mdcts(mode *CeltMode, shortBlocks int, input [][]int, output [][]int, C int, CC int, LM int, upsample int) {
	overlap := mode.Overlap
	N := 0
	B := 0
	shift := 0
	if shortBlocks != 0 {
		B = shortBlocks
		N = mode.ShortMdctSize
		shift = mode.MaxLM
	} else {
		B = 1
		N = mode.ShortMdctSize << LM
		shift = mode.MaxLM - LM
	}

	for c := 0; c < CC; c++ {
		for b := 0; b < B; b++ {
			Clt_mdct_forward(mode.Mdct, input[c], b*N, output[c], b, mode.Window, overlap, shift, B)
		}
	}

	if CC == 2 && C == 1 {
		for i := 0; i < B*N; i++ {
			output[0][i] = inlines.ADD32(inlines.HALF32(output[0][i]), inlines.HALF32(output[1][i]))
		}
	}

	if upsample != 1 {
		for c := 0; c < C; c++ {
			bound := B * N / upsample
			for i := 0; i < bound; i++ {
				output[c][i] *= upsample
			}
			for i := bound; i < B*N; i++ {
				output[c][i] = 0
			}
		}
	}
}

func Celt_preemphasis1(
	pcmp []int16, // short[] pcmp
	inp []int, // int[] inp
	inp_ptr int, // int inp_ptr
	N int, // int N
	CC int, // int CC
	upsample int, // int upsample
	coef []int, // int[] coef
	mem *comm.BoxedValueInt, // BoxedValueInt mem
	clip int, // int clip
) {
	var i int
	var coef0 int
	var m int
	var Nu int

	coef0 = coef[0]
	m = mem.Val

	/* Fast path for the normal 48kHz case and no clipping */
	if coef[1] == 0 && upsample == 1 && clip == 0 {
		for i = 0; i < N; i++ {
			var x int
			// 完全保留原始索引计算
			x = int(pcmp[CC*i])
			/* Apply pre-emphasis */
			// 直接使用原始方法调用
			inp[inp_ptr+i] = inlines.SHL32(x, CeltConstants.SIG_SHIFT) - m
			m = inlines.SHR32(inlines.MULT16_16(coef0, x), 15-CeltConstants.SIG_SHIFT)
		}
		mem.Val = m
		return
	}

	Nu = N / upsample
	if upsample != 1 {
		// 保持原始方法调用
		arrayUtil.MemSetWithOffset(inp, 0, inp_ptr, N)
	}
	for i = 0; i < Nu; i++ {
		// 保持原始位置计算
		inp[inp_ptr+(i*upsample)] = int(pcmp[CC*i])
	}

	for i = 0; i < N; i++ {
		var x int
		// 保持原始位置读取
		x = inp[inp_ptr+i]
		/* Apply pre-emphasis */
		// 保持原始方法调用
		inp[inp_ptr+i] = inlines.SHL32(x, CeltConstants.SIG_SHIFT) - m
		m = inlines.SHR32(inlines.MULT16_16(coef0, x), 15-CeltConstants.SIG_SHIFT)
	}

	mem.Val = m
}

func celt_preemphasis(pcmp []int16, pcmp_ptr int, inp []int, inp_ptr int, N int, CC int, upsample int, coef []int, mem *comm.BoxedValueInt, clip int) {
	coef0 := coef[0]
	m := mem.Val

	if coef[1] == 0 && upsample == 1 && clip == 0 {
		for i := 0; i < N; i++ {
			x := int(pcmp[pcmp_ptr+CC*i])
			inp[inp_ptr+i] = inlines.SHL32(x, CeltConstants.SIG_SHIFT) - m
			m = inlines.SHR32(inlines.MULT16_16(int(coef0), int(x)), 15-CeltConstants.SIG_SHIFT)
		}
		mem.Val = m
		return
	}

	Nu := N / upsample
	if upsample != 1 {
		for i := inp_ptr; i < inp_ptr+N; i++ {
			inp[i] = 0
		}
	}
	for i := 0; i < Nu; i++ {
		inp[inp_ptr+i*upsample] = int(pcmp[pcmp_ptr+CC*i])
	}

	for i := 0; i < N; i++ {
		x := inp[inp_ptr+i]
		inp[inp_ptr+i] = inlines.SHL32(x, CeltConstants.SIG_SHIFT) - m
		m = inlines.SHR32(inlines.MULT16_16(int(coef0), int(x)), 15-CeltConstants.SIG_SHIFT)
	}
	mem.Val = m
}

func l1_metric(tmp []int, N int, LM int, bias int) int {
	L1 := 0
	for i := 0; i < N; i++ {
		L1 += inlines.EXTEND32Int(inlines.ABS32(tmp[i]))
	}
	L1 += inlines.MAC16_32_Q15Int(L1, int(LM*bias), int(L1))
	return L1
}

func tf_analysis(m *CeltMode, len int, isTransient int, tf_res []int, lambda int, X [][]int, N0 int, LM int, tf_sum *comm.BoxedValueInt, tf_estimate int, tf_chan int) int {
	metric := make([]int, len)
	cost0 := 0
	cost1 := 0
	path0 := make([]int, len)
	path1 := make([]int, len)
	selcost := [2]int{0, 0}
	tf_select := 0
	bias := 0

	bias = int(inlines.MULT16_16_Q14(int16(math.Trunc(0.5+0.04*(1<<15))), inlines.MAX16(int16(0)-int16(math.Trunc(0.5+0.25*(1<<14))), int16(math.Trunc(0.5+0.5*(1<<14)))-int16(tf_estimate))))

	tmp := make([]int, (m.eBands[len]-m.eBands[len-1])<<LM)
	tmp_1 := make([]int, (m.eBands[len]-m.eBands[len-1])<<LM)

	tf_sum.Val = 0
	for i := 0; i < len; i++ {
		N := int(m.eBands[i+1]-m.eBands[i]) << LM
		narrow := 0
		if (m.eBands[i+1] - m.eBands[i]) == 1 {
			narrow = 1
		} else {
			narrow = 0
		}
		copy(tmp, X[tf_chan][m.eBands[i]<<LM:])

		_LM := LM
		if isTransient == 0 {
			_LM = 0
		}

		L1 := l1_metric(tmp, N, _LM, bias)
		best_L1 := L1
		best_level := 0
		if isTransient != 0 && narrow == 0 {
			copy(tmp_1, tmp)
			haar1ZeroOffset(tmp_1, N>>LM, 1<<LM)
			L1 = l1_metric(tmp_1, N, LM+1, bias)
			if L1 < best_L1 {
				best_L1 = L1
				best_level = -1
			}
		}
		for k := 0; k < LM+comm.BoolToInt(!(isTransient != 0 || narrow != 0)); k++ {
			B := 0
			if isTransient != 0 {
				B = LM - k - 1
			} else {
				B = k + 1
			}
			haar1ZeroOffset(tmp, N>>k, 1<<k)
			L1 = l1_metric(tmp, N, B, bias)
			if L1 < best_L1 {
				best_L1 = L1
				best_level = k + 1
			}
		}
		if isTransient != 0 {
			metric[i] = 2 * best_level
		} else {
			metric[i] = -2 * best_level
		}
		_lm := 0
		if isTransient != 0 {
			_lm = LM
		}

		tf_sum.Val += (_lm) - metric[i]/2
		if narrow != 0 && (metric[i] == 0 || metric[i] == -2*LM) {
			metric[i] -= 1
		}
	}

	for sel := 0; sel < 2; sel++ {
		cost0 = 0
		cost1 = 0
		if isTransient == 0 {
			cost1 = lambda
		}
		for i := 1; i < len; i++ {
			curr0 := inlines.IMIN(cost0, cost1+lambda)
			curr1 := inlines.IMIN(cost0+lambda, cost1)
			cost0 = curr0 + inlines.Abs(metric[i]-2*int(CeltTables.Tf_select_table[LM][4*isTransient+2*sel+0]))
			cost1 = curr1 + inlines.Abs(metric[i]-2*int(CeltTables.Tf_select_table[LM][4*isTransient+2*sel+1]))
		}
		cost0 = inlines.IMIN(cost0, cost1)
		selcost[sel] = cost0
	}
	if selcost[1] < selcost[0] && isTransient != 0 {
		tf_select = 1
	}
	cost0 = 0
	cost1 = 0
	if isTransient == 0 {
		cost1 = lambda
	}
	for i := 1; i < len; i++ {
		var curr0 = 0
		var curr1 = 0
		from0 := cost0
		from1 := cost1 + lambda
		if from0 < from1 {
			curr0 = from0
			path0[i] = 0
		} else {
			curr0 = from1
			path0[i] = 1
		}

		from0 = cost0 + lambda
		from1 = cost1
		if from0 < from1 {
			curr1 = from0
			path1[i] = 0
		} else {
			curr1 = from1
			path1[i] = 1
		}
		cost0 = curr0 + inlines.Abs(metric[i]-2*int(CeltTables.Tf_select_table[LM][4*isTransient+2*tf_select+0]))
		cost1 = curr1 + inlines.Abs(metric[i]-2*int(CeltTables.Tf_select_table[LM][4*isTransient+2*tf_select+1]))
	}
	if cost0 < cost1 {
		tf_res[len-1] = 0
	} else {
		tf_res[len-1] = 1
	}
	for i := len - 2; i >= 0; i-- {
		if tf_res[i+1] == 1 {
			tf_res[i] = path1[i+1]
		} else {
			tf_res[i] = path0[i+1]
		}
	}
	return tf_select
}

func tf_encode(start int, end int, isTransient int, tf_res []int, LM int, tf_select int, enc *comm.EntropyCoder) {
	curr := 0
	tf_select_rsv := 0
	tf_changed := 0
	logp := 0
	if isTransient != 0 {
		logp = 2
	} else {
		logp = 4
	}
	budget := enc.Storage * 8
	tell := enc.Tell()
	if LM > 0 && tell+logp+1 <= budget {
		tf_select_rsv = 1
	}
	budget -= tf_select_rsv

	for i := start; i < end; i++ {
		if tell+logp <= budget {
			enc.Enc_bit_logp(tf_res[i]^curr, logp)
			tell = enc.Tell()
			curr = tf_res[i]
			if curr != 0 {
				tf_changed = 1
			}
		} else {
			tf_res[i] = curr
		}
		if isTransient != 0 {
			logp = 4
		} else {
			logp = 5
		}
	}

	if tf_select_rsv != 0 && CeltTables.Tf_select_table[LM][4*isTransient+0+tf_changed] != CeltTables.Tf_select_table[LM][4*isTransient+2+tf_changed] {
		enc.Enc_bit_logp(tf_select, 1)
	} else {
		tf_select = 0
	}

	for i := start; i < end; i++ {
		tf_res[i] = int(CeltTables.Tf_select_table[LM][4*isTransient+2*tf_select+tf_res[i]])
	}
}

func alloc_trim_analysis(m *CeltMode, X [][]int, bandLogE [][]int, end int, LM int, C int, analysis *AnalysisInfo, stereo_saving *comm.BoxedValueInt, tf_estimate int, intensity int, surround_trim int) int {
	var i int
	diff := 0
	var c int
	var trim_index int
	trim := 1280
	var logXC, logXC2 int
	if C == 2 {
		sum := 0
		var minXC int
		for i = 0; i < 8; i++ {
			partial := kernels.Celt_inner_prod_int(X[0], int(m.eBands[i]<<LM), X[1], int(m.eBands[i]<<LM), int(m.eBands[i+1]-m.eBands[i])<<LM)
			sum = inlines.ADD16Int(sum, int(inlines.EXTRACT16(inlines.SHR32(partial, 18))))
		}
		sum = inlines.MULT16_16_Q15Int(4096, sum)
		sum = inlines.MIN16Int(1024, inlines.ABS32(sum))
		minXC = sum
		for i = 8; i < intensity; i++ {
			partial := kernels.Celt_inner_prod_int(X[0], int(m.eBands[i]<<LM), X[1], int(m.eBands[i]<<LM), int(m.eBands[i+1]-m.eBands[i])<<LM)
			minXC = inlines.MIN16Int(minXC, inlines.ABS16(int(inlines.EXTRACT16(inlines.SHR32(partial, 18)))))
		}
		minXC = inlines.MIN16Int(1024, inlines.ABS32(minXC))
		logXC = inlines.Celt_log2(1049625 - int(inlines.MULT16_16(int(sum), int(sum))))
		logXC2 = inlines.MAX16Int(inlines.HALF16Int(logXC), inlines.Celt_log2(1049625-int(inlines.MULT16_16(int(minXC), int(minXC)))))
		q6 := int(int16(6 * (1 << CeltConstants.DB_SHIFT)))
		logXC = inlines.PSHR32(logXC-q6, CeltConstants.DB_SHIFT-8)
		logXC2 = inlines.PSHR32(logXC2-q6, CeltConstants.DB_SHIFT-8)
		trim += inlines.MAX16Int(-1024, inlines.MULT16_16_Q15Int(24576, logXC))
		stereo_saving.Val = inlines.MIN16Int(int(stereo_saving.Val)+64, -inlines.HALF16Int(logXC2))
	}
	c = 0
	for c < C {
		for i = 0; i < end-1; i++ {
			diff += bandLogE[c][i] * (2 + 2*i - end)
		}
		c++
	}
	diff /= C * (end - 1)
	q1 := int(int16(1 << CeltConstants.DB_SHIFT))
	temp := inlines.SHR16Int(diff+q1, CeltConstants.DB_SHIFT-8) / 6
	temp = inlines.MIN16Int(512, temp)
	temp = inlines.MAX16Int(-512, temp)
	trim -= temp
	trim -= inlines.SHR16Int(surround_trim, CeltConstants.DB_SHIFT-8)
	trim = trim - 2*inlines.SHR16Int(tf_estimate, 14-8)
	if analysis.Enabled && analysis.Valid != 0 {
		temp2 := int(512 * (analysis.Tonality_slope + 0.05))
		temp2 = inlines.MIN16Int(512, temp2)
		temp2 = inlines.MAX16Int(-512, temp2)
		trim -= temp2
	}
	trim_index = inlines.PSHR32(trim, 8)
	trim_index = inlines.IMAX(0, inlines.IMIN(10, trim_index))
	return trim_index
}
func stereo_analysis(m *CeltMode, X [][]int, LM int) int {
	thetas := 0
	sumLR := CeltConstants.EPSILON
	sumMS := CeltConstants.EPSILON

	for i := 0; i < 13; i++ {
		for j := m.eBands[i] << LM; j < m.eBands[i+1]<<LM; j++ {
			L := inlines.EXTEND32Int(X[0][j])
			R := inlines.EXTEND32Int(X[1][j])
			M := inlines.ADD32(L, R)
			S := inlines.SUB32(L, R)
			sumLR = inlines.ADD32(sumLR, inlines.ADD32(inlines.ABS32(L), inlines.ABS32(R)))
			sumMS = inlines.ADD32(sumMS, inlines.ADD32(inlines.ABS32(M), inlines.ABS32(S)))
		}
	}
	sumMS = inlines.MULT16_32_Q15Int(int(math.Trunc(0.707107*32767.5)), sumMS)
	thetas = 13
	if LM <= 1 {
		thetas -= 8
	}

	left := inlines.MULT16_32_Q15(int16(int(m.eBands[13])<<(LM+1)+thetas), sumMS)
	right := inlines.MULT16_32_Q15(int16(m.eBands[13]<<(LM+1)), sumLR)
	if left > right {
		return 1
	}
	return 0
}

func median_of_5(x []int, x_ptr int) int {
	var t0, t1, t2, t3, t4 int
	t2 = x[x_ptr+2]
	if x[x_ptr] > x[x_ptr+1] {
		t0 = x[x_ptr+1]
		t1 = x[x_ptr]
	} else {
		t0 = x[x_ptr]
		t1 = x[x_ptr+1]
	}
	if x[x_ptr+3] > x[x_ptr+4] {
		t3 = x[x_ptr+4]
		t4 = x[x_ptr+3]
	} else {
		t3 = x[x_ptr+3]
		t4 = x[x_ptr+4]
	}
	if t0 > t3 {
		// swap the pairs
		tmp := t3
		t3 = t0
		t0 = tmp
		tmp = t4
		t4 = t1
		t1 = tmp
	}
	if t2 > t1 {
		if t1 < t3 {
			return inlines.MIN16Int(t2, t3)
		} else {
			return inlines.MIN16Int(t4, t1)
		}
	} else if t2 < t3 {
		return inlines.MIN16Int(t1, t3)
	} else {
		return inlines.MIN16Int(t2, t4)
	}
}

func median_of_3(x []int, x_ptr int) int {
	var t0, t1, t2 int
	if x[x_ptr] > x[x_ptr+1] {
		t0 = x[x_ptr+1]
		t1 = x[x_ptr]
	} else {
		t0 = x[x_ptr]
		t1 = x[x_ptr+1]
	}
	t2 = x[x_ptr+2]
	if t1 < t2 {
		return t1
	} else if t0 < t2 {
		return t2
	} else {
		return t0
	}
}

func dynalloc_analysisbak(bandLogE [][]int, bandLogE2 [][]int, nbEBands int, start int, end int, C int, offsets []int, lsb_depth int, logN []int16, isTransient int, vbr int, constrained_vbr int, eBands []int16, LM int, effectiveBytes int, tot_boost_ *comm.BoxedValueInt, lfe int, surround_dynalloc []int) int {
	tot_boost := 0
	maxDepth := int(-31.9 * float32(int(1)<<CeltConstants.DB_SHIFT))
	noise_floor := make([]int, C*nbEBands)
	follower := make([][]int, 2)
	for i := range follower {
		follower[i] = make([]int, nbEBands)
	}

	for i := 0; i < end; i++ {
		noise_floor[i] = int(inlines.MULT16_16(int(0.0625*float32(int(1)<<CeltConstants.DB_SHIFT)), int(logN[i]))) +
			int(0.5*float32(int(1)<<CeltConstants.DB_SHIFT)) +
			(9-lsb_depth)<<CeltConstants.DB_SHIFT -
			int(CeltTables.EMeans[i])<<6 +
			int(inlines.MULT16_16(int(0.0062*float32(int(1)<<CeltConstants.DB_SHIFT)), int((i+5)*(i+5))))
	}

	for c := 0; c < C; c++ {
		for i := 0; i < end; i++ {
			depth := bandLogE[c][i] - noise_floor[i]
			if depth > maxDepth {
				maxDepth = depth
			}
		}
	}

	if effectiveBytes > 50 && LM >= 1 && lfe == 0 {
		last := 0
		for c := 0; c < C; c++ {
			f := follower[c]
			f[0] = bandLogE2[c][0]
			for i := 1; i < end; i++ {
				if bandLogE2[c][i] > bandLogE2[c][i-1]+int(0.5*float32(int(1)<<CeltConstants.DB_SHIFT)) {
					last = i
				}
				f[i] = inlines.MIN16Int(f[i-1]+int(1.5*float32(int(1)<<CeltConstants.DB_SHIFT)), bandLogE2[c][i])
			}
			for i := last - 1; i >= 0; i-- {
				f[i] = inlines.MIN16Int(f[i], inlines.MIN16Int(f[i+1]+int(2.0*float32(int(1)<<CeltConstants.DB_SHIFT)), bandLogE2[c][i]))
			}
			offset := int(1.0 * float32(int(1)<<CeltConstants.DB_SHIFT))
			for i := 2; i < end-2; i++ {
				med := median_of_5(bandLogE2[c], i-2) - offset
				if f[i] < med {
					f[i] = med
				}
			}
			tmp := median_of_3(bandLogE2[c], 0) - offset
			if f[0] < tmp {
				f[0] = tmp
			}
			if f[1] < tmp {
				f[1] = tmp
			}
			tmp = median_of_3(bandLogE2[c], end-3) - offset
			if f[end-2] < tmp {
				f[end-2] = tmp
			}
			if f[end-1] < tmp {
				f[end-1] = tmp
			}
			for i := 0; i < end; i++ {
				if f[i] < noise_floor[i] {
					f[i] = noise_floor[i]
				}
			}
		}

		if C == 2 {
			for i := start; i < end; i++ {
				f0 := follower[0][i]
				f1 := follower[1][i]
				if f1 < f0-int(4.0*float32(int(1)<<CeltConstants.DB_SHIFT)) {
					f1 = f0 - int(4.0*float32(int(1)<<CeltConstants.DB_SHIFT))
				}
				if f0 < f1-int(4.0*float32(int(1)<<CeltConstants.DB_SHIFT)) {
					f0 = f1 - int(4.0*float32(int(1)<<CeltConstants.DB_SHIFT))
				}
				follower[0][i] = (inlines.MAX16Int(0, bandLogE[0][i]-f0) + inlines.MAX16Int(0, bandLogE[1][i]-f1)) / 2
			}
		} else {
			for i := start; i < end; i++ {
				follower[0][i] = inlines.MAX16Int(0, bandLogE[0][i]-follower[0][i])
			}
		}

		for i := start; i < end; i++ {
			if follower[0][i] < surround_dynalloc[i] {
				follower[0][i] = surround_dynalloc[i]
			}
		}

		if vbr == 0 || (constrained_vbr != 0 && isTransient == 0) {
			for i := start; i < end; i++ {
				follower[0][i] /= 2
			}
		}

		for i := start; i < end; i++ {
			width := C * (int(eBands[i+1]) - int(eBands[i])) << LM
			boost := 0
			boost_bits := 0
			if i < 8 {
				follower[0][i] *= 2
			}
			if i >= 12 {
				follower[0][i] /= 2
			}
			if follower[0][i] > int(4.0*float32(int(1)<<CeltConstants.DB_SHIFT)) {
				follower[0][i] = int(4.0 * float32(int(1)<<CeltConstants.DB_SHIFT))
			}

			if width < 6 {
				boost = follower[0][i] >> CeltConstants.DB_SHIFT
				boost_bits = boost * width << BITRES
			} else if width > 48 {
				boost = (follower[0][i] * 8) >> CeltConstants.DB_SHIFT
				boost_bits = (boost * width << BITRES) / 8
			} else {
				boost = (follower[0][i] * width) / (6 << CeltConstants.DB_SHIFT)
				boost_bits = boost * 6 << BITRES
			}

			if (vbr == 0 || (constrained_vbr != 0 && isTransient == 0)) && (tot_boost+boost_bits)>>(BITRES+3) > effectiveBytes/4 {
				cap := (effectiveBytes / 4) << (BITRES + 3)
				offsets[i] = (cap - tot_boost) >> BITRES
				tot_boost = cap
				break
			} else {
				offsets[i] = boost
				tot_boost += boost_bits
			}
		}
	}

	tot_boost_.Val = tot_boost
	return maxDepth
}

func dynalloc_analysis(bandLogE [][]int, bandLogE2 [][]int, nbEBands int, start int, end int, C int, offsets []int, lsb_depth int, logN []int16, isTransient int, vbr int, constrained_vbr int, eBands []int16, LM int, effectiveBytes int, tot_boost_ *comm.BoxedValueInt, lfe int, surround_dynalloc []int) int {

	var i, c int
	var tot_boost = 0
	var maxDepth int
	follower := arrayUtil.InitTwoDimensionalArrayInt(2, nbEBands)
	noise_floor := make([]int, C*nbEBands) // opt: partitioned array

	arrayUtil.MemSetLen(offsets, 0, nbEBands)
	/* Dynamic allocation code */
	maxDepth = int(0 - (int16(0.5 + (31.9)*float64((int32(1))<<(CeltConstants.DB_SHIFT)))))
	for i = 0; i < end; i++ {
		/* Noise floor must take into account eMeans, the depth, the width of the bands
		   and the preemphasis filter (approx. square of bark band ID) */
		noise_floor[i] = inlines.MULT16_16Short((int16(0.5+(0.0625)*float64((int32(1))<<(CeltConstants.DB_SHIFT)))), logN[i]) +
			int(int16(math.Trunc(0.5+0.5*float64((int32(1))<<(CeltConstants.DB_SHIFT))))) +
			inlines.SHL16Int((9-lsb_depth), CeltConstants.DB_SHIFT) -
			int(inlines.SHL16(int16(eMeans[i]), 6)) +
			inlines.MULT16_16Short((int16(0.5+(0.0062)*float64((int32(1))<<(CeltConstants.DB_SHIFT)))), int16((i+5)*(i+5)))
	}
	c = 0
	for {
		for i = 0; i < end; i++ {
			maxDepth = inlines.MAX16Int(maxDepth, (bandLogE[c][i] - noise_floor[i]))
		}
		c++
		if c < C {
			continue
		}
		break
	}
	/* Make sure that dynamic allocation can't make us bust the budget */
	if effectiveBytes > 50 && LM >= 1 && lfe == 0 {
		var last = 0
		c = 0
		for {
			var offset int
			var tmp int
			f := follower[c]
			f[0] = bandLogE2[c][0]
			for i = 1; i < end; i++ {
				/* The last band to be at least 3 dB higher than the previous one
				   is the last we'll consider. Otherwise, we run into problems on
				   bandlimited signals. */
				if bandLogE2[c][i] > bandLogE2[c][i-1]+int(int16(0.5+(0.5)*float64((int32(1))<<(CeltConstants.DB_SHIFT)))) {
					last = i
				}
				f[i] = inlines.MIN16Int((f[i-1] + int(int16(0.5+(1.5)*float64((int32(1))<<(CeltConstants.DB_SHIFT))))), bandLogE2[c][i])
			}
			for i = last - 1; i >= 0; i-- {
				f[i] = inlines.MIN16Int(f[i], inlines.MIN16Int((f[i+1]+int(int16(0.5+(2.0)*float64((int32(1))<<(CeltConstants.DB_SHIFT))))), bandLogE2[c][i]))
			}

			/* Combine with a median filter to avoid dynalloc triggering unnecessarily.
			   The "offset" value controls how conservative we are -- a higher offset
			   reduces the impact of the median filter and makes dynalloc use more bits. */
			offset = int(int16(0.5 + (1.0)*float64((int32(1))<<(CeltConstants.DB_SHIFT))))
			for i = 2; i < end-2; i++ {
				f[i] = inlines.MAX16Int(f[i], median_of_5(bandLogE2[c], i-2)-offset)
			}
			tmp = median_of_3(bandLogE2[c], 0) - offset
			f[0] = inlines.MAX16Int(f[0], tmp)
			f[1] = inlines.MAX16Int(f[1], tmp)
			tmp = median_of_3(bandLogE2[c], end-3) - offset
			f[end-2] = inlines.MAX16Int(f[end-2], tmp)
			f[end-1] = inlines.MAX16Int(f[end-1], tmp)

			for i = 0; i < end; i++ {
				f[i] = inlines.MAX16Int(f[i], noise_floor[i])
			}
			c++
			if c < C {
				continue
			}
			break
		}
		if C == 2 {
			for i = start; i < end; i++ {
				/* Consider 24 dB "cross-talk" */
				follower[1][i] = inlines.MAX16Int(follower[1][i], follower[0][i]-int(int16(0.5+(4.0)*float64((int32(1))<<(CeltConstants.DB_SHIFT)))))
				follower[0][i] = inlines.MAX16Int(follower[0][i], follower[1][i]-int(int16(0.5+(4.0)*float64((int32(1))<<(CeltConstants.DB_SHIFT)))))
				follower[0][i] = inlines.HALF16Int(inlines.MAX16Int(0, bandLogE[0][i]-follower[0][i]) + inlines.MAX16Int(0, bandLogE[1][i]-follower[1][i]))
			}
		} else {
			for i = start; i < end; i++ {
				follower[0][i] = inlines.MAX16Int(0, bandLogE[0][i]-follower[0][i])
			}
		}
		for i = start; i < end; i++ {
			follower[0][i] = inlines.MAX16Int(follower[0][i], surround_dynalloc[i])
		}
		/* For non-transient CBR/CVBR frames, halve the dynalloc contribution */
		if (vbr == 0 || constrained_vbr != 0) && isTransient == 0 {
			for i = start; i < end; i++ {
				follower[0][i] = inlines.HALF16Int(follower[0][i])
			}
		}
		for i = start; i < end; i++ {
			var width int
			var boost int
			var boost_bits int

			if i < 8 {
				follower[0][i] *= 2
			}
			if i >= 12 {
				follower[0][i] = inlines.HALF16Int(follower[0][i])
			}
			follower[0][i] = inlines.MIN16Int(follower[0][i], int(int16(0.5+(4)*float64((int32(1))<<(CeltConstants.DB_SHIFT)))))

			width = C * int(eBands[i+1]-eBands[i]) << LM
			if width < 6 {
				boost = int(inlines.SHR32((follower[0][i]), CeltConstants.DB_SHIFT))
				boost_bits = boost * width << BITRES
			} else if width > 48 {
				boost = inlines.SHR32((follower[0][i])*8, CeltConstants.DB_SHIFT)
				boost_bits = (boost * width << BITRES) / 8
			} else {
				boost = inlines.SHR32((follower[0][i])*width/6, CeltConstants.DB_SHIFT)
				boost_bits = boost * 6 << BITRES
			}
			/* For CBR and non-transient CVBR frames, limit dynalloc to 1/4 of the bits */
			if (vbr == 0 || (constrained_vbr != 0 && isTransient == 0)) &&
				(tot_boost+boost_bits)>>BITRES>>3 > effectiveBytes/4 {
				cap := ((effectiveBytes / 4) << BITRES << 3)
				offsets[i] = cap - tot_boost
				tot_boost = cap
				break
			} else {
				offsets[i] = boost

				tot_boost += boost_bits
			}
		}
	}
	tot_boost_.Val = tot_boost

	return maxDepth
}
func deemphasis(input [][]int, input_ptrs []int, pcm []int16, pcm_ptr int, N int, C int, downsample int, coef []int,
	mem []int, accum int) {
	var c int
	var Nd int
	var apply_downsampling int = 0
	var coef0 int
	scratch := make([]int, N)
	coef0 = coef[0]
	Nd = N / downsample
	c = 0
	for {
		var j int
		var x_ptr int
		var y int
		m := mem[c]
		x := input[c]
		x_ptr = input_ptrs[c]
		y = pcm_ptr + c
		if downsample > 1 {
			/* Shortcut for the standard (non-custom modes) case */
			for j = 0; j < N; j++ {
				tmp := x[x_ptr+j] + m + CeltConstants.VERY_SMALL
				m = inlines.MULT16_32_Q15Int(coef0, tmp)
				scratch[j] = tmp
			}
			apply_downsampling = 1
		} else if accum != 0 {
			for j = 0; j < N; j++ {
				tmp := x[x_ptr+j] + m + CeltConstants.VERY_SMALL
				m = inlines.MULT16_32_Q15Int(coef0, tmp)
				pcm[y+(j*C)] = inlines.SAT16(inlines.ADD32(int(pcm[y+(j*C)]), int(inlines.SIG2WORD16(tmp))))
			}
		} else {
			for j = 0; j < N; j++ {
				tmp := (x[x_ptr+j] + m + CeltConstants.VERY_SMALL)
				if x[x_ptr+j] > 0 && m > 0 && tmp < 0 {
					tmp = math.MaxInt32
					m = math.MaxInt32
				} else {
					m = inlines.MULT16_32_Q15Int(coef0, tmp)
				}
				pcm[y+(j*C)] = inlines.SIG2WORD16(tmp)
			}
		}
		mem[c] = m

		if apply_downsampling != 0 {
			/* Perform down-sampling */
			{
				for j = 0; j < Nd; j++ {
					pcm[y+(j*C)] = inlines.SIG2WORD16(scratch[j*downsample])
				}
			}
		}
		c++
		if c < C {
			continue
		}
		break
	}

}

func celt_synthesis(mode *CeltMode, X [][]int, out_syn [][]int, out_syn_ptrs []int,
	oldBandE []int, start int, effEnd int, C int, CC int,
	isTransient int, LM int, downsample int,
	silence int) {
	var c, i int
	var M int
	var b int
	var B int
	var N, NB int
	var shift int
	var nbEBands int
	var overlap int
	var freq []int

	overlap = mode.Overlap
	nbEBands = mode.nbEBands
	N = mode.ShortMdctSize << LM

	freq = make([]int, N)
	/**
	 * < Interleaved signal MDCTs
	 */
	M = 1 << LM

	if isTransient != 0 {
		B = M
		NB = mode.ShortMdctSize
		shift = mode.MaxLM
	} else {
		B = 1
		NB = mode.ShortMdctSize << LM
		shift = mode.MaxLM - LM
	}

	if CC == 2 && C == 1 {
		/* Copying a mono streams to two channels */
		var freq2 int
		denormalise_bands(mode, X[0], freq, 0, oldBandE, 0, start, effEnd, M,
			downsample, silence)
		/* Store a temporary copy in the output buffer because the IMDCT destroys its input. */
		freq2 = out_syn_ptrs[1] + (overlap / 2)
		//System.arraycopy(freq, 0, out_syn[1], freq2, N)
		copy(out_syn[1][freq2:freq2+N], freq)
		for b = 0; b < B; b++ {
			clt_mdct_backward(mode.Mdct, out_syn[1], freq2+b, out_syn[0], out_syn_ptrs[0]+(NB*b), mode.Window, overlap, shift, B)
		}
		for b = 0; b < B; b++ {
			clt_mdct_backward(mode.Mdct, freq, b, out_syn[1], out_syn_ptrs[1]+(NB*b), mode.Window, overlap, shift, B)
		}
	} else if CC == 1 && C == 2 {
		/* Downmixing a stereo stream to mono */
		freq2 := out_syn_ptrs[0] + (overlap / 2)
		denormalise_bands(mode, X[0], freq, 0, oldBandE, 0, start, effEnd, M,
			downsample, silence)
		/* Use the output buffer as temp array before downmixing. */
		denormalise_bands(mode, X[1], out_syn[0], freq2, oldBandE, nbEBands, start, effEnd, M,
			downsample, silence)
		for i = 0; i < N; i++ {
			freq[i] = inlines.HALF32(inlines.ADD32(freq[i], out_syn[0][freq2+i]))
		}
		for b = 0; b < B; b++ {
			clt_mdct_backward(mode.Mdct, freq, b, out_syn[0], out_syn_ptrs[0]+(NB*b), mode.Window, overlap, shift, B)
		}
	} else {
		/* Normal case (mono or stereo) */
		c = 0
		for {
			denormalise_bands(mode, X[c], freq, 0, oldBandE, c*nbEBands, start, effEnd, M,
				downsample, silence)
			for b = 0; b < B; b++ {
				clt_mdct_backward(mode.Mdct, freq, b, out_syn[c], out_syn_ptrs[c]+(NB*b), mode.Window, overlap, shift, B)
			}
			c++
			if c < CC {
				continue
			}
			break
		}
	}

}

func tf_decode(start int, end int, isTransient int, tf_res []int, LM int, dec *comm.EntropyCoder) {
	curr := 0
	tf_select := 0
	tf_select_rsv := 0
	tf_changed := 0
	logp := 0
	if isTransient != 0 {
		logp = 2
	} else {
		logp = 4
	}
	budget := dec.Storage * 8
	tell := dec.Tell()
	if LM > 0 && tell+logp+1 <= budget {
		tf_select_rsv = 1
	}
	budget -= tf_select_rsv

	for i := start; i < end; i++ {
		if tell+logp <= budget {
			bit := dec.Dec_bit_logp(int64(logp))
			curr ^= bit
			if bit != 0 {
				tf_changed = 1
			}
			tell = dec.Tell()
		}
		tf_res[i] = curr
		if isTransient != 0 {
			logp = 4
		} else {
			logp = 5
		}
	}

	if tf_select_rsv != 0 && CeltTables.Tf_select_table[LM][4*isTransient+0+tf_changed] != CeltTables.Tf_select_table[LM][4*isTransient+2+tf_changed] {
		tf_select = dec.Dec_bit_logp(1)
	}

	for i := start; i < end; i++ {
		tf_res[i] = int(CeltTables.Tf_select_table[LM][4*isTransient+2*tf_select+tf_res[i]])
	}
}

func celt_plc_pitch_search(decode_mem [][]int, C int) int {
	pitch_index := comm.BoxedValueInt{Val: 0}
	lp_pitch_buf := make([]int, CeltConstants.DECODE_BUFFER_SIZE>>1)
	pitch_downsample(decode_mem, lp_pitch_buf, CeltConstants.DECODE_BUFFER_SIZE, C)
	pitch_search(lp_pitch_buf, CeltConstants.PLC_PITCH_LAG_MAX>>1, lp_pitch_buf, CeltConstants.DECODE_BUFFER_SIZE-CeltConstants.PLC_PITCH_LAG_MAX, CeltConstants.PLC_PITCH_LAG_MAX-CeltConstants.PLC_PITCH_LAG_MIN, &pitch_index)
	return CeltConstants.PLC_PITCH_LAG_MAX - pitch_index.Val
}

func Resampling_factor(rate int) int {
	switch rate {
	case 48000:
		return 1
	case 24000:
		return 2
	case 16000:
		return 3
	case 12000:
		return 4
	case 8000:
		return 6
	default:
		panic("resampling_factor: unsupported rate")
	}
}

func comb_filter_const(y []int, y_ptr int, x []int, x_ptr int, T int, N int, g10 int, g11 int, g12 int) {
	var x0, x1, x2, x3, x4 int
	var i int
	xpt := x_ptr - T
	x4 = x[xpt-2]
	x3 = x[xpt-1]
	x2 = x[xpt]
	x1 = x[xpt+1]
	for i = 0; i < N; i++ {
		x0 = x[xpt+i+2]
		y[y_ptr+i] = x[x_ptr+i] +
			inlines.MULT16_32_Q15Int(g10, x2) +
			inlines.MULT16_32_Q15Int(g11, inlines.ADD32(x1, x3)) +
			inlines.MULT16_32_Q15Int(g12, inlines.ADD32(x0, x4))
		x4 = x3
		x3 = x2
		x2 = x1
		x1 = x0
	}
}

var gains [][]int16

func init() {
	gains = [][]int16{
		{int16(math.Trunc(0.5 + (0.3066406250)*((1)<<(15)))), int16(math.Trunc(0.5 + (0.2170410156)*((1)<<(15)))), int16(math.Trunc(0.5 + (0.1296386719)*((1)<<(15))))},
		{int16(math.Trunc(0.5 + (0.4638671875)*((1)<<(15)))), int16(math.Trunc(0.5 + (0.2680664062)*((1)<<(15)))), int16(math.Trunc(0.5 + (0.0)*((1)<<(15))))},
		{int16(math.Trunc(0.5 + (0.7998046875)*((1)<<(15)))), int16(math.Trunc(0.5 + (0.1000976562)*((1)<<(15)))), int16(math.Trunc(0.5 + (0.0)*((1)<<(15))))},
	}
}

func comb_filter(y []int, y_ptr int, x []int, x_ptr int, T0 int, T1 int, N int, g0 int, g1 int, tapset0 int, tapset1 int, window []int, overlap int) {
	var i int
	/* printf ("%d %d %f %f\n", T0, T1, g0, g1); */
	var g00, g01, g02, g10, g11, g12 int
	var x0, x1, x2, x3, x4 int

	if g0 == 0 && g1 == 0 {
		/* OPT: Happens to work without the OPUS_MOVE(), but only because the current encoder already copies x to y */
		if x_ptr != y_ptr {
			//x.MemMoveTo(y, N);
		}

		return
	}
	g00 = inlines.MULT16_16_P15Int(g0, int(gains[tapset0][0]))
	g01 = inlines.MULT16_16_P15Int(g0, int(gains[tapset0][1]))
	g02 = inlines.MULT16_16_P15Int(g0, int(gains[tapset0][2]))
	g10 = inlines.MULT16_16_P15Int(g1, int(gains[tapset1][0]))
	g11 = inlines.MULT16_16_P15Int(g1, int(gains[tapset1][1]))
	g12 = inlines.MULT16_16_P15Int(g1, int(gains[tapset1][2]))
	x1 = x[x_ptr-T1+1]
	x2 = x[x_ptr-T1]
	x3 = x[x_ptr-T1-1]
	x4 = x[x_ptr-T1-2]
	/* If the filter didn't change, we don't need the overlap */
	if g0 == g1 && T0 == T1 && tapset0 == tapset1 {
		overlap = 0
	}
	for i = 0; i < overlap; i++ {
		var f int
		x0 = x[x_ptr+i-T1+2]
		f = inlines.MULT16_16_Q15Int(window[i], window[i])
		y[y_ptr+i] = x[x_ptr+i] +
			inlines.MULT16_32_Q15(inlines.MULT16_16_Q15(int16(CeltConstants.Q15ONE-f), int16(g00)), x[x_ptr+i-T0]) +
			inlines.MULT16_32_Q15(inlines.MULT16_16_Q15(int16(CeltConstants.Q15ONE-f), int16(g01)), inlines.ADD32(x[x_ptr+i-T0+1], x[x_ptr+i-T0-1])) +
			inlines.MULT16_32_Q15(inlines.MULT16_16_Q15(int16(CeltConstants.Q15ONE-f), int16(g02)), inlines.ADD32(x[x_ptr+i-T0+2], x[x_ptr+i-T0-2])) +
			inlines.MULT16_32_Q15Int(inlines.MULT16_16_Q15Int(f, g10), x2) +
			inlines.MULT16_32_Q15Int(inlines.MULT16_16_Q15Int(f, g11), inlines.ADD32(x1, x3)) +
			+inlines.MULT16_32_Q15Int(inlines.MULT16_16_Q15Int(f, g12), inlines.ADD32(x0, x4))
		x4 = x3
		x3 = x2
		x2 = x1
		x1 = x0

	}
	if g1 == 0 {
		/* OPT: Happens to work without the OPUS_MOVE(), but only because the current encoder already copies x to y */
		if x_ptr != y_ptr {
			//x.Point(overlap).MemMoveTo(y.Point(overlap), N - overlap);
		}
		return
	}

	/* Compute the part with the constant filter. */
	comb_filter_const(y, y_ptr+i, x, x_ptr+i, T1, N-i, g10, g11, g12)
}

func init_caps(m *CeltMode, cap []int, LM int, C int) {
	for i := 0; i < m.nbEBands; i++ {
		N := int(m.eBands[i+1]-m.eBands[i]) << LM
		cap[i] = int(m.cache.caps[m.nbEBands*(2*LM+C-1)+i]+64) * C * N >> 2
	}
}
