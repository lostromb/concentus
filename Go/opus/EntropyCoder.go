package opus

import "fmt"

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
	storage     int
	end_offs    int
	end_window  int64
	nend_bits   int
	nbits_total int
	offs        int
	rng         int64
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
	ec.storage = 0
	ec.end_offs = 0
	ec.end_window = 0
	ec.nend_bits = 0
	ec.offs = 0
	ec.rng = 0
	ec.val = 0
	ec.ext = 0
	ec.rem = 0
	ec.error = 0
}

func (ec *EntropyCoder) Assign(other *EntropyCoder) {
	ec.buf = other.buf
	ec.buf_ptr = other.buf_ptr
	ec.storage = other.storage
	ec.end_offs = other.end_offs
	ec.end_window = other.end_window
	ec.nend_bits = other.nend_bits
	ec.nbits_total = other.nbits_total
	ec.offs = other.offs
	ec.rng = other.rng
	ec.val = other.val
	ec.ext = other.ext
	ec.rem = other.rem
	ec.error = other.error
}

func (ec *EntropyCoder) get_buffer() []byte {
	bufCopy := make([]byte, ec.storage)
	copy(bufCopy, ec.buf[ec.buf_ptr:ec.buf_ptr+ec.storage])
	return bufCopy
}

func (ec *EntropyCoder) write_buffer(data []byte, data_ptr int, target_offset int, size int) {
	copy(ec.buf[ec.buf_ptr+target_offset:], data[data_ptr:data_ptr+size])
}

func (ec *EntropyCoder) read_byte() int {
	if ec.offs < ec.storage {
		val := ec.buf[ec.buf_ptr+ec.offs]
		ec.offs++
		return int(val)
	}
	return 0
}

func (ec *EntropyCoder) read_byte_from_end() int {
	if ec.end_offs < ec.storage {
		ec.end_offs++
		return int(ec.buf[ec.buf_ptr+(ec.storage-ec.end_offs)])
	}
	return 0
}

func (ec *EntropyCoder) write_byte(_value int64) int {
	if ec.offs+ec.end_offs >= ec.storage {
		return -1
	}
	ec.buf[ec.buf_ptr+ec.offs] = byte(_value)
	ec.offs++
	return 0
}

func (ec *EntropyCoder) write_byte_at_end(_value int64) int {
	if ec.offs+ec.end_offs >= ec.storage {
		return -1
	}
	ec.end_offs++
	ec.buf[ec.buf_ptr+(ec.storage-ec.end_offs)] = byte(_value)
	return 0
}

func (ec *EntropyCoder) dec_normalize() {
	for ec.rng <= EC_CODE_BOT {
		var sym int
		ec.nbits_total += EC_SYM_BITS
		ec.rng = CapToUInt32(ec.rng << EC_SYM_BITS)

		/*Use up the remaining bits from our last symbol.*/
		sym = ec.rem

		/*Read the next value from the input.*/
		ec.rem = ec.read_byte()

		/*Take the rest of the bits we need from this new symbol.*/
		sym = (sym<<EC_SYM_BITS | ec.rem) >> (EC_SYM_BITS - EC_CODE_EXTRA)

		/*And subtract them from val, capped to be less than EC_CODE_TOP.*/
		ec.val = CapToUInt32(((int64(ec.val) << EC_SYM_BITS) + int64(EC_SYM_MAX & ^sym)) & int64(EC_CODE_TOP-1))
	}
}

func (ec *EntropyCoder) dec_init(_buf []byte, _buf_ptr int, _storage int) {

	ec.buf = _buf
	ec.buf_ptr = _buf_ptr
	ec.storage = _storage
	ec.end_offs = 0
	ec.end_window = 0
	ec.nend_bits = 0
	ec.nbits_total = EC_CODE_BITS + 1 - ((EC_CODE_BITS-EC_CODE_EXTRA)/EC_SYM_BITS)*EC_SYM_BITS
	ec.offs = 0
	ec.rng = 1 << EC_CODE_EXTRA
	ec.rem = ec.read_byte()
	//ec.val = ec.rng - 1 - int64(ec.rem>>(EC_SYM_BITS-EC_CODE_EXTRA))
	ec.val = CapToUInt32(ec.rng - 1 - int64(ec.rem>>(EC_SYM_BITS-EC_CODE_EXTRA)))
	ec.error = 0
	ec.dec_normalize()
}

func (ec *EntropyCoder) decode(_ft int64) int64 {
	_ft = CapToUInt32(_ft)
	ec.ext = CapToUInt32(ec.rng / _ft)
	s := CapToUInt32(ec.val / ec.ext)
	return CapToUInt32(_ft - EC_MINI(CapToUInt32(s+1), _ft))
}

func (ec *EntropyCoder) decode_bin(_bits int) int64 {
	ec.ext = ec.rng >> _bits
	s := CapToUInt32(ec.val / ec.ext)
	return CapToUInt32(CapToUInt32(1<<_bits) - EC_MINI(CapToUInt32(s+1), 1<<_bits))
}

func (ec *EntropyCoder) dec_update(_fl int64, _fh int64, _ft int64) {
	_fl = CapToUInt32(_fl)
	_fh = CapToUInt32(_fh)
	_ft = CapToUInt32(_ft)
	s := CapToUInt32(ec.ext * (_ft - _fh))
	ec.val = ec.val - s
	if _fl > 0 {
		ec.rng = CapToUInt32(ec.ext * (_fh - _fl))
	} else {
		ec.rng = ec.rng - s
	}
	ec.dec_normalize()
}

func (ec *EntropyCoder) dec_bit_logp(_logp int64) int {
	var r int64
	var d int64
	var s int64
	var ret int
	r = ec.rng
	d = ec.val
	s = r >> _logp
	ret = 0
	if d < s {
		ret = 1
	}

	if ret == 0 {
		ec.val = CapToUInt32(d - s)
	}
	if ret != 0 {
		ec.rng = s
	} else {
		ec.rng = r - s
	}
	ec.dec_normalize()
	return ret
}

func (ec *EntropyCoder) dec_icdf(_icdf []int16, _ftb int) int {
	var t int64
	var s = ec.rng
	var d = ec.val
	var r = s >> _ftb
	var ret int = -1
	for {
		ret++
		t = s
		s = CapToUInt32(r * int64(_icdf[ret]))
		if d < s {
			continue
		}
		break
	}
	ec.val = CapToUInt32(d - s)
	ec.rng = CapToUInt32(t - s)
	ec.dec_normalize()
	return ret
}

func (ec *EntropyCoder) dec_icdf_offset(_icdf []int16, _icdf_offset int, _ftb int) int {
	var t int64
	var s = ec.rng
	var d = ec.val
	var r = s >> _ftb
	var ret = _icdf_offset - 1

	for {
		ret++
		t = s
		s = CapToUInt32(r * int64(_icdf[ret]))
		if d < s {
			continue
		}
		break
	}
	ec.val = CapToUInt32(d - s)
	ec.rng = CapToUInt32(t - s)

	ec.dec_normalize()
	return ret - _icdf_offset
}

func (ec *EntropyCoder) dec_uint(_ft int64) int64 {
	_ft = CapToUInt32(_ft)
	var ft int64
	var s int64
	var ftb int
	/*In order to optimize EC_ILOG(), it is undefined for the value 0.*/
	OpusAssert(_ft > 1)
	_ft--
	ftb = EC_ILOG(_ft)
	if ftb > EC_UINT_BITS {
		var t int64
		ftb -= EC_UINT_BITS
		ft = CapToUInt32((_ft >> ftb) + 1)
		s = CapToUInt32(ec.decode(ft))
		ec.dec_update(s, (s + 1), ft)
		t = CapToUInt32(s<<ftb | int64(ec.dec_bits(ftb)))
		if t <= _ft {
			return t
		}
		ec.error = 1
		return _ft
	} else {
		_ft++
		s = CapToUInt32(ec.decode(_ft))
		ec.dec_update(s, s+1, _ft)
		return s
	}

}

func (ec *EntropyCoder) dec_bits(_bits int) int {

	var window int64
	var available int
	var ret int
	window = ec.end_window
	available = ec.nend_bits
	if available < _bits {
		for {
			window = CapToUInt32(window | int64(ec.read_byte_from_end()<<available))
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
	ec.end_window = CapToUInt32(window)
	ec.nend_bits = available
	ec.nbits_total = ec.nbits_total + _bits
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

	for ec.rng <= EC_CODE_BOT {
		ec.enc_carry_out(int(int32(ec.val >> EC_CODE_SHIFT)))
		/*Move the next-to-high-order symbol into the high-order position.*/
		ec.val = CapToUInt32((ec.val << EC_SYM_BITS) & (EC_CODE_TOP - 1))
		ec.rng = CapToUInt32(ec.rng << EC_SYM_BITS)
		ec.nbits_total += EC_SYM_BITS
	}

}

func (ec *EntropyCoder) enc_init(_buf []byte, buf_ptr int, _size int) {
	ec.buf = _buf
	ec.buf_ptr = buf_ptr
	ec.end_offs = 0
	ec.end_window = 0
	ec.nend_bits = 0
	ec.nbits_total = EC_CODE_BITS + 1
	ec.offs = 0
	ec.rng = CapToUInt32(EC_CODE_TOP)
	ec.rem = -1
	ec.val = 0
	ec.ext = 0
	ec.storage = _size
	ec.error = 0
}

func (ec *EntropyCoder) encode(_fl int64, _fh int64, _ft int64) {
	_fl = CapToUInt32(_fl)
	_fh = CapToUInt32(_fh)
	_ft = CapToUInt32(_ft)
	r := CapToUInt32(ec.rng / _ft)
	if _fl > 0 {
		ec.val += CapToUInt32(ec.rng - (r * (_ft - _fl)))
		ec.rng = CapToUInt32(r * (_fh - _fl))
	} else {
		ec.rng = CapToUInt32(ec.rng - (r * (_ft - _fh)))
	}

	ec.enc_normalize()
}

func (ec *EntropyCoder) encode_bin(_fl int64, _fh int64, _bits int) {
	_fl = CapToUInt32(_fl)
	_fh = CapToUInt32(_fh)
	r := CapToUInt32(ec.rng >> _bits)
	if _fl > 0 {
		ec.val = CapToUInt32(ec.val + CapToUInt32(ec.rng-(r*((1<<_bits)-_fl))))
		ec.rng = CapToUInt32(r * (_fh - _fl))
	} else {
		ec.rng = CapToUInt32(ec.rng - (r * ((1 << _bits) - _fh)))
	}

	ec.enc_normalize()
}

func (ec *EntropyCoder) enc_bit_logp(_val int, _logp int) {

	r := ec.rng
	l := ec.val
	s := r >> _logp
	r -= s
	if _val != 0 {
		ec.val = CapToUInt32(l + r)
	}
	if _val != 0 {
		ec.rng = s
	} else {
		ec.rng = r
	}
	ec.enc_normalize()
}

func (ec *EntropyCoder) enc_icdf(_s int, _icdf []int16, _ftb int) {
	old := ec.rng
	r := CapToUInt32(ec.rng >> _ftb)
	if _s > 0 {
		ec.val = ec.val + CapToUInt32(ec.rng-CapToUInt32(r*int64(_icdf[_s-1])))
		ec.rng = (r * int64(_icdf[_s-1]-_icdf[_s]))
	} else {
		ec.rng = CapToUInt32(ec.rng - (r * int64(_icdf[_s])))
	}
	ec.enc_normalize()
	if Debug {
		//panic("enc_icdf")
		fmt.Printf("enc_icdf nbits_total:%d this.rng  :%d old:%d  _ftb:%d _s:%d\r\n", ec.nbits_total, ec.rng, old, _ftb, _s)
	}
}

func (ec *EntropyCoder) enc_icdf_offset(_s int, _icdf []int16, icdf_ptr int, _ftb int) {
	old := ec.rng
	r := CapToUInt32(ec.rng >> _ftb)
	if _s > 0 {
		ec.val = ec.val + CapToUInt32(ec.rng-CapToUInt32(r*int64(_icdf[icdf_ptr+_s-1])))
		ec.rng = CapToUInt32(r * CapToUInt32(int64(_icdf[icdf_ptr+_s-1]-_icdf[icdf_ptr+_s])))
	} else {
		ec.rng = CapToUInt32(ec.rng - r*int64(_icdf[icdf_ptr+_s]))
	}
	ec.enc_normalize()
	if Debug {
		fmt.Printf("enc_icdf_offset nbits_total:%d this.rng  :%d old:%d  _ftb:%d _s:%d\r\n", ec.nbits_total, ec.rng, old, _ftb, _s)
	}
}

func (ec *EntropyCoder) enc_uint(_fl int64, _ft int64) {

	_fl = CapToUInt32(_fl)
	_ft = CapToUInt32(_ft)

	var ft int64
	var fl int64
	var ftb int
	/*In order to optimize EC_ILOG(), it is undefined for the value 0.*/
	OpusAssert(_ft > 1)
	_ft--
	ftb = EC_ILOG(_ft)
	if ftb > EC_UINT_BITS {
		ftb -= EC_UINT_BITS
		ft = CapToUInt32((_ft >> ftb) + 1)
		fl = CapToUInt32(_fl >> ftb)
		ec.encode(fl, fl+1, ft)
		ec.enc_bits(_fl&CapToUInt32((1<<ftb)-1), ftb)
	} else {
		ec.encode(_fl, _fl+1, _ft+1)
	}

}

func (ec *EntropyCoder) enc_bits(_fl int64, _bits int) {
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
	ec.nbits_total += _bits

}

func (ec *EntropyCoder) enc_patch_initial_bits(_val int64, _nbits int) {
	shift := EC_SYM_BITS - _nbits
	mask := int64(((1 << _nbits) - 1) << shift)
	if ec.offs > 0 {
		ec.buf[ec.buf_ptr] = (ec.buf[ec.buf_ptr] & ^byte(mask)) | byte(_val<<shift)
	} else if ec.rem >= 0 {
		ec.rem = int((int64(ec.rem) & ^mask) | (_val << shift))
	} else if ec.rng <= EC_CODE_TOP>>_nbits {
		ec.val = (ec.val & ^(mask << EC_CODE_SHIFT)) | (_val << (EC_CODE_SHIFT + shift))
	} else {
		ec.error = -1
	}
}

func (ec *EntropyCoder) enc_shrink(_size int) {
	if ec.offs+ec.end_offs > _size {
		panic("offs + end_offs > size")
	}
	copy(ec.buf[ec.buf_ptr+_size-ec.end_offs:], ec.buf[ec.buf_ptr+ec.storage-ec.end_offs:ec.buf_ptr+ec.storage])
	ec.storage = _size
}

func (ec *EntropyCoder) range_bytes() int {
	return ec.offs
}

func (ec *EntropyCoder) get_error() int {
	return ec.error
}

func (ec *EntropyCoder) tell() int {
	return ec.nbits_total - EC_ILOG(int64(ec.rng))
}

func (ec *EntropyCoder) tell_frac() int {
	var nbits int
	var r int
	var l int
	var b int64
	nbits = ec.nbits_total << BITRES
	l = EC_ILOG(ec.rng)
	r = int(ec.rng >> (l - 16))
	b = int64(int(r>>12) - 8)
	b = CapToUInt32(b + int64(boolToInt(r > correction[b])))
	l = int(((l << 3) + int(b)))
	return nbits - l
}

func (ec *EntropyCoder) enc_done() {
	var window int64
	var used int
	var msk int64
	var end int64
	var l int

	/*We output the minimum number of bits that ensures that the symbols encoded
	  thus far will be decoded correctly regardless of the bits that follow.*/
	l = EC_CODE_BITS - EC_ILOG(ec.rng)
	msk = CapToUInt32(int64(uint32(EC_CODE_TOP-1)) >> l)

	end = CapToUInt32((CapToUInt32(ec.val + msk)) & ^msk)

	if (end | msk) >= ec.val+ec.rng {
		l++
		msk >>= 1
		end = CapToUInt32((CapToUInt32(ec.val + msk)) & ^msk)
	}

	for l > 0 {
		ec.enc_carry_out(int(end >> EC_CODE_SHIFT))
		end = CapToUInt32((end << EC_SYM_BITS) & (EC_CODE_TOP - 1))
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
		MemSetWithOffset(ec.buf, 0, ec.buf_ptr+ec.offs, ec.storage-ec.offs-ec.end_offs)
		if used > 0 {
			/*If there's no range coder data at all, give up.*/
			if ec.end_offs >= ec.storage {
				ec.error = -1
			} else {
				l = -l
				/*If we've busted, don't add too many extra bits to the last byte; it
				  would corrupt the range coder data, and that's more important.*/
				if ec.offs+ec.end_offs >= ec.storage && l < used {
					window = CapToUInt32(window & ((1 << l) - 1))
					ec.error = -1
				}

				z := ec.buf_ptr + ec.storage - ec.end_offs - 1
				ec.buf[z] = (byte)(ec.buf[z] | (byte)(window&0xFF))
			}
		}
	}
}
