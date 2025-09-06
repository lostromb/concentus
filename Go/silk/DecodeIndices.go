package silk

import "github.com/dosgo/concentus/go/comm"

func Silk_decode_indices(psDec *SilkChannelDecoder, psRangeDec *comm.EntropyCoder, FrameIndex int, decode_LBRR int, condCoding int) {
	var i, k, Ix int
	var decode_absolute_lagIndex, delta_lagIndex int
	ec_ix := make([]int16, psDec.LPC_order)
	pred_Q8 := make([]int16, psDec.LPC_order)

	if decode_LBRR != 0 || psDec.VAD_flags[FrameIndex] != 0 {
		Ix = psRangeDec.Dec_icdf(SilkTables.Silk_type_offset_VAD_iCDF, 8) + 2
	} else {
		Ix = psRangeDec.Dec_icdf(SilkTables.Silk_type_offset_no_VAD_iCDF, 8)
	}
	psDec.Indices.SignalType = byte(Ix >> 1)
	psDec.Indices.QuantOffsetType = byte(Ix & 1)

	if condCoding == SilkConstants.CODE_CONDITIONALLY {
		psDec.Indices.GainsIndices[0] = int8(psRangeDec.Dec_icdf(SilkTables.Silk_delta_gain_iCDF, 8))
	} else {
		tmp := psRangeDec.Dec_icdf(SilkTables.Silk_gain_iCDF[psDec.Indices.SignalType], 8)
		psDec.Indices.GainsIndices[0] = int8(tmp << 3)
		psDec.Indices.GainsIndices[0] += int8(psRangeDec.Dec_icdf(SilkTables.Silk_uniform8_iCDF, 8))
	}

	for i = 1; i < psDec.Nb_subfr; i++ {
		psDec.Indices.GainsIndices[i] = int8(psRangeDec.Dec_icdf(SilkTables.Silk_delta_gain_iCDF, 8))
	}

	psDec.Indices.NLSFIndices[0] = int8(psRangeDec.Dec_icdf_offset(psDec.psNLSF_CB.CB1_iCDF, (int(psDec.Indices.SignalType) >> 1 * int(psDec.psNLSF_CB.nVectors)), 8))
	silk_NLSF_unpack(ec_ix, pred_Q8, psDec.psNLSF_CB, int(psDec.Indices.NLSFIndices[0]))
	if psDec.psNLSF_CB.order != int16(psDec.LPC_order) {
		panic("assertion failed: psDec.psNLSF_CB.order == psDec.LPC_order")
	}
	for i = 0; i < int(psDec.psNLSF_CB.order); i++ {
		Ix = psRangeDec.Dec_icdf_offset(psDec.psNLSF_CB.ec_iCDF, int(ec_ix[i]), 8)
		if Ix == 0 {
			Ix -= psRangeDec.Dec_icdf(SilkTables.Silk_NLSF_EXT_iCDF, 8)
		} else if Ix == 2*SilkConstants.NLSF_QUANT_MAX_AMPLITUDE {
			Ix += psRangeDec.Dec_icdf(SilkTables.Silk_NLSF_EXT_iCDF, 8)
		}
		psDec.Indices.NLSFIndices[i+1] = int8(Ix - SilkConstants.NLSF_QUANT_MAX_AMPLITUDE)
	}

	if psDec.Nb_subfr == SilkConstants.MAX_NB_SUBFR {
		psDec.Indices.NLSFInterpCoef_Q2 = byte(psRangeDec.Dec_icdf(SilkTables.Silk_NLSF_interpolation_factor_iCDF, 8))
	} else {
		psDec.Indices.NLSFInterpCoef_Q2 = 4
	}

	if psDec.Indices.SignalType == byte(SilkConstants.TYPE_VOICED) {
		decode_absolute_lagIndex = 1
		if condCoding == SilkConstants.CODE_CONDITIONALLY && psDec.ec_prevSignalType == SilkConstants.TYPE_VOICED {
			delta_lagIndex = int(psRangeDec.Dec_icdf(SilkTables.Silk_pitch_delta_iCDF, 8))
			if delta_lagIndex > 0 {
				delta_lagIndex -= 9
				psDec.Indices.lagIndex = int16(int(psDec.ec_prevLagIndex) + delta_lagIndex)
				decode_absolute_lagIndex = 0
			}
		}
		if decode_absolute_lagIndex != 0 {
			tmp := psRangeDec.Dec_icdf(SilkTables.Silk_pitch_lag_iCDF, 8)
			base := tmp * (psDec.Fs_kHz >> 1)
			lowBits := psRangeDec.Dec_icdf(psDec.pitch_lag_low_bits_iCDF, 8)
			psDec.Indices.lagIndex = int16(base + lowBits)
		}
		psDec.ec_prevLagIndex = int16(psDec.Indices.lagIndex)

		psDec.Indices.contourIndex = int8(psRangeDec.Dec_icdf(psDec.pitch_contour_iCDF, 8))

		psDec.Indices.PERIndex = int8(psRangeDec.Dec_icdf(SilkTables.Silk_LTP_per_index_iCDF, 8))

		for k = 0; k < psDec.Nb_subfr; k++ {
			ptr := SilkTables.Silk_LTP_gain_iCDF_ptrs[psDec.Indices.PERIndex]
			psDec.Indices.LTPIndex[k] = int8(psRangeDec.Dec_icdf(ptr, 8))
		}

		if condCoding == SilkConstants.CODE_INDEPENDENTLY {
			psDec.Indices.LTP_scaleIndex = int8(psRangeDec.Dec_icdf(SilkTables.Silk_LTPscale_iCDF, 8))
		} else {
			psDec.Indices.LTP_scaleIndex = 0
		}
	}
	psDec.ec_prevSignalType = int(psDec.Indices.SignalType)

	psDec.Indices.Seed = int8(psRangeDec.Dec_icdf(SilkTables.Silk_uniform4_iCDF, 8))
}
