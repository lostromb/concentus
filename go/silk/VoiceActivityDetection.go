package silk

var tiltWeights = []int{30000, 6000, -12000, -12000}

func silk_VAD_Init(psSilk_VAD *SilkVADState) int {
	ret := 0
	psSilk_VAD.Reset()

	for b := 0; b < VAD_N_BANDS; b++ {
		bias := inlines.Silk_max_32(inlines.Silk_DIV32_16(SilkConstants.VAD_NOISE_LEVELS_BIAS, int(b+1)), 1)
		psSilk_VAD.NoiseLevelBias[b] = bias
	}

	for b := 0; b < VAD_N_BANDS; b++ {
		psSilk_VAD.NL[b] = inlines.Silk_MUL(100, psSilk_VAD.NoiseLevelBias[b])
		psSilk_VAD.inv_NL[b] = inlines.DIV32(0x7FFFFFFF, psSilk_VAD.NL[b])
	}

	psSilk_VAD.counter = 15

	for b := 0; b < VAD_N_BANDS; b++ {
		psSilk_VAD.NrgRatioSmth_Q8[b] = 100 * 256
	}

	return ret
}

func silk_VAD_GetSA_Q8(psEncC *SilkChannelEncoder, pIn []int16, pIn_ptr int) int {
	SA_Q15 := 0
	pSNR_dB_Q7 := 0
	input_tilt := 0
	decimated_framelength1 := inlines.Silk_RSHIFT(psEncC.Frame_length, 1)
	decimated_framelength2 := inlines.Silk_RSHIFT(psEncC.Frame_length, 2)
	decimated_framelength := inlines.Silk_RSHIFT(psEncC.Frame_length, 3)

	X_offset := [4]int{
		0,
		decimated_framelength + decimated_framelength2,
		decimated_framelength + decimated_framelength2 + decimated_framelength,
		decimated_framelength + decimated_framelength2 + decimated_framelength + decimated_framelength2,
	}
	totalLen := X_offset[3] + decimated_framelength1
	X := make([]int16, totalLen)

	silk_ana_filt_bank_1(pIn, pIn_ptr, psEncC.sVAD.AnaState, X, X, X_offset[3], psEncC.Frame_length)
	silk_ana_filt_bank_1(X, 0, psEncC.sVAD.AnaState1, X, X, X_offset[2], decimated_framelength1)
	silk_ana_filt_bank_1(X, 0, psEncC.sVAD.AnaState2, X, X, X_offset[1], decimated_framelength2)

	X[decimated_framelength-1] = int16(inlines.Silk_RSHIFT(int(X[decimated_framelength-1]), 1))
	HPstateTmp := X[decimated_framelength-1]

	for i := decimated_framelength - 1; i > 0; i-- {
		X[i-1] = int16(inlines.Silk_RSHIFT(int(X[i-1]), 1))
		X[i] -= X[i-1]
	}

	X[0] -= psEncC.sVAD.HPstate
	psEncC.sVAD.HPstate = HPstateTmp

	Xnrg := [4]int{}
	NrgToNoiseRatio_Q8 := [4]int{}

	for b := 0; b < VAD_N_BANDS; b++ {
		shift := inlines.Silk_min_int(VAD_N_BANDS-b, VAD_N_BANDS-1)
		decimated_framelength = inlines.Silk_RSHIFT(psEncC.Frame_length, shift)
		dec_subframe_length := inlines.Silk_RSHIFT(decimated_framelength, VAD_INTERNAL_SUBFRAMES_LOG2)
		dec_subframe_offset := 0

		Xnrg[b] = psEncC.sVAD.XnrgSubfr[b]
		sumSquared := 0
		for s := 0; s < VAD_INTERNAL_SUBFRAMES; s++ {
			sumSquared = 0
			for i := 0; i < dec_subframe_length; i++ {
				x_tmp := inlines.Silk_RSHIFT(int(X[X_offset[b]+i+dec_subframe_offset]), 3)
				sumSquared = inlines.Silk_SMLABB(sumSquared, x_tmp, x_tmp)
				inlines.OpusAssert(sumSquared >= 0)
			}

			if s < VAD_INTERNAL_SUBFRAMES-1 {
				Xnrg[b] = inlines.Silk_ADD_POS_SAT32(Xnrg[b], sumSquared)
			} else {
				Xnrg[b] = inlines.Silk_ADD_POS_SAT32(Xnrg[b], inlines.Silk_RSHIFT(sumSquared, 1))
			}
			dec_subframe_offset += dec_subframe_length
		}
		psEncC.sVAD.XnrgSubfr[b] = sumSquared
	}

	silk_VAD_GetNoiseLevels(Xnrg[:], psEncC.sVAD)

	sumSquared := 0
	for b := 0; b < VAD_N_BANDS; b++ {
		speech_nrg := Xnrg[b] - psEncC.sVAD.NL[b]
		if speech_nrg > 0 {
			if (Xnrg[b] & 0xFF800000) == 0 {
				NrgToNoiseRatio_Q8[b] = inlines.DIV32(inlines.Silk_LSHIFT(Xnrg[b], 8), psEncC.sVAD.NL[b]+1)
			} else {
				NrgToNoiseRatio_Q8[b] = inlines.DIV32(Xnrg[b], inlines.Silk_RSHIFT(psEncC.sVAD.NL[b], 8)+1)
			}

			SNR_Q7 := inlines.Silk_lin2log(NrgToNoiseRatio_Q8[b]) - 8*128
			sumSquared = inlines.Silk_SMLABB(sumSquared, SNR_Q7, SNR_Q7)

			if speech_nrg < (1 << 20) {
				SNR_Q7 = inlines.Silk_SMULWB(inlines.Silk_LSHIFT(inlines.Silk_SQRT_APPROX(speech_nrg), 6), SNR_Q7)
			}
			input_tilt = inlines.Silk_SMLAWB(input_tilt, tiltWeights[b], SNR_Q7)
		} else {
			NrgToNoiseRatio_Q8[b] = 256
		}
	}

	sumSquared = inlines.Silk_DIV32_16(sumSquared, VAD_N_BANDS)
	pSNR_dB_Q7 = 3 * inlines.Silk_SQRT_APPROX(sumSquared)

	SA_Q15 = silk_sigm_Q15(inlines.Silk_SMULWB(VAD_SNR_FACTOR_Q16, int(pSNR_dB_Q7)) - VAD_NEGATIVE_OFFSET_Q5)

	psEncC.input_tilt_Q15 = inlines.Silk_LSHIFT(silk_sigm_Q15(input_tilt)-16384, 1)

	speech_nrg := 0
	for b := 0; b < VAD_N_BANDS; b++ {
		speech_nrg += (b + 1) * inlines.Silk_RSHIFT(Xnrg[b]-psEncC.sVAD.NL[b], 4)
	}

	if speech_nrg <= 0 {
		SA_Q15 = inlines.Silk_RSHIFT(SA_Q15, 1)
	} else if speech_nrg < 32768 {
		if psEncC.Frame_length == 10*psEncC.Fs_kHz {
			speech_nrg = inlines.Silk_LSHIFT_SAT32(speech_nrg, 16)
		} else {
			speech_nrg = inlines.Silk_LSHIFT_SAT32(speech_nrg, 15)
		}
		speech_nrg = inlines.Silk_SQRT_APPROX(speech_nrg)
		SA_Q15 = inlines.Silk_SMULWB(32768+speech_nrg, int(SA_Q15))
	}

	if SA_Q15 < 0 {
		SA_Q15 = 0
	}
	psEncC.Speech_activity_Q8 = inlines.Silk_min_int(inlines.Silk_RSHIFT(SA_Q15, 7), 255)

	smooth_coef_Q16 := int(inlines.Silk_SMULWB(VAD_SNR_SMOOTH_COEF_Q18, inlines.Silk_SMULWB(int(SA_Q15), int(SA_Q15))))
	if psEncC.Frame_length == 10*psEncC.Fs_kHz {
		smooth_coef_Q16 >>= 1
	}

	for b := 0; b < VAD_N_BANDS; b++ {
		psEncC.sVAD.NrgRatioSmth_Q8[b] = inlines.Silk_SMLAWB(psEncC.sVAD.NrgRatioSmth_Q8[b],
			NrgToNoiseRatio_Q8[b]-psEncC.sVAD.NrgRatioSmth_Q8[b], int(smooth_coef_Q16))
		SNR_Q7 := 3 * (inlines.Silk_lin2log(psEncC.sVAD.NrgRatioSmth_Q8[b]) - 8*128)
		psEncC.Input_quality_bands_Q15[b] = silk_sigm_Q15(inlines.Silk_RSHIFT(SNR_Q7-16*128, 4))
	}

	return 0
}

func silk_VAD_GetNoiseLevels(pX []int, psSilk_VAD *SilkVADState) {
	for k := 0; k < VAD_N_BANDS; k++ {
		nl := psSilk_VAD.NL[k]
		inlines.OpusAssert(nl >= 0)

		nrg := inlines.Silk_ADD_POS_SAT32(pX[k], psSilk_VAD.NoiseLevelBias[k])
		inlines.OpusAssert(nrg > 0)

		inv_nrg := inlines.DIV32(0x7FFFFFFF, nrg)
		inlines.OpusAssert(inv_nrg >= 0)

		coef := 0
		if nrg > inlines.Silk_LSHIFT(nl, 3) {
			coef = VAD_NOISE_LEVEL_SMOOTH_COEF_Q16 >> 3
		} else if nrg < nl {
			coef = VAD_NOISE_LEVEL_SMOOTH_COEF_Q16
		} else {
			coef = inlines.Silk_SMULWB(inlines.Silk_SMULWW(inv_nrg, nl), VAD_NOISE_LEVEL_SMOOTH_COEF_Q16<<1)
		}

		min_coef := 0
		if psSilk_VAD.counter < 1000 {
			min_coef = inlines.Silk_DIV32_16(0x7FFF, int(inlines.Silk_RSHIFT(psSilk_VAD.counter, 4)+1))
		}
		coef = inlines.Silk_max_int(coef, min_coef)

		psSilk_VAD.inv_NL[k] = inlines.Silk_SMLAWB(psSilk_VAD.inv_NL[k], inv_nrg-psSilk_VAD.inv_NL[k], coef)
		inlines.OpusAssert(psSilk_VAD.inv_NL[k] >= 0)

		nl = inlines.DIV32(0x7FFFFFFF, psSilk_VAD.inv_NL[k])
		inlines.OpusAssert(nl >= 0)

		nl = inlines.Silk_min(nl, 0x00FFFFFF)
		psSilk_VAD.NL[k] = nl
	}
	psSilk_VAD.counter++
}
