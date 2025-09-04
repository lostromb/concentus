package opus

func silk_solve_LDL(A []int, A_ptr int, M int, b []int, x_Q16 []int) {
	OpusAssert(M <= SilkConstants.MAX_MATRIX_SIZE)
	L_Q16 := make([]int, M*M)
	Y := make([]int, SilkConstants.MAX_MATRIX_SIZE)
	inv_D := make([]int, SilkConstants.MAX_MATRIX_SIZE*2)

	silk_LDL_factorize(A, A_ptr, M, L_Q16, inv_D)
	silk_LS_SolveFirst(L_Q16, M, b, Y)
	silk_LS_divide_Q16(Y, inv_D, M)
	silk_LS_SolveLast(L_Q16, M, Y, x_Q16)
}

func silk_LDL_factorize(A []int, A_ptr int, M int, L_Q16 []int, inv_D []int) {
	var i, j, k, status, loop_count int
	var scratch1 []int
	var scratch1_ptr int
	var scratch2 []int
	var scratch2_ptr int
	var diag_min_value, tmp_32, err int
	v_Q0 := make([]int, M)
	D_Q0 := make([]int, M)
	var one_div_diag_Q36, one_div_diag_Q40, one_div_diag_Q48 int

	OpusAssert(M <= SilkConstants.MAX_MATRIX_SIZE)

	status = 1
	diag_min_value = silk_max_32(
		silk_SMMUL(
			silk_ADD_SAT32(A[A_ptr], A[A_ptr+silk_SMULBB(M, M)-1]),
			int(float64(TuningParameters.FIND_LTP_COND_FAC)*float64(int(1<<31))+0.5),
		),
		1<<9,
	)
	for loop_count = 0; loop_count < M && status == 1; loop_count++ {
		status = 0
		for j = 0; j < M; j++ {
			scratch1 = L_Q16
			scratch1_ptr = MatrixGetPointer(j, 0, M)
			tmp_32 = 0
			for i = 0; i < j; i++ {
				v_Q0[i] = silk_SMULWW(D_Q0[i], scratch1[scratch1_ptr+i])
				tmp_32 = silk_SMLAWW(tmp_32, v_Q0[i], scratch1[scratch1_ptr+i])
			}
			tmp_32 = silk_SUB32(MatrixGetPtr(A, A_ptr, j, j, M), tmp_32)

			if tmp_32 < diag_min_value {
				tmp_32 = silk_SUB32(silk_SMULBB(loop_count+1, diag_min_value), tmp_32)
				for i = 0; i < M; i++ {
					MatrixSet(A, A_ptr, i, i, M, silk_ADD32(MatrixGetPtr(A, A_ptr, i, i, M), tmp_32))
				}
				status = 1
				break
			}
			D_Q0[j] = tmp_32

			one_div_diag_Q36 = silk_INVERSE32_varQ(tmp_32, 36)
			one_div_diag_Q40 = silk_LSHIFT(one_div_diag_Q36, 4)
			err = silk_SUB32(int(1<<24), silk_SMULWW(tmp_32, one_div_diag_Q40))
			one_div_diag_Q48 = silk_SMULWW(err, one_div_diag_Q40)

			inv_D[j*2+0] = one_div_diag_Q36
			inv_D[j*2+1] = one_div_diag_Q48
			MatrixSet5(L_Q16, j, j, M, 65536)
			scratch1 = A
			scratch1_ptr = MatrixGetPointer(j, 0, M) + A_ptr
			scratch2 = L_Q16
			scratch2_ptr = MatrixGetPointer(j+1, 0, M)
			for i = j + 1; i < M; i++ {
				tmp_32 = 0
				for k = 0; k < j; k++ {
					tmp_32 = silk_SMLAWW(tmp_32, v_Q0[k], scratch2[scratch2_ptr+k])
				}
				tmp_32 = silk_SUB32(scratch1[scratch1_ptr+i], tmp_32)
				MatrixSet5(L_Q16, i, j, M, silk_ADD32(
					silk_SMMUL(tmp_32, one_div_diag_Q48),
					silk_RSHIFT(silk_SMULWW(tmp_32, one_div_diag_Q36), 4),
				))
				scratch2_ptr += M
			}
		}
	}
	OpusAssert(status == 0)
}

func silk_LS_divide_Q16(T []int, inv_D []int, M int) {
	var i int
	var tmp_32, one_div_diag_Q36, one_div_diag_Q48 int
	for i = 0; i < M; i++ {
		one_div_diag_Q36 = inv_D[i*2+0]
		one_div_diag_Q48 = inv_D[i*2+1]
		tmp_32 = T[i]
		T[i] = silk_ADD32(
			silk_SMMUL(tmp_32, one_div_diag_Q48),
			silk_RSHIFT(silk_SMULWW(tmp_32, one_div_diag_Q36), 4),
		)
	}
}

func silk_LS_SolveFirst(L_Q16 []int, M int, b []int, x_Q16 []int) {
	var i, j, ptr32 int
	var tmp_32 int
	for i = 0; i < M; i++ {
		ptr32 = MatrixGetPointer(i, 0, M)
		tmp_32 = 0
		for j = 0; j < i; j++ {
			tmp_32 = silk_SMLAWW(tmp_32, L_Q16[ptr32+j], x_Q16[j])
		}
		x_Q16[i] = silk_SUB32(b[i], tmp_32)
	}
}

func silk_LS_SolveLast(L_Q16 []int, M int, b []int, x_Q16 []int) {
	var i, j, ptr32 int
	var tmp_32 int
	for i = M - 1; i >= 0; i-- {
		ptr32 = MatrixGetPointer(0, i, M)
		tmp_32 = 0
		for j = M - 1; j > i; j-- {
			tmp_32 = silk_SMLAWW(tmp_32, L_Q16[ptr32+silk_SMULBB(j, M)], x_Q16[j])
		}
		x_Q16[i] = silk_SUB32(b[i], tmp_32)
	}
}
