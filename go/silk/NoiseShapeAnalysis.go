package silk

import (
	"math"

	"github.com/lostromb/concentus/go/comm"
)

func warped_gain(coefs_Q24 []int, lambda_Q16 int, order int) int {
	var i int
	var gain_Q24 int

	lambda_Q16 = -lambda_Q16
	gain_Q24 = coefs_Q24[order-1]
	for i = order - 2; i >= 0; i-- {
		gain_Q24 = inlines.Silk_SMLAWB(coefs_Q24[i], gain_Q24, lambda_Q16)
	}
	gain_Q24 = inlines.Silk_SMLAWB((1 << 24), gain_Q24, -lambda_Q16)
	return inlines.Silk_INVERSE32_varQ(gain_Q24, 40)
}

func limit_warped_coefs(coefs_syn_Q24 []int, coefs_ana_Q24 []int, lambda_Q16 int, limit_Q24 int, order int) {
	var i, iter, ind int
	var tmp, maxabs_Q24, chirp_Q16, gain_syn_Q16, gain_ana_Q16 int
	var nom_Q16, den_Q24 int

	lambda_Q16 = -lambda_Q16
	for i = order - 1; i > 0; i-- {
		coefs_syn_Q24[i-1] = inlines.Silk_SMLAWB(coefs_syn_Q24[i-1], coefs_syn_Q24[i], lambda_Q16)
		coefs_ana_Q24[i-1] = inlines.Silk_SMLAWB(coefs_ana_Q24[i-1], coefs_ana_Q24[i], lambda_Q16)
	}
	lambda_Q16 = -lambda_Q16
	nom_Q16 = inlines.Silk_SMLAWB((1 << 16), -lambda_Q16, lambda_Q16)
	den_Q24 = inlines.Silk_SMLAWB((1 << 24), coefs_syn_Q24[0], lambda_Q16)
	gain_syn_Q16 = inlines.Silk_DIV32_varQ(nom_Q16, den_Q24, 24)
	den_Q24 = inlines.Silk_SMLAWB((1 << 24), coefs_ana_Q24[0], lambda_Q16)
	gain_ana_Q16 = inlines.Silk_DIV32_varQ(nom_Q16, den_Q24, 24)
	for i = 0; i < order; i++ {
		coefs_syn_Q24[i] = inlines.Silk_SMULWW(gain_syn_Q16, coefs_syn_Q24[i])
		coefs_ana_Q24[i] = inlines.Silk_SMULWW(gain_ana_Q16, coefs_ana_Q24[i])
	}

	for iter = 0; iter < 10; iter++ {
		maxabs_Q24 = -1
		for i = 0; i < order; i++ {
			tmp = inlines.Silk_max(inlines.Silk_abs_int(coefs_syn_Q24[i]), inlines.Silk_abs_int(coefs_ana_Q24[i]))
			if tmp > maxabs_Q24 {
				maxabs_Q24 = tmp
				ind = i
			}
		}
		if maxabs_Q24 <= limit_Q24 {
			return
		}

		for i = 1; i < order; i++ {
			coefs_syn_Q24[i-1] = inlines.Silk_SMLAWB(coefs_syn_Q24[i-1], coefs_syn_Q24[i], lambda_Q16)
			coefs_ana_Q24[i-1] = inlines.Silk_SMLAWB(coefs_ana_Q24[i-1], coefs_ana_Q24[i], lambda_Q16)
		}
		gain_syn_Q16 = inlines.Silk_INVERSE32_varQ(gain_syn_Q16, 32)
		gain_ana_Q16 = inlines.Silk_INVERSE32_varQ(gain_ana_Q16, 32)
		for i = 0; i < order; i++ {
			coefs_syn_Q24[i] = inlines.Silk_SMULWW(gain_syn_Q16, coefs_syn_Q24[i])
			coefs_ana_Q24[i] = inlines.Silk_SMULWW(gain_ana_Q16, coefs_ana_Q24[i])
		}

		chirp_Q16 = int(math.Trunc(0.99*float64(int64(1)<<(16))+0.5)) - inlines.Silk_DIV32_varQ(
			inlines.Silk_SMULWB(maxabs_Q24-limit_Q24, inlines.Silk_SMLABB(int(math.Trunc(0.8*float64(int64(1)<<(10))+0.5)), int(math.Trunc(0.1*float64(int64(1)<<10)+0.5)), iter)),
			inlines.Silk_MUL(maxabs_Q24, ind+1), 22)

		silk_bwexpander_32(coefs_syn_Q24, order, chirp_Q16)
		silk_bwexpander_32(coefs_ana_Q24, order, chirp_Q16)

		lambda_Q16 = -lambda_Q16
		for i = order - 1; i > 0; i-- {
			coefs_syn_Q24[i-1] = inlines.Silk_SMLAWB(coefs_syn_Q24[i-1], coefs_syn_Q24[i], lambda_Q16)
			coefs_ana_Q24[i-1] = inlines.Silk_SMLAWB(coefs_ana_Q24[i-1], coefs_ana_Q24[i], lambda_Q16)
		}
		lambda_Q16 = -lambda_Q16
		nom_Q16 = inlines.Silk_SMLAWB((1 << 16), -lambda_Q16, lambda_Q16)
		den_Q24 = inlines.Silk_SMLAWB((1 << 24), coefs_syn_Q24[0], lambda_Q16)
		gain_syn_Q16 = inlines.Silk_DIV32_varQ(nom_Q16, den_Q24, 24)
		den_Q24 = inlines.Silk_SMLAWB((1 << 24), coefs_ana_Q24[0], lambda_Q16)
		gain_ana_Q16 = inlines.Silk_DIV32_varQ(nom_Q16, den_Q24, 24)
		for i = 0; i < order; i++ {
			coefs_syn_Q24[i] = inlines.Silk_SMULWW(gain_syn_Q16, coefs_syn_Q24[i])
			coefs_ana_Q24[i] = inlines.Silk_SMULWW(gain_ana_Q16, coefs_ana_Q24[i])
		}
	}
	inlines.OpusAssert(false)
}

func silk_noise_shape_analysis(psEnc *SilkChannelEncoder, psEncCtrl *SilkEncoderControl, pitch_res []int16, pitch_res_ptr int, x []int16, x_ptr int) {
	psShapeSt := psEnc.SShape
	var k, i, nSamples, Qnrg int
	var b_Q14, warping_Q16, scale int
	var SNR_adj_dB_Q7, HarmBoost_Q16, HarmShapeGain_Q16, Tilt_Q16, tmp32 int
	var nrg, pre_nrg_Q30, log_energy_Q7, log_energy_prev_Q7, energy_variation_Q7 int
	var delta_Q16, BWExp1_Q16, BWExp2_Q16, gain_mult_Q16, gain_add_Q16, strength_Q16, b_Q8 int
	auto_corr := make([]int, MAX_SHAPE_LPC_ORDER+1)
	refl_coef_Q16 := make([]int, MAX_SHAPE_LPC_ORDER)
	AR1_Q24 := make([]int, MAX_SHAPE_LPC_ORDER)
	AR2_Q24 := make([]int, MAX_SHAPE_LPC_ORDER)
	var x_windowed []int16
	x_ptr2 := x_ptr - psEnc.la_shape

	SNR_adj_dB_Q7 = psEnc.SNR_dB_Q7

	psEncCtrl.input_quality_Q14 = (psEnc.Input_quality_bands_Q15[0] + psEnc.Input_quality_bands_Q15[1]) >> 2
	//psEncCtrl.coding_quality_Q14 = silk_sigm_Q15((SNR_adj_dB_Q7-(20<<7))>>4) >> 1

	psEncCtrl.coding_quality_Q14 = inlines.Silk_RSHIFT(silk_sigm_Q15(inlines.Silk_RSHIFT_ROUND(SNR_adj_dB_Q7-int(math.Trunc(20.0*float64(int64(1)<<(7))+0.5)), 4)), 1)
	if psEnc.UseCBR == 0 {
		b_Q8 = (int(math.Trunc(1.0*float64(int64(1)<<(8)) + 0.5))) - psEnc.Speech_activity_Q8
		b_Q8 = inlines.Silk_SMULWB(inlines.Silk_LSHIFT(b_Q8, 8), b_Q8)
		SNR_adj_dB_Q7 = inlines.Silk_SMLAWB(SNR_adj_dB_Q7,
			inlines.Silk_SMULBB((int(float64(0-TuningParameters.BG_SNR_DECR_dB)*float64(int64(1)<<(7))+0.5))>>(4+1), b_Q8), /* Q11*/
			inlines.Silk_SMULWB((int(math.Trunc(1.0*float64(int64(1)<<(14))+0.5)))+psEncCtrl.input_quality_Q14, psEncCtrl.coding_quality_Q14))
		/* Q12*/
	}

	if psEnc.indices.SignalType == TYPE_VOICED {

		SNR_adj_dB_Q7 = inlines.Silk_SMLAWB(SNR_adj_dB_Q7, ((int)(float64(TuningParameters.HARM_SNR_INCR_dB)*float64(int64(1)<<(8)) + 0.5)), psEnc.LTPCorr_Q15)

	} else {
		SNR_adj_dB_Q7 = inlines.Silk_SMLAWB(SNR_adj_dB_Q7,
			inlines.Silk_SMLAWB(6<<9, -int(math.Trunc(0.4*float64(1<<18))), psEnc.SNR_dB_Q7),
			(1<<14)-psEncCtrl.input_quality_Q14)
	}

	if psEnc.indices.SignalType == TYPE_VOICED {
		psEnc.indices.QuantOffsetType = 0
		psEncCtrl.sparseness_Q8 = 0
	} else {
		nSamples = psEnc.Fs_kHz << 1
		energy_variation_Q7 = 0
		log_energy_prev_Q7 = 0
		pitch_res_ptr2 := pitch_res_ptr
		var nrg int
		var scale int
		for k = 0; k < inlines.Silk_SMULBB(SUB_FRAME_LENGTH_MS, psEnc.nb_subfr)/2; k++ {
			boxed_nrg := comm.BoxedValueInt{0}
			boxed_scale := comm.BoxedValueInt{0}
			silk_sum_sqr_shift5(&boxed_nrg, &boxed_scale, pitch_res, pitch_res_ptr2, nSamples)
			nrg = boxed_nrg.Val
			scale = boxed_scale.Val
			nrg += int(nSamples >> scale)
			log_energy_Q7 = inlines.Silk_lin2log(nrg)
			if k > 0 {
				energy_variation_Q7 += inlines.Silk_abs(log_energy_Q7 - log_energy_prev_Q7)
			}
			log_energy_prev_Q7 = log_energy_Q7
			pitch_res_ptr2 += nSamples
		}
		psEncCtrl.sparseness_Q8 = inlines.Silk_RSHIFT(silk_sigm_Q15(inlines.Silk_SMULWB(energy_variation_Q7-(int(math.Trunc(5.0*float64(int64(1)<<(7))+0.5))), (int(math.Trunc(0.1*float64(int64(1)<<(16))+0.5))))), 7)

		if psEncCtrl.sparseness_Q8 > int(float64(TuningParameters.SPARSENESS_THRESHOLD_QNT_OFFSET)*float64(int64(1)<<(8))+0.5) {
			psEnc.indices.QuantOffsetType = 0
		} else {
			psEnc.indices.QuantOffsetType = 1
		}

		//SNR_adj_dB_Q7 = inlines.Silk_SMLAWB(SNR_adj_dB_Q7, int(TuningParameters.SPARSE_SNR_INCR_dB)<<15, psEncCtrl.sparseness_Q8-(1<<7))
		SNR_adj_dB_Q7 = inlines.Silk_SMLAWB(SNR_adj_dB_Q7, (int(float64(TuningParameters.SPARSE_SNR_INCR_dB)*float64(int64(1)<<(15)) + 0.5)), psEncCtrl.sparseness_Q8-(int(math.Trunc(0.5*float64(int64(1)<<(8))+0.5))))
	}

	//strength_Q16 = inlines.Silk_SMULWB(psEncCtrl.predGain_Q16, int(int64(TuningParameters.FIND_PITCH_WHITE_NOISE_FRACTION)<<16))
	strength_Q16 = inlines.Silk_SMULWB(psEncCtrl.predGain_Q16, int(float64(TuningParameters.FIND_PITCH_WHITE_NOISE_FRACTION)*float64(int64(1)<<(16))+0.5))

	BWExp1_Q16 = inlines.Silk_DIV32_varQ(((int)(float64(TuningParameters.BANDWIDTH_EXPANSION)*float64(int64(1)<<(16)) + 0.5)),
		inlines.Silk_SMLAWW(int(math.Trunc(float64(1.0)*float64(int64(1)<<(16))+0.5)), strength_Q16, strength_Q16), 16)

	BWExp2_Q16 = BWExp1_Q16

	delta_Q16 = inlines.Silk_SMULWB(int(math.Round(1.0*float64(int64(1)<<(16)))+0.5)-inlines.Silk_SMULBB(3, psEncCtrl.coding_quality_Q14),
		int(float64(TuningParameters.LOW_RATE_BANDWIDTH_EXPANSION_DELTA)*float64(int64(1)<<(16))+0.5))

	BWExp1_Q16 -= delta_Q16
	BWExp2_Q16 += delta_Q16
	BWExp1_Q16 = inlines.Silk_DIV32_16(BWExp1_Q16<<14, BWExp2_Q16>>2)

	if psEnc.warping_Q16 > 0 {
		warping_Q16 = inlines.Silk_SMLAWB(psEnc.warping_Q16, int(psEncCtrl.coding_quality_Q14), int(math.Trunc(0.01*float64(1<<18))))
	} else {
		warping_Q16 = 0
	}

	x_windowed = make([]int16, psEnc.shapeWinLength)
	for k = 0; k < psEnc.nb_subfr; k++ {
		flat_part := psEnc.Fs_kHz * 3
		slope_part := (psEnc.shapeWinLength - flat_part) >> 1
		silk_apply_sine_window(x_windowed, 0, x, x_ptr2, 1, slope_part)
		shift := slope_part
		copy(x_windowed[slope_part:slope_part+flat_part], x[x_ptr2+slope_part:x_ptr2+slope_part+flat_part])
		shift += flat_part
		silk_apply_sine_window(x_windowed, shift, x, x_ptr2+shift, 2, slope_part)
		x_ptr2 += psEnc.subfr_length
		scale_boxed := comm.BoxedValueInt{scale}
		if psEnc.warping_Q16 > 0 {

			//silk_warped_autocorr(x_windowed, warping_Q16, psEnc.shapeWinLength, psEnc.shapingLPCOrder)
			comm.Silk_warped_autocorr(auto_corr, &scale_boxed, x_windowed, warping_Q16, psEnc.shapeWinLength, psEnc.shapingLPCOrder)
		} else {

			// silk_autocorr(x_windowed, psEnc.shapeWinLength, psEnc.shapingLPCOrder+1)
			comm.Silk_autocorr(auto_corr, &scale_boxed, x_windowed, psEnc.shapeWinLength, psEnc.shapingLPCOrder+1)
		}
		scale = scale_boxed.Val
		auto_corr[0] = inlines.Silk_ADD32(auto_corr[0], inlines.Silk_max_32(inlines.Silk_SMULWB(inlines.Silk_RSHIFT(auto_corr[0], 4),
			(int(math.Trunc(float64(TuningParameters.SHAPE_WHITE_NOISE_FRACTION)*float64(int64(1)<<(20))+0.5)))), 1))

		nrg = silk_schur64(refl_coef_Q16, auto_corr, psEnc.shapingLPCOrder)

		inlines.OpusAssert(nrg >= 0)
		silk_k2a_Q16(AR2_Q24, refl_coef_Q16, psEnc.shapingLPCOrder)

		Qnrg = -scale
		inlines.OpusAssert(Qnrg >= -12)
		inlines.OpusAssert(Qnrg <= 30)
		if (Qnrg & 1) != 0 {
			Qnrg -= 1
			nrg >>= 1
		}
		tmp32 = inlines.Silk_SQRT_APPROX(nrg)
		Qnrg >>= 1
		psEncCtrl.Gains_Q16[k] = inlines.Silk_LSHIFT_SAT32(tmp32, 16-Qnrg)
		if psEnc.warping_Q16 > 0 {
			gain_mult_Q16 = warped_gain(AR2_Q24, warping_Q16, psEnc.shapingLPCOrder)

			inlines.OpusAssert(psEncCtrl.Gains_Q16[k] >= 0)
			if inlines.Silk_SMULWW(psEncCtrl.Gains_Q16[k]>>1, gain_mult_Q16) >= (int(1) << 30) {
				psEncCtrl.Gains_Q16[k] = math.MaxInt32
			} else {
				psEncCtrl.Gains_Q16[k] = inlines.Silk_SMULWW(psEncCtrl.Gains_Q16[k], gain_mult_Q16)
			}
		}

		silk_bwexpander_32(AR2_Q24, psEnc.shapingLPCOrder, BWExp2_Q16)
		copy(AR1_Q24, AR2_Q24)
		inlines.OpusAssert(BWExp1_Q16 <= (1 << 16))
		silk_bwexpander_32(AR1_Q24, psEnc.shapingLPCOrder, BWExp1_Q16)
		pre_nrg_Q30 = silk_LPC_inverse_pred_gain_Q24(AR2_Q24, psEnc.shapingLPCOrder)

		nrg = silk_LPC_inverse_pred_gain_Q24(AR1_Q24, psEnc.shapingLPCOrder)

		pre_nrg_Q30 = inlines.Silk_LSHIFT32(inlines.Silk_SMULWB(pre_nrg_Q30, int(math.Trunc((0.7)*float64(int64(1)<<(15))+0.5))), 1)

		psEncCtrl.GainsPre_Q14[k] = int(math.Trunc((0.3)*float64(int64(1)<<(14))+0.5)) + inlines.Silk_DIV32_varQ(pre_nrg_Q30, nrg, 14)

		limit_warped_coefs(AR2_Q24, AR1_Q24, warping_Q16, int(math.Trunc(3.999*float64(1<<24))), psEnc.shapingLPCOrder)

		for i = 0; i < psEnc.shapingLPCOrder; i++ {
			psEncCtrl.AR1_Q13[k*SilkConstants.MAX_SHAPE_LPC_ORDER+i] = int16(inlines.Silk_SAT16(int(inlines.Silk_RSHIFT_ROUND(int(AR1_Q24[i]), 11))))
			psEncCtrl.AR2_Q13[k*SilkConstants.MAX_SHAPE_LPC_ORDER+i] = int16(inlines.Silk_SAT16(int(inlines.Silk_RSHIFT_ROUND(int(AR2_Q24[i]), 11))))
		}

	}

	gain_mult_Q16 = inlines.Silk_log2lin(-inlines.Silk_SMLAWB(-(int(math.Trunc(16.0*float64(int64(1)<<(7)) + 0.5))), SNR_adj_dB_Q7, (int(math.Trunc(0.16*float64(int64(1)<<(16)) + 0.5)))))

	gain_add_Q16 = inlines.Silk_log2lin(inlines.Silk_SMLAWB(int(math.Trunc((16.0)*(1<<(7))+0.5)), int(float64(SilkConstants.MIN_QGAIN_DB)*float64(int64(1)<<(7))+0.5), int(math.Trunc((0.16)*float64(int64(1)<<(16))+0.5))))

	inlines.OpusAssert(gain_mult_Q16 > 0)

	for k = 0; k < psEnc.nb_subfr; k++ {
		psEncCtrl.Gains_Q16[k] = inlines.Silk_SMULWW(psEncCtrl.Gains_Q16[k], gain_mult_Q16)
		inlines.OpusAssert(psEncCtrl.Gains_Q16[k] >= 0)
		psEncCtrl.Gains_Q16[k] = inlines.Silk_ADD_POS_SAT32(psEncCtrl.Gains_Q16[k], gain_add_Q16)
	}

	gain_mult_Q16 = int(math.Trunc(1.0*float64(int64(1)<<(16))+0.5)) + inlines.Silk_RSHIFT_ROUND(inlines.Silk_MLA(int(float64(TuningParameters.INPUT_TILT)*float64(int64(1)<<(26))+0.5),
		psEncCtrl.coding_quality_Q14, int(float64(TuningParameters.HIGH_RATE_INPUT_TILT)*float64(int64(1)<<(12))+0.5)), 10)

	for k = 0; k < psEnc.nb_subfr; k++ {
		psEncCtrl.GainsPre_Q14[k] = inlines.Silk_SMULWB(gain_mult_Q16, psEncCtrl.GainsPre_Q14[k])
	}

	strength_Q16 = inlines.Silk_MUL(int(float64(TuningParameters.LOW_FREQ_SHAPING)*float64(int64(1)<<(4))+0.5), inlines.Silk_SMLAWB(int(math.Trunc(1.0*float64(int64(1)<<(12))+0.5)),
		int(float64(TuningParameters.LOW_QUALITY_LOW_FREQ_SHAPING_DECR)*float64(int64(1)<<(13))+0.5), psEnc.Input_quality_bands_Q15[0]-int(math.Trunc(1.0)*float64(int64(1)<<(15))+0.5)))
	strength_Q16 = inlines.Silk_RSHIFT(inlines.Silk_MUL(strength_Q16, psEnc.Speech_activity_Q8), 8)

	if psEnc.indices.SignalType == TYPE_VOICED {

		fs_kHz_inv := inlines.Silk_DIV32_16((int(math.Trunc((0.2)*float64(int64(1)<<(14)) + 0.5))), psEnc.Fs_kHz)
		for k = 0; k < psEnc.nb_subfr; k++ {
			b_Q14 = fs_kHz_inv + inlines.Silk_DIV32_16(int(math.Trunc((3.0)*float64(int64(1)<<(14))+0.5)), psEncCtrl.pitchL[k])
			/* Pack two coefficients in one int32 */
			psEncCtrl.LF_shp_Q14[k] = inlines.Silk_LSHIFT(int(math.Trunc((1.0)*float64(int64(1)<<(14))+0.5))-b_Q14-inlines.Silk_SMULWB(strength_Q16, b_Q14), 16)
			psEncCtrl.LF_shp_Q14[k] |= (b_Q14 - int(math.Trunc((1.0)*float64(int64(1)<<(14))+0.5))) & 0xFFFF
		}
		inlines.OpusAssert((float64(TuningParameters.HARM_HP_NOISE_COEF)*float64(int64(1)<<(24)) + 0.5) < ((0.5)*float64(int64(1)<<(24)) + 0.5))
		/* Guarantees that second argument to SMULWB() is within range of an short*/
		Tilt_Q16 = -int(float64(TuningParameters.HP_NOISE_COEF)*float64(int64(1)<<(16))+0.5) - inlines.Silk_SMULWB((int(math.Trunc(1.0*float64(int64(1)<<16)+0.5))-int(float64(TuningParameters.HP_NOISE_COEF)*float64(int64(1)<<(16))+0.5)),
			inlines.Silk_SMULWB(int(float64(TuningParameters.HARM_HP_NOISE_COEF)*float64(int64(1)<<(24))+0.5), psEnc.Speech_activity_Q8))
	} else {
		b_Q14 = inlines.Silk_DIV32_16(21299, psEnc.Fs_kHz)
		/* 1.3_Q0 = 21299_Q14*/
		/* Pack two coefficients in one int32 */
		psEncCtrl.LF_shp_Q14[0] = inlines.Silk_LSHIFT(int(math.Trunc(1.0*float64(int64(1)<<(14))+0.5))-b_Q14-
			inlines.Silk_SMULWB(strength_Q16, inlines.Silk_SMULWB(int(math.Trunc(0.6*float64(int64(1)<<(16))+0.5)), b_Q14)), 16)
		psEncCtrl.LF_shp_Q14[0] |= (b_Q14 - int(math.Trunc(1.0*float64(int64(1)<<(14))+0.5))) & 0xFFFF
		for k = 1; k < psEnc.nb_subfr; k++ {
			psEncCtrl.LF_shp_Q14[k] = psEncCtrl.LF_shp_Q14[0]
		}
		Tilt_Q16 = -int(float64(TuningParameters.HP_NOISE_COEF)*float64(int64(1)<<(16)) + 0.5)
	}

	/**
	 * *************************
	 */
	/* HARMONIC SHAPING CONTROL */
	/**
	 * *************************
	 */
	/* Control boosting of harmonic frequencies */
	HarmBoost_Q16 = inlines.Silk_SMULWB(inlines.Silk_SMULWB(int(math.Trunc((1.0)*float64(int64(1)<<(17))+0.5))-inlines.Silk_LSHIFT(psEncCtrl.coding_quality_Q14, 3),
		psEnc.LTPCorr_Q15), int(float64(TuningParameters.LOW_RATE_HARMONIC_BOOST)*float64(int64(1)<<(16))+0.5))

	/* More harmonic boost for noisy input signals */
	HarmBoost_Q16 = inlines.Silk_SMLAWB(HarmBoost_Q16,
		(int(math.Trunc((1.0)*float64(int64(1)<<(16))+0.5)))-inlines.Silk_LSHIFT(psEncCtrl.input_quality_Q14, 2), int(float64(TuningParameters.LOW_INPUT_QUALITY_HARMONIC_BOOST)*float64(int64(1)<<(16))+0.5))

	if SilkConstants.USE_HARM_SHAPING != 0 && psEnc.indices.SignalType == TYPE_VOICED {
		/* More harmonic noise shaping for high bitrates or noisy input */
		HarmShapeGain_Q16 = inlines.Silk_SMLAWB(int(float64(TuningParameters.HARMONIC_SHAPING)*float64(int64(1)<<(16))+0.5),
			(int(math.Trunc((1.0)*float64(int64(1)<<(16))+0.5)))-inlines.Silk_SMULWB(int(math.Trunc((1.0)*float64(int64(1)<<(18))+0.5))-inlines.Silk_LSHIFT(psEncCtrl.coding_quality_Q14, 4),
				psEncCtrl.input_quality_Q14), int(float64(TuningParameters.HIGH_RATE_OR_LOW_QUALITY_HARMONIC_SHAPING)*float64(int64(1)<<(16))+0.5))

		/* Less harmonic noise shaping for less periodic signals */
		HarmShapeGain_Q16 = inlines.Silk_SMULWB(inlines.Silk_LSHIFT(HarmShapeGain_Q16, 1),
			inlines.Silk_SQRT_APPROX(inlines.Silk_LSHIFT(psEnc.LTPCorr_Q15, 15)))
	} else {
		HarmShapeGain_Q16 = 0
	}

	/**
	 * **********************
	 */
	/* Smooth over subframes */
	/**
	 * **********************
	 */
	for k = 0; k < SilkConstants.MAX_NB_SUBFR; k++ {
		psShapeSt.HarmBoost_smth_Q16 = inlines.Silk_SMLAWB(psShapeSt.HarmBoost_smth_Q16, HarmBoost_Q16-psShapeSt.HarmBoost_smth_Q16, int(float64(TuningParameters.SUBFR_SMTH_COEF)*float64(int64(1)<<(16))+0.5))
		psShapeSt.HarmShapeGain_smth_Q16 = inlines.Silk_SMLAWB(psShapeSt.HarmShapeGain_smth_Q16, HarmShapeGain_Q16-psShapeSt.HarmShapeGain_smth_Q16, int(float64(TuningParameters.SUBFR_SMTH_COEF)*float64(int64(1)<<(16))+0.5))
		psShapeSt.Tilt_smth_Q16 = inlines.Silk_SMLAWB(psShapeSt.Tilt_smth_Q16, Tilt_Q16-psShapeSt.Tilt_smth_Q16, int(float64(TuningParameters.SUBFR_SMTH_COEF)*float64(int64(1)<<(16))+0.5))

		psEncCtrl.HarmBoost_Q14[k] = inlines.Silk_RSHIFT_ROUND(psShapeSt.HarmBoost_smth_Q16, 2)
		psEncCtrl.HarmShapeGain_Q14[k] = inlines.Silk_RSHIFT_ROUND(psShapeSt.HarmShapeGain_smth_Q16, 2)
		psEncCtrl.Tilt_Q14[k] = inlines.Silk_RSHIFT_ROUND(psShapeSt.Tilt_smth_Q16, 2)
	}
}
