package opus

import "github.com/lostromb/concentus/go/silk"

func silk_HP_variable_cutoff(state_Fxx []*silk.SilkChannelEncoder) {
	var quality_Q15 int
	var pitch_freq_Hz_Q16, pitch_freq_log_Q7, delta_freq_Q7 int
	psEncC1 := state_Fxx[0]

	if int(psEncC1.PrevSignalType) == SilkConstants.TYPE_VOICED {
		pitch_freq_Hz_Q16 = inlines.Silk_DIV32_16(inlines.Silk_LSHIFT(inlines.Silk_MUL(psEncC1.Fs_kHz, 1000), 16), psEncC1.PrevLag)
		pitch_freq_log_Q7 = inlines.Silk_lin2log(pitch_freq_Hz_Q16) - (16 << 7)

		quality_Q15 = psEncC1.Input_quality_bands_Q15[0]
		min_cutoff_log_Q7 := inlines.Silk_lin2log(int(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ)) - (16 << 7)
		pitch_freq_log_Q7 = inlines.Silk_SMLAWB(pitch_freq_log_Q7, inlines.Silk_SMULWB(inlines.Silk_LSHIFT(-quality_Q15, 2), quality_Q15), pitch_freq_log_Q7-min_cutoff_log_Q7)

		delta_freq_Q7 = pitch_freq_log_Q7 - inlines.Silk_RSHIFT(psEncC1.Variable_HP_smth1_Q15, 8)
		if delta_freq_Q7 < 0 {
			delta_freq_Q7 = inlines.Silk_MUL(delta_freq_Q7, 3)
		}

		max_delta_freq_Q7 := int(TuningParameters.VARIABLE_HP_MAX_DELTA_FREQ*(1<<7) + 0.5)
		delta_freq_Q7 = inlines.Silk_LIMIT_32(delta_freq_Q7, -max_delta_freq_Q7, max_delta_freq_Q7)

		smth_coef1_Q16 := int(TuningParameters.VARIABLE_HP_SMTH_COEF1*(1<<16) + 0.5)
		psEncC1.Variable_HP_smth1_Q15 = inlines.Silk_SMLAWB(psEncC1.Variable_HP_smth1_Q15, inlines.Silk_SMULBB(psEncC1.Speech_activity_Q8, delta_freq_Q7), smth_coef1_Q16)

		min_cutoff_log_Q8 := inlines.Silk_LSHIFT(inlines.Silk_lin2log(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ), 8)
		max_cutoff_log_Q8 := inlines.Silk_LSHIFT(inlines.Silk_lin2log(TuningParameters.VARIABLE_HP_MAX_CUTOFF_HZ), 8)
		psEncC1.Variable_HP_smth1_Q15 = inlines.Silk_LIMIT_32(psEncC1.Variable_HP_smth1_Q15, min_cutoff_log_Q8, max_cutoff_log_Q8)
	}
}
