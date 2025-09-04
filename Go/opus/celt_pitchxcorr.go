package opus

func pitch_xcorr(_x []int, _y []int, xcorr []int, len int, max_pitch int) int {
	var i int
	maxcorr := int(1)
	OpusAssert(max_pitch > 0)
	sum0 := BoxedValueInt{0}
	sum1 := BoxedValueInt{0}
	sum2 := BoxedValueInt{0}
	sum3 := BoxedValueInt{0}
	for i = 0; i < max_pitch-3; i += 4 {
		sum0.Val = 0
		sum1.Val = 0
		sum2.Val = 0
		sum3.Val = 0
		xcorr_kernel_int(_x, _y, i, &sum0, &sum1, &sum2, &sum3, len)
		xcorr[i] = sum0.Val
		xcorr[i+1] = sum1.Val
		xcorr[i+2] = sum2.Val
		xcorr[i+3] = sum3.Val
		sum0.Val = MAX32(sum0.Val, sum1.Val)
		sum2.Val = MAX32(sum2.Val, sum3.Val)
		sum0.Val = MAX32(sum0.Val, sum2.Val)
		maxcorr = MAX32(maxcorr, sum0.Val)
	}
	for ; i < max_pitch; i++ {
		inner_sum := celt_inner_prod_int(_x, 0, _y, i, len)
		xcorr[i] = inner_sum
		maxcorr = MAX32(maxcorr, inner_sum)
	}
	return maxcorr
}

func pitch_xcorr1(_x []int16, _x_ptr int, _y []int16, _y_ptr int, xcorr []int, len int, max_pitch int) int {
	var i int
	var maxcorr = 1
	OpusAssert(max_pitch > 0)
	sum0 := &BoxedValueInt{0}
	sum1 := &BoxedValueInt{0}
	sum2 := &BoxedValueInt{0}
	sum3 := &BoxedValueInt{0}
	for i = 0; i < max_pitch-3; i += 4 {
		sum0.Val = 0
		sum1.Val = 0
		sum2.Val = 0
		sum3.Val = 0
		xcorr_kernel(_x, _x_ptr, _y, _y_ptr+i, sum0, sum1, sum2, sum3, len)

		xcorr[i] = sum0.Val
		xcorr[i+1] = sum1.Val
		xcorr[i+2] = sum2.Val
		xcorr[i+3] = sum3.Val
		sum0.Val = MAX32(sum0.Val, sum1.Val)
		sum2.Val = MAX32(sum2.Val, sum3.Val)
		sum0.Val = MAX32(sum0.Val, sum2.Val)
		maxcorr = MAX32(maxcorr, sum0.Val)
	}
	/* In case max_pitch isn't a multiple of 4, do non-unrolled version. */
	for ; i < max_pitch; i++ {
		inner_sum := celt_inner_prod(_x, _x_ptr, _y, _y_ptr+i, len)
		xcorr[i] = inner_sum
		maxcorr = MAX32(maxcorr, inner_sum)
	}
	return maxcorr
}

func pitch_xcorr2(_x []int16, _y []int16, xcorr []int, len int, max_pitch int) int {

	var i int
	var maxcorr int = 1
	OpusAssert(max_pitch > 0)
	sum0 := &BoxedValueInt{0}
	sum1 := &BoxedValueInt{0}
	sum2 := &BoxedValueInt{0}
	sum3 := &BoxedValueInt{0}
	for i = 0; i < max_pitch-3; i += 4 {
		sum0.Val = 0
		sum1.Val = 0
		sum2.Val = 0
		sum3.Val = 0
		xcorr_kernel(_x, 0, _y, i, sum0, sum1, sum2, sum3, len)

		xcorr[i] = sum0.Val
		xcorr[i+1] = sum1.Val
		xcorr[i+2] = sum2.Val
		xcorr[i+3] = sum3.Val
		sum0.Val = MAX32(sum0.Val, sum1.Val)
		sum2.Val = MAX32(sum2.Val, sum3.Val)
		sum0.Val = MAX32(sum0.Val, sum2.Val)
		maxcorr = MAX32(maxcorr, sum0.Val)
	}
	/* In case max_pitch isn't a multiple of 4, do non-unrolled version. */
	for ; i < max_pitch; i++ {
		inner_sum := celt_inner_prod_short(_x, _y, i, len)
		xcorr[i] = inner_sum
		maxcorr = MAX32(maxcorr, inner_sum)
	}
	return maxcorr
}
