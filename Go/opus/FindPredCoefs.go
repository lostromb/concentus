package opus

import (
	"math"
)

func silk_find_pred_coefs(
	psEnc *SilkChannelEncoder,
	psEncCtrl *SilkEncoderControl,
	res_pitch []int16,
	x []int16,
	x_ptr int,
	condCoding int,
) {
	var i int
	invGains_Q16 := make([]int, SilkConstants.MAX_NB_SUBFR)
	local_gains := make([]int, SilkConstants.MAX_NB_SUBFR)
	Wght_Q15 := make([]int, SilkConstants.MAX_NB_SUBFR)
	NLSF_Q15 := make([]int16, SilkConstants.MAX_LPC_ORDER)
	var x_ptr2 int
	var x_pre_ptr int
	var LPC_in_pre []int16
	var tmp, min_gain_Q16, minInvGain_Q30 int
	LTP_corrs_rshift := make([]int, SilkConstants.MAX_NB_SUBFR)

	/* weighting for weighted least squares */
	min_gain_Q16 = math.MaxInt32 >> 6
	for i = 0; i < psEnc.nb_subfr; i++ {
		min_gain_Q16 = silk_min(min_gain_Q16, psEncCtrl.Gains_Q16[i])
	}
	for i = 0; i < psEnc.nb_subfr; i++ {
		/* Divide to Q16 */
		OpusAssert(psEncCtrl.Gains_Q16[i] > 0)
		/* Invert and normalize gains, and ensure that maximum invGains_Q16 is within range of a 16 bit int */
		invGains_Q16[i] = silk_DIV32_varQ(min_gain_Q16, psEncCtrl.Gains_Q16[i], 16-2)

		/* Ensure Wght_Q15 a minimum value 1 */
		invGains_Q16[i] = silk_max(invGains_Q16[i], 363)

		/* Square the inverted gains */
		OpusAssert(invGains_Q16[i] == silk_SAT16(invGains_Q16[i]))
		tmp = silk_SMULWB(invGains_Q16[i], invGains_Q16[i])
		Wght_Q15[i] = silk_RSHIFT(tmp, 1)

		/* Invert the inverted and normalized gains */
		local_gains[i] = silk_DIV32(int(int32(1)<<16), invGains_Q16[i])
	}

	LPC_in_pre = make([]int16, psEnc.nb_subfr*psEnc.predictLPCOrder+psEnc.frame_length)
	if psEnc.indices.signalType == TYPE_VOICED {

		var WLTP []int

		/**
		 * *******
		 */
		/* VOICED */
		/**
		 * *******
		 */
		OpusAssert(psEnc.ltp_mem_length-psEnc.predictLPCOrder >= psEncCtrl.pitchL[0]+SilkConstants.LTP_ORDER/2)

		WLTP = make([]int, psEnc.nb_subfr*SilkConstants.LTP_ORDER*SilkConstants.LTP_ORDER)

		/* LTP analysis */
		boxed_codgain := &BoxedValueInt{psEncCtrl.LTPredCodGain_Q7}
		silk_find_LTP(psEncCtrl.LTPCoef_Q14, WLTP, boxed_codgain,
			res_pitch, psEncCtrl.pitchL, Wght_Q15, psEnc.subfr_length,
			psEnc.nb_subfr, psEnc.ltp_mem_length, LTP_corrs_rshift)
		psEncCtrl.LTPredCodGain_Q7 = boxed_codgain.Val

		/* Quantize LTP gain parameters */
		boxed_periodicity := &BoxedValueByte{psEnc.indices.PERIndex}
		boxed_gain := &BoxedValueInt{psEnc.sum_log_gain_Q7}
		silk_quant_LTP_gains(psEncCtrl.LTPCoef_Q14, psEnc.indices.LTPIndex, boxed_periodicity,
			boxed_gain, WLTP, psEnc.mu_LTP_Q9, psEnc.LTPQuantLowComplexity, psEnc.nb_subfr)
		psEnc.indices.PERIndex = boxed_periodicity.Val
		psEnc.sum_log_gain_Q7 = boxed_gain.Val

		/* Control LTP scaling */
		silk_LTP_scale_ctrl(psEnc, psEncCtrl, condCoding)

		/* Create LTP residual */
		silk_LTP_analysis_filter(LPC_in_pre, x, x_ptr-psEnc.predictLPCOrder, psEncCtrl.LTPCoef_Q14,
			psEncCtrl.pitchL, invGains_Q16, psEnc.subfr_length, psEnc.nb_subfr, psEnc.predictLPCOrder)

	} else {

		/**
		 * *********
		 */
		/* UNVOICED */
		/**
		 * *********
		 */
		/* Create signal with prepended subframes, scaled by inverse gains */
		x_ptr2 = x_ptr - psEnc.predictLPCOrder
		x_pre_ptr = 0
		for i = 0; i < psEnc.nb_subfr; i++ {
			silk_scale_copy_vector16(LPC_in_pre, x_pre_ptr, x, x_ptr2, invGains_Q16[i],
				psEnc.subfr_length+psEnc.predictLPCOrder)
			x_pre_ptr += psEnc.subfr_length + psEnc.predictLPCOrder
			x_ptr2 += psEnc.subfr_length
		}

		MemSetLen(psEncCtrl.LTPCoef_Q14, 0, psEnc.nb_subfr*SilkConstants.LTP_ORDER)
		psEncCtrl.LTPredCodGain_Q7 = 0
		psEnc.sum_log_gain_Q7 = 0
	}

	/* Limit on total predictive coding gain */
	if psEnc.first_frame_after_reset != 0 {
		minInvGain_Q30 = int((1.0/float64(SilkConstants.MAX_PREDICTION_POWER_GAIN_AFTER_RESET))*float64(int64(1)<<(30)) + 0.5)
	} else {
		minInvGain_Q30 = silk_log2lin(silk_SMLAWB(16<<7, psEncCtrl.LTPredCodGain_Q7, int(math.Trunc((1.0/3)*float64(int64(1)<<(16))+0.5))))
		/* Q16 */
		minInvGain_Q30 = silk_DIV32_varQ(minInvGain_Q30,
			silk_SMULWW(int(float64(SilkConstants.MAX_PREDICTION_POWER_GAIN)*float64(int64(1)<<(0))+0.5),
				silk_SMLAWB(int(math.Trunc(0.25*float64(int64(1)<<(18))+0.5)), int(math.Trunc(0.75*float64(int64(1)<<(18))+0.5)), psEncCtrl.coding_quality_Q14)), 14)
	}

	silk_find_LPC(psEnc, NLSF_Q15, LPC_in_pre, minInvGain_Q30)

	/* Quantize LSFs */
	silk_process_NLSFs(psEnc, psEncCtrl.PredCoef_Q12, NLSF_Q15, psEnc.prev_NLSFq_Q15)

	/* Calculate residual energy using quantized LPC coefficients */
	silk_residual_energy(psEncCtrl.ResNrg[:], psEncCtrl.ResNrgQ[:], LPC_in_pre, psEncCtrl.PredCoef_Q12, local_gains,
		psEnc.subfr_length, psEnc.nb_subfr, psEnc.predictLPCOrder)

	/* Copy to prediction struct for use in next frame for interpolation */
	//	System.arraycopy(NLSF_Q15, 0, psEnc.prev_NLSFq_Q15, 0, SilkConstants.MAX_LPC_ORDER)
	//
	copy(psEnc.prev_NLSFq_Q15, NLSF_Q15[:SilkConstants.MAX_LPC_ORDER])
}
