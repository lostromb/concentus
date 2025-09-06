package silk

func silk_bwexpander_32(ar []int, d int, chirp_Q16 int) {
	var i int
	var chirp_minus_one_Q16 = chirp_Q16 - 65536

	for i = 0; i < d-1; i++ {
		ar[i] = inlines.Silk_SMULWW(chirp_Q16, ar[i])
		chirp_Q16 += inlines.Silk_RSHIFT_ROUND(inlines.Silk_MUL(chirp_Q16, chirp_minus_one_Q16), 16)
	}
	ar[d-1] = inlines.Silk_SMULWW(chirp_Q16, ar[d-1])

}

func silk_bwexpander(ar []int16, d int, chirp_Q16 int) {
	var i int
	chirp_minus_one_Q16 := chirp_Q16 - 65536

	/* NB: Dont use silk_SMULWB, instead of silk_RSHIFT_ROUND( silk_MUL(), 16 ), below.  */
	/* Bias in silk_SMULWB can lead to unstable filters                                */
	for i = 0; i < d-1; i++ {
		ar[i] = int16(inlines.Silk_RSHIFT_ROUND(inlines.Silk_MUL(chirp_Q16, int(ar[i])), 16))
		chirp_Q16 += inlines.Silk_RSHIFT_ROUND(inlines.Silk_MUL(chirp_Q16, chirp_minus_one_Q16), 16)
	}
	ar[d-1] = int16(inlines.Silk_RSHIFT_ROUND(inlines.Silk_MUL(chirp_Q16, int(ar[d-1])), 16))

}
