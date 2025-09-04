package opus

func silk_decode_parameters(
	psDec *SilkChannelDecoder,
	psDecCtrl *SilkDecoderControl,
	condCoding int) {

	pNLSF_Q15 := make([]int16, psDec.LPC_order)
	pNLSF0_Q15 := make([]int16, psDec.LPC_order)
	var cbk_ptr_Q7 [][]int8

	boxedLastGainIndex := BoxedValueByte{Val: psDec.LastGainIndex}
	silk_gains_dequant(psDecCtrl.Gains_Q16, psDec.indices.GainsIndices,
		&boxedLastGainIndex, boolToInt(condCoding == SilkConstants.CODE_CONDITIONALLY), psDec.nb_subfr)
	psDec.LastGainIndex = boxedLastGainIndex.Val

	silk_NLSF_decode(pNLSF_Q15, psDec.indices.NLSFIndices, psDec.psNLSF_CB)
	silk_NLSF2A(psDecCtrl.PredCoef_Q12[1], pNLSF_Q15, psDec.LPC_order)

	if psDec.first_frame_after_reset == 1 {
		psDec.indices.NLSFInterpCoef_Q2 = 4
	}

	if psDec.indices.NLSFInterpCoef_Q2 < 4 {
		for i := 0; i < psDec.LPC_order; i++ {
			pNLSF0_Q15[i] = int16(int(psDec.prevNLSF_Q15[i]) + silk_RSHIFT(silk_MUL(int(psDec.indices.NLSFInterpCoef_Q2),
				int(pNLSF_Q15[i]-psDec.prevNLSF_Q15[i])), 2))
		}
		silk_NLSF2A(psDecCtrl.PredCoef_Q12[0], pNLSF0_Q15, psDec.LPC_order)
	} else {
		copy(psDecCtrl.PredCoef_Q12[0][:psDec.LPC_order], psDecCtrl.PredCoef_Q12[1][:psDec.LPC_order])
	}

	copy(psDec.prevNLSF_Q15[:psDec.LPC_order], pNLSF_Q15)

	if psDec.lossCnt != 0 {
		silk_bwexpander(psDecCtrl.PredCoef_Q12[0], psDec.LPC_order, BWE_AFTER_LOSS_Q16)
		silk_bwexpander(psDecCtrl.PredCoef_Q12[1], psDec.LPC_order, BWE_AFTER_LOSS_Q16)
	}

	if psDec.indices.signalType == TYPE_VOICED {
		silk_decode_pitch(psDec.indices.lagIndex, psDec.indices.contourIndex, psDecCtrl.pitchL, psDec.fs_kHz, psDec.nb_subfr)

		cbk_ptr_Q7 = silk_LTP_vq_ptrs_Q7[psDec.indices.PERIndex]
		for k := 0; k < psDec.nb_subfr; k++ {
			Ix := psDec.indices.LTPIndex[k]
			for i := 0; i < LTP_ORDER; i++ {
				psDecCtrl.LTPCoef_Q14[k*LTP_ORDER+i] = int16(silk_LSHIFT(int(cbk_ptr_Q7[Ix][i]), 7))
			}
		}

		Ix := psDec.indices.LTP_scaleIndex
		psDecCtrl.LTP_scale_Q14 = int(silk_LTPScales_table_Q14[Ix])
	} else {
		MemSetLen(psDecCtrl.pitchL, 0, int(psDec.nb_subfr))
		MemSetLen(psDecCtrl.LTPCoef_Q14, 0, SilkConstants.LTP_ORDER*psDec.nb_subfr)
		psDec.indices.PERIndex = 0
		psDecCtrl.LTP_scale_Q14 = 0
	}
}
