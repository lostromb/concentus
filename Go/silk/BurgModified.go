package silk

import (
	"math"

	"github.com/dosgo/concentus/go/comm"
	"github.com/dosgo/concentus/go/comm/arrayUtil"
)

const (
	MAX_FRAME_SIZE   = 384
	QA25             = 25
	N_BITS_HEAD_ROOM = 2
	MIN_RSHIFTS      = -16
	MAX_RSHIFTS      = 32 - QA25
)

//var SILK_CONST_FIND_LPC_COND_FAC_32 int = 42950

func BurgModified_silk_burg_modified(res_nrg *comm.BoxedValueInt, res_nrg_Q *comm.BoxedValueInt, A_Q16 []int, x []int16, x_ptr int, minInvGain_Q30 int, subfr_length int, nb_subfr int, D int) {

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

	inlines.OpusAssert(subfr_length*nb_subfr <= MAX_FRAME_SIZE)

	/* Compute autocorrelations, added over subframes */
	C0_64 = inlines.Silk_inner_prod16_aligned_64(x, x_ptr, x, x_ptr, subfr_length*nb_subfr)
	lz = inlines.Silk_CLZ64(C0_64)
	rshifts = 32 + 1 + N_BITS_HEAD_ROOM - lz
	if rshifts > MAX_RSHIFTS {
		rshifts = MAX_RSHIFTS
	}
	if rshifts < MIN_RSHIFTS {
		rshifts = MIN_RSHIFTS
	}

	if rshifts > 0 {
		C0 = int(inlines.Silk_RSHIFT64(C0_64, rshifts))
	} else {
		C0 = inlines.Silk_LSHIFT32(int(C0_64), -rshifts)
	}

	CAb[0] = C0 + inlines.Silk_SMMUL(int(float64(TuningParameters.FIND_LPC_COND_FAC)*float64(int64(1)<<(32))+0.5), C0) + 1

	CAf[0] = CAb[0]
	/* Q(-rshifts) */
	arrayUtil.MemSetLen(C_first_row, 0, SilkConstants.SILK_MAX_ORDER_LPC)

	if rshifts > 0 {
		for s = 0; s < nb_subfr; s++ {
			x_offset = x_ptr + s*subfr_length
			for n = 1; n < D+1; n++ {
				C_first_row[n-1] += int(inlines.Silk_RSHIFT64(inlines.Silk_inner_prod16_aligned_64(x, x_offset, x, x_offset+n, subfr_length-n), rshifts))
			}
		}
	} else {
		for s = 0; s < nb_subfr; s++ {
			var i int
			var d int
			x_offset = x_ptr + s*subfr_length
			comm.Pitch_xcorr1(x, x_offset, x, x_offset+1, xcorr, subfr_length-D, D)
			for n = 1; n < D+1; n++ {
				d = 0
				for i = n + subfr_length - D; i < subfr_length; i++ {
					d = inlines.MAC16_16Int(d, x[x_offset+i], x[x_offset+i-n])
				}
				xcorr[n-1] += d
			}
			for n = 1; n < D+1; n++ {
				C_first_row[n-1] += inlines.Silk_LSHIFT32(xcorr[n-1], -rshifts)
			}
		}
	}
	//System.arraycopy(C_first_row, 0, C_last_row, 0, SilkConstants.SILK_MAX_ORDER_LPC)
	copy(C_last_row, C_first_row[:SilkConstants.SILK_MAX_ORDER_LPC])
	/* Initialize */
	CAb[0] = C0 + inlines.Silk_SMMUL(int(float64(TuningParameters.FIND_LPC_COND_FAC)*float64(int64(1)<<(32))+0.5), C0) + 1
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
				x1 = -inlines.Silk_LSHIFT32(int(x[x_offset+n]), 16-rshifts)
				/* Q(16-rshifts) */
				x2 = -inlines.Silk_LSHIFT32(int(x[x_offset+subfr_length-n-1]), 16-rshifts)
				/* Q(16-rshifts) */
				tmp1 = inlines.Silk_LSHIFT32(int(x[x_offset+n]), QA25-16)
				/* Q(QA-16) */
				tmp2 = inlines.Silk_LSHIFT32(int(x[x_offset+subfr_length-n-1]), QA25-16)
				/* Q(QA-16) */
				for k = 0; k < n; k++ {
					C_first_row[k] = inlines.Silk_SMLAWB(C_first_row[k], x1, int(x[x_offset+n-k-1]))
					/* Q( -rshifts ) */
					C_last_row[k] = inlines.Silk_SMLAWB(C_last_row[k], x2, int(x[x_offset+subfr_length-n+k]))
					/* Q( -rshifts ) */
					Atmp_QA = Af_QA[k]
					tmp1 = inlines.Silk_SMLAWB(tmp1, Atmp_QA, int(x[x_offset+n-k-1]))
					/* Q(QA-16) */
					tmp2 = inlines.Silk_SMLAWB(tmp2, Atmp_QA, int(x[x_offset+subfr_length-n+k]))
					/* Q(QA-16) */
				}
				tmp1 = inlines.Silk_LSHIFT32(-tmp1, 32-QA25-rshifts)
				/* Q(16-rshifts) */
				tmp2 = inlines.Silk_LSHIFT32(-tmp2, 32-QA25-rshifts)
				/* Q(16-rshifts) */
				for k = 0; k <= n; k++ {
					CAf[k] = inlines.Silk_SMLAWB(CAf[k], tmp1, int(x[x_offset+n-k]))
					/* Q( -rshift ) */
					CAb[k] = inlines.Silk_SMLAWB(CAb[k], tmp2, int(x[x_offset+subfr_length-n+k-1]))
					/* Q( -rshift ) */
				}
			}
		} else {
			for s = 0; s < nb_subfr; s++ {
				x_offset = x_ptr + s*subfr_length
				x1 = -inlines.Silk_LSHIFT32(int(x[x_offset+n]), -rshifts)
				/* Q( -rshifts ) */
				x2 = -inlines.Silk_LSHIFT32(int(x[x_offset+subfr_length-n-1]), -rshifts)
				/* Q( -rshifts ) */
				tmp1 = inlines.Silk_LSHIFT32(int(x[x_offset+n]), 17)
				/* Q17 */
				tmp2 = inlines.Silk_LSHIFT32(int(x[x_offset+subfr_length-n-1]), 17)
				/* Q17 */
				for k = 0; k < n; k++ {
					C_first_row[k] = inlines.Silk_MLA(C_first_row[k], x1, int(x[x_offset+n-k-1]))
					/* Q( -rshifts ) */
					C_last_row[k] = inlines.Silk_MLA(C_last_row[k], x2, int(x[x_offset+subfr_length-n+k]))
					/* Q( -rshifts ) */
					Atmp1 = inlines.Silk_RSHIFT_ROUND(Af_QA[k], QA25-17)
					/* Q17 */
					tmp1 = inlines.Silk_MLA(tmp1, int(x[x_offset+n-k-1]), Atmp1)
					/* Q17 */
					tmp2 = inlines.Silk_MLA(tmp2, int(x[x_offset+subfr_length-n+k]), Atmp1)
					/* Q17 */
				}
				tmp1 = -tmp1
				/* Q17 */
				tmp2 = -tmp2
				/* Q17 */
				for k = 0; k <= n; k++ {
					CAf[k] = inlines.Silk_SMLAWW(CAf[k], tmp1,
						inlines.Silk_LSHIFT32(int(x[x_offset+n-k]), -rshifts-1))
					/* Q( -rshift ) */
					CAb[k] = inlines.Silk_SMLAWW(CAb[k], tmp2,
						inlines.Silk_LSHIFT32(int(x[x_offset+subfr_length-n+k-1]), -rshifts-1))
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
		nrg = inlines.Silk_ADD32(CAb[0], CAf[0])
		/* Q( 1-rshifts ) */
		for k = 0; k < n; k++ {
			Atmp_QA = Af_QA[k]
			lz = inlines.Silk_CLZ32(inlines.Silk_abs(Atmp_QA)) - 1
			lz = inlines.Silk_min(32-QA25, lz)
			Atmp1 = inlines.Silk_LSHIFT32(Atmp_QA, lz)
			/* Q( QA + lz ) */

			tmp1 = inlines.Silk_ADD_LSHIFT32(tmp1, inlines.Silk_SMMUL(C_last_row[n-k-1], Atmp1), 32-QA25-lz)
			/* Q( -rshifts ) */
			tmp2 = inlines.Silk_ADD_LSHIFT32(tmp2, inlines.Silk_SMMUL(C_first_row[n-k-1], Atmp1), 32-QA25-lz)
			/* Q( -rshifts ) */
			num = inlines.Silk_ADD_LSHIFT32(num, inlines.Silk_SMMUL(CAb[n-k], Atmp1), 32-QA25-lz)
			/* Q( -rshifts ) */
			nrg = inlines.Silk_ADD_LSHIFT32(nrg, inlines.Silk_SMMUL(inlines.Silk_ADD32(CAb[k+1], CAf[k+1]),
				Atmp1), 32-QA25-lz)
			/* Q( 1-rshifts ) */
		}
		CAf[n+1] = tmp1
		/* Q( -rshifts ) */
		CAb[n+1] = tmp2
		/* Q( -rshifts ) */
		num = inlines.Silk_ADD32(num, tmp2)
		/* Q( -rshifts ) */
		num = inlines.Silk_LSHIFT32(-num, 1)
		/* Q( 1-rshifts ) */

		/* Calculate the next order reflection (parcor) coefficient */
		if inlines.Silk_abs(num) < nrg {
			rc_Q31 = inlines.Silk_DIV32_varQ(num, nrg, 31)
		} else {
			rc_Q31 = math.MinInt32
			if num > 0 {
				rc_Q31 = math.MaxInt32
			}

		}
		/* Update inverse prediction gain */
		tmp1 = int(int32(1)<<30) - inlines.Silk_SMMUL(rc_Q31, rc_Q31)

		tmp1 = inlines.Silk_LSHIFT(inlines.Silk_SMMUL(invGain_Q30, tmp1), 2)

		if tmp1 <= minInvGain_Q30 {
			/* Max prediction gain exceeded; set reflection coefficient such that max prediction gain is exactly hit */
			tmp2 = int(int32(1)<<30) - inlines.Silk_DIV32_varQ(minInvGain_Q30, invGain_Q30, 30)
			/* Q30 */
			rc_Q31 = inlines.Silk_SQRT_APPROX(tmp2)
			/* Q15 */
			/* Newton-Raphson iteration */
			rc_Q31 = inlines.Silk_RSHIFT32(rc_Q31+inlines.Silk_DIV32(tmp2, rc_Q31), 1)
			/* Q15 */
			rc_Q31 = inlines.Silk_LSHIFT32(rc_Q31, 16)
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
			Af_QA[k] = inlines.Silk_ADD_LSHIFT32(tmp1, inlines.Silk_SMMUL(tmp2, rc_Q31), 1)
			/* QA */
			Af_QA[n-k-1] = inlines.Silk_ADD_LSHIFT32(tmp2, inlines.Silk_SMMUL(tmp1, rc_Q31), 1)
			/* QA */
		}
		Af_QA[n] = inlines.Silk_RSHIFT32(rc_Q31, 31-QA25)
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
			CAf[k] = inlines.Silk_ADD_LSHIFT32(tmp1, inlines.Silk_SMMUL(tmp2, rc_Q31), 1)
			/* Q( -rshifts ) */
			CAb[n-k+1] = inlines.Silk_ADD_LSHIFT32(tmp2, inlines.Silk_SMMUL(tmp1, rc_Q31), 1)
			/* Q( -rshifts ) */
		}
	}

	if reached_max_gain != 0 {
		for k = 0; k < D; k++ {
			/* Scale coefficients */
			A_Q16[k] = -inlines.Silk_RSHIFT_ROUND(Af_QA[k], QA25-16)
		}
		/* Subtract energy of preceding samples from C0 */
		if rshifts > 0 {
			for s = 0; s < nb_subfr; s++ {
				x_offset = x_ptr + s*subfr_length
				C0 -= int(inlines.Silk_RSHIFT64(inlines.Silk_inner_prod16_aligned_64(x, x_offset, x, x_offset, D), rshifts))
			}
		} else {
			for s = 0; s < nb_subfr; s++ {
				x_offset = x_ptr + s*subfr_length
				C0 -= inlines.Silk_LSHIFT32(inlines.Silk_inner_prod_self(x, x_offset, D), -rshifts)
			}
		}
		/* Approximate residual energy */
		res_nrg.Val = inlines.Silk_LSHIFT(inlines.Silk_SMMUL(invGain_Q30, C0), 2)
		res_nrg_Q.Val = 0 - rshifts
	} else {
		/* Return residual energy */
		nrg = CAf[0]
		/* Q( -rshifts ) */
		tmp1 = int(int32(1) << 16)
		/* Q16 */
		for k = 0; k < D; k++ {
			Atmp1 = inlines.Silk_RSHIFT_ROUND(Af_QA[k], QA25-16)
			/* Q16 */
			nrg = inlines.Silk_SMLAWW(nrg, CAf[k+1], Atmp1)
			/* Q( -rshifts ) */
			tmp1 = inlines.Silk_SMLAWW(tmp1, Atmp1, Atmp1)
			/* Q16 */
			A_Q16[k] = -Atmp1
		}
		res_nrg.Val = inlines.Silk_SMLAWW(nrg, inlines.Silk_SMMUL(int(float64(TuningParameters.FIND_LPC_COND_FAC)*float64(int64(1)<<(32))+0.5), C0), -tmp1)
	}

}
