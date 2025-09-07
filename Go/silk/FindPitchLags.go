package silk

import (
	"math"

	"github.com/lostromb/concentus/go/comm"
)

func silk_find_pitch_lags(psEnc *SilkChannelEncoder, psEncCtrl *SilkEncoderControl, res []int16, x []int16, x_ptr int) {
	var buf_len, i int
	var thrhld_Q13, res_nrg int
	var x_buf, x_buf_ptr int
	var Wsig []int16
	var Wsig_ptr int
	var auto_corr [MAX_FIND_PITCH_LPC_ORDER + 1]int
	var rc_Q15 [MAX_FIND_PITCH_LPC_ORDER]int16
	var A_Q24 [MAX_FIND_PITCH_LPC_ORDER]int
	var A_Q12 [MAX_FIND_PITCH_LPC_ORDER]int16

	buf_len = psEnc.la_pitch + psEnc.Frame_length + psEnc.ltp_mem_length

	inlines.OpusAssert(buf_len >= psEnc.pitch_LPC_win_length)

	x_buf = x_ptr - psEnc.ltp_mem_length

	Wsig = make([]int16, psEnc.pitch_LPC_win_length)

	x_buf_ptr = x_buf + buf_len - psEnc.pitch_LPC_win_length
	Wsig_ptr = 0
	silk_apply_sine_window(Wsig, Wsig_ptr, x, x_buf_ptr, 1, psEnc.la_pitch)

	Wsig_ptr += psEnc.la_pitch
	x_buf_ptr += psEnc.la_pitch
	copy(Wsig[Wsig_ptr:], x[x_buf_ptr:x_buf_ptr+(psEnc.pitch_LPC_win_length-inlines.Silk_LSHIFT(psEnc.la_pitch, 1))])

	Wsig_ptr += psEnc.pitch_LPC_win_length - inlines.Silk_LSHIFT(psEnc.la_pitch, 1)
	x_buf_ptr += psEnc.pitch_LPC_win_length - inlines.Silk_LSHIFT(psEnc.la_pitch, 1)
	silk_apply_sine_window(Wsig, Wsig_ptr, x, x_buf_ptr, 2, psEnc.la_pitch)

	boxed_scale := comm.BoxedValueInt{0}

	comm.Silk_autocorr(auto_corr[:], &boxed_scale, Wsig, psEnc.pitch_LPC_win_length, psEnc.pitchEstimationLPCOrder+1)
	//	scale = boxed_scale.Val
	auto_corr[0] = inlines.Silk_SMLAWB(auto_corr[0], auto_corr[0], int((TuningParameters.FIND_PITCH_WHITE_NOISE_FRACTION)*(1<<16)+0.5)) + 1

	res_nrg = silk_schur(rc_Q15[:], auto_corr[:], psEnc.pitchEstimationLPCOrder)

	if res_nrg < 1 {
		res_nrg = 1
	}
	psEncCtrl.predGain_Q16 = inlines.Silk_DIV32_varQ(auto_corr[0], int(res_nrg), 16)

	silk_k2a(A_Q24[:], rc_Q15[:], psEnc.pitchEstimationLPCOrder)

	for i = 0; i < psEnc.pitchEstimationLPCOrder; i++ {
		A_Q12[i] = int16(inlines.Silk_SAT16(inlines.Silk_RSHIFT(A_Q24[i], 12)))
	}

	silk_bwexpander(A_Q12[:], psEnc.pitchEstimationLPCOrder, int((TuningParameters.FIND_PITCH_BANDWIDTH_EXPANSION)*(1<<16)+0.5))

	silk_LPC_analysis_filter(res, 0, x, x_buf, A_Q12[:], 0, buf_len, psEnc.pitchEstimationLPCOrder)

	if int(psEnc.indices.SignalType) != SilkConstants.TYPE_NO_VOICE_ACTIVITY && psEnc.First_frame_after_reset == 0 {

		thrhld_Q13 = int(math.Trunc(((0.6)*float64(int64(1)<<(13)) + 0.5)))

		thrhld_Q13 = inlines.Silk_SMLABB(thrhld_Q13, int(math.Trunc((-0.004)*float64(int64(1)<<(13))+0.5)), psEnc.pitchEstimationLPCOrder)

		thrhld_Q13 = inlines.Silk_SMLAWB(thrhld_Q13, int(math.Trunc((-0.1)*float64(int64(1)<<(21))+0.5)), psEnc.Speech_activity_Q8)
		thrhld_Q13 = inlines.Silk_SMLABB(thrhld_Q13, int(math.Trunc((-0.15)*float64(int64(1)<<(13))+0.5)), inlines.Silk_RSHIFT(int(psEnc.PrevSignalType), 1))
		thrhld_Q13 = inlines.Silk_SMLAWB(thrhld_Q13, int(math.Trunc((-0.1)*float64(int64(1)<<(14))+0.5)), psEnc.input_tilt_Q15)
		thrhld_Q13 = inlines.Silk_SAT16(thrhld_Q13)

		lagIndex := comm.BoxedValueShort{psEnc.indices.lagIndex}
		contourIndex := comm.BoxedValueByte{psEnc.indices.contourIndex}
		LTPcorr_Q15 := comm.BoxedValueInt{psEnc.LTPCorr_Q15}

		if silk_pitch_analysis_core(res, psEncCtrl.pitchL[:], &lagIndex, &contourIndex, &LTPcorr_Q15, psEnc.PrevLag, psEnc.pitchEstimationThreshold_Q16, thrhld_Q13, psEnc.Fs_kHz, psEnc.pitchEstimationComplexity, psEnc.nb_subfr) == 0 {
			psEnc.indices.SignalType = byte(SilkConstants.TYPE_VOICED)
		} else {
			psEnc.indices.SignalType = byte(SilkConstants.TYPE_UNVOICED)
		}

		psEnc.indices.lagIndex = lagIndex.Val

		psEnc.indices.contourIndex = contourIndex.Val

		psEnc.LTPCorr_Q15 = LTPcorr_Q15.Val
	} else {
		for i := range psEncCtrl.pitchL {
			psEncCtrl.pitchL[i] = 0
		}
		psEnc.indices.lagIndex = 0
		psEnc.indices.contourIndex = 0
		psEnc.LTPCorr_Q15 = 0
	}

}
