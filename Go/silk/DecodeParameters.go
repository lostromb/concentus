package silk

import (
	"github.com/dosgo/concentus/go/comm"
	"github.com/dosgo/concentus/go/comm/arrayUtil"
)

func silk_decode_parameters(
	psDec *SilkChannelDecoder,
	psDecCtrl *SilkDecoderControl,
	condCoding int) {

	pNLSF_Q15 := make([]int16, psDec.LPC_order)
	pNLSF0_Q15 := make([]int16, psDec.LPC_order)
	var cbk_ptr_Q7 [][]int8

	boxedLastGainIndex := comm.BoxedValueByte{Val: psDec.LastGainIndex}
	silk_gains_dequant(psDecCtrl.Gains_Q16, psDec.Indices.GainsIndices,
		&boxedLastGainIndex, comm.BoolToInt(condCoding == SilkConstants.CODE_CONDITIONALLY), psDec.Nb_subfr)
	psDec.LastGainIndex = boxedLastGainIndex.Val

	silk_NLSF_decode(pNLSF_Q15, psDec.Indices.NLSFIndices, psDec.psNLSF_CB)
	silk_NLSF2A(psDecCtrl.PredCoef_Q12[1], pNLSF_Q15, psDec.LPC_order)

	if psDec.First_frame_after_reset == 1 {
		psDec.Indices.NLSFInterpCoef_Q2 = 4
	}

	if psDec.Indices.NLSFInterpCoef_Q2 < 4 {
		for i := 0; i < psDec.LPC_order; i++ {
			pNLSF0_Q15[i] = int16(int(psDec.prevNLSF_Q15[i]) + inlines.Silk_RSHIFT(inlines.Silk_MUL(int(psDec.Indices.NLSFInterpCoef_Q2),
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

	if psDec.Indices.SignalType == TYPE_VOICED {
		silk_decode_pitch(psDec.Indices.lagIndex, psDec.Indices.contourIndex, psDecCtrl.pitchL, psDec.Fs_kHz, psDec.Nb_subfr)

		cbk_ptr_Q7 = silk_LTP_vq_ptrs_Q7[psDec.Indices.PERIndex]
		for k := 0; k < psDec.Nb_subfr; k++ {
			Ix := psDec.Indices.LTPIndex[k]
			for i := 0; i < LTP_ORDER; i++ {
				psDecCtrl.LTPCoef_Q14[k*LTP_ORDER+i] = int16(inlines.Silk_LSHIFT(int(cbk_ptr_Q7[Ix][i]), 7))
			}
		}

		Ix := psDec.Indices.LTP_scaleIndex
		psDecCtrl.LTP_scale_Q14 = int(silk_LTPScales_table_Q14[Ix])
	} else {
		arrayUtil.MemSetLen(psDecCtrl.pitchL, 0, int(psDec.Nb_subfr))
		arrayUtil.MemSetLen(psDecCtrl.LTPCoef_Q14, 0, SilkConstants.LTP_ORDER*psDec.Nb_subfr)
		psDec.Indices.PERIndex = 0
		psDecCtrl.LTP_scale_Q14 = 0
	}
}
