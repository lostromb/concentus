package opus

var freq_table_Q16 = [27]int16{
	12111, 9804, 8235, 7100, 6239, 5565, 5022, 4575, 4202,
	3885, 3612, 3375, 3167, 2984, 2820, 2674, 2542, 2422,
	2313, 2214, 2123, 2038, 1961, 1889, 1822, 1760, 1702,
}

func silk_apply_sine_window(px_win []int16, px_win_ptr int, px []int16, px_ptr int, win_type int, length int) {
	var k, f_Q16, c_Q16 int
	var S0_Q16, S1_Q16 int

	OpusAssert(win_type == 1 || win_type == 2)
	OpusAssert(length >= 16 && length <= 120)
	OpusAssert((length & 3) == 0)

	k = (length >> 2) - 4
	OpusAssert(k >= 0 && k <= 26)
	f_Q16 = int(freq_table_Q16[k])

	c_Q16 = silk_SMULWB(f_Q16, -f_Q16)
	OpusAssert(c_Q16 >= -32768)

	if win_type == 1 {
		S0_Q16 = 0
		S1_Q16 = f_Q16 + silk_RSHIFT(length, 3)
	} else {
		S0_Q16 = 1 << 16
		S1_Q16 = (1 << 16) + silk_RSHIFT(c_Q16, 1) + silk_RSHIFT(length, 4)
	}

	for k = 0; k < length; k += 4 {
		px_win[px_win_ptr+k] = int16(silk_SMULWB(silk_RSHIFT(S0_Q16+S1_Q16, 1), int(px[px_ptr+k])))
		px_win[px_win_ptr+k+1] = int16(silk_SMULWB(S1_Q16, int(px[px_ptr+k+1])))
		S0_Q16 = silk_SMULWB(S1_Q16, c_Q16) + silk_LSHIFT(S1_Q16, 1) - S0_Q16 + 1
		S0_Q16 = silk_min(S0_Q16, 1<<16)

		px_win[px_win_ptr+k+2] = int16(silk_SMULWB(silk_RSHIFT(S0_Q16+S1_Q16, 1), int(px[px_ptr+k+2])))
		px_win[px_win_ptr+k+3] = int16(silk_SMULWB(S0_Q16, int(px[px_ptr+k+3])))
		S1_Q16 = silk_SMULWB(S0_Q16, c_Q16) + silk_LSHIFT(S0_Q16, 1) - S1_Q16
		S1_Q16 = silk_min(S1_Q16, 1<<16)
	}
}
