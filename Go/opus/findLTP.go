package opus

import "math"

const LTP_CORRS_HEAD_ROOM = 2

func silk_find_LTP(b_Q14 []int16, WLTP []int, LTPredCodGain_Q7 *BoxedValueInt, r_lpc []int16, lag []int, Wght_Q15 []int, subfr_length int, nb_subfr int, mem_offset int, corr_rshifts []int) {
	var i, k, lshift int
	var r_ptr int
	var lag_ptr int
	var b_Q14_ptr int

	var regu int
	var WLTP_ptr int
	b_Q16 := make([]int, SilkConstants.LTP_ORDER)
	delta_b_Q14 := make([]int, SilkConstants.LTP_ORDER)
	d_Q14 := make([]int, SilkConstants.MAX_NB_SUBFR)
	nrg := make([]int, SilkConstants.MAX_NB_SUBFR)
	var g_Q26 int
	w := make([]int, SilkConstants.MAX_NB_SUBFR)
	var WLTP_max, max_abs_d_Q14, max_w_bits int

	var temp32, denom32 int
	var extra_shifts int
	var rr_shifts, maxRshifts, maxRshifts_wxtra, LZs int
	var LPC_res_nrg, LPC_LTP_res_nrg, div_Q16 int
	Rr := make([]int, SilkConstants.LTP_ORDER)
	rr := make([]int, SilkConstants.MAX_NB_SUBFR)
	var wd, m_Q12 int

	b_Q14_ptr = 0
	WLTP_ptr = 0
	r_ptr = mem_offset
	for k = 0; k < nb_subfr; k++ {
		lag_ptr = r_ptr - (lag[k] + SilkConstants.LTP_ORDER/2)
		boxed_rr := &BoxedValueInt{0}
		boxed_rr_shift := &BoxedValueInt{0}
		silk_sum_sqr_shift5(boxed_rr, boxed_rr_shift, r_lpc, r_ptr, subfr_length)
		/* rr[ k ] in Q( -rr_shifts ) */
		rr[k] = boxed_rr.Val
		rr_shifts = boxed_rr_shift.Val

		/* Assure headroom */
		LZs = silk_CLZ32(rr[k])
		if LZs < LTP_CORRS_HEAD_ROOM {
			rr[k] = silk_RSHIFT_ROUND(rr[k], LTP_CORRS_HEAD_ROOM-LZs)
			rr_shifts += (LTP_CORRS_HEAD_ROOM - LZs)
		}
		corr_rshifts[k] = rr_shifts
		boxed_shifts := &BoxedValueInt{corr_rshifts[k]}
		CorrelateMatrix.silk_corrMatrix(r_lpc, lag_ptr, subfr_length, SilkConstants.LTP_ORDER, LTP_CORRS_HEAD_ROOM, WLTP, WLTP_ptr, boxed_shifts)
		/* WLTP_ptr in Q( -corr_rshifts[ k ] ) */
		corr_rshifts[k] = boxed_shifts.Val

		/* The correlation vector always has lower max abs value than rr and/or RR so head room is assured */
		CorrelateMatrix.silk_corrVector(r_lpc, lag_ptr, r_lpc, r_ptr, subfr_length, SilkConstants.LTP_ORDER, Rr, corr_rshifts[k])
		/* Rr_ptr   in Q( -corr_rshifts[ k ] ) */
		if corr_rshifts[k] > rr_shifts {
			rr[k] = silk_RSHIFT(rr[k], corr_rshifts[k]-rr_shifts)
			/* rr[ k ] in Q( -corr_rshifts[ k ] ) */
		}
		OpusAssert(rr[k] >= 0)

		regu = 1
		regu = silk_SMLAWB(regu, rr[k], int(float64(TuningParameters.LTP_DAMPING/3)*float64(int64(1)<<(16))+0.5))
		regu = silk_SMLAWB(regu, MatrixGetPtr(WLTP, WLTP_ptr, 0, 0, SilkConstants.LTP_ORDER), (int(float64(TuningParameters.LTP_DAMPING/3)*float64(int64(1)<<(16)) + 0.5)))
		regu = silk_SMLAWB(regu, MatrixGetPtr(WLTP, WLTP_ptr, SilkConstants.LTP_ORDER-1, SilkConstants.LTP_ORDER-1, SilkConstants.LTP_ORDER), (int(float64(TuningParameters.LTP_DAMPING/3)*float64(int64(1)<<(16)) + 0.5)))
		silk_regularize_correlations(WLTP, WLTP_ptr, rr, k, regu, SilkConstants.LTP_ORDER)

		silk_solve_LDL(WLTP, WLTP_ptr, SilkConstants.LTP_ORDER, Rr, b_Q16)
		/* WLTP_ptr and Rr_ptr both in Q(-corr_rshifts[k]) */

		/* Limit and store in Q14 */
		silk_fit_LTP(b_Q16, b_Q14, b_Q14_ptr)

		/* Calculate residual energy */
		nrg[k] = silk_residual_energy16_covar(b_Q14, b_Q14_ptr, WLTP, WLTP_ptr, Rr, rr[k], SilkConstants.LTP_ORDER, 14)
		/* nrg in Q( -corr_rshifts[ k ] ) */

		/* temp = Wght[ k ] / ( nrg[ k ] * Wght[ k ] + 0.01f * subfr_length ); */
		extra_shifts = silk_min_int(corr_rshifts[k], LTP_CORRS_HEAD_ROOM)
		denom32 = silk_LSHIFT_SAT32(silk_SMULWB(nrg[k], Wght_Q15[k]), 1+extra_shifts) +
			silk_RSHIFT(silk_SMULWB(subfr_length, 655), corr_rshifts[k]-extra_shifts)
		/* Q( -corr_rshifts[ k ] + extra_shifts ) */
		denom32 = silk_max(denom32, 1)
		OpusAssert((Wght_Q15[k] << 16) < math.MaxInt32)
		/* Wght always < 0.5 in Q0 */
		temp32 = silk_DIV32(silk_LSHIFT(Wght_Q15[k], 16), denom32)
		/* Q( 15 + 16 + corr_rshifts[k] - extra_shifts ) */
		temp32 = silk_RSHIFT(temp32, 31+corr_rshifts[k]-extra_shifts-26)
		/* Q26 */

		/* Limit temp such that the below scaling never wraps around */
		WLTP_max = 0
		for i = WLTP_ptr; i < WLTP_ptr+(SilkConstants.LTP_ORDER*SilkConstants.LTP_ORDER); i++ {
			WLTP_max = silk_max(WLTP[i], WLTP_max)
		}
		lshift = silk_CLZ32(WLTP_max) - 1 - 3
		/* keep 3 bits free for vq_nearest_neighbor */
		OpusAssert(26-18+lshift >= 0)
		if 26-18+lshift < 31 {
			temp32 = silk_min_32(temp32, silk_LSHIFT(1, 26-18+lshift))
		}

		silk_scale_vector32_Q26_lshift_18(WLTP, WLTP_ptr, temp32, SilkConstants.LTP_ORDER*SilkConstants.LTP_ORDER)
		/* WLTP_ptr in Q( 18 - corr_rshifts[ k ] ) */

		w[k] = MatrixGetPtr(WLTP, WLTP_ptr, SilkConstants.LTP_ORDER/2, SilkConstants.LTP_ORDER/2, SilkConstants.LTP_ORDER)
		/* w in Q( 18 - corr_rshifts[ k ] ) */
		OpusAssert(w[k] >= 0)

		r_ptr += subfr_length
		b_Q14_ptr += SilkConstants.LTP_ORDER
		WLTP_ptr += (SilkConstants.LTP_ORDER * SilkConstants.LTP_ORDER)
	}

	maxRshifts = 0
	for k = 0; k < nb_subfr; k++ {
		maxRshifts = silk_max_int(corr_rshifts[k], maxRshifts)
	}

	/* Compute LTP coding gain */
	if LTPredCodGain_Q7 != nil {
		LPC_LTP_res_nrg = 0
		LPC_res_nrg = 0
		OpusAssert(LTP_CORRS_HEAD_ROOM >= 2)
		/* Check that no overflow will happen when adding */
		for k = 0; k < nb_subfr; k++ {
			LPC_res_nrg = silk_ADD32(LPC_res_nrg, silk_RSHIFT(silk_ADD32(silk_SMULWB(rr[k], Wght_Q15[k]), 1), 1+(maxRshifts-corr_rshifts[k])))
			/* Q( -maxRshifts ) */
			LPC_LTP_res_nrg = silk_ADD32(LPC_LTP_res_nrg, silk_RSHIFT(silk_ADD32(silk_SMULWB(nrg[k], Wght_Q15[k]), 1), 1+(maxRshifts-corr_rshifts[k])))
			/* Q( -maxRshifts ) */
		}
		LPC_LTP_res_nrg = silk_max(LPC_LTP_res_nrg, 1)
		/* avoid division by zero */

		div_Q16 = silk_DIV32_varQ(LPC_res_nrg, LPC_LTP_res_nrg, 16)
		LTPredCodGain_Q7.Val = silk_SMULBB(3, silk_lin2log(div_Q16)-(16<<7))

		OpusAssert(LTPredCodGain_Q7.Val == silk_SAT16(silk_MUL(3, silk_lin2log(div_Q16)-(16<<7))))
	}

	/* smoothing */
	/* d = sum( B, 1 ); */
	b_Q14_ptr = 0
	for k = 0; k < nb_subfr; k++ {
		d_Q14[k] = 0
		for i = b_Q14_ptr; i < b_Q14_ptr+SilkConstants.LTP_ORDER; i++ {
			d_Q14[k] += int(b_Q14[i])
		}
		b_Q14_ptr += SilkConstants.LTP_ORDER
	}

	/* m = ( w * d' ) / ( sum( w ) + 1e-3 ); */

	/* Find maximum absolute value of d_Q14 and the bits used by w in Q0 */
	max_abs_d_Q14 = 0
	max_w_bits = 0
	for k = 0; k < nb_subfr; k++ {
		max_abs_d_Q14 = silk_max_32(max_abs_d_Q14, silk_abs(d_Q14[k]))
		/* w[ k ] is in Q( 18 - corr_rshifts[ k ] ) */
		/* Find bits needed in Q( 18 - maxRshifts ) */
		max_w_bits = silk_max_32(max_w_bits, 32-silk_CLZ32(w[k])+corr_rshifts[k]-maxRshifts)
	}

	/* max_abs_d_Q14 = (5 << 15); worst case, i.e. SilkConstants.LTP_ORDER * -silk_int16_MIN */
	OpusAssert(max_abs_d_Q14 <= (5 << 15))

	/* How many bits is needed for w*d' in Q( 18 - maxRshifts ) in the worst case, of all d_Q14's being equal to max_abs_d_Q14 */
	extra_shifts = max_w_bits + 32 - silk_CLZ32(max_abs_d_Q14) - 14

	/* Subtract what we got available; bits in output var plus maxRshifts */
	extra_shifts -= (32 - 1 - 2 + maxRshifts)
	/* Keep sign bit free as well as 2 bits for accumulation */
	extra_shifts = silk_max_int(extra_shifts, 0)

	maxRshifts_wxtra = maxRshifts + extra_shifts

	temp32 = silk_RSHIFT(262, maxRshifts+extra_shifts) + 1
	/* 1e-3f in Q( 18 - (maxRshifts + extra_shifts) ) */
	wd = 0
	for k = 0; k < nb_subfr; k++ {
		/* w has at least 2 bits of headroom so no overflow should happen */
		temp32 = silk_ADD32(temp32, silk_RSHIFT(w[k], maxRshifts_wxtra-corr_rshifts[k]))
		/* Q( 18 - maxRshifts_wxtra ) */
		wd = silk_ADD32(wd, silk_LSHIFT(silk_SMULWW(silk_RSHIFT(w[k], maxRshifts_wxtra-corr_rshifts[k]), d_Q14[k]), 2))
		/* Q( 18 - maxRshifts_wxtra ) */
	}
	m_Q12 = silk_DIV32_varQ(wd, temp32, 12)

	b_Q14_ptr = 0
	for k = 0; k < nb_subfr; k++ {
		/* w[ k ] from Q( 18 - corr_rshifts[ k ] ) to Q( 16 ) */
		if 2-corr_rshifts[k] > 0 {
			temp32 = silk_RSHIFT(w[k], 2-corr_rshifts[k])
		} else {
			temp32 = silk_LSHIFT_SAT32(w[k], corr_rshifts[k]-2)
		}

		g_Q26 = silk_MUL(
			silk_DIV32(
				int(float64(TuningParameters.LTP_SMOOTHING)*float64(int64(1)<<(26))+0.5),
				silk_RSHIFT((int(float64(TuningParameters.LTP_SMOOTHING)*float64(int64(1)<<(26))+0.5)), 10)+temp32),
			silk_LSHIFT_SAT32(silk_SUB_SAT32(m_Q12, silk_RSHIFT(d_Q14[k], 2)), 4))
		/* Q16 */

		temp32 = 0
		for i = 0; i < SilkConstants.LTP_ORDER; i++ {
			delta_b_Q14[i] = int(silk_max_16(b_Q14[b_Q14_ptr+i], 1638))
			/* 1638_Q14 = 0.1_Q0 */
			temp32 += delta_b_Q14[i]
			/* Q14 */
		}
		temp32 = silk_DIV32(g_Q26, temp32)
		/* Q14 . Q12 */
		for i = 0; i < SilkConstants.LTP_ORDER; i++ {
			b_Q14[b_Q14_ptr+i] = int16(silk_LIMIT_32(int(b_Q14[b_Q14_ptr+i])+silk_SMULWB(silk_LSHIFT_SAT32(temp32, 4), delta_b_Q14[i]), -16000, 28000))
		}
		b_Q14_ptr += SilkConstants.LTP_ORDER
	}
}

func silk_fit_LTP(LTP_coefs_Q16 []int, LTP_coefs_Q14 []int16, LTP_coefs_Q14_ptr int) {
	for i := 0; i < LTP_ORDER; i++ {
		val := silk_RSHIFT_ROUND(int(LTP_coefs_Q16[i]), 2)
		if val < -32768 {
			val = -32768
		} else if val > 32767 {
			val = 32767
		}
		LTP_coefs_Q14[LTP_coefs_Q14_ptr+i] = int16(val)
	}
}
