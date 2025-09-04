package opus

func celt_fir(x []int16, x_ptr int, num []int16, y []int16, y_ptr int, N int, ord int, mem []int16) {
	var i, j int
	rnum := make([]int16, ord)
	local_x := make([]int16, N+ord)

	for i = 0; i < ord; i++ {
		rnum[i] = num[ord-i-1]
	}

	for i = 0; i < ord; i++ {
		local_x[i] = mem[ord-i-1]
	}

	for i = 0; i < N; i++ {
		local_x[i+ord] = x[x_ptr+i]
	}

	for i = 0; i < ord; i++ {
		mem[i] = x[x_ptr+N-i-1]
	}

	sum0 := &BoxedValueInt{0}
	sum1 := &BoxedValueInt{0}
	sum2 := &BoxedValueInt{0}
	sum3 := &BoxedValueInt{0}

	for i = 0; i < N-3; i += 4 {
		sum0.Val = 0
		sum1.Val = 0
		sum2.Val = 0
		sum3.Val = 0
		xcorr_kernel(rnum, 0, local_x, i, sum0, sum1, sum2, sum3, ord)
		y[y_ptr+i] = SATURATE16((ADD32(EXTEND32(x[x_ptr+i]), PSHR32(sum0.Val, CeltConstants.SIG_SHIFT))))
		y[y_ptr+i+1] = SATURATE16((ADD32(EXTEND32(x[x_ptr+i+1]), PSHR32(sum1.Val, CeltConstants.SIG_SHIFT))))
		y[y_ptr+i+2] = SATURATE16((ADD32(EXTEND32(x[x_ptr+i+2]), PSHR32(sum2.Val, CeltConstants.SIG_SHIFT))))
		y[y_ptr+i+3] = SATURATE16((ADD32(EXTEND32(x[x_ptr+i+3]), PSHR32(sum3.Val, CeltConstants.SIG_SHIFT))))
	}

	for ; i < N; i++ {
		var sum = 0

		for j = 0; j < ord; j++ {
			sum = MAC16_16Int(sum, rnum[j], local_x[i+j])
		}

		y[y_ptr+i] = SATURATE16((ADD32(EXTEND32(x[x_ptr+i]), PSHR32(sum, CeltConstants.SIG_SHIFT))))
	}
}

func celt_fir_int(x []int, x_ptr int, num []int, num_ptr int, y []int, y_ptr int, N int, ord int, mem []int) {
	rnum := make([]int, ord)
	local_x := make([]int, N+ord)

	for i := 0; i < ord; i++ {
		rnum[i] = num[num_ptr+ord-i-1]
	}

	for i := 0; i < ord; i++ {
		local_x[i] = mem[ord-i-1]
	}

	for i := 0; i < N; i++ {
		local_x[i+ord] = x[x_ptr+i]
	}

	for i := 0; i < ord; i++ {
		mem[i] = x[x_ptr+N-i-1]
	}

	sum0 := &BoxedValueInt{Val: 0}
	sum1 := &BoxedValueInt{Val: 0}
	sum2 := &BoxedValueInt{Val: 0}
	sum3 := &BoxedValueInt{Val: 0}

	i := 0
	for ; i < N-3; i += 4 {
		sum0.Val = 0
		sum1.Val = 0
		sum2.Val = 0
		sum3.Val = 0
		xcorr_kernel_int(rnum, local_x, i, sum0, sum1, sum2, sum3, ord)
		y[y_ptr+i] = int(SATURATE16(ADD32(EXTEND32(int16(x[x_ptr+i])), PSHR32(sum0.Val, CeltConstants.SIG_SHIFT))))
		y[y_ptr+i+1] = int(SATURATE16(ADD32(EXTEND32(int16(x[x_ptr+i+1])), PSHR32(sum1.Val, CeltConstants.SIG_SHIFT))))
		y[y_ptr+i+2] = int(SATURATE16(ADD32(EXTEND32(int16(x[x_ptr+i+2])), PSHR32(sum2.Val, CeltConstants.SIG_SHIFT))))
		y[y_ptr+i+3] = int(SATURATE16(ADD32(EXTEND32(int16(x[x_ptr+i+3])), PSHR32(sum3.Val, CeltConstants.SIG_SHIFT))))
	}

	for ; i < N; i++ {
		sum := int(0)
		for j := 0; j < ord; j++ {
			sum = MAC16_16IntAll(sum, rnum[j], local_x[i+j])
		}
		y[y_ptr+i] = int(SATURATE16(ADD32(EXTEND32(int16(x[x_ptr+i])), PSHR32(sum, CeltConstants.SIG_SHIFT))))
	}
}

func xcorr_kernel(x []int16, x_ptr int, y []int16, y_ptr int, _sum0 *BoxedValueInt, _sum1 *BoxedValueInt, _sum2 *BoxedValueInt, _sum3 *BoxedValueInt, len int) {

	sum0 := _sum0.Val
	sum1 := _sum1.Val
	sum2 := _sum2.Val
	sum3 := _sum3.Val

	OpusAssert(len >= 3)
	var y_0, y_1, y_2, y_3 int16
	y_3 = 0
	y_0 = y[y_ptr]
	y_ptr++
	y_1 = y[y_ptr]
	y_ptr++
	y_2 = y[y_ptr]
	y_ptr++

	j := 0
	for ; j < len-3; j += 4 {
		tmp := x[x_ptr]
		x_ptr++
		y_3 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16Int(sum0, tmp, y_0)
		sum1 = MAC16_16Int(sum1, tmp, y_1)
		sum2 = MAC16_16Int(sum2, tmp, y_2)
		sum3 = MAC16_16Int(sum3, tmp, y_3)

		tmp = x[x_ptr]
		x_ptr++
		y_0 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16Int(sum0, tmp, y_1)
		sum1 = MAC16_16Int(sum1, tmp, y_2)
		sum2 = MAC16_16Int(sum2, tmp, y_3)
		sum3 = MAC16_16Int(sum3, tmp, y_0)

		tmp = x[x_ptr]
		x_ptr++
		y_1 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16Int(sum0, tmp, y_2)
		sum1 = MAC16_16Int(sum1, tmp, y_3)
		sum2 = MAC16_16Int(sum2, tmp, y_0)
		sum3 = MAC16_16Int(sum3, tmp, y_1)

		tmp = x[x_ptr]
		x_ptr++
		y_2 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16Int(sum0, tmp, y_3)
		sum1 = MAC16_16Int(sum1, tmp, y_0)
		sum2 = MAC16_16Int(sum2, tmp, y_1)
		sum3 = MAC16_16Int(sum3, tmp, y_2)
	}

	if j < len {
		j++
		tmp := x[x_ptr]
		x_ptr++
		y_3 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16Int(sum0, tmp, y_0)
		sum1 = MAC16_16Int(sum1, tmp, y_1)
		sum2 = MAC16_16Int(sum2, tmp, y_2)
		sum3 = MAC16_16Int(sum3, tmp, y_3)
	}

	if j < len {
		j++
		tmp := x[x_ptr]
		x_ptr++
		y_0 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16Int(sum0, tmp, y_1)
		sum1 = MAC16_16Int(sum1, tmp, y_2)
		sum2 = MAC16_16Int(sum2, tmp, y_3)
		sum3 = MAC16_16Int(sum3, tmp, y_0)
	}

	if j < len {
		tmp := x[x_ptr]
		x_ptr++
		y_1 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16Int(sum0, tmp, y_2)
		sum1 = MAC16_16Int(sum1, tmp, y_3)
		sum2 = MAC16_16Int(sum2, tmp, y_0)
		sum3 = MAC16_16Int(sum3, tmp, y_1)
	}

	_sum0.Val = sum0
	_sum1.Val = sum1
	_sum2.Val = sum2
	_sum3.Val = sum3
}

func xcorr_kernel_int(x []int, y []int, y_ptr int, _sum0 *BoxedValueInt, _sum1 *BoxedValueInt, _sum2 *BoxedValueInt, _sum3 *BoxedValueInt, len int) {
	sum0 := _sum0.Val
	sum1 := _sum1.Val
	sum2 := _sum2.Val
	sum3 := _sum3.Val

	OpusAssert(len >= 3)
	var y_0, y_1, y_2, y_3 int
	y_3 = 0
	y_0 = y[y_ptr]
	y_ptr++
	y_1 = y[y_ptr]
	y_ptr++
	y_2 = y[y_ptr]
	y_ptr++
	x_ptr := 0

	j := 0
	for ; j < len-3; j += 4 {
		tmp := x[x_ptr]
		x_ptr++
		y_3 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16IntAll(sum0, tmp, y_0)
		sum1 = MAC16_16IntAll(sum1, tmp, y_1)
		sum2 = MAC16_16IntAll(sum2, tmp, y_2)
		sum3 = MAC16_16IntAll(sum3, tmp, y_3)

		tmp = x[x_ptr]
		x_ptr++
		y_0 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16IntAll(sum0, tmp, y_1)
		sum1 = MAC16_16IntAll(sum1, tmp, y_2)
		sum2 = MAC16_16IntAll(sum2, tmp, y_3)
		sum3 = MAC16_16IntAll(sum3, tmp, y_0)

		tmp = x[x_ptr]
		x_ptr++
		y_1 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16IntAll(sum0, tmp, y_2)
		sum1 = MAC16_16IntAll(sum1, tmp, y_3)
		sum2 = MAC16_16IntAll(sum2, tmp, y_0)
		sum3 = MAC16_16IntAll(sum3, tmp, y_1)

		tmp = x[x_ptr]
		x_ptr++
		y_2 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16IntAll(sum0, tmp, y_3)
		sum1 = MAC16_16IntAll(sum1, tmp, y_0)
		sum2 = MAC16_16IntAll(sum2, tmp, y_1)
		sum3 = MAC16_16IntAll(sum3, tmp, y_2)
	}

	if j < len {
		j++
		tmp := x[x_ptr]
		x_ptr++
		y_3 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16IntAll(sum0, tmp, y_0)
		sum1 = MAC16_16IntAll(sum1, tmp, y_1)
		sum2 = MAC16_16IntAll(sum2, tmp, y_2)
		sum3 = MAC16_16IntAll(sum3, tmp, y_3)
	}

	if j < len {
		j++
		tmp := x[x_ptr]
		x_ptr++
		y_0 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16IntAll(sum0, tmp, y_1)
		sum1 = MAC16_16IntAll(sum1, tmp, y_2)
		sum2 = MAC16_16IntAll(sum2, tmp, y_3)
		sum3 = MAC16_16IntAll(sum3, tmp, y_0)
	}

	if j < len {
		tmp := x[x_ptr]
		x_ptr++
		y_1 = y[y_ptr]
		y_ptr++
		sum0 = MAC16_16IntAll(sum0, tmp, y_2)
		sum1 = MAC16_16IntAll(sum1, tmp, y_3)
		sum2 = MAC16_16IntAll(sum2, tmp, y_0)
		sum3 = MAC16_16IntAll(sum3, tmp, y_1)
	}

	_sum0.Val = sum0
	_sum1.Val = sum1
	_sum2.Val = sum2
	_sum3.Val = sum3
}

func celt_inner_prod(x []int16, x_ptr int, y []int16, y_ptr int, N int) int {
	xy := int(0)
	for i := 0; i < N; i++ {
		xy = MAC16_16Int(xy, x[x_ptr+i], y[y_ptr+i])
	}
	return xy
}

func celt_inner_prod_short(x []int16, y []int16, y_ptr int, N int) int {
	xy := int(0)
	for i := 0; i < N; i++ {
		xy = MAC16_16Int(xy, x[i], y[y_ptr+i])
	}
	return xy
}

func celt_inner_prod_int(x []int, x_ptr int, y []int, y_ptr int, N int) int {
	xy := int(0)
	for i := 0; i < N; i++ {
		xy = MAC16_16IntAll(xy, x[x_ptr+i], y[y_ptr+i])
	}
	return xy
}

func dual_inner_prod(x []int, x_ptr int, y01 []int, y01_ptr int, y02 []int, y02_ptr int, N int, xy1 *BoxedValueInt, xy2 *BoxedValueInt) {
	xy01 := int(0)
	xy02 := int(0)
	for i := 0; i < N; i++ {
		xy01 = MAC16_16IntAll(xy01, x[x_ptr+i], y01[y01_ptr+i])
		xy02 = MAC16_16IntAll(xy02, x[x_ptr+i], y02[y02_ptr+i])
	}
	xy1.Val = xy01
	xy2.Val = xy02
}
