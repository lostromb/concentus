package opus

const MaxInt16 = 32767

func silk_CNG_exc(
	exc_Q10 []int,
	exc_Q10_ptr int,
	exc_buf_Q14 []int,
	Gain_Q16 int,
	length int,
	rand_seed *BoxedValueInt) {

	seed := rand_seed.Val
	exc_mask := CNG_BUF_MASK_MAX

	for exc_mask > length {
		exc_mask = silk_RSHIFT(exc_mask, 1)
	}

	for i := exc_Q10_ptr; i < exc_Q10_ptr+length; i++ {
		seed = silk_RAND(seed)
		idx := int(silk_RSHIFT(seed, 24) & exc_mask)
		OpusAssert(idx >= 0)
		OpusAssert(idx <= CNG_BUF_MASK_MAX)
		exc_Q10[i] = int(silk_SAT16(silk_SMULWW(int(exc_buf_Q14[idx]), Gain_Q16>>4)))
	}

	rand_seed.Val = seed
}

func silk_CNG_Reset(psDec *SilkChannelDecoder) {
	NLSF_step_Q15 := silk_DIV32_16(MaxInt16, psDec.LPC_order+1)
	NLSF_acc_Q15 := 0
	for i := 0; i < psDec.LPC_order; i++ {
		NLSF_acc_Q15 += NLSF_step_Q15
		psDec.sCNG.CNG_smth_NLSF_Q15[i] = int16(NLSF_acc_Q15)
	}
	psDec.sCNG.CNG_smth_Gain_Q16 = 0
	psDec.sCNG.rand_seed = 3176576
}

func silk_CNG(
	psDec *SilkChannelDecoder,
	psDecCtrl *SilkDecoderControl,
	frame []int16,
	frame_ptr int,
	length int) {

	psCNG := psDec.sCNG

	if psDec.fs_kHz != psCNG.fs_kHz {
		silk_CNG_Reset(psDec)
		psCNG.fs_kHz = psDec.fs_kHz
	}

	if psDec.lossCnt == 0 && psDec.prevSignalType == TYPE_NO_VOICE_ACTIVITY {
		for i := 0; i < psDec.LPC_order; i++ {
			diff := int(psDec.prevNLSF_Q15[i]) - int(psCNG.CNG_smth_NLSF_Q15[i])
			psCNG.CNG_smth_NLSF_Q15[i] += int16(silk_SMULWB(diff, CNG_NLSF_SMTH_Q16))
		}

		max_Gain_Q16 := 0
		//subfr := 0
		for i := 0; i < psDec.nb_subfr; i++ {
			if psDecCtrl.Gains_Q16[i] > max_Gain_Q16 {
				max_Gain_Q16 = psDecCtrl.Gains_Q16[i]
				//	subfr = i
			}
		}

		lengthToMove := (psDec.nb_subfr - 1) * psDec.subfr_length
		copy(psCNG.CNG_exc_buf_Q14[0:lengthToMove], psCNG.CNG_exc_buf_Q14[psDec.subfr_length:psDec.subfr_length+lengthToMove])

		for i := 0; i < psDec.nb_subfr; i++ {
			gainDiff := psDecCtrl.Gains_Q16[i] - psCNG.CNG_smth_Gain_Q16
			psCNG.CNG_smth_Gain_Q16 += silk_SMULWB(gainDiff, CNG_GAIN_SMTH_Q16)
		}
	}

	if psDec.lossCnt != 0 {
		CNG_sig_Q10 := make([]int, length+MAX_LPC_ORDER)
		gain_Q16 := silk_SMULWW(int(psDec.sPLC.randScale_Q14), psDec.sPLC.prevGain_Q16[1])

		if gain_Q16 >= (1<<21) || psCNG.CNG_smth_Gain_Q16 > (1<<23) {
			gain_Q16 = silk_SMULTT(gain_Q16, gain_Q16)
			gain_Q16 = silk_SUB_LSHIFT32(silk_SMULTT(psCNG.CNG_smth_Gain_Q16, psCNG.CNG_smth_Gain_Q16), gain_Q16, 5)
			gain_Q16 = silk_LSHIFT32(silk_SQRT_APPROX(gain_Q16), 16)
		} else {
			gain_Q16 = silk_SMULWW(gain_Q16, gain_Q16)
			gain_Q16 = silk_SUB_LSHIFT32(silk_SMULWW(psCNG.CNG_smth_Gain_Q16, psCNG.CNG_smth_Gain_Q16), gain_Q16, 5)
			gain_Q16 = silk_LSHIFT32(silk_SQRT_APPROX(gain_Q16), 8)
		}

		boxed_rand_seed := BoxedValueInt{psCNG.rand_seed}
		silk_CNG_exc(CNG_sig_Q10, MAX_LPC_ORDER, psCNG.CNG_exc_buf_Q14, gain_Q16, length, &boxed_rand_seed)
		psCNG.rand_seed = boxed_rand_seed.Val

		A_Q12 := make([]int16, psDec.LPC_order)
		silk_NLSF2A(A_Q12, psCNG.CNG_smth_NLSF_Q15, psDec.LPC_order)

		copy(CNG_sig_Q10[0:MAX_LPC_ORDER], psCNG.CNG_synth_state)

		for i := 0; i < length; i++ {
			lpci := MAX_LPC_ORDER + i
			OpusAssert(psDec.LPC_order == 10 || psDec.LPC_order == 16)
			sum_Q6 := silk_RSHIFT(psDec.LPC_order, 1)
			sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-1], int(A_Q12[0]))
			sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-2], int(A_Q12[1]))
			sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-3], int(A_Q12[2]))
			sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-4], int(A_Q12[3]))
			sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-5], int(A_Q12[4]))
			sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-6], int(A_Q12[5]))
			sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-7], int(A_Q12[6]))
			sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-8], int(A_Q12[7]))
			sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-9], int(A_Q12[8]))
			sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-10], int(A_Q12[9]))

			if psDec.LPC_order == 16 {
				sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-11], int(A_Q12[10]))
				sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-12], int(A_Q12[11]))
				sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-13], int(A_Q12[12]))
				sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-14], int(A_Q12[13]))
				sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-15], int(A_Q12[14]))
				sum_Q6 = silk_SMLAWB(sum_Q6, CNG_sig_Q10[lpci-16], int(A_Q12[15]))
			}

			CNG_sig_Q10[lpci] = silk_ADD_LSHIFT(CNG_sig_Q10[lpci], sum_Q6, 4)
			frame[frame_ptr+i] = silk_ADD_SAT16(frame[frame_ptr+i], int16(silk_RSHIFT_ROUND(CNG_sig_Q10[lpci], 10)))
		}

		copy(psCNG.CNG_synth_state, CNG_sig_Q10[length:length+MAX_LPC_ORDER])
	} else {
		for i := 0; i < psDec.LPC_order; i++ {
			psCNG.CNG_synth_state[i] = 0
		}
	}
}
