package silk

import (
	"math"

	"github.com/dosgo/concentus/go/comm"
)

func silk_process_gains(
	psEnc *SilkChannelEncoder,
	psEncCtrl *SilkEncoderControl,
	condCoding int,
) {

	psShapeSt := psEnc.SShape
	var k int
	var s_Q16, InvMaxSqrVal_Q16, gain, gain_squared, ResNrg, ResNrgPart, quant_offset_Q10 int

	/* Gain reduction when LTP coding gain is high */
	if psEnc.indices.SignalType == TYPE_VOICED {
		/*s = -0.5f * silk_sigmoid( 0.25f * ( psEncCtrl.LTPredCodGain - 12.0f ) ); */
		s_Q16 = 0 - silk_sigm_Q15(inlines.Silk_RSHIFT_ROUND(psEncCtrl.LTPredCodGain_Q7-(int(math.Trunc(12.0*float64(int64(1)<<(7))+0.5))), 4))

		for k = 0; k < psEnc.nb_subfr; k++ {
			psEncCtrl.Gains_Q16[k] = inlines.Silk_SMLAWB(psEncCtrl.Gains_Q16[k], psEncCtrl.Gains_Q16[k], s_Q16)
		}
	}

	/* Limit the quantized signal */
	/* InvMaxSqrVal = pow( 2.0f, 0.33f * ( 21.0f - SNR_dB ) ) / subfr_length; */
	InvMaxSqrVal_Q16 = inlines.Silk_DIV32_16(inlines.Silk_log2lin(
		inlines.Silk_SMULWB((int(math.Trunc((21+16/0.33)*float64(int64(1)<<(7))+0.5)))-psEnc.SNR_dB_Q7, int(math.Trunc(0.33*float64(int64(1)<<16)+0.5)))), psEnc.subfr_length)

	for k = 0; k < psEnc.nb_subfr; k++ {
		/* Soft limit on ratio residual energy and squared gains */
		ResNrg = psEncCtrl.ResNrg[k]
		ResNrgPart = inlines.Silk_SMULWW(ResNrg, InvMaxSqrVal_Q16)

		if psEncCtrl.ResNrgQ[k] > 0 {
			ResNrgPart = inlines.Silk_RSHIFT_ROUND(ResNrgPart, psEncCtrl.ResNrgQ[k])
		} else if ResNrgPart >= inlines.Silk_RSHIFT(math.MaxInt32, -psEncCtrl.ResNrgQ[k]) {
			ResNrgPart = math.MaxInt32
		} else {
			ResNrgPart = inlines.Silk_LSHIFT(ResNrgPart, -psEncCtrl.ResNrgQ[k])
		}
		gain = psEncCtrl.Gains_Q16[k]
		gain_squared = inlines.Silk_ADD_SAT32(ResNrgPart, inlines.Silk_SMMUL(gain, gain))

		if gain_squared < math.MaxInt16 {
			/* recalculate with higher precision */
			gain_squared = inlines.Silk_SMLAWW(inlines.Silk_LSHIFT(ResNrgPart, 16), gain, gain)
			inlines.OpusAssert(gain_squared > 0)
			gain = inlines.Silk_SQRT_APPROX(gain_squared)
			/* Q8   */
			gain = inlines.Silk_min(gain, math.MaxInt32>>8)
			psEncCtrl.Gains_Q16[k] = inlines.Silk_LSHIFT_SAT32(gain, 8)

			/* Q16  */
		} else {

			gain = inlines.Silk_SQRT_APPROX(gain_squared)
			/* Q0   */
			gain = inlines.Silk_min(gain, math.MaxInt32>>16)
			psEncCtrl.Gains_Q16[k] = inlines.Silk_LSHIFT_SAT32(gain, 16)

			/* Q16  */
		}

	}

	/* Save unquantized gains and gain Index */
	//System.arraycopy(psEncCtrl.Gains_Q16, 0, psEncCtrl.GainsUnq_Q16, 0, psEnc.nb_subfr)
	copy(psEncCtrl.GainsUnq_Q16[:], psEncCtrl.Gains_Q16[:psEnc.nb_subfr])
	psEncCtrl.lastGainIndexPrev = psShapeSt.LastGainIndex

	/* Quantize gains */
	boxed_lastGainIndex := &comm.BoxedValueByte{psShapeSt.LastGainIndex}

	silk_gains_quant(psEnc.indices.GainsIndices, psEncCtrl.Gains_Q16,
		boxed_lastGainIndex, comm.BoolToInt(condCoding == SilkConstants.CODE_CONDITIONALLY), psEnc.nb_subfr)

	psShapeSt.LastGainIndex = boxed_lastGainIndex.Val

	/* Set quantizer offset for voiced signals. Larger offset when LTP coding gain is low or tilt is high (ie low-pass) */
	if psEnc.indices.SignalType == TYPE_VOICED {
		if psEncCtrl.LTPredCodGain_Q7+inlines.Silk_RSHIFT(psEnc.input_tilt_Q15, 8) > (int(math.Trunc(1.0*float64(int64(1)<<(7)) + 0.5))) {
			psEnc.indices.QuantOffsetType = 0
		} else {
			psEnc.indices.QuantOffsetType = 1
		}
	}

	/* Quantizer boundary adjustment */
	quant_offset_Q10 = int(silk_Quantization_Offsets_Q10[psEnc.indices.SignalType>>1][psEnc.indices.QuantOffsetType])
	psEncCtrl.Lambda_Q10 = (int(float64(TuningParameters.LAMBDA_OFFSET)*float64(int64(1)<<(10)) + 0.5)) +
		inlines.Silk_SMULBB((int(float64(TuningParameters.LAMBDA_DELAYED_DECISIONS)*float64(int64(1)<<(10))+0.5)), psEnc.nStatesDelayedDecision) +
		inlines.Silk_SMULWB((int(float64(TuningParameters.LAMBDA_SPEECH_ACT)*float64(int64(1)<<(18))+0.5)), psEnc.Speech_activity_Q8) +
		inlines.Silk_SMULWB((int(float64(TuningParameters.LAMBDA_INPUT_QUALITY)*float64(int64(1)<<(12))+0.5)), psEncCtrl.input_quality_Q14) +
		inlines.Silk_SMULWB((int(float64(TuningParameters.LAMBDA_CODING_QUALITY)*float64(int64(1)<<(12))+0.5)), psEncCtrl.coding_quality_Q14) +
		inlines.Silk_SMULWB((int(float64(TuningParameters.LAMBDA_QUANT_OFFSET)*float64(int64(1)<<(16))+0.5)), quant_offset_Q10)

	inlines.OpusAssert(psEncCtrl.Lambda_Q10 > 0)
	inlines.OpusAssert(psEncCtrl.Lambda_Q10 < (int(math.Trunc(2*float64(int64(1)<<(10)) + 0.5))))

}
