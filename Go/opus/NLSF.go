package opus

import (
	"math"
)

const (
	MAX_STABILIZE_LOOPS   = 20
	BIN_DIV_STEPS_A2NLSF  = 3
	MAX_ITERATIONS_A2NLSF = 30
	QA16                  = 16
)

func silk_NLSF_VQ(err_Q26 []int, in_Q15 []int16, pCB_Q8 []int16, K int, LPC_order int) {
	var diff_Q15, sum_error_Q30, sum_error_Q26 int
	pCB_idx := 0

	OpusAssert(err_Q26 != nil)
	OpusAssert(LPC_order <= 16)
	OpusAssert((LPC_order & 1) == 0)

	for i := 0; i < K; i++ {
		sum_error_Q26 = 0
		for m := 0; m < LPC_order; m += 2 {
			diff_Q15 = silk_SUB_LSHIFT32(int(in_Q15[m]), int(pCB_Q8[pCB_idx]), 7)
			sum_error_Q30 = silk_SMULBB(diff_Q15, diff_Q15)
			diff_Q15 = silk_SUB_LSHIFT32(int(in_Q15[m+1]), int(pCB_Q8[pCB_idx+1]), 7)
			sum_error_Q30 = silk_SMLABB(sum_error_Q30, diff_Q15, diff_Q15)
			sum_error_Q26 = silk_ADD_RSHIFT32(sum_error_Q26, sum_error_Q30, 4)
			OpusAssert(sum_error_Q26 >= 0)
			OpusAssert(sum_error_Q30 >= 0)
			pCB_idx += 2
		}
		err_Q26[i] = sum_error_Q26
	}
}

func silk_NLSF_VQ_weights_laroia(pNLSFW_Q_OUT []int16, pNLSF_Q15 []int16, D int) {
	var k int
	var tmp1_int, tmp2_int int

	OpusAssert(pNLSFW_Q_OUT != nil)
	OpusAssert(D > 0)
	OpusAssert((D & 1) == 0)

	// First value
	tmp1_int = silk_max_int(int(pNLSF_Q15[0]), 1)
	tmp1_int = silk_DIV32(int(int32(1)<<(15+SilkConstants.NLSF_W_Q)), tmp1_int)
	tmp2_int = silk_max_int(int(pNLSF_Q15[1]-pNLSF_Q15[0]), 1)
	tmp2_int = silk_DIV32(int(int32(1)<<(15+SilkConstants.NLSF_W_Q)), tmp2_int)
	pNLSFW_Q_OUT[0] = int16(silk_min_int(tmp1_int+tmp2_int, math.MaxInt16))

	OpusAssert(pNLSFW_Q_OUT[0] > 0)

	// Main loop
	for k = 1; k < D-1; k += 2 {
		tmp1_int = silk_max_int(int(pNLSF_Q15[k+1]-pNLSF_Q15[k]), 1)
		tmp1_int = silk_DIV32(int(int32(1)<<(15+SilkConstants.NLSF_W_Q)), tmp1_int)
		pNLSFW_Q_OUT[k] = int16(silk_min_int(tmp1_int+tmp2_int, math.MaxInt16))
		OpusAssert(pNLSFW_Q_OUT[k] > 0)

		tmp2_int = silk_max_int(int(pNLSF_Q15[k+2]-pNLSF_Q15[k+1]), 1)
		tmp2_int = silk_DIV32(int(int32(1)<<(15+SilkConstants.NLSF_W_Q)), tmp2_int)
		pNLSFW_Q_OUT[k+1] = int16(silk_min_int(tmp1_int+tmp2_int, math.MaxInt16))
		OpusAssert(pNLSFW_Q_OUT[k+1] > 0)
	}

	// Last value
	tmp1_int = silk_max_int(int(int32(1)<<15-int32(pNLSF_Q15[D-1])), 1)
	tmp1_int = silk_DIV32(int(int32(1)<<(15+SilkConstants.NLSF_W_Q)), tmp1_int)
	pNLSFW_Q_OUT[D-1] = int16(silk_min_int(tmp1_int+tmp2_int, math.MaxInt16))

	OpusAssert(pNLSFW_Q_OUT[D-1] > 0)
}

func silk_NLSF_residual_dequant(x_Q10 []int16, indices []int8, indices_ptr int, pred_coef_Q8 []int16, quant_step_size_Q16 int, order int16) {
	var pred_Q10, out_Q10 int

	out_Q10 = 0
	for i := int(order) - 1; i >= 0; i-- {
		pred_Q10 = silk_RSHIFT(int(silk_SMULBB(out_Q10, int(pred_coef_Q8[i]))), 8)
		out_Q10 = int(indices[indices_ptr+i]) << 10
		if out_Q10 > 0 {
			out_Q10 -= int(SilkConstants.NLSF_QUANT_LEVEL_ADJ * (1 << 10))
		} else if out_Q10 < 0 {
			out_Q10 += int(SilkConstants.NLSF_QUANT_LEVEL_ADJ * (1 << 10))
		}
		out_Q10 = silk_SMLAWB(pred_Q10, out_Q10, quant_step_size_Q16)
		x_Q10[i] = int16(out_Q10)
	}
}

func silk_NLSF_unpack(ec_ix []int16, pred_Q8 []int16, psNLSF_CB *NLSFCodebook, CB1_index int) {
	var entry int16
	ec_sel_ptr := CB1_index * int(psNLSF_CB.order) / 2

	for i := 0; i < int(psNLSF_CB.order); i += 2 {
		entry = psNLSF_CB.ec_sel[ec_sel_ptr]
		ec_sel_ptr++
		ec_ix[i] = int16(silk_SMULBB(int((entry>>1)&7), int(2*SilkConstants.NLSF_QUANT_MAX_AMPLITUDE+1)))
		pred_Q8[i] = psNLSF_CB.pred_Q8[i+(int(entry)&1)*(int(psNLSF_CB.order)-1)]
		ec_ix[i+1] = int16(silk_SMULBB(int((entry>>5)&7), int(2*SilkConstants.NLSF_QUANT_MAX_AMPLITUDE+1)))
		pred_Q8[i+1] = psNLSF_CB.pred_Q8[i+(silk_RSHIFT(int(entry), 4)&1)*(int(psNLSF_CB.order)-1)+1]
	}
}

func silk_NLSF_stabilize(NLSF_Q15 []int16, NDeltaMin_Q15 []int16, L int) {
	var I, k, loops int
	var center_freq_Q15 int16
	var diff_Q15, min_diff_Q15, min_center_Q15, max_center_Q15 int

	OpusAssert(int(NDeltaMin_Q15[L]) >= 1)

	for loops = 0; loops < MAX_STABILIZE_LOOPS; loops++ {
		min_diff_Q15 = int(NLSF_Q15[0] - NDeltaMin_Q15[0])
		I = 0

		for i := 1; i <= L-1; i++ {
			diff_Q15 = int(NLSF_Q15[i] - (NLSF_Q15[i-1] + NDeltaMin_Q15[i]))
			if diff_Q15 < min_diff_Q15 {
				min_diff_Q15 = diff_Q15
				I = i
			}
		}

		diff_Q15 = (1 << 15) - int(NLSF_Q15[L-1]) + int(NDeltaMin_Q15[L])
		if diff_Q15 < min_diff_Q15 {
			min_diff_Q15 = diff_Q15
			I = L
		}

		if min_diff_Q15 >= 0 {
			return
		}

		if I == 0 {
			NLSF_Q15[0] = NDeltaMin_Q15[0]
		} else if I == L {
			NLSF_Q15[L-1] = int16((1 << 15) - int(NDeltaMin_Q15[L]))
		} else {
			min_center_Q15 = 0
			for k = 0; k < I; k++ {
				min_center_Q15 += int(NDeltaMin_Q15[k])
			}
			min_center_Q15 += int(silk_RSHIFT(int(NDeltaMin_Q15[I]), 1))

			max_center_Q15 = 1 << 15
			for k = L; k > I; k-- {
				max_center_Q15 -= int(NDeltaMin_Q15[k])
			}
			max_center_Q15 -= int(silk_RSHIFT(int(NDeltaMin_Q15[I]), 1))

			center_freq_Q15 = int16(silk_LIMIT_32(
				silk_RSHIFT_ROUND(int(NLSF_Q15[I-1])+int(NLSF_Q15[I]), 1),
				min_center_Q15, max_center_Q15))
			NLSF_Q15[I-1] = center_freq_Q15 - int16(silk_RSHIFT(int(NDeltaMin_Q15[I]), 1))
			NLSF_Q15[I] = NLSF_Q15[I-1] + NDeltaMin_Q15[I]
		}
	}

	if loops == MAX_STABILIZE_LOOPS {
		silk_insertion_sort_increasing_all_values_int16(NLSF_Q15, L)
		NLSF_Q15[0] = int16(silk_max_int(int(NLSF_Q15[0]), int(NDeltaMin_Q15[0])))
		for i := 1; i < L; i++ {
			NLSF_Q15[i] = int16(silk_max_int(int(NLSF_Q15[i]), int(NLSF_Q15[i-1])+int(NDeltaMin_Q15[i])))
		}
		NLSF_Q15[L-1] = int16(silk_min_int(int(NLSF_Q15[L-1]), (1<<15)-int(NDeltaMin_Q15[L])))
		for i := L - 2; i >= 0; i-- {
			NLSF_Q15[i] = int16(silk_min_int(int(NLSF_Q15[i]), int(NLSF_Q15[i+1])-int(NDeltaMin_Q15[i+1])))
		}
	}
}

func silk_NLSF_decode(pNLSF_Q15 []int16, NLSFIndices []int8, psNLSF_CB *NLSFCodebook) {
	pred_Q8 := make([]int16, psNLSF_CB.order)
	ec_ix := make([]int16, psNLSF_CB.order)
	res_Q10 := make([]int16, psNLSF_CB.order)
	W_tmp_QW := make([]int16, psNLSF_CB.order)
	var W_tmp_Q9, NLSF_Q15_tmp int

	pCB_element := int(NLSFIndices[0]) * int(psNLSF_CB.order)
	for i := 0; i < int(psNLSF_CB.order); i++ {
		pNLSF_Q15[i] = int16(psNLSF_CB.CB1_NLSF_Q8[pCB_element+i] << 7)
	}

	silk_NLSF_unpack(ec_ix, pred_Q8, psNLSF_CB, int(NLSFIndices[0]))
	silk_NLSF_residual_dequant(res_Q10, NLSFIndices, 1, pred_Q8, int(psNLSF_CB.quantStepSize_Q16), int16(psNLSF_CB.order))
	silk_NLSF_VQ_weights_laroia(W_tmp_QW, pNLSF_Q15, int(psNLSF_CB.order))

	for i := 0; i < int(psNLSF_CB.order); i++ {
		W_tmp_Q9 = silk_SQRT_APPROX(int(W_tmp_QW[i]) << (18 - SilkConstants.NLSF_W_Q))
		NLSF_Q15_tmp = int(pNLSF_Q15[i]) + silk_DIV32_16(int(res_Q10[i])<<14, int(W_tmp_Q9))
		pNLSF_Q15[i] = int16(silk_LIMIT(NLSF_Q15_tmp, 0, 32767))
	}

	silk_NLSF_stabilize(pNLSF_Q15, psNLSF_CB.deltaMin_Q15, int(psNLSF_CB.order))
}

func silk_NLSF_del_dec_quant(indices []int8, x_Q10 []int16, w_Q5 []int16, pred_coef_Q8 []int16, ec_ix []int16, ec_rates_Q5 []int16, quant_step_size_Q16 int, inv_quant_step_size_Q6 int16, mu_Q20 int, order int16) int {

	var i, j, nStates, ind_tmp, ind_min_max, ind_max_min, in_Q10, res_Q10 int
	var pred_Q10, diff_Q10, out0_Q10, out1_Q10, rate0_Q5, rate1_Q5 int
	var RD_tmp_Q25, min_Q25, min_max_Q25, max_min_Q25, pred_coef_Q16 int
	ind_sort := make([]int, SilkConstants.NLSF_QUANT_DEL_DEC_STATES)
	ind := make([][]int8, SilkConstants.NLSF_QUANT_DEL_DEC_STATES)
	for i = 0; i < SilkConstants.NLSF_QUANT_DEL_DEC_STATES; i++ {
		ind[i] = make([]int8, SilkConstants.MAX_LPC_ORDER)
	}

	prev_out_Q10 := make([]int16, 2*SilkConstants.NLSF_QUANT_DEL_DEC_STATES)
	RD_Q25 := make([]int, 2*SilkConstants.NLSF_QUANT_DEL_DEC_STATES)
	RD_min_Q25 := make([]int, SilkConstants.NLSF_QUANT_DEL_DEC_STATES)
	RD_max_Q25 := make([]int, SilkConstants.NLSF_QUANT_DEL_DEC_STATES)
	var rates_Q5 int

	out0_Q10_table := make([]int, 2*SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT)
	out1_Q10_table := make([]int, 2*SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT)

	for i = 0 - SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT; i <= SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT-1; i++ {
		out0_Q10 = silk_LSHIFT(i, 10)
		out1_Q10 = int(silk_ADD16(int16(out0_Q10), 1024))

		if i > 0 {
			out0_Q10 = int(silk_SUB16(int16(out0_Q10), int16((float64(SilkConstants.NLSF_QUANT_LEVEL_ADJ)*float64(int64(1)<<(10)) + 0.5))))
			out1_Q10 = int(silk_SUB16(int16(out1_Q10), int16((float64(SilkConstants.NLSF_QUANT_LEVEL_ADJ)*float64(int64(1)<<(10)) + 0.5))))
		} else if i == 0 {
			out1_Q10 = int(silk_SUB16(int16(out1_Q10), int16((float64(SilkConstants.NLSF_QUANT_LEVEL_ADJ)*float64(int64(1)<<(10)) + 0.5))))
		} else if i == -1 {
			out0_Q10 = int(silk_ADD16(int16(out0_Q10), int16((float64(SilkConstants.NLSF_QUANT_LEVEL_ADJ)*float64(int64(1)<<(10)) + 0.5))))
		} else {
			out0_Q10 = int(silk_ADD16(int16(out0_Q10), int16((float64(SilkConstants.NLSF_QUANT_LEVEL_ADJ)*float64(int64(1)<<(10)) + 0.5))))
			out1_Q10 = int(silk_ADD16(int16(out1_Q10), int16((float64(SilkConstants.NLSF_QUANT_LEVEL_ADJ)*float64(int64(1)<<(10)) + 0.5))))
		}

		out0_Q10_table[i+SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT] = silk_SMULWB(out0_Q10, quant_step_size_Q16)
		out1_Q10_table[i+SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT] = silk_SMULWB(out1_Q10, quant_step_size_Q16)
	}

	OpusAssert((SilkConstants.NLSF_QUANT_DEL_DEC_STATES & (SilkConstants.NLSF_QUANT_DEL_DEC_STATES - 1)) == 0) // must be power of two

	nStates = 1
	RD_Q25[0] = 0
	prev_out_Q10[0] = 0

	for i = int(order) - 1; ; i-- {
		pred_coef_Q16 = silk_LSHIFT(int(pred_coef_Q8[i]), 8)
		in_Q10 = int(x_Q10[i])

		for j = 0; j < nStates; j++ {
			pred_Q10 = silk_SMULWB(pred_coef_Q16, int(prev_out_Q10[j]))
			res_Q10 = int(silk_SUB16(int16(in_Q10), int16(pred_Q10)))
			ind_tmp = silk_SMULWB(int(inv_quant_step_size_Q6), res_Q10)
			ind_tmp = silk_LIMIT(ind_tmp, 0-SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT, SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT-1)
			ind[j][i] = int8(ind_tmp)
			rates_Q5 = int(ec_ix[i]) + ind_tmp

			// compute outputs for ind_tmp and ind_tmp + 1
			out0_Q10 = out0_Q10_table[ind_tmp+SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT]
			out1_Q10 = out1_Q10_table[ind_tmp+SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT]

			out0_Q10 = int(silk_ADD16(int16(out0_Q10), int16(pred_Q10)))
			out1_Q10 = int(silk_ADD16(int16(out1_Q10), int16(pred_Q10)))
			prev_out_Q10[j] = int16(out0_Q10)
			prev_out_Q10[j+nStates] = int16(out1_Q10)

			// compute RD for ind_tmp and ind_tmp + 1
			if ind_tmp+1 >= SilkConstants.NLSF_QUANT_MAX_AMPLITUDE {
				if ind_tmp+1 == SilkConstants.NLSF_QUANT_MAX_AMPLITUDE {
					rate0_Q5 = int(ec_rates_Q5[rates_Q5+SilkConstants.NLSF_QUANT_MAX_AMPLITUDE])
					rate1_Q5 = 280
				} else {
					rate0_Q5 = silk_SMLABB(280-(43*SilkConstants.NLSF_QUANT_MAX_AMPLITUDE), 43, ind_tmp)
					rate1_Q5 = int(silk_ADD16(int16(rate0_Q5), 43))
				}
			} else if ind_tmp <= 0-SilkConstants.NLSF_QUANT_MAX_AMPLITUDE {
				if ind_tmp == 0-SilkConstants.NLSF_QUANT_MAX_AMPLITUDE {
					rate0_Q5 = 280
					rate1_Q5 = int(ec_rates_Q5[rates_Q5+1+SilkConstants.NLSF_QUANT_MAX_AMPLITUDE])
				} else {
					rate0_Q5 = silk_SMLABB(280-43*SilkConstants.NLSF_QUANT_MAX_AMPLITUDE, -43, ind_tmp)
					rate1_Q5 = int(silk_SUB16(int16(rate0_Q5), 43))
				}
			} else {
				rate0_Q5 = int(ec_rates_Q5[rates_Q5+SilkConstants.NLSF_QUANT_MAX_AMPLITUDE])
				rate1_Q5 = int(ec_rates_Q5[rates_Q5+1+SilkConstants.NLSF_QUANT_MAX_AMPLITUDE])
			}

			RD_tmp_Q25 = RD_Q25[j]
			diff_Q10 = int(silk_SUB16(int16(in_Q10), int16(out0_Q10)))

			RD_Q25[j] = silk_SMLABB(silk_MLA(RD_tmp_Q25, silk_SMULBB(diff_Q10, diff_Q10), int(w_Q5[i])), mu_Q20, rate0_Q5)
			diff_Q10 = int(silk_SUB16(int16(in_Q10), int16(out1_Q10)))
			RD_Q25[j+nStates] = silk_SMLABB(silk_MLA(RD_tmp_Q25, silk_SMULBB(diff_Q10, diff_Q10), int(w_Q5[i])), mu_Q20, rate1_Q5)
		}

		if nStates <= (SilkConstants.NLSF_QUANT_DEL_DEC_STATES >> 1) {
			// double number of states and copy
			for j = 0; j < nStates; j++ {
				ind[j+nStates][i] = int8(ind[j][i] + 1)
			}
			nStates = silk_LSHIFT(nStates, 1)

			for j = nStates; j < SilkConstants.NLSF_QUANT_DEL_DEC_STATES; j++ {
				ind[j][i] = ind[j-nStates][i]
			}
		} else if i > 0 {
			// sort lower and upper half of RD_Q25, pairwise
			for j = 0; j < SilkConstants.NLSF_QUANT_DEL_DEC_STATES; j++ {
				if RD_Q25[j] > RD_Q25[j+SilkConstants.NLSF_QUANT_DEL_DEC_STATES] {
					RD_max_Q25[j] = RD_Q25[j]
					RD_min_Q25[j] = RD_Q25[j+SilkConstants.NLSF_QUANT_DEL_DEC_STATES]
					RD_Q25[j] = RD_min_Q25[j]
					RD_Q25[j+SilkConstants.NLSF_QUANT_DEL_DEC_STATES] = RD_max_Q25[j]

					// swap prev_out values
					out0_Q10 = int(prev_out_Q10[j])
					prev_out_Q10[j] = prev_out_Q10[j+SilkConstants.NLSF_QUANT_DEL_DEC_STATES]
					prev_out_Q10[j+SilkConstants.NLSF_QUANT_DEL_DEC_STATES] = int16(out0_Q10)
					ind_sort[j] = j + SilkConstants.NLSF_QUANT_DEL_DEC_STATES
				} else {
					RD_min_Q25[j] = RD_Q25[j]
					RD_max_Q25[j] = RD_Q25[j+SilkConstants.NLSF_QUANT_DEL_DEC_STATES]
					ind_sort[j] = j
				}
			}

			// compare the highest RD values of the winning half with the lowest one in the losing half, and copy if necessary
			// afterwards ind_sort[] will contain the indices of the NLSF_QUANT_DEL_DEC_STATES winning RD values
			for {
				min_max_Q25 = math.MaxInt32
				max_min_Q25 = 0
				ind_min_max = 0
				ind_max_min = 0

				for j = 0; j < SilkConstants.NLSF_QUANT_DEL_DEC_STATES; j++ {
					if min_max_Q25 > RD_max_Q25[j] {
						min_max_Q25 = RD_max_Q25[j]
						ind_min_max = j
					}
					if max_min_Q25 < RD_min_Q25[j] {
						max_min_Q25 = RD_min_Q25[j]
						ind_max_min = j
					}
				}

				if min_max_Q25 >= max_min_Q25 {
					break
				}

				// copy ind_min_max to ind_max_min
				ind_sort[ind_max_min] = ind_sort[ind_min_max] ^ SilkConstants.NLSF_QUANT_DEL_DEC_STATES
				RD_Q25[ind_max_min] = RD_Q25[ind_min_max+SilkConstants.NLSF_QUANT_DEL_DEC_STATES]
				prev_out_Q10[ind_max_min] = prev_out_Q10[ind_min_max+SilkConstants.NLSF_QUANT_DEL_DEC_STATES]
				RD_min_Q25[ind_max_min] = 0
				RD_max_Q25[ind_min_max] = math.MaxInt32
				//	System.arraycopy(ind[ind_min_max], 0, ind[ind_max_min], 0, order)
				copy(ind[ind_max_min], ind[ind_min_max][:order])
			}

			// increment index if it comes from the upper half
			for j = 0; j < SilkConstants.NLSF_QUANT_DEL_DEC_STATES; j++ {
				x := silk_RSHIFT(ind_sort[j], SilkConstants.NLSF_QUANT_DEL_DEC_STATES_LOG2)
				ind[j][i] += int8(x)
			}
		} else {
			// i == 0
			break
		}
	}

	// last sample: find winner, copy indices and return RD value
	ind_tmp = 0
	min_Q25 = math.MaxInt32
	for j = 0; j < 2*SilkConstants.NLSF_QUANT_DEL_DEC_STATES; j++ {
		if min_Q25 > RD_Q25[j] {
			min_Q25 = RD_Q25[j]
			ind_tmp = j
		}
	}

	for j = 0; j < int(order); j++ {
		indices[j] = ind[ind_tmp&(SilkConstants.NLSF_QUANT_DEL_DEC_STATES-1)][j]
		OpusAssert(int(indices[j]) >= 0-SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT)
		OpusAssert(int(indices[j]) <= SilkConstants.NLSF_QUANT_MAX_AMPLITUDE_EXT)
	}

	indices[0] = int8(int(indices[0]) + silk_RSHIFT(ind_tmp, SilkConstants.NLSF_QUANT_DEL_DEC_STATES_LOG2))
	OpusAssert(indices[0] <= NLSF_QUANT_MAX_AMPLITUDE_EXT)
	OpusAssert(min_Q25 >= 0)
	return min_Q25
}

func silk_NLSF_encode(NLSFIndices []int8, pNLSF_Q15 []int16, psNLSF_CB *NLSFCodebook, pW_QW []int16, NLSF_mu_Q20 int, nSurvivors int, signalType int) int {

	var i, s, ind1, prob_Q8, bits_q7 int
	var W_tmp_Q9 int
	var err_Q26 []int
	var RD_Q25 []int
	var tempIndices1 []int
	var tempIndices2 [][]int8
	res_Q15 := make([]int16, psNLSF_CB.order)
	res_Q10 := make([]int16, psNLSF_CB.order)
	NLSF_tmp_Q15 := make([]int16, psNLSF_CB.order)
	W_tmp_QW := make([]int16, psNLSF_CB.order)
	W_adj_Q5 := make([]int16, psNLSF_CB.order)
	pred_Q8 := make([]int16, psNLSF_CB.order)
	ec_ix := make([]int16, psNLSF_CB.order)
	pCB := psNLSF_CB.CB1_NLSF_Q8
	var iCDF_ptr int
	var pCB_element int

	OpusAssert(nSurvivors <= SilkConstants.NLSF_VQ_MAX_SURVIVORS)
	OpusAssert(signalType >= 0 && signalType <= 2)
	OpusAssert(NLSF_mu_Q20 <= 32767 && NLSF_mu_Q20 >= 0)

	// NLSF stabilization
	silk_NLSF_stabilize(pNLSF_Q15, psNLSF_CB.deltaMin_Q15, int(psNLSF_CB.order))

	// First stage: VQ
	err_Q26 = make([]int, psNLSF_CB.nVectors)
	silk_NLSF_VQ(err_Q26, pNLSF_Q15, psNLSF_CB.CB1_NLSF_Q8, int(psNLSF_CB.nVectors), int(psNLSF_CB.order))

	// Sort the quantization errors
	tempIndices1 = make([]int, nSurvivors)
	silk_insertion_sort_increasing(err_Q26, tempIndices1, int(psNLSF_CB.nVectors), nSurvivors)

	RD_Q25 = make([]int, nSurvivors)
	tempIndices2 = InitTwoDimensionalArrayByte(nSurvivors, SilkConstants.MAX_LPC_ORDER)

	// Loop over survivors
	for s = 0; s < nSurvivors; s++ {
		ind1 = tempIndices1[s]

		// Residual after first stage
		pCB_element = ind1 * int(psNLSF_CB.order) // opt: potential 1:2 partitioned buffer
		for i = 0; i < int(psNLSF_CB.order); i++ {
			NLSF_tmp_Q15[i] = silk_LSHIFT16(pCB[pCB_element+i], 7)
			res_Q15[i] = int16(pNLSF_Q15[i] - NLSF_tmp_Q15[i])
		}

		// Weights from codebook vector
		silk_NLSF_VQ_weights_laroia(W_tmp_QW, NLSF_tmp_Q15, int(psNLSF_CB.order))

		// Apply square-rooted weights
		for i = 0; i < int(psNLSF_CB.order); i++ {
			W_tmp_Q9 = silk_SQRT_APPROX(silk_LSHIFT(int(W_tmp_QW[i]), 18-SilkConstants.NLSF_W_Q))
			res_Q10[i] = int16(silk_RSHIFT(silk_SMULBB(int(res_Q15[i]), W_tmp_Q9), 14))
		}

		// Modify input weights accordingly
		for i = 0; i < int(psNLSF_CB.order); i++ {
			W_adj_Q5[i] = int16(silk_DIV32_16(silk_LSHIFT(int(pW_QW[i]), 5), int(W_tmp_QW[i])))
		}

		// Unpack entropy table indices and predictor for current CB1 index
		silk_NLSF_unpack(ec_ix, pred_Q8, psNLSF_CB, ind1)

		// Trellis quantizer
		RD_Q25[s] = silk_NLSF_del_dec_quant(
			tempIndices2[s],
			res_Q10,
			W_adj_Q5,
			pred_Q8,
			ec_ix,
			psNLSF_CB.ec_Rates_Q5,
			int(psNLSF_CB.quantStepSize_Q16),
			psNLSF_CB.invQuantStepSize_Q6,
			NLSF_mu_Q20,
			psNLSF_CB.order)

		// Add rate for first stage
		iCDF_ptr = (signalType >> 1) * int(psNLSF_CB.nVectors)

		if ind1 == 0 {
			prob_Q8 = 256 - int(psNLSF_CB.CB1_iCDF[iCDF_ptr+ind1])
		} else {
			prob_Q8 = int(psNLSF_CB.CB1_iCDF[iCDF_ptr+ind1-1] - psNLSF_CB.CB1_iCDF[iCDF_ptr+ind1])
		}

		bits_q7 = (8 << 7) - silk_lin2log(prob_Q8)
		RD_Q25[s] = silk_SMLABB(RD_Q25[s], bits_q7, silk_RSHIFT(NLSF_mu_Q20, 2))
	}

	// Find the lowest rate-distortion error
	bestIndex := make([]int, 1)
	silk_insertion_sort_increasing(RD_Q25, bestIndex, nSurvivors, 1)

	NLSFIndices[0] = int8(tempIndices1[bestIndex[0]])
	//System.arraycopy(tempIndices2[bestIndex[0]], 0, NLSFIndices, 1, psNLSF_CB.order)
	copy(NLSFIndices[1:], tempIndices2[bestIndex[0]][0:psNLSF_CB.order])

	// Decode
	silk_NLSF_decode(pNLSF_Q15, NLSFIndices, psNLSF_CB)

	return RD_Q25[0]
}

func silk_NLSF2A_find_poly(o []int, cLSF []int, cLSF_ptr int, dd int) {
	var k, n, ftmp int

	o[0] = silk_LSHIFT(1, QA16)
	o[1] = 0 - cLSF[cLSF_ptr]
	for k = 1; k < dd; k++ {
		ftmp = cLSF[cLSF_ptr+(2*k)]
		/* QA*/
		o[k+1] = silk_LSHIFT(o[k-1], 1) - int(silk_RSHIFT_ROUND64(silk_SMULL(ftmp, o[k]), QA16))
		for n = k; n > 1; n-- {
			o[n] += o[n-2] - int(silk_RSHIFT_ROUND64(silk_SMULL(ftmp, o[n-1]), QA16))
		}
		o[1] -= ftmp
	}
}

var ordering16 = []int8{0, 15, 8, 7, 4, 11, 12, 3, 2, 13, 10, 5, 6, 9, 14, 1}
var ordering10 = []int8{0, 9, 6, 3, 4, 5, 8, 1, 2, 7}

func silk_NLSF2A(a_Q12 []int16, NLSF []int16, d int) {

	var ordering []int8
	if d == 16 {
		ordering = ordering16
	} else {
		ordering = ordering10
	}

	OpusAssert(LSF_COS_TAB_SZ == 128)
	OpusAssert(d == 10 || d == 16)
	var i int
	cos_LSF_QA := make([]int, d)
	for k := 0; k < d; k++ {
		OpusAssert(int(NLSF[k]) >= 0)
		f_int := int(NLSF[k]) >> (15 - 7)
		f_frac := int(NLSF[k]) - (f_int << (15 - 7))
		OpusAssert(f_int >= 0)
		OpusAssert(f_int < LSF_COS_TAB_SZ)
		cos_val := SilkTables.Silk_LSFCosTab_Q12[f_int]
		delta := SilkTables.Silk_LSFCosTab_Q12[f_int+1] - cos_val
		cos_LSF_QA[ordering[k]] = int(silk_RSHIFT_ROUND(int(cos_val)<<8+int(delta)*int(f_frac), 20-QA16))
	}

	dd := d / 2
	P := make([]int, dd+1)
	Q := make([]int, dd+1)
	a32_QA1 := make([]int, d)

	P[0] = 1 << QA16
	P[1] = -cos_LSF_QA[0]
	silk_NLSF2A_find_poly(P, cos_LSF_QA, 0, dd)

	Q[0] = 1 << QA16
	Q[1] = -cos_LSF_QA[1]
	silk_NLSF2A_find_poly(Q, cos_LSF_QA, 1, dd)

	for k := 0; k < dd; k++ {
		Ptmp := P[k+1] + P[k]
		Qtmp := Q[k+1] - Q[k]
		a32_QA1[k] = -Qtmp - Ptmp
		a32_QA1[d-k-1] = Qtmp - Ptmp
	}

	for i = 0; i < 10; i++ {
		maxabs := int(0)
		idx := 0
		for k := 0; k < d; k++ {
			absval := silk_abs(a32_QA1[k])
			if absval > maxabs {
				maxabs = absval
				idx = k
			}
		}
		maxabs = int(silk_RSHIFT_ROUND(int(maxabs), int(QA16+1-12)))

		if maxabs > math.MaxInt16 {
			maxabs = silk_min_int(maxabs, 163838)
			sc_Q16 := int(math.Trunc(0.999*65536.0+0.5)) - silk_DIV32(int(maxabs-math.MaxInt16)<<14, silk_RSHIFT32(silk_MUL(maxabs, int(idx+1)), 2))
			silk_bwexpander_32(a32_QA1, d, sc_Q16)
		} else {
			break
		}
	}

	if i == 10 {
		for k := 0; k < d; k++ {
			a_Q12[k] = int16(silk_SAT16(int(silk_RSHIFT_ROUND(int(a32_QA1[k]), int(QA16+1-12)))))
			a32_QA1[k] = int(a_Q12[k]) << (QA16 + 1 - 12)
		}
	} else {
		for k := 0; k < d; k++ {
			a_Q12[k] = int16(silk_RSHIFT_ROUND(int(a32_QA1[k]), int(QA16+1-12)))
		}
	}

	for i := 0; i < SilkConstants.MAX_LPC_STABILIZE_ITERATIONS; i++ {
		if silk_LPC_inverse_pred_gain(a_Q12, d) < int((1.0/SilkConstants.MAX_PREDICTION_POWER_GAIN)*1073741824.0+0.5) {
			silk_bwexpander_32(a32_QA1, d, 65536-int(2<<i))
			for k := 0; k < d; k++ {
				a_Q12[k] = int16(silk_RSHIFT_ROUND(int(a32_QA1[k]), int(QA16+1-12)))
			}
		} else {
			break
		}
	}
}

func silk_A2NLSF_trans_poly(p []int, dd int) {
	for k := 2; k <= dd; k++ {
		for n := dd; n > k; n-- {
			p[n-2] -= p[n]
		}
		p[k-2] -= p[k] << 1
	}
}

func silk_A2NLSF_eval_poly(p []int, x int, dd int) int {
	x_Q16 := x << 4
	y32 := p[dd]
	if dd == 8 {
		y32 = silk_SMLAWW(p[7], y32, x_Q16)
		y32 = silk_SMLAWW(p[6], y32, x_Q16)
		y32 = silk_SMLAWW(p[5], y32, x_Q16)
		y32 = silk_SMLAWW(p[4], y32, x_Q16)
		y32 = silk_SMLAWW(p[3], y32, x_Q16)
		y32 = silk_SMLAWW(p[2], y32, x_Q16)
		y32 = silk_SMLAWW(p[1], y32, x_Q16)
		y32 = silk_SMLAWW(p[0], y32, x_Q16)
	} else {
		for n := dd - 1; n >= 0; n-- {
			y32 = silk_SMLAWW(p[n], y32, x_Q16)
		}
	}
	return y32
}

func silk_A2NLSF_init(a_Q16 []int, P []int, Q []int, dd int) {
	P[dd] = 1 << 16
	Q[dd] = 1 << 16
	for k := 0; k < dd; k++ {
		P[k] = -a_Q16[dd-k-1] - a_Q16[dd+k]
		Q[k] = -a_Q16[dd-k-1] + a_Q16[dd+k]
	}
	for k := dd; k > 0; k-- {
		P[k-1] -= P[k]
		Q[k-1] += Q[k]
	}
	silk_A2NLSF_trans_poly(P, dd)
	silk_A2NLSF_trans_poly(Q, dd)
}

func silk_A2NLSF(NLSF []int16, a_Q16 []int, d int) {
	var i, k, m, dd, root_ix, ffrac int
	var xlo, xhi, xmid int
	var ylo, yhi, ymid, thr int
	var nom, den int
	P := make([]int, SilkConstants.SILK_MAX_ORDER_LPC/2+1)
	Q := make([]int, SilkConstants.SILK_MAX_ORDER_LPC/2+1)
	PQ := make([][]int, 2)
	var p []int

	/* Store pointers to array */
	PQ[0] = P
	PQ[1] = Q

	dd = silk_RSHIFT(d, 1)

	silk_A2NLSF_init(a_Q16, P, Q, dd)

	/* Find roots, alternating between P and Q */
	p = P
	/* Pointer to polynomial */

	xlo = int(silk_LSFCosTab_Q12[0])
	/* Q12*/
	ylo = silk_A2NLSF_eval_poly(p, xlo, dd)

	if ylo < 0 {
		/* Set the first NLSF to zero and move on to the next */
		NLSF[0] = 0
		p = Q
		/* Pointer to polynomial */
		ylo = silk_A2NLSF_eval_poly(p, xlo, dd)
		root_ix = 1
		/* Index of current root */
	} else {
		root_ix = 0
		/* Index of current root */
	}
	k = 1
	/* Loop counter */
	i = 0
	/* Counter for bandwidth expansions applied */
	thr = 0
	for {
		/* Evaluate polynomial */
		xhi = int(silk_LSFCosTab_Q12[k])
		/* Q12 */
		yhi = silk_A2NLSF_eval_poly(p, xhi, dd)

		/* Detect zero crossing */
		if (ylo <= 0 && yhi >= thr) || (ylo >= 0 && yhi <= -thr) {
			if yhi == 0 {
				/* If the root lies exactly at the end of the current       */
				/* interval, look for the next root in the next interval    */
				thr = 1
			} else {
				thr = 0
			}
			/* Binary division */
			ffrac = -256
			for m = 0; m < BIN_DIV_STEPS_A2NLSF; m++ {
				/* Evaluate polynomial */
				xmid = silk_RSHIFT_ROUND(xlo+xhi, 1)
				ymid = silk_A2NLSF_eval_poly(p, xmid, dd)

				/* Detect zero crossing */
				if (ylo <= 0 && ymid >= 0) || (ylo >= 0 && ymid <= 0) {
					/* Reduce frequency */
					xhi = xmid
					yhi = ymid
				} else {
					/* Increase frequency */
					xlo = xmid
					ylo = ymid
					ffrac = silk_ADD_RSHIFT(ffrac, 128, m)
				}
			}

			/* Interpolate */
			if silk_abs(ylo) < 65536 {
				/* Avoid dividing by zero */
				den = ylo - yhi
				nom = silk_LSHIFT(ylo, 8-BIN_DIV_STEPS_A2NLSF) + silk_RSHIFT(den, 1)
				if den != 0 {
					ffrac += silk_DIV32(nom, den)
				}
			} else {
				/* No risk of dividing by zero because abs(ylo - yhi) >= abs(ylo) >= 65536 */
				ffrac += silk_DIV32(ylo, silk_RSHIFT(ylo-yhi, 8-BIN_DIV_STEPS_A2NLSF))
			}
			NLSF[root_ix] = int16(silk_min_32(silk_LSHIFT(k, 8)+ffrac, math.MaxInt16))

			OpusAssert(NLSF[root_ix] >= 0)

			root_ix++
			/* Next root */
			if root_ix >= d {
				/* Found all roots */
				break
			}

			/* Alternate pointer to polynomial */
			p = PQ[root_ix&1]

			/* Evaluate polynomial */
			xlo = int(silk_LSFCosTab_Q12[k-1])
			/* Q12*/
			ylo = silk_LSHIFT(1-(root_ix&2), 12)
		} else {
			/* Increment loop counter */
			k++
			xlo = xhi
			ylo = yhi
			thr = 0

			if k > SilkConstants.LSF_COS_TAB_SZ {
				i++
				if i > MAX_ITERATIONS_A2NLSF {
					/* Set NLSFs to white spectrum and exit */
					NLSF[0] = int16(silk_DIV32_16(1<<15, int(d+1)))
					for k = 1; k < d; k++ {
						NLSF[k] = int16(silk_SMULBB(k+1, int(NLSF[0])))
					}
					return
				}

				/* Error: Apply progressively more bandwidth expansion and run again */
				silk_bwexpander_32(a_Q16, d, 65536-silk_SMULBB(10+i, i))
				/* 10_Q16 = 0.00015*/

				silk_A2NLSF_init(a_Q16, P, Q, dd)
				p = P
				/* Pointer to polynomial */
				xlo = int(silk_LSFCosTab_Q12[0])
				/* Q12*/
				ylo = silk_A2NLSF_eval_poly(p, xlo, dd)
				if ylo < 0 {
					/* Set the first NLSF to zero and move on to the next */
					NLSF[0] = 0
					p = Q
					/* Pointer to polynomial */
					ylo = silk_A2NLSF_eval_poly(p, xlo, dd)
					root_ix = 1
					/* Index of current root */
				} else {
					root_ix = 0
					/* Index of current root */
				}
				k = 1
				/* Reset loop counter */
			}
		}
	}
}

func silk_process_NLSFs(psEncC *SilkChannelEncoder, PredCoef_Q12 [][]int16, pNLSF_Q15 []int16, prev_NLSFq_Q15 []int16) {

	var i int
	var doInterpolate bool
	var NLSF_mu_Q20 int
	var i_sqr_Q15 int
	pNLSF0_temp_Q15 := make([]int16, SilkConstants.MAX_LPC_ORDER)
	pNLSFW_QW := make([]int16, SilkConstants.MAX_LPC_ORDER)
	pNLSFW0_temp_QW := make([]int16, SilkConstants.MAX_LPC_ORDER)

	OpusAssert(psEncC.speech_activity_Q8 >= 0)
	OpusAssert(psEncC.speech_activity_Q8 <= (int(math.Trunc(1.0*float64(int64(1)<<(8)) + 0.5))))
	OpusAssert(psEncC.useInterpolatedNLSFs == 1 || psEncC.indices.NLSFInterpCoef_Q2 == (1<<2))

	/**
	 * ********************
	 */
	/* Calculate mu values */
	/**
	 * ********************
	 */
	/* NLSF_mu  = 0.003 - 0.0015 * psEnc.speech_activity; */
	NLSF_mu_Q20 = silk_SMLAWB((int(math.Trunc(0.003*float64(int64(1)<<(20)) + 0.5))), (int(math.Trunc(-0.001*float64(int64(1)<<(28)) + 0.5))), psEncC.speech_activity_Q8)
	if psEncC.nb_subfr == 2 {
		/* Multiply by 1.5 for 10 ms packets */
		NLSF_mu_Q20 = silk_ADD_RSHIFT(NLSF_mu_Q20, NLSF_mu_Q20, 1)
	}

	OpusAssert(NLSF_mu_Q20 > 0)
	OpusAssert(NLSF_mu_Q20 <= (int(math.Trunc(0.005*float64(int64(1)<<(20)) + 0.5))))

	/* Calculate NLSF weights */
	silk_NLSF_VQ_weights_laroia(pNLSFW_QW, pNLSF_Q15, psEncC.predictLPCOrder)

	/* Update NLSF weights for interpolated NLSFs */
	doInterpolate = (psEncC.useInterpolatedNLSFs == 1) && (psEncC.indices.NLSFInterpCoef_Q2 < 4)
	if doInterpolate {
		/* Calculate the interpolated NLSF vector for the first half */
		silk_interpolate(pNLSF0_temp_Q15, prev_NLSFq_Q15, pNLSF_Q15,
			int(psEncC.indices.NLSFInterpCoef_Q2), psEncC.predictLPCOrder)

		/* Calculate first half NLSF weights for the interpolated NLSFs */
		silk_NLSF_VQ_weights_laroia(pNLSFW0_temp_QW, pNLSF0_temp_Q15, psEncC.predictLPCOrder)

		/* Update NLSF weights with contribution from first half */
		i_sqr_Q15 = silk_LSHIFT(silk_SMULBB(int(psEncC.indices.NLSFInterpCoef_Q2), int(psEncC.indices.NLSFInterpCoef_Q2)), 11)

		for i = 0; i < psEncC.predictLPCOrder; i++ {
			pNLSFW_QW[i] = int16(silk_SMLAWB(silk_RSHIFT(int(pNLSFW_QW[i]), 1), int(pNLSFW0_temp_QW[i]), i_sqr_Q15))
			OpusAssert(pNLSFW_QW[i] >= 1)
		}
	}

	//////////////////////////////////////////////////////////////////////////
	silk_NLSF_encode(psEncC.indices.NLSFIndices, pNLSF_Q15, psEncC.psNLSF_CB, pNLSFW_QW,
		NLSF_mu_Q20, psEncC.NLSF_MSVQ_Survivors, int(psEncC.indices.signalType))

	/* Convert quantized NLSFs back to LPC coefficients */
	silk_NLSF2A(PredCoef_Q12[1], pNLSF_Q15, psEncC.predictLPCOrder)

	if doInterpolate {
		/* Calculate the interpolated, quantized LSF vector for the first half */
		silk_interpolate(pNLSF0_temp_Q15, prev_NLSFq_Q15, pNLSF_Q15,
			int(psEncC.indices.NLSFInterpCoef_Q2), psEncC.predictLPCOrder)

		/* Convert back to LPC coefficients */
		silk_NLSF2A(PredCoef_Q12[0], pNLSF0_temp_Q15, psEncC.predictLPCOrder)

	} else {
		/* Copy LPC coefficients for first half from second half */
		//System.arraycopy(PredCoef_Q12[1], 0, PredCoef_Q12[0], 0, psEncC.predictLPCOrder)
		copy(PredCoef_Q12[0], PredCoef_Q12[1][:psEncC.predictLPCOrder])
	}

}
