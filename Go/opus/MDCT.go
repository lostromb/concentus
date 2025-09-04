package opus

func clt_mdct_forward(l *MDCTLookup, input []int, input_ptr int, output []int, output_ptr int, window []int, overlap int, shift int, stride int) {
	var i int
	var N, N2, N4 int
	var f []int
	var f2 []int
	st := l.kfft[shift]
	var trig []int16
	trig_ptr := 0
	var scale int

	scale_shift := st.scale_shift - 1
	scale = int(st.scale)

	N = l.n
	trig = l.trig
	for i = 0; i < shift; i++ {
		N = N >> 1
		trig_ptr += N
	}
	N2 = N >> 1
	N4 = N >> 2

	f = make([]int, N2)
	f2 = make([]int, N4*2)

	{
		xp1 := input_ptr + (overlap >> 1)
		xp2 := input_ptr + N2 - 1 + (overlap >> 1)
		yp := 0
		wp1 := (overlap >> 1)
		wp2 := ((overlap >> 1) - 1)
		for i = 0; i < ((overlap + 3) >> 2); i++ {
			f[yp] = MULT16_32_Q15Int(window[wp2], input[xp1+N2]) + MULT16_32_Q15Int(window[wp1], input[xp2])
			yp++
			f[yp] = MULT16_32_Q15Int(window[wp1], input[xp1]) - MULT16_32_Q15Int(window[wp2], input[xp2-N2])
			yp++
			xp1 += 2
			xp2 -= 2
			wp1 += 2
			wp2 -= 2
		}
		wp1 = 0
		wp2 = overlap - 1
		for ; i < N4-((overlap+3)>>2); i++ {
			f[yp] = input[xp2]
			yp++
			f[yp] = input[xp1]
			yp++
			xp1 += 2
			xp2 -= 2
		}
		for ; i < N4; i++ {
			f[yp] = MULT16_32_Q15Int(window[wp2], input[xp2]) - MULT16_32_Q15Int(window[wp1], input[xp1-N2])
			yp++
			f[yp] = MULT16_32_Q15Int(window[wp2], input[xp1]) + MULT16_32_Q15Int(window[wp1], input[xp2+N2])
			yp++
			xp1 += 2
			xp2 -= 2
			wp1 += 2
			wp2 -= 2
		}
	}
	{
		yp := 0
		t := trig_ptr
		for i = 0; i < N4; i++ {
			var t0, t1 int
			var re, im, yr, yi int
			t0 = int(trig[t+i])
			t1 = int(trig[t+N4+i])
			re = f[yp]
			yp++
			im = f[yp]
			yp++
			yr = S_MUL(re, t0) - S_MUL(im, t1)
			yi = S_MUL(im, t0) + S_MUL(re, t1)
			idx := 2 * int(st.bitrev[i])
			f2[idx] = PSHR32(MULT16_32_Q16Int(scale, yr), scale_shift)
			f2[idx+1] = PSHR32(MULT16_32_Q16Int(scale, yi), scale_shift)
		}
	}

	opus_fft_impl(st, f2, 0)

	{
		fp := 0
		yp1 := output_ptr
		yp2 := output_ptr + (stride * (N2 - 1))
		t := trig_ptr
		for i = 0; i < N4; i++ {
			var yr, yi int
			yr = S_MUL(f2[fp+1], int(trig[t+N4+i])) - S_MUL(f2[fp], int(trig[t+i]))
			yi = S_MUL(f2[fp], int(trig[t+N4+i])) + S_MUL(f2[fp+1], int(trig[t+i]))
			output[yp1] = yr
			output[yp2] = yi
			fp += 2
			yp1 += 2 * stride
			yp2 -= 2 * stride
		}
	}
}

func clt_mdct_backward(l *MDCTLookup, input []int, input_ptr int, output []int, output_ptr int, window []int, overlap int, shift int, stride int) {
	var i int
	var N, N2, N4 int
	trig := 0
	var xp1, xp2, yp, yp0, yp1 int

	N = l.n
	for i = 0; i < shift; i++ {
		N >>= 1
		trig += N
	}
	N2 = N >> 1
	N4 = N >> 2

	xp2 = input_ptr + (stride * (N2 - 1))
	yp = output_ptr + (overlap >> 1)
	bitrev := l.kfft[shift].bitrev
	bitrav_ptr := 0
	for i = 0; i < N4; i++ {
		rev := int(bitrev[bitrav_ptr])
		bitrav_ptr++
		ypr := yp + 2*rev
		output[ypr+1] = S_MUL(input[xp2], int(l.trig[trig+i])) + S_MUL(input[input_ptr], int(l.trig[trig+N4+i]))
		output[ypr] = S_MUL(input[input_ptr], int(l.trig[trig+i])) - S_MUL(input[xp2], int(l.trig[trig+N4+i]))
		input_ptr += 2 * stride
		xp2 -= 2 * stride
	}

	opus_fft_impl(l.kfft[shift], output, output_ptr+(overlap>>1))

	yp0 = output_ptr + (overlap >> 1)
	yp1 = output_ptr + (overlap >> 1) + N2 - 2
	t := trig

	tN4m1 := t + N4 - 1
	tN2m1 := t + N2 - 1
	for i = 0; i < (N4+1)>>1; i++ {
		var re, im, yr, yi int
		var t0, t1 int
		re = output[yp0+1]
		im = output[yp0]
		t0 = int(l.trig[t+i])
		t1 = int(l.trig[t+N4+i])
		yr = S_MUL(re, t0) + S_MUL(im, t1)
		yi = S_MUL(re, t1) - S_MUL(im, t0)
		re = output[yp1+1]
		im = output[yp1]
		output[yp0] = yr
		output[yp1+1] = yi
		t0 = int(l.trig[tN4m1-i])
		t1 = int(l.trig[tN2m1-i])
		yr = S_MUL(re, t0) + S_MUL(im, t1)
		yi = S_MUL(re, t1) - S_MUL(im, t0)
		output[yp1] = yr
		output[yp0+1] = yi
		yp0 += 2
		yp1 -= 2
	}

	xp1 = output_ptr + overlap - 1
	yp1 = output_ptr
	wp1 := 0
	wp2 := overlap - 1

	for i = 0; i < overlap/2; i++ {
		x1 := output[xp1]
		x2 := output[yp1]
		output[yp1] = MULT16_32_Q15Int(window[wp2], x2) - MULT16_32_Q15Int(window[wp1], x1)
		yp1++
		output[xp1] = MULT16_32_Q15Int(window[wp1], x2) + MULT16_32_Q15Int(window[wp2], x1)
		xp1--
		wp1++
		wp2--
	}
}
