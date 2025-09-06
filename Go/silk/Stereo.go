package silk

import (
	"math"

	"github.com/dosgo/concentus/go/comm"
	"github.com/dosgo/concentus/go/comm/arrayUtil"
)

func Silk_stereo_decode_pred(
	psRangeDec *comm.EntropyCoder,
	pred_Q13 []int) {
	var n int
	ix := arrayUtil.InitTwoDimensionalArrayInt(2, 3)
	var low_Q13, step_Q13 int

	n = psRangeDec.Dec_icdf(SilkTables.Silk_stereo_pred_joint_iCDF[:], 8)
	ix[0][2] = inlines.Silk_DIV32_16(n, 5)
	ix[1][2] = n - 5*ix[0][2]
	for n = 0; n < 2; n++ {
		ix[n][0] = psRangeDec.Dec_icdf(SilkTables.Silk_uniform3_iCDF[:], 8)
		ix[n][1] = psRangeDec.Dec_icdf(SilkTables.Silk_uniform5_iCDF[:], 8)
	}

	for n = 0; n < 2; n++ {
		ix[n][0] += 3 * ix[n][2]
		low_Q13 = int(SilkTables.Silk_stereo_pred_quant_Q13[ix[n][0]])
		step_Q13 = inlines.Silk_SMULWB(int(SilkTables.Silk_stereo_pred_quant_Q13[ix[n][0]+1])-low_Q13,
			int((0.5/float64(SilkConstants.STEREO_QUANT_SUB_STEPS))*float64(1<<16)+0.5))
		pred_Q13[n] = inlines.Silk_SMLABB(low_Q13, step_Q13, 2*ix[n][1]+1)
	}

	pred_Q13[0] -= pred_Q13[1]
}

func Silk_stereo_decode_mid_only(
	psRangeDec *comm.EntropyCoder,
	decode_only_mid *comm.BoxedValueInt) {
	decode_only_mid.Val = psRangeDec.Dec_icdf(SilkTables.Silk_stereo_only_code_mid_iCDF[:], 8)
}

func Silk_stereo_encode_pred(psRangeEnc *comm.EntropyCoder, ix [][]byte) {
	var n int

	n = 5*int(ix[0][2]) + int(ix[1][2])
	inlines.OpusAssert(n < 25)
	psRangeEnc.Enc_icdf(n, SilkTables.Silk_stereo_pred_joint_iCDF[:], 8)
	for n = 0; n < 2; n++ {
		inlines.OpusAssert(int(ix[n][0]) < 3)
		inlines.OpusAssert(int(ix[n][1]) < SilkConstants.STEREO_QUANT_SUB_STEPS)
		psRangeEnc.Enc_icdf(int(ix[n][0]), SilkTables.Silk_uniform3_iCDF[:], 8)
		psRangeEnc.Enc_icdf(int(ix[n][1]), SilkTables.Silk_uniform5_iCDF[:], 8)
	}
}

func Silk_stereo_encode_mid_only(psRangeEnc *comm.EntropyCoder, mid_only_flag byte) {
	psRangeEnc.Enc_icdf(int(mid_only_flag), SilkTables.Silk_stereo_only_code_mid_iCDF[:], 8)
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
	nrgx := comm.BoxedValueInt{0}
	nrgy := comm.BoxedValueInt{0}
	scale1 := comm.BoxedValueInt{0}
	scale2 := comm.BoxedValueInt{0}
	var corr, pred_Q13, pred2_Q10 int

	silk_sum_sqr_shift4(&nrgx, &scale1, x, length)
	silk_sum_sqr_shift4(&nrgy, &scale2, y, length)
	scale = inlines.Silk_max_int(scale1.Val, scale2.Val)

	scale = scale + (scale & 1)
	nrgy.Val = inlines.Silk_RSHIFT32(nrgy.Val, scale-scale2.Val)
	nrgx.Val = inlines.Silk_RSHIFT32(nrgx.Val, scale-scale1.Val)
	nrgx.Val = inlines.Silk_max_int(nrgx.Val, 1)

	corr = inlines.Silk_inner_prod_aligned_scale(x, y, scale, length)

	pred_Q13 = inlines.Silk_DIV32_varQ(corr, nrgx.Val, 13)
	pred_Q13 = inlines.Silk_LIMIT(pred_Q13, -(1 << 14), 1<<14)
	pred2_Q10 = inlines.Silk_SMULWB(pred_Q13, pred_Q13)

	smooth_coef_Q16 = inlines.Silk_max_int(smooth_coef_Q16, inlines.Silk_abs(pred2_Q10))

	inlines.OpusAssert(smooth_coef_Q16 < 32768)
	scale = inlines.Silk_RSHIFT(scale, 1)
	mid_res_amp_Q0[mid_res_amp_Q0_ptr] = inlines.Silk_SMLAWB(mid_res_amp_Q0[mid_res_amp_Q0_ptr],
		inlines.Silk_LSHIFT(inlines.Silk_SQRT_APPROX(nrgx.Val), scale)-mid_res_amp_Q0[mid_res_amp_Q0_ptr], smooth_coef_Q16)
	nrgy.Val = inlines.Silk_SUB_LSHIFT32(nrgy.Val, inlines.Silk_SMULWB(corr, pred_Q13), 3+1)
	nrgy.Val = inlines.Silk_ADD_LSHIFT32(nrgy.Val, inlines.Silk_SMULWB(nrgx.Val, pred2_Q10), 6)
	mid_res_amp_Q0[mid_res_amp_Q0_ptr+1] = inlines.Silk_SMLAWB(mid_res_amp_Q0[mid_res_amp_Q0_ptr+1],
		inlines.Silk_LSHIFT(inlines.Silk_SQRT_APPROX(nrgy.Val), scale)-mid_res_amp_Q0[mid_res_amp_Q0_ptr+1], smooth_coef_Q16)

	*ratio_Q14 = inlines.Silk_DIV32_varQ(mid_res_amp_Q0[mid_res_amp_Q0_ptr+1], inlines.Silk_max(mid_res_amp_Q0[mid_res_amp_Q0_ptr], 1), 14)
	*ratio_Q14 = inlines.Silk_LIMIT(*ratio_Q14, 0, 32767)

	return pred_Q13
}

func Silk_stereo_LR_to_MS(
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
		x1[mid+n] = int16(inlines.Silk_RSHIFT_ROUND(sum, 1))
		side[n] = int16(inlines.Silk_SAT16(inlines.Silk_RSHIFT_ROUND(diff, 1)))
	}

	copy(x1[mid:], state.SMid[:])
	copy(side[:], state.SSide[:])
	copy(state.SMid[:], x1[mid+frame_length:])
	copy(state.SSide[:], side[frame_length:])

	LP_mid = make([]int16, frame_length)
	HP_mid = make([]int16, frame_length)
	for n = 0; n < frame_length; n++ {
		sum = inlines.Silk_RSHIFT_ROUND(inlines.Silk_ADD_LSHIFT32(int(x1[mid+n])+int(x1[mid+n+2]), int(x1[mid+n+1]), 1), 2)
		LP_mid[n] = int16(sum)
		HP_mid[n] = int16(int(x1[mid+n+1]) - sum)
	}

	LP_side = make([]int16, frame_length)
	HP_side = make([]int16, frame_length)
	for n = 0; n < frame_length; n++ {
		sum = inlines.Silk_RSHIFT_ROUND(inlines.Silk_ADD_LSHIFT32(int(side[n])+int(side[n+2]), int(side[n+1]), 1), 2)
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
	smooth_coef_Q16 = inlines.Silk_SMULWB(inlines.Silk_SMULBB(prev_speech_act_Q8, prev_speech_act_Q8), smooth_coef_Q16)

	pred_Q13[0] = silk_stereo_find_predictor(LP_ratio_Q14, LP_mid, LP_side, state.Mid_side_amp_Q0[:], 0, frame_length, smooth_coef_Q16)
	pred_Q13[1] = silk_stereo_find_predictor(HP_ratio_Q14, HP_mid, HP_side, state.Mid_side_amp_Q0[:], 2, frame_length, smooth_coef_Q16)

	frac_Q16 = inlines.Silk_SMLABB(*HP_ratio_Q14, *LP_ratio_Q14, 3)
	frac_Q16 = inlines.Silk_min(frac_Q16, int(math.Trunc(1*float64(1<<16)+0.5)))

	if is10msFrame != 0 {
		total_rate_bps -= 1200
	} else {
		total_rate_bps -= 600
	}

	if total_rate_bps < 1 {
		total_rate_bps = 1
	}
	min_mid_rate_bps = inlines.Silk_SMLABB(2000, fs_kHz, 900)

	inlines.OpusAssert(min_mid_rate_bps < 32767)
	frac_3_Q16 = inlines.Silk_MUL(3, frac_Q16)

	mid_side_rates_bps[0] = inlines.Silk_DIV32_varQ(total_rate_bps, int(math.Trunc((8+5)*float64(int64(1)<<(16))+0.5))+frac_3_Q16, 16+3)

	if mid_side_rates_bps[0] < min_mid_rate_bps {
		mid_side_rates_bps[0] = min_mid_rate_bps
		mid_side_rates_bps[1] = total_rate_bps - mid_side_rates_bps[0]

		width_Q14 = inlines.Silk_DIV32_varQ(inlines.Silk_LSHIFT(mid_side_rates_bps[1], 1)-min_mid_rate_bps,
			inlines.Silk_SMULWB(int(math.Trunc((1)*(1<<(16))+0.5))+frac_3_Q16, min_mid_rate_bps), 14+2)

		width_Q14 = inlines.Silk_LIMIT(width_Q14, 0, int(math.Trunc(1*float64(1<<14)+0.5)))
	} else {
		mid_side_rates_bps[1] = total_rate_bps - mid_side_rates_bps[0]
		width_Q14 = int(math.Trunc((1)*(1<<(14)) + 0.5))
	}

	state.Smth_width_Q14 = int16(inlines.Silk_SMLAWB(int(state.Smth_width_Q14), width_Q14-int(state.Smth_width_Q14), smooth_coef_Q16))

	*mid_only_flag = 0
	if toMono != 0 {
		width_Q14 = 0
		pred_Q13[0] = 0
		pred_Q13[1] = 0
		silk_stereo_quant_pred(pred_Q13, ix)
	} else if state.Width_prev_Q14 == 0 &&
		(8*total_rate_bps < 13*min_mid_rate_bps || inlines.Silk_SMULWB(frac_Q16, int(state.Smth_width_Q14)) < int(math.Trunc(0.05*float64(1<<14)+0.5))) {
		pred_Q13[0] = inlines.Silk_RSHIFT(inlines.Silk_SMULBB(int(state.Smth_width_Q14), pred_Q13[0]), 14)
		pred_Q13[1] = inlines.Silk_RSHIFT(inlines.Silk_SMULBB(int(state.Smth_width_Q14), pred_Q13[1]), 14)
		silk_stereo_quant_pred(pred_Q13, ix)
		width_Q14 = 0
		pred_Q13[0] = 0
		pred_Q13[1] = 0
		mid_side_rates_bps[0] = total_rate_bps
		mid_side_rates_bps[1] = 0
		*mid_only_flag = 1
	} else if state.Width_prev_Q14 != 0 &&
		(8*total_rate_bps < 11*min_mid_rate_bps || inlines.Silk_SMULWB(frac_Q16, int(state.Smth_width_Q14)) < int(math.Trunc(0.02*float64(1<<14)+0.5))) {
		pred_Q13[0] = inlines.Silk_RSHIFT(inlines.Silk_SMULBB(int(state.Smth_width_Q14), pred_Q13[0]), 14)
		pred_Q13[1] = inlines.Silk_RSHIFT(inlines.Silk_SMULBB(int(state.Smth_width_Q14), pred_Q13[1]), 14)
		silk_stereo_quant_pred(pred_Q13, ix)
		width_Q14 = 0
		pred_Q13[0] = 0
		pred_Q13[1] = 0
	} else if int(state.Smth_width_Q14) > int(math.Trunc(0.95*float64(1<<14)+0.5)) {
		silk_stereo_quant_pred(pred_Q13, ix)
		width_Q14 = int(math.Trunc(1*float64(1<<14) + 0.5))
	} else {
		pred_Q13[0] = inlines.Silk_RSHIFT(inlines.Silk_SMULBB(int(state.Smth_width_Q14), pred_Q13[0]), 14)
		pred_Q13[1] = inlines.Silk_RSHIFT(inlines.Silk_SMULBB(int(state.Smth_width_Q14), pred_Q13[1]), 14)
		silk_stereo_quant_pred(pred_Q13, ix)
		width_Q14 = int(state.Smth_width_Q14)
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
		mid_side_rates_bps[0] = inlines.Silk_max_int(1, total_rate_bps-mid_side_rates_bps[1])
	}

	pred0_Q13 = -int(state.Pred_prev_Q13[0])
	pred1_Q13 = -int(state.Pred_prev_Q13[1])
	w_Q24 = inlines.Silk_LSHIFT(int(state.Width_prev_Q14), 10)
	denom_Q16 = inlines.Silk_DIV32_16(1<<16, SilkConstants.STEREO_INTERP_LEN_MS*fs_kHz)
	//delta0_Q13 = 0 - inlines.Silk_RSHIFT_ROUND(inlines.Silk_SMULBB(pred_Q13[0]-int(state.pred_prev_Q13[0]), denom_Q16), 16)
	delta0_Q13 = 0 - inlines.Silk_RSHIFT_ROUND(inlines.Silk_SMULBB(pred_Q13[0]-int(state.Pred_prev_Q13[0]), denom_Q16), 16)
	delta1_Q13 = 0 - inlines.Silk_RSHIFT_ROUND(inlines.Silk_SMULBB(pred_Q13[1]-int(state.Pred_prev_Q13[1]), denom_Q16), 16)

	deltaw_Q24 = inlines.Silk_LSHIFT(inlines.Silk_SMULWB(width_Q14-int(state.Width_prev_Q14), denom_Q16), 10)

	for n = 0; n < SilkConstants.STEREO_INTERP_LEN_MS*fs_kHz; n++ {
		pred0_Q13 += delta0_Q13
		pred1_Q13 += delta1_Q13
		w_Q24 += deltaw_Q24
		//dosgo int32
		sum = int((inlines.Silk_LSHIFT32_32(inlines.Silk_ADD_LSHIFT(int(int32(x1[mid+n])+int32(x1[mid+n+2])), int(x1[mid+n+1]), 1), 9)))

		/* Q11 */
		sum = inlines.Silk_SMLAWB(inlines.Silk_SMULWB(w_Q24, int(side[n+1])), sum, pred0_Q13)

		/* Q8  */
		sum = inlines.Silk_SMLAWB(sum, inlines.Silk_LSHIFT(int(x1[mid+n+1]), 11), pred1_Q13)

		/* Q8  */
		x2[x2_ptr+n-1] = int16(inlines.Silk_SAT16(inlines.Silk_RSHIFT_ROUND(sum, 8)))
	}

	pred0_Q13 = -pred_Q13[0]
	pred1_Q13 = -pred_Q13[1]
	w_Q24 = inlines.Silk_LSHIFT(width_Q14, 10)
	for n = SilkConstants.STEREO_INTERP_LEN_MS * fs_kHz; n < frame_length; n++ {
		sum = int((inlines.Silk_LSHIFT32_32(inlines.Silk_ADD_LSHIFT(int(int32(x1[mid+n])+int32(x1[mid+n+2])), int(int32(x1[mid+n+1])), 1), 9)))
		sum = inlines.Silk_SMLAWB(inlines.Silk_SMULWB(w_Q24, int(side[n+1])), sum, pred0_Q13)
		sum = inlines.Silk_SMLAWB(sum, inlines.Silk_LSHIFT(int(x1[mid+n+1]), 11), pred1_Q13)
		x2[x2_ptr+n-1] = int16(inlines.Silk_SAT16(inlines.Silk_RSHIFT_ROUND(sum, 8)))
	}
	state.Pred_prev_Q13[0] = int16(pred_Q13[0])
	state.Pred_prev_Q13[1] = int16(pred_Q13[1])
	state.Width_prev_Q14 = int16(width_Q14)
}

func Silk_stereo_MS_to_LR(
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

	copy(x1[x1_ptr:], state.SMid[:])
	copy(x2[x2_ptr:], state.SSide[:])
	copy(state.SMid[:], x1[x1_ptr+frame_length:])
	copy(state.SSide[:], x2[x2_ptr+frame_length:])

	pred0_Q13 = int(state.Pred_prev_Q13[0])
	pred1_Q13 = int(state.Pred_prev_Q13[1])
	denom_Q16 = inlines.Silk_DIV32_16(1<<16, SilkConstants.STEREO_INTERP_LEN_MS*fs_kHz)
	delta0_Q13 = inlines.Silk_RSHIFT_ROUND(inlines.Silk_SMULBB(pred_Q13[0]-int(state.Pred_prev_Q13[0]), denom_Q16), 16)
	delta1_Q13 = inlines.Silk_RSHIFT_ROUND(inlines.Silk_SMULBB(pred_Q13[1]-int(state.Pred_prev_Q13[1]), denom_Q16), 16)
	for n = 0; n < SilkConstants.STEREO_INTERP_LEN_MS*fs_kHz; n++ {
		pred0_Q13 += delta0_Q13
		pred1_Q13 += delta1_Q13
		sum = int((inlines.Silk_LSHIFT32_32(inlines.Silk_ADD_LSHIFT(int(int32(x1[x1_ptr+n])+int32(x1[x1_ptr+n+2])), int(x1[x1_ptr+n+1]), 1), 9)))
		sum = inlines.Silk_SMLAWB(inlines.Silk_LSHIFT(int(x2[x2_ptr+n+1]), 8), sum, pred0_Q13)
		sum = inlines.Silk_SMLAWB(sum, inlines.Silk_LSHIFT(int(x1[x1_ptr+n+1]), 11), pred1_Q13)
		x2[x2_ptr+n+1] = int16(inlines.Silk_SAT16(inlines.Silk_RSHIFT_ROUND(sum, 8)))
	}
	pred0_Q13 = pred_Q13[0]
	pred1_Q13 = pred_Q13[1]
	for n = SilkConstants.STEREO_INTERP_LEN_MS * fs_kHz; n < frame_length; n++ {
		sum = int(inlines.Silk_LSHIFT32_32(inlines.Silk_ADD_LSHIFT(int(int32(x1[x1_ptr+n])+int32(x1[x1_ptr+n+2])), int(x1[x1_ptr+n+1]), 1), 9))
		sum = inlines.Silk_SMLAWB(inlines.Silk_LSHIFT(int(x2[x2_ptr+n+1]), 8), sum, pred0_Q13)
		sum = inlines.Silk_SMLAWB(sum, inlines.Silk_LSHIFT(int(x1[x1_ptr+n+1]), 11), pred1_Q13)
		x2[x2_ptr+n+1] = int16(inlines.Silk_SAT16(inlines.Silk_RSHIFT_ROUND(sum, 8)))
	}
	state.Pred_prev_Q13[0] = int16(pred_Q13[0])
	state.Pred_prev_Q13[1] = int16(pred_Q13[1])

	for n = 0; n < frame_length; n++ {
		sum = int(x1[x1_ptr+n+1]) + int(x2[x2_ptr+n+1])
		diff = int(x1[x1_ptr+n+1]) - int(x2[x2_ptr+n+1])
		x1[x1_ptr+n+1] = int16(inlines.Silk_SAT16(sum))
		x2[x2_ptr+n+1] = int16(inlines.Silk_SAT16(diff))
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
			step_Q13 = inlines.Silk_SMULWB(int(SilkTables.Silk_stereo_pred_quant_Q13[i+1])-low_Q13,
				int(0.5/float64(SilkConstants.STEREO_QUANT_SUB_STEPS)*float64(1<<16)+0.5))

			for j = 0; !done && j < byte(SilkConstants.STEREO_QUANT_SUB_STEPS); j++ {
				lvl_Q13 = inlines.Silk_SMLABB(low_Q13, step_Q13, 2*int(j)+1)
				err_Q13 = inlines.Silk_abs(pred_Q13[n] - lvl_Q13)
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

		ix[n][2] = byte(inlines.Silk_DIV32_16(int(ix[n][0]), 3))
		ix[n][0] = ix[n][0] - byte(ix[n][2]*3)
		pred_Q13[n] = quant_pred_Q13
	}

	pred_Q13[0] -= pred_Q13[1]
}
