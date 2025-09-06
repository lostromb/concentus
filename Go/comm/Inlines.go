package comm

import (
	"math"
)

type Inlines struct{}

var inlines = Inlines{}

func (i *Inlines) OpusAssert(condition bool) {
	if !condition {
		panic("assertion failed")
	}
}

func (i *Inlines) OpusAssertMsg(condition bool, message string) {
	if !condition {
		panic(message)
	}
}

func (i *Inlines) CapToUint(val int) int {
	return int(uint(val))
}

func (i *Inlines) CapToUintLong(val int64) int64 {
	return int64(int64(val))
}

func (i *Inlines) MULT16_16SU(a, b int) int {
	return (int(int16(a)) * (int)(b&0xFFFF))
}

func (i *Inlines) MULT16_32_Q16(a int16, b int) int {
	return i.ADD32(i.MULT16_16(int(a), i.SHR(b, 16)), i.SHR(i.MULT16_16SU(int(a), b&0x0000ffff), 16))
}

func (i *Inlines) MULT16_32_Q16Int(a, b int) int {
	return i.ADD32(i.MULT16_16(int(a), i.SHR(b, 16)), i.SHR(i.MULT16_16SU(a, b&0x0000ffff), 16))
}

func (i *Inlines) MULT16_32_P16(a int16, b int) int {
	return i.ADD32(i.MULT16_16(int(a), i.SHR(b, 16)), i.PSHR(i.MULT16_16SU(int(a), b&0x0000ffff), 16))
}

func (i *Inlines) MULT16_32_P16Int(a, b int) int {
	return i.ADD32(i.MULT16_16(int(a), i.SHR(b, 16)), i.PSHR(i.MULT16_16SU(a, b&0x0000ffff), 16))
}

func (i *Inlines) MULT16_32_Q15(a int16, b int) int {
	return (int(a) * (b >> 16) << 1) + (int(a)*(b&0xFFFF))>>15
}

func (i *Inlines) MULT16_32_Q15Int(a, b int) int {
	return ((a * (b >> 16)) << 1) + ((a * (b & 0xFFFF)) >> 15)
}

func (i *Inlines) MULT32_32_Q31(a, b int) int {
	return i.ADD32(i.ADD32(i.SHL(i.MULT16_16(i.SHR(a, 16), i.SHR(b, 16)), 1), i.SHR(inlines.MULT16_16SU(inlines.SHR(a, 16), b&0x0000ffff), 15)), inlines.SHR(inlines.MULT16_16SU(inlines.SHR(b, 16), a&0x0000ffff), 15))
}

func (i *Inlines) QCONST16(x float64, bits int) int16 {
	return int16(0.5 + x*float64(int(1<<bits)))
}

func (i *Inlines) QCONST32(x float64, bits int) int {
	return int(0.5 + x*float64(int(1<<bits)))
}

func (i *Inlines) NEG16(x int16) int16 {
	return -x
}

func (i *Inlines) NEG16Int(x int) int {
	return -x
}

func (i *Inlines) NEG32(x int) int {
	return -x
}

func (i *Inlines) EXTRACT16(x int) int16 {
	return int16(x)
}

func (i *Inlines) EXTEND32(x int16) int {
	return int(x)
}

func (i *Inlines) EXTEND32Int(x int) int {
	return x
}

func (i *Inlines) SHR16(a int16, shift int) int16 {
	return a >> shift
}

func (i *Inlines) SHR16Int(a, shift int) int {
	return a >> shift
}

func (i *Inlines) SHL16(a int16, shift int) int16 {
	return a << shift
}

func (inlines *Inlines) SHL16Int(a, shift int) int {
	return a << shift
}

func (inlines *Inlines) SHR32(a, shift int) int {
	return a >> shift
}

func (inlines *Inlines) SHR321(a, shift int32) int32 {
	return a >> shift
}

func (inlines *Inlines) SHL32(a, shift int) int {
	return a << shift
}

func (inlines *Inlines) PSHR32(a, shift int) int {
	return inlines.SHR32(a+(inlines.EXTEND32(1)<<shift>>1), shift)
}

func (inlines *Inlines) PSHR16(a int16, shift int) int16 {
	return inlines.SHR16(int16(a+(1<<shift>>1)), shift)
}

func (inlines *Inlines) PSHR16Int(a, shift int) int {
	return inlines.SHR32(a+(1<<shift>>1), shift)
}

func (inlines *Inlines) VSHR32(a, shift int) int {
	if shift > 0 {
		return inlines.SHR32(a, shift)
	}
	return inlines.SHL32(a, -shift)
}

func (inlines *Inlines) SHR(a, shift int) int {
	return a >> shift
}

func (inlines *Inlines) SHL(a, shift int) int {
	return a << shift
}

func (inlines *Inlines) PSHR(a, shift int) int {
	return inlines.SHR(a+(inlines.EXTEND32(1)<<shift>>1), shift)
}

func (inlines *Inlines) SATURATE(x, a int) int {
	if x > a {
		return a
	} else if x < -a {
		return -a
	}
	return x
}

func (inlines *Inlines) SATURATE16(x int) int16 {
	if x > 32767 {
		return 32767
	} else if x < -32768 {
		return -32768
	}
	return int16(x)
}

func (inlines *Inlines) ROUND16(x int16, a int16) int16 {
	return inlines.EXTRACT16(inlines.PSHR32(int(x), int(a)))
}

func (inlines *Inlines) ROUND16Int(x, a int) int {
	return inlines.PSHR32(x, a)
}

func (inlines *Inlines) PDIV32(a, b int) int {
	return a / b
}

func (inlines *Inlines) HALF16(x int16) int16 {
	return inlines.SHR16(x, 1)
}

func (inlines *Inlines) HALF16Int(x int) int {
	return inlines.SHR32(x, 1)
}

func (inlines *Inlines) HALF32(x int) int {
	return inlines.SHR32(x, 1)
}

func (inlines *Inlines) ADD16(a, b int16) int16 {
	return a + b
}

func (inlines *Inlines) ADD16Int(a, b int) int {
	return a + b
}

func (inlines *Inlines) SUB16(a, b int16) int16 {
	return a - b
}

func (inlines *Inlines) SUB16Int(a, b int) int {
	return a - b
}

func (inlines *Inlines) ADD32(a, b int) int {
	return a + b
}

func (inlines *Inlines) SUB32(a, b int) int {
	return a - b
}

func (inlines *Inlines) MULT16_16_16(a, b int16) int16 {
	return int16(int16(a) * int16(b))
}

func (inlines *Inlines) MULT16_16_16Int(a, b int) int {
	return a * b
}

func (inlines *Inlines) MULT16_16(a, b int) int {
	return int(a) * int(b)
}

func (inlines *Inlines) MULT16_16Short(a, b int16) int {
	return int(a) * int(b)
}

func (inlines *Inlines) MAC16_16(c int16, a, b int16) int {
	return int(c) + int(a)*int(b)
}

func (inlines *Inlines) MAC16_16Int(c int, a, b int16) int {
	return c + int(a)*int(b)
}

func (inlines *Inlines) MAC16_16IntAll(c, a, b int) int {
	return c + a*b
}

func (inlines *Inlines) MAC16_32_Q15(c int, a int16, b int16) int {
	return inlines.ADD32(c, inlines.ADD32(inlines.MULT16_16(int(a), inlines.SHR(int(b), 15)), inlines.SHR(inlines.MULT16_16(int(a), int(b&0x00007fff)), 15)))
}

func (inlines *Inlines) MAC16_32_Q15Int(c, a, b int) int {
	return inlines.ADD32(c, inlines.ADD32(inlines.MULT16_16(int(a), inlines.SHR(b, 15)), inlines.SHR(inlines.MULT16_16(int(a), b&0x00007fff), 15)))
}

func (inlines *Inlines) MAC16_32_Q16(c int, a int16, b int16) int {
	return inlines.ADD32(c, inlines.ADD32(inlines.MULT16_16(int(a), inlines.SHR(int(b), 16)), inlines.SHR(inlines.MULT16_16SU(int(a), int(int(b)&0x0000ffff)), 16)))
}

func (inlines *Inlines) MAC16_32_Q16Int(c, a, b int) int {
	return inlines.ADD32(c, inlines.ADD32(inlines.MULT16_16(int(a), inlines.SHR(b, 16)), inlines.SHR(inlines.MULT16_16SU(a, b&0x0000ffff), 16)))
}

func (inlines *Inlines) MULT16_16_Q11_32(a, b int16) int {
	return inlines.SHR(inlines.MULT16_16Short(a, b), 11)
}

func (inlines *Inlines) MULT16_16_Q11_32Int(a, b int) int {
	return inlines.SHR(inlines.MULT16_16(int(a), int(b)), 11)
}

func (inlines *Inlines) MULT16_16_Q11(a, b int16) int16 {
	return int16(inlines.SHR(inlines.MULT16_16Short(a, b), 11))
}

func (inlines *Inlines) MULT16_16_Q11Int(a, b int) int {
	return inlines.SHR(inlines.MULT16_16(int(a), int(b)), 11)
}

func (inlines *Inlines) MULT16_16_Q13(a, b int16) int16 {
	return int16(inlines.SHR(inlines.MULT16_16Short(a, b), 13))
}

func (inlines *Inlines) MULT16_16_Q13Int(a, b int) int {
	return inlines.SHR(inlines.MULT16_16(a, b), 13)
}

func (inlines *Inlines) MULT16_16_Q14(a, b int16) int16 {
	return int16(inlines.SHR(inlines.MULT16_16(int(a), int(b)), 14))
}

func (inlines *Inlines) MULT16_16_Q14Int(a, b int) int {
	return inlines.SHR(inlines.MULT16_16(a, b), 14)
}

func (inlines *Inlines) MULT16_16_Q15(a, b int16) int16 {
	return int16(inlines.SHR(inlines.MULT16_16(int(a), int(b)), 15))
}

func (inlines *Inlines) MULT16_16_Q15Int(a, b int) int {
	return inlines.SHR(inlines.MULT16_16(a, b), 15)
}

func (inlines *Inlines) MULT16_16_P13(a, b int16) int16 {
	return int16(inlines.SHR(inlines.ADD32(4096, inlines.MULT16_16Short(a, b)), 13))
}

func (inlines *Inlines) MULT16_16_P13Int(a, b int) int {
	return inlines.SHR(inlines.ADD32(4096, inlines.MULT16_16(int(a), int(b))), 13)
}

func (inlines *Inlines) MULT16_16_P14(a, b int16) int16 {
	return int16(inlines.SHR(inlines.ADD32(8192, inlines.MULT16_16Short(a, b)), 14))
}

func (inlines *Inlines) MULT16_16_P14Int(a, b int) int {
	return inlines.SHR(inlines.ADD32(8192, inlines.MULT16_16(a, b)), 14)
}

func (inlines *Inlines) MULT16_16_P15(a, b int16) int16 {
	return int16(inlines.SHR(inlines.ADD32(16384, inlines.MULT16_16Short(a, b)), 15))
}

func (inlines *Inlines) MULT16_16_P15Int(a, b int) int {
	return inlines.SHR(inlines.ADD32(16384, inlines.MULT16_16(a, b)), 15)
}

func (inlines *Inlines) DIV32_16(a int, b int16) int16 {
	return int16(a / int(b))
}

func (inlines *Inlines) DIV32_16Int(a, b int) int {
	return a / b
}

func (inlines *Inlines) DIV32(a, b int) int {
	return a / b
}

func (inlines *Inlines) SAT16(x int) int16 {
	if x > 32767 {
		return 32767
	} else if x < -32768 {
		return -32768
	}
	return int16(x)
}

func (inlines *Inlines) SIG2WORD16(x int) int16 {
	x = inlines.PSHR32(x, 12)
	if x < -32768 {
		x = -32768
	} else if x > 32767 {
		x = 32767
	}
	return inlines.EXTRACT16(x)
}

func (inlines *Inlines) MIN(a, b int16) int16 {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) MAX(a, b int16) int16 {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) MIN16(a, b int16) int16 {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) MAX16(a, b int16) int16 {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) MIN16Int(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) MAX16Int(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) MIN16Float(a, b float32) float32 {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) MAX16Float(a, b float32) float32 {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) MINInt(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) MAXInt(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) IMIN(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) IMINLong(a, b int64) int64 {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) IMAX(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) MIN32(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) MAX32(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) MIN32Float(a, b float32) float32 {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) MAX32Float(a, b float32) float32 {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) ABS16(x int) int {
	if x < 0 {
		return -x
	}
	return x
}

func (inlines *Inlines) ABS16Float(x float32) float32 {
	if x < 0 {
		return -x
	}
	return x
}

func (inlines *Inlines) ABS16Short(x int16) int16 {
	if x < 0 {
		return -x
	}
	return x
}

func (inlines *Inlines) ABS32(x int) int {
	if x < 0 {
		return -x
	}
	return x
}

func (inlines *Inlines) Celt_udiv(n, d int) int {
	inlines.OpusAssert(d > 0)
	return n / d
}

func (inlines *Inlines) Celt_sudiv(n, d int) int {
	inlines.OpusAssert(d > 0)
	return n / d
}

func (inlines *Inlines) Celt_div(a, b int) int {
	return inlines.MULT32_32_Q31(a, inlines.Celt_rcp(b))
}

func (inlines *Inlines) Celt_ilog2(x int) int {
	inlines.OpusAssertMsg(x > 0, "celt_ilog2() only defined for strictly positive numbers")
	return inlines.EC_ILOG(int64(x)) - 1
}

func (inlines *Inlines) Celt_zlog2(x int) int {
	if x <= 0 {
		return 0
	}
	return inlines.Celt_ilog2(x)
}

func (inlines *Inlines) Celt_maxabs16(x []int, x_ptr, len int) int {
	maxval := 0
	minval := 0
	for i := x_ptr; i < len+x_ptr; i++ {
		maxval = inlines.MAX32(maxval, x[i])
		minval = inlines.MIN32(minval, x[i])
	}
	return inlines.MAX32(inlines.EXTEND32Int(maxval), -inlines.EXTEND32Int(minval))
}

func (inlines *Inlines) Celt_maxabs32(x []int, x_ptr, len int) int {
	maxval := 0
	minval := 0
	for i := x_ptr; i < x_ptr+len; i++ {
		maxval = inlines.MAX32(maxval, x[i])
		minval = inlines.MIN32(minval, x[i])
	}
	return inlines.MAX32(maxval, -minval)
}

func (inlines *Inlines) Celt_maxabs32Short(x []int16, x_ptr, len int) int16 {
	maxval := int16(0)
	minval := int16(0)
	for i := x_ptr; i < x_ptr+len; i++ {
		maxval = inlines.MAX16(maxval, x[i])
		minval = inlines.MIN16(minval, x[i])
	}
	if maxval > -minval {
		return maxval
	}
	return -minval
}

func (inlines *Inlines) FRAC_MUL16(a, b int) int {
	return (16384 + int(int32(a)*int32(b))) >> 15
}

func (inlines *Inlines) Isqrt32(_val int64) int {
	g := 0
	bshift := (inlines.EC_ILOG(_val) - 1) >> 1
	b := 1 << bshift
	for bshift >= 0 {
		t := int64(((g << 1) + b) << bshift)
		if t <= _val {
			g += b
			_val -= t
		}
		b >>= 1
		bshift--
	}
	return g
}

var sqrt_C = []int16{23175, 11561, -3011, 1699, -664}

func (inlines *Inlines) Celt_sqrt(x int) int {
	if x == 0 {
		return 0
	} else if x >= 1073741824 {
		return 32767
	}
	k := (inlines.Celt_ilog2(x) >> 1) - 7
	x = inlines.VSHR32(x, 2*k)
	n := int16(x - 32768)
	rt := inlines.ADD16(sqrt_C[0], inlines.MULT16_16_Q15(n, inlines.ADD16(sqrt_C[1], inlines.MULT16_16_Q15(n, inlines.ADD16(sqrt_C[2],
		inlines.MULT16_16_Q15(n, inlines.ADD16(sqrt_C[3], inlines.MULT16_16_Q15(n, sqrt_C[4]))))))))
	rt = int16(inlines.VSHR32(int(rt), 7-k))
	return int(rt)
}

func (inlines *Inlines) Celt_rcp(x int) int {
	inlines.OpusAssertMsg(x > 0, "celt_rcp() only defined for positive values")
	i := inlines.Celt_ilog2(x)
	n := inlines.VSHR32(x, i-15) - 32768
	r := inlines.ADD16Int(30840, inlines.MULT16_16_Q15Int(-15420, int(n)))
	r = inlines.SUB16Int(r, inlines.MULT16_16_Q15Int(r,
		inlines.ADD16Int(inlines.MULT16_16_Q15Int(r, int(n)), inlines.ADD16Int(r, -32768))))
	r = inlines.SUB16Int(r, inlines.ADD16Int(1, inlines.MULT16_16_Q15Int(r,
		inlines.ADD16Int(inlines.MULT16_16_Q15Int(r, int(n)), inlines.ADD16Int(r, -32768)))))
	return inlines.VSHR32(inlines.EXTEND32Int(r), i-16)
}

func (inlines *Inlines) Celt_rsqrt_norm(x int) int {
	n := x - 32768
	r := inlines.ADD16Int(23557, inlines.MULT16_16_Q15Int(int(n), inlines.ADD16Int(-13490, inlines.MULT16_16_Q15Int(int(n), 6713))))
	r2 := inlines.MULT16_16_Q15Int(r, r)

	y := inlines.SHL16Int(inlines.SUB16Int(inlines.ADD16Int(inlines.MULT16_16_Q15Int(r2, n), r2), 16384), 1)
	return inlines.ADD16Int(r, inlines.MULT16_16_Q15Int(r, inlines.MULT16_16_Q15Int(int(y),
		inlines.SUB16Int(inlines.MULT16_16_Q15Int(int(y), 12288), 16384))))
}

func (inlines *Inlines) Frac_div32(a, b int) int {
	shift := inlines.Celt_ilog2(b) - 29
	a = inlines.VSHR32(a, shift)
	b = inlines.VSHR32(b, shift)
	rcp := inlines.ROUND16Int(inlines.Celt_rcp(inlines.ROUND16Int(b, 16)), 3)
	result := inlines.MULT16_32_Q15Int(rcp, a)
	rem := inlines.PSHR32(a, 2) - inlines.MULT32_32_Q31(result, b)
	result = inlines.ADD32(result, inlines.SHL32(inlines.MULT16_32_Q15Int(rcp, rem), 2))
	if result >= 536870912 {
		return 2147483647
	} else if result <= -536870912 {
		return -2147483647
	}
	return inlines.SHL32(result, 2)
}

var log2_C0 = -6801 + (1 << 3)

func (inlines *Inlines) Celt_log2(x int) int {
	if x == 0 {
		return -32767
	}
	i := inlines.Celt_ilog2(x)
	n := inlines.VSHR32(x, i-15) - 32768 - 16384
	frac := inlines.ADD16Int(log2_C0, inlines.MULT16_16_Q15Int(int(n), inlines.ADD16Int(15746, inlines.MULT16_16_Q15Int(int(n), inlines.ADD16Int(-5217, inlines.MULT16_16_Q15Int(int(n), inlines.ADD16Int(2545, inlines.MULT16_16_Q15Int(int(n), -1401))))))))
	return inlines.SHL16Int(int(i-13), 10) + inlines.SHR16Int(int(frac), 4)
}

func (inlines *Inlines) Celt_exp2_frac(x int) int {
	frac := inlines.SHL16Int(int(x), 4)
	return inlines.ADD16Int(16383, inlines.MULT16_16_Q15Int(frac, inlines.ADD16Int(22804, inlines.MULT16_16_Q15Int(frac, inlines.ADD16Int(14819, inlines.MULT16_16_Q15Int(10204, int(frac)))))))
}

func (inlines *Inlines) Celt_exp2(inLog_Q7 int) int {
	integer := inlines.SHR16Int((inLog_Q7), 10)
	if integer > 14 {
		return 0x7f000000
	} else if integer < -15 {
		return 0
	}
	frac := inlines.Celt_exp2_frac(int(inLog_Q7 - inlines.SHL16Int(integer, 10)))
	return inlines.VSHR32(inlines.EXTEND32Int(frac), -int(integer)-2)
}

func (inlines *Inlines) celt_atan01(x int) int {
	return inlines.MULT16_16_P15Int(int(x), inlines.ADD32(32767, inlines.MULT16_16_P15Int(int(x), inlines.ADD32(-21, inlines.MULT16_16_P15Int(int(x), inlines.ADD32(-11943, inlines.MULT16_16_P15Int(4936, int(x))))))))
}

func (inlines *Inlines) Celt_atan2p(y, x int) int {
	if y < x {
		arg := inlines.Celt_div(inlines.SHL32(inlines.EXTEND32Int(y), 15), x)
		if arg >= 32767 {
			arg = 32767
		}
		return inlines.SHR32(inlines.celt_atan01(int(inlines.EXTRACT16(arg))), 1)
	}
	arg := inlines.Celt_div(inlines.SHL32(inlines.EXTEND32Int(x), 15), y)
	if arg >= 32767 {
		arg = 32767
	}
	return 25736 - inlines.SHR16Int(inlines.celt_atan01(int(inlines.EXTRACT16(arg))), 1)
}

func (inlines *Inlines) Celt_cos_norm(x int) int {
	x = x & 0x0001ffff
	if x > inlines.SHL32(inlines.EXTEND32(1), 16) {
		x = inlines.SUB32(inlines.SHL32(inlines.EXTEND32(1), 17), x)
	}
	if (x & 0x00007fff) != 0 {
		if x < inlines.SHL32(inlines.EXTEND32(1), 15) {
			return inlines._celt_cos_pi_2(int(inlines.EXTRACT16(x)))
		}
		return inlines.NEG32(inlines._celt_cos_pi_2(int(inlines.EXTRACT16(65536 - x))))
	} else if (x & 0x0000ffff) != 0 {
		return 0
	} else if (x & 0x0001ffff) != 0 {
		return -32767
	}
	return 32767
}

func (inlines *Inlines) _celt_cos_pi_2(x int) int {
	x2 := inlines.MULT16_16_P15Int(int(x), int(x))
	return inlines.ADD32(1, inlines.MIN32(32766, inlines.ADD32(inlines.SUB16Int(32767, x2), inlines.MULT16_16_P15Int(int(x2), inlines.ADD32(-7651, inlines.MULT16_16_P15Int(int(x2), inlines.ADD32(8277, inlines.MULT16_16_P15Int(-626, int(x2)))))))))
}

func (inlines *Inlines) Silk_ROR32(a32, rot int) int {
	if rot == 0 {
		return a32
	} else if rot < 0 {
		m := -rot
		return (a32 << m) | (a32 >> (32 - m))
	}
	return (a32 << (32 - rot)) | (a32 >> rot)
}

func (inlines *Inlines) Silk_MUL(a32, b32 int) int {
	return a32 * b32
}

/*
	func silk_MLA(a32, b32, c32 int) int {
		ret := a32 + b32*c32
		OpusAssert(int64(ret) == int64(a32)+int64(b32)*int64(c32))
		return ret
	}
*/
func (inlines *Inlines) Silk_MLA(a32, b32, c32 int) int {
	ret := inlines.Silk_ADD32((a32), ((b32) * (c32)))
	inlines.OpusAssert(int64(ret) == int64(a32)+int64(b32)*int64(c32))
	return ret
}

func (inlines *Inlines) Silk_SMULTT(a32, b32 int) int {
	return (a32 >> 16) * (b32 >> 16)
}

func (inlines *Inlines) Silk_SMLATT(a32, b32, c32 int) int {
	return a32 + ((b32 >> 16) * (c32 >> 16))
}

func (inlines *Inlines) Silk_SMLALBB(a64 int64, b16, c16 int16) int64 {
	return a64 + int64(b16)*int64(c16)
}

func (inlines *Inlines) Silk_SMULL(a32, b32 int) int64 {
	return int64(a32) * int64(b32)
}

func (inlines *Inlines) Silk_ADD32_ovflw(a, b int32) int32 {
	return int32(int64(a) + int64(b))
}

func (inlines *Inlines) Silk_ADD32_ovflwLong(a, b int64) int {
	return int(a + b)
}

func (inlines *Inlines) Silk_SUB32_ovflw(a, b int) int {
	return int(a - b)
}

func (inlines *Inlines) Silk_MLA_ovflw(a32, b32, c32 int32) int32 {
	return inlines.Silk_ADD32_ovflw(a32, b32*c32)
}

func (inlines *Inlines) Silk_SMLABB_ovflw(a32, b32, c32 int32) int32 {
	return inlines.Silk_ADD32_ovflw(a32, int32(b32)*int32(c32))
}

func (inlines *Inlines) Silk_SMULBB(a32, b32 int) int {
	return int(int(int16(a32)) * int(int16(b32)))
}
func (inlines *Inlines) Silk_SMULWB(a32, b32 int) int {
	return int(int32(int64(a32) * int64(int16(b32)) >> 16))
}

func (inlines *Inlines) Silk_SMLABB(a32, b32, c32 int) int {
	return ((a32) + int(int32(int16(b32))*int32(int16(c32))))
}

func (inlines *Inlines) Silk_DIV32_16(a32, b32 int) int {
	return a32 / b32
}

func (inlines *Inlines) Silk_DIV32(a32, b32 int) int {
	return a32 / b32
}

func (inlines *Inlines) Silk_ADD16(a, b int16) int16 {
	return a + b
}

func (inlines *Inlines) Silk_ADD32(a, b int) int {
	return a + b
}

func (inlines *Inlines) Silk_ADD64(a, b int64) int64 {
	return a + b
}

func (inlines *Inlines) Silk_SUB16(a, b int16) int16 {
	return a - b
}

func (inlines *Inlines) Silk_SUB32(a, b int) int {
	return a - b
}

func (inlines *Inlines) Silk_SUB64(a, b int64) int64 {
	return a - b
}

func (inlines *Inlines) Silk_SAT8(a int) int {
	if a > 127 {
		return 127
	} else if a < -128 {
		return -128
	}
	return a
}

func (inlines *Inlines) Silk_SAT16(a int) int {
	if a > math.MaxInt16 {
		return math.MaxInt16
	}
	if (a) < math.MinInt16 {
		return math.MinInt16
	}
	return a
}

func (inlines *Inlines) Silk_SAT32(a int64) int {
	if a > 2147483647 {
		return 2147483647
	} else if a < -2147483648 {
		return -2147483648
	}
	return int(a)
}

func (inlines *Inlines) Silk_ADD_SAT16(a16, b16 int16) int16 {
	res := int16(inlines.Silk_SAT16(int(a16) + int(b16)))
	inlines.OpusAssert(res == int16(inlines.Silk_SAT16(int(a16)+int(b16))))
	return res
}

func (inlines *Inlines) Silk_ADD_SAT32(a32, b32 int) int {
	sum := int64(a32) + int64(b32)
	if sum > 2147483647 {
		return 2147483647
	} else if sum < -2147483648 {
		return -2147483648
	}
	return int(sum)
}

func (inlines *Inlines) Silk_ADD_SAT64(a64, b64 int64) int64 {
	if (a64 > 0 && b64 > math.MaxInt64-a64) || (a64 < 0 && b64 < math.MinInt64-a64) {
		if a64 > 0 {
			return math.MaxInt64
		}
		return math.MinInt64
	}
	return a64 + b64
}

func (inlines *Inlines) Silk_SUB_SAT16(a16, b16 int16) int16 {
	res := int16(inlines.Silk_SAT16(int(a16) - int(b16)))
	inlines.OpusAssert(res == int16(inlines.Silk_SAT16(int(a16)-int(b16))))
	return res
}

func (inlines *Inlines) Silk_SUB_SAT32(a32, b32 int) int {
	diff := int64(a32) - int64(b32)
	if diff > 2147483647 {
		return 2147483647
	} else if diff < -2147483648 {
		return -2147483648
	}
	return int(diff)
}

func (inlines *Inlines) Silk_SUB_SAT64(a64, b64 int64) int64 {
	if (b64 > 0 && a64 < math.MinInt64+b64) || (b64 < 0 && a64 > math.MaxInt64+b64) {
		if b64 > 0 {
			return math.MinInt64
		}
		return math.MaxInt64
	}
	return a64 - b64
}

func (inlines *Inlines) Silk_ADD_POS_SAT8(a, b byte) byte {
	sum := int(a) + int(b)
	if sum > 127 {
		return 127
	}
	return byte(sum)
}

func (inlines *Inlines) Silk_ADD_POS_SAT16(a, b int16) int16 {
	sum := int(a) + int(b)
	if sum > 32767 {
		return 32767
	}
	return int16(sum)
}

func (inlines *Inlines) Silk_ADD_POS_SAT32(a, b int) int {
	sum := int64(a) + int64(b)
	if sum > 2147483647 {
		return 2147483647
	}
	return int(sum)
}

func (inlines *Inlines) Silk_ADD_POS_SAT64(a, b int64) int64 {
	if a > math.MaxInt64-b {
		return math.MaxInt64
	}
	return a + b
}

func (inlines *Inlines) Silk_LSHIFT8(a byte, shift int) byte {
	return a << shift
}

func (inlines *Inlines) Silk_LSHIFT16(a int16, shift int) int16 {
	return a << shift
}

func (inlines *Inlines) Silk_LSHIFT32(a, shift int) int {
	return a << shift
}

func (inlines *Inlines) Silk_LSHIFT32_32(a, shift int) int32 {
	return int32(a << shift)
}

func (inlines *Inlines) Silk_LSHIFT64(a int64, shift int) int64 {
	return a << shift
}

func (inlines *Inlines) Silk_LSHIFT(a, shift int) int {
	return a << shift
}

func silk_LSHIFT_ovflw(a, shift int) int {
	return a << shift
}

func (inlines *Inlines) Silk_LSHIFT_SAT32(a, shift int) int {
	return (inlines.Silk_LSHIFT32(inlines.Silk_LIMIT((a), inlines.Silk_RSHIFT32(math.MinInt32, (shift)), inlines.Silk_RSHIFT32(math.MaxInt32, (shift))), (shift)))
}

func (inlines *Inlines) Silk_RSHIFT8(a byte, shift int) byte {
	return a >> shift
}

func (inlines *Inlines) Silk_RSHIFT16(a int16, shift int) int16 {
	return a >> shift
}

func (inlines *Inlines) Silk_RSHIFT32(a, shift int) int {
	return a >> shift
}

func (inlines *Inlines) Silk_RSHIFT(a, shift int) int {
	return a >> shift
}

func (inlines *Inlines) Silk_RSHIFT64(a int64, shift int) int64 {
	return a >> shift
}

func (inlines *Inlines) Silk_RSHIFT_uint(a int64, shift int) int64 {
	return int64(inlines.CapToUInt32(a) >> shift)
}

func (inlines *Inlines) Silk_ADD_LSHIFT(a, b, shift int) int {
	return a + (b << shift)
}

func (inlines *Inlines) Silk_ADD_LSHIFT32(a, b, shift int) int {
	return a + int(int32(b)<<shift)
}

func (inlines *Inlines) Silk_ADD_RSHIFT(a, b, shift int) int {
	return a + (b >> shift)
}

func (inlines *Inlines) Silk_ADD_RSHIFT32(a, b, shift int) int {
	return a + (b >> shift)
}

func (inlines *Inlines) Silk_ADD_RSHIFT_uint(a, b int64, shift int) int64 {
	ret := inlines.CapToUInt32(a + (inlines.CapToUInt32(b) >> shift))
	return ret
	/* shift  > 0 */
}
func (inlines *Inlines) CapToUInt32(val int64) int64 {
	return int64(0xFFFFFFFF & int(val))
}

func (inlines *Inlines) Silk_SUB_LSHIFT32(a, b, shift int) int {
	return a - (b << shift)
}

func (inlines *Inlines) Silk_SUB_RSHIFT32(a, b, shift int) int {
	return a - (b >> shift)
}

func (inlines *Inlines) silk_RSHIFT_ROUND32(a, shift int32) int32 {
	if shift == 1 {
		return (a >> 1) + (a & 1)
	}

	return ((a >> (shift - 1)) + 1) >> 1
}

func (inlines *Inlines) Silk_RSHIFT_ROUND(a, shift int) int {
	if shift == 1 {
		return (a >> 1) + (a & 1)
	}
	return ((a >> (shift - 1)) + 1) >> 1
}

func (inlines *Inlines) Silk_RSHIFT_ROUND64(a int64, shift int) int64 {
	if shift == 1 {
		return (a >> 1) + (a & 1)
	}
	return ((a >> (shift - 1)) + 1) >> 1
}

func (inlines *Inlines) Silk_min(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_max(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_minFloat(a, b float32) float32 {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_maxFloat(a, b float32) float32 {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) SILK_CONST(number float32, scale int) int {
	return int(math.Trunc(float64(int(number*float32(1))<<(scale)) + 0.5))
}

func (inlines *Inlines) Silk_min_int(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_min_16(a, b int16) int16 {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_min_32(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_min_64(a, b int64) int64 {
	if a < b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_max_int(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_max_16(a, b int16) int16 {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_max_32(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_max_64(a, b int64) int64 {
	if a > b {
		return a
	}
	return b
}

func (inlines *Inlines) Silk_LIMITFloat(a, limit1, limit2 float32) float32 {
	if limit1 > limit2 {
		if a > limit1 {
			return limit1
		} else if a < limit2 {
			return limit2
		}
		return a
	}
	if a > limit2 {
		return limit2
	} else if a < limit1 {
		return limit1
	}
	return a
}

func (inlines *Inlines) Silk_LIMIT(a, limit1, limit2 int) int {
	return inlines.Silk_LIMIT_32(a, limit1, limit2)
}

func (inlines *Inlines) Silk_LIMIT_int(a, limit1, limit2 int) int {
	return inlines.Silk_LIMIT_32(a, limit1, limit2)
}

func (inlines *Inlines) Silk_LIMIT_16(a, limit1, limit2 int16) int16 {
	if limit1 > limit2 {
		if a > limit1 {
			return limit1
		} else if a < limit2 {
			return limit2
		}
		return a
	}
	if a > limit2 {
		return limit2
	} else if a < limit1 {
		return limit1
	}
	return a
}

func (inlines *Inlines) Silk_LIMIT_32(a, limit1, limit2 int) int {
	if limit1 > limit2 {
		if a > limit1 {
			return limit1
		} else if a < limit2 {
			return limit2
		}
		return a
	}
	if a > limit2 {
		return limit2
	} else if a < limit1 {
		return limit1
	}
	return a
}

func (inlines *Inlines) Silk_abs(a int) int {
	if a < 0 {
		return -a
	}
	return a
}

func (inlines *Inlines) Silk_abs_int16(a int) int {
	return (a ^ (a >> 15)) - (a >> 15)
}

func (inlines *Inlines) Silk_abs_int(a int) int {
	return (a ^ (a >> 31)) - (a >> 31)
}

func (inlines *Inlines) Silk_abs_int64(a int64) int64 {
	if a < 0 {
		return -a
	}
	return a
}

func (inlines *Inlines) Silk_sign(a int) int {
	if a > 0 {
		return 1
	} else if a < 0 {
		return -1
	}
	return 0
}

func (inlines *Inlines) Silk_RAND(seed int) int {
	return int(inlines.Silk_MLA_ovflw(907633515, int32(seed), 196314165))
}

func (inlines *Inlines) Silk_SMMUL(a32, b32 int) int {
	return int(inlines.Silk_RSHIFT64(inlines.Silk_SMULL(a32, b32), 32))
}

func (inlines *Inlines) Silk_SMLAWT(a32, b32, c32 int) int {
	return a32 + ((b32 >> 16) * (c32 >> 16)) + ((b32 & 0x0000FFFF) * (c32 >> 16) >> 16)
}
func (inlines *Inlines) Silk_abs_int32(a int) int {
	return (a ^ (a >> 31)) - (a >> 31)
}
func (inlines *Inlines) Silk_DIV32_varQ(a32, b32, Qres int) int {

	var a_headrm, b_headrm, lshift int
	var b32_inv, a32_nrm, b32_nrm, result int

	inlines.OpusAssert(b32 != 0)
	inlines.OpusAssert(Qres >= 0)

	/* Compute number of bits head room and normalize inputs */
	a_headrm = inlines.Silk_CLZ32(inlines.Silk_abs(a32)) - 1
	a32_nrm = inlines.Silk_LSHIFT(a32, a_headrm)
	/* Q: a_headrm                  */
	b_headrm = inlines.Silk_CLZ32(inlines.Silk_abs(b32)) - 1

	b32_nrm = inlines.Silk_LSHIFT(b32, b_headrm)
	/* Q: b_headrm                  */

	/* Inverse of b32, with 14 bits of precision */
	b32_inv = inlines.Silk_DIV32_16(math.MaxInt32>>2, inlines.Silk_RSHIFT(b32_nrm, 16))
	/* Q: 29 + 16 - b_headrm        */

	/* First approximation */
	result = inlines.Silk_SMULWB(a32_nrm, b32_inv)
	/* Q: 29 + a_headrm - b_headrm  */

	/* Compute residual by subtracting product of denominator and first approximation */
	/* It's OK to overflow because the final value of a32_nrm should always be small */
	a32_nrm = inlines.Silk_SUB32_ovflw(a32_nrm, silk_LSHIFT_ovflw(inlines.Silk_SMMUL(b32_nrm, result), 3))
	/* Q: a_headrm   */

	/* Refinement */
	result = inlines.Silk_SMLAWB(result, a32_nrm, b32_inv)
	/* Q: 29 + a_headrm - b_headrm  */

	/* Convert to Qres domain */
	lshift = 29 + a_headrm - b_headrm - Qres
	if lshift < 0 {
		return inlines.Silk_LSHIFT_SAT32(result, -lshift)
	} else if lshift < 32 {
		return inlines.Silk_RSHIFT(result, lshift)
	} else {
		/* Avoid undefined result */
		return 0
	}

}

func (inlines *Inlines) Silk_INVERSE32_varQ(b32, Qres int) int {
	inlines.OpusAssert(b32 != 0)
	inlines.OpusAssert(Qres > 0)
	b_headrm := inlines.Silk_CLZ32(inlines.Silk_abs(b32)) - 1
	b32_nrm := inlines.Silk_LSHIFT(b32, b_headrm)
	b32_inv := inlines.Silk_DIV32_16(2147483647>>2, int(inlines.Silk_RSHIFT(b32_nrm, 16)))
	result := inlines.Silk_LSHIFT(b32_inv, 16)
	err_Q32 := inlines.Silk_LSHIFT(((1 << 29) - inlines.Silk_SMULWB(b32_nrm, b32_inv)), 3)
	result = inlines.Silk_SMLAWW(result, err_Q32, b32_inv)
	lshift := 61 - b_headrm - Qres
	if lshift <= 0 {
		return inlines.Silk_LSHIFT_SAT32(result, -lshift)
	} else if lshift < 32 {
		return inlines.Silk_RSHIFT(result, lshift)
	}
	return 0
}

func (inlines *Inlines) Silk_SMLAWB(a32, b32, c32 int) int {

	return a32 + inlines.Silk_SMULWB(b32, c32)
}

func (inlines *Inlines) Silk_SMULWT(a32, b32 int) int {
	return ((a32>>16)*(b32>>16) + (((a32 & 0x0000FFFF) * (b32 >> 16)) >> 16))
}

func (inlines *Inlines) Silk_SMULBT(a32, b32 int) int {
	return int(int(a32)) * (b32 >> 16)
}

func (inlines *Inlines) Silk_SMLABT(a32, b32, c32 int) int {
	return ((a32) + ((int)(int16(b32)))*((c32)>>16))
}
func (inlines *Inlines) Silk_SMLAL(a64 int64, b32, c32 int) int64 {
	return a64 + int64(b32)*int64(c32)
}

func (inlines *Inlines) MatrixGetPointer(row, column, N int) int {
	return row*N + column
}

func (inlines *Inlines) MatrixGet(Matrix_base_adr []int, row, column, N int) int {
	return Matrix_base_adr[row*N+column]
}

func (inlines *Inlines) MatrixGetVals(Matrix_base_adr []*Silk_pe_stage3_vals, row int, column int, N int) *Silk_pe_stage3_vals {
	return Matrix_base_adr[((row)*(N))+(column)]
}

func (inlines *Inlines) MatrixGetShort(Matrix_base_adr []int16, row, column, N int) int16 {
	return Matrix_base_adr[row*N+column]
}

func (inlines *Inlines) MatrixGetPtr(Matrix_base_adr []int, matrix_ptr, row, column, N int) int {
	return Matrix_base_adr[matrix_ptr+row*N+column]
}

func (inlines *Inlines) MatrixGetShortPtr(Matrix_base_adr []int16, matrix_ptr, row, column, N int) int16 {
	return Matrix_base_adr[matrix_ptr+row*N+column]
}

func (inlines *Inlines) MatrixSet(Matrix_base_adr []int, matrix_ptr, row, column, N, value int) {
	Matrix_base_adr[matrix_ptr+row*N+column] = value
}

func (inlines *Inlines) MatrixSet5(Matrix_base_adr []int, row, column, N, value int) {
	Matrix_base_adr[row*N+column] = value
}

func (inlines *Inlines) MatrixSetShort(Matrix_base_adr []int16, matrix_ptr, row, column, N int, value int16) {
	Matrix_base_adr[matrix_ptr+row*N+column] = value
}
func (inlines *Inlines) MatrixSetShort5(Matrix_base_adr []int16, row, column, N int, value int16) {
	Matrix_base_adr[row*N+column] = value
}

func (inlines *Inlines) MatrixSetNoPtr(Matrix_base_adr []int, row, column, N, value int) {
	Matrix_base_adr[row*N+column] = value
}

func (inlines *Inlines) MatrixSetShortNoPtr(Matrix_base_adr []int16, row, column, N int, value int16) {
	Matrix_base_adr[row*N+column] = value
}

func (inlines *Inlines) Silk_SMULWW(a32, b32 int) int {
	return inlines.Silk_MLA(inlines.Silk_SMULWB(a32, b32), a32, inlines.Silk_RSHIFT_ROUND(b32, 16))
}

func (inlines *Inlines) Silk_SMLAWW(a32, b32, c32 int) int {
	return inlines.Silk_MLA(inlines.Silk_SMLAWB(a32, b32, c32), b32, inlines.Silk_RSHIFT_ROUND(c32, 16))
}

func (inlines *Inlines) Silk_CLZ64(input int64) int {
	in_upper := int(input >> 32)
	if in_upper == 0 {
		return 32 + inlines.Silk_CLZ32(int(input))
	}
	return inlines.Silk_CLZ32(in_upper)
}

func (inlines *Inlines) Silk_CLZ32(in32 int) int {
	if in32 == 0 {
		return 32
	}
	return 32 - inlines.EC_ILOG(int64(in32))
}

func (inlines *Inlines) silk_CLZ_FRAC(input int, lz, frac_Q7 *BoxedValueInt) {
	lzeros := inlines.Silk_CLZ32(input)
	lz.Val = lzeros
	frac_Q7.Val = inlines.Silk_ROR32(input, 24-lzeros) & 0x7f
}

func (inlines *Inlines) Silk_SQRT_APPROX(x int) int {
	if x <= 0 {
		return 0
	}
	lz := &BoxedValueInt{}
	frac_Q7 := &BoxedValueInt{}
	inlines.silk_CLZ_FRAC(x, lz, frac_Q7)
	y := 46214
	if (lz.Val & 1) != 0 {
		y = 32768
	}
	y >>= (lz.Val >> 1)
	y = inlines.Silk_SMLAWB(y, y, inlines.Silk_SMULBB(213, frac_Q7.Val))
	return y
}

func (inlines *Inlines) MUL32_FRAC_Q(a32, b32, Q int) int {
	return int(inlines.Silk_RSHIFT_ROUND64(inlines.Silk_SMULL(a32, b32), Q))
}

func (inlines *Inlines) Silk_lin2log(inLin int) int {
	lz := &BoxedValueInt{}
	frac_Q7 := &BoxedValueInt{}
	inlines.silk_CLZ_FRAC(inLin, lz, frac_Q7)
	return inlines.Silk_LSHIFT(31-lz.Val, 7) + inlines.Silk_SMLAWB(frac_Q7.Val, inlines.Silk_MUL(frac_Q7.Val, 128-frac_Q7.Val), 179)
}

func (inlines *Inlines) Silk_log2lin(inLog_Q7 int) int {
	if inLog_Q7 < 0 {
		return 0
	} else if inLog_Q7 >= 3967 {
		return 2147483647
	}
	integer := inLog_Q7 >> 7
	output := 1 << integer
	frac_Q7 := inLog_Q7 & 0x7F
	if inLog_Q7 < 2048 {
		output = inlines.Silk_ADD_RSHIFT32(output, inlines.Silk_MUL(output, inlines.Silk_SMLAWB(frac_Q7, inlines.Silk_SMULBB(frac_Q7, 128-frac_Q7), -174)), 7)
	} else {
		output = inlines.Silk_MLA(output, inlines.Silk_RSHIFT(output, 7), inlines.Silk_SMLAWB(frac_Q7, inlines.Silk_SMULBB(frac_Q7, 128-frac_Q7), -174))
	}
	return output
}

func (inlines *Inlines) Silk_interpolate(xi, x0, x1 []int16, ifact_Q2, d int) {
	inlines.OpusAssert(ifact_Q2 >= 0)
	inlines.OpusAssert(ifact_Q2 <= 4)

	for i := 0; i < d; i++ {
		xi[i] = int16(inlines.Silk_ADD_RSHIFT(int(x0[i]), inlines.Silk_SMULBB(int(x1[i]-x0[i]), ifact_Q2), 2))
	}
}
func (inlines *Inlines) Silk_inner_prod_aligned_scale(inVec1, inVec2 []int16, scale, len int) int {
	var i int = 0
	var sum int = 0
	for i = 0; i < len; i++ {
		sum = inlines.Silk_ADD_RSHIFT32(sum, inlines.Silk_SMULBB(int(inVec1[i]), int(inVec2[i])), scale)
	}
	return sum
}

func (inlines *Inlines) Silk_scale_copy_vector16(data_out []int16, data_out_ptr int, data_in []int16, data_in_ptr int, gain_Q16, dataSize int) {
	for i := 0; i < dataSize; i++ {
		data_out[data_out_ptr+i] = int16(inlines.Silk_SMULWB(gain_Q16, int(data_in[data_in_ptr+i])))
	}
}

func (inlines *Inlines) Silk_scale_vector32_Q26_lshift_18(data1 []int, data1_ptr int, gain_Q26, dataSize int) {
	for i := data1_ptr; i < data1_ptr+dataSize; i++ {
		data1[i] = int(inlines.Silk_RSHIFT64(inlines.Silk_SMULL(int(data1[i]), int(gain_Q26)), 8))
	}
}

func (inlines *Inlines) Silk_inner_prod(inVec1 []int16, inVec1_ptr int, inVec2 []int16, inVec2_ptr int, len int) int {
	xy := 0
	for i := 0; i < len; i++ {
		xy = inlines.MAC16_16Int(xy, inVec1[inVec1_ptr+i], inVec2[inVec2_ptr+i])
	}
	return xy
}

func (inlines *Inlines) Silk_inner_prod_self(inVec []int16, inVec_ptr int, len int) int {
	xy := 0
	for i := inVec_ptr; i < inVec_ptr+len; i++ {
		xy = inlines.MAC16_16Int(xy, inVec[i], inVec[i])
	}
	return xy
}

func (inlines *Inlines) Silk_inner_prod16_aligned_64(inVec1 []int16, inVec1_ptr int, inVec2 []int16, inVec2_ptr int, len int) int64 {
	sum := int64(0)
	for i := 0; i < len; i++ {
		sum = inlines.Silk_SMLALBB(sum, inVec1[inVec1_ptr+i], inVec2[inVec2_ptr+i])
	}
	return sum
}

func (inlines *Inlines) EC_MINI(a, b int64) int64 {
	if b < a {
		return b
	}
	return a
}

func (inlines *Inlines) EC_ILOG(x int64) int {
	if x == 0 {
		return 1
	}
	x |= (x >> 1)
	x |= (x >> 2)
	x |= (x >> 4)
	x |= (x >> 8)
	x |= (x >> 16)
	y := x - ((x >> 1) & 0x55555555)
	y = (((y >> 2) & 0x33333333) + (y & 0x33333333))
	y = (((y >> 4) + y) & 0x0f0f0f0f)
	y += (y >> 8)
	y += (y >> 16)
	y = (y & 0x0000003f)
	return int(y)
}

func (inlines *Inlines) Abs(a int) int {
	if a < 0 {
		return -a
	}
	return a
}

func (inlines *Inlines) SignedByteToUnsignedInt(b int8) int {
	return int(b)
}
