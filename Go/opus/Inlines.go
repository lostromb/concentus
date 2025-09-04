package opus

import (
	"math"
)

func OpusAssert(condition bool) {
	if !condition {
		panic("assertion failed")
	}
}

func OpusAssertMsg(condition bool, message string) {
	if !condition {
		panic(message)
	}
}

func CapToUint(val int) int {
	return int(uint(val))
}

func CapToUintLong(val int64) int64 {
	return int64(int64(val))
}

func MULT16_16SU(a, b int) int {
	return (int(int16(a)) * (int)(b&0xFFFF))
}

func MULT16_32_Q16(a int16, b int) int {
	return ADD32(MULT16_16(int(a), SHR(b, 16)), SHR(MULT16_16SU(int(a), b&0x0000ffff), 16))
}

func MULT16_32_Q16Int(a, b int) int {
	return ADD32(MULT16_16(int(a), SHR(b, 16)), SHR(MULT16_16SU(a, b&0x0000ffff), 16))
}

func MULT16_32_P16(a int16, b int) int {
	return ADD32(MULT16_16(int(a), SHR(b, 16)), PSHR(MULT16_16SU(int(a), b&0x0000ffff), 16))
}

func MULT16_32_P16Int(a, b int) int {
	return ADD32(MULT16_16(int(a), SHR(b, 16)), PSHR(MULT16_16SU(a, b&0x0000ffff), 16))
}

func MULT16_32_Q15(a int16, b int) int {
	return (int(a) * (b >> 16) << 1) + (int(a)*(b&0xFFFF))>>15
}

func MULT16_32_Q15Int(a, b int) int {
	return ((a * (b >> 16)) << 1) + ((a * (b & 0xFFFF)) >> 15)
}

func MULT32_32_Q31(a, b int) int {
	return ADD32(ADD32(SHL(MULT16_16(SHR(a, 16), SHR(b, 16)), 1), SHR(MULT16_16SU(SHR(a, 16), b&0x0000ffff), 15)), SHR(MULT16_16SU(SHR(b, 16), a&0x0000ffff), 15))
}

func QCONST16(x float64, bits int) int16 {
	return int16(0.5 + x*float64(int(1<<bits)))
}

func QCONST32(x float64, bits int) int {
	return int(0.5 + x*float64(int(1<<bits)))
}

func NEG16(x int16) int16 {
	return -x
}

func NEG16Int(x int) int {
	return -x
}

func NEG32(x int) int {
	return -x
}

func EXTRACT16(x int) int16 {
	return int16(x)
}

func EXTEND32(x int16) int {
	return int(x)
}

func EXTEND32Int(x int) int {
	return x
}

func SHR16(a int16, shift int) int16 {
	return a >> shift
}

func SHR16Int(a, shift int) int {
	return a >> shift
}

func SHL16(a int16, shift int) int16 {
	return a << shift
}

func SHL16Int(a, shift int) int {
	return a << shift
}

func SHR32(a, shift int) int {
	return a >> shift
}

func SHR321(a, shift int32) int32 {
	return a >> shift
}

func SHL32(a, shift int) int {
	return a << shift
}

func PSHR32(a, shift int) int {
	return SHR32(a+(EXTEND32(1)<<shift>>1), shift)
}

func PSHR16(a int16, shift int) int16 {
	return SHR16(int16(a+(1<<shift>>1)), shift)
}

func PSHR16Int(a, shift int) int {
	return SHR32(a+(1<<shift>>1), shift)
}

func VSHR32(a, shift int) int {
	if shift > 0 {
		return SHR32(a, shift)
	}
	return SHL32(a, -shift)
}

func SHR(a, shift int) int {
	return a >> shift
}

func SHL(a, shift int) int {
	return a << shift
}

func PSHR(a, shift int) int {
	return SHR(a+(EXTEND32(1)<<shift>>1), shift)
}

func SATURATE(x, a int) int {
	if x > a {
		return a
	} else if x < -a {
		return -a
	}
	return x
}

func SATURATE16(x int) int16 {
	if x > 32767 {
		return 32767
	} else if x < -32768 {
		return -32768
	}
	return int16(x)
}

func ROUND16(x int16, a int16) int16 {
	return EXTRACT16(PSHR32(int(x), int(a)))
}

func ROUND16Int(x, a int) int {
	return PSHR32(x, a)
}

func PDIV32(a, b int) int {
	return a / b
}

func HALF16(x int16) int16 {
	return SHR16(x, 1)
}

func HALF16Int(x int) int {
	return SHR32(x, 1)
}

func HALF32(x int) int {
	return SHR32(x, 1)
}

func ADD16(a, b int16) int16 {
	return a + b
}

func ADD16Int(a, b int) int {
	return a + b
}

func SUB16(a, b int16) int16 {
	return a - b
}

func SUB16Int(a, b int) int {
	return a - b
}

func ADD32(a, b int) int {
	return a + b
}

func SUB32(a, b int) int {
	return a - b
}

func MULT16_16_16(a, b int16) int16 {
	return int16(int16(a) * int16(b))
}

func MULT16_16_16Int(a, b int) int {
	return a * b
}

func MULT16_16(a, b int) int {
	return int(a) * int(b)
}

func MULT16_16Short(a, b int16) int {
	return int(a) * int(b)
}

func MAC16_16(c int16, a, b int16) int {
	return int(c) + int(a)*int(b)
}

func MAC16_16Int(c int, a, b int16) int {
	return c + int(a)*int(b)
}

func MAC16_16IntAll(c, a, b int) int {
	return c + a*b
}

func MAC16_32_Q15(c int, a int16, b int16) int {
	return ADD32(c, ADD32(MULT16_16(int(a), SHR(int(b), 15)), SHR(MULT16_16(int(a), int(b&0x00007fff)), 15)))
}

func MAC16_32_Q15Int(c, a, b int) int {
	return ADD32(c, ADD32(MULT16_16(int(a), SHR(b, 15)), SHR(MULT16_16(int(a), b&0x00007fff), 15)))
}

func MAC16_32_Q16(c int, a int16, b int16) int {
	return ADD32(c, ADD32(MULT16_16(int(a), SHR(int(b), 16)), SHR(MULT16_16SU(int(a), int(int(b)&0x0000ffff)), 16)))
}

func MAC16_32_Q16Int(c, a, b int) int {
	return ADD32(c, ADD32(MULT16_16(int(a), SHR(b, 16)), SHR(MULT16_16SU(a, b&0x0000ffff), 16)))
}

func MULT16_16_Q11_32(a, b int16) int {
	return SHR(MULT16_16Short(a, b), 11)
}

func MULT16_16_Q11_32Int(a, b int) int {
	return SHR(MULT16_16(int(a), int(b)), 11)
}

func MULT16_16_Q11(a, b int16) int16 {
	return int16(SHR(MULT16_16Short(a, b), 11))
}

func MULT16_16_Q11Int(a, b int) int {
	return SHR(MULT16_16(int(a), int(b)), 11)
}

func MULT16_16_Q13(a, b int16) int16 {
	return int16(SHR(MULT16_16Short(a, b), 13))
}

func MULT16_16_Q13Int(a, b int) int {
	return SHR(MULT16_16(a, b), 13)
}

func MULT16_16_Q14(a, b int16) int16 {
	return int16(SHR(MULT16_16(int(a), int(b)), 14))
}

func MULT16_16_Q14Int(a, b int) int {
	return SHR(MULT16_16(a, b), 14)
}

func MULT16_16_Q15(a, b int16) int16 {
	return int16(SHR(MULT16_16(int(a), int(b)), 15))
}

func MULT16_16_Q15Int(a, b int) int {
	return SHR(MULT16_16(a, b), 15)
}

func MULT16_16_P13(a, b int16) int16 {
	return int16(SHR(ADD32(4096, MULT16_16Short(a, b)), 13))
}

func MULT16_16_P13Int(a, b int) int {
	return SHR(ADD32(4096, MULT16_16(int(a), int(b))), 13)
}

func MULT16_16_P14(a, b int16) int16 {
	return int16(SHR(ADD32(8192, MULT16_16Short(a, b)), 14))
}

func MULT16_16_P14Int(a, b int) int {
	return SHR(ADD32(8192, MULT16_16(a, b)), 14)
}

func MULT16_16_P15(a, b int16) int16 {
	return int16(SHR(ADD32(16384, MULT16_16Short(a, b)), 15))
}

func MULT16_16_P15Int(a, b int) int {
	return SHR(ADD32(16384, MULT16_16(a, b)), 15)
}

func DIV32_16(a int, b int16) int16 {
	return int16(a / int(b))
}

func DIV32_16Int(a, b int) int {
	return a / b
}

func DIV32(a, b int) int {
	return a / b
}

func SAT16(x int) int16 {
	if x > 32767 {
		return 32767
	} else if x < -32768 {
		return -32768
	}
	return int16(x)
}

func SIG2WORD16(x int) int16 {
	x = PSHR32(x, 12)
	if x < -32768 {
		x = -32768
	} else if x > 32767 {
		x = 32767
	}
	return EXTRACT16(x)
}

func MIN(a, b int16) int16 {
	if a < b {
		return a
	}
	return b
}

func MAX(a, b int16) int16 {
	if a > b {
		return a
	}
	return b
}

func MIN16(a, b int16) int16 {
	if a < b {
		return a
	}
	return b
}

func MAX16(a, b int16) int16 {
	if a > b {
		return a
	}
	return b
}

func MIN16Int(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func MAX16Int(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func MIN16Float(a, b float32) float32 {
	if a < b {
		return a
	}
	return b
}

func MAX16Float(a, b float32) float32 {
	if a > b {
		return a
	}
	return b
}

func MINInt(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func MAXInt(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func IMIN(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func IMINLong(a, b int64) int64 {
	if a < b {
		return a
	}
	return b
}

func IMAX(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func MIN32(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func MAX32(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func MIN32Float(a, b float32) float32 {
	if a < b {
		return a
	}
	return b
}

func MAX32Float(a, b float32) float32 {
	if a > b {
		return a
	}
	return b
}

func ABS16(x int) int {
	if x < 0 {
		return -x
	}
	return x
}

func ABS16Float(x float32) float32 {
	if x < 0 {
		return -x
	}
	return x
}

func ABS16Short(x int16) int16 {
	if x < 0 {
		return -x
	}
	return x
}

func ABS32(x int) int {
	if x < 0 {
		return -x
	}
	return x
}

func celt_udiv(n, d int) int {
	OpusAssert(d > 0)
	return n / d
}

func celt_sudiv(n, d int) int {
	OpusAssert(d > 0)
	return n / d
}

func celt_div(a, b int) int {
	return MULT32_32_Q31(a, celt_rcp(b))
}

func celt_ilog2(x int) int {
	OpusAssertMsg(x > 0, "celt_ilog2() only defined for strictly positive numbers")
	return EC_ILOG(int64(x)) - 1
}

func celt_zlog2(x int) int {
	if x <= 0 {
		return 0
	}
	return celt_ilog2(x)
}

func celt_maxabs16(x []int, x_ptr, len int) int {
	maxval := 0
	minval := 0
	for i := x_ptr; i < len+x_ptr; i++ {
		maxval = MAX32(maxval, x[i])
		minval = MIN32(minval, x[i])
	}
	return MAX32(EXTEND32Int(maxval), -EXTEND32Int(minval))
}

func celt_maxabs32(x []int, x_ptr, len int) int {
	maxval := 0
	minval := 0
	for i := x_ptr; i < x_ptr+len; i++ {
		maxval = MAX32(maxval, x[i])
		minval = MIN32(minval, x[i])
	}
	return MAX32(maxval, -minval)
}

func celt_maxabs32Short(x []int16, x_ptr, len int) int16 {
	maxval := int16(0)
	minval := int16(0)
	for i := x_ptr; i < x_ptr+len; i++ {
		maxval = MAX16(maxval, x[i])
		minval = MIN16(minval, x[i])
	}
	if maxval > -minval {
		return maxval
	}
	return -minval
}

func FRAC_MUL16(a, b int) int {
	return (16384 + int(int32(a)*int32(b))) >> 15
}

func isqrt32(_val int64) int {
	g := 0
	bshift := (EC_ILOG(_val) - 1) >> 1
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

func celt_sqrt(x int) int {
	if x == 0 {
		return 0
	} else if x >= 1073741824 {
		return 32767
	}
	k := (celt_ilog2(x) >> 1) - 7
	x = VSHR32(x, 2*k)
	n := int16(x - 32768)
	rt := ADD16(sqrt_C[0], MULT16_16_Q15(n, ADD16(sqrt_C[1], MULT16_16_Q15(n, ADD16(sqrt_C[2],
		MULT16_16_Q15(n, ADD16(sqrt_C[3], MULT16_16_Q15(n, sqrt_C[4]))))))))
	rt = int16(VSHR32(int(rt), 7-k))
	return int(rt)
}

func celt_rcp(x int) int {
	OpusAssertMsg(x > 0, "celt_rcp() only defined for positive values")
	i := celt_ilog2(x)
	n := VSHR32(x, i-15) - 32768
	r := ADD16Int(30840, MULT16_16_Q15Int(-15420, int(n)))
	r = SUB16Int(r, MULT16_16_Q15Int(r,
		ADD16Int(MULT16_16_Q15Int(r, int(n)), ADD16Int(r, -32768))))
	r = SUB16Int(r, ADD16Int(1, MULT16_16_Q15Int(r,
		ADD16Int(MULT16_16_Q15Int(r, int(n)), ADD16Int(r, -32768)))))
	return VSHR32(EXTEND32Int(r), i-16)
}

func celt_rsqrt_norm(x int) int {
	n := x - 32768
	r := ADD16Int(23557, MULT16_16_Q15Int(int(n), ADD16Int(-13490, MULT16_16_Q15Int(int(n), 6713))))
	r2 := MULT16_16_Q15Int(r, r)

	y := SHL16Int(SUB16Int(ADD16Int(MULT16_16_Q15Int(r2, n), r2), 16384), 1)
	return ADD16Int(r, MULT16_16_Q15Int(r, MULT16_16_Q15Int(int(y),
		SUB16Int(MULT16_16_Q15Int(int(y), 12288), 16384))))
}

func frac_div32(a, b int) int {
	shift := celt_ilog2(b) - 29
	a = VSHR32(a, shift)
	b = VSHR32(b, shift)
	rcp := ROUND16Int(celt_rcp(ROUND16Int(b, 16)), 3)
	result := MULT16_32_Q15Int(rcp, a)
	rem := PSHR32(a, 2) - MULT32_32_Q31(result, b)
	result = ADD32(result, SHL32(MULT16_32_Q15Int(rcp, rem), 2))
	if result >= 536870912 {
		return 2147483647
	} else if result <= -536870912 {
		return -2147483647
	}
	return SHL32(result, 2)
}

var log2_C0 = -6801 + (1 << 3)

func celt_log2(x int) int {
	if x == 0 {
		return -32767
	}
	i := celt_ilog2(x)
	n := VSHR32(x, i-15) - 32768 - 16384
	frac := ADD16Int(log2_C0, MULT16_16_Q15Int(int(n), ADD16Int(15746, MULT16_16_Q15Int(int(n), ADD16Int(-5217, MULT16_16_Q15Int(int(n), ADD16Int(2545, MULT16_16_Q15Int(int(n), -1401))))))))
	return SHL16Int(int(i-13), 10) + SHR16Int(int(frac), 4)
}

func celt_exp2_frac(x int) int {
	frac := SHL16Int(int(x), 4)
	return ADD16Int(16383, MULT16_16_Q15Int(frac, ADD16Int(22804, MULT16_16_Q15Int(frac, ADD16Int(14819, MULT16_16_Q15Int(10204, int(frac)))))))
}

func celt_exp2(inLog_Q7 int) int {
	integer := SHR16Int((inLog_Q7), 10)
	if integer > 14 {
		return 0x7f000000
	} else if integer < -15 {
		return 0
	}
	frac := celt_exp2_frac(int(inLog_Q7 - SHL16Int(integer, 10)))
	return VSHR32(EXTEND32Int(frac), -int(integer)-2)
}

func celt_atan01(x int) int {
	return MULT16_16_P15Int(int(x), ADD32(32767, MULT16_16_P15Int(int(x), ADD32(-21, MULT16_16_P15Int(int(x), ADD32(-11943, MULT16_16_P15Int(4936, int(x))))))))
}

func celt_atan2p(y, x int) int {
	if y < x {
		arg := celt_div(SHL32(EXTEND32Int(y), 15), x)
		if arg >= 32767 {
			arg = 32767
		}
		return SHR32(celt_atan01(int(EXTRACT16(arg))), 1)
	}
	arg := celt_div(SHL32(EXTEND32Int(x), 15), y)
	if arg >= 32767 {
		arg = 32767
	}
	return 25736 - SHR16Int(celt_atan01(int(EXTRACT16(arg))), 1)
}

func celt_cos_norm(x int) int {
	x = x & 0x0001ffff
	if x > SHL32(EXTEND32(1), 16) {
		x = SUB32(SHL32(EXTEND32(1), 17), x)
	}
	if (x & 0x00007fff) != 0 {
		if x < SHL32(EXTEND32(1), 15) {
			return _celt_cos_pi_2(int(EXTRACT16(x)))
		}
		return NEG32(_celt_cos_pi_2(int(EXTRACT16(65536 - x))))
	} else if (x & 0x0000ffff) != 0 {
		return 0
	} else if (x & 0x0001ffff) != 0 {
		return -32767
	}
	return 32767
}

func _celt_cos_pi_2(x int) int {
	x2 := MULT16_16_P15Int(int(x), int(x))
	return ADD32(1, MIN32(32766, ADD32(SUB16Int(32767, x2), MULT16_16_P15Int(int(x2), ADD32(-7651, MULT16_16_P15Int(int(x2), ADD32(8277, MULT16_16_P15Int(-626, int(x2)))))))))
}

func FLOAT2INT16(x float32) int16 {

	x = x * CeltConstants.CELT_SIG_SCALE
	if x < math.MinInt16 {
		x = math.MinInt16
	}
	if x > math.MaxInt16 {
		x = math.MaxInt16
	}
	return int16(x)
}

func silk_ROR32(a32, rot int) int {
	if rot == 0 {
		return a32
	} else if rot < 0 {
		m := -rot
		return (a32 << m) | (a32 >> (32 - m))
	}
	return (a32 << (32 - rot)) | (a32 >> rot)
}

func silk_MUL(a32, b32 int) int {
	return a32 * b32
}

/*
	func silk_MLA(a32, b32, c32 int) int {
		ret := a32 + b32*c32
		OpusAssert(int64(ret) == int64(a32)+int64(b32)*int64(c32))
		return ret
	}
*/
func silk_MLA(a32, b32, c32 int) int {
	ret := silk_ADD32((a32), ((b32) * (c32)))
	OpusAssert(int64(ret) == int64(a32)+int64(b32)*int64(c32))
	return ret
}

func silk_SMULTT(a32, b32 int) int {
	return (a32 >> 16) * (b32 >> 16)
}

func silk_SMLATT(a32, b32, c32 int) int {
	return a32 + ((b32 >> 16) * (c32 >> 16))
}

func silk_SMLALBB(a64 int64, b16, c16 int16) int64 {
	return a64 + int64(b16)*int64(c16)
}

func silk_SMULL(a32, b32 int) int64 {
	return int64(a32) * int64(b32)
}

func silk_ADD32_ovflw(a, b int32) int32 {
	return int32(int64(a) + int64(b))
}

func silk_ADD32_ovflwLong(a, b int64) int {
	return int(a + b)
}

func silk_SUB32_ovflw(a, b int) int {
	return int(a - b)
}

func silk_MLA_ovflw(a32, b32, c32 int32) int32 {
	return silk_ADD32_ovflw(a32, b32*c32)
}

func silk_SMLABB_ovflw(a32, b32, c32 int32) int32 {
	return silk_ADD32_ovflw(a32, int32(b32)*int32(c32))
}

func silk_SMULBB(a32, b32 int) int {
	return int(int(int16(a32)) * int(int16(b32)))
}
func silk_SMULWB(a32, b32 int) int {
	return int(int32(int64(a32) * int64(int16(b32)) >> 16))
}

func silk_SMLABB(a32, b32, c32 int) int {
	return ((a32) + int(int32(int16(b32))*int32(int16(c32))))
}

func silk_DIV32_16(a32, b32 int) int {
	return a32 / b32
}

func silk_DIV32(a32, b32 int) int {
	return a32 / b32
}

func silk_ADD16(a, b int16) int16 {
	return a + b
}

func silk_ADD32(a, b int) int {
	return a + b
}

func silk_ADD64(a, b int64) int64 {
	return a + b
}

func silk_SUB16(a, b int16) int16 {
	return a - b
}

func silk_SUB32(a, b int) int {
	return a - b
}

func silk_SUB64(a, b int64) int64 {
	return a - b
}

func silk_SAT8(a int) int {
	if a > 127 {
		return 127
	} else if a < -128 {
		return -128
	}
	return a
}

func silk_SAT16(a int) int {
	if a > math.MaxInt16 {
		return math.MaxInt16
	}
	if (a) < math.MinInt16 {
		return math.MinInt16
	}
	return a
}

func silk_SAT32(a int64) int {
	if a > 2147483647 {
		return 2147483647
	} else if a < -2147483648 {
		return -2147483648
	}
	return int(a)
}

func silk_ADD_SAT16(a16, b16 int16) int16 {
	res := int16(silk_SAT16(int(a16) + int(b16)))
	OpusAssert(res == int16(silk_SAT16(int(a16)+int(b16))))
	return res
}

func silk_ADD_SAT32(a32, b32 int) int {
	sum := int64(a32) + int64(b32)
	if sum > 2147483647 {
		return 2147483647
	} else if sum < -2147483648 {
		return -2147483648
	}
	return int(sum)
}

func silk_ADD_SAT64(a64, b64 int64) int64 {
	if (a64 > 0 && b64 > math.MaxInt64-a64) || (a64 < 0 && b64 < math.MinInt64-a64) {
		if a64 > 0 {
			return math.MaxInt64
		}
		return math.MinInt64
	}
	return a64 + b64
}

func silk_SUB_SAT16(a16, b16 int16) int16 {
	res := int16(silk_SAT16(int(a16) - int(b16)))
	OpusAssert(res == int16(silk_SAT16(int(a16)-int(b16))))
	return res
}

func silk_SUB_SAT32(a32, b32 int) int {
	diff := int64(a32) - int64(b32)
	if diff > 2147483647 {
		return 2147483647
	} else if diff < -2147483648 {
		return -2147483648
	}
	return int(diff)
}

func silk_SUB_SAT64(a64, b64 int64) int64 {
	if (b64 > 0 && a64 < math.MinInt64+b64) || (b64 < 0 && a64 > math.MaxInt64+b64) {
		if b64 > 0 {
			return math.MinInt64
		}
		return math.MaxInt64
	}
	return a64 - b64
}

func silk_ADD_POS_SAT8(a, b byte) byte {
	sum := int(a) + int(b)
	if sum > 127 {
		return 127
	}
	return byte(sum)
}

func silk_ADD_POS_SAT16(a, b int16) int16 {
	sum := int(a) + int(b)
	if sum > 32767 {
		return 32767
	}
	return int16(sum)
}

func silk_ADD_POS_SAT32(a, b int) int {
	sum := int64(a) + int64(b)
	if sum > 2147483647 {
		return 2147483647
	}
	return int(sum)
}

func silk_ADD_POS_SAT64(a, b int64) int64 {
	if a > math.MaxInt64-b {
		return math.MaxInt64
	}
	return a + b
}

func silk_LSHIFT8(a byte, shift int) byte {
	return a << shift
}

func silk_LSHIFT16(a int16, shift int) int16 {
	return a << shift
}

func silk_LSHIFT32(a, shift int) int {
	return a << shift
}

func silk_LSHIFT32_32(a, shift int) int32 {
	return int32(a << shift)
}

func silk_LSHIFT64(a int64, shift int) int64 {
	return a << shift
}

func silk_LSHIFT(a, shift int) int {
	return a << shift
}

func silk_LSHIFT_ovflw(a, shift int) int {
	return a << shift
}

func silk_LSHIFT_SAT32(a, shift int) int {
	return (silk_LSHIFT32(silk_LIMIT((a), silk_RSHIFT32(math.MinInt32, (shift)), silk_RSHIFT32(math.MaxInt32, (shift))), (shift)))
}

func silk_RSHIFT8(a byte, shift int) byte {
	return a >> shift
}

func silk_RSHIFT16(a int16, shift int) int16 {
	return a >> shift
}

func silk_RSHIFT32(a, shift int) int {
	return a >> shift
}

func silk_RSHIFT(a, shift int) int {
	return a >> shift
}

func silk_RSHIFT64(a int64, shift int) int64 {
	return a >> shift
}

func silk_RSHIFT_uint(a int64, shift int) int64 {
	return int64(CapToUInt32(a) >> shift)
}

func silk_ADD_LSHIFT(a, b, shift int) int {
	return a + (b << shift)
}

func silk_ADD_LSHIFT32(a, b, shift int) int {
	return a + int(int32(b)<<shift)
}

func silk_ADD_RSHIFT(a, b, shift int) int {
	return a + (b >> shift)
}

func silk_ADD_RSHIFT32(a, b, shift int) int {
	return a + (b >> shift)
}

func silk_ADD_RSHIFT_uint(a, b int64, shift int) int64 {
	ret := CapToUInt32(a + (CapToUInt32(b) >> shift))
	return ret
	/* shift  > 0 */
}
func CapToUInt32(val int64) int64 {
	return int64(0xFFFFFFFF & int(val))
}

func silk_SUB_LSHIFT32(a, b, shift int) int {
	return a - (b << shift)
}

func silk_SUB_RSHIFT32(a, b, shift int) int {
	return a - (b >> shift)
}

func silk_RSHIFT_ROUND32(a, shift int32) int32 {
	if shift == 1 {
		return (a >> 1) + (a & 1)
	}

	return ((a >> (shift - 1)) + 1) >> 1
}

func silk_RSHIFT_ROUND(a, shift int) int {
	if shift == 1 {
		return (a >> 1) + (a & 1)
	}
	return ((a >> (shift - 1)) + 1) >> 1
}

func silk_RSHIFT_ROUND64(a int64, shift int) int64 {
	if shift == 1 {
		return (a >> 1) + (a & 1)
	}
	return ((a >> (shift - 1)) + 1) >> 1
}

func silk_min(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func silk_max(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func silk_minFloat(a, b float32) float32 {
	if a < b {
		return a
	}
	return b
}

func silk_maxFloat(a, b float32) float32 {
	if a > b {
		return a
	}
	return b
}

func SILK_CONST(number float32, scale int) int {
	return int(math.Trunc(float64(int(number*float32(1))<<(scale)) + 0.5))
}

func silk_min_int(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func silk_min_16(a, b int16) int16 {
	if a < b {
		return a
	}
	return b
}

func silk_min_32(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func silk_min_64(a, b int64) int64 {
	if a < b {
		return a
	}
	return b
}

func silk_max_int(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func silk_max_16(a, b int16) int16 {
	if a > b {
		return a
	}
	return b
}

func silk_max_32(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func silk_max_64(a, b int64) int64 {
	if a > b {
		return a
	}
	return b
}

func silk_LIMITFloat(a, limit1, limit2 float32) float32 {
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

func silk_LIMIT(a, limit1, limit2 int) int {
	return silk_LIMIT_32(a, limit1, limit2)
}

func silk_LIMIT_int(a, limit1, limit2 int) int {
	return silk_LIMIT_32(a, limit1, limit2)
}

func silk_LIMIT_16(a, limit1, limit2 int16) int16 {
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

func silk_LIMIT_32(a, limit1, limit2 int) int {
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

func silk_abs(a int) int {
	if a < 0 {
		return -a
	}
	return a
}

func silk_abs_int16(a int) int {
	return (a ^ (a >> 15)) - (a >> 15)
}

func silk_abs_int(a int) int {
	return (a ^ (a >> 31)) - (a >> 31)
}

func silk_abs_int64(a int64) int64 {
	if a < 0 {
		return -a
	}
	return a
}

func silk_sign(a int) int {
	if a > 0 {
		return 1
	} else if a < 0 {
		return -1
	}
	return 0
}

func silk_RAND(seed int) int {
	return int(silk_MLA_ovflw(907633515, int32(seed), 196314165))
}

func silk_SMMUL(a32, b32 int) int {
	return int(silk_RSHIFT64(silk_SMULL(a32, b32), 32))
}

func silk_SMLAWT(a32, b32, c32 int) int {
	return a32 + ((b32 >> 16) * (c32 >> 16)) + ((b32 & 0x0000FFFF) * (c32 >> 16) >> 16)
}
func silk_abs_int32(a int) int {
	return (a ^ (a >> 31)) - (a >> 31)
}
func silk_DIV32_varQ(a32, b32, Qres int) int {

	var a_headrm, b_headrm, lshift int
	var b32_inv, a32_nrm, b32_nrm, result int

	OpusAssert(b32 != 0)
	OpusAssert(Qres >= 0)

	/* Compute number of bits head room and normalize inputs */
	a_headrm = silk_CLZ32(silk_abs(a32)) - 1
	a32_nrm = silk_LSHIFT(a32, a_headrm)
	/* Q: a_headrm                  */
	b_headrm = silk_CLZ32(silk_abs(b32)) - 1

	b32_nrm = silk_LSHIFT(b32, b_headrm)
	/* Q: b_headrm                  */

	/* Inverse of b32, with 14 bits of precision */
	b32_inv = silk_DIV32_16(math.MaxInt32>>2, silk_RSHIFT(b32_nrm, 16))
	/* Q: 29 + 16 - b_headrm        */

	/* First approximation */
	result = silk_SMULWB(a32_nrm, b32_inv)
	/* Q: 29 + a_headrm - b_headrm  */

	/* Compute residual by subtracting product of denominator and first approximation */
	/* It's OK to overflow because the final value of a32_nrm should always be small */
	a32_nrm = silk_SUB32_ovflw(a32_nrm, silk_LSHIFT_ovflw(silk_SMMUL(b32_nrm, result), 3))
	/* Q: a_headrm   */

	/* Refinement */
	result = silk_SMLAWB(result, a32_nrm, b32_inv)
	/* Q: 29 + a_headrm - b_headrm  */

	/* Convert to Qres domain */
	lshift = 29 + a_headrm - b_headrm - Qres
	if lshift < 0 {
		return silk_LSHIFT_SAT32(result, -lshift)
	} else if lshift < 32 {
		return silk_RSHIFT(result, lshift)
	} else {
		/* Avoid undefined result */
		return 0
	}

}

func silk_INVERSE32_varQ(b32, Qres int) int {
	OpusAssert(b32 != 0)
	OpusAssert(Qres > 0)
	b_headrm := silk_CLZ32(silk_abs(b32)) - 1
	b32_nrm := silk_LSHIFT(b32, b_headrm)
	b32_inv := silk_DIV32_16(2147483647>>2, int(silk_RSHIFT(b32_nrm, 16)))
	result := silk_LSHIFT(b32_inv, 16)
	err_Q32 := silk_LSHIFT(((1 << 29) - silk_SMULWB(b32_nrm, b32_inv)), 3)
	result = silk_SMLAWW(result, err_Q32, b32_inv)
	lshift := 61 - b_headrm - Qres
	if lshift <= 0 {
		return silk_LSHIFT_SAT32(result, -lshift)
	} else if lshift < 32 {
		return silk_RSHIFT(result, lshift)
	}
	return 0
}

func silk_SMLAWB(a32, b32, c32 int) int {

	return a32 + silk_SMULWB(b32, c32)
}

func silk_SMULWT(a32, b32 int) int {
	return ((a32>>16)*(b32>>16) + (((a32 & 0x0000FFFF) * (b32 >> 16)) >> 16))
}

func silk_SMULBT(a32, b32 int) int {
	return int(int(a32)) * (b32 >> 16)
}

func silk_SMLABT(a32, b32, c32 int) int {
	return ((a32) + ((int)(int16(b32)))*((c32)>>16))
}
func silk_SMLAL(a64 int64, b32, c32 int) int64 {
	return a64 + int64(b32)*int64(c32)
}

func MatrixGetPointer(row, column, N int) int {
	return row*N + column
}

func MatrixGet(Matrix_base_adr []int, row, column, N int) int {
	return Matrix_base_adr[row*N+column]
}

func MatrixGetVals(Matrix_base_adr []*silk_pe_stage3_vals, row int, column int, N int) *silk_pe_stage3_vals {
	return Matrix_base_adr[((row)*(N))+(column)]
}

func MatrixGetShort(Matrix_base_adr []int16, row, column, N int) int16 {
	return Matrix_base_adr[row*N+column]
}

func MatrixGetPtr(Matrix_base_adr []int, matrix_ptr, row, column, N int) int {
	return Matrix_base_adr[matrix_ptr+row*N+column]
}

func MatrixGetShortPtr(Matrix_base_adr []int16, matrix_ptr, row, column, N int) int16 {
	return Matrix_base_adr[matrix_ptr+row*N+column]
}

func MatrixSet(Matrix_base_adr []int, matrix_ptr, row, column, N, value int) {
	Matrix_base_adr[matrix_ptr+row*N+column] = value
}

func MatrixSet5(Matrix_base_adr []int, row, column, N, value int) {
	Matrix_base_adr[row*N+column] = value
}

func MatrixSetShort(Matrix_base_adr []int16, matrix_ptr, row, column, N int, value int16) {
	Matrix_base_adr[matrix_ptr+row*N+column] = value
}
func MatrixSetShort5(Matrix_base_adr []int16, row, column, N int, value int16) {
	Matrix_base_adr[row*N+column] = value
}

func MatrixSetNoPtr(Matrix_base_adr []int, row, column, N, value int) {
	Matrix_base_adr[row*N+column] = value
}

func MatrixSetShortNoPtr(Matrix_base_adr []int16, row, column, N int, value int16) {
	Matrix_base_adr[row*N+column] = value
}

func silk_SMULWW(a32, b32 int) int {
	return silk_MLA(silk_SMULWB(a32, b32), a32, silk_RSHIFT_ROUND(b32, 16))
}

func silk_SMLAWW(a32, b32, c32 int) int {
	return silk_MLA(silk_SMLAWB(a32, b32, c32), b32, silk_RSHIFT_ROUND(c32, 16))
}

func silk_CLZ64(input int64) int {
	in_upper := int(input >> 32)
	if in_upper == 0 {
		return 32 + silk_CLZ32(int(input))
	}
	return silk_CLZ32(in_upper)
}

func silk_CLZ32(in32 int) int {
	if in32 == 0 {
		return 32
	}
	return 32 - EC_ILOG(int64(in32))
}

func silk_CLZ_FRAC(input int, lz, frac_Q7 *BoxedValueInt) {
	lzeros := silk_CLZ32(input)
	lz.Val = lzeros
	frac_Q7.Val = silk_ROR32(input, 24-lzeros) & 0x7f
}

func silk_SQRT_APPROX(x int) int {
	if x <= 0 {
		return 0
	}
	lz := &BoxedValueInt{}
	frac_Q7 := &BoxedValueInt{}
	silk_CLZ_FRAC(x, lz, frac_Q7)
	y := 46214
	if (lz.Val & 1) != 0 {
		y = 32768
	}
	y >>= (lz.Val >> 1)
	y = silk_SMLAWB(y, y, silk_SMULBB(213, frac_Q7.Val))
	return y
}

func MUL32_FRAC_Q(a32, b32, Q int) int {
	return int(silk_RSHIFT_ROUND64(silk_SMULL(a32, b32), Q))
}

func silk_lin2log(inLin int) int {
	lz := &BoxedValueInt{}
	frac_Q7 := &BoxedValueInt{}
	silk_CLZ_FRAC(inLin, lz, frac_Q7)
	return silk_LSHIFT(31-lz.Val, 7) + silk_SMLAWB(frac_Q7.Val, silk_MUL(frac_Q7.Val, 128-frac_Q7.Val), 179)
}

func silk_log2lin(inLog_Q7 int) int {
	if inLog_Q7 < 0 {
		return 0
	} else if inLog_Q7 >= 3967 {
		return 2147483647
	}
	integer := inLog_Q7 >> 7
	output := 1 << integer
	frac_Q7 := inLog_Q7 & 0x7F
	if inLog_Q7 < 2048 {
		output = silk_ADD_RSHIFT32(output, silk_MUL(output, silk_SMLAWB(frac_Q7, silk_SMULBB(frac_Q7, 128-frac_Q7), -174)), 7)
	} else {
		output = silk_MLA(output, silk_RSHIFT(output, 7), silk_SMLAWB(frac_Q7, silk_SMULBB(frac_Q7, 128-frac_Q7), -174))
	}
	return output
}

func silk_interpolate(xi, x0, x1 []int16, ifact_Q2, d int) {
	OpusAssert(ifact_Q2 >= 0)
	OpusAssert(ifact_Q2 <= 4)

	for i = 0; i < d; i++ {
		xi[i] = int16(silk_ADD_RSHIFT(int(x0[i]), silk_SMULBB(int(x1[i]-x0[i]), ifact_Q2), 2))
	}
}
func silk_inner_prod_aligned_scale(inVec1, inVec2 []int16, scale, len int) int {
	var i int = 0
	var sum int = 0
	for i = 0; i < len; i++ {
		sum = silk_ADD_RSHIFT32(sum, silk_SMULBB(int(inVec1[i]), int(inVec2[i])), scale)
	}
	return sum
}

func silk_scale_copy_vector16(data_out []int16, data_out_ptr int, data_in []int16, data_in_ptr int, gain_Q16, dataSize int) {
	for i := 0; i < dataSize; i++ {
		data_out[data_out_ptr+i] = int16(silk_SMULWB(gain_Q16, int(data_in[data_in_ptr+i])))
	}
}

func silk_scale_vector32_Q26_lshift_18(data1 []int, data1_ptr int, gain_Q26, dataSize int) {
	for i := data1_ptr; i < data1_ptr+dataSize; i++ {
		data1[i] = int(silk_RSHIFT64(silk_SMULL(int(data1[i]), int(gain_Q26)), 8))
	}
}

func silk_inner_prod(inVec1 []int16, inVec1_ptr int, inVec2 []int16, inVec2_ptr int, len int) int {
	xy := 0
	for i := 0; i < len; i++ {
		xy = MAC16_16Int(xy, inVec1[inVec1_ptr+i], inVec2[inVec2_ptr+i])
	}
	return xy
}

func silk_inner_prod_self(inVec []int16, inVec_ptr int, len int) int {
	xy := 0
	for i := inVec_ptr; i < inVec_ptr+len; i++ {
		xy = MAC16_16Int(xy, inVec[i], inVec[i])
	}
	return xy
}

func silk_inner_prod16_aligned_64(inVec1 []int16, inVec1_ptr int, inVec2 []int16, inVec2_ptr int, len int) int64 {
	sum := int64(0)
	for i := 0; i < len; i++ {
		sum = silk_SMLALBB(sum, inVec1[inVec1_ptr+i], inVec2[inVec2_ptr+i])
	}
	return sum
}

func EC_MINI(a, b int64) int64 {
	if b < a {
		return b
	}
	return a
}

func EC_ILOG(x int64) int {
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

func abs(a int) int {
	if a < 0 {
		return -a
	}
	return a
}

func SignedByteToUnsignedInt(b int8) int {
	return int(b)
}
