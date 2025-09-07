package silk

import "github.com/lostromb/concentus/go/comm"

func Silk_encode_indices(psEncC *SilkChannelEncoder, psRangeEnc *comm.EntropyCoder, FrameIndex int, encode_LBRR int, condCoding int) {
	var i, k, typeOffset int
	var encode_absolute_lagIndex, delta_lagIndex int
	ec_ix := make([]int16, SilkConstants.MAX_LPC_ORDER)
	pred_Q8 := make([]int16, SilkConstants.MAX_LPC_ORDER)
	var psIndices *SideInfoIndices

	if encode_LBRR != 0 {
		psIndices = psEncC.Indices_LBRR[FrameIndex]
	} else {
		psIndices = psEncC.indices
	}

	/**
	 * ****************************************
	 */
	/* Encode signal type and quantizer offset */
	/**
	 * ****************************************
	 */

	typeOffset = 2*int(psIndices.SignalType) + int(psIndices.QuantOffsetType)
	inlines.OpusAssert(typeOffset >= 0 && typeOffset < 6)
	inlines.OpusAssert(encode_LBRR == 0 || typeOffset >= 2)

	if encode_LBRR != 0 || typeOffset >= 2 {
		psRangeEnc.Enc_icdf(typeOffset-2, silk_type_offset_VAD_iCDF, 8)
	} else {
		psRangeEnc.Enc_icdf(typeOffset, silk_type_offset_no_VAD_iCDF, 8)
	}

	/**
	 * *************
	 */
	/* Encode gains */
	/**
	 * *************
	 */
	/* first subframe */
	if condCoding == SilkConstants.CODE_CONDITIONALLY {
		/* conditional coding */
		inlines.OpusAssert(psIndices.GainsIndices[0] >= 0 && psIndices.GainsIndices[0] < MAX_DELTA_GAIN_QUANT-MIN_DELTA_GAIN_QUANT+1)
		psRangeEnc.Enc_icdf(int((psIndices.GainsIndices[0])), silk_delta_gain_iCDF, 8)
	} else {
		/* independent coding, in two stages: MSB bits followed by 3 LSBs */
		inlines.OpusAssert(psIndices.GainsIndices[0] >= 0 && psIndices.GainsIndices[0] < N_LEVELS_QGAIN)

		psRangeEnc.Enc_icdf(inlines.Silk_RSHIFT(int(psIndices.GainsIndices[0]), 3), silk_gain_iCDF[psIndices.SignalType], 8)

		psRangeEnc.Enc_icdf(int(psIndices.GainsIndices[0]&7), silk_uniform8_iCDF, 8)
	}
	/* remaining subframes */

	for i = 1; i < psEncC.nb_subfr; i++ {
		inlines.OpusAssert(psIndices.GainsIndices[i] >= 0 && psIndices.GainsIndices[i] < MAX_DELTA_GAIN_QUANT-MIN_DELTA_GAIN_QUANT+1)
		//    System.out.printf("psIndices.GainsIndices[i]:%s\r\n", java.util.Arrays.toString(psIndices.GainsIndices));
		psRangeEnc.Enc_icdf(int(psIndices.GainsIndices[i]), silk_delta_gain_iCDF, 8)

	}

	/**
	 * *************
	 */
	/* Encode NLSFs */
	/**
	 * *************
	 */

	psRangeEnc.Enc_icdf_offset(int(psIndices.NLSFIndices[0]), psEncC.psNLSF_CB.CB1_iCDF, int(int16(psIndices.SignalType>>1)*psEncC.psNLSF_CB.nVectors), 8)
	silk_NLSF_unpack(ec_ix, pred_Q8, psEncC.psNLSF_CB, int(psIndices.NLSFIndices[0]))
	inlines.OpusAssert(int(psEncC.psNLSF_CB.order) == psEncC.predictLPCOrder)
	for i = 0; i < int(psEncC.psNLSF_CB.order); i++ {
		if psIndices.NLSFIndices[i+1] >= NLSF_QUANT_MAX_AMPLITUDE {
			psRangeEnc.Enc_icdf_offset(2*SilkConstants.NLSF_QUANT_MAX_AMPLITUDE, psEncC.psNLSF_CB.ec_iCDF, int(ec_ix[i]), 8)
			psRangeEnc.Enc_icdf(int(psIndices.NLSFIndices[i+1]-NLSF_QUANT_MAX_AMPLITUDE), silk_NLSF_EXT_iCDF, 8)
		} else if psIndices.NLSFIndices[i+1] <= 0-NLSF_QUANT_MAX_AMPLITUDE {
			psRangeEnc.Enc_icdf_offset(0, psEncC.psNLSF_CB.ec_iCDF, int(ec_ix[i]), 8)
			psRangeEnc.Enc_icdf(-int(psIndices.NLSFIndices[i+1])-NLSF_QUANT_MAX_AMPLITUDE, silk_NLSF_EXT_iCDF, 8)
		} else {

			psRangeEnc.Enc_icdf_offset(int(psIndices.NLSFIndices[i+1])+NLSF_QUANT_MAX_AMPLITUDE, psEncC.psNLSF_CB.ec_iCDF, int(ec_ix[i]), 8)
		}
	}

	/* Encode NLSF interpolation factor */
	if psEncC.nb_subfr == SilkConstants.MAX_NB_SUBFR {
		inlines.OpusAssert(psIndices.NLSFInterpCoef_Q2 >= 0 && psIndices.NLSFInterpCoef_Q2 < 5)
		psRangeEnc.Enc_icdf(int(psIndices.NLSFInterpCoef_Q2), silk_NLSF_interpolation_factor_iCDF, 8)
	}

	if psIndices.SignalType == TYPE_VOICED {
		/**
		 * ******************
		 */
		/* Encode pitch lags */
		/**
		 * ******************
		 */
		/* lag index */
		encode_absolute_lagIndex = 1
		if condCoding == SilkConstants.CODE_CONDITIONALLY && psEncC.ec_prevSignalType == SilkConstants.TYPE_VOICED {
			/* Delta Encoding */
			delta_lagIndex = int(psIndices.lagIndex - psEncC.ec_prevLagIndex)

			if delta_lagIndex < -8 || delta_lagIndex > 11 {
				delta_lagIndex = 0
			} else {
				delta_lagIndex = delta_lagIndex + 9
				encode_absolute_lagIndex = 0
				/* Only use delta */
			}

			inlines.OpusAssert(delta_lagIndex >= 0 && delta_lagIndex < 21)
			psRangeEnc.Enc_icdf(delta_lagIndex, silk_pitch_delta_iCDF, 8)
		}

		if encode_absolute_lagIndex != 0 {
			/* Absolute encoding */
			var pitch_high_bits, pitch_low_bits int
			pitch_high_bits = inlines.Silk_DIV32_16(int(psIndices.lagIndex), inlines.Silk_RSHIFT(psEncC.Fs_kHz, 1))
			pitch_low_bits = int(psIndices.lagIndex) - inlines.Silk_SMULBB(pitch_high_bits, inlines.Silk_RSHIFT(psEncC.Fs_kHz, 1))
			inlines.OpusAssert(pitch_low_bits < psEncC.Fs_kHz/2)
			inlines.OpusAssert(pitch_high_bits < 32)
			psRangeEnc.Enc_icdf(pitch_high_bits, silk_pitch_lag_iCDF, 8)
			psRangeEnc.Enc_icdf(pitch_low_bits, psEncC.pitch_lag_low_bits_iCDF, 8)
		}
		psEncC.ec_prevLagIndex = psIndices.lagIndex

		/* Countour index */
		inlines.OpusAssert(psIndices.contourIndex >= 0)
		inlines.OpusAssert((psIndices.contourIndex < 34 && psEncC.Fs_kHz > 8 && psEncC.nb_subfr == 4) || (psIndices.contourIndex < 11 && psEncC.Fs_kHz == 8 && psEncC.nb_subfr == 4) || (psIndices.contourIndex < 12 && psEncC.Fs_kHz > 8 && psEncC.nb_subfr == 2) || (psIndices.contourIndex < 3 && psEncC.Fs_kHz == 8 && psEncC.nb_subfr == 2))

		psRangeEnc.Enc_icdf(int(psIndices.contourIndex), psEncC.pitch_contour_iCDF, 8)

		/**
		 * *****************
		 */
		/* Encode LTP gains */
		/**
		 * *****************
		 */
		/* PERIndex value */
		inlines.OpusAssert(psIndices.PERIndex >= 0 && psIndices.PERIndex < 3)
		psRangeEnc.Enc_icdf(int(psIndices.PERIndex), silk_LTP_per_index_iCDF, 8)

		/* Codebook Indices */
		for k = 0; k < psEncC.nb_subfr; k++ {
			inlines.OpusAssert(psIndices.LTPIndex[k] >= 0 && psIndices.LTPIndex[k] < (8<<psIndices.PERIndex))

			psRangeEnc.Enc_icdf(int(psIndices.LTPIndex[k]), silk_LTP_gain_iCDF_ptrs[psIndices.PERIndex], 8)
		}

		/**
		 * *******************
		 */
		/* Encode LTP scaling */
		/**
		 * *******************
		 */
		if condCoding == SilkConstants.CODE_INDEPENDENTLY {
			inlines.OpusAssert(psIndices.LTP_scaleIndex >= 0 && psIndices.LTP_scaleIndex < 3)
			psRangeEnc.Enc_icdf(int(psIndices.LTP_scaleIndex), silk_LTPscale_iCDF, 8)
		}

		inlines.OpusAssert(condCoding == 0 || psIndices.LTP_scaleIndex == 0)
	}

	psEncC.ec_prevSignalType = int(psIndices.SignalType)

	/**
	 * ************
	 */
	/* Encode seed */
	/**
	 * ************
	 */
	inlines.OpusAssert(psIndices.Seed >= 0 && psIndices.Seed < 4)
	psRangeEnc.Enc_icdf(int(psIndices.Seed), silk_uniform4_iCDF, 8)

}
