package silk

func silk_decode_pitch(
	lagIndex int16,
	contourIndex int8,
	pitch_lags []int,
	Fs_kHz int,
	nb_subfr int,
) {
	var lag, k, min_lag, max_lag int
	var Lag_CB_ptr [][]int8

	if Fs_kHz == 8 {
		if nb_subfr == PE_MAX_NB_SUBFR {
			Lag_CB_ptr = silk_CB_lags_stage2
		} else {
			inlines.OpusAssert(nb_subfr == (PE_MAX_NB_SUBFR >> 1))
			Lag_CB_ptr = silk_CB_lags_stage2_10_ms
		}
	} else {
		if nb_subfr == PE_MAX_NB_SUBFR {
			Lag_CB_ptr = silk_CB_lags_stage3
		} else {
			inlines.OpusAssert(nb_subfr == (PE_MAX_NB_SUBFR >> 1))
			Lag_CB_ptr = silk_CB_lags_stage3_10_ms
		}
	}

	min_lag = inlines.Silk_SMULBB(PE_MIN_LAG_MS, Fs_kHz)
	max_lag = inlines.Silk_SMULBB(PE_MAX_LAG_MS, Fs_kHz)
	lag = min_lag + int(lagIndex)

	for k = 0; k < nb_subfr; k++ {
		pitch_lags[k] = lag + int(Lag_CB_ptr[k][contourIndex])
		pitch_lags[k] = inlines.Silk_LIMIT(pitch_lags[k], min_lag, max_lag)
	}
}
