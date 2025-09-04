package opus

/* Step up function, converts reflection coefficients to prediction coefficients */
func silk_k2a(
	A_Q24 []int, /* O    Prediction coefficients [order] Q24                         */
	rc_Q15 []int16, /* I    Reflection coefficients [order] Q15                         */
	order int, /* I    Prediction order                                            */
) {
	var k, n int
	Atmp := make([]int, SilkConstants.SILK_MAX_ORDER_LPC)

	for k = 0; k < order; k++ {
		for n = 0; n < k; n++ {
			Atmp[n] = A_Q24[n]
		}
		for n = 0; n < k; n++ {
			A_Q24[n] = silk_SMLAWB(A_Q24[n], silk_LSHIFT(Atmp[k-n-1], 1), int(rc_Q15[k]))
		}
		A_Q24[k] = 0 - silk_LSHIFT(int(rc_Q15[k]), 9)
	}
}

/* Step up function, converts reflection coefficients to prediction coefficients */
func silk_k2a_Q16(
	A_Q24 []int, /* O    Prediction coefficients [order] Q24                         */
	rc_Q16 []int, /* I    Reflection coefficients [order] Q16                         */
	order int, /* I    Prediction order                                            */
) {
	var k, n int
	Atmp := make([]int, SilkConstants.SILK_MAX_ORDER_LPC)
	for k = 0; k < order; k++ {
		for n = 0; n < k; n++ {
			Atmp[n] = A_Q24[n]
		}
		for n = 0; n < k; n++ {
			A_Q24[n] = silk_SMLAWW(A_Q24[n], Atmp[k-n-1], rc_Q16[k])
		}

		A_Q24[k] = 0 - (silk_LSHIFT((rc_Q16[k]), (8)))
	}
}
