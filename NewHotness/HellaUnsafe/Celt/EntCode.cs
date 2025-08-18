using System.Numerics;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Celt
{
    internal static class EntCode
    {
        internal unsafe struct ec_ctx
        {
            /*Buffered input/output.*/
            internal byte* buf;
            /*The size of the buffer.*/
            internal uint storage;
            /*The offset at which the last byte containing raw bits was read/written.*/
            internal uint end_offs;
            /*Bits that will be read from/written at the end.*/
            internal uint end_window;
            /*Number of valid bits in end_window.*/
            internal int nend_bits;
            /*The total number of whole bits read/written.
              This does not include partial bits currently in the range coder.*/
            internal int nbits_total;
            /*The offset at which the next range coder byte will be read/written.*/
            internal uint offs;
            /*The number of values in the current range.*/
            internal uint rng;
            /*In the decoder: the difference between the top of the current range and
               the input value, minus one.
              In the encoder: the low end of the current range.*/
            internal uint val;
            /*In the decoder: the saved normalization factor from ec_decode().
              In the encoder: the number of oustanding carry propagating symbols.*/
            internal uint ext;
            /*A buffered input/output symbol, awaiting carry propagation.*/
            internal int rem;
            /*Nonzero if an error occurred.*/
            internal int error;
        };

        /*The resolution of fractional-precision bit usage measurements, i.e.,
            3 => 1/8th bits.*/
        internal const int BITRES = 3;

        internal const int EC_WINDOW_SIZE = sizeof(uint);

        /*The number of bits to use for the range-coded part of unsigned integers.*/
        internal const int EC_UINT_BITS = 8;

        /*The number of bits to output at a time.*/
        internal const int EC_SYM_BITS = 8;
        /*The total number of bits in each of the state registers.*/
        internal const int EC_CODE_BITS = 32;
        /*The maximum symbol value.*/
        internal const uint EC_SYM_MAX = ((1U << EC_SYM_BITS) - 1);
        /*Bits to shift by to move a symbol into the high-order position.*/
        internal const int EC_CODE_SHIFT = (EC_CODE_BITS - EC_SYM_BITS - 1);
        /*Carry bit of the high-order range symbol.*/
        internal const uint EC_CODE_TOP = (((uint)1U) << (EC_CODE_BITS - 1));
        /*Low-order bit of the high-order range symbol.*/
        internal const uint EC_CODE_BOT = (EC_CODE_TOP >> EC_SYM_BITS);
        /*The number of bits available for the last, partial symbol in the code field.*/
        internal const int EC_CODE_EXTRA = ((EC_CODE_BITS - 2) % EC_SYM_BITS + 1);

        internal static uint celt_udiv(uint n, uint d)
        {
            ASSERT(d > 0);
            return n / d;
        }

        internal static int celt_udiv(int n, int d)
        {
            ASSERT(d > 0);
            return n / d;
        }

        internal static uint celt_udiv(uint n, ushort d)
        {
            ASSERT(d > 0);
            return n / d;
        }

        internal static int celt_sudiv(int n, int d)
        {
            ASSERT(d > 0);
            return n / d;
        }

        internal static int celt_sudiv(int n, short d)
        {
            ASSERT(d > 0);
            return n / d;
        }

        /// <summary>
        /// returns the value that has fewer higher-order bits, ignoring sign bit (? I think?)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        internal static uint EC_MINI(uint a, uint b)
        {
            return unchecked(a + (b - a & (b < a ? 0xFFFFFFFFU : 0)));
        }

        /// <summary>
        /// Counts leading zeroes.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        internal static int EC_CLZ(uint x)
        {
            return x == 0 ? 0 : BitOperations.LeadingZeroCount(x) - 31;

            //if (x == 0)
            //    return 0;

            //x |= x >> 1;
            //x |= x >> 2;
            //x |= x >> 4;
            //x |= x >> 8;
            //x |= x >> 16;
            //uint y = x - (x >> 1 & 0x55555555);
            //y = (y >> 2 & 0x33333333) + (y & 0x33333333);
            //y = (y >> 4) + y & 0x0f0f0f0f;
            //y += y >> 8;
            //y += y >> 16;
            //y = y & 0x0000003f;
            //return (int)(1 - y);
        }

        internal static int EC_ILOG(int x)
        {
            return EC_ILOG((uint)x);
        }

        /// <summary>
        /// returns inverse base-2 log of a value
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        internal static int EC_ILOG(uint x)
        {
            // Implementation 1
            //if (x == 0)
            //    return 1;
            //x |= x >> 1;
            //x |= x >> 2;
            //x |= x >> 4;
            //x |= x >> 8;
            //x |= x >> 16;
            //uint y = x - (x >> 1 & 0x55555555);
            //y = (y >> 2 & 0x33333333) + (y & 0x33333333);
            //y = (y >> 4) + y & 0x0f0f0f0f;
            //y += y >> 8;
            //y += y >> 16;
            //y = y & 0x0000003f;
            //return (int)y;

            // Implementation 2
            //int ret;
            //int m;
            //ret = x == 0 ? 0 : 1;
            //m = ((x & 0xFFFF0000U) == 0 ? 0 : 1) << 4;
            //x >>= m;
            //ret |= m;
            //m = ((x & 0xFF00U) == 0 ? 0 : 1) << 3;
            //x >>= m;
            //ret |= m;
            //m = ((x & 0xF0U) == 0 ? 0 : 1) << 2;
            //x >>= m;
            //ret |= m;
            //m = ((x & 0xCU) == 0 ? 0 : 1) << 1;
            //x >>= m;
            //ret |= m;
            //ret += (x & 0x2U) == 0 ? 0 : 1;
            //return ret;

            // Implementation 3
            // return 1 - EC_CLZ(_x)

            // Implementation 4
            return x == 0 ? 1 : 32 - BitOperations.LeadingZeroCount(x);
        }

        internal static unsafe int ec_tell(ec_ctx* _this)
        {
            return _this->nbits_total - EC_ILOG(_this->rng);
        }

        #region ENTDEC (Decoder portion)

        internal static unsafe int ec_read_byte(ec_ctx* _this)
        {
            return _this->offs < _this->storage ? _this->buf[_this->offs++] : 0;
        }

        internal static unsafe int ec_read_byte_from_end(ec_ctx* _this)
        {
            return _this->end_offs < _this->storage ?
             _this->buf[_this->storage - ++(_this->end_offs)] : 0;
        }

        /*Normalizes the contents of val and rng so that rng lies entirely in the
   high-order symbol.*/
        internal static unsafe void ec_dec_normalize(ec_ctx* _this)
        {
            /*If the range is too small, rescale it and input some bits.*/
            while (_this->rng <= EC_CODE_BOT)
            {
                int sym;
                _this->nbits_total += EC_SYM_BITS;
                _this->rng <<= EC_SYM_BITS;
                /*Use up the remaining bits from our last symbol.*/
                sym = _this->rem;
                /*Read the next value from the input.*/
                _this->rem = ec_read_byte(_this);
                /*Take the rest of the bits we need from this new symbol.*/
                sym = (sym << EC_SYM_BITS | _this->rem) >> (EC_SYM_BITS - EC_CODE_EXTRA);
                /*And subtract them from val, capped to be less than EC_CODE_TOP.*/
                _this->val = (uint)((_this->val << EC_SYM_BITS) + (EC_SYM_MAX & ~sym)) & (EC_CODE_TOP - 1);
            }
        }

        internal static unsafe void ec_dec_init(ec_ctx* _this, byte* _buf, uint _storage)
        {
            _this->buf = _buf;
            _this->storage = _storage;
            _this->end_offs = 0;
            _this->end_window = 0;
            _this->nend_bits = 0;
            /*This is the offset from which ec_tell() will subtract partial bits.
              The final value after the ec_dec_normalize() call will be the same as in
               the encoder, but we have to compensate for the bits that are added there.*/
            _this->nbits_total = EC_CODE_BITS + 1
             - ((EC_CODE_BITS - EC_CODE_EXTRA) / EC_SYM_BITS) * EC_SYM_BITS;
            _this->offs = 0;
            _this->rng = 1U << EC_CODE_EXTRA;
            _this->rem = ec_read_byte(_this);
            _this->val = _this->rng - 1 - (uint)(_this->rem >> (EC_SYM_BITS - EC_CODE_EXTRA));
            _this->error = 0;
            /*Normalize the interval.*/
            ec_dec_normalize(_this);
        }

        internal static unsafe uint ec_decode(ec_ctx* _this, uint _ft)
        {
            uint s;
            _this->ext = celt_udiv(_this->rng, _ft);
            s = (uint)(_this->val / _this->ext);
            return _ft - EC_MINI(s + 1, _ft);
        }

        internal static unsafe uint ec_decode_bin(ec_ctx* _this, uint _bits)
        {
            uint s;
            _this->ext = _this->rng >> (int)_bits;
            s = (uint)(_this->val / _this->ext);
            return (1U << (int)_bits) - EC_MINI(s + 1U, 1U << (int)_bits);
        }

        internal static unsafe void ec_dec_update(ec_ctx* _this, uint _fl, uint _fh, uint _ft)
        {
            uint s;
            s = IMUL32(_this->ext, _ft - _fh);
            _this->val -= s;
            _this->rng = _fl > 0 ? IMUL32(_this->ext, _fh - _fl) : _this->rng - s;
            ec_dec_normalize(_this);
        }

        /*The probability of having a "one" is 1/(1<<_logp).*/
        internal static unsafe int ec_dec_bit_logp(ec_ctx* _this, uint _logp)
        {
            uint r;
            uint d;
            uint s;
            int ret;
            r = _this->rng;
            d = _this->val;
            s = r >> (int)_logp;
            ret = (d < s) ? 1 : 0;
            if (ret == 0) _this->val = d - s;
            _this->rng = ret != 0 ? s : r - s;
            ec_dec_normalize(_this);
            return ret;
        }

        internal static unsafe int ec_dec_icdf(ec_ctx* _this, in byte* _icdf, uint _ftb)
        {
            uint r;
            uint d;
            uint s;
            uint t;
            int ret;
            s = _this->rng;
            d = _this->val;
            r = s >> (int)_ftb;
            ret = -1;
            do
            {
                t = s;
                s = IMUL32(r, _icdf[++ret]);
            }
            while (d < s);
            _this->val = d - s;
            _this->rng = t - s;
            ec_dec_normalize(_this);
            return ret;
        }

        internal static unsafe int ec_dec_icdf16(ec_ctx* _this, in ushort* _icdf, uint _ftb)
        {
            uint r;
            uint d;
            uint s;
            uint t;
            int ret;
            s = _this->rng;
            d = _this->val;
            r = s >> (int)_ftb;
            ret = -1;
            do
            {
                t = s;
                s = IMUL32(r, _icdf[++ret]);
            }
            while (d < s);
            _this->val = d - s;
            _this->rng = t - s;
            ec_dec_normalize(_this);
            return ret;
        }

        internal static unsafe uint ec_dec_uint(ec_ctx* _this, uint _ft)
        {
            uint ft;
            uint s;
            int ftb;
            /*In order to optimize EC_ILOG(), it is undefined for the value 0.*/
            ASSERT(_ft > 1);
            _ft--;
            ftb = EC_ILOG(_ft);
            if (ftb > EC_UINT_BITS)
            {
                uint t;
                ftb -= EC_UINT_BITS;
                ft = (uint)(_ft >> ftb) + 1;
                s = ec_decode(_this, ft);
                ec_dec_update(_this, s, s + 1, ft);
                t = (uint)s << ftb | ec_dec_bits(_this, (uint)ftb);
                if (t <= _ft) return t;
                _this->error = 1;
                return _ft;
            }
            else
            {
                _ft++;
                s = ec_decode(_this, (uint)_ft);
                ec_dec_update(_this, s, s + 1, (uint)_ft);
                return s;
            }
        }

        internal static unsafe uint ec_dec_bits(ec_ctx* _this, uint _bits)
        {
            uint window;
            int available;
            uint ret;
            window = _this->end_window;
            available = _this->nend_bits;
            if ((uint)available < _bits)
            {
                do
                {
                    window |= (uint)ec_read_byte_from_end(_this) << available;
                    available += EC_SYM_BITS;
                }
                while (available <= EC_WINDOW_SIZE - EC_SYM_BITS);
            }
            ret = (uint)window & (((uint)1 << (int)_bits) - 1U);
            window >>= (int)_bits;
            available -= (int)_bits;
            _this->end_window = window;
            _this->nend_bits = available;
            _this->nbits_total += (int)_bits;
            return ret;
        }

        #endregion

        #region ENTENC (Encoder portion)

        internal static unsafe int ec_write_byte(ec_ctx* _this, uint _value)
        {
            if (_this->offs + _this->end_offs >= _this->storage) return -1;
            _this->buf[_this->offs++] = (byte)_value;
            return 0;
        }

        internal static unsafe int ec_write_byte_at_end(ec_ctx* _this, uint _value)
        {
            if (_this->offs + _this->end_offs >= _this->storage) return -1;
            _this->buf[_this->storage - ++(_this->end_offs)] = (byte)_value;
            return 0;
        }

        /*Outputs a symbol, with a carry bit.
  If there is a potential to propagate a carry over several symbols, they are
   buffered until it can be determined whether or not an actual carry will
   occur.
  If the counter for the buffered symbols overflows, then the stream becomes
   undecodable.
  This gives a theoretical limit of a few billion symbols in a single packet on
   32-bit systems.
  The alternative is to truncate the range in order to force a carry, but
   requires similar carry tracking in the decoder, needlessly slowing it down.*/
        internal static unsafe void ec_enc_carry_out(ec_ctx* _this, int _c)
        {
            if (_c != EC_SYM_MAX)
            {
                /*No further carry propagation possible, flush buffer.*/
                int carry;
                carry = _c >> EC_SYM_BITS;
                /*Don't output a byte on the first write.
                  This compare should be taken care of by branch-prediction thereafter.*/
                if (_this->rem >= 0) _this->error |= ec_write_byte(_this, (uint)(_this->rem + carry));
                if (_this->ext > 0)
                {
                    uint sym = (uint)(EC_SYM_MAX + carry) & EC_SYM_MAX;
                    do _this->error |= ec_write_byte(_this, sym);
                    while (--(_this->ext) > 0);
                }
                _this->rem = (int)(_c & EC_SYM_MAX);
            }
            else _this->ext++;
        }

        internal static unsafe void ec_enc_normalize(ec_ctx* _this)
        {
            /*If the range is too small, output some bits and rescale it.*/
            while (_this->rng <= EC_CODE_BOT)
            {
                ec_enc_carry_out(_this, (int)(_this->val >> EC_CODE_SHIFT));
                /*Move the next-to-high-order symbol into the high-order position.*/
                _this->val = (_this->val << EC_SYM_BITS) & (EC_CODE_TOP - 1);
                _this->rng <<= EC_SYM_BITS;
                _this->nbits_total += EC_SYM_BITS;
            }
        }

        internal static unsafe void ec_enc_init(ec_ctx* _this, byte* _buf, uint _size)
        {
            _this->buf = _buf;
            _this->end_offs = 0;
            _this->end_window = 0;
            _this->nend_bits = 0;
            /*This is the offset from which ec_tell() will subtract partial bits.*/
            _this->nbits_total = EC_CODE_BITS + 1;
            _this->offs = 0;
            _this->rng = EC_CODE_TOP;
            _this->rem = -1;
            _this->val = 0;
            _this->ext = 0;
            _this->storage = _size;
            _this->error = 0;
        }

        internal static unsafe void ec_encode(ec_ctx* _this, uint _fl, uint _fh, uint _ft)
        {
            uint r;
            r = celt_udiv(_this->rng, _ft);
            if (_fl > 0)
            {
                _this->val += _this->rng - IMUL32(r, (_ft - _fl));
                _this->rng = IMUL32(r, (_fh - _fl));
            }
            else _this->rng -= IMUL32(r, (_ft - _fh));
            ec_enc_normalize(_this);
        }

        internal static unsafe void ec_encode_bin(ec_ctx* _this, uint _fl, uint _fh, uint _bits)
        {
            uint r;
            r = _this->rng >> (int)_bits;
            if (_fl > 0)
            {
                _this->val += _this->rng - IMUL32(r, ((1U << (int)_bits) - _fl));
                _this->rng = IMUL32(r, (_fh - _fl));
            }
            else _this->rng -= IMUL32(r, ((1U << (int)_bits) - _fh));
            ec_enc_normalize(_this);
        }

        /*The probability of having a "one" is 1/(1<<_logp).*/
        internal static unsafe void ec_enc_bit_logp(ec_ctx* _this, int _val, uint _logp)
        {
            uint r;
            uint s;
            uint l;
            r = _this->rng;
            l = _this->val;
            s = r >> (int)_logp;
            r -= s;
            if (_val != 0) _this->val = l + r;
            _this->rng = _val != 0 ? s : r;
            ec_enc_normalize(_this);
        }

        internal static unsafe void ec_enc_icdf(ec_ctx* _this, int _s, in byte* _icdf, uint _ftb)
        {
            uint r = _this->rng >> (int)_ftb;
            if (_s > 0)
            {
                _this->val += _this->rng - IMUL32(r, _icdf[_s - 1]);
                _this->rng = IMUL32(r, (uint)(_icdf[_s - 1] - _icdf[_s]));
            }
            else _this->rng -= IMUL32(r, _icdf[_s]);
            ec_enc_normalize(_this);
        }

        internal static unsafe void ec_enc_icdf16(ec_ctx* _this, int _s, in ushort* _icdf, uint _ftb)
        {
            uint r = _this->rng >> (int)_ftb;
            if (_s > 0)
            {
                _this->val += _this->rng - IMUL32(r, _icdf[_s - 1]);
                _this->rng = IMUL32(r, (uint)(_icdf[_s - 1] - _icdf[_s]));
            }
            else _this->rng -= IMUL32(r, _icdf[_s]);
            ec_enc_normalize(_this);
        }

        internal static unsafe void ec_enc_uint(ec_ctx* _this, uint _fl, uint _ft)
        {
            uint ft;
            uint fl;
            int ftb;
            /*In order to optimize EC_ILOG(), it is undefined for the value 0.*/
            ASSERT(_ft > 1);
            _ft--;
            ftb = EC_ILOG(_ft);
            if (ftb > EC_UINT_BITS)
            {
                ftb -= EC_UINT_BITS;
                ft = (_ft >> ftb) + 1;
                fl = (uint)(_fl >> ftb);
                ec_encode(_this, fl, fl + 1, ft);
                ec_enc_bits(_this, _fl & (((uint)1 << ftb) - 1U), (uint)ftb);
            }
            else ec_encode(_this, _fl, _fl + 1, _ft + 1);
        }

        internal static unsafe void ec_enc_bits(ec_ctx* _this, uint _fl, uint _bits)
        {
            uint window;
            int used;
            window = _this->end_window;
            used = _this->nend_bits;
            ASSERT(_bits > 0);
            if (used + _bits > EC_WINDOW_SIZE)
            {
                do
                {
                    _this->error |= ec_write_byte_at_end(_this, (uint)window & EC_SYM_MAX);
                    window >>= EC_SYM_BITS;
                    used -= EC_SYM_BITS;
                }
                while (used >= EC_SYM_BITS);
            }
            window |= (uint)_fl << used;
            used += (int)_bits;
            _this->end_window = window;
            _this->nend_bits = used;
            _this->nbits_total += (int)_bits;
        }

        internal static unsafe void ec_enc_patch_initial_bits(ec_ctx* _this, uint _val, uint _nbits)
        {
            int shift;
            uint mask;
            ASSERT(_nbits <= EC_SYM_BITS);
            shift = (int)(EC_SYM_BITS - _nbits);
            mask = ((1U << (int)_nbits) - 1) << shift;
            if (_this->offs > 0)
            {
                /*The first byte has been finalized.*/
                _this->buf[0] = (byte)((_this->buf[0] & ~mask) | _val << shift);
            }
            else if (_this->rem >= 0)
            {
                /*The first byte is still awaiting carry propagation.*/
                _this->rem = (int)((_this->rem & ~mask) | _val << shift);
            }
            else if (_this->rng <= (EC_CODE_TOP >> (int)_nbits))
            {
                /*The renormalization loop has never been run.*/
                _this->val = (_this->val & ~((uint)mask << EC_CODE_SHIFT)) |
                 (uint)_val << (EC_CODE_SHIFT + shift);
            }
            /*The encoder hasn't even encoded _nbits of data yet.*/
            else _this->error = -1;
        }

        internal static unsafe void ec_enc_shrink(ec_ctx* _this, uint _size)
        {
            ASSERT(_this->offs + _this->end_offs <= _size);
            OPUS_MOVE(_this->buf + _size - _this->end_offs,
             _this->buf + _this->storage - _this->end_offs, _this->end_offs);
            _this->storage = _size;
        }

        internal static unsafe void ec_enc_done(ec_ctx* _this)
        {
            uint window;
            int used;
            uint msk;
            uint end;
            int l;
            /*We output the minimum number of bits that ensures that the symbols encoded
               thus far will be decoded correctly regardless of the bits that follow.*/
            l = EC_CODE_BITS - EC_ILOG(_this->rng);
            msk = (EC_CODE_TOP - 1) >> l;
            end = (_this->val + msk) & ~msk;
            if ((end | msk) >= _this->val + _this->rng)
            {
                l++;
                msk >>= 1;
                end = (_this->val + msk) & ~msk;
            }
            while (l > 0)
            {
                ec_enc_carry_out(_this, (int)(end >> EC_CODE_SHIFT));
                end = (end << EC_SYM_BITS) & (EC_CODE_TOP - 1);
                l -= EC_SYM_BITS;
            }
            /*If we have a buffered byte flush it into the output buffer.*/
            if (_this->rem >= 0 || _this->ext > 0) ec_enc_carry_out(_this, 0);
            /*If we have buffered extra bits, flush them as well.*/
            window = _this->end_window;
            used = _this->nend_bits;
            while (used >= EC_SYM_BITS)
            {
                _this->error |= ec_write_byte_at_end(_this, (uint)window & EC_SYM_MAX);
                window >>= EC_SYM_BITS;
                used -= EC_SYM_BITS;
            }
            /*Clear any excess space and add any remaining extra bits to the last byte.*/
            if (_this->error == 0)
            {
                OPUS_CLEAR(_this->buf + _this->offs,
                 _this->storage - _this->offs - _this->end_offs);
                if (used > 0)
                {
                    /*If there's no range coder data at all, give up.*/
                    if (_this->end_offs >= _this->storage) _this->error = -1;
                    else
                    {
                        l = -l;
                        /*If we've busted, don't add too many extra bits to the last byte; it
                           would corrupt the range coder data, and that's more important.*/
                        if (_this->offs + _this->end_offs >= _this->storage && l < used)
                        {
                            window &= (1U << l) - 1;
                            _this->error = -1;
                        }
                        _this->buf[_this->storage - _this->end_offs - 1] |= (byte)window;
                    }
                }
            }
        }

        #endregion
    }
}
