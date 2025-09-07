package silk

import (
	"math"

	"github.com/lostromb/concentus/go/comm"
)

func silk_quant_LTP_gains(
	B_Q14 []int16,
	cbk_index []int8,
	periodicity_index *comm.BoxedValueByte,
	sum_log_gain_Q7 *comm.BoxedValueInt,
	W_Q18 []int,
	mu_Q9 int,
	lowComplexity int,
	nb_subfr int) {
	var j, k, cbk_size int
	temp_idx := make([]int8, SilkConstants.MAX_NB_SUBFR)
	var cl_ptr_Q5 []int16
	var cbk_ptr_Q7 [][]int8
	var cbk_gain_ptr_Q7 []int16
	var b_Q14_ptr int
	var W_Q18_ptr int
	var rate_dist_Q14_subfr, rate_dist_Q14, min_rate_dist_Q14 int
	var sum_log_gain_tmp_Q7, best_sum_log_gain_Q7, max_gain_Q7, gain_Q7 int

	/**
	 * ************************************************
	 */
	/* iterate over different codebooks with different */
	/* rates/distortions, and choose best */
	/**
	 * ************************************************
	 */
	min_rate_dist_Q14 = math.MaxInt32
	best_sum_log_gain_Q7 = 0
	for k = 0; k < 3; k++ {
		/* Safety margin for pitch gain control, to take into account factors
		   such as state rescaling/rewhitening. */
		gain_safety := int(math.Trunc(0.4*float64(int64(1)<<(7)) + 0.5))

		cl_ptr_Q5 = silk_LTP_gain_BITS_Q5_ptrs[k]
		cbk_ptr_Q7 = silk_LTP_vq_ptrs_Q7[k]
		cbk_gain_ptr_Q7 = silk_LTP_vq_gain_ptrs_Q7[k]
		cbk_size = int(silk_LTP_vq_sizes[k])

		/* Set up pointer to first subframe */
		W_Q18_ptr = 0
		b_Q14_ptr = 0

		rate_dist_Q14 = 0
		sum_log_gain_tmp_Q7 = sum_log_gain_Q7.Val
		for j = 0; j < nb_subfr; j++ {
			max_gain_Q7 = inlines.Silk_log2lin((int(float64(TuningParameters.MAX_SUM_LOG_GAIN_DB/6.0)*float64(int64(1)<<(7))+0.5)-sum_log_gain_tmp_Q7)+
				int(math.Trunc(7*float64(int64(1)<<(7))+0.5))) - gain_safety

			temp_idx_box := &comm.BoxedValueByte{temp_idx[j]}
			rate_dist_Q14_subfr_box := &comm.BoxedValueInt{0}
			gain_Q7_box := &comm.BoxedValueInt{0}
			silk_VQ_WMat_EC(
				temp_idx_box,            /* O    index of best codebook vector                           */
				rate_dist_Q14_subfr_box, /* O    best weighted quantization error + mu * rate            */
				gain_Q7_box,             /* O    sum of absolute LTP coefficients                        */
				B_Q14,
				b_Q14_ptr, /* I    input vector to be quantized                            */
				W_Q18,
				W_Q18_ptr,       /* I    weighting matrix                                        */
				cbk_ptr_Q7,      /* I    codebook                                                */
				cbk_gain_ptr_Q7, /* I    codebook effective gains                                */
				cl_ptr_Q5,       /* I    code length for each codebook vector                    */
				mu_Q9,           /* I    tradeoff between weighted error and rate                */
				max_gain_Q7,     /* I    maximum sum of absolute LTP coefficients                */
				cbk_size,
			)
			rate_dist_Q14_subfr = rate_dist_Q14_subfr_box.Val
			gain_Q7 = gain_Q7_box.Val
			temp_idx[j] = temp_idx_box.Val

			rate_dist_Q14 = inlines.Silk_ADD_POS_SAT32(rate_dist_Q14, rate_dist_Q14_subfr)
			sum_log_gain_tmp_Q7 = inlines.Silk_max(0, sum_log_gain_tmp_Q7+
				inlines.Silk_lin2log(gain_safety+gain_Q7)-(int(math.Trunc(7*float64(int64(1)<<(7))+0.5))))

			b_Q14_ptr += SilkConstants.LTP_ORDER
			W_Q18_ptr += SilkConstants.LTP_ORDER * SilkConstants.LTP_ORDER
		}

		/* Avoid never finding a codebook */
		rate_dist_Q14 = inlines.Silk_min(math.MaxInt32-1, rate_dist_Q14)

		if rate_dist_Q14 < min_rate_dist_Q14 {
			min_rate_dist_Q14 = rate_dist_Q14
			periodicity_index.Val = int8(k)
			//System.arraycopy(temp_idx, 0, cbk_index, 0, nb_subfr)
			copy(cbk_index, temp_idx[:nb_subfr])
			best_sum_log_gain_Q7 = sum_log_gain_tmp_Q7
		}

		/* Break early in low-complexity mode if rate distortion is below threshold */
		if lowComplexity != 0 && (rate_dist_Q14 < int(silk_LTP_gain_middle_avg_RD_Q14)) {
			break
		}
	}

	cbk_ptr_Q7 = silk_LTP_vq_ptrs_Q7[periodicity_index.Val]

	for j = 0; j < nb_subfr; j++ {
		for k = 0; k < SilkConstants.LTP_ORDER; k++ {
			B_Q14[j*SilkConstants.LTP_ORDER+k] = int16(inlines.Silk_LSHIFT(int(cbk_ptr_Q7[cbk_index[j]][k]), 7))
		}
	}

	sum_log_gain_Q7.Val = best_sum_log_gain_Q7
}
