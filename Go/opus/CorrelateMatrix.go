package opus

type correlateMatrix struct{}

var CorrelateMatrix correlateMatrix

func (c *correlateMatrix) silk_corrVector(
	x []int16,
	x_ptr int,
	t []int16,
	t_ptr int,
	L int,
	order int,
	Xt []int,
	rshifts int) {

	var lag, i int
	var ptr1, ptr2 int
	var inner_prod int

	ptr1 = x_ptr + order - 1
	ptr2 = t_ptr

	if rshifts > 0 {
		for lag = 0; lag < order; lag++ {
			inner_prod = 0
			for i = 0; i < L; i++ {
				inner_prod += silk_RSHIFT32(silk_SMULBB(int(x[ptr1+i]), int(t[ptr2+i])), rshifts)
			}
			Xt[lag] = inner_prod
			ptr1--
		}
	} else {
		OpusAssert(rshifts == 0)
		for lag = 0; lag < order; lag++ {
			Xt[lag] = silk_inner_prod(x, ptr1, t, ptr2, L)
			ptr1--
		}
	}
}

func (c *correlateMatrix) silk_corrMatrix(
	x []int16,
	x_ptr int,
	L int,
	order int,
	head_room int,
	XX []int,
	XX_ptr int,
	rshifts *BoxedValueInt) {

	var i, j, lag, head_room_rshifts int
	var energy, rshifts_local int

	boxed_energy := BoxedValueInt{Val: 0}
	boxed_rshifts_local := BoxedValueInt{Val: 0}
	silk_sum_sqr_shift5(&boxed_energy, &boxed_rshifts_local, x, x_ptr, L+order-1)
	energy = boxed_energy.Val
	rshifts_local = boxed_rshifts_local.Val

	head_room_rshifts = silk_max(head_room-silk_CLZ32(energy), 0)

	energy = silk_RSHIFT32(energy, head_room_rshifts)
	rshifts_local += head_room_rshifts

	for i = x_ptr; i < x_ptr+order-1; i++ {
		energy -= silk_RSHIFT32(silk_SMULBB(int(x[i]), int(x[i])), rshifts_local)
	}
	if rshifts_local < rshifts.Val {
		energy = silk_RSHIFT32(energy, rshifts.Val-rshifts_local)
		rshifts_local = rshifts.Val
	}

	MatrixSet(XX, XX_ptr, 0, 0, order, energy)
	ptr1 := x_ptr + order - 1
	for j = 1; j < order; j++ {
		energy = silk_SUB32(energy, silk_RSHIFT32(silk_SMULBB(int(x[ptr1+L-j]), int(x[ptr1+L-j])), rshifts_local))
		energy = silk_ADD32(energy, silk_RSHIFT32(silk_SMULBB(int(x[ptr1-j]), int(x[ptr1-j])), rshifts_local))
		MatrixSet(XX, XX_ptr, j, j, order, energy)
	}

	ptr2 := x_ptr + order - 2
	if rshifts_local > 0 {
		for lag = 1; lag < order; lag++ {
			energy = 0
			for i = 0; i < L; i++ {
				energy += silk_RSHIFT32(silk_SMULBB(int(x[ptr1+i]), int(x[ptr2+i])), rshifts_local)
			}
			MatrixSet(XX, XX_ptr, lag, 0, order, energy)
			MatrixSet(XX, XX_ptr, 0, lag, order, energy)
			for j = 1; j < (order - lag); j++ {
				energy = silk_SUB32(energy, silk_RSHIFT32(silk_SMULBB(int(x[ptr1+L-j]), int(x[ptr2+L-j])), rshifts_local))
				energy = silk_ADD32(energy, silk_RSHIFT32(silk_SMULBB(int(x[ptr1-j]), int(x[ptr2-j])), rshifts_local))
				MatrixSet(XX, XX_ptr, lag+j, j, order, energy)
				MatrixSet(XX, XX_ptr, j, lag+j, order, energy)
			}
			ptr2--
		}
	} else {
		for lag = 1; lag < order; lag++ {
			energy = silk_inner_prod(x, ptr1, x, ptr2, L)
			MatrixSet(XX, XX_ptr, lag, 0, order, energy)
			MatrixSet(XX, XX_ptr, 0, lag, order, energy)
			for j = 1; j < (order - lag); j++ {
				energy = silk_SUB32(energy, silk_SMULBB(int(x[ptr1+L-j]), int(x[ptr2+L-j])))
				energy = silk_SMLABB(energy, int(x[ptr1-j]), int(x[ptr2-j]))
				MatrixSet(XX, XX_ptr, lag+j, j, order, energy)
				MatrixSet(XX, XX_ptr, j, lag+j, order, energy)
			}
			ptr2--
		}
	}
	rshifts.Val = rshifts_local
}
