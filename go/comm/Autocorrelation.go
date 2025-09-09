package comm

const QC = 10
const QS = 14

func Silk_autocorr(results []int, scale *BoxedValueInt, inputData []int16, inputDataSize int, correlationCount int) {
	corrCount := inlines.Silk_min_int(inputDataSize, correlationCount)
	scale.Val = _celt_autocorr(inputData, results, corrCount-1, inputDataSize)
}

func _celt_autocorr(x []int16, ac []int, lag int, n int) int {

	var d int
	var i, k int
	var fastN = n - lag
	var shift int
	var xptr []int16
	var xx = make([]int16, n)
	inlines.OpusAssert(n > 0)
	xptr = x

	shift = 0

	var ac0 int
	ac0 = 1 + (n << 7)
	if (n & 1) != 0 {
		ac0 += inlines.SHR32(inlines.MULT16_16Short(xptr[0], xptr[0]), 9)
	}
	for i = (n & 1); i < n; i += 2 {
		ac0 += inlines.SHR32(inlines.MULT16_16Short(xptr[i], xptr[i]), 9)
		ac0 += inlines.SHR32(inlines.MULT16_16Short(xptr[i+1], xptr[i+1]), 9)
	}
	shift = inlines.Celt_ilog2(ac0) - 30 + 10
	shift = (shift) / 2
	if shift > 0 {
		for i = 0; i < n; i++ {
			xx[i] = int16(inlines.PSHR32(int(xptr[i]), int(shift)))
		}
		xptr = xx
	} else {
		shift = 0
	}

	pitch_xcorr2(xptr, xptr, ac, fastN, lag+1)

	for k = 0; k <= lag; k++ {
		i = k + fastN
		d = 0
		for ; i < n; i++ {
			d = inlines.MAC16_16Int(d, xptr[i], xptr[i-k])
		}
		ac[k] += d
	}

	shift = 2 * shift
	if shift <= 0 {
		ac[0] += inlines.SHL32(1, -shift)
	}

	if ac[0] < 268435456 {
		var shift2 = 29 - inlines.EC_ILOG(int64(ac[0]))
		for i = 0; i <= lag; i++ {
			ac[i] = inlines.SHL32(ac[i], shift2)
		}
		shift -= shift2
	} else if ac[0] >= 536870912 {
		var shift2 = 1
		if ac[0] >= 1073741824 {
			shift2++
		}

		for i = 0; i <= lag; i++ {
			ac[i] = int(inlines.SHR32(int(ac[i]), int(shift2)))
		}
		shift += shift2
	}
	return shift
}

func Celt_autocorr_with_window(x []int, ac []int, window []int, overlap int, lag int, n int) int {
	d := int(0)
	fastN := n - lag
	shift := 0
	var xptr []int
	xx := make([]int, n)

	inlines.OpusAssert(n > 0)
	inlines.OpusAssert(overlap >= 0)

	if overlap == 0 {
		xptr = x
	} else {
		for i := 0; i < n; i++ {
			xx[i] = x[i]
		}
		for i := 0; i < overlap; i++ {
			xx[i] = inlines.MULT16_16_Q15Int(x[i], window[i])
			xx[n-i-1] = inlines.MULT16_16_Q15Int(x[n-i-1], window[i])
		}
		xptr = xx
	}

	ac0 := int(1 + (n << 7))
	if (n & 1) != 0 {
		ac0 += inlines.SHR32(inlines.MULT16_16(xptr[0], xptr[0]), 9)
	}
	for i := (n & 1); i < n; i += 2 {
		ac0 += inlines.SHR32(inlines.MULT16_16(xptr[i], xptr[i]), 9)
		ac0 += inlines.SHR32(inlines.MULT16_16(xptr[i+1], xptr[i+1]), 9)
	}

	shift = inlines.Celt_ilog2(ac0) - 30 + 10
	shift = (shift) / 2
	if shift > 0 {
		for i := 0; i < n; i++ {
			xx[i] = inlines.PSHR32(xptr[i], shift)
		}
		xptr = xx
	} else {
		shift = 0
	}

	Pitch_xcorr(xptr, xptr, ac, fastN, lag+1)
	for k := 0; k <= lag; k++ {
		d = 0
		for i := k + fastN; i < n; i++ {
			d = inlines.MAC16_16IntAll(d, xptr[i], xptr[i-k])
		}
		ac[k] += d
	}

	shift = 2 * shift
	if shift <= 0 {
		ac[0] += inlines.SHL32(1, -shift)
	}
	if ac[0] < 268435456 {
		shift2 := 29 - inlines.EC_ILOG(int64(ac[0]))
		for i := 0; i <= lag; i++ {
			ac[i] = inlines.SHL32(ac[i], shift2)
		}
		shift -= shift2
	} else if ac[0] >= 536870912 {
		shift2 := 1
		if ac[0] >= 1073741824 {
			shift2++
		}
		for i := 0; i <= lag; i++ {
			ac[i] = inlines.SHR32(ac[i], shift2)
		}
		shift += shift2
	}

	return shift
}

func Silk_warped_autocorr(corr []int, scale *BoxedValueInt, input []int16, warping_Q16 int, length int, order int) {
	var n, i, lsh int
	var tmp1_QS, tmp2_QS int
	state_QS := make([]int, SilkConstants.MAX_SHAPE_LPC_ORDER+1)
	corr_QC := make([]int64, SilkConstants.MAX_SHAPE_LPC_ORDER+1)

	inlines.OpusAssert((order & 1) == 0)
	inlines.OpusAssert(2*QS-QC >= 0)

	for n = 0; n < length; n++ {
		tmp1_QS = int(inlines.SHL32(int(input[n]), QS))
		for i = 0; i < order; i += 2 {
			tmp2_QS = inlines.Silk_SMLAWB(state_QS[i], state_QS[i+1]-tmp1_QS, int(warping_Q16))
			state_QS[i] = tmp1_QS
			corr_QC[i] += inlines.Silk_RSHIFT64(inlines.Silk_SMULL(int(tmp1_QS), int(state_QS[0])), 2*QS-QC)
			tmp1_QS = inlines.Silk_SMLAWB(state_QS[i+1], state_QS[i+2]-tmp2_QS, int(warping_Q16))
			state_QS[i+1] = tmp2_QS
			corr_QC[i+1] += inlines.Silk_RSHIFT64(inlines.Silk_SMULL(int(tmp2_QS), int(state_QS[0])), 2*QS-QC)
		}
		state_QS[order] = tmp1_QS
		corr_QC[order] += inlines.Silk_RSHIFT64(inlines.Silk_SMULL(int(tmp1_QS), int(state_QS[0])), 2*QS-QC)
	}

	lsh = inlines.Silk_CLZ64(corr_QC[0]) - 35
	lsh = inlines.Silk_LIMIT(lsh, -12-QC, 30-QC)
	scale.Val = -(QC + lsh)
	inlines.OpusAssert(scale.Val >= -30 && scale.Val <= 12)
	if lsh >= 0 {
		for i = 0; i < order+1; i++ {
			corr[i] = int(inlines.Silk_LSHIFT64(corr_QC[i], lsh))
		}
	} else {
		for i = 0; i < order+1; i++ {
			corr[i] = int(inlines.Silk_RSHIFT64(corr_QC[i], -lsh))
		}
	}
}
