package comm

import (
	"fmt"

	"github.com/dosgo/concentus/go/comm/arrayUtil"
)

const (
	EC_WINDOW_SIZE = 32
	EC_UINT_BITS   = 8
	BITRES         = 3
	EC_SYM_BITS    = 8
	EC_CODE_BITS   = 32
	EC_SYM_MAX     = 0x000000FF
	EC_CODE_SHIFT  = 23
	EC_CODE_TOP    = 0x80000000
	EC_CODE_BOT    = 0x00800000
	EC_CODE_EXTRA  = 7
)

var correction = []int{35733, 38967, 42495, 46340, 50535, 55109, 60097, 65535}

type EntropyCoder struct {
	buf         []byte
	buf_ptr     int
	Storage     int
	end_offs    int
	end_window  int64
	nend_bits   int
	Nbits_total int
	Offs        int
	Rng         int64
	val         int64
	ext         int64
	rem         int
	error       int
}

func NewEntropyCoder() *EntropyCoder {
	obj := &EntropyCoder{}
	obj.Reset()
	return obj
}
func (ec *EntropyCoder) Reset() {
	ec.buf = nil
	ec.buf_ptr = 0
	ec.Storage = 0
	ec.end_offs = 0
	ec.end_window = 0
	ec.nend_bits = 0
	ec.Offs = 0
	ec.Rng = 0
	ec.val = 0
	ec.ext = 0
	ec.rem = 0
	ec.error = 0
}

func (ec *EntropyCoder) Assign(other *EntropyCoder) {
	ec.buf = other.buf
	ec.buf_ptr = other.buf_ptr
	ec.Storage = other.Storage
	ec.end_offs = other.end_offs
	ec.end_window = other.end_window
	ec.nend_bits = other.nend_bits
	ec.Nbits_total = other.Nbits_total
	ec.Offs = other.Offs
	ec.Rng = other.Rng
	ec.val = other.val
	ec.ext = other.ext
	ec.rem = other.rem
	ec.error = other.error
}

func (ec *EntropyCoder) Get_buffer() []byte {
	bufCopy := make([]byte, ec.Storage)
	copy(bufCopy, ec.buf[ec.buf_ptr:ec.buf_ptr+ec.Storage])
	return bufCopy
}

func (ec *EntropyCoder) Write_buffer(data []byte, data_ptr int, target_offset int, size int) {
	copy(ec.buf[ec.buf_ptr+target_offset:], data[data_ptr:data_ptr+size])
}

func (ec *EntropyCoder) read_byte() int {
	if ec.Offs < ec.Storage {
		val := ec.buf[ec.buf_ptr+ec.Offs]
		ec.Offs++
		return int(val)
	}
	return 0
}

func (ec *EntropyCoder) read_byte_from_end() int {
	if ec.end_offs < ec.Storage {
		ec.end_offs++
		return int(ec.buf[ec.buf_ptr+(ec.Storage-ec.end_offs)])
	}
	return 0
}

func (ec *EntropyCoder) write_byte(_value int64) int {
	if ec.Offs+ec.end_offs >= ec.Storage {
		return -1
	}
	ec.buf[ec.buf_ptr+ec.Offs] = byte(_value)
	ec.Offs++
	return 0
}

func (ec *EntropyCoder) write_byte_at_end(_value int64) int {
	if ec.Offs+ec.end_offs >= ec.Storage {
		return -1
	}
	ec.end_offs++
	ec.buf[ec.buf_ptr+(ec.Storage-ec.end_offs)] = byte(_value)
	return 0
}

func (ec *EntropyCoder) dec_normalize() {
	for ec.Rng <= EC_CODE_BOT {
		var sym int
		ec.Nbits_total += EC_SYM_BITS
		ec.Rng = inlines.CapToUInt32(ec.Rng << EC_SYM_BITS)

		/*Use up the remaining bits from our last symbol.*/
		sym = ec.rem

		/*Read the next value from the input.*/
		ec.rem = ec.read_byte()

		/*Take the rest of the bits we need from this new symbol.*/
		sym = (sym<<EC_SYM_BITS | ec.rem) >> (EC_SYM_BITS - EC_CODE_EXTRA)

		/*And subtract them from val, capped to be less than EC_CODE_TOP.*/
		ec.val = inlines.CapToUInt32(((int64(ec.val) << EC_SYM_BITS) + int64(EC_SYM_MAX & ^sym)) & int64(EC_CODE_TOP-1))
	}
}

func (ec *EntropyCoder) Dec_init(_buf []byte, _buf_ptr int, _storage int) {

	ec.buf = _buf
	ec.buf_ptr = _buf_ptr
	ec.Storage = _storage
	ec.end_offs = 0
	ec.end_window = 0
	ec.nend_bits = 0
	ec.Nbits_total = EC_CODE_BITS + 1 - ((EC_CODE_BITS-EC_CODE_EXTRA)/EC_SYM_BITS)*EC_SYM_BITS
	ec.Offs = 0
	ec.Rng = 1 << EC_CODE_EXTRA
	ec.rem = ec.read_byte()
	//ec.val = ec.rng - 1 - int64(ec.rem>>(EC_SYM_BITS-EC_CODE_EXTRA))
	ec.val = inlines.CapToUInt32(ec.Rng - 1 - int64(ec.rem>>(EC_SYM_BITS-EC_CODE_EXTRA)))
	ec.error = 0
	ec.dec_normalize()
}

func (ec *EntropyCoder) Decode(_ft int64) int64 {
	_ft = inlines.CapToUInt32(_ft)
	ec.ext = inlines.CapToUInt32(ec.Rng / _ft)
	s := inlines.CapToUInt32(ec.val / ec.ext)
	return inlines.CapToUInt32(_ft - inlines.EC_MINI(inlines.CapToUInt32(s+1), _ft))
}

func (ec *EntropyCoder) Decode_bin(_bits int) int64 {
	ec.ext = ec.Rng >> _bits
	s := inlines.CapToUInt32(ec.val / ec.ext)
	return inlines.CapToUInt32(inlines.CapToUInt32(1<<_bits) - inlines.EC_MINI(inlines.CapToUInt32(s+1), 1<<_bits))
}

func (ec *EntropyCoder) Dec_update(_fl int64, _fh int64, _ft int64) {
	_fl = inlines.CapToUInt32(_fl)
	_fh = inlines.CapToUInt32(_fh)
	_ft = inlines.CapToUInt32(_ft)
	s := inlines.CapToUInt32(ec.ext * (_ft - _fh))
	ec.val = ec.val - s
	if _fl > 0 {
		ec.Rng = inlines.CapToUInt32(ec.ext * (_fh - _fl))
	} else {
		ec.Rng = ec.Rng - s
	}
	ec.dec_normalize()
}

func (ec *EntropyCoder) Dec_bit_logp(_logp int64) int {
	var r int64
	var d int64
	var s int64
	var ret int
	r = ec.Rng
	d = ec.val
	s = r >> _logp
	ret = 0
	if d < s {
		ret = 1
	}

	if ret == 0 {
		ec.val = inlines.CapToUInt32(d - s)
	}
	if ret != 0 {
		ec.Rng = s
	} else {
		ec.Rng = r - s
	}
	ec.dec_normalize()
	return ret
}

func (ec *EntropyCoder) Dec_icdf(_icdf []int16, _ftb int) int {
	var t int64
	var s = ec.Rng
	var d = ec.val
	var r = s >> _ftb
	var ret int = -1
	for {
		ret++
		t = s
		s = inlines.CapToUInt32(r * int64(_icdf[ret]))
		if d < s {
			continue
		}
		break
	}
	ec.val = inlines.CapToUInt32(d - s)
	ec.Rng = inlines.CapToUInt32(t - s)
	ec.dec_normalize()
	return ret
}

func (ec *EntropyCoder) Dec_icdf_offset(_icdf []int16, _icdf_offset int, _ftb int) int {
	var t int64
	var s = ec.Rng
	var d = ec.val
	var r = s >> _ftb
	var ret = _icdf_offset - 1

	for {
		ret++
		t = s
		s = inlines.CapToUInt32(r * int64(_icdf[ret]))
		if d < s {
			continue
		}
		break
	}
	ec.val = inlines.CapToUInt32(d - s)
	ec.Rng = inlines.CapToUInt32(t - s)

	ec.dec_normalize()
	return ret - _icdf_offset
}

func (ec *EntropyCoder) Dec_uint(_ft int64) int64 {
	_ft = inlines.CapToUInt32(_ft)
	var ft int64
	var s int64
	var ftb int
	/*In order to optimize inlines.EC_ILOG(), it is undefined for the value 0.*/
	inlines.OpusAssert(_ft > 1)
	_ft--
	ftb = inlines.EC_ILOG(_ft)
	if ftb > EC_UINT_BITS {
		var t int64
		ftb -= EC_UINT_BITS
		ft = inlines.CapToUInt32((_ft >> ftb) + 1)
		s = inlines.CapToUInt32(ec.Decode(ft))
		ec.Dec_update(s, (s + 1), ft)
		t = inlines.CapToUInt32(s<<ftb | int64(ec.Dec_bits(ftb)))
		if t <= _ft {
			return t
		}
		ec.error = 1
		return _ft
	} else {
		_ft++
		s = inlines.CapToUInt32(ec.Decode(_ft))
		ec.Dec_update(s, s+1, _ft)
		return s
	}

}

func (ec *EntropyCoder) Dec_bits(_bits int) int {

	var window int64
	var available int
	var ret int
	window = ec.end_window
	available = ec.nend_bits
	if available < _bits {
		for {
			window = inlines.CapToUInt32(window | int64(ec.read_byte_from_end()<<available))
			available += EC_SYM_BITS
			if available <= EC_WINDOW_SIZE-EC_SYM_BITS {
				continue
			}
			break
		}

	}
	ret = int(0xFFFFFFFF & (window & ((1 << _bits) - 1)))
	window = window >> _bits
	available = available - _bits
	ec.end_window = inlines.CapToUInt32(window)
	ec.nend_bits = available
	ec.Nbits_total = ec.Nbits_total + _bits
	return ret
}

func (ec *EntropyCoder) enc_carry_out(_c int) {

	if _c != EC_SYM_MAX {
		carry := _c >> EC_SYM_BITS
		if ec.rem >= 0 {
			ec.error |= ec.write_byte(int64(ec.rem + carry))
		}
		if ec.ext > 0 {
			sym := (EC_SYM_MAX + carry) & EC_SYM_MAX
			for ec.ext > 0 {
				ec.error |= ec.write_byte(int64(sym))
				ec.ext--
			}
		}
		ec.rem = _c & EC_SYM_MAX
	} else {
		ec.ext++
	}
}

var i = 0

func (ec *EntropyCoder) enc_normalize() {
	/*If the range is too small, output some bits and rescale it.*/

	for ec.Rng <= EC_CODE_BOT {
		ec.enc_carry_out(int(int32(ec.val >> EC_CODE_SHIFT)))
		/*Move the next-to-high-order symbol into the high-order position.*/
		ec.val = inlines.CapToUInt32((ec.val << EC_SYM_BITS) & (EC_CODE_TOP - 1))
		ec.Rng = inlines.CapToUInt32(ec.Rng << EC_SYM_BITS)
		ec.Nbits_total += EC_SYM_BITS
	}

}

func (ec *EntropyCoder) Enc_init(_buf []byte, buf_ptr int, _size int) {
	ec.buf = _buf
	ec.buf_ptr = buf_ptr
	ec.end_offs = 0
	ec.end_window = 0
	ec.nend_bits = 0
	ec.Nbits_total = EC_CODE_BITS + 1
	ec.Offs = 0
	ec.Rng = inlines.CapToUInt32(EC_CODE_TOP)
	ec.rem = -1
	ec.val = 0
	ec.ext = 0
	ec.Storage = _size
	ec.error = 0
}

func (ec *EntropyCoder) Encode(_fl int64, _fh int64, _ft int64) {
	_fl = inlines.CapToUInt32(_fl)
	_fh = inlines.CapToUInt32(_fh)
	_ft = inlines.CapToUInt32(_ft)
	r := inlines.CapToUInt32(ec.Rng / _ft)
	if _fl > 0 {
		ec.val += inlines.CapToUInt32(ec.Rng - (r * (_ft - _fl)))
		ec.Rng = inlines.CapToUInt32(r * (_fh - _fl))
	} else {
		ec.Rng = inlines.CapToUInt32(ec.Rng - (r * (_ft - _fh)))
	}

	ec.enc_normalize()
}

func (ec *EntropyCoder) Encode_bin(_fl int64, _fh int64, _bits int) {
	_fl = inlines.CapToUInt32(_fl)
	_fh = inlines.CapToUInt32(_fh)
	r := inlines.CapToUInt32(ec.Rng >> _bits)
	if _fl > 0 {
		ec.val = inlines.CapToUInt32(ec.val + inlines.CapToUInt32(ec.Rng-(r*((1<<_bits)-_fl))))
		ec.Rng = inlines.CapToUInt32(r * (_fh - _fl))
	} else {
		ec.Rng = inlines.CapToUInt32(ec.Rng - (r * ((1 << _bits) - _fh)))
	}

	ec.enc_normalize()
}

func (ec *EntropyCoder) Enc_bit_logp(_val int, _logp int) {

	r := ec.Rng
	l := ec.val
	s := r >> _logp
	r -= s
	if _val != 0 {
		ec.val = inlines.CapToUInt32(l + r)
	}
	if _val != 0 {
		ec.Rng = s
	} else {
		ec.Rng = r
	}
	ec.enc_normalize()
}

func (ec *EntropyCoder) Enc_icdf(_s int, _icdf []int16, _ftb int) {
	old := ec.Rng
	r := inlines.CapToUInt32(ec.Rng >> _ftb)
	if _s > 0 {
		ec.val = ec.val + inlines.CapToUInt32(ec.Rng-inlines.CapToUInt32(r*int64(_icdf[_s-1])))
		ec.Rng = (r * int64(_icdf[_s-1]-_icdf[_s]))
	} else {
		ec.Rng = inlines.CapToUInt32(ec.Rng - (r * int64(_icdf[_s])))
	}
	ec.enc_normalize()
	if Debug {
		//panic("enc_icdf")
		fmt.Printf("enc_icdf nbits_total:%d this.rng  :%d old:%d  _ftb:%d _s:%d\r\n", ec.Nbits_total, ec.Rng, old, _ftb, _s)
	}
}

func (ec *EntropyCoder) Enc_icdf_offset(_s int, _icdf []int16, icdf_ptr int, _ftb int) {
	old := ec.Rng
	r := inlines.CapToUInt32(ec.Rng >> _ftb)
	if _s > 0 {
		ec.val = ec.val + inlines.CapToUInt32(ec.Rng-inlines.CapToUInt32(r*int64(_icdf[icdf_ptr+_s-1])))
		ec.Rng = inlines.CapToUInt32(r * inlines.CapToUInt32(int64(_icdf[icdf_ptr+_s-1]-_icdf[icdf_ptr+_s])))
	} else {
		ec.Rng = inlines.CapToUInt32(ec.Rng - r*int64(_icdf[icdf_ptr+_s]))
	}
	ec.enc_normalize()
	if Debug {
		fmt.Printf("enc_icdf_offset nbits_total:%d this.rng  :%d old:%d  _ftb:%d _s:%d\r\n", ec.Nbits_total, ec.Rng, old, _ftb, _s)
	}
}

func (ec *EntropyCoder) Enc_uint(_fl int64, _ft int64) {

	_fl = inlines.CapToUInt32(_fl)
	_ft = inlines.CapToUInt32(_ft)

	var ft int64
	var fl int64
	var ftb int
	/*In order to optimize inlines.EC_ILOG(), it is undefined for the value 0.*/
	inlines.OpusAssert(_ft > 1)
	_ft--
	ftb = inlines.EC_ILOG(_ft)
	if ftb > EC_UINT_BITS {
		ftb -= EC_UINT_BITS
		ft = inlines.CapToUInt32((_ft >> ftb) + 1)
		fl = inlines.CapToUInt32(_fl >> ftb)
		ec.Encode(fl, fl+1, ft)
		ec.Enc_bits(_fl&inlines.CapToUInt32((1<<ftb)-1), ftb)
	} else {
		ec.Encode(_fl, _fl+1, _ft+1)
	}

}

func (ec *EntropyCoder) Enc_bits(_fl int64, _bits int) {
	window := ec.end_window
	used := ec.nend_bits
	if used+_bits > EC_WINDOW_SIZE {
		for used >= EC_SYM_BITS {
			ec.error |= ec.write_byte_at_end(int64(window & EC_SYM_MAX))
			window >>= EC_SYM_BITS
			used -= EC_SYM_BITS
		}
	}
	window |= int64(_fl) << used
	used += _bits
	ec.end_window = window
	ec.nend_bits = used
	ec.Nbits_total += _bits

}

func (ec *EntropyCoder) Enc_patch_initial_bits(_val int64, _nbits int) {
	shift := EC_SYM_BITS - _nbits
	mask := int64(((1 << _nbits) - 1) << shift)
	if ec.Offs > 0 {
		ec.buf[ec.buf_ptr] = (ec.buf[ec.buf_ptr] & ^byte(mask)) | byte(_val<<shift)
	} else if ec.rem >= 0 {
		ec.rem = int((int64(ec.rem) & ^mask) | (_val << shift))
	} else if ec.Rng <= EC_CODE_TOP>>_nbits {
		ec.val = (ec.val & ^(mask << EC_CODE_SHIFT)) | (_val << (EC_CODE_SHIFT + shift))
	} else {
		ec.error = -1
	}
}

func (ec *EntropyCoder) Enc_shrink(_size int) {
	if ec.Offs+ec.end_offs > _size {
		panic("offs + end_offs > size")
	}
	copy(ec.buf[ec.buf_ptr+_size-ec.end_offs:], ec.buf[ec.buf_ptr+ec.Storage-ec.end_offs:ec.buf_ptr+ec.Storage])
	ec.Storage = _size
}

func (ec *EntropyCoder) Range_bytes() int {
	return ec.Offs
}

func (ec *EntropyCoder) Get_error() int {
	return ec.error
}

func (ec *EntropyCoder) Tell() int {
	return ec.Nbits_total - inlines.EC_ILOG(int64(ec.Rng))
}

func (ec *EntropyCoder) Tell_frac() int {
	var nbits int
	var r int
	var l int
	var b int64
	nbits = ec.Nbits_total << BITRES
	l = inlines.EC_ILOG(ec.Rng)
	r = int(ec.Rng >> (l - 16))
	b = int64(int(r>>12) - 8)
	b = inlines.CapToUInt32(b + int64(BoolToInt(r > correction[b])))
	l = int(((l << 3) + int(b)))
	return nbits - l
}

func (ec *EntropyCoder) Enc_done() {
	var window int64
	var used int
	var msk int64
	var end int64
	var l int

	/*We output the minimum number of bits that ensures that the symbols encoded
	  thus far will be decoded correctly regardless of the bits that follow.*/
	l = EC_CODE_BITS - inlines.EC_ILOG(ec.Rng)
	msk = inlines.CapToUInt32(int64(uint32(EC_CODE_TOP-1)) >> l)

	end = inlines.CapToUInt32((inlines.CapToUInt32(ec.val + msk)) & ^msk)

	if (end | msk) >= ec.val+ec.Rng {
		l++
		msk >>= 1
		end = inlines.CapToUInt32((inlines.CapToUInt32(ec.val + msk)) & ^msk)
	}

	for l > 0 {
		ec.enc_carry_out(int(end >> EC_CODE_SHIFT))
		end = inlines.CapToUInt32((end << EC_SYM_BITS) & (EC_CODE_TOP - 1))
		l -= EC_SYM_BITS
	}

	/*If we have a buffered byte flush it into the output buffer.*/
	if ec.rem >= 0 || ec.ext > 0 {
		ec.enc_carry_out(0)
	}

	/*If we have buffered extra bits, flush them as well.*/
	window = ec.end_window
	used = ec.nend_bits

	for used >= EC_SYM_BITS {
		ec.error |= ec.write_byte_at_end(window & EC_SYM_MAX)
		window = window >> EC_SYM_BITS
		used -= EC_SYM_BITS
	}

	/*Clear any excess space and add any remaining extra bits to the last byte.*/
	if ec.error == 0 {
		arrayUtil.MemSetWithOffset(ec.buf, 0, ec.buf_ptr+ec.Offs, ec.Storage-ec.Offs-ec.end_offs)
		if used > 0 {
			/*If there's no range coder data at all, give up.*/
			if ec.end_offs >= ec.Storage {
				ec.error = -1
			} else {
				l = -l
				/*If we've busted, don't add too many extra bits to the last byte; it
				  would corrupt the range coder data, and that's more important.*/
				if ec.Offs+ec.end_offs >= ec.Storage && l < used {
					window = inlines.CapToUInt32(window & ((1 << l) - 1))
					ec.error = -1
				}

				z := ec.buf_ptr + ec.Storage - ec.end_offs - 1
				ec.buf[z] = (byte)(ec.buf[z] | (byte)(window&0xFF))
			}
		}
	}
}
