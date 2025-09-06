package celt

import "math"

const MAXFACTORS = 8

func S_MUL(a, b int) int {
	return inlines.MULT16_32_Q15Int((b), a)
}

func HALF_OF(x int) int {
	return x >> 1
}

func kf_bfly2(Fout []int, fout_ptr int, m int, N int) {
	var Fout2 int
	var i int
	{
		var tw int16
		tw = int16(math.Trunc(0.5 + 0.7071067812*float64(1<<15)))
		inlines.OpusAssertMsg(m == 4, "")
		for i = 0; i < N; i++ {
			var t_r, t_i int
			Fout2 = fout_ptr + 8
			t_r = Fout[Fout2+0]
			t_i = Fout[Fout2+1]
			Fout[Fout2+0] = Fout[fout_ptr+0] - t_r
			Fout[Fout2+1] = Fout[fout_ptr+1] - t_i
			Fout[fout_ptr+0] += t_r
			Fout[fout_ptr+1] += t_i

			t_r = S_MUL(Fout[Fout2+2]+Fout[Fout2+3], int(tw))
			t_i = S_MUL(Fout[Fout2+3]-Fout[Fout2+2], int(tw))
			Fout[Fout2+2] = Fout[fout_ptr+2] - t_r
			Fout[Fout2+3] = Fout[fout_ptr+3] - t_i
			Fout[fout_ptr+2] += t_r
			Fout[fout_ptr+3] += t_i

			t_r = Fout[Fout2+5]
			t_i = 0 - Fout[Fout2+4]
			Fout[Fout2+4] = Fout[fout_ptr+4] - t_r
			Fout[Fout2+5] = Fout[fout_ptr+5] - t_i
			Fout[fout_ptr+4] += t_r
			Fout[fout_ptr+5] += t_i

			t_r = S_MUL(Fout[Fout2+7]-Fout[Fout2+6], int(tw))
			t_i = S_MUL(0-Fout[Fout2+7]-Fout[Fout2+6], int(tw))
			Fout[Fout2+6] = Fout[fout_ptr+6] - t_r
			Fout[Fout2+7] = Fout[fout_ptr+7] - t_i
			Fout[fout_ptr+6] += t_r
			Fout[fout_ptr+7] += t_i

			fout_ptr += 16
		}
	}
}

func kf_bfly4(Fout []int, fout_ptr int, fstride int, st *FFTState, m int, N int, mm int) {
	var i int

	if m == 1 {
		var scratch0, scratch1, scratch2, scratch3 int
		for i = 0; i < N; i++ {
			scratch0 = Fout[fout_ptr+0] - Fout[fout_ptr+4]
			scratch1 = Fout[fout_ptr+1] - Fout[fout_ptr+5]
			Fout[fout_ptr+0] += Fout[fout_ptr+4]
			Fout[fout_ptr+1] += Fout[fout_ptr+5]
			scratch2 = Fout[fout_ptr+2] + Fout[fout_ptr+6]
			scratch3 = Fout[fout_ptr+3] + Fout[fout_ptr+7]
			Fout[fout_ptr+4] = Fout[fout_ptr+0] - scratch2
			Fout[fout_ptr+5] = Fout[fout_ptr+1] - scratch3
			Fout[fout_ptr+0] += scratch2
			Fout[fout_ptr+1] += scratch3
			scratch2 = Fout[fout_ptr+2] - Fout[fout_ptr+6]
			scratch3 = Fout[fout_ptr+3] - Fout[fout_ptr+7]
			Fout[fout_ptr+2] = scratch0 + scratch3
			Fout[fout_ptr+3] = scratch1 - scratch2
			Fout[fout_ptr+6] = scratch0 - scratch3
			Fout[fout_ptr+7] = scratch1 + scratch2
			fout_ptr += 8
		}
	} else {
		var j int
		var scratch0, scratch1, scratch2, scratch3, scratch4, scratch5, scratch6, scratch7, scratch8, scratch9, scratch10, scratch11 int
		var tw1, tw2, tw3 int
		Fout_beg := fout_ptr
		for i = 0; i < N; i++ {
			fout_ptr = Fout_beg + 2*i*mm
			m1 := fout_ptr + (2 * m)
			m2 := fout_ptr + (4 * m)
			m3 := fout_ptr + (6 * m)
			tw1 = 0
			tw2 = 0
			tw3 = 0
			for j = 0; j < m; j++ {
				scratch0 = S_MUL(Fout[m1], int(st.twiddles[tw1])) - S_MUL(Fout[m1+1], int(st.twiddles[tw1+1]))
				scratch1 = S_MUL(Fout[m1], int(st.twiddles[tw1+1])) + S_MUL(Fout[m1+1], int(st.twiddles[tw1]))
				scratch2 = S_MUL(Fout[m2], int(st.twiddles[tw2])) - S_MUL(Fout[m2+1], int(st.twiddles[tw2+1]))
				scratch3 = S_MUL(Fout[m2], int(st.twiddles[tw2+1])) + S_MUL(Fout[m2+1], int(st.twiddles[tw2]))
				scratch4 = S_MUL(Fout[m3], int(st.twiddles[tw3])) - S_MUL(Fout[m3+1], int(st.twiddles[tw3+1]))
				scratch5 = S_MUL(Fout[m3], int(st.twiddles[tw3+1])) + S_MUL(Fout[m3+1], int(st.twiddles[tw3]))
				scratch10 = Fout[fout_ptr+0] - scratch2
				scratch11 = Fout[fout_ptr+1] - scratch3
				Fout[fout_ptr+0] += scratch2
				Fout[fout_ptr+1] += scratch3
				scratch6 = scratch0 + scratch4
				scratch7 = scratch1 + scratch5
				scratch8 = scratch0 - scratch4
				scratch9 = scratch1 - scratch5
				Fout[m2+0] = Fout[fout_ptr+0] - scratch6
				Fout[m2+1] = Fout[fout_ptr+1] - scratch7
				tw1 += fstride * 2
				tw2 += fstride * 4
				tw3 += fstride * 6
				Fout[fout_ptr+0] += scratch6
				Fout[fout_ptr+1] += scratch7
				Fout[m1+0] = scratch10 + scratch9
				Fout[m1+1] = scratch11 - scratch8
				Fout[m3+0] = scratch10 - scratch9
				Fout[m3+1] = scratch11 + scratch8
				fout_ptr += 2
				m1 += 2
				m2 += 2
				m3 += 2
			}
		}
	}
}

func kf_bfly3(Fout []int, fout_ptr int, fstride int, st *FFTState, m int, N int, mm int) {
	var i int
	var k int
	m1 := 2 * m
	m2 := 4 * m
	var tw1, tw2 int
	var scratch0, scratch1, scratch2, scratch3, scratch4, scratch5, scratch6, scratch7 int

	Fout_beg := fout_ptr

	for i = 0; i < N; i++ {
		fout_ptr = Fout_beg + 2*i*mm
		tw1 = 0
		tw2 = 0
		k = m
		for k != 0 {
			scratch2 = S_MUL(Fout[fout_ptr+m1], int(st.twiddles[tw1])) - S_MUL(Fout[fout_ptr+m1+1], int(st.twiddles[tw1+1]))
			scratch3 = S_MUL(Fout[fout_ptr+m1], int(st.twiddles[tw1+1])) + S_MUL(Fout[fout_ptr+m1+1], int(st.twiddles[tw1]))
			scratch4 = S_MUL(Fout[fout_ptr+m2], int(st.twiddles[tw2])) - S_MUL(Fout[fout_ptr+m2+1], int(st.twiddles[tw2+1]))
			scratch5 = S_MUL(Fout[fout_ptr+m2], int(st.twiddles[tw2+1])) + S_MUL(Fout[fout_ptr+m2+1], int(st.twiddles[tw2]))

			scratch6 = scratch2 + scratch4
			scratch7 = scratch3 + scratch5
			scratch0 = scratch2 - scratch4
			scratch1 = scratch3 - scratch5

			tw1 += fstride * 2
			tw2 += fstride * 4

			Fout[fout_ptr+m1+0] = Fout[fout_ptr+0] - HALF_OF(scratch6)
			Fout[fout_ptr+m1+1] = Fout[fout_ptr+1] - HALF_OF(scratch7)

			scratch0 = S_MUL(scratch0, -28378)
			scratch1 = S_MUL(scratch1, -28378)

			Fout[fout_ptr+0] += scratch6
			Fout[fout_ptr+1] += scratch7

			Fout[fout_ptr+m2+0] = Fout[fout_ptr+m1+0] + scratch1
			Fout[fout_ptr+m2+1] = Fout[fout_ptr+m1+1] - scratch0

			Fout[fout_ptr+m1+0] -= scratch1
			Fout[fout_ptr+m1+1] += scratch0

			fout_ptr += 2
			k--
		}
	}
}

func kf_bfly5(Fout []int, fout_ptr int, fstride int, st *FFTState, m int, N int, mm int) {
	var Fout0, Fout1, Fout2, Fout3, Fout4 int
	var i, u int
	var scratch0, scratch1, scratch2, scratch3, scratch4, scratch5 int
	var scratch6, scratch7, scratch8, scratch9, scratch10, scratch11 int
	var scratch12, scratch13, scratch14, scratch15, scratch16, scratch17 int
	var scratch18, scratch19, scratch20, scratch21, scratch22, scratch23 int
	var scratch24, scratch25 int

	Fout_beg := fout_ptr

	ya_r := int16(10126)
	ya_i := int16(-31164)
	yb_r := int16(-26510)
	yb_i := int16(-19261)
	var tw1, tw2, tw3, tw4 int

	for i = 0; i < N; i++ {
		tw1 = 0
		tw2 = 0
		tw3 = 0
		tw4 = 0
		fout_ptr = Fout_beg + 2*i*mm
		Fout0 = fout_ptr
		Fout1 = fout_ptr + (2 * m)
		Fout2 = fout_ptr + (4 * m)
		Fout3 = fout_ptr + (6 * m)
		Fout4 = fout_ptr + (8 * m)

		for u = 0; u < m; u++ {
			scratch0 = Fout[Fout0+0]
			scratch1 = Fout[Fout0+1]

			scratch2 = S_MUL(Fout[Fout1+0], int(st.twiddles[tw1])) - S_MUL(Fout[Fout1+1], int(st.twiddles[tw1+1]))
			scratch3 = S_MUL(Fout[Fout1+0], int(st.twiddles[tw1+1])) + S_MUL(Fout[Fout1+1], int(st.twiddles[tw1]))
			scratch4 = S_MUL(Fout[Fout2+0], int(st.twiddles[tw2])) - S_MUL(Fout[Fout2+1], int(st.twiddles[tw2+1]))
			scratch5 = S_MUL(Fout[Fout2+0], int(st.twiddles[tw2+1])) + S_MUL(Fout[Fout2+1], int(st.twiddles[tw2]))
			scratch6 = S_MUL(Fout[Fout3+0], int(st.twiddles[tw3])) - S_MUL(Fout[Fout3+1], int(st.twiddles[tw3+1]))
			scratch7 = S_MUL(Fout[Fout3+0], int(st.twiddles[tw3+1])) + S_MUL(Fout[Fout3+1], int(st.twiddles[tw3]))
			scratch8 = S_MUL(Fout[Fout4+0], int(st.twiddles[tw4])) - S_MUL(Fout[Fout4+1], int(st.twiddles[tw4+1]))
			scratch9 = S_MUL(Fout[Fout4+0], int(st.twiddles[tw4+1])) + S_MUL(Fout[Fout4+1], int(st.twiddles[tw4]))

			tw1 += 2 * fstride
			tw2 += 4 * fstride
			tw3 += 6 * fstride
			tw4 += 8 * fstride

			scratch14 = scratch2 + scratch8
			scratch15 = scratch3 + scratch9
			scratch20 = scratch2 - scratch8
			scratch21 = scratch3 - scratch9
			scratch16 = scratch4 + scratch6
			scratch17 = scratch5 + scratch7
			scratch18 = scratch4 - scratch6
			scratch19 = scratch5 - scratch7

			Fout[Fout0+0] += scratch14 + scratch16
			Fout[Fout0+1] += scratch15 + scratch17

			scratch10 = scratch0 + S_MUL(scratch14, int(ya_r)) + S_MUL(scratch16, int(yb_r))
			scratch11 = scratch1 + S_MUL(scratch15, int(ya_r)) + S_MUL(scratch17, int(yb_r))

			scratch12 = S_MUL(scratch21, int(ya_i)) + S_MUL(scratch19, int(yb_i))
			scratch13 = 0 - S_MUL(scratch20, int(ya_i)) - S_MUL(scratch18, int(yb_i))

			Fout[Fout1+0] = scratch10 - scratch12
			Fout[Fout1+1] = scratch11 - scratch13
			Fout[Fout4+0] = scratch10 + scratch12
			Fout[Fout4+1] = scratch11 + scratch13

			scratch22 = scratch0 + S_MUL(scratch14, int(yb_r)) + S_MUL(scratch16, int(ya_r))
			scratch23 = scratch1 + S_MUL(scratch15, int(yb_r)) + S_MUL(scratch17, int(ya_r))
			scratch24 = 0 - S_MUL(scratch21, int(yb_i)) + S_MUL(scratch19, int(ya_i))
			scratch25 = S_MUL(scratch20, int(yb_i)) - S_MUL(scratch18, int(ya_i))

			Fout[Fout2+0] = scratch22 + scratch24
			Fout[Fout2+1] = scratch23 + scratch25
			Fout[Fout3+0] = scratch22 - scratch24
			Fout[Fout3+1] = scratch23 - scratch25

			Fout0 += 2
			Fout1 += 2
			Fout2 += 2
			Fout3 += 2
			Fout4 += 2
		}
	}
}

func opus_fft_impl(st *FFTState, fout []int, fout_ptr int) {
	var m2, m int
	var p int
	var L int
	fstride := [MAXFACTORS]int{}
	var i int
	var shift int

	if st.shift > 0 {
		shift = st.shift
	} else {
		shift = 0
	}

	fstride[0] = 1
	L = 0
	for {
		p = int(st.factors[2*L])
		m = int(st.factors[2*L+1])
		if L+1 < MAXFACTORS {
			fstride[L+1] = fstride[L] * p
		}
		L++
		if m == 1 {
			break
		}
	}

	m = int(st.factors[2*L-1])
	for i = L - 1; i >= 0; i-- {
		if i != 0 {
			m2 = int(st.factors[2*i-1])
		} else {
			m2 = 1
		}
		switch st.factors[2*i] {
		case 2:
			kf_bfly2(fout, fout_ptr, m, fstride[i])
		case 4:
			kf_bfly4(fout, fout_ptr, fstride[i]<<shift, st, m, fstride[i], m2)
		case 3:
			kf_bfly3(fout, fout_ptr, fstride[i]<<shift, st, m, fstride[i], m2)
		case 5:
			kf_bfly5(fout, fout_ptr, fstride[i]<<shift, st, m, fstride[i], m2)
		}
		m = m2
	}
}

func Opus_fft(st *FFTState, fin []int, fout []int) {
	var i int
	scale_shift := st.scale_shift - 1
	scale := int16(st.scale)

	inlines.OpusAssertMsg(!(fin != nil && fout != nil && &fin[0] == &fout[0]), "In-place FFT not supported")

	for i = 0; i < st.nfft; i++ {
		rev := st.bitrev[i]
		fout[2*rev] = inlines.SHR32(inlines.MULT16_32_Q16(scale, fin[2*i]), scale_shift)
		fout[2*rev+1] = inlines.SHR32(inlines.MULT16_32_Q16(scale, fin[2*i+1]), scale_shift)
	}

	opus_fft_impl(st, fout, 0)
}
