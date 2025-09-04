package opus

import (
	"math"
)

func MemSetInt(array []int, value, length int) {
	for i := 0; i < length; i++ {
		array[i] = value
	}
}

const SILK_CONST_0_99_Q15 = 32440
const SILK_CONST_0_99_Q16 = 64881

func silk_schur(rc_Q15 []int16, c []int, order int) int {
	k, n, lz := 0, 0, 0
	C := InitTwoDimensionalArrayInt(SILK_MAX_ORDER_LPC+1, 2)
	Ctmp1, Ctmp2, rc_tmp_Q15 := 0, 0, 0

	if !(order == 6 || order == 8 || order == 10 || order == 12 || order == 14 || order == 16) {
		panic("OpusAssert failed")
	}

	lz = silk_CLZ32(c[0])

	if lz < 2 {
		for k = 0; k < order+1; k++ {
			C[k][0] = silk_RSHIFT(c[k], 1)
			C[k][1] = silk_RSHIFT(c[k], 1)
		}
	} else if lz > 2 {
		lz -= 2
		for k = 0; k < order+1; k++ {
			C[k][0] = silk_LSHIFT(c[k], lz)
			C[k][1] = silk_LSHIFT(c[k], lz)
		}
	} else {
		for k = 0; k < order+1; k++ {
			C[k][0] = c[k]
			C[k][1] = c[k]
		}
	}

	for k = 0; k < order; k++ {
		if silk_abs_int(C[k+1][0]) >= C[0][1] {
			if C[k+1][0] > 0 {
				rc_Q15[k] = -SILK_CONST_0_99_Q15
			} else {
				rc_Q15[k] = SILK_CONST_0_99_Q15
			}
			k++
			break
		}

		rc_tmp_Q15 = -silk_DIV32_16(C[k+1][0], silk_max_32(silk_RSHIFT(C[0][1], 15), 1))
		rc_tmp_Q15 = silk_SAT16(rc_tmp_Q15)
		rc_Q15[k] = int16(rc_tmp_Q15)

		for n = 0; n < order-k; n++ {
			Ctmp1 = C[n+k+1][0]
			Ctmp2 = C[n][1]
			C[n+k+1][0] = silk_SMLAWB(Ctmp1, silk_LSHIFT(Ctmp2, 1), rc_tmp_Q15)
			C[n][1] = silk_SMLAWB(Ctmp2, silk_LSHIFT(Ctmp1, 1), rc_tmp_Q15)
		}
	}

	for ; k < order; k++ {
		rc_Q15[k] = 0
	}

	return silk_max_32(1, C[0][1])
}

func silk_schur64(rc_Q16 []int, c []int, order int) int {
	var k, n int
	C := InitTwoDimensionalArrayInt(SilkConstants.SILK_MAX_ORDER_LPC+1, 2)
	var Ctmp1_Q30, Ctmp2_Q30, rc_tmp_Q31 int

	OpusAssert(order == 6 || order == 8 || order == 10 || order == 12 || order == 14 || order == 16)

	/* Check for invalid input */
	if c[0] <= 0 {
		MemSetLen(rc_Q16, 0, order)
		return 0
	}

	for k = 0; k < order+1; k++ {
		C[k][0] = c[k]
		C[k][1] = c[k]
	}

	for k = 0; k < order; k++ {
		/* Check that we won't be getting an unstable rc, otherwise stop here. */
		if silk_abs_int32(C[k+1][0]) >= C[0][1] {
			if C[k+1][0] > 0 {
				rc_Q16[k] = -int(math.Trunc((.99)*float64(1<<(16)) + 0.5))
			} else {
				rc_Q16[k] = int(math.Trunc((.99)*float64(1<<(16)) + 0.5))

			}
			k++
			break
		}

		/* Get reflection coefficient: divide two Q30 values and get result in Q31 */
		rc_tmp_Q31 = silk_DIV32_varQ(-C[k+1][0], C[0][1], 31)
		/* Save the output */
		rc_Q16[k] = silk_RSHIFT_ROUND(rc_tmp_Q31, 15)

		/* Update correlations */
		for n = 0; n < order-k; n++ {
			Ctmp1_Q30 = C[n+k+1][0]
			Ctmp2_Q30 = C[n][1]

			/* Multiply and add the highest int32 */
			C[n+k+1][0] = Ctmp1_Q30 + silk_SMMUL(silk_LSHIFT(Ctmp2_Q30, 1), rc_tmp_Q31)
			C[n][1] = Ctmp2_Q30 + silk_SMMUL(silk_LSHIFT(Ctmp1_Q30, 1), rc_tmp_Q31)
		}
	}
	for ; k < order; k++ {
		rc_Q16[k] = 0
	}

	return silk_max_32(1, C[0][1])
}
