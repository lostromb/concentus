package opus

import (
	"math"
)

var SPREAD_FACTOR = [3]int{15, 10, 5}

func exp_rotation1(X []int, X_ptr int, len int, stride int, c int, s int) {
	ms := NEG16Int(s)
	Xptr := X_ptr
	for i := 0; i < len-stride; i++ {
		x1 := X[Xptr]
		x2 := X[Xptr+stride]
		X[Xptr+stride] = int(EXTRACT16(PSHR32(MAC16_16IntAll(MULT16_16(c, x2), s, x1), 15)))
		X[Xptr+stride] = int(EXTRACT16(PSHR32(MAC16_16IntAll(MULT16_16(c, x2), s, x1), 15)))
		X[Xptr] = int(EXTRACT16(PSHR32(MAC16_16IntAll(MULT16_16(c, x1), ms, x2), 15)))
		Xptr++
	}
	Xptr = X_ptr + (len - 2*stride - 1)
	for i := len - 2*stride - 1; i >= 0; i-- {
		x1 := X[Xptr]
		x2 := X[Xptr+stride]
		X[Xptr+stride] = int(EXTRACT16(PSHR32(MAC16_16IntAll(MULT16_16(c, x2), s, x1), 15)))
		X[Xptr] = int(EXTRACT16(PSHR32(MAC16_16IntAll(MULT16_16(c, x1), ms, x2), 15)))
		Xptr--
	}
}

func exp_rotation(X []int, X_ptr int, len int, dir int, stride int, K int, spread int) {
	if 2*K >= len || spread == Spread.SPREAD_NONE {
		return
	}

	factor := SPREAD_FACTOR[spread-1]
	gain := celt_div(int(MULT16_16(int(CeltConstants.Q15_ONE), len)), (len + factor*K))
	theta := HALF16Int(MULT16_16_Q15Int(gain, gain))
	c := celt_cos_norm(EXTEND32Int(theta))
	s := celt_cos_norm(EXTEND32Int(SUB16Int(CeltConstants.Q15ONE, theta)))
	stride2 := 0
	if len >= 8*stride {
		stride2 = 1
		for (stride2*stride2+stride2)*stride+(stride>>2) < len {
			stride2++
		}
	}

	len = celt_udiv(len, stride)
	for i := 0; i < stride; i++ {
		if dir < 0 {
			if stride2 != 0 {
				exp_rotation1(X, X_ptr+i*len, len, stride2, s, c)
			}
			exp_rotation1(X, X_ptr+i*len, len, 1, c, s)
		} else {
			exp_rotation1(X, X_ptr+i*len, len, 1, c, NEG16Int(s))
			if stride2 != 0 {
				exp_rotation1(X, X_ptr+i*len, len, stride2, s, NEG16Int(c))
			}
		}
	}
}

func normalise_residual(iy []int, X []int, X_ptr int, N int, Ryy int, gain int) {
	k := celt_ilog2(Ryy) >> 1
	t := VSHR32(Ryy, 2*(k-7))
	g := MULT16_16_P15Int(celt_rsqrt_norm(t), gain)
	for i := 0; i < N; i++ {
		X[X_ptr+i] = int(EXTRACT16(PSHR32(MULT16_16(g, iy[i]), k+1)))
	}
}

func extract_collapse_mask(iy []int, N int, B int) int {
	if B <= 1 {
		return 1
	}
	N0 := celt_udiv(N, B)
	collapse_mask := 0
	for i := 0; i < B; i++ {
		tmp := 0
		for j := 0; j < N0; j++ {
			tmp |= iy[i*N0+j]
		}
		if tmp != 0 {
			collapse_mask |= 1 << i
		}
	}
	return collapse_mask
}

func alg_quant(X []int, X_ptr int, N int, K int, spread int, B int, enc *EntropyCoder) int {
	y := make([]int, N)
	iy := make([]int, N)
	signx := make([]int, N)
	var i, j int
	var s int
	var pulsesLeft int
	var sum int
	var xy int
	var yy int
	var collapse_mask int

	OpusAssertMsg(K > 0, "alg_quant() needs at least one pulse")
	OpusAssertMsg(N > 1, "alg_quant() needs at least two dimensions")

	exp_rotation(X, X_ptr, N, 1, B, K, spread)

	sum = 0
	j = 0
	for j < N {
		if X[X_ptr+j] > 0 {
			signx[j] = 1
		} else {
			signx[j] = -1
			X[X_ptr+j] = -X[X_ptr+j]
		}
		iy[j] = 0
		y[j] = 0
		j++
	}

	xy = 0
	yy = 0

	pulsesLeft = K

	if K > (N >> 1) {
		var rcp int
		j = 0
		for j < N {
			sum += X[X_ptr+j]
			j++
		}

		if sum <= K {
			X[X_ptr] = 16384
			j = X_ptr + 1
			for j < X_ptr+N {
				X[j] = 0
				j++
			}
			sum = 16384
		}

		rcp = int(EXTRACT16(MULT16_32_Q16Int(int(K-1), celt_rcp(sum))))
		j = 0
		for j < N {
			iy[j] = MULT16_16_Q15Int(int(X[X_ptr+j]), int(rcp))
			y[j] = int(iy[j])
			yy = MAC16_16Int(yy, int16(y[j]), int16(y[j]))
			xy = MAC16_16Int(xy, int16(X[X_ptr+j]), int16(y[j]))
			y[j] *= 2
			pulsesLeft -= iy[j]
			j++
		}
	}

	OpusAssertMsg(pulsesLeft >= 1, "Allocated too many pulses in the quick pass")

	if pulsesLeft > N+3 {
		tmp := pulsesLeft
		yy = MAC16_16Int(yy, int16(tmp), int16(tmp))
		yy = MAC16_16Int(yy, int16(tmp), int16(y[0]))
		iy[0] += pulsesLeft
		pulsesLeft = 0
	}

	s = 1
	for i = 0; i < pulsesLeft; i++ {
		best_id := 0
		var best_num int = 0 - int(CeltConstants.VERY_LARGE16)
		best_den := 0
		rshift := 1 + celt_ilog2(K-pulsesLeft+i+1)
		yy = ADD16Int(yy, 1)
		j = 0
		for j < N {
			var Rxy, Ryy int
			Rxy = int(EXTRACT16(SHR32(ADD32(int(xy), EXTEND32Int(X[X_ptr+j])), rshift)))
			Ryy = ADD16Int(yy, y[j])
			Rxy = MULT16_16_Q15Int(int(Rxy), int(Rxy))
			if MULT16_16(int(best_den), int(Rxy)) > MULT16_16(int(Ryy), int(best_num)) {
				best_den = Ryy
				best_num = int(Rxy)
				best_id = j
			}
			j++
		}

		xy = ADD32(xy, EXTEND32Int(X[X_ptr+best_id]))
		yy = ADD16Int(yy, y[best_id])
		y[best_id] += 2 * s
		iy[best_id]++
	}

	j = 0
	for j < N {
		X[X_ptr+j] = MULT16_16(int(signx[j]), int(X[X_ptr+j]))
		if signx[j] < 0 {
			iy[j] = -iy[j]
		}
		j++
	}
	encode_pulses(iy, N, K, enc)

	collapse_mask = extract_collapse_mask(iy, N, B)

	return collapse_mask
}

func alg_unquant(X []int, X_ptr int, N int, K int, spread int, B int, dec *EntropyCoder, gain int) int {
	OpusAssertMsg(K > 0, "alg_unquant() needs at least one pulse")
	OpusAssertMsg(N > 1, "alg_unquant() needs at least two dimensions")
	iy := make([]int, N)
	Ryy := decode_pulses(iy, N, K, dec)
	normalise_residual(iy, X, X_ptr, N, Ryy, gain)
	exp_rotation(X, X_ptr, N, -1, B, K, spread)
	collapse_mask := extract_collapse_mask(iy, N, B)
	return collapse_mask
}

func renormalise_vector(X []int, X_ptr int, N int, gain int) {
	//PrintFuncArgs(X, X_ptr, N, gain)

	var i int
	var k int
	var E int
	var g int
	var t int
	var xptr int
	E = CeltConstants.EPSILON + celt_inner_prod_int(X, X_ptr, X, X_ptr, N)
	k = celt_ilog2(E) >> 1
	t = VSHR32(E, 2*(k-7))
	g = MULT16_16_P15Int(celt_rsqrt_norm(t), gain)

	xptr = X_ptr
	for i = 0; i < N; i++ {
		X[xptr] = int(EXTRACT16(PSHR32(MULT16_16(g, X[xptr]), k+1)))
		xptr++
	}
}

func stereo_itheta(X []int, X_ptr int, Y []int, Y_ptr int, stereo int, N int) int {
	Emid := CeltConstants.EPSILON
	Eside := CeltConstants.EPSILON
	if stereo != 0 {
		for i := 0; i < N; i++ {
			m := ADD16Int(SHR16Int(X[X_ptr+i], 1), SHR16Int(Y[Y_ptr+i], 1))
			s := SUB16Int(SHR16Int(X[X_ptr+i], 1), SHR16Int(Y[Y_ptr+i], 1))
			Emid = MAC16_16IntAll(Emid, m, m)
			Eside = MAC16_16IntAll(Eside, s, s)
		}
	} else {
		Emid += celt_inner_prod_int(X, X_ptr, X, X_ptr, N)
		Eside += celt_inner_prod_int(Y, Y_ptr, Y, Y_ptr, N)
	}
	mid := celt_sqrt(Emid)
	side := celt_sqrt(Eside)
	itheta := MULT16_16_Q15Int(int(math.Trunc(0.5+(0.63662)*(1<<15))), celt_atan2p(side, mid))
	return itheta
}
