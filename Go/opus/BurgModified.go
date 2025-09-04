package opus

import (
	"math"
)

const (
	MAX_FRAME_SIZE   = 384
	QA25             = 25
	N_BITS_HEAD_ROOM = 2
	MIN_RSHIFTS      = -16
	MAX_RSHIFTS      = 32 - QA25
)

//var SILK_CONST_FIND_LPC_COND_FAC_32 int = 42950

func BurgModified_silk_burg_modified(res_nrg *BoxedValueInt, res_nrg_Q *BoxedValueInt, A_Q16 []int, x []int16, x_ptr int, minInvGain_Q30 int, subfr_length int, nb_subfr int, D int) {

	var k, n, s, lz, rshifts, reached_max_gain int
	var C0, num, nrg, rc_Q31, invGain_Q30, Atmp_QA, Atmp1, tmp1, tmp2, x1, x2 int
	var x_offset int
	C_first_row := make([]int, SilkConstants.SILK_MAX_ORDER_LPC)
	C_last_row := make([]int, SilkConstants.SILK_MAX_ORDER_LPC)

	Af_QA := make([]int, SilkConstants.SILK_MAX_ORDER_LPC)

	CAf := make([]int, SilkConstants.SILK_MAX_ORDER_LPC+1)

	CAb := make([]int, SilkConstants.SILK_MAX_ORDER_LPC+1)

	xcorr := make([]int, SilkConstants.SILK_MAX_ORDER_LPC)
	var C0_64 int64

	OpusAssert(subfr_length*nb_subfr <= MAX_FRAME_SIZE)

	/* Compute autocorrelations, added over subframes */
	C0_64 = silk_inner_prod16_aligned_64(x, x_ptr, x, x_ptr, subfr_length*nb_subfr)
	lz = silk_CLZ64(C0_64)
	rshifts = 32 + 1 + N_BITS_HEAD_ROOM - lz
	if rshifts > MAX_RSHIFTS {
		rshifts = MAX_RSHIFTS
	}
	if rshifts < MIN_RSHIFTS {
		rshifts = MIN_RSHIFTS
	}

	if rshifts > 0 {
		C0 = int(silk_RSHIFT64(C0_64, rshifts))
	} else {
		C0 = silk_LSHIFT32(int(C0_64), -rshifts)
	}

	CAb[0] = C0 + silk_SMMUL(int(float64(TuningParameters.FIND_LPC_COND_FAC)*float64(int64(1)<<(32))+0.5), C0) + 1

	CAf[0] = CAb[0]
	/* Q(-rshifts) */
	MemSetLen(C_first_row, 0, SilkConstants.SILK_MAX_ORDER_LPC)

	if rshifts > 0 {
		for s = 0; s < nb_subfr; s++ {
			x_offset = x_ptr + s*subfr_length
			for n = 1; n < D+1; n++ {
				C_first_row[n-1] += int(silk_RSHIFT64(silk_inner_prod16_aligned_64(x, x_offset, x, x_offset+n, subfr_length-n), rshifts))
			}
		}
	} else {
		for s = 0; s < nb_subfr; s++ {
			var i int
			var d int
			x_offset = x_ptr + s*subfr_length
			pitch_xcorr1(x, x_offset, x, x_offset+1, xcorr, subfr_length-D, D)
			for n = 1; n < D+1; n++ {
				d = 0
				for i = n + subfr_length - D; i < subfr_length; i++ {
					d = MAC16_16Int(d, x[x_offset+i], x[x_offset+i-n])
				}
				xcorr[n-1] += d
			}
			for n = 1; n < D+1; n++ {
				C_first_row[n-1] += silk_LSHIFT32(xcorr[n-1], -rshifts)
			}
		}
	}
	//System.arraycopy(C_first_row, 0, C_last_row, 0, SilkConstants.SILK_MAX_ORDER_LPC)
	copy(C_last_row, C_first_row[:SilkConstants.SILK_MAX_ORDER_LPC])
	/* Initialize */
	CAb[0] = C0 + silk_SMMUL(int(float64(TuningParameters.FIND_LPC_COND_FAC)*float64(int64(1)<<(32))+0.5), C0) + 1
	CAf[0] = CAb[0]
	/* Q(-rshifts) */

	invGain_Q30 = int(int32(1) << 30)
	reached_max_gain = 0
	for n = 0; n < D; n++ {
		/* Update first row of correlation matrix (without first element) */
		/* Update last row of correlation matrix (without last element, stored in reversed order) */
		/* Update C * Af */
		/* Update C * flipud(Af) (stored in reversed order) */
		if rshifts > -2 {
			for s = 0; s < nb_subfr; s++ {
				x_offset = x_ptr + s*subfr_length
				x1 = -silk_LSHIFT32(int(x[x_offset+n]), 16-rshifts)
				/* Q(16-rshifts) */
				x2 = -silk_LSHIFT32(int(x[x_offset+subfr_length-n-1]), 16-rshifts)
				/* Q(16-rshifts) */
				tmp1 = silk_LSHIFT32(int(x[x_offset+n]), QA25-16)
				/* Q(QA-16) */
				tmp2 = silk_LSHIFT32(int(x[x_offset+subfr_length-n-1]), QA25-16)
				/* Q(QA-16) */
				for k = 0; k < n; k++ {
					C_first_row[k] = silk_SMLAWB(C_first_row[k], x1, int(x[x_offset+n-k-1]))
					/* Q( -rshifts ) */
					C_last_row[k] = silk_SMLAWB(C_last_row[k], x2, int(x[x_offset+subfr_length-n+k]))
					/* Q( -rshifts ) */
					Atmp_QA = Af_QA[k]
					tmp1 = silk_SMLAWB(tmp1, Atmp_QA, int(x[x_offset+n-k-1]))
					/* Q(QA-16) */
					tmp2 = silk_SMLAWB(tmp2, Atmp_QA, int(x[x_offset+subfr_length-n+k]))
					/* Q(QA-16) */
				}
				tmp1 = silk_LSHIFT32(-tmp1, 32-QA25-rshifts)
				/* Q(16-rshifts) */
				tmp2 = silk_LSHIFT32(-tmp2, 32-QA25-rshifts)
				/* Q(16-rshifts) */
				for k = 0; k <= n; k++ {
					CAf[k] = silk_SMLAWB(CAf[k], tmp1, int(x[x_offset+n-k]))
					/* Q( -rshift ) */
					CAb[k] = silk_SMLAWB(CAb[k], tmp2, int(x[x_offset+subfr_length-n+k-1]))
					/* Q( -rshift ) */
				}
			}
		} else {
			for s = 0; s < nb_subfr; s++ {
				x_offset = x_ptr + s*subfr_length
				x1 = -silk_LSHIFT32(int(x[x_offset+n]), -rshifts)
				/* Q( -rshifts ) */
				x2 = -silk_LSHIFT32(int(x[x_offset+subfr_length-n-1]), -rshifts)
				/* Q( -rshifts ) */
				tmp1 = silk_LSHIFT32(int(x[x_offset+n]), 17)
				/* Q17 */
				tmp2 = silk_LSHIFT32(int(x[x_offset+subfr_length-n-1]), 17)
				/* Q17 */
				for k = 0; k < n; k++ {
					C_first_row[k] = silk_MLA(C_first_row[k], x1, int(x[x_offset+n-k-1]))
					/* Q( -rshifts ) */
					C_last_row[k] = silk_MLA(C_last_row[k], x2, int(x[x_offset+subfr_length-n+k]))
					/* Q( -rshifts ) */
					Atmp1 = silk_RSHIFT_ROUND(Af_QA[k], QA25-17)
					/* Q17 */
					tmp1 = silk_MLA(tmp1, int(x[x_offset+n-k-1]), Atmp1)
					/* Q17 */
					tmp2 = silk_MLA(tmp2, int(x[x_offset+subfr_length-n+k]), Atmp1)
					/* Q17 */
				}
				tmp1 = -tmp1
				/* Q17 */
				tmp2 = -tmp2
				/* Q17 */
				for k = 0; k <= n; k++ {
					CAf[k] = silk_SMLAWW(CAf[k], tmp1,
						silk_LSHIFT32(int(x[x_offset+n-k]), -rshifts-1))
					/* Q( -rshift ) */
					CAb[k] = silk_SMLAWW(CAb[k], tmp2,
						silk_LSHIFT32(int(x[x_offset+subfr_length-n+k-1]), -rshifts-1))
					/* Q( -rshift ) */
				}
			}
		}

		/* Calculate nominator and denominator for the next order reflection (parcor) coefficient */
		tmp1 = C_first_row[n]
		/* Q( -rshifts ) */
		tmp2 = C_last_row[n]
		/* Q( -rshifts ) */
		num = 0
		/* Q( -rshifts ) */
		nrg = silk_ADD32(CAb[0], CAf[0])
		/* Q( 1-rshifts ) */
		for k = 0; k < n; k++ {
			Atmp_QA = Af_QA[k]
			lz = silk_CLZ32(silk_abs(Atmp_QA)) - 1
			lz = silk_min(32-QA25, lz)
			Atmp1 = silk_LSHIFT32(Atmp_QA, lz)
			/* Q( QA + lz ) */

			tmp1 = silk_ADD_LSHIFT32(tmp1, silk_SMMUL(C_last_row[n-k-1], Atmp1), 32-QA25-lz)
			/* Q( -rshifts ) */
			tmp2 = silk_ADD_LSHIFT32(tmp2, silk_SMMUL(C_first_row[n-k-1], Atmp1), 32-QA25-lz)
			/* Q( -rshifts ) */
			num = silk_ADD_LSHIFT32(num, silk_SMMUL(CAb[n-k], Atmp1), 32-QA25-lz)
			/* Q( -rshifts ) */
			nrg = silk_ADD_LSHIFT32(nrg, silk_SMMUL(silk_ADD32(CAb[k+1], CAf[k+1]),
				Atmp1), 32-QA25-lz)
			/* Q( 1-rshifts ) */
		}
		CAf[n+1] = tmp1
		/* Q( -rshifts ) */
		CAb[n+1] = tmp2
		/* Q( -rshifts ) */
		num = silk_ADD32(num, tmp2)
		/* Q( -rshifts ) */
		num = silk_LSHIFT32(-num, 1)
		/* Q( 1-rshifts ) */

		/* Calculate the next order reflection (parcor) coefficient */
		if silk_abs(num) < nrg {
			rc_Q31 = silk_DIV32_varQ(num, nrg, 31)
		} else {
			rc_Q31 = math.MinInt32
			if num > 0 {
				rc_Q31 = math.MaxInt32
			}

		}
		/* Update inverse prediction gain */
		tmp1 = int(int32(1)<<30) - silk_SMMUL(rc_Q31, rc_Q31)

		tmp1 = silk_LSHIFT(silk_SMMUL(invGain_Q30, tmp1), 2)

		if tmp1 <= minInvGain_Q30 {
			/* Max prediction gain exceeded; set reflection coefficient such that max prediction gain is exactly hit */
			tmp2 = int(int32(1)<<30) - silk_DIV32_varQ(minInvGain_Q30, invGain_Q30, 30)
			/* Q30 */
			rc_Q31 = silk_SQRT_APPROX(tmp2)
			/* Q15 */
			/* Newton-Raphson iteration */
			rc_Q31 = silk_RSHIFT32(rc_Q31+silk_DIV32(tmp2, rc_Q31), 1)
			/* Q15 */
			rc_Q31 = silk_LSHIFT32(rc_Q31, 16)
			/* Q31 */
			if num < 0 {
				/* Ensure adjusted reflection coefficients has the original sign */
				rc_Q31 = -rc_Q31
			}
			invGain_Q30 = minInvGain_Q30
			reached_max_gain = 1
		} else {
			invGain_Q30 = tmp1
		}

		/* Update the AR coefficients */
		for k = 0; k < (n+1)>>1; k++ {
			tmp1 = Af_QA[k]
			/* QA */
			tmp2 = Af_QA[n-k-1]
			/* QA */
			Af_QA[k] = silk_ADD_LSHIFT32(tmp1, silk_SMMUL(tmp2, rc_Q31), 1)
			/* QA */
			Af_QA[n-k-1] = silk_ADD_LSHIFT32(tmp2, silk_SMMUL(tmp1, rc_Q31), 1)
			/* QA */
		}
		Af_QA[n] = silk_RSHIFT32(rc_Q31, 31-QA25)
		/* QA */

		if reached_max_gain != 0 {
			/* Reached max prediction gain; set remaining coefficients to zero and exit loop */
			for k = n + 1; k < D; k++ {
				Af_QA[k] = 0
			}
			break
		}

		/* Update C * Af and C * Ab */
		for k = 0; k <= n+1; k++ {
			tmp1 = CAf[k]
			/* Q( -rshifts ) */
			tmp2 = CAb[n-k+1]
			/* Q( -rshifts ) */
			CAf[k] = silk_ADD_LSHIFT32(tmp1, silk_SMMUL(tmp2, rc_Q31), 1)
			/* Q( -rshifts ) */
			CAb[n-k+1] = silk_ADD_LSHIFT32(tmp2, silk_SMMUL(tmp1, rc_Q31), 1)
			/* Q( -rshifts ) */
		}
	}

	if reached_max_gain != 0 {
		for k = 0; k < D; k++ {
			/* Scale coefficients */
			A_Q16[k] = -silk_RSHIFT_ROUND(Af_QA[k], QA25-16)
		}
		/* Subtract energy of preceding samples from C0 */
		if rshifts > 0 {
			for s = 0; s < nb_subfr; s++ {
				x_offset = x_ptr + s*subfr_length
				C0 -= int(silk_RSHIFT64(silk_inner_prod16_aligned_64(x, x_offset, x, x_offset, D), rshifts))
			}
		} else {
			for s = 0; s < nb_subfr; s++ {
				x_offset = x_ptr + s*subfr_length
				C0 -= silk_LSHIFT32(silk_inner_prod_self(x, x_offset, D), -rshifts)
			}
		}
		/* Approximate residual energy */
		res_nrg.Val = silk_LSHIFT(silk_SMMUL(invGain_Q30, C0), 2)
		res_nrg_Q.Val = 0 - rshifts
	} else {
		/* Return residual energy */
		nrg = CAf[0]
		/* Q( -rshifts ) */
		tmp1 = int(int32(1) << 16)
		/* Q16 */
		for k = 0; k < D; k++ {
			Atmp1 = silk_RSHIFT_ROUND(Af_QA[k], QA25-16)
			/* Q16 */
			nrg = silk_SMLAWW(nrg, CAf[k+1], Atmp1)
			/* Q( -rshifts ) */
			tmp1 = silk_SMLAWW(tmp1, Atmp1, Atmp1)
			/* Q16 */
			A_Q16[k] = -Atmp1
		}
		res_nrg.Val = silk_SMLAWW(nrg, silk_SMMUL(int(float64(TuningParameters.FIND_LPC_COND_FAC)*float64(int64(1)<<(32))+0.5), C0), -tmp1)
	}

}
