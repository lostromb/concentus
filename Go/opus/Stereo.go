package opus

import (
	"math"
)

func silk_stereo_decode_pred(
	psRangeDec *EntropyCoder,
	pred_Q13 []int) {
	var n int
	ix := InitTwoDimensionalArrayInt(2, 3)
	var low_Q13, step_Q13 int

	n = psRangeDec.dec_icdf(SilkTables.Silk_stereo_pred_joint_iCDF[:], 8)
	ix[0][2] = silk_DIV32_16(n, 5)
	ix[1][2] = n - 5*ix[0][2]
	for n = 0; n < 2; n++ {
		ix[n][0] = psRangeDec.dec_icdf(SilkTables.Silk_uniform3_iCDF[:], 8)
		ix[n][1] = psRangeDec.dec_icdf(SilkTables.Silk_uniform5_iCDF[:], 8)
	}

	for n = 0; n < 2; n++ {
		ix[n][0] += 3 * ix[n][2]
		low_Q13 = int(SilkTables.Silk_stereo_pred_quant_Q13[ix[n][0]])
		step_Q13 = silk_SMULWB(int(SilkTables.Silk_stereo_pred_quant_Q13[ix[n][0]+1])-low_Q13,
			int((0.5/float64(SilkConstants.STEREO_QUANT_SUB_STEPS))*float64(1<<16)+0.5))
		pred_Q13[n] = silk_SMLABB(low_Q13, step_Q13, 2*ix[n][1]+1)
	}

	pred_Q13[0] -= pred_Q13[1]
}

func silk_stereo_decode_mid_only(
	psRangeDec *EntropyCoder,
	decode_only_mid *BoxedValueInt) {
	decode_only_mid.Val = psRangeDec.dec_icdf(SilkTables.Silk_stereo_only_code_mid_iCDF[:], 8)
}

func silk_stereo_encode_pred(psRangeEnc *EntropyCoder, ix [][]byte) {
	var n int

	n = 5*int(ix[0][2]) + int(ix[1][2])
	OpusAssert(n < 25)
	psRangeEnc.enc_icdf(n, SilkTables.Silk_stereo_pred_joint_iCDF[:], 8)
	for n = 0; n < 2; n++ {
		OpusAssert(int(ix[n][0]) < 3)
		OpusAssert(int(ix[n][1]) < SilkConstants.STEREO_QUANT_SUB_STEPS)
		psRangeEnc.enc_icdf(int(ix[n][0]), SilkTables.Silk_uniform3_iCDF[:], 8)
		psRangeEnc.enc_icdf(int(ix[n][1]), SilkTables.Silk_uniform5_iCDF[:], 8)
	}
}

func silk_stereo_encode_mid_only(psRangeEnc *EntropyCoder, mid_only_flag byte) {
	psRangeEnc.enc_icdf(int(mid_only_flag), SilkTables.Silk_stereo_only_code_mid_iCDF[:], 8)
}

func silk_stereo_find_predictor(
	ratio_Q14 *int,
	x []int16,
	y []int16,
	mid_res_amp_Q0 []int,
	mid_res_amp_Q0_ptr int,
	length int,
	smooth_coef_Q16 int) int {
	var scale int
	nrgx := BoxedValueInt{0}
	nrgy := BoxedValueInt{0}
	scale1 := BoxedValueInt{0}
	scale2 := BoxedValueInt{0}
	var corr, pred_Q13, pred2_Q10 int

	silk_sum_sqr_shift4(&nrgx, &scale1, x, length)
	silk_sum_sqr_shift4(&nrgy, &scale2, y, length)
	scale = silk_max_int(scale1.Val, scale2.Val)

	scale = scale + (scale & 1)
	nrgy.Val = silk_RSHIFT32(nrgy.Val, scale-scale2.Val)
	nrgx.Val = silk_RSHIFT32(nrgx.Val, scale-scale1.Val)
	nrgx.Val = silk_max_int(nrgx.Val, 1)

	corr = silk_inner_prod_aligned_scale(x, y, scale, length)

	pred_Q13 = silk_DIV32_varQ(corr, nrgx.Val, 13)
	pred_Q13 = silk_LIMIT(pred_Q13, -(1 << 14), 1<<14)
	pred2_Q10 = silk_SMULWB(pred_Q13, pred_Q13)

	smooth_coef_Q16 = silk_max_int(smooth_coef_Q16, silk_abs(pred2_Q10))

	OpusAssert(smooth_coef_Q16 < 32768)
	scale = silk_RSHIFT(scale, 1)
	mid_res_amp_Q0[mid_res_amp_Q0_ptr] = silk_SMLAWB(mid_res_amp_Q0[mid_res_amp_Q0_ptr],
		silk_LSHIFT(silk_SQRT_APPROX(nrgx.Val), scale)-mid_res_amp_Q0[mid_res_amp_Q0_ptr], smooth_coef_Q16)
	nrgy.Val = silk_SUB_LSHIFT32(nrgy.Val, silk_SMULWB(corr, pred_Q13), 3+1)
	nrgy.Val = silk_ADD_LSHIFT32(nrgy.Val, silk_SMULWB(nrgx.Val, pred2_Q10), 6)
	mid_res_amp_Q0[mid_res_amp_Q0_ptr+1] = silk_SMLAWB(mid_res_amp_Q0[mid_res_amp_Q0_ptr+1],
		silk_LSHIFT(silk_SQRT_APPROX(nrgy.Val), scale)-mid_res_amp_Q0[mid_res_amp_Q0_ptr+1], smooth_coef_Q16)

	*ratio_Q14 = silk_DIV32_varQ(mid_res_amp_Q0[mid_res_amp_Q0_ptr+1], silk_max(mid_res_amp_Q0[mid_res_amp_Q0_ptr], 1), 14)
	*ratio_Q14 = silk_LIMIT(*ratio_Q14, 0, 32767)

	return pred_Q13
}

func silk_stereo_LR_to_MS(
	state *StereoEncodeState,
	x1 []int16,
	x1_ptr int,
	x2 []int16,
	x2_ptr int,
	ix [][]byte,
	mid_only_flag *byte,
	mid_side_rates_bps []int,
	total_rate_bps int,
	prev_speech_act_Q8 int,
	toMono int,
	fs_kHz int,
	frame_length int) {

	var n, is10msFrame, denom_Q16, delta0_Q13, delta1_Q13 int
	var sum, diff, smooth_coef_Q16, pred0_Q13, pred1_Q13 int
	pred_Q13 := make([]int, 2)
	var frac_Q16, frac_3_Q16, min_mid_rate_bps, width_Q14, w_Q24, deltaw_Q24 int
	LP_ratio_Q14 := new(int)
	HP_ratio_Q14 := new(int)
	var side []int16
	var LP_mid, HP_mid, LP_side, HP_side []int16
	mid := x1_ptr - 2

	side = make([]int16, frame_length+2)

	for n = 0; n < frame_length+2; n++ {
		sum = int(x1[x1_ptr+n-2]) + int(x2[x2_ptr+n-2])
		diff = int(x1[x1_ptr+n-2]) - int(x2[x2_ptr+n-2])
		x1[mid+n] = int16(silk_RSHIFT_ROUND(sum, 1))
		side[n] = int16(silk_SAT16(silk_RSHIFT_ROUND(diff, 1)))
	}

	copy(x1[mid:], state.sMid[:])
	copy(side[:], state.sSide[:])
	copy(state.sMid[:], x1[mid+frame_length:])
	copy(state.sSide[:], side[frame_length:])

	LP_mid = make([]int16, frame_length)
	HP_mid = make([]int16, frame_length)
	for n = 0; n < frame_length; n++ {
		sum = silk_RSHIFT_ROUND(silk_ADD_LSHIFT32(int(x1[mid+n])+int(x1[mid+n+2]), int(x1[mid+n+1]), 1), 2)
		LP_mid[n] = int16(sum)
		HP_mid[n] = int16(int(x1[mid+n+1]) - sum)
	}

	LP_side = make([]int16, frame_length)
	HP_side = make([]int16, frame_length)
	for n = 0; n < frame_length; n++ {
		sum = silk_RSHIFT_ROUND(silk_ADD_LSHIFT32(int(side[n])+int(side[n+2]), int(side[n+1]), 1), 2)
		LP_side[n] = int16(sum)
		HP_side[n] = int16(int(side[n+1]) - sum)
	}

	if frame_length == 10*fs_kHz {
		is10msFrame = 1
	} else {
		is10msFrame = 0
	}
	if is10msFrame != 0 {
		smooth_coef_Q16 = int(float64(SilkConstants.STEREO_RATIO_SMOOTH_COEF/2)*float64(1<<16) + 0.5)
	} else {
		smooth_coef_Q16 = int(float64(SilkConstants.STEREO_RATIO_SMOOTH_COEF)*float64(1<<16) + 0.5)
	}
	smooth_coef_Q16 = silk_SMULWB(silk_SMULBB(prev_speech_act_Q8, prev_speech_act_Q8), smooth_coef_Q16)

	pred_Q13[0] = silk_stereo_find_predictor(LP_ratio_Q14, LP_mid, LP_side, state.mid_side_amp_Q0[:], 0, frame_length, smooth_coef_Q16)
	pred_Q13[1] = silk_stereo_find_predictor(HP_ratio_Q14, HP_mid, HP_side, state.mid_side_amp_Q0[:], 2, frame_length, smooth_coef_Q16)

	frac_Q16 = silk_SMLABB(*HP_ratio_Q14, *LP_ratio_Q14, 3)
	frac_Q16 = silk_min(frac_Q16, int(math.Trunc(1*float64(1<<16)+0.5)))

	if is10msFrame != 0 {
		total_rate_bps -= 1200
	} else {
		total_rate_bps -= 600
	}

	if total_rate_bps < 1 {
		total_rate_bps = 1
	}
	min_mid_rate_bps = silk_SMLABB(2000, fs_kHz, 900)

	OpusAssert(min_mid_rate_bps < 32767)
	frac_3_Q16 = silk_MUL(3, frac_Q16)

	mid_side_rates_bps[0] = silk_DIV32_varQ(total_rate_bps, int(math.Trunc((8+5)*float64(int64(1)<<(16))+0.5))+frac_3_Q16, 16+3)

	if mid_side_rates_bps[0] < min_mid_rate_bps {
		mid_side_rates_bps[0] = min_mid_rate_bps
		mid_side_rates_bps[1] = total_rate_bps - mid_side_rates_bps[0]

		width_Q14 = silk_DIV32_varQ(silk_LSHIFT(mid_side_rates_bps[1], 1)-min_mid_rate_bps,
			silk_SMULWB(int(math.Trunc((1)*(1<<(16))+0.5))+frac_3_Q16, min_mid_rate_bps), 14+2)

		width_Q14 = silk_LIMIT(width_Q14, 0, int(math.Trunc(1*float64(1<<14)+0.5)))
	} else {
		mid_side_rates_bps[1] = total_rate_bps - mid_side_rates_bps[0]
		width_Q14 = int(math.Trunc((1)*(1<<(14)) + 0.5))
	}

	state.smth_width_Q14 = int16(silk_SMLAWB(int(state.smth_width_Q14), width_Q14-int(state.smth_width_Q14), smooth_coef_Q16))

	*mid_only_flag = 0
	if toMono != 0 {
		width_Q14 = 0
		pred_Q13[0] = 0
		pred_Q13[1] = 0
		silk_stereo_quant_pred(pred_Q13, ix)
	} else if state.width_prev_Q14 == 0 &&
		(8*total_rate_bps < 13*min_mid_rate_bps || silk_SMULWB(frac_Q16, int(state.smth_width_Q14)) < int(math.Trunc(0.05*float64(1<<14)+0.5))) {
		pred_Q13[0] = silk_RSHIFT(silk_SMULBB(int(state.smth_width_Q14), pred_Q13[0]), 14)
		pred_Q13[1] = silk_RSHIFT(silk_SMULBB(int(state.smth_width_Q14), pred_Q13[1]), 14)
		silk_stereo_quant_pred(pred_Q13, ix)
		width_Q14 = 0
		pred_Q13[0] = 0
		pred_Q13[1] = 0
		mid_side_rates_bps[0] = total_rate_bps
		mid_side_rates_bps[1] = 0
		*mid_only_flag = 1
	} else if state.width_prev_Q14 != 0 &&
		(8*total_rate_bps < 11*min_mid_rate_bps || silk_SMULWB(frac_Q16, int(state.smth_width_Q14)) < int(math.Trunc(0.02*float64(1<<14)+0.5))) {
		pred_Q13[0] = silk_RSHIFT(silk_SMULBB(int(state.smth_width_Q14), pred_Q13[0]), 14)
		pred_Q13[1] = silk_RSHIFT(silk_SMULBB(int(state.smth_width_Q14), pred_Q13[1]), 14)
		silk_stereo_quant_pred(pred_Q13, ix)
		width_Q14 = 0
		pred_Q13[0] = 0
		pred_Q13[1] = 0
	} else if int(state.smth_width_Q14) > int(math.Trunc(0.95*float64(1<<14)+0.5)) {
		silk_stereo_quant_pred(pred_Q13, ix)
		width_Q14 = int(math.Trunc(1*float64(1<<14) + 0.5))
	} else {
		pred_Q13[0] = silk_RSHIFT(silk_SMULBB(int(state.smth_width_Q14), pred_Q13[0]), 14)
		pred_Q13[1] = silk_RSHIFT(silk_SMULBB(int(state.smth_width_Q14), pred_Q13[1]), 14)
		silk_stereo_quant_pred(pred_Q13, ix)
		width_Q14 = int(state.smth_width_Q14)
	}

	if *mid_only_flag == 1 {
		state.silent_side_len += int16(frame_length - SilkConstants.STEREO_INTERP_LEN_MS*fs_kHz)
		if state.silent_side_len < int16(SilkConstants.LA_SHAPE_MS*fs_kHz) {
			*mid_only_flag = 0
		} else {
			state.silent_side_len = 10000
		}
	} else {
		state.silent_side_len = 0
	}

	if *mid_only_flag == 0 && mid_side_rates_bps[1] < 1 {
		mid_side_rates_bps[1] = 1
		mid_side_rates_bps[0] = silk_max_int(1, total_rate_bps-mid_side_rates_bps[1])
	}

	pred0_Q13 = -int(state.pred_prev_Q13[0])
	pred1_Q13 = -int(state.pred_prev_Q13[1])
	w_Q24 = silk_LSHIFT(int(state.width_prev_Q14), 10)
	denom_Q16 = silk_DIV32_16(1<<16, SilkConstants.STEREO_INTERP_LEN_MS*fs_kHz)
	//delta0_Q13 = 0 - silk_RSHIFT_ROUND(silk_SMULBB(pred_Q13[0]-int(state.pred_prev_Q13[0]), denom_Q16), 16)
	delta0_Q13 = 0 - silk_RSHIFT_ROUND(silk_SMULBB(pred_Q13[0]-int(state.pred_prev_Q13[0]), denom_Q16), 16)
	delta1_Q13 = 0 - silk_RSHIFT_ROUND(silk_SMULBB(pred_Q13[1]-int(state.pred_prev_Q13[1]), denom_Q16), 16)

	deltaw_Q24 = silk_LSHIFT(silk_SMULWB(width_Q14-int(state.width_prev_Q14), denom_Q16), 10)

	for n = 0; n < SilkConstants.STEREO_INTERP_LEN_MS*fs_kHz; n++ {
		pred0_Q13 += delta0_Q13
		pred1_Q13 += delta1_Q13
		w_Q24 += deltaw_Q24
		//dosgo int32
		sum = int((silk_LSHIFT32_32(silk_ADD_LSHIFT(int(int32(x1[mid+n])+int32(x1[mid+n+2])), int(x1[mid+n+1]), 1), 9)))

		/* Q11 */
		sum = silk_SMLAWB(silk_SMULWB(w_Q24, int(side[n+1])), sum, pred0_Q13)

		/* Q8  */
		sum = silk_SMLAWB(sum, silk_LSHIFT(int(x1[mid+n+1]), 11), pred1_Q13)

		/* Q8  */
		x2[x2_ptr+n-1] = int16(silk_SAT16(silk_RSHIFT_ROUND(sum, 8)))
	}

	pred0_Q13 = -pred_Q13[0]
	pred1_Q13 = -pred_Q13[1]
	w_Q24 = silk_LSHIFT(width_Q14, 10)
	for n = SilkConstants.STEREO_INTERP_LEN_MS * fs_kHz; n < frame_length; n++ {
		sum = int((silk_LSHIFT32_32(silk_ADD_LSHIFT(int(int32(x1[mid+n])+int32(x1[mid+n+2])), int(int32(x1[mid+n+1])), 1), 9)))
		sum = silk_SMLAWB(silk_SMULWB(w_Q24, int(side[n+1])), sum, pred0_Q13)
		sum = silk_SMLAWB(sum, silk_LSHIFT(int(x1[mid+n+1]), 11), pred1_Q13)
		x2[x2_ptr+n-1] = int16(silk_SAT16(silk_RSHIFT_ROUND(sum, 8)))
	}
	state.pred_prev_Q13[0] = int16(pred_Q13[0])
	state.pred_prev_Q13[1] = int16(pred_Q13[1])
	state.width_prev_Q14 = int16(width_Q14)
}

func silk_stereo_MS_to_LR(
	state *StereoDecodeState,
	x1 []int16,
	x1_ptr int,
	x2 []int16,
	x2_ptr int,
	pred_Q13 []int,
	fs_kHz int,
	frame_length int) {
	var n, denom_Q16, delta0_Q13, delta1_Q13 int
	var sum, diff, pred0_Q13, pred1_Q13 int

	copy(x1[x1_ptr:], state.sMid[:])
	copy(x2[x2_ptr:], state.sSide[:])
	copy(state.sMid[:], x1[x1_ptr+frame_length:])
	copy(state.sSide[:], x2[x2_ptr+frame_length:])

	pred0_Q13 = int(state.pred_prev_Q13[0])
	pred1_Q13 = int(state.pred_prev_Q13[1])
	denom_Q16 = silk_DIV32_16(1<<16, SilkConstants.STEREO_INTERP_LEN_MS*fs_kHz)
	delta0_Q13 = silk_RSHIFT_ROUND(silk_SMULBB(pred_Q13[0]-int(state.pred_prev_Q13[0]), denom_Q16), 16)
	delta1_Q13 = silk_RSHIFT_ROUND(silk_SMULBB(pred_Q13[1]-int(state.pred_prev_Q13[1]), denom_Q16), 16)
	for n = 0; n < SilkConstants.STEREO_INTERP_LEN_MS*fs_kHz; n++ {
		pred0_Q13 += delta0_Q13
		pred1_Q13 += delta1_Q13
		sum = int((silk_LSHIFT32_32(silk_ADD_LSHIFT(int(int32(x1[x1_ptr+n])+int32(x1[x1_ptr+n+2])), int(x1[x1_ptr+n+1]), 1), 9)))
		sum = silk_SMLAWB(silk_LSHIFT(int(x2[x2_ptr+n+1]), 8), sum, pred0_Q13)
		sum = silk_SMLAWB(sum, silk_LSHIFT(int(x1[x1_ptr+n+1]), 11), pred1_Q13)
		x2[x2_ptr+n+1] = int16(silk_SAT16(silk_RSHIFT_ROUND(sum, 8)))
	}
	pred0_Q13 = pred_Q13[0]
	pred1_Q13 = pred_Q13[1]
	for n = SilkConstants.STEREO_INTERP_LEN_MS * fs_kHz; n < frame_length; n++ {
		sum = int(silk_LSHIFT32_32(silk_ADD_LSHIFT(int(int32(x1[x1_ptr+n])+int32(x1[x1_ptr+n+2])), int(x1[x1_ptr+n+1]), 1), 9))
		sum = silk_SMLAWB(silk_LSHIFT(int(x2[x2_ptr+n+1]), 8), sum, pred0_Q13)
		sum = silk_SMLAWB(sum, silk_LSHIFT(int(x1[x1_ptr+n+1]), 11), pred1_Q13)
		x2[x2_ptr+n+1] = int16(silk_SAT16(silk_RSHIFT_ROUND(sum, 8)))
	}
	state.pred_prev_Q13[0] = int16(pred_Q13[0])
	state.pred_prev_Q13[1] = int16(pred_Q13[1])

	for n = 0; n < frame_length; n++ {
		sum = int(x1[x1_ptr+n+1]) + int(x2[x2_ptr+n+1])
		diff = int(x1[x1_ptr+n+1]) - int(x2[x2_ptr+n+1])
		x1[x1_ptr+n+1] = int16(silk_SAT16(sum))
		x2[x2_ptr+n+1] = int16(silk_SAT16(diff))
	}
}

func silk_stereo_quant_pred(
	pred_Q13 []int,
	ix [][]byte) {
	var i, j byte
	var n int
	var low_Q13, step_Q13, lvl_Q13, err_min_Q13, err_Q13, quant_pred_Q13 int
	for i := 0; i < 2; i++ {
		for j := 0; j < 3; j++ {
			ix[i][j] = 0
		}
	}

	for n = 0; n < 2; n++ {
		done := false
		err_min_Q13 = math.MaxInt32
		for i = 0; !done && i < byte(SilkConstants.STEREO_QUANT_TAB_SIZE)-1; i++ {
			low_Q13 = int(SilkTables.Silk_stereo_pred_quant_Q13[i])
			step_Q13 = silk_SMULWB(int(SilkTables.Silk_stereo_pred_quant_Q13[i+1])-low_Q13,
				int(0.5/float64(SilkConstants.STEREO_QUANT_SUB_STEPS)*float64(1<<16)+0.5))

			for j = 0; !done && j < byte(SilkConstants.STEREO_QUANT_SUB_STEPS); j++ {
				lvl_Q13 = silk_SMLABB(low_Q13, step_Q13, 2*int(j)+1)
				err_Q13 = silk_abs(pred_Q13[n] - lvl_Q13)
				if err_Q13 < err_min_Q13 {
					err_min_Q13 = err_Q13
					quant_pred_Q13 = lvl_Q13
					(ix)[n][0] = i
					(ix)[n][1] = j
				} else {
					done = true
				}
			}
		}

		ix[n][2] = byte(silk_DIV32_16(int(ix[n][0]), 3))
		ix[n][0] = ix[n][0] - byte(ix[n][2]*3)
		pred_Q13[n] = quant_pred_Q13
	}

	pred_Q13[0] -= pred_Q13[1]
}
