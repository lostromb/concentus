package celt

import (
	"encoding/json"
	"fmt"
	"math"

	"github.com/lostromb/concentus/go/comm"
	"github.com/lostromb/concentus/go/comm/arrayUtil"
)

type band_ctx struct {
	encode         int
	m              *CeltMode
	i              int
	intensity      int
	spread         int
	tf_change      int
	ec             *comm.EntropyCoder
	remaining_bits int
	bandE          [][]int
	seed           int
}

type split_ctx struct {
	inv    int
	imid   int
	iside  int
	delta  int
	itheta int
	qalloc int
}

func hysteresis_decision(val int, thresholds []int, hysteresis []int, N int, prev int) int {
	i := 0
	for i < N {
		if val < thresholds[i] {
			break
		}
		i++
	}
	if i > prev && val < thresholds[prev]+hysteresis[prev] {
		i = prev
	}
	if i < prev && val > thresholds[prev-1]-hysteresis[prev-1] {
		i = prev
	}
	return i
}

func celt_lcg_rand(seed int) int {
	return int(int32(1664525*seed + 1013904223))
}

func bitexact_cos(x int) int {
	var tmp = 0
	var x2 = 0
	tmp = (4096 + (x * x)) >> 13
	inlines.OpusAssert(tmp <= 32767)
	x2 = (tmp)
	x2 = ((32767 - x2) + inlines.FRAC_MUL16(x2, (-7651+inlines.FRAC_MUL16(x2, (8277+inlines.FRAC_MUL16(-626, x2))))))
	inlines.OpusAssert(x2 <= 32766)
	return (1 + x2)
}

func bitexact_log2tan(isin int, icos int) int {
	lc := inlines.EC_ILOG(int64(icos))
	ls := inlines.EC_ILOG(int64(isin))
	icos <<= 15 - lc
	isin <<= 15 - ls
	return (ls-lc)*(1<<11) +
		inlines.FRAC_MUL16(isin, inlines.FRAC_MUL16(isin, -2597)+7932) -
		inlines.FRAC_MUL16(icos, inlines.FRAC_MUL16(icos, -2597)+7932)
}

func Compute_band_energies(m *CeltMode, X [][]int, bandE [][]int, end int, C int, LM int) {
	eBands := m.eBands
	//N := m.shortMdctSize << LM
	for c := 0; c < C; c++ {
		for i := 0; i < end; i++ {
			maxval := inlines.Celt_maxabs32(X[c], int(eBands[i]<<LM), int(eBands[i+1]-eBands[i])<<LM)
			if maxval > 0 {
				shift := inlines.Celt_ilog2(maxval) - 14 + ((int(m.logN[i])>>BITRES + LM + 1) >> 1)
				j := eBands[i] << LM
				sum := 0
				if shift > 0 {
					for j < eBands[i+1]<<LM {
						x := inlines.EXTRACT16(inlines.SHR32(X[c][j], shift))
						sum = inlines.MAC16_16Int(sum, x, x)
						j++
					}
				} else {
					for j < eBands[i+1]<<LM {
						x := inlines.EXTRACT16(inlines.SHL32(X[c][j], -shift))
						sum = inlines.MAC16_16Int(sum, x, x)
						j++
					}
				}
				bandE[c][i] = CeltConstants.EPSILON + inlines.VSHR32(inlines.Celt_sqrt(sum), -shift)
			} else {
				bandE[c][i] = CeltConstants.EPSILON
			}
		}
	}
}

func normalise_bands(m *CeltMode, freq [][]int, X [][]int, bandE [][]int, end int, C int, M int) {
	eBands := m.eBands
	for c := 0; c < C; c++ {
		for i := 0; i < end; i++ {
			shift := inlines.Celt_zlog2(bandE[c][i]) - 13
			E := inlines.VSHR32(bandE[c][i], shift)
			g := int(inlines.EXTRACT16(inlines.Celt_rcp(inlines.SHL32(E, 3))))
			j := M * int(eBands[i])
			endBand := M * int(eBands[i+1])
			for j < endBand {
				X[c][j] = inlines.MULT16_16_Q15Int(inlines.VSHR32(freq[c][j], shift-1), g)
				j++
			}
		}
	}
}

func denormalise_bands(m *CeltMode, X []int, freq []int, freq_ptr int, bandLogE []int, bandLogE_ptr int, start int, end int, M int, downsample int, silence int) {
	eBands := m.eBands
	N := M * m.ShortMdctSize
	bound := M * int(eBands[end])
	if downsample != 1 {
		bound = inlines.IMIN(bound, N/downsample)
	}
	if silence != 0 {
		bound = 0
		start = 0
		end = 0
	}
	f := freq_ptr
	x := M * int(eBands[start])

	for i := 0; i < M*int(eBands[start]); i++ {
		freq[f] = 0
		f++
	}

	for i := start; i < end; i++ {
		j := M * int(eBands[i])
		band_end := M * int(eBands[i+1])
		lg := inlines.ADD16Int(bandLogE[bandLogE_ptr+i], inlines.SHL16Int(int(CeltTables.EMeans[i]), 6))
		shift := 16 - (int(lg) >> int(CeltConstants.DB_SHIFT))
		g := 0
		if shift > 31 {
			shift = 0
			g = 0
		} else {
			g = inlines.Celt_exp2_frac(lg & ((1 << CeltConstants.DB_SHIFT) - 1))
		}
		if shift < 0 {
			if shift < -2 {
				g = 32767
				shift = -2
			}
			for j < band_end {
				freq[f] = inlines.SHR32(inlines.MULT16_16(X[x], g), -shift)
				j++
				x++
				f++
			}
		} else {
			for j < band_end {
				freq[f] = inlines.SHR32(inlines.MULT16_16(X[x], g), shift)
				j++
				x++
				f++
			}
		}
	}

	inlines.OpusAssert(start <= end)
	for i := bound; i < N; i++ {
		freq[freq_ptr+i] = 0
	}
}

func anti_collapse(m *CeltMode, X_ [][]int, collapse_masks []int16, LM int, C int, size int, start int, end int, logE []int, prev1logE []int, prev2logE []int, pulses []int, seed int) {
	var c, i, j, k int
	for i = start; i < end; i++ {
		var N0 int
		var thresh, sqrt_1 int
		var depth int
		var shift int
		var thresh32 int

		N0 = int(m.eBands[i+1] - m.eBands[i])
		/* depth in 1/8 bits */
		inlines.OpusAssert(pulses[i] >= 0)
		depth = inlines.Celt_udiv(1+pulses[i], int(m.eBands[i+1]-m.eBands[i])) >> LM

		thresh32 = inlines.SHR32(inlines.Celt_exp2(int(0-inlines.SHL16(int16(depth), 10-BITRES))), 1)
		thresh = (inlines.MULT16_32_Q15(int16(math.Trunc(0.5+(0.5)*float64((int32(1))<<(15)))), inlines.MIN32(32767, thresh32)))
		{
			var t int
			t = N0 << LM
			shift = inlines.Celt_ilog2(t) >> 1
			t = inlines.SHL32(t, (7-shift)<<1)
			sqrt_1 = inlines.Celt_rsqrt_norm(t)
		}

		c = 0
		for {
			var X int
			var prev1 int
			var prev2 int
			var Ediff int
			var r int
			var renormalize = 0
			prev1 = prev1logE[c*m.nbEBands+i]
			prev2 = prev2logE[c*m.nbEBands+i]
			if C == 1 {
				prev1 = inlines.MAX16Int(prev1, prev1logE[m.nbEBands+i])
				prev2 = inlines.MAX16Int(prev2, prev2logE[m.nbEBands+i])
			}
			Ediff = inlines.EXTEND32(int16(logE[c*m.nbEBands+i])) - inlines.EXTEND32(inlines.MIN16(int16(prev1), int16(prev2)))
			Ediff = inlines.MAX32(0, Ediff)

			if Ediff < 16384 {
				r32 := inlines.SHR32(inlines.Celt_exp2(int(0-inlines.EXTRACT16(Ediff))), 1)
				r = (2 * inlines.MIN16Int(16383, (r32)))
			} else {
				r = 0
			}
			if LM == 3 {
				r = inlines.MULT16_16_Q14Int(23170, inlines.MIN32(23169, r)) // opus bug: was inlines.MIN32
			}
			r = inlines.SHR16Int(inlines.MIN16Int(thresh, r), 1)
			r = (inlines.SHR32(inlines.MULT16_16_Q15Int(sqrt_1, r), shift))

			X = int(m.eBands[i] << LM)
			for k = 0; k < 1<<LM; k++ {
				/* Detect collapse */
				if int32(collapse_masks[i*C+c])&int32(1<<k) == 0 {
					/* Fill with noise */
					Xk := X + k
					for j = 0; j < N0; j++ {
						seed = celt_lcg_rand(seed)
						if (seed & 0x8000) != 0 {
							X_[c][Xk+(j<<LM)] = r
						} else {
							X_[c][Xk+(j<<LM)] = 0 - r
						}

					}
					renormalize = 1
				}
			}
			/* We just added some energy, so we need to renormalise */
			if renormalize != 0 {
				renormalise_vector(X_[c], X, N0<<LM, CeltConstants.Q15ONE)
			}
			c++
			if c < C {
				continue
			}
			break

		}
	}
}

func intensity_stereo(m *CeltMode, X []int, X_ptr int, Y []int, Y_ptr int, bandE [][]int, bandID int, N int) {
	var i = bandID
	var j = 0
	var a1, a2 int
	var left, right int
	var norm int = 0
	var shift = inlines.Celt_zlog2(inlines.MAX32(bandE[0][i], bandE[1][i])) - 13
	left = inlines.VSHR32(bandE[0][i], shift)
	right = inlines.VSHR32(bandE[1][i], shift)
	norm = CeltConstants.EPSILON + inlines.Celt_sqrt(CeltConstants.EPSILON+inlines.MULT16_16(left, left)+inlines.MULT16_16(right, right))
	a1 = inlines.DIV32_16Int(inlines.SHL32(left, 14), norm)
	a2 = inlines.DIV32_16Int(inlines.SHL32(right, 14), norm)
	for j = 0; j < N; j++ {
		//   System.out.println("eeeeee\r\n");
		var r, l int
		l = X[X_ptr+j]
		r = Y[Y_ptr+j]
		X[X_ptr+j] = int(inlines.EXTRACT16(inlines.SHR32(inlines.MAC16_16IntAll(inlines.MULT16_16(a1, l), a2, r), 14)))
		/* Side is not encoded, no need to calculate */
	}
}

func stereo_split(X []int, X_ptr int, Y []int, Y_ptr int, N int) {
	for j := 0; j < N; j++ {
		l := inlines.MULT16_16(int(math.Trunc(0.5+(.70710678)*((1)<<(15)))), X[X_ptr+j])
		r := inlines.MULT16_16(int(math.Trunc(0.5+(.70710678)*((1)<<(15)))), Y[Y_ptr+j])
		X[X_ptr+j] = int(inlines.EXTRACT16(inlines.SHR32(inlines.ADD32(l, r), 15)))
		Y[Y_ptr+j] = int(inlines.EXTRACT16(inlines.SHR32(inlines.SUB32(r, l), 15)))
	}
}

func stereo_merge(X []int, X_ptr int, Y []int, Y_ptr int, mid int, N int) {
	xp := &comm.BoxedValueInt{Val: 0}
	side := &comm.BoxedValueInt{Val: 0}
	kernels.Dual_inner_prod(Y, Y_ptr, X, X_ptr, Y, Y_ptr, N, xp, side)
	xp.Val = inlines.MULT16_32_Q15Int(mid, xp.Val)
	mid2 := inlines.SHR16Int(mid, 1)
	El := inlines.MULT16_16(mid2, mid2) + side.Val - (2 * xp.Val)
	Er := inlines.MULT16_16(mid2, mid2) + side.Val + (2 * xp.Val)
	if Er < inlines.QCONST32(6e-4, 28) || El < inlines.QCONST32(6e-4, 28) {
		copy(Y[Y_ptr:Y_ptr+N], X[X_ptr:X_ptr+N])
		return
	}

	kl := inlines.Celt_ilog2(El) >> 1
	kr := inlines.Celt_ilog2(Er) >> 1
	t := inlines.VSHR32(El, (kl-7)<<1)
	lgain := inlines.Celt_rsqrt_norm(t)
	t = inlines.VSHR32(Er, (kr-7)<<1)
	rgain := inlines.Celt_rsqrt_norm(t)

	if kl < 7 {
		kl = 7
	}
	if kr < 7 {
		kr = 7
	}

	for j := 0; j < N; j++ {
		l := inlines.MULT16_16_P15Int(mid, X[X_ptr+j])
		r := Y[Y_ptr+j]
		X[X_ptr+j] = int(inlines.EXTRACT16(inlines.PSHR32(inlines.MULT16_16(lgain, inlines.SUB16Int(l, r)), kl+1)))
		Y[Y_ptr+j] = int(inlines.EXTRACT16(inlines.PSHR32(inlines.MULT16_16(rgain, inlines.ADD16Int(l, r)), kr+1)))
	}
}

func spreading_decision(m *CeltMode, X [][]int, average *comm.BoxedValueInt, last_decision int, hf_average *comm.BoxedValueInt, tapset_decision *comm.BoxedValueInt, update_hf int, end int, C int, M int) int {
	eBands := m.eBands
	inlines.OpusAssert(end > 0)

	if M*int(eBands[end]-eBands[end-1]) <= 8 {
		return SPREAD_NONE
	}

	sum := 0
	nbBands := 0
	hf_sum := 0

	for c := 0; c < C; c++ {
		for i := 0; i < end; i++ {
			N := M * int(eBands[i+1]-eBands[i])
			if N <= 8 {
				continue
			}

			tcount := [3]int{0, 0, 0}
			x_ptr := M * int(eBands[i])
			for j := x_ptr; j < x_ptr+N; j++ {
				x2N := inlines.MULT16_16(inlines.MULT16_16_Q15Int(X[c][j], X[c][j]), N)
				if x2N < inlines.QCONST32(0.25, 13) {
					tcount[0]++
				}
				if x2N < inlines.QCONST32(0.0625, 13) {
					tcount[1]++
				}
				if x2N < inlines.QCONST32(0.015625, 13) {
					tcount[2]++
				}
			}

			if i > m.nbEBands-4 {
				hf_sum += inlines.Celt_udiv(32*(tcount[1]+tcount[0]), N)
			}

			tmp := 0
			if 2*tcount[2] >= N {
				tmp = 1
			}
			if 2*tcount[1] >= N {
				tmp++
			}
			if 2*tcount[0] >= N {
				tmp++
			}
			sum += tmp * 256
			nbBands++
		}
	}

	if update_hf != 0 {
		if hf_sum > 0 {
			hf_sum = inlines.Celt_udiv(hf_sum, C*(4-m.nbEBands+end))
		}

		hf_average.Val = (hf_average.Val + hf_sum) >> 1
		hf_sum = hf_average.Val

		if tapset_decision.Val == 2 {
			hf_sum += 4
		} else if tapset_decision.Val == 0 {
			hf_sum -= 4
		}
		if hf_sum > 22 {
			tapset_decision.Val = 2
		} else if hf_sum > 18 {
			tapset_decision.Val = 1
		} else {
			tapset_decision.Val = 0
		}
	}

	inlines.OpusAssert(nbBands > 0)
	sum = inlines.Celt_udiv(sum, nbBands)
	sum = (sum + average.Val) >> 1
	average.Val = sum
	sum = (3*sum + (((3 - last_decision) << 7) + 64) + 2) >> 2

	decision := SPREAD_NONE
	if sum < 80 {
		decision = SPREAD_AGGRESSIVE
	} else if sum < 256 {
		decision = SPREAD_NORMAL
	} else if sum < 384 {
		decision = SPREAD_LIGHT
	} else {
		decision = SPREAD_NONE
	}
	return decision
}

func deinterleave_hadamard(X []int, X_ptr int, N0 int, stride int, hadamard int) {
	var i, j int
	var N int
	N = N0 * stride
	tmp := make([]int, N)

	inlines.OpusAssert(stride > 0)
	if hadamard != 0 {
		var ordery = (stride - 2)

		for i = 0; i < stride; i++ {
			for j = 0; j < N0; j++ {
				tmp[ordery_table[ordery+i]*N0+j] = X[j*stride+i+X_ptr]
			}
		}
	} else {
		for i = 0; i < stride; i++ {
			for j = 0; j < N0; j++ {
				tmp[i*N0+j] = X[j*stride+i+X_ptr]
			}
		}
	}

	//System.arraycopy(tmp, 0, X, X_ptr, N)
	copy(X[X_ptr:], tmp[0:N])
}

func interleave_hadamard(X []int, X_ptr int, N0 int, stride int, hadamard int) {
	N := N0 * stride
	tmp := make([]int, N)

	if hadamard != 0 {
		ordery := stride - 2
		for i := 0; i < stride; i++ {
			for j := 0; j < N0; j++ {
				tmp[j*stride+i] = X[X_ptr+ordery_table[ordery+i]*N0+j]
			}
		}
	} else {
		for i := 0; i < stride; i++ {
			for j := 0; j < N0; j++ {
				tmp[j*stride+i] = X[X_ptr+i*N0+j]
			}
		}
	}
	copy(X[X_ptr:X_ptr+N], tmp)
}

func haar1(X []int, X_ptr int, N0 int, stride int) {
	var i, j int
	N0 >>= 1
	for i = 0; i < stride; i++ {
		for j = 0; j < N0; j++ {
			var tmpidx = X_ptr + i + (stride * 2 * j)
			var tmp1, tmp2 int
			tmp1 = inlines.MULT16_16(int(int16(math.Trunc(0.5+(0.70710678)*float64(int32(1)<<(15))))), X[tmpidx])
			tmp2 = inlines.MULT16_16(int(int16(math.Trunc(0.5+(0.70710678)*float64(int32(1)<<(15))))), X[tmpidx+stride])
			X[tmpidx] = int(inlines.EXTRACT16(inlines.PSHR32(inlines.ADD32(tmp1, tmp2), 15)))
			X[tmpidx+stride] = int(inlines.EXTRACT16(inlines.PSHR32(inlines.SUB32(tmp1, tmp2), 15)))
		}
	}
}

func haar1ZeroOffset(X []int, N0 int, stride int) {
	var i, j int
	N0 >>= 1
	for i = 0; i < stride; i++ {
		for j = 0; j < N0; j++ {
			tmpidx := i + (stride * 2 * j)

			tmp1 := inlines.MULT16_16(int(math.Trunc(0.5+(.70710678)*((1)<<(15)))), X[tmpidx])
			tmp2 := inlines.MULT16_16(int(math.Trunc(0.5+(.70710678)*((1)<<(15)))), X[tmpidx+stride])
			X[tmpidx] = int(inlines.EXTRACT16(inlines.PSHR32(inlines.ADD32(tmp1, tmp2), 15)))
			X[tmpidx+stride] = int(inlines.EXTRACT16(inlines.PSHR32(inlines.SUB32(tmp1, tmp2), 15)))
		}
	}
}

func compute_qn(N int, b int, offset int, pulse_cap int, stereo int) int {
	exp2_table8 := []int16{16384, 17866, 19483, 21247, 23170, 25267, 27554, 30048}

	N2 := 2*N - 1
	if stereo != 0 && N == 2 {
		N2--
	}
	qb := inlines.Celt_sudiv(b+N2*offset, N2)
	qb = inlines.IMIN(b-pulse_cap-(4<<BITRES), qb)
	qb = inlines.IMIN(8<<BITRES, qb)

	qn := 1
	if qb >= (1<<BITRES)>>1 {
		qn = int(exp2_table8[qb&0x7]) >> (14 - (qb >> BITRES))
		qn = ((qn + 1) >> 1) << 1
	}
	inlines.OpusAssert(qn <= 256)
	return qn
}

func compute_theta1(ctx *band_ctx, sctx *split_ctx, X []int, X_ptr int, Y []int, Y_ptr int, N int, b *comm.BoxedValueInt, B int, B0 int, LM int, stereo int, fill *comm.BoxedValueInt) {
	encode := ctx.encode
	m := ctx.m
	i := ctx.i
	intensity := ctx.intensity
	ec := ctx.ec
	bandE := ctx.bandE
	var inv = 0
	pulse_cap := int(m.logN[i]) + LM*(1<<BITRES)
	offset := (pulse_cap >> 1)
	if stereo != 0 && N == 2 {
		offset -= CeltConstants.QTHETA_OFFSET_TWOPHASE
	} else {
		offset -= CeltConstants.QTHETA_OFFSET
	}
	qn := compute_qn(N, b.Val, offset, pulse_cap, stereo)
	if stereo != 0 && i >= intensity {
		qn = 1
	}

	itheta := 0
	if encode != 0 {
		itheta = stereo_itheta(X, X_ptr, Y, Y_ptr, stereo, N)
	}

	tell := int(ec.Tell_frac())
	if qn != 1 {
		if encode != 0 {
			itheta = (itheta*qn + 8192) >> 14
		}

		if stereo != 0 && N > 2 {
			p0 := 3
			x := itheta
			x0 := qn / 2
			ft := inlines.CapToUintLong(int64(p0*(x0+1) + x0))
			if encode != 0 {
				if x <= x0 {
					ec.Encode(int64(p0*x), int64(p0*(x+1)), ft)
				} else {
					ec.Encode(int64((x-1-x0)+(x0+1)*p0), int64((x-x0)+(x0+1)*p0), ft)
				}
			} else {
				fs := int(ec.Decode(ft))
				if fs < (x0+1)*p0 {
					x = fs / p0
				} else {
					x = x0 + 1 + (fs - (x0+1)*p0)
				}
				if x <= x0 {
					ec.Dec_update(int64(p0*x), int64(p0*(x+1)), ft)
				} else {
					ec.Dec_update(int64((x-1-x0)+(x0+1)*p0), int64((x-x0)+(x0+1)*p0), ft)
				}
				itheta = x
			}
		} else if B0 > 1 || stereo != 0 {
			if encode != 0 {
				ec.Enc_uint(int64(itheta), int64(qn+1))
			} else {
				itheta = int(ec.Dec_uint(int64(qn + 1)))
			}
		} else {
			var fs = 1
			ft := ((qn >> 1) + 1) * ((qn >> 1) + 1)
			if encode != 0 {
				fs := 0
				fl := 0
				if itheta <= qn>>1 {
					fs = itheta + 1
					fl = itheta * (itheta + 1) >> 1
				} else {
					fs = qn + 1 - itheta
					fl = ft - ((qn + 1 - itheta) * (qn + 2 - itheta) >> 1)
				}
				ec.Encode(int64(fl), int64(fl+fs), int64(ft))
			} else {
				fm := int(ec.Decode(int64(ft)))
				fl := 0
				if fm < (qn>>1)*((qn>>1)+1)>>1 {
					itheta = (inlines.Isqrt32(int64(8*fm+1)) - 1) >> 1
					fs = itheta + 1
					fl = itheta * (itheta + 1) >> 1
				} else {
					itheta = (2*(qn+1) - inlines.Isqrt32(int64(8*(ft-fm-1)+1))) >> 1
					fs = qn + 1 - itheta
					fl = ft - ((qn + 1 - itheta) * (qn + 2 - itheta) >> 1)
				}
				ec.Dec_update(int64(fl), int64(fl+fs), int64(ft))
			}
		}
		inlines.OpusAssert(itheta >= 0)
		itheta = inlines.Celt_udiv(itheta*16384, qn)
		if encode != 0 && stereo != 0 {
			if itheta == 0 {
				intensity_stereo(m, X, X_ptr, Y, Y_ptr, bandE, i, N)
			} else {
				stereo_split(X, X_ptr, Y, Y_ptr, N)
			}
		}
	} else if stereo != 0 {

		if encode != 0 {
			if itheta > 8192 {
				inv = 1
				for j := 0; j < N; j++ {
					Y[Y_ptr+j] = -Y[Y_ptr+j]
				}
			}
			intensity_stereo(m, X, X_ptr, Y, Y_ptr, bandE, i, N)
		}
		if b.Val > 2<<BITRES && ctx.remaining_bits > 2<<BITRES {
			if encode != 0 {
				ec.Enc_bit_logp(inv, 2)
			} else {
				inv = ec.Dec_bit_logp(2)
			}
		} else {
			inv = 0
		}
		itheta = 0
	}
	qalloc := int(ec.Tell_frac()) - tell
	b.Val -= qalloc

	var imid = 0
	var iside = 0
	var delta = 0
	if itheta == 0 {
		imid = 32767
		iside = 0
		fill.Val &= (1 << B) - 1
		delta = -16384
	} else if itheta == 16384 {
		imid = 0
		iside = 32767
		fill.Val &= ((1 << B) - 1) << B
		delta = 16384
	} else {
		imid = bitexact_cos(itheta)
		iside = bitexact_cos((16384 - itheta))
		/* This is the mid vs side allocation that minimizes squared error
		   in that band. */
		//System.out.println("compute_theta-1 delta:"+ delta);
		delta = inlines.FRAC_MUL16((N-1)<<7, bitexact_log2tan(iside, imid))
	}

	sctx.inv = inv
	sctx.imid = imid
	sctx.iside = iside
	sctx.delta = delta
	sctx.itheta = itheta
	sctx.qalloc = qalloc
}

func compute_theta(ctx *band_ctx, sctx *split_ctx, X []int, X_ptr int, Y []int, Y_ptr int, N int, b *comm.BoxedValueInt, B int, B0 int, LM int, stereo int, fill *comm.BoxedValueInt) {
	var qn int
	var itheta = 0
	var delta int
	var imid, iside int
	var qalloc int
	var pulse_cap int
	var offset int
	var tell int
	var inv = 0
	var encode int
	var m *CeltMode
	var i int
	var intensity int
	var ec *comm.EntropyCoder // porting note: pointer
	var bandE [][]int

	encode = ctx.encode
	m = ctx.m
	i = ctx.i
	intensity = ctx.intensity
	ec = ctx.ec
	bandE = ctx.bandE

	/* Decide on the resolution to give to the split parameter theta */
	pulse_cap = int(m.logN[i]) + LM*(1<<BITRES)
	if stereo != 0 && N == 2 {
		offset = (pulse_cap >> 1) - (CeltConstants.QTHETA_OFFSET_TWOPHASE)
	} else {
		offset = (pulse_cap >> 1) - CeltConstants.QTHETA_OFFSET
	}

	qn = compute_qn(N, b.Val, offset, pulse_cap, stereo)
	if stereo != 0 && i >= intensity {
		qn = 1
	}

	if encode != 0 {
		/* theta is the atan() of the ratio between the (normalized)
		   side and mid. With just that parameter, we can re-scale both
		   mid and side because we know that 1) they have unit norm and
		   2) they are orthogonal. */
		itheta = stereo_itheta(X, X_ptr, Y, Y_ptr, stereo, N)
	}

	tell = ec.Tell_frac()

	if qn != 1 {
		if encode != 0 {
			itheta = (itheta*qn + 8192) >> 14
		}

		/* Entropy coding of the angle. We use a uniform pdf for the
		   time split, a step for stereo, and a triangular one for the rest. */
		if stereo != 0 && N > 2 {
			var p0 = 3
			var x = itheta
			var x0 = qn / 2
			var ft = inlines.CapToUInt32(int64(p0*(x0+1) + x0))
			/* Use a probability of p0 up to itheta=8192 and then use 1 after */
			if encode != 0 {
				if x <= x0 {
					ec.Encode(int64((p0 * x)), int64((p0 * (x + 1))), ft)
				} else {
					ec.Encode(int64((x-1-x0)+(x0+1)*p0), int64((x-x0)+(x0+1)*p0), ft)

				}

			} else {
				var fs = ec.Decode(ft)
				if fs < int64((x0+1)*p0) {
					x = int(fs / int64(p0))
				} else {
					x = x0 + 1 + int(fs-int64(x0+1)*int64(p0))
				}
				if x <= x0 {
					ec.Dec_update(int64(p0*x), int64(p0*(x+1)), ft)
				} else {
					ec.Dec_update(int64((x-1-x0)+(x0+1)*p0), int64((x-x0)+(x0+1)*p0), ft)
				}

				itheta = x
			}
		} else if B0 > 1 || stereo != 0 {
			/* Uniform pdf */
			if encode != 0 {
				ec.Enc_uint(int64(itheta), int64(qn+1))
			} else {
				itheta = int(ec.Dec_uint(int64(qn + 1)))
			}
		} else {
			var fs = 1
			ft := ((qn >> 1) + 1) * ((qn >> 1) + 1)
			if encode != 0 {
				var fl int

				if itheta <= (qn >> 1) {
					fs = itheta + 1
					fl = itheta * (itheta + 1) >> 1
				} else {
					fs = qn + 1 - itheta
					fl = ft - ((qn + 1 - itheta) * (qn + 2 - itheta) >> 1)
				}

				ec.Encode(int64(fl), int64(fl+fs), int64(ft))
			} else {
				/* Triangular pdf */
				var fl = 0
				var fm int
				fm = int(ec.Decode(int64(ft)))

				if fm < ((qn >> 1) * ((qn >> 1) + 1) >> 1) {
					itheta = (inlines.Isqrt32(int64(8*fm+1)) - 1) >> 1
					fs = itheta + 1
					fl = itheta * (itheta + 1) >> 1
				} else {
					itheta = (2*(qn+1) - inlines.Isqrt32(int64(8*(ft-fm-1)+1))) >> 1
					fs = qn + 1 - itheta
					fl = ft - ((qn + 1 - itheta) * (qn + 2 - itheta) >> 1)
				}

				ec.Dec_update(int64(fl), int64(fl+fs), int64(ft))
			}
		}
		inlines.OpusAssert(itheta >= 0)
		itheta = inlines.Celt_udiv(itheta*16384, qn)
		if encode != 0 && stereo != 0 {
			if itheta == 0 {
				intensity_stereo(m, X, X_ptr, Y, Y_ptr, bandE, i, N)
			} else {
				stereo_split(X, X_ptr, Y, Y_ptr, N)
			}
		}
	} else if stereo != 0 {
		if encode != 0 {
			inv = comm.BoolToInt(itheta > 8192)
			if inv != 0 {
				var j int
				for j = 0; j < N; j++ {
					Y[Y_ptr+j] = (0 - Y[Y_ptr+j])
				}
			}
			intensity_stereo(m, X, X_ptr, Y, Y_ptr, bandE, i, N)
		}
		if b.Val > 2<<BITRES && ctx.remaining_bits > 2<<BITRES {
			if encode != 0 {
				ec.Enc_bit_logp(inv, 2)
			} else {
				inv = ec.Dec_bit_logp(2)
			}
		} else {
			inv = 0
		}
		itheta = 0
	}
	qalloc = ec.Tell_frac() - tell
	b.Val -= qalloc

	if itheta == 0 {
		imid = 32767
		iside = 0
		fill.Val &= (1 << B) - 1
		delta = -16384
	} else if itheta == 16384 {
		imid = 0
		iside = 32767
		fill.Val &= ((1 << B) - 1) << B
		delta = 16384
	} else {
		imid = bitexact_cos(itheta)
		iside = bitexact_cos((16384 - itheta))
		/* This is the mid vs side allocation that minimizes squared error
		   in that band. */
		delta = inlines.FRAC_MUL16((N-1)<<7, bitexact_log2tan(iside, imid))
	}

	sctx.inv = inv
	sctx.imid = imid
	sctx.iside = iside
	sctx.delta = delta
	sctx.itheta = itheta
	sctx.qalloc = qalloc
}

func quant_band_n1(ctx *band_ctx, X []int, X_ptr int, Y []int, Y_ptr int, b int, lowband_out []int, lowband_out_ptr int) int {
	resynth := 0
	if ctx.encode == 0 {
		resynth = 1
	}
	stereo := 0
	if Y != nil {
		stereo = 1
	}
	encode := ctx.encode
	ec := ctx.ec

	x := X
	x_ptr := X_ptr
	c := 0
	for c < 1+stereo {
		sign := 0
		if ctx.remaining_bits >= 1<<BITRES {
			if encode != 0 {
				if x[x_ptr] < 0 {
					sign = 1
				}
				ec.Enc_bits(int64(sign), 1)
			} else {
				sign = ec.Dec_bits(1)
			}
			ctx.remaining_bits -= 1 << BITRES
			b -= 1 << BITRES
		}
		if resynth != 0 {
			if sign != 0 {
				x[x_ptr] = -CeltConstants.NORM_SCALING
			} else {
				x[x_ptr] = CeltConstants.NORM_SCALING
			}
		}
		x = Y
		x_ptr = Y_ptr
		c++
	}
	if lowband_out != nil {
		lowband_out[lowband_out_ptr] = inlines.SHR16Int(X[X_ptr], 4)
	}
	return 1
}

func quant_partition(ctx *band_ctx, X []int, X_ptr int, N int, b int, B int, lowband []int, lowband_ptr int, LM int, gain int, fill int) int {
	var cache_ptr int
	var q int
	var curr_bits int
	var imid int = 0
	var iside int = 0
	var B0 = B
	var mid = 0
	var side = 0
	var cm = 0
	var resynth = comm.BoolToInt(ctx.encode == 0)
	var Y = 0
	var encode int
	var m *CeltMode //porting note: pointer
	var i int
	var spread int
	var ec *comm.EntropyCoder //porting note: pointer

	encode = ctx.encode
	m = ctx.m
	i = ctx.i
	spread = ctx.spread
	ec = ctx.ec
	cache := m.cache.bits
	/* If we need 1.5 more bits than we can produce, split the band in two. */
	cache_ptr = int(m.cache.index[(LM+1)*m.nbEBands+i])
	if LM != -1 && b > int(cache[cache_ptr+int(cache[cache_ptr])])+12 && N > 2 {
		var mbits, sbits, delta int
		var itheta int
		var qalloc int
		sctx := split_ctx{}
		var next_lowband2 = 0
		var rebalance int

		N >>= 1
		Y = X_ptr + N
		LM -= 1
		if B == 1 {
			fill = (fill & 1) | (fill << 1)
		}

		B = (B + 1) >> 1

		boxed_b := &comm.BoxedValueInt{b}
		boxed_fill := &comm.BoxedValueInt{fill}
		compute_theta(ctx, &sctx, X, X_ptr, X, Y, N, boxed_b, B, B0, LM, 0, boxed_fill)
		b = boxed_b.Val
		fill = boxed_fill.Val

		imid = sctx.imid
		iside = sctx.iside
		delta = sctx.delta
		itheta = sctx.itheta
		qalloc = sctx.qalloc
		mid = (imid)
		side = (iside)

		/* Give more bits to low-energy MDCTs than they would otherwise deserve */
		if B0 > 1 && ((itheta & 0x3fff) != 0) {
			if itheta > 8192 /* Rough approximation for pre-echo masking */ {
				delta -= delta >> (4 - LM)
			} else /* Corresponds to a forward-masking slope of 1.5 dB per 10 ms */ {
				delta = inlines.IMIN(0, delta+(N<<BITRES>>(5-LM)))
			}
		}
		mbits = inlines.IMAX(0, inlines.IMIN(b, (b-delta)/2))
		sbits = b - mbits
		ctx.remaining_bits -= qalloc

		if lowband != nil {
			next_lowband2 = (lowband_ptr + N)
			/* >32-bit split case */
		}

		rebalance = ctx.remaining_bits
		if mbits >= sbits {
			cm = quant_partition(ctx, X, X_ptr, N, mbits, B,
				lowband, lowband_ptr, LM,
				inlines.MULT16_16_P15Int(gain, mid), fill)
			rebalance = mbits - (rebalance - ctx.remaining_bits)
			if rebalance > 3<<BITRES && itheta != 0 {
				sbits += rebalance - (3 << BITRES)
			}
			cm |= quant_partition(ctx, X, Y, N, sbits, B,
				lowband, next_lowband2, LM,
				inlines.MULT16_16_P15Int(gain, side), fill>>B) << (B0 >> 1)
		} else {
			cm = quant_partition(ctx, X, Y, N, sbits, B,
				lowband, next_lowband2, LM,
				inlines.MULT16_16_P15Int(gain, side), fill>>B) << (B0 >> 1)
			rebalance = sbits - (rebalance - ctx.remaining_bits)
			if rebalance > 3<<BITRES && itheta != 16384 {
				mbits += rebalance - (3 << BITRES)
			}
			cm |= quant_partition(ctx, X, X_ptr, N, mbits, B,
				lowband, lowband_ptr, LM,
				inlines.MULT16_16_P15Int(gain, mid), fill)
		}
	} else {
		/* This is the basic no-split case */
		q = bits2pulses(m, i, LM, b)
		curr_bits = pulses2bits(m, i, LM, q)
		ctx.remaining_bits -= curr_bits

		/* Ensures we can never bust the budget */
		for ctx.remaining_bits < 0 && q > 0 {
			ctx.remaining_bits += curr_bits
			q--
			curr_bits = pulses2bits(m, i, LM, q)
			ctx.remaining_bits -= curr_bits
		}

		if q != 0 {
			K := get_pulses(q)

			/* Finally do the actual quantization */
			if encode != 0 {
				cm = alg_quant(X, X_ptr, N, K, spread, B, ec)
			} else {
				cm = alg_unquant(X, X_ptr, N, K, spread, B, ec, gain)
			}
		} else {
			/* If there's no pulse, fill the band anyway */
			var j int

			if resynth != 0 {
				var cm_mask int
				/* B can be as large as 16, so this shift might overflow an int on a
				   16-bit platform; use a long to get defined behavior.*/
				cm_mask = (1 << B) - 1
				fill = fill & cm_mask

				if fill == 0 {
					arrayUtil.MemSetWithOffset(X, 0, X_ptr, N)
				} else {
					if lowband == nil {
						/* Noise */
						for j = 0; j < N; j++ {
							ctx.seed = celt_lcg_rand(ctx.seed)
							X[X_ptr+j] = int(int32(ctx.seed) >> 20)
						}
						cm = cm_mask
					} else {
						/* Folded spectrum */
						for j = 0; j < N; j++ {
							var tmp int
							ctx.seed = celt_lcg_rand(ctx.seed)

							/* About 48 dB below the "normal" folding level */
							tmp = int(math.Trunc(0.5 + (1.0/256)*float64(int32(1)<<(10))))
							if ((ctx.seed) & 0x8000) != 0 {
								tmp = tmp
							} else {
								tmp = 0 - tmp
							}

							X[X_ptr+j] = (lowband[lowband_ptr+j] + tmp)
						}
						cm = fill
					}

					renormalise_vector(X, X_ptr, N, gain)
				}
			}
		}
	}

	return cm
}

var bit_interleave_table = []byte{0, 1, 1, 1, 2, 3, 3, 3, 2, 3, 3, 3, 2, 3, 3, 3}

var bit_deinterleave_table = []int16{0x00, 0x03, 0x0C, 0x0F, 0x30, 0x33, 0x3C, 0x3F, 0xC0, 0xC3, 0xCC, 0xCF, 0xF0, 0xF3, 0xFC, 0xFF}

func quant_band(ctx *band_ctx, X []int, X_ptr int, N int, b int, B int, lowband []int, lowband_ptr int, LM int, lowband_out []int, lowband_out_ptr int, gain int, lowband_scratch []int, lowband_scratch_ptr int, fill int) int {
	var N0 = N
	var N_B = N
	var N_B0 int
	var B0 = B
	var time_divide = 0
	var recombine = 0
	var longBlocks int
	var cm = 0
	var resynth = comm.BoolToInt(ctx.encode == 0)
	var k int
	var encode int
	var tf_change int

	encode = ctx.encode
	tf_change = ctx.tf_change

	longBlocks = comm.BoolToInt(B0 == 1)

	N_B = inlines.Celt_udiv(N_B, B)

	/* Special case for one sample */
	if N == 1 {
		return quant_band_n1(ctx, X, X_ptr, nil, 0, b, lowband_out, lowband_out_ptr)
	}

	if tf_change > 0 {
		recombine = tf_change
	}
	/* Band recombining to increase frequency resolution */

	if lowband_scratch != nil && lowband != nil && (recombine != 0 || ((N_B&1) == 0 && tf_change < 0) || B0 > 1) {
		//System.arraycopy(lowband, lowband_ptr, lowband_scratch, lowband_scratch_ptr, N)
		copy(lowband_scratch[lowband_scratch_ptr:], lowband[lowband_ptr:lowband_ptr+N])
		lowband = lowband_scratch
		lowband_ptr = lowband_scratch_ptr
	}

	for k = 0; k < recombine; k++ {
		if encode != 0 {
			haar1(X, X_ptr, N>>k, 1<<k)
		}
		if lowband != nil {
			haar1(lowband, lowband_ptr, N>>k, 1<<k)
		}
		var idx1 = fill & 0xF
		var idx2 = fill >> 4
		if idx1 < 0 {
			if comm.Debug {
				fmt.Println("e")
			}
		}
		if idx2 < 0 {
			if comm.Debug {
				fmt.Println("e")
			}
		}
		fill = int(bit_interleave_table[fill&0xF] | bit_interleave_table[fill>>4]<<2)
	}
	B >>= recombine
	N_B <<= recombine

	/* Increasing the time resolution */
	for (N_B&1) == 0 && tf_change < 0 {
		if encode != 0 {
			haar1(X, X_ptr, N_B, B)
		}
		if lowband != nil {
			haar1(lowband, lowband_ptr, N_B, B)
		}
		fill |= fill << B
		B <<= 1
		N_B >>= 1
		time_divide++
		tf_change++
	}
	B0 = B
	N_B0 = N_B

	/* Reorganize the samples in time order instead of frequency order */
	if B0 > 1 {
		if encode != 0 {
			deinterleave_hadamard(X, X_ptr, N_B>>recombine, B0<<recombine, longBlocks)
		}
		if lowband != nil {
			deinterleave_hadamard(lowband, lowband_ptr, N_B>>recombine, B0<<recombine, longBlocks)
		}
	}

	cm = quant_partition(ctx, X, X_ptr, N, b, B, lowband, lowband_ptr, LM, gain, fill)

	/* This code is used by the decoder and by the resynthesis-enabled encoder */
	if resynth != 0 {
		/* Undo the sample reorganization going from time order to frequency order */
		if B0 > 1 {
			interleave_hadamard(X, X_ptr, N_B>>recombine, B0<<recombine, longBlocks)
		}

		/* Undo time-freq changes that we did earlier */
		N_B = N_B0
		B = B0
		for k = 0; k < time_divide; k++ {
			B >>= 1
			N_B <<= 1
			cm |= cm >> B
			haar1(X, X_ptr, N_B, B)
		}

		for k = 0; k < recombine; k++ {
			cm = int(bit_deinterleave_table[cm])
			haar1(X, X_ptr, N0>>k, 1<<k)
		}
		B <<= recombine

		/* Scale output for later folding */
		if lowband_out != nil {
			var j int
			var n int
			n = (inlines.Celt_sqrt(inlines.SHL32(N0, 22)))
			for j = 0; j < N0; j++ {
				lowband_out[lowband_out_ptr+j] = inlines.MULT16_16_Q15Int(n, X[X_ptr+j])
			}
		}

		cm = cm & ((1 << B) - 1)
	}
	return cm
}

func quant_band_stereo(ctx *band_ctx, X []int, X_ptr int, Y []int, Y_ptr int, N int, b int, B int, lowband []int, lowband_ptr int, LM int, lowband_out []int, lowband_out_ptr int, lowband_scratch []int, lowband_scratch_ptr int, fill int) int {

	imid := 0
	iside := 0
	inv := 0
	cm := 0
	resynth := 0
	if ctx.encode == 0 {
		resynth = 1
	}
	encode := ctx.encode
	ec := ctx.ec
	orig_fill := fill

	if N == 1 {
		return quant_band_n1(ctx, X, X_ptr, Y, Y_ptr, b, lowband_out, lowband_out_ptr)
	}

	boxed_b := &comm.BoxedValueInt{Val: b}
	boxed_fill := &comm.BoxedValueInt{Val: fill}
	sctx := &split_ctx{}

	compute_theta(ctx, sctx, X, X_ptr, Y, Y_ptr, N, boxed_b, B, B, LM, 1, boxed_fill)

	b = boxed_b.Val
	fill = boxed_fill.Val
	inv = sctx.inv
	imid = sctx.imid
	iside = sctx.iside
	delta := sctx.delta
	itheta := sctx.itheta
	qalloc := sctx.qalloc
	mid := imid
	side := iside

	if N == 2 {
		mbits := b
		sbits := 0
		if itheta != 0 && itheta != 16384 {
			sbits = 1 << BITRES
		}
		mbits -= sbits
		c := 0
		if itheta > 8192 {
			c = 1
		}
		ctx.remaining_bits -= qalloc + sbits

		x2 := X
		x2_ptr := X_ptr
		y2 := Y
		y2_ptr := Y_ptr
		if c != 0 {
			x2 = Y
			x2_ptr = Y_ptr
			y2 = X
			y2_ptr = X_ptr
		}

		sign := 0
		if sbits != 0 {
			if encode != 0 {
				if x2[x2_ptr]*y2[y2_ptr+1]-x2[x2_ptr+1]*y2[y2_ptr] < 0 {
					sign = 1
				}
				ec.Enc_bits(int64(sign), 1)
			} else {
				sign = ec.Dec_bits(1)
			}
		}
		sign = 1 - 2*sign
		cm = quant_band(ctx, x2, x2_ptr, N, mbits, B, lowband, lowband_ptr, LM, lowband_out, lowband_out_ptr, CeltConstants.Q15ONE, lowband_scratch, lowband_scratch_ptr, orig_fill)

		y2[y2_ptr] = -sign * x2[x2_ptr+1]
		y2[y2_ptr+1] = sign * x2[x2_ptr]

		if resynth != 0 {
			X[X_ptr] = inlines.MULT16_16_Q15Int(mid, X[X_ptr])
			X[X_ptr+1] = inlines.MULT16_16_Q15Int(mid, X[X_ptr+1])
			Y[Y_ptr] = inlines.MULT16_16_Q15Int(side, Y[Y_ptr])
			Y[Y_ptr+1] = inlines.MULT16_16_Q15Int(side, Y[Y_ptr+1])
			tmp := X[X_ptr]
			X[X_ptr] = tmp - Y[Y_ptr]
			Y[Y_ptr] = tmp + Y[Y_ptr]
			tmp = X[X_ptr+1]
			X[X_ptr+1] = tmp - Y[Y_ptr+1]
			Y[Y_ptr+1] = tmp + Y[Y_ptr+1]
		}
	} else {
		mbits := inlines.IMAX(0, inlines.IMIN(b, (b-delta)/2))
		sbits := b - mbits
		ctx.remaining_bits -= qalloc

		rebalance := ctx.remaining_bits
		if mbits >= sbits {

			cm = quant_band(ctx, X, X_ptr, N, mbits, B, lowband, lowband_ptr, LM, lowband_out, lowband_out_ptr, CeltConstants.Q15ONE, lowband_scratch, lowband_scratch_ptr, fill)
			rebalance = mbits - (rebalance - ctx.remaining_bits)
			if rebalance > 3<<BITRES && itheta != 0 {
				sbits += rebalance - (3 << BITRES)
			}
			cm |= quant_band(ctx, Y, Y_ptr, N, sbits, B, nil, 0, LM, nil, 0, side, nil, 0, fill>>B)
		} else {
			cm = quant_band(ctx, Y, Y_ptr, N, sbits, B, nil, 0, LM, nil, 0, side, nil, 0, fill>>B)
			rebalance = sbits - (rebalance - ctx.remaining_bits)
			if rebalance > 3<<BITRES && itheta != 16384 {
				mbits += rebalance - (3 << BITRES)
			}

			cm |= quant_band(ctx, X, X_ptr, N, mbits, B, lowband, lowband_ptr, LM, lowband_out, lowband_out_ptr, CeltConstants.Q15ONE, lowband_scratch, lowband_scratch_ptr, fill)
		}
	}

	if resynth != 0 && N != 2 {
		stereo_merge(X, X_ptr, Y, Y_ptr, mid, N)
	}
	if inv != 0 {
		for j := Y_ptr; j < Y_ptr+N; j++ {
			Y[j] = -Y[j]
		}
	}
	return cm
}

func quant_all_bands(encode int, m *CeltMode, start int, end int, X_ []int, Y_ []int, collapse_masks []int16, bandE [][]int, pulses []int, shortBlocks int, spread int, dual_stereo int, intensity int, tf_res []int, total_bits int, balance int, ec *comm.EntropyCoder, LM int, codedBands int, seed *comm.BoxedValueInt) {

	eBands := m.eBands
	M := 1 << LM
	B := 1
	if shortBlocks != 0 {
		B = M
	}
	var C = 1
	if Y_ != nil {
		C = 2
	}
	norm_offset := M * int(eBands[start])
	norm := make([]int, (C * (M*int(eBands[m.nbEBands-1]) - norm_offset)))
	norm2 := M*int(eBands[m.nbEBands-1]) - norm_offset
	lowband_scratch := X_
	lowband_scratch_ptr := M * int(eBands[m.nbEBands-1])
	lowband_offset := 0
	if Y_ != nil {
		C = 2
	}

	ctx := &band_ctx{
		encode:    encode,
		m:         m,
		intensity: intensity,
		spread:    spread,
		ec:        ec,
		bandE:     bandE,
		seed:      seed.Val,
	}
	resynth := 0
	if encode == 0 {
		resynth = 1
	}
	if comm.Debug {
		fmt.Printf("start :%d end:%d M:%d\r\n", start, end, M)
	}
	update_lowband := 1
	for i := start; i < end; i++ {
		ctx.i = i
		last := 0
		if i == end-1 {
			last = 1
		}

		X_ptr := M * int(eBands[i])
		X := X_
		Y := Y_
		Y_ptr := M * int(eBands[i])
		N := M*int(eBands[i+1]) - X_ptr
		tell := int(ec.Tell_frac())
		if i != start {
			balance -= tell
		}
		remaining_bits := total_bits - tell - 1
		ctx.remaining_bits = remaining_bits

		b := 0
		if i <= codedBands-1 {
			curr_balance := inlines.Celt_sudiv(balance, inlines.IMIN(3, codedBands-i))
			b = inlines.IMAX(0, inlines.IMIN(16383, inlines.IMIN(remaining_bits+1, pulses[i]+curr_balance)))
			if comm.Debug {
				//	fmt.Printf("curr_balance:%d b:%d\r\n", curr_balance, b)
			}
		}

		effective_lowband := -1
		var x_cm = int64(0)
		var y_cm = int64(0)

		if resynth != 0 && M*int(eBands[i])-N >= M*int(eBands[start]) && (update_lowband != 0 || lowband_offset == 0) {
			lowband_offset = i
		}
		tf_change := tf_res[i]
		ctx.tf_change = tf_change

		if i >= m.effEBands {

			X = norm
			X_ptr = 0
			if Y_ != nil {
				Y = norm
				Y_ptr = 0
			}
			lowband_scratch = nil
		}
		if i == end-1 {
			lowband_scratch = nil
		}

		if lowband_offset != 0 && (spread != Spread.SPREAD_AGGRESSIVE || B > 1 || tf_change < 0) {
			var fold_start int
			var fold_end int
			var fold_i int
			/* This ensures we never repeat spectral content within one band */
			effective_lowband = inlines.IMAX(0, M*int(eBands[lowband_offset])-norm_offset-N)

			fold_start = lowband_offset
			fold_start--
			for M*int(eBands[fold_start]) > effective_lowband+norm_offset {
				fold_start--
			}

			//while (M * eBands[--fold_start] > effective_lowband + norm_offset) ;
			fold_end = lowband_offset - 1
			fold_end++
			for M*int(eBands[fold_end]) < effective_lowband+norm_offset+N {
				fold_end++
			}
			//while (M * eBands[++fold_end] < effective_lowband + norm_offset + N) ;

			x_cm = 0
			y_cm = 0
			fold_i = fold_start

			for {
				x_cm |= int64(collapse_masks[fold_i*C+0])
				y_cm |= int64(collapse_masks[fold_i*C+C-1])
				fold_i++
				if fold_i < fold_end {
					continue
				}
				break
			}
		} else {
			x_cm = ((1 << B) - 1)
			y_cm = ((1 << B) - 1)
		}

		if dual_stereo != 0 && i == intensity {
			dual_stereo = 0
			if resynth != 0 {
				for j := 0; j < M*int(eBands[i])-norm_offset; j++ {
					norm[j] = inlines.HALF32(norm[j] + norm[norm2+j])
				}
			}
		}

		if dual_stereo != 0 {
			var lowband []int
			var lowband_out []int
			if effective_lowband != -1 {
				lowband = norm
			}
			if last == 0 {
				lowband_out = norm
			}

			x_cm = int64(quant_band(ctx, X, X_ptr, N, b/2, B, lowband, effective_lowband, LM, lowband_out, M*int(eBands[i])-norm_offset, CeltConstants.Q15ONE, lowband_scratch, lowband_scratch_ptr, int(x_cm)))

			y_cm = int64(quant_band(ctx, Y, Y_ptr, N, b/2, B, lowband, norm2+effective_lowband, LM, lowband_out, norm2+(M*int(eBands[i])-norm_offset), CeltConstants.Q15ONE, lowband_scratch, lowband_scratch_ptr, int(y_cm)))

		} else {
			var lowband []int
			var lowband_out []int
			if effective_lowband != -1 {
				lowband = norm
			}
			if last == 0 {
				lowband_out = norm
			}
			if Y != nil {

				x_cm = int64(quant_band_stereo(ctx, X, X_ptr, Y, Y_ptr, N,
					b,
					B,
					lowband,
					effective_lowband,
					LM,
					lowband_out,
					M*int(eBands[i])-norm_offset,
					lowband_scratch,
					lowband_scratch_ptr,
					int(x_cm|y_cm)))

				Xstr, _ := json.Marshal(X)
				if comm.Debug && i == 20 {
					fmt.Printf("quant_all_bands effective_lowband:%d last:%d i:%d X:%s\r\n", effective_lowband, last, i, Xstr)
				}
			} else {

				x_cm = int64(quant_band(
					ctx,
					X,
					X_ptr,
					N,
					b,
					B,
					lowband,
					effective_lowband,
					LM,
					lowband_out,
					M*int(eBands[i])-norm_offset,
					CeltConstants.Q15ONE,
					lowband_scratch,
					lowband_scratch_ptr,
					int(x_cm|y_cm))) // opt: lots of pointers are created here too

			}
			y_cm = x_cm
		}
		collapse_masks[i*C+0] = int16(x_cm)
		collapse_masks[i*C+C-1] = int16(y_cm)
		balance += pulses[i] + tell
		if b > N<<BITRES {
			update_lowband = 1
		} else {
			update_lowband = 0
		}
	}
	seed.Val = ctx.seed
}
