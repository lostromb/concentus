package opus

func silk_residual_energy(
	nrgs []int,
	nrgsQ []int,
	x []int16,
	a_Q12 [][]int16,
	gains []int,
	subfr_length int,
	nb_subfr int,
	LPC_order int,
) {
	var offset, i, j, lz1, lz2 int
	rshift := &BoxedValueInt{0}
	energy := &BoxedValueInt{0}
	var LPC_res_ptr int
	var LPC_res []int16
	var x_ptr int
	var tmp32 int

	x_ptr = 0
	offset = LPC_order + subfr_length

	/* Filter input to create the LPC residual for each frame half, and measure subframe energies */
	LPC_res = make([]int16, (SilkConstants.MAX_NB_SUBFR>>1)*offset)
	OpusAssert((nb_subfr>>1)*(SilkConstants.MAX_NB_SUBFR>>1) == nb_subfr)
	for i = 0; i < nb_subfr>>1; i++ {
		/* Calculate half frame LPC residual signal including preceding samples */
		silk_LPC_analysis_filter(LPC_res, 0, x, x_ptr, a_Q12[i], 0, (SilkConstants.MAX_NB_SUBFR>>1)*offset, LPC_order)

		/* Point to first subframe of the just calculated LPC residual signal */
		LPC_res_ptr = LPC_order
		for j = 0; j < (SilkConstants.MAX_NB_SUBFR >> 1); j++ {
			/* Measure subframe energy */
			silk_sum_sqr_shift5(energy, rshift, LPC_res, LPC_res_ptr, subfr_length)
			nrgs[i*(SilkConstants.MAX_NB_SUBFR>>1)+j] = energy.Val

			/* Set Q values for the measured energy */
			nrgsQ[i*(SilkConstants.MAX_NB_SUBFR>>1)+j] = 0 - rshift.Val

			/* Move to next subframe */
			LPC_res_ptr += offset
		}
		/* Move to next frame half */
		x_ptr += (SilkConstants.MAX_NB_SUBFR >> 1) * offset
	}

	/* Apply the squared subframe gains */
	for i = 0; i < nb_subfr; i++ {
		/* Fully upscale gains and energies */
		lz1 = silk_CLZ32(nrgs[i]) - 1
		lz2 = silk_CLZ32(gains[i]) - 1

		tmp32 = silk_LSHIFT32(gains[i], lz2)

		/* Find squared gains */
		tmp32 = silk_SMMUL(tmp32, tmp32)
		/* Q( 2 * lz2 - 32 )*/

		/* Scale energies */
		nrgs[i] = silk_SMMUL(tmp32, silk_LSHIFT32(nrgs[i], lz1))
		/* Q( nrgsQ[ i ] + lz1 + 2 * lz2 - 32 - 32 )*/
		nrgsQ[i] += lz1 + 2*lz2 - 32 - 32
	}

}

func silk_residual_energy16_covar(
	c []int16,
	c_ptr int,
	wXX []int,
	wXX_ptr int,
	wXx []int,
	wxx int,
	D int,
	cQ int,
) int {
	OpusAssert(D >= 0)
	OpusAssert(D <= 16)
	OpusAssert(cQ > 0)
	OpusAssert(cQ < 16)

	lshifts := 16 - cQ
	Qxtra := lshifts
	c_max := 0

	for i := c_ptr; i < c_ptr+D; i++ {
		abs_c := silk_abs(int(c[i]))
		if abs_c > c_max {
			c_max = abs_c
		}
	}

	Qxtra = silk_min_int(Qxtra, silk_CLZ32(c_max)-17)
	w_max := silk_max_32(wXX[wXX_ptr], wXX[wXX_ptr+(D*D)-1])
	tmp_val := silk_RSHIFT(silk_SMULWB(w_max, c_max), 4)
	Qxtra = silk_min_int(Qxtra, silk_CLZ32(D*tmp_val)-5)
	Qxtra = silk_max_int(Qxtra, 0)

	cn := make([]int, D)
	for i := 0; i < D; i++ {
		cn[i] = silk_LSHIFT(int(c[c_ptr+i]), Qxtra)
		OpusAssert(silk_abs(cn[i]) <= (32768))
	}
	lshifts -= Qxtra

	tmp := 0
	for i := 0; i < D; i++ {
		tmp = silk_SMLAWB(tmp, wXx[i], cn[i])
	}
	nrg := silk_RSHIFT(wxx, 1+lshifts) - tmp

	tmp2 := 0
	for i := 0; i < D; i++ {
		tmp = 0
		pRow := wXX_ptr + i*D
		for j := i + 1; j < D; j++ {
			tmp = silk_SMLAWB(tmp, wXX[pRow+j], cn[j])
		}
		tmp = silk_SMLAWB(tmp, silk_RSHIFT(wXX[pRow+i], 1), cn[i])
		tmp2 = silk_SMLAWB(tmp2, tmp, cn[i])
	}
	nrg = silk_ADD_LSHIFT32(nrg, tmp2, lshifts)

	if nrg < 1 {
		nrg = 1
	} else if nrg > silk_RSHIFT(2147483647, lshifts+2) {
		nrg = 1073741824
	} else {
		nrg = silk_LSHIFT(nrg, lshifts+1)
	}
	return nrg
}
