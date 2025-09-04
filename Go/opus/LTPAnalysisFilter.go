package opus

func silk_LTP_analysis_filter(
	LTP_res []int16,
	x []int16,
	x_ptr int,
	LTPCoef_Q14 []int16,
	pitchL []int,
	invGains_Q16 []int,
	subfr_length int,
	nb_subfr int,
	pre_length int) {
	var x_ptr2, x_lag_ptr int
	Btmp_Q14 := make([]int16, SilkConstants.LTP_ORDER)
	var LTP_res_ptr int
	var k, i int
	var LTP_est int

	x_ptr2 = x_ptr
	LTP_res_ptr = 0
	for k = 0; k < nb_subfr; k++ {
		x_lag_ptr = x_ptr2 - pitchL[k]

		Btmp_Q14[0] = LTPCoef_Q14[k*SilkConstants.LTP_ORDER]
		Btmp_Q14[1] = LTPCoef_Q14[k*SilkConstants.LTP_ORDER+1]
		Btmp_Q14[2] = LTPCoef_Q14[k*SilkConstants.LTP_ORDER+2]
		Btmp_Q14[3] = LTPCoef_Q14[k*SilkConstants.LTP_ORDER+3]
		Btmp_Q14[4] = LTPCoef_Q14[k*SilkConstants.LTP_ORDER+4]

		/* LTP analysis FIR filter */
		for i = 0; i < subfr_length+pre_length; i++ {
			var LTP_res_ptri = LTP_res_ptr + i
			LTP_res[LTP_res_ptri] = x[x_ptr2+i]

			/* Long-term prediction */
			LTP_est = silk_SMULBB(int(x[x_lag_ptr+SilkConstants.LTP_ORDER/2]), int(Btmp_Q14[0]))
			LTP_est = int(silk_SMLABB_ovflw(int32(LTP_est), int32(x[x_lag_ptr+1]), int32(Btmp_Q14[1])))
			LTP_est = int(silk_SMLABB_ovflw(int32(LTP_est), int32(x[x_lag_ptr]), int32(Btmp_Q14[2])))
			LTP_est = int(silk_SMLABB_ovflw(int32(LTP_est), int32(x[x_lag_ptr-1]), int32(Btmp_Q14[3])))
			LTP_est = int(silk_SMLABB_ovflw(int32(LTP_est), int32(x[x_lag_ptr-2]), int32(Btmp_Q14[4])))

			LTP_est = silk_RSHIFT_ROUND(LTP_est, 14)
			/* round and . Q0*/

			/* Subtract long-term prediction */
			LTP_res[LTP_res_ptri] = int16(silk_SAT16(int(x[x_ptr2+i]) - LTP_est))

			/* Scale residual */
			LTP_res[LTP_res_ptri] = int16(silk_SMULWB(invGains_Q16[k], int(LTP_res[LTP_res_ptri])))

			x_lag_ptr++
		}

		/* Update pointers */
		LTP_res_ptr += subfr_length + pre_length
		x_ptr2 += subfr_length
	}
}
