package opus

func combine_and_check(pulses_comb []int, pulses_comb_ptr int, pulses_in []int, pulses_in_ptr int, max_pulses int, _len int) int {
	for k := 0; k < _len; k++ {
		k2p := 2*k + pulses_in_ptr
		sum := pulses_in[k2p] + pulses_in[k2p+1]
		if sum > max_pulses {
			return 1
		}
		pulses_comb[pulses_comb_ptr+k] = sum
	}
	return 0
}
func combine_and_check4(
	pulses_comb []int,
	pulses_in []int,
	max_pulses int,
	_len int) int {
	for k := 0; k < _len; k++ {
		sum := pulses_in[2*k] + pulses_in[2*k+1]
		if sum > max_pulses {
			return 1
		}
		pulses_comb[k] = sum
	}
	return 0
}

func silk_encode_pulses(
	psRangeEnc *EntropyCoder,
	signalType int,
	quantOffsetType int,
	pulses []int8,
	frame_length int) {

	var i, k, j, iter, bit, nLS, scale_down, RateLevelIndex int
	var abs_q, minSumBits_Q5, sumBits_Q5 int
	var abs_pulses []int
	var sum_pulses []int
	var nRshifts []int
	pulses_comb := make([]int, 8)
	var abs_pulses_ptr int
	var pulses_ptr int
	var nBits_ptr []int16

	for idx := range pulses_comb {
		pulses_comb[idx] = 0
	}

	OpusAssert(1<<SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH == SilkConstants.SHELL_CODEC_FRAME_LENGTH)
	iter = int(silk_RSHIFT(int(frame_length), int(SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH)))
	if iter*SilkConstants.SHELL_CODEC_FRAME_LENGTH < frame_length {
		OpusAssert(frame_length == 12*10)
		iter++
		for idx := frame_length; idx < frame_length+SilkConstants.SHELL_CODEC_FRAME_LENGTH; idx++ {
			if idx < len(pulses) {
				pulses[idx] = 0
			}
		}
	}

	abs_pulses = make([]int, iter*SilkConstants.SHELL_CODEC_FRAME_LENGTH)
	OpusAssert((SilkConstants.SHELL_CODEC_FRAME_LENGTH & 3) == 0)
	// unrolled loop
	for i = 0; i < iter*SilkConstants.SHELL_CODEC_FRAME_LENGTH; i += 4 {
		abs_pulses[i+0] = silk_abs(int(pulses[i+0]))
		abs_pulses[i+1] = silk_abs(int(pulses[i+1]))
		abs_pulses[i+2] = silk_abs(int(pulses[i+2]))
		abs_pulses[i+3] = silk_abs(int(pulses[i+3]))
	}

	sum_pulses = make([]int, iter)
	nRshifts = make([]int, iter)
	abs_pulses_ptr = 0
	for i = 0; i < iter; i++ {
		nRshifts[i] = 0

		for {
			scale_down = combine_and_check(pulses_comb, 0, abs_pulses, abs_pulses_ptr, int(silk_max_pulses_table[0]), 8)
			/* 2+2 . 4 */
			scale_down += combine_and_check4(pulses_comb, pulses_comb, int(silk_max_pulses_table[1]), 4)
			/* 4+4 . 8 */
			scale_down += combine_and_check4(pulses_comb, pulses_comb, int(silk_max_pulses_table[2]), 2)
			/* 8+8 . 16 */
			scale_down += combine_and_check(sum_pulses, i, pulses_comb, 0, int(silk_max_pulses_table[3]), 1)

			if scale_down != 0 {
				nRshifts[i]++
				for k = abs_pulses_ptr; k < abs_pulses_ptr+SilkConstants.SHELL_CODEC_FRAME_LENGTH; k++ {
					abs_pulses[k] = int(silk_RSHIFT((abs_pulses[k]), 1))
				}
			} else {
				break
			}
		}
		abs_pulses_ptr += SilkConstants.SHELL_CODEC_FRAME_LENGTH
	}

	minSumBits_Q5 = int(^uint(0) >> 1)
	for k = 0; k < SilkConstants.N_RATE_LEVELS-1; k++ {
		nBits_ptr = SilkTables.Silk_pulses_per_block_BITS_Q5[k]
		sumBits_Q5 = int(SilkTables.Silk_rate_levels_BITS_Q5[signalType>>1][k])
		for i = 0; i < iter; i++ {
			if nRshifts[i] > 0 {
				sumBits_Q5 += int(nBits_ptr[SilkConstants.SILK_MAX_PULSES+1])
			} else {
				sumBits_Q5 += int(nBits_ptr[sum_pulses[i]])
			}
		}
		if sumBits_Q5 < minSumBits_Q5 {
			minSumBits_Q5 = sumBits_Q5
			RateLevelIndex = k
		}
	}

	psRangeEnc.enc_icdf(RateLevelIndex, SilkTables.Silk_rate_levels_iCDF[signalType>>1], 8)

	for i = 0; i < iter; i++ {
		if nRshifts[i] == 0 {
			psRangeEnc.enc_icdf(sum_pulses[i], SilkTables.Silk_pulses_per_block_iCDF[RateLevelIndex], 8)
		} else {
			psRangeEnc.enc_icdf(SilkConstants.SILK_MAX_PULSES+1, SilkTables.Silk_pulses_per_block_iCDF[RateLevelIndex], 8)
			for k = 0; k < nRshifts[i]-1; k++ {
				psRangeEnc.enc_icdf(SilkConstants.SILK_MAX_PULSES+1, SilkTables.Silk_pulses_per_block_iCDF[SilkConstants.N_RATE_LEVELS-1], 8)
			}
			psRangeEnc.enc_icdf(sum_pulses[i], SilkTables.Silk_pulses_per_block_iCDF[SilkConstants.N_RATE_LEVELS-1], 8)
		}
	}

	for i = 0; i < iter; i++ {
		if sum_pulses[i] > 0 {
			silk_shell_encoder(psRangeEnc, abs_pulses, i*SilkConstants.SHELL_CODEC_FRAME_LENGTH)
		}
	}

	for i = 0; i < iter; i++ {
		if nRshifts[i] > 0 {
			pulses_ptr = i * SilkConstants.SHELL_CODEC_FRAME_LENGTH
			nLS = nRshifts[i] - 1
			for k = 0; k < SilkConstants.SHELL_CODEC_FRAME_LENGTH; k++ {
				val := int(pulses[pulses_ptr+k])
				if val < 0 {
					abs_q = -val
				} else {
					abs_q = val
				}
				for j = nLS; j > 0; j-- {
					bit = (abs_q >> j) & 1
					psRangeEnc.enc_icdf(bit, SilkTables.Silk_lsb_iCDF, 8)
				}
				bit = abs_q & 1
				psRangeEnc.enc_icdf(bit, SilkTables.Silk_lsb_iCDF, 8)
			}
		}
	}

	silk_encode_signs(psRangeEnc, pulses, frame_length, signalType, quantOffsetType, sum_pulses)
}
