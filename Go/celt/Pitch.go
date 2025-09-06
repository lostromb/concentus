package celt

import (
	"math"

	"github.com/dosgo/concentus/go/comm"
)

func find_best_pitch(xcorr []int, y []int, len int, max_pitch int, best_pitch []int, yshift int, maxcorr int) {
	Syy := 1
	best_num_0 := -1
	best_num_1 := -1
	best_den_0 := 0
	best_den_1 := 0
	best_pitch[0] = 0
	best_pitch[1] = 1
	xshift := inlines.Celt_ilog2(maxcorr) - 14

	for j := 0; j < len; j++ {
		Syy = inlines.ADD32(Syy, inlines.SHR32(inlines.MULT16_16(y[j], y[j]), yshift))
	}

	for i := 0; i < max_pitch; i++ {
		if xcorr[i] > 0 {
			xcorr16 := inlines.EXTRACT16(inlines.VSHR32(xcorr[i], xshift))
			num := inlines.MULT16_16_Q15Int(int(xcorr16), int(xcorr16))
			if inlines.MULT16_32_Q15Int(num, best_den_1) > inlines.MULT16_32_Q15Int(best_num_1, Syy) {
				if inlines.MULT16_32_Q15Int(num, best_den_0) > inlines.MULT16_32_Q15Int(best_num_0, Syy) {
					best_num_1 = best_num_0
					best_den_1 = best_den_0
					best_pitch[1] = best_pitch[0]
					best_num_0 = num
					best_den_0 = Syy
					best_pitch[0] = i
				} else {
					best_num_1 = num
					best_den_1 = Syy
					best_pitch[1] = i
				}
			}
		}
		Syy += inlines.SHR32(inlines.MULT16_16(y[i+len], y[i+len]), yshift) - inlines.SHR32(inlines.MULT16_16(y[i], y[i]), yshift)
		Syy = inlines.MAX32(1, Syy)
	}
}

func celt_fir5(x []int, num []int, y []int, N int, mem []int) {
	num0 := num[0]
	num1 := num[1]
	num2 := num[2]
	num3 := num[3]
	num4 := num[4]
	mem0 := mem[0]
	mem1 := mem[1]
	mem2 := mem[2]
	mem3 := mem[3]
	mem4 := mem[4]

	for i := 0; i < N; i++ {
		sum := inlines.SHL32(inlines.EXTEND32Int(x[i]), CeltConstants.SIG_SHIFT)
		sum = inlines.MAC16_16IntAll(sum, num0, mem0)
		sum = inlines.MAC16_16IntAll(sum, num1, mem1)
		sum = inlines.MAC16_16IntAll(sum, num2, mem2)
		sum = inlines.MAC16_16IntAll(sum, num3, mem3)
		sum = inlines.MAC16_16IntAll(sum, num4, mem4)
		mem4 = mem3
		mem3 = mem2
		mem2 = mem1
		mem1 = mem0
		mem0 = x[i]
		y[i] = inlines.ROUND16Int(sum, CeltConstants.SIG_SHIFT)
	}

	mem[0] = mem0
	mem[1] = mem1
	mem[2] = mem2
	mem[3] = mem3
	mem[4] = mem4
}

func pitch_downsample(x [][]int, x_lp []int, len int, C int) {
	ac := make([]int, 5)
	tmp := CeltConstants.Q15ONE
	lpc := make([]int, 4)
	mem := []int{0, 0, 0, 0, 0}
	lpc2 := make([]int, 5)
	c1 := int(math.Trunc(0.5 + (0.8)*((1)<<(15))))

	maxabs := inlines.Celt_maxabs32(x[0], 0, len)
	if C == 2 {
		maxabs_1 := inlines.Celt_maxabs32(x[1], 0, len)
		maxabs = inlines.MAX32(maxabs, maxabs_1)
	}
	if maxabs < 1 {
		maxabs = 1
	}
	shift := inlines.Celt_ilog2(maxabs) - 10
	if shift < 0 {
		shift = 0
	}
	if C == 2 {
		shift++
	}

	halflen := len >> 1
	for i := 1; i < halflen; i++ {
		x_lp[i] = inlines.SHR32(inlines.HALF32(inlines.HALF32(x[0][2*i-1]+x[0][2*i+1])+x[0][2*i]), shift)
	}
	x_lp[0] = inlines.SHR32(inlines.HALF32(inlines.HALF32(x[0][1])+x[0][0]), shift)

	if C == 2 {
		for i := 1; i < halflen; i++ {
			x_lp[i] += inlines.SHR32(inlines.HALF32(inlines.HALF32(x[1][2*i-1]+x[1][2*i+1])+x[1][2*i]), shift)
		}
		x_lp[0] += inlines.SHR32(inlines.HALF32(inlines.HALF32(x[1][1])+x[1][0]), shift)
	}

	comm.Celt_autocorr_with_window(x_lp, ac, nil, 0, 4, halflen)

	ac[0] += inlines.SHR32(ac[0], 13)
	for i := 1; i <= 4; i++ {
		ac[i] -= inlines.MULT16_32_Q15Int(2*i*i, ac[i])
	}

	celt_lpc(lpc, ac, 4)
	for i := 0; i < 4; i++ {
		tmp = inlines.MULT16_16_Q15Int(int(math.Trunc(0.5+0.9*float64(1<<15))), tmp)
		lpc[i] = inlines.MULT16_16_Q15Int(lpc[i], tmp)
	}

	lpc2[0] = lpc[0] + int(0.5+0.8*float32(int(1)<<CeltConstants.SIG_SHIFT))
	lpc2[1] = lpc[1] + inlines.MULT16_16_Q15Int(c1, lpc[0])
	lpc2[2] = lpc[2] + inlines.MULT16_16_Q15Int(c1, lpc[1])
	lpc2[3] = lpc[3] + inlines.MULT16_16_Q15Int(c1, lpc[2])
	lpc2[4] = inlines.MULT16_16_Q15Int(c1, lpc[3])

	celt_fir5(x_lp, lpc2, x_lp, halflen, mem)
}

func pitch_search(x_lp []int, x_lp_ptr int, y []int, len int, max_pitch int, pitch *comm.BoxedValueInt) {
	inlines.OpusAssert(len > 0)
	inlines.OpusAssert(max_pitch > 0)
	lag := len + max_pitch

	x_lp4 := make([]int, len>>2)
	y_lp4 := make([]int, lag>>2)
	xcorr := make([]int, max_pitch>>1)

	for j := 0; j < len>>2; j++ {
		x_lp4[j] = x_lp[x_lp_ptr+2*j]
	}
	for j := 0; j < lag>>2; j++ {
		y_lp4[j] = y[2*j]
	}

	xmax := inlines.Celt_maxabs32(x_lp4, 0, len>>2)
	ymax := inlines.Celt_maxabs32(y_lp4, 0, lag>>2)
	shift := inlines.Celt_ilog2(inlines.MAX32(1, inlines.MAX32(xmax, ymax))) - 11
	if shift > 0 {
		for j := 0; j < len>>2; j++ {
			x_lp4[j] = inlines.SHR16Int(x_lp4[j], shift)
		}
		for j := 0; j < lag>>2; j++ {
			y_lp4[j] = inlines.SHR16Int(y_lp4[j], shift)
		}
		shift *= 2
	} else {
		shift = 0
	}

	maxcorr := comm.Pitch_xcorr(x_lp4, y_lp4, xcorr, len>>2, max_pitch>>2)
	best_pitch := []int{0, 0}
	find_best_pitch(xcorr, y_lp4, len>>2, max_pitch>>2, best_pitch, 0, maxcorr)

	maxcorr = 1
	for i := 0; i < max_pitch>>1; i++ {
		if inlines.Abs(i-2*best_pitch[0]) > 2 && inlines.Abs(i-2*best_pitch[1]) > 2 {
			xcorr[i] = 0
			continue
		}
		sum := 0
		for j := 0; j < len>>1; j++ {
			sum += inlines.SHR32(inlines.MULT16_16(x_lp[x_lp_ptr+j], y[i+j]), shift)
		}
		xcorr[i] = inlines.MAX32(-1, sum)
		if sum > maxcorr {
			maxcorr = sum
		}
	}
	find_best_pitch(xcorr, y, len>>1, max_pitch>>1, best_pitch, shift+1, maxcorr)

	offset := 0
	if best_pitch[0] > 0 && best_pitch[0] < (max_pitch>>1)-1 {
		a := xcorr[best_pitch[0]-1]
		b := xcorr[best_pitch[0]]
		c := xcorr[best_pitch[0]+1]
		if (c - a) > inlines.MULT16_32_Q15Int(int(math.Trunc(0.5+0.7*float64(1<<15))), b-a) {
			offset = 1
		} else if (a - c) > inlines.MULT16_32_Q15Int(int(math.Trunc(0.5+0.7*float64(1<<15))), b-c) {
			offset = -1
		}
	}
	pitch.Val = 2*best_pitch[0] - offset
}

var second_check = []int{0, 0, 3, 2, 3, 2, 5, 2, 3, 2, 3, 2, 5, 2, 3, 2}

func remove_doubling(x []int, maxperiod int, minperiod int, N int, T0_ *comm.BoxedValueInt, prev_period int, prev_gain int) int {
	maxperiod /= 2
	minperiod /= 2
	T0_.Val /= 2
	prev_period /= 2
	N /= 2
	x_ptr := maxperiod
	if T0_.Val >= maxperiod {
		T0_.Val = maxperiod - 1
	}

	T := T0_.Val
	T0 := T0_.Val
	yy_lookup := make([]int, maxperiod+1)
	xx := 0
	xy := 0
	boxed_xx := comm.BoxedValueInt{0}
	boxed_xy := comm.BoxedValueInt{0}
	boxed_xy2 := comm.BoxedValueInt{0}

	kernels.Dual_inner_prod(x, x_ptr, x, x_ptr, x, x_ptr-T0, N, &boxed_xx, &boxed_xy)
	yy_lookup[0] = boxed_xx.Val
	yy := boxed_xx.Val
	for i := 1; i <= maxperiod; i++ {
		xi := x_ptr - i
		yy = yy + inlines.MULT16_16(x[xi], x[xi]) - inlines.MULT16_16(x[xi+N], x[xi+N])
		yy_lookup[i] = inlines.MAX32(0, yy)
	}
	yy = yy_lookup[T0]
	best_xy := xy
	best_yy := yy

	x2y2 := 1 + inlines.HALF32(inlines.MULT32_32_Q31(xx, yy))
	sh := inlines.Celt_ilog2(x2y2) >> 1
	t := inlines.VSHR32(x2y2, 2*(sh-7))
	g := inlines.VSHR32(inlines.MULT16_32_Q15Int(inlines.Celt_rsqrt_norm(t), xy), sh+1)
	g0 := g

	for k := 2; k <= 15; k++ {
		T1 := (2*T0 + k) / (2 * k)
		if T1 < minperiod {
			break
		}
		var T1b int
		if k == 2 {
			if T1+T0 > maxperiod {
				T1b = T0
			} else {
				T1b = T0 + T1
			}
		} else {
			T1b = (2*second_check[k]*T0 + k) / (2 * k)
		}
		xy2 := 0
		kernels.Dual_inner_prod(x, x_ptr, x, x_ptr-T1, x, x_ptr-T1b, N, &boxed_xy, &boxed_xy2)
		xy = boxed_xy.Val
		xy2 = boxed_xy2.Val
		xy += xy2
		yy = yy_lookup[T1] + yy_lookup[T1b]

		x2y2 = 1 + inlines.MULT32_32_Q31(xx, yy)
		sh = inlines.Celt_ilog2(x2y2) >> 1
		t = inlines.VSHR32(x2y2, 2*(sh-7))
		g1 := inlines.VSHR32(inlines.MULT16_32_Q15Int(inlines.Celt_rsqrt_norm(t), xy), sh+1)

		cont := 0
		if inlines.Abs(T1-prev_period) <= 1 {
			cont = prev_gain
		} else if inlines.Abs(T1-prev_period) <= 2 && 5*k*k < T0 {
			cont = inlines.HALF16Int(prev_gain)
		}

		thresh := inlines.MAX16Int(int(math.Trunc(0.5+0.3*float64(1<<15))), inlines.MULT16_16_Q15Int(int(math.Trunc(0.5+0.7*float64(1<<15))), g0)-cont)
		if T1 < 3*minperiod {
			thresh = inlines.MAX16Int(int(math.Trunc(0.5+0.4*float64(1<<15))), inlines.MULT16_16_Q15Int(int(math.Trunc(0.5+0.85*float64(1<<15))), g0)-cont)
		} else if T1 < 2*minperiod {
			thresh = inlines.MAX16Int(int(math.Trunc(0.5+0.5*float64(1<<15))), inlines.MULT16_16_Q15Int(int(math.Trunc(0.5+0.9*float64(1<<15))), g0)-cont)
		}
		if g1 > thresh {
			best_xy = xy
			best_yy = yy
			T = T1
			g = g1
		}
	}

	best_xy = inlines.MAX32(0, best_xy)
	pg := CeltConstants.Q15ONE
	if best_yy > best_xy {
		pg = inlines.SHR32(inlines.Frac_div32(best_xy, best_yy+1), 16)
	}

	xcorr := [3]int{}
	for k := 0; k < 3; k++ {
		xcorr[k] = kernels.Celt_inner_prod_int(x, x_ptr, x, x_ptr-(T+k-1), N)
	}

	offset := 0
	if (xcorr[2] - xcorr[0]) > inlines.MULT16_32_Q15Int(int(math.Trunc(0.5+0.7*float64(1<<15))), xcorr[1]-xcorr[0]) {
		offset = 1
	} else if (xcorr[0] - xcorr[2]) > inlines.MULT16_32_Q15Int(int(math.Trunc(0.5+0.7*float64(1<<15))), xcorr[1]-xcorr[2]) {
		offset = -1
	}

	if pg > g {
		pg = g
	}

	T0_.Val = 2*T + offset
	if T0_.Val < minperiod*2 {
		T0_.Val = minperiod * 2
	}
	return pg
}
