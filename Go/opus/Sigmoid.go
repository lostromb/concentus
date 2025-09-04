package opus

var sigm_LUT_slope_Q10 = [6]int{237, 153, 73, 30, 12, 7}
var sigm_LUT_pos_Q15 = [6]int{16384, 23955, 28861, 31213, 32178, 32548}
var sigm_LUT_neg_Q15 = [6]int{16384, 8812, 3906, 1554, 589, 219}

func silk_sigm_Q15(in_Q5 int) int {
	var ind int

	if in_Q5 < 0 {
		/* Negative input */
		in_Q5 = -in_Q5
		if in_Q5 >= 6*32 {
			return 0
			/* Clip */
		} else {
			/* Linear interpolation of look up table */
			ind = silk_RSHIFT(in_Q5, 5)
			return (sigm_LUT_neg_Q15[ind] - silk_SMULBB(sigm_LUT_slope_Q10[ind], in_Q5&0x1F))
		}
	} else /* Positive input */ if in_Q5 >= 6*32 {
		return 32767
		/* clip */
	} else {
		/* Linear interpolation of look up table */
		ind = silk_RSHIFT(in_Q5, 5)
		return (sigm_LUT_pos_Q15[ind] + silk_SMULBB(sigm_LUT_slope_Q10[ind], in_Q5&0x1F))
	}
}
