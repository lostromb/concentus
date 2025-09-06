package celt

import "github.com/dosgo/concentus/go/comm"

func celt_lpc(_lpc []int, ac []int, p int) {
	var i, j int
	var r int
	error := ac[0]
	lpc := make([]int, p)

	if ac[0] != 0 {
		for i = 0; i < p; i++ {
			var rr int
			for j = 0; j < i; j++ {
				rr += inlines.MULT32_32_Q31(lpc[j], ac[i-j])
			}
			rr += inlines.SHR32(ac[i+1], 3)
			r = 0 - inlines.Frac_div32(inlines.SHL32(rr, 3), error)
			lpc[i] = inlines.SHR32(r, 3)

			for j = 0; j < (i+1)>>1; j++ {
				tmp1 := lpc[j]
				tmp2 := lpc[i-1-j]
				lpc[j] = tmp1 + inlines.MULT32_32_Q31(r, tmp2)
				lpc[i-1-j] = tmp2 + inlines.MULT32_32_Q31(r, tmp1)
			}

			error = error - inlines.MULT32_32_Q31(inlines.MULT32_32_Q31(r, r), error)
			if error < inlines.SHR32(ac[0], 10) {
				break
			}
		}
	}

	for i = 0; i < p; i++ {
		_lpc[i] = inlines.ROUND16Int(lpc[i], 16)
	}
}

func celt_iir(_x []int, _x_ptr int, den []int, _y []int, _y_ptr int, N int, ord int, mem []int) {
	var i, j int
	rden := make([]int, ord)
	y := make([]int, N+ord)
	inlines.OpusAssert((ord & 3) == 0)

	var _sum0, _sum1, _sum2, _sum3 comm.BoxedValueInt
	var sum0, sum1, sum2, sum3 int

	for i = 0; i < ord; i++ {
		rden[i] = den[ord-i-1]
	}
	for i = 0; i < ord; i++ {
		y[i] = -mem[ord-i-1]
	}
	for ; i < N+ord; i++ {
		y[i] = 0
	}
	for i = 0; i < N-3; i += 4 {
		_sum0.Val = _x[_x_ptr+i]
		_sum1.Val = _x[_x_ptr+i+1]
		_sum2.Val = _x[_x_ptr+i+2]
		_sum3.Val = _x[_x_ptr+i+3]
		kernels.Xcorr_kernel_int(rden, y, i, &_sum0, &_sum1, &_sum2, &_sum3, ord)
		sum0 = _sum0.Val
		sum1 = _sum1.Val
		sum2 = _sum2.Val
		sum3 = _sum3.Val

		y[i+ord] = -inlines.ROUND16Int(sum0, CeltConstants.SIG_SHIFT)
		_y[_y_ptr+i] = sum0
		sum1 = inlines.MAC16_16IntAll(sum1, y[i+ord], den[0])
		y[i+ord+1] = -inlines.ROUND16Int(sum1, CeltConstants.SIG_SHIFT)
		_y[_y_ptr+i+1] = sum1
		sum2 = inlines.MAC16_16IntAll(sum2, y[i+ord+1], den[0])
		sum2 = inlines.MAC16_16IntAll(sum2, y[i+ord], den[1])
		y[i+ord+2] = -inlines.ROUND16Int(sum2, CeltConstants.SIG_SHIFT)
		_y[_y_ptr+i+2] = sum2

		sum3 = inlines.MAC16_16IntAll(sum3, y[i+ord+2], den[0])
		sum3 = inlines.MAC16_16IntAll(sum3, y[i+ord+1], den[1])
		sum3 = inlines.MAC16_16IntAll(sum3, y[i+ord], den[2])
		y[i+ord+3] = -inlines.ROUND16Int(sum3, CeltConstants.SIG_SHIFT)
		_y[_y_ptr+i+3] = sum3
	}
	for ; i < N; i++ {
		sum := _x[_x_ptr+i]
		for j = 0; j < ord; j++ {
			sum -= inlines.MULT16_16(rden[j], y[i+j])
		}
		y[i+ord] = inlines.ROUND16Int(sum, CeltConstants.SIG_SHIFT)
		_y[_y_ptr+i] = sum
	}
	for i = 0; i < ord; i++ {
		mem[i] = _y[_y_ptr+N-i-1]
	}
}
