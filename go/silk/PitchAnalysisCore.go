package silk

import (
	"math"

	"github.com/lostromb/concentus/go/comm"
	"github.com/lostromb/concentus/go/comm/arrayUtil"
)

const (
	SCRATCH_SIZE   = 22
	SF_LENGTH_4KHZ = PE_SUBFR_LENGTH_MS * 4
	SF_LENGTH_8KHZ = PE_SUBFR_LENGTH_MS * 8
	MIN_LAG_4KHZ   = PE_MIN_LAG_MS * 4
	MIN_LAG_8KHZ   = PE_MIN_LAG_MS * 8
	MAX_LAG_4KHZ   = PE_MAX_LAG_MS * 4
	MAX_LAG_8KHZ   = PE_MAX_LAG_MS*8 - 1
	CSTRIDE_4KHZ   = MAX_LAG_4KHZ + 1 - MIN_LAG_4KHZ
	CSTRIDE_8KHZ   = MAX_LAG_8KHZ + 3 - (MIN_LAG_8KHZ - 2)
	D_COMP_MIN     = MIN_LAG_8KHZ - 3
	D_COMP_MAX     = MAX_LAG_8KHZ + 4
	D_COMP_STRIDE  = D_COMP_MAX - D_COMP_MIN
)

func silk_pitch_analysis_core(frame []int16, pitch_out []int, lagIndex *comm.BoxedValueShort, contourIndex *comm.BoxedValueByte, LTPCorr_Q15 *comm.BoxedValueInt, prevLag int, search_thres1_Q16 int, search_thres2_Q13 int, Fs_kHz int, complexity int, nb_subfr int) int {

	var frame_8kHz []int16
	var frame_4kHz []int16
	filt_state := make([]int, 6)
	var input_frame_ptr []int16
	var i, k, d, j int
	var C []int16
	var xcorr32 []int
	var basis []int16
	var basis_ptr int
	var target []int16
	var target_ptr int
	var cross_corr, normalizer, energy, shift, energy_basis, energy_target int
	var Cmax, length_d_srch, length_d_comp int
	d_srch := make([]int, SilkConstants.PE_D_SRCH_LENGTH)
	var d_comp []int16
	var sum, threshold, lag_counter int
	var CBimax, CBimax_new, CBimax_old, lag, start_lag, end_lag, lag_new int
	var CCmax, CCmax_b, CCmax_new_b, CCmax_new int
	CC := make([]int, SilkConstants.PE_NB_CBKS_STAGE2_EXT)
	//var energies_st3 []silk_pe_stage3_vals
	//var cross_corr_st3 []silk_pe_stage3_vals
	var frame_length, frame_length_8kHz, frame_length_4kHz int
	var sf_length int
	var min_lag int
	var max_lag int
	var contour_bias_Q15, diff int
	var nb_cbk_search int
	var delta_lag_log2_sqr_Q7, lag_log2_Q7, prevLag_log2_Q7, prev_lag_bias_Q13 int
	var Lag_CB_ptr [][]int8

	/* Check for valid sampling frequency */
	inlines.OpusAssert(Fs_kHz == 8 || Fs_kHz == 12 || Fs_kHz == 16)

	/* Check for valid complexity setting */
	inlines.OpusAssert(complexity >= SilkConstants.SILK_PE_MIN_COMPLEX)
	inlines.OpusAssert(complexity <= SilkConstants.SILK_PE_MAX_COMPLEX)

	inlines.OpusAssert(search_thres1_Q16 >= 0 && search_thres1_Q16 <= (1<<16))
	inlines.OpusAssert(search_thres2_Q13 >= 0 && search_thres2_Q13 <= (1<<13))

	/* Set up frame lengths max / min lag for the sampling frequency */
	frame_length = (SilkConstants.PE_LTP_MEM_LENGTH_MS + nb_subfr*SilkConstants.PE_SUBFR_LENGTH_MS) * Fs_kHz
	frame_length_4kHz = (SilkConstants.PE_LTP_MEM_LENGTH_MS + nb_subfr*SilkConstants.PE_SUBFR_LENGTH_MS) * 4
	frame_length_8kHz = (SilkConstants.PE_LTP_MEM_LENGTH_MS + nb_subfr*SilkConstants.PE_SUBFR_LENGTH_MS) * 8
	sf_length = SilkConstants.PE_SUBFR_LENGTH_MS * Fs_kHz
	min_lag = SilkConstants.PE_MIN_LAG_MS * Fs_kHz
	max_lag = SilkConstants.PE_MAX_LAG_MS*Fs_kHz - 1

	/* Resample from input sampled at Fs_kHz to 8 kHz */
	frame_8kHz = make([]int16, frame_length_8kHz)

	if Fs_kHz == 16 {
		arrayUtil.MemSetLen(filt_state, 0, 2)
		silk_resampler_down2(filt_state, frame_8kHz, frame, frame_length)
	} else if Fs_kHz == 12 {
		arrayUtil.MemSetLen(filt_state, 0, 6)
		silk_resampler_down2_3(filt_state, frame_8kHz, frame, frame_length)
	} else {
		inlines.OpusAssert(Fs_kHz == 8)
		//System.arraycopy(frame, 0, frame_8kHz, 0, frame_length_8kHz)
		copy(frame_8kHz, frame[:frame_length_8kHz])
	}

	/* Decimate again to 4 kHz */
	arrayUtil.MemSetLen(filt_state, 0, 2)
	/* Set state to zero */
	frame_4kHz = make([]int16, frame_length_4kHz)
	silk_resampler_down2(filt_state, frame_4kHz, frame_8kHz, frame_length_8kHz)

	/* Low-pass filter */
	for i = frame_length_4kHz - 1; i > 0; i-- {
		frame_4kHz[i] = inlines.Silk_ADD_SAT16(frame_4kHz[i], frame_4kHz[i-1])
	}

	/**
	   * *****************************************************************************
	   ** Scale 4 kHz signal down to prevent correlations measures from
	   * overflowing * find scaling as max scaling for each 8kHz(?) subframe
	  ******************************************************************************
	*/

	/* Inner product is calculated with different lengths, so scale for the worst case */
	boxed_energy := comm.BoxedValueInt{0}
	boxed_shift := comm.BoxedValueInt{0}
	silk_sum_sqr_shift4(&boxed_energy, &boxed_shift, frame_4kHz, frame_length_4kHz)
	energy = boxed_energy.Val
	shift = boxed_shift.Val

	if shift > 0 {
		shift = inlines.Silk_RSHIFT(shift, 1)
		for i = 0; i < frame_length_4kHz; i++ {
			frame_4kHz[i] = inlines.Silk_RSHIFT16(frame_4kHz[i], shift)
		}
	}

	/**
	   * ****************************************************************************
	   * FIRST STAGE, operating in 4 khz
	  *****************************************************************************
	*/
	C = make([]int16, nb_subfr*CSTRIDE_8KHZ)
	xcorr32 = make([]int, MAX_LAG_4KHZ-MIN_LAG_4KHZ+1)
	arrayUtil.MemSetLen(C, 0, (nb_subfr>>1)*CSTRIDE_4KHZ)
	target = frame_4kHz
	target_ptr = inlines.Silk_LSHIFT(SF_LENGTH_4KHZ, 2)

	for k = 0; k < nb_subfr>>1; k++ {
		basis = target
		basis_ptr = target_ptr - MIN_LAG_4KHZ

		comm.Pitch_xcorr1(target, target_ptr, target, target_ptr-MAX_LAG_4KHZ, xcorr32, SF_LENGTH_8KHZ, MAX_LAG_4KHZ-MIN_LAG_4KHZ+1)

		/* Calculate first vector products before loop */
		cross_corr = xcorr32[MAX_LAG_4KHZ-MIN_LAG_4KHZ]
		normalizer = inlines.Silk_inner_prod_self(target, target_ptr, SF_LENGTH_8KHZ)

		normalizer = inlines.Silk_ADD32(normalizer, inlines.Silk_inner_prod_self(basis, basis_ptr, SF_LENGTH_8KHZ))
		normalizer = inlines.Silk_ADD32(normalizer, inlines.Silk_SMULBB(SF_LENGTH_8KHZ, 4000))

		inlines.MatrixSetShort5(C, k, 0, CSTRIDE_4KHZ,
			int16(inlines.Silk_DIV32_varQ(cross_corr, normalizer, 13+1)))
		/* Q13 */

		/* From now on normalizer is computed recursively */
		for d = MIN_LAG_4KHZ + 1; d <= MAX_LAG_4KHZ; d++ {
			basis_ptr--

			cross_corr = xcorr32[MAX_LAG_4KHZ-d]

			/* Add contribution of new sample and remove contribution from oldest sample */
			normalizer = inlines.Silk_ADD32(normalizer,
				inlines.Silk_SMULBB(int(basis[basis_ptr]), int(basis[basis_ptr]))-
					inlines.Silk_SMULBB(int(basis[basis_ptr+SF_LENGTH_8KHZ]), int(basis[basis_ptr+SF_LENGTH_8KHZ])))

			inlines.MatrixSetShort5(C, k, d-MIN_LAG_4KHZ, CSTRIDE_4KHZ,
				int16(inlines.Silk_DIV32_varQ(cross_corr, normalizer, 13+1)))
			/* Q13 */
		}
		/* Update target pointer */
		target_ptr += SF_LENGTH_8KHZ
	}

	/* Combine two subframes into single correlation measure and apply short-lag bias */
	if nb_subfr == SilkConstants.PE_MAX_NB_SUBFR {
		for i = MAX_LAG_4KHZ; i >= MIN_LAG_4KHZ; i-- {
			sum = int(inlines.MatrixGetShort(C, 0, i-MIN_LAG_4KHZ, CSTRIDE_4KHZ)) +
				int(inlines.MatrixGetShort(C, 1, i-MIN_LAG_4KHZ, CSTRIDE_4KHZ))
			/* Q14 */
			sum = inlines.Silk_SMLAWB(sum, sum, inlines.Silk_LSHIFT(-i, 4))
			/* Q14 */
			C[i-MIN_LAG_4KHZ] = int16(sum)
			/* Q14 */
		}
	} else {
		/* Only short-lag bias */
		for i = MAX_LAG_4KHZ; i >= MIN_LAG_4KHZ; i-- {
			sum = inlines.Silk_LSHIFT(int(C[i-MIN_LAG_4KHZ]), 1)
			/* Q14 */
			sum = inlines.Silk_SMLAWB(sum, sum, inlines.Silk_LSHIFT(-i, 4))
			/* Q14 */
			C[i-MIN_LAG_4KHZ] = int16(sum)
			/* Q14 */
		}
	}

	/* Sort */
	length_d_srch = inlines.Silk_ADD_LSHIFT32(4, complexity, 1)
	inlines.OpusAssert(3*length_d_srch <= SilkConstants.PE_D_SRCH_LENGTH)
	silk_insertion_sort_decreasing_int16(C, d_srch, CSTRIDE_4KHZ, length_d_srch)

	/* Escape if correlation is very low already here */
	Cmax = int(C[0])
	/* Q14 */
	if Cmax < int(math.Trunc((0.2)*float64(int64(1)<<(14))+0.5)) {
		arrayUtil.MemSetLen(pitch_out, 0, nb_subfr)
		LTPCorr_Q15.Val = 0
		lagIndex.Val = 0
		contourIndex.Val = 0
		return 1
	}

	threshold = inlines.Silk_SMULWB(search_thres1_Q16, Cmax)
	for i = 0; i < length_d_srch; i++ {
		/* Convert to 8 kHz indices for the sorted correlation that exceeds the threshold */
		if int(C[i]) > threshold {
			d_srch[i] = inlines.Silk_LSHIFT(d_srch[i]+MIN_LAG_4KHZ, 1)
		} else {
			length_d_srch = i
			break
		}
	}
	inlines.OpusAssert(length_d_srch > 0)

	d_comp = make([]int16, D_COMP_STRIDE)
	for i = D_COMP_MIN; i < D_COMP_MAX; i++ {
		d_comp[i-D_COMP_MIN] = 0
	}
	for i = 0; i < length_d_srch; i++ {
		d_comp[d_srch[i]-D_COMP_MIN] = 1
	}

	/* Convolution */
	for i = D_COMP_MAX - 1; i >= MIN_LAG_8KHZ; i-- {
		d_comp[i-D_COMP_MIN] += (d_comp[i-1-D_COMP_MIN] + d_comp[i-2-D_COMP_MIN])
	}

	length_d_srch = 0
	for i = MIN_LAG_8KHZ; i < MAX_LAG_8KHZ+1; i++ {
		if d_comp[i+1-D_COMP_MIN] > 0 {
			d_srch[length_d_srch] = i
			length_d_srch++
		}
	}

	/* Convolution */
	for i = D_COMP_MAX - 1; i >= MIN_LAG_8KHZ; i-- {
		d_comp[i-D_COMP_MIN] += (d_comp[i-1-D_COMP_MIN] + d_comp[i-2-D_COMP_MIN] + d_comp[i-3-D_COMP_MIN])
	}

	length_d_comp = 0
	for i = MIN_LAG_8KHZ; i < D_COMP_MAX; i++ {
		if d_comp[i-D_COMP_MIN] > 0 {
			d_comp[length_d_comp] = int16(i - 2)
			length_d_comp++
		}
	}

	/**
	   * ********************************************************************************
	   ** SECOND STAGE, operating at 8 kHz, on lag sections with high
	   * correlation
	  ************************************************************************************
	*/
	/**
	   * ****************************************************************************
	   ** Scale signal down to avoid correlations measures from overflowing
	  ******************************************************************************
	*/
	/* find scaling as max scaling for each subframe */
	boxed_energy.Val = 0
	boxed_shift.Val = 0
	silk_sum_sqr_shift4(&boxed_energy, &boxed_shift, frame_8kHz, frame_length_8kHz)
	energy = boxed_energy.Val
	shift = boxed_shift.Val

	if shift > 0 {
		shift = inlines.Silk_RSHIFT(shift, 1)
		for i = 0; i < frame_length_8kHz; i++ {
			frame_8kHz[i] = inlines.Silk_RSHIFT16(frame_8kHz[i], shift)
		}
	}

	/**
	   * *******************************************************************************
	   * Find energy of each subframe projected onto its history, for a range
	   * of delays
	  ********************************************************************************
	*/
	arrayUtil.MemSetLen(C, 0, nb_subfr*CSTRIDE_8KHZ)

	target = frame_8kHz
	target_ptr = SilkConstants.PE_LTP_MEM_LENGTH_MS * 8
	for k = 0; k < nb_subfr; k++ {

		energy_target = inlines.Silk_ADD32(inlines.Silk_inner_prod(target, target_ptr, target, target_ptr, SF_LENGTH_8KHZ), 1)
		for j = 0; j < length_d_comp; j++ {
			d = int(d_comp[j])
			basis = target
			basis_ptr = target_ptr - d

			cross_corr = inlines.Silk_inner_prod(target, target_ptr, basis, basis_ptr, SF_LENGTH_8KHZ)
			if cross_corr > 0 {
				energy_basis = inlines.Silk_inner_prod_self(basis, basis_ptr, SF_LENGTH_8KHZ)
				inlines.MatrixSetShort5(C, k, d-(MIN_LAG_8KHZ-2), CSTRIDE_8KHZ,
					int16(inlines.Silk_DIV32_varQ(cross_corr, inlines.Silk_ADD32(energy_target, energy_basis), 13+1)))
				/* Q13 */
			} else {
				inlines.MatrixSetShort5(C, k, d-(MIN_LAG_8KHZ-2), CSTRIDE_8KHZ, 0)
			}
		}
		target_ptr += SF_LENGTH_8KHZ
	}

	/* search over lag range and lags codebook */
	/* scale factor for lag codebook, as a function of center lag */
	CCmax = math.MinInt32
	CCmax_b = math.MinInt32

	CBimax = 0
	/* To avoid returning undefined lag values */
	lag = -1
	/* To check if lag with strong enough correlation has been found */

	if prevLag > 0 {
		if Fs_kHz == 12 {
			prevLag = inlines.Silk_DIV32_16(inlines.Silk_LSHIFT(prevLag, 1), 3)
		} else if Fs_kHz == 16 {
			prevLag = inlines.Silk_RSHIFT(prevLag, 1)
		}
		prevLag_log2_Q7 = inlines.Silk_lin2log(prevLag)
	} else {
		prevLag_log2_Q7 = 0
	}
	inlines.OpusAssert(search_thres2_Q13 == inlines.Silk_SAT16(search_thres2_Q13))
	/* Set up stage 2 codebook based on number of subframes */
	if nb_subfr == SilkConstants.PE_MAX_NB_SUBFR {
		Lag_CB_ptr = silk_CB_lags_stage2
		if Fs_kHz == 8 && complexity > SilkConstants.SILK_PE_MIN_COMPLEX {
			/* If input is 8 khz use a larger codebook here because it is last stage */
			nb_cbk_search = SilkConstants.PE_NB_CBKS_STAGE2_EXT
		} else {
			nb_cbk_search = SilkConstants.PE_NB_CBKS_STAGE2
		}
	} else {
		Lag_CB_ptr = silk_CB_lags_stage2_10_ms
		nb_cbk_search = SilkConstants.PE_NB_CBKS_STAGE2_10MS
	}

	for k = 0; k < length_d_srch; k++ {
		d = d_srch[k]
		for j = 0; j < nb_cbk_search; j++ {
			CC[j] = 0
			for i = 0; i < nb_subfr; i++ {
				var d_subfr int
				/* Try all codebooks */
				d_subfr = d + int(Lag_CB_ptr[i][j])
				CC[j] = CC[j] +
					int(inlines.MatrixGetShort(C, i, d_subfr-(MIN_LAG_8KHZ-2), CSTRIDE_8KHZ))
			}
		}
		/* Find best codebook */
		CCmax_new = math.MinInt32
		CBimax_new = 0
		for i = 0; i < nb_cbk_search; i++ {
			if CC[i] > CCmax_new {
				CCmax_new = CC[i]
				CBimax_new = i
			}
		}

		/* Bias towards shorter lags */
		lag_log2_Q7 = inlines.Silk_lin2log(d)
		/* Q7 */
		inlines.OpusAssert(lag_log2_Q7 == inlines.Silk_SAT16(lag_log2_Q7))
		inlines.OpusAssert(nb_subfr*int(float64(SilkConstants.PE_SHORTLAG_BIAS)*float64(int64(1)<<(13))+0.5) == inlines.Silk_SAT16(nb_subfr*int(float64(SilkConstants.PE_SHORTLAG_BIAS)*float64(int64(1)<<(13))+0.5)))
		CCmax_new_b = CCmax_new - inlines.Silk_RSHIFT(inlines.Silk_SMULBB(nb_subfr*(int(float64(SilkConstants.PE_SHORTLAG_BIAS)*float64(int64(1)<<(13))+0.5)), lag_log2_Q7), 7)
		/* Q13 */

		/* Bias towards previous lag */
		inlines.OpusAssert(nb_subfr*int(float64(SilkConstants.PE_PREVLAG_BIAS)*float64(int64(1)<<(13))+0.5) == inlines.Silk_SAT16(nb_subfr*int(float64(SilkConstants.PE_PREVLAG_BIAS)*float64(int64(1)<<(13))+0.5)))
		if prevLag > 0 {
			delta_lag_log2_sqr_Q7 = lag_log2_Q7 - prevLag_log2_Q7
			inlines.OpusAssert(delta_lag_log2_sqr_Q7 == inlines.Silk_SAT16(delta_lag_log2_sqr_Q7))
			delta_lag_log2_sqr_Q7 = inlines.Silk_RSHIFT(inlines.Silk_SMULBB(delta_lag_log2_sqr_Q7, delta_lag_log2_sqr_Q7), 7)
			prev_lag_bias_Q13 = inlines.Silk_RSHIFT(inlines.Silk_SMULBB(nb_subfr*(int(float64(SilkConstants.PE_PREVLAG_BIAS)*float64(int64(1)<<(13))+0.5)), LTPCorr_Q15.Val), 15)
			/* Q13 */
			prev_lag_bias_Q13 = inlines.Silk_DIV32(inlines.Silk_MUL(prev_lag_bias_Q13, delta_lag_log2_sqr_Q7), delta_lag_log2_sqr_Q7+int(math.Trunc(0.5*float64(int64(1)<<(7))+0.5)))
			CCmax_new_b -= prev_lag_bias_Q13
			/* Q13 */
		}

		if CCmax_new_b > CCmax_b &&
			CCmax_new > inlines.Silk_SMULBB(nb_subfr, search_thres2_Q13) &&
			silk_CB_lags_stage2[0][CBimax_new] <= MIN_LAG_8KHZ /* Lag must be in range                             */ {
			CCmax_b = CCmax_new_b
			CCmax = CCmax_new
			lag = d
			CBimax = CBimax_new
		}
	}

	if lag == -1 {
		/* No suitable candidate found */
		arrayUtil.MemSetLen(pitch_out, 0, nb_subfr)
		LTPCorr_Q15.Val = 0
		lagIndex.Val = 0
		contourIndex.Val = 0

		return 1
	}

	/* Output normalized correlation */
	LTPCorr_Q15.Val = inlines.Silk_LSHIFT(inlines.Silk_DIV32_16(CCmax, nb_subfr), 2)
	inlines.OpusAssert(LTPCorr_Q15.Val >= 0)

	if Fs_kHz > 8 {
		var scratch_mem []int16
		/**
		 * ************************************************************************
		 */
		/* Scale input signal down to avoid correlations measures from overflowing */
		/**
		 * ************************************************************************
		 */
		/* find scaling as max scaling for each subframe */
		boxed_energy.Val = 0
		boxed_shift.Val = 0
		silk_sum_sqr_shift4(&boxed_energy, &boxed_shift, frame, frame_length)
		energy = boxed_energy.Val
		shift = boxed_shift.Val

		if shift > 0 {
			scratch_mem = make([]int16, frame_length)
			/* Move signal to scratch mem because the input signal should be unchanged */
			shift = inlines.Silk_RSHIFT(shift, 1)
			for i = 0; i < frame_length; i++ {
				scratch_mem[i] = inlines.Silk_RSHIFT16(frame[i], shift)
			}
			input_frame_ptr = scratch_mem
		} else {
			input_frame_ptr = frame
		}

		/* Search in original signal */
		CBimax_old = CBimax
		/* Compensate for decimation */
		inlines.OpusAssert(lag == inlines.Silk_SAT16(lag))
		if Fs_kHz == 12 {
			lag = inlines.Silk_RSHIFT(inlines.Silk_SMULBB(lag, 3), 1)
		} else if Fs_kHz == 16 {
			lag = inlines.Silk_LSHIFT(lag, 1)
		} else {
			lag = inlines.Silk_SMULBB(lag, 3)
		}

		lag = inlines.Silk_LIMIT_int(lag, min_lag, max_lag)
		start_lag = inlines.Silk_max_int(lag-2, min_lag)
		end_lag = inlines.Silk_min_int(lag+2, max_lag)
		lag_new = lag
		/* to avoid undefined lag */
		CBimax = 0
		/* to avoid undefined lag */

		CCmax = math.MinInt32
		/* pitch lags according to second stage */
		for k = 0; k < nb_subfr; k++ {
			pitch_out[k] = lag + 2*int(silk_CB_lags_stage2[k][CBimax_old])
		}

		/* Set up codebook parameters according to complexity setting and frame length */
		if nb_subfr == SilkConstants.PE_MAX_NB_SUBFR {
			nb_cbk_search = int(silk_nb_cbk_searchs_stage3[complexity])
			Lag_CB_ptr = silk_CB_lags_stage3
		} else {
			nb_cbk_search = SilkConstants.PE_NB_CBKS_STAGE3_10MS
			Lag_CB_ptr = silk_CB_lags_stage3_10_ms
		}

		/* Calculate the correlations and energies needed in stage 3 */
		energies_st3 := make([]*comm.Silk_pe_stage3_vals, nb_subfr*nb_cbk_search)
		cross_corr_st3 := make([]*comm.Silk_pe_stage3_vals, nb_subfr*nb_cbk_search)
		for c := 0; c < nb_subfr*nb_cbk_search; c++ {
			energies_st3[c] = &comm.Silk_pe_stage3_vals{} // fixme: these can be replaced with a linearized array probably, or at least a struct
			cross_corr_st3[c] = &comm.Silk_pe_stage3_vals{}
		}
		silk_P_Ana_calc_corr_st3(cross_corr_st3, input_frame_ptr, start_lag, sf_length, nb_subfr, complexity)
		silk_P_Ana_calc_energy_st3(energies_st3, input_frame_ptr, start_lag, sf_length, nb_subfr, complexity)

		lag_counter = 0
		inlines.OpusAssert(lag == inlines.Silk_SAT16(lag))
		contour_bias_Q15 = inlines.Silk_DIV32_16((int(float64(SilkConstants.PE_FLATCONTOUR_BIAS)*float64(int64(1)<<(15)) + 0.5)), lag)

		target = input_frame_ptr
		target_ptr = SilkConstants.PE_LTP_MEM_LENGTH_MS * Fs_kHz
		energy_target = inlines.Silk_ADD32(inlines.Silk_inner_prod_self(target, target_ptr, nb_subfr*sf_length), 1)
		for d = start_lag; d <= end_lag; d++ {
			for j = 0; j < nb_cbk_search; j++ {
				cross_corr = 0
				energy = energy_target
				for k = 0; k < nb_subfr; k++ {
					cross_corr = inlines.Silk_ADD32(cross_corr,
						inlines.MatrixGetVals(cross_corr_st3, k, j,
							nb_cbk_search).Values[lag_counter])
					energy = inlines.Silk_ADD32(energy,
						inlines.MatrixGetVals(energies_st3, k, j,
							nb_cbk_search).Values[lag_counter])
					inlines.OpusAssert(energy >= 0)
				}
				if cross_corr > 0 {
					CCmax_new = inlines.Silk_DIV32_varQ(cross_corr, energy, 13+1)
					/* Q13 */
					/* Reduce depending on flatness of contour */
					diff = math.MaxInt16 - inlines.Silk_MUL(contour_bias_Q15, j)
					/* Q15 */
					inlines.OpusAssert(diff == inlines.Silk_SAT16(diff))
					CCmax_new = inlines.Silk_SMULWB(CCmax_new, diff)
					/* Q14 */
				} else {
					CCmax_new = 0
				}
				if CCmax_new > CCmax && (d+int(silk_CB_lags_stage3[0][j])) <= max_lag {
					CCmax = CCmax_new
					lag_new = d
					CBimax = j
				}
			}
			lag_counter++
		}

		for k = 0; k < nb_subfr; k++ {
			pitch_out[k] = lag_new + int(Lag_CB_ptr[k][CBimax])
			pitch_out[k] = inlines.Silk_LIMIT(pitch_out[k], min_lag, SilkConstants.PE_MAX_LAG_MS*Fs_kHz)
		}
		lagIndex.Val = int16(lag_new - min_lag)
		contourIndex.Val = int8(CBimax)

	} else {
		/* Fs_kHz == 8 */
		/* Save Lags */
		for k = 0; k < nb_subfr; k++ {
			pitch_out[k] = lag + int(Lag_CB_ptr[k][CBimax])
			pitch_out[k] = inlines.Silk_LIMIT(pitch_out[k], MIN_LAG_8KHZ, SilkConstants.PE_MAX_LAG_MS*8)
		}
		lagIndex.Val = int16(lag - MIN_LAG_8KHZ)
		contourIndex.Val = int8(CBimax)
	}
	inlines.OpusAssert(lagIndex.Val >= 0)
	/* return as voiced */

	return 0
}

func silk_P_Ana_calc_corr_st3(cross_corr_st3 []*comm.Silk_pe_stage3_vals, frame []int16, start_lag int, sf_length int, nb_subfr int, complexity int) {
	var target_ptr int
	var i, j, k, lag_counter, lag_low, lag_high int
	var nb_cbk_search, delta, idx int
	scratch_mem := make([]int, SCRATCH_SIZE)
	xcorr32 := make([]int, SCRATCH_SIZE)
	var Lag_range_ptr, Lag_CB_ptr [][]int8

	if nb_subfr == PE_MAX_NB_SUBFR {
		Lag_range_ptr = silk_Lag_range_stage3[complexity]
		Lag_CB_ptr = silk_CB_lags_stage3
		nb_cbk_search = int(silk_nb_cbk_searchs_stage3[complexity])
	} else {
		Lag_range_ptr = silk_Lag_range_stage3_10_ms
		Lag_CB_ptr = silk_CB_lags_stage3_10_ms
		nb_cbk_search = PE_NB_CBKS_STAGE3_10MS
	}

	target_ptr = inlines.Silk_LSHIFT(sf_length, 2)
	for k = 0; k < nb_subfr; k++ {
		lag_counter = 0
		lag_low = int(Lag_range_ptr[k][0])
		lag_high = int(Lag_range_ptr[k][1])
		comm.Pitch_xcorr1(frame, target_ptr, frame, target_ptr-start_lag-lag_high, xcorr32, sf_length, lag_high-lag_low+1)
		for j = lag_low; j <= lag_high; j++ {
			scratch_mem[lag_counter] = xcorr32[lag_high-j]
			lag_counter++
		}

		delta = int(Lag_range_ptr[k][0])
		for i = 0; i < nb_cbk_search; i++ {
			idx = int(Lag_CB_ptr[k][i]) - delta
			for j = 0; j < PE_NB_STAGE3_LAGS; j++ {
				cross_corr_st3[k*nb_cbk_search+i].Values[j] = scratch_mem[idx+j]
			}
		}
		target_ptr += sf_length
	}
}

func silk_P_Ana_calc_energy_st3(energies_st3 []*comm.Silk_pe_stage3_vals, frame []int16, start_lag int, sf_length int, nb_subfr int, complexity int) {
	var target_ptr, basis_ptr int
	var energy int
	var k, i, j, lag_counter int
	var nb_cbk_search, delta, idx, lag_diff int
	scratch_mem := make([]int, SCRATCH_SIZE)
	var Lag_range_ptr, Lag_CB_ptr [][]int8

	if nb_subfr == PE_MAX_NB_SUBFR {
		Lag_range_ptr = silk_Lag_range_stage3[complexity]
		Lag_CB_ptr = silk_CB_lags_stage3
		nb_cbk_search = int(silk_nb_cbk_searchs_stage3[complexity])
	} else {
		Lag_range_ptr = silk_Lag_range_stage3_10_ms
		Lag_CB_ptr = silk_CB_lags_stage3_10_ms
		nb_cbk_search = PE_NB_CBKS_STAGE3_10MS
	}

	target_ptr = inlines.Silk_LSHIFT(sf_length, 2)
	for k = 0; k < nb_subfr; k++ {
		lag_counter = 0
		basis_ptr = target_ptr - (start_lag + int(Lag_range_ptr[k][0]))
		energy = inlines.Silk_inner_prod_self(frame, basis_ptr, sf_length)
		scratch_mem[lag_counter] = energy
		lag_counter++

		lag_diff = int(Lag_range_ptr[k][1]) - int(Lag_range_ptr[k][0]) + 1
		for i = 1; i < lag_diff; i++ {
			energy -= int(frame[basis_ptr+sf_length-i]) * int(frame[basis_ptr+sf_length-i])
			energy = inlines.Silk_ADD_SAT32(energy, int(frame[basis_ptr-i])*int(frame[basis_ptr-i]))
			scratch_mem[lag_counter] = energy
			lag_counter++
		}

		delta = int(Lag_range_ptr[k][0])
		for i = 0; i < nb_cbk_search; i++ {
			idx = int(Lag_CB_ptr[k][i]) - delta
			for j = 0; j < PE_NB_STAGE3_LAGS; j++ {
				energies_st3[k*nb_cbk_search+i].Values[j] = scratch_mem[idx+j]
			}
		}
		target_ptr += sf_length
	}
}
