package opus

func silk_decode_pulses(
	psRangeDec *EntropyCoder,
	pulses []int16,
	signalType int,
	quantOffsetType int,
	frame_length int) {

	var i, j, k, iter, abs_q, nLS, RateLevelIndex int
	var sum_pulses [MAX_NB_SHELL_BLOCKS]int
	var nLshifts [MAX_NB_SHELL_BLOCKS]int
	var pulses_ptr int

	RateLevelIndex = psRangeDec.dec_icdf(SilkTables.Silk_rate_levels_iCDF[signalType>>1], 8)

	OpusAssert(1<<SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH == SilkConstants.SHELL_CODEC_FRAME_LENGTH)
	iter = frame_length >> SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH
	if iter*SilkConstants.SHELL_CODEC_FRAME_LENGTH < frame_length {
		OpusAssert(frame_length == 120)
		iter++
	}

	for i = 0; i < iter; i++ {
		nLshifts[i] = 0
		sum_pulses[i] = psRangeDec.dec_icdf(SilkTables.Silk_pulses_per_block_iCDF[RateLevelIndex], 8)

		for sum_pulses[i] == SilkConstants.SILK_MAX_PULSES+1 {
			nLshifts[i]++
			table := SilkTables.Silk_pulses_per_block_iCDF[SilkConstants.N_RATE_LEVELS-1]
			if nLshifts[i] == 10 {
				sum_pulses[i] = psRangeDec.dec_icdf_offset(table, 1, 8)

			} else {
				sum_pulses[i] = psRangeDec.dec_icdf_offset(table, 0, 8)
			}
		}
	}

	for i = 0; i < iter; i++ {
		if sum_pulses[i] > 0 {
			silk_shell_decoder(pulses, i*SilkConstants.SHELL_CODEC_FRAME_LENGTH, psRangeDec, sum_pulses[i])
		} else {
			start := i * SilkConstants.SHELL_CODEC_FRAME_LENGTH
			end := start + SilkConstants.SHELL_CODEC_FRAME_LENGTH
			for idx := start; idx < end; idx++ {
				pulses[idx] = 0
			}
		}
	}

	for i = 0; i < iter; i++ {
		if nLshifts[i] > 0 {
			nLS = nLshifts[i]
			pulses_ptr = i * SilkConstants.SHELL_CODEC_FRAME_LENGTH
			for k = 0; k < SilkConstants.SHELL_CODEC_FRAME_LENGTH; k++ {
				abs_q = int(pulses[pulses_ptr+k])
				for j = 0; j < nLS; j++ {
					abs_q <<= 1
					abs_q += psRangeDec.dec_icdf(SilkTables.Silk_lsb_iCDF, 8)
				}
				pulses[pulses_ptr+k] = int16(abs_q)
			}
			sum_pulses[i] |= nLS << 5
		}
	}
	silk_decode_signs(psRangeDec, pulses, frame_length, signalType, quantOffsetType, sum_pulses[:])
}
