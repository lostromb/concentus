using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common
{
    /*A range decoder.
  This is an entropy decoder based upon \cite{Mar79}, which is itself a
   rediscovery of the FIFO arithmetic code introduced by \cite{Pas76}.
  It is very similar to arithmetic encoding, except that encoding is done with
   digits in any base, instead of with bits, and so it is faster when using
   larger bases (i.e.: a byte).
  The author claims an average waste of $\frac{1}{2}\log_b(2b)$ bits, where $b$
   is the base, longer than the theoretical optimum, but to my knowledge there
   is no published justification for this claim.
  This only seems true when using near-infinite precision arithmetic so that
   the process is carried out with no rounding errors.

  An excellent description of implementation details is available at
   http://www.arturocampos.com/ac_range.html
  A recent work \cite{MNW98} which proposes several changes to arithmetic
   encoding for efficiency actually re-discovers many of the principles
   behind range encoding, and presents a good theoretical analysis of them.

  End of stream is handled by writing out the smallest number of bits that
   ensures that the stream will be correctly decoded regardless of the value of
   any subsequent bits.
  ec_tell() can be used to determine how many bits were needed to decode
   all the symbols thus far; other data can be packed in the remaining bits of
   the input buffer.
  @PHDTHESIS{Pas76,
    author="Richard Clark Pasco",
    title="Source coding algorithms for fast data compression",
    school="Dept. of Electrical Engineering, Stanford University",
    address="Stanford, CA",
    month=May,
    year=1976
  }
  @INPROCEEDINGS{Mar79,
   author="Martin, G.N.N.",
   title="Range encoding: an algorithm for removing redundancy from a digitised
    message",
   booktitle="Video & Data Recording Conference",
   year=1979,
   address="Southampton",
   month=Jul
  }
  @ARTICLE{MNW98,
   author="Alistair Moffat and Radford Neal and Ian H. Witten",
   title="Arithmetic Coding Revisited",
   journal="{ACM} Transactions on Information Systems",
   year=1998,
   volume=16,
   number=3,
   pages="256--294",
   month=Jul,
   URL="http://www.stanford.edu/class/ee398a/handouts/papers/Moffat98ArithmCoding.pdf"
  }*/
    internal static class EntropyCoder
    {
        private const bool EC_DIFF = false;

        private const int EC_WINDOW_SIZE = ((int)sizeof(uint) * 8);

        ///*The number of bits to use for the range-coded part of uint integers.*/
        private const int EC_UINT_BITS = 8;

        ///*The resolution of fractional-precision bit usage measurements, i.e.,
        //   3 => 1/8th bits.*/
        public const int BITRES = 3;

        /*The number of bits to output at a time.*/
        private const int EC_SYM_BITS = (8);

        /*The total number of bits in each of the state registers.*/
        private const int EC_CODE_BITS = (32);

        /*The maximum symbol value.*/
        private const uint EC_SYM_MAX = ((1U << EC_SYM_BITS) - 1);

        /*Bits to shift by to move a symbol into the high-order position.*/
        private const uint EC_CODE_SHIFT = (EC_CODE_BITS - EC_SYM_BITS - 1);

        /*Carry bit of the high-order range symbol.*/
        private const uint EC_CODE_TOP = ((1U) << (EC_CODE_BITS - 1));

        /*Low-order bit of the high-order range symbol.*/
        private const uint EC_CODE_BOT = (EC_CODE_TOP >> EC_SYM_BITS);

        /*The number of bits available for the last, partial symbol in the code field.*/
        private const int EC_CODE_EXTRA = ((EC_CODE_BITS - 2) % EC_SYM_BITS + 1);

        internal static int ec_read_byte(ec_ctx _this)
        {
            return _this.offs < _this.storage ? _this.buf[_this.offs++] : 0;
        }

        internal static int ec_read_byte_from_end(ec_ctx _this)
        {
            return _this.end_offs < _this.storage ?
             _this.buf[(_this.storage - ++(_this.end_offs))] : 0;
        }

        internal static int ec_write_byte(ec_ctx _this, uint _value)
        {
            if (EC_DIFF) Debug.WriteLine("1a 0x{0:x}", (uint)_value);
            if (EC_DIFF) Debug.WriteLine("8a 0x{0:x}", (uint)_this.nbits_total);
            if (_this.offs + _this.end_offs >= _this.storage)
            {
                return -1;
            }
            _this.buf[_this.offs++] = (byte)_value;
            return 0;
        }

        internal static int ec_write_byte_at_end(ec_ctx _this, uint _value)
        {
            if (EC_DIFF) Debug.WriteLine("1b 0x{0:x}", (uint)_value);
            if (EC_DIFF) Debug.WriteLine("8b 0x{0:x}", (uint)_this.nbits_total);
            if (_this.offs + _this.end_offs >= _this.storage)
            {
                return -1;
            }

            _this.buf[(_this.storage - ++(_this.end_offs))] = (byte)_value;
            return 0;
        }

        /// <summary>
        /// Normalizes the contents of val and rng so that rng lies entirely in the high-order symbol.
        /// </summary>
        /// <param name="_this"></param>
        internal static void ec_dec_normalize(ec_ctx _this)
        {
            /*If the range is too small, rescale it and input some bits.*/
            while (_this.rng <= EC_CODE_BOT)
            {
                int sym;
                _this.nbits_total += EC_SYM_BITS;
                if (EC_DIFF) Debug.WriteLine("1ic 0x{0:x}", (uint)_this.nbits_total);
                _this.rng <<= EC_SYM_BITS;

                /*Use up the remaining bits from our last symbol.*/
                sym = _this.rem;

                /*Read the next value from the input.*/
                _this.rem = ec_read_byte(_this);
                if (EC_DIFF) Debug.WriteLine("1c 0x{0:x}", (uint)_this.rem);

                /*Take the rest of the bits we need from this new symbol.*/
                sym = (sym << EC_SYM_BITS | _this.rem) >> (EC_SYM_BITS - EC_CODE_EXTRA);
                if (EC_DIFF) Debug.WriteLine("1d 0x{0:x}", (uint)sym);

                /*And subtract them from val, capped to be less than EC_CODE_TOP.*/
                _this.val = (uint)((_this.val << EC_SYM_BITS) + (EC_SYM_MAX & ~sym)) & (EC_CODE_TOP - 1);
            }
        }

        internal static void ec_dec_init(ec_ctx _this, Pointer<byte> _buf, uint _storage)
        {
            _this.buf = _buf;
            _this.storage = _storage;
            _this.end_offs = 0;
            _this.end_window = 0;
            _this.nend_bits = 0;
            /*This is the offset from which ec_tell() will subtract partial bits.
              The final value after the ec_dec_normalize() call will be the same as in
               the encoder, but we have to compensate for the bits that are added there.*/
            _this.nbits_total = EC_CODE_BITS + 1
            - ((EC_CODE_BITS - EC_CODE_EXTRA) / EC_SYM_BITS) * EC_SYM_BITS;
            if (EC_DIFF) Debug.WriteLine("1id 0x{0:x}", (uint)_this.nbits_total);
            _this.offs = 0;
            _this.rng = 1U << EC_CODE_EXTRA;
            _this.rem = ec_read_byte(_this);
            _this.val = _this.rng - 1 - (uint)(_this.rem >> (EC_SYM_BITS - EC_CODE_EXTRA));
            _this.error = 0;
            /*Normalize the interval.*/
            ec_dec_normalize(_this);
        }

        internal static uint ec_decode(ec_ctx _this, uint _ft)
        {
            uint s;
            _this.ext = _this.rng / _ft;
            s = (uint)(_this.val / _this.ext);
            return _ft - Inlines.EC_MINI(s + 1, _ft);
        }

        internal static uint ec_decode_bin(ec_ctx _this, uint _bits)
        {
            uint s;
            _this.ext = _this.rng >> (int)_bits;
            s = (uint)(_this.val / _this.ext);
            return (1U << (int)_bits) - Inlines.EC_MINI(s + 1U, 1U << (int)_bits);
        }

        internal static void ec_dec_update(ec_ctx _this, uint _fl, uint _fh, uint _ft)
        {
            uint s;
            s = _this.ext * (_ft - _fh);
            _this.val -= s;
            _this.rng = _fl > 0 ? _this.ext * (_fh - _fl) : _this.rng - s;
            ec_dec_normalize(_this);
        }

        /// <summary>
        /// The probability of having a "one" is 1/(1<<_logp).
        /// </summary>
        /// <param name="_this"></param>
        /// <param name="_logp"></param>
        /// <returns></returns>
        internal static int ec_dec_bit_logp(ec_ctx _this, uint _logp)
        {
            uint r;
            uint d;
            uint s;
            int ret;
            r = _this.rng;
            d = _this.val;
            s = r >> (int)_logp;
            ret = d < s ? 1 : 0;
            if (ret == 0) _this.val = d - s;
            _this.rng = ret != 0 ? s : r - s;
            ec_dec_normalize(_this);
            return ret;
        }

        internal static int ec_dec_icdf(ec_ctx _this, Pointer<byte> _icdf, uint _ftb)
        {
            uint r;
            uint d;
            uint s;
            uint t;
            int ret;
            s = _this.rng;
            d = _this.val;
            r = s >> (int)_ftb;
            ret = -1;
            do
            {
                t = s;
                s = r * _icdf[++ret];
            }
            while (d < s);
            _this.val = d - s;
            _this.rng = t - s;
            ec_dec_normalize(_this);
            return ret;
        }

        internal static uint ec_dec_uint(ec_ctx _this, uint _ft)
        {
            uint ft;
            uint s;
            int ftb;
            /*In order to optimize EC_ILOG(), it is undefined for the value 0.*/
            Inlines.OpusAssert(_ft > 1);
            _ft--;
            ftb = Inlines.EC_ILOG(_ft);
            if (ftb > EC_UINT_BITS)
            {
                uint t;
                ftb -= EC_UINT_BITS;
                ft = (uint)(_ft >> ftb) + 1;
                s = ec_decode(_this, ft);
                ec_dec_update(_this, s, s + 1, ft);
                t = (uint)s << ftb | ec_dec_bits(_this, (uint)ftb);
                if (t <= _ft) return t;
                _this.error = 1;
                return _ft;
            }
            else {
                _ft++;
                s = ec_decode(_this, (uint)_ft);
                ec_dec_update(_this, s, s + 1, (uint)_ft);
                return s;
            }
        }

        internal static uint ec_dec_bits(ec_ctx _this, uint _bits)
        {
            uint window;
            int available;
            uint ret;
            window = _this.end_window;
            available = _this.nend_bits;
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
            window = window >> (int)_bits;
            available = available - (int)_bits;
            _this.end_window = window;
            _this.nend_bits = available;
            _this.nbits_total = _this.nbits_total + (int)_bits;
            if (EC_DIFF) Debug.WriteLine("1if 0x{0:x}", (uint)_this.nbits_total);
            return ret;
        }

        /// <summary>
        /// Outputs a symbol, with a carry bit.
        /// If there is a potential to propagate a carry over several symbols, they are
        /// buffered until it can be determined whether or not an actual carry will
        /// occur.
        /// If the counter for the buffered symbols overflows, then the stream becomes
        /// undecodable.
        /// This gives a theoretical limit of a few billion symbols in a single packet on
        /// 32-bit systems.
        /// The alternative is to truncate the range in order to force a carry, but
        /// requires similar carry tracking in the decoder, needlessly slowing it down.
        /// </summary>
        /// <param name="_this"></param>
        /// <param name="_c"></param>
        internal static void ec_enc_carry_out(ec_ctx _this, int _c)
        {
            if (EC_DIFF) Debug.WriteLine("1e 0x{0:x}", (uint)_c);
            if (EC_DIFF) Debug.WriteLine("8c 0x{0:x}", (uint)_this.nbits_total);
            if (_c != EC_SYM_MAX)
            {
                /*No further carry propagation possible, flush buffer.*/
                int carry;
                carry = _c >> EC_SYM_BITS;

                /*Don't output a byte on the first write.
                  This compare should be taken care of by branch-prediction thereafter.*/
                if (_this.rem >= 0)
                {
                    _this.error |= ec_write_byte(_this, (uint)(_this.rem + carry));
                }

                if (_this.ext > 0)
                {
                    uint sym;
                    sym = (EC_SYM_MAX + (uint)carry) & EC_SYM_MAX;
                    do _this.error |= ec_write_byte(_this, sym);
                    while (--(_this.ext) > 0);
                }

                _this.rem = (int)((uint)_c & EC_SYM_MAX);
                if (EC_DIFF) Debug.WriteLine("6a 0x{0:x}", (uint)_this.rem);
            }
            else
            {
                _this.ext++;
            }
            if (EC_DIFF) Debug.WriteLine("6b 0x{0:x}", (uint)_this.ext);
        }

        internal static void ec_enc_normalize(ec_ctx _this)
        {
            /*If the range is too small, output some bits and rescale it.*/
            if (EC_DIFF) Debug.WriteLine("8d 0x{0:x}", (uint)_this.nbits_total);
            while (_this.rng <= EC_CODE_BOT)
            {
                if (EC_DIFF) Debug.WriteLine("8e 0x{0:x}", (uint)_this.nbits_total);
                ec_enc_carry_out(_this, (int)(_this.val >> (int)EC_CODE_SHIFT));
                /*Move the next-to-high-order symbol into the high-order position.*/
                _this.val = (_this.val << EC_SYM_BITS) & (EC_CODE_TOP - 1);
                if (EC_DIFF) Debug.WriteLine("1i 0x{0:x}", (uint)_this.val);
                _this.rng = _this.rng << EC_SYM_BITS;
                if (EC_DIFF) Debug.WriteLine("7a 0x{0:x}", (uint)_this.nbits_total);
                _this.nbits_total += EC_SYM_BITS;
                if (EC_DIFF) Debug.WriteLine("1ia 0x{0:x}", (uint)_this.nbits_total);
                if (EC_DIFF) Debug.WriteLine("6c 0x{0:x}", (uint)_this.rng);
            }
        }

        internal static void ec_enc_init(ec_ctx _this, Pointer<byte> _buf, uint _size)
        {
            _this.buf = _buf;
            _this.end_offs = 0;
            _this.end_window = 0;
            _this.nend_bits = 0;
            /*This is the offset from which ec_tell() will subtract partial bits.*/
            _this.nbits_total = EC_CODE_BITS + 1;
            _this.offs = 0;
            _this.rng = EC_CODE_TOP;
            _this.rem = -1;
            _this.val = 0;
            _this.ext = 0;
            _this.storage = _size;
            _this.error = 0;
        }

        internal static void ec_encode(ec_ctx _this, uint _fl, uint _fh, uint _ft)
        {
            if (EC_DIFF) Debug.WriteLine("1f 0x{0:x}", (uint)_fl);
            if (EC_DIFF) Debug.WriteLine("1g 0x{0:x}", (uint)_fh);
            if (EC_DIFF) Debug.WriteLine("1h 0x{0:x}", (uint)_ft);
            if (EC_DIFF) Debug.WriteLine("8f 0x{0:x}", (uint)_this.nbits_total);
            uint r;
            r = _this.rng / _ft;
            if (_fl > 0)
            {
                _this.val += _this.rng - (r * (_ft - _fl));
                _this.rng = (r * (_fh - _fl));
            }
            else
            {
                _this.rng -= (r * (_ft - _fh));
            }

            if (EC_DIFF) Debug.WriteLine("6d 0x{0:x}", (uint)_this.val);
            if (EC_DIFF) Debug.WriteLine("6e 0x{0:x}", (uint)_this.rng);
            ec_enc_normalize(_this);
        }

        internal static void ec_encode_bin(ec_ctx _this, uint _fl, uint _fh, uint _bits)
        {
            if (EC_DIFF) Debug.WriteLine("1i 0x{0:x}", (uint)_fl);
            if (EC_DIFF) Debug.WriteLine("1j 0x{0:x}", (uint)_fh);
            if (EC_DIFF) Debug.WriteLine("1k 0x{0:x}", (uint)_bits);
            if (EC_DIFF) Debug.WriteLine("8g 0x{0:x}", (uint)_this.nbits_total);
            uint r;
            r = _this.rng >> (int)_bits;
            if (_fl > 0)
            {
                _this.val += _this.rng - (r * ((1U << (int)_bits) - _fl));
                _this.rng = (r * (_fh - _fl));
            }
            else _this.rng -= (r * ((1U << (int)_bits) - _fh));
            if (EC_DIFF) Debug.WriteLine("6g 0x{0:x}", (uint)_this.val);
            if (EC_DIFF) Debug.WriteLine("6h 0x{0:x}", (uint)_this.rng);
            ec_enc_normalize(_this);
        }

        /*The probability of having a "one" is 1/(1<<_logp).*/
        internal static void ec_enc_bit_logp(ec_ctx _this, int _val, uint _logp)
        {
            if (EC_DIFF) Debug.WriteLine("1l 0x{0:x}", (uint)_val);
            if (EC_DIFF) Debug.WriteLine("1m 0x{0:x}", (uint)_logp);
            if (EC_DIFF) Debug.WriteLine("8h 0x{0:x}", (uint)_this.nbits_total);
            uint r;
            uint s;
            uint l;
            r = _this.rng;
            l = _this.val;
            s = r >> (int)_logp;
            r -= s;
            if (_val != 0)
            {
                _this.val = l + r;
            }

            _this.rng = _val != 0 ? s : r;
            if (EC_DIFF) Debug.WriteLine("6j 0x{0:x}", (uint)_this.val);
            if (EC_DIFF) Debug.WriteLine("6k 0x{0:x}", (uint)_this.rng);
            ec_enc_normalize(_this);
        }

        internal static void ec_enc_icdf(ec_ctx _this, int _s, Pointer<byte> _icdf, uint _ftb)
        {
            if (EC_DIFF) Debug.WriteLine("1n 0x{0:x}", (uint)_s);
            if (EC_DIFF) Debug.WriteLine("1p 0x{0:x}", (uint)_ftb);
            if (EC_DIFF) Debug.WriteLine("8i 0x{0:x}", (uint)_this.nbits_total);
            uint r;
            r = _this.rng >> (int)_ftb;
            if (_s > 0)
            {
                _this.val += _this.rng - (r * _icdf[_s - 1]);
                _this.rng = (r * (uint)(_icdf[_s - 1] - _icdf[_s]));
                if (EC_DIFF) Debug.WriteLine("1oa 0x{0:x}", (uint)_icdf[_s - 1]);
                if (EC_DIFF) Debug.WriteLine("1ob 0x{0:x}", (uint)_icdf[_s]);
            }
            else
            {
                _this.rng -= (r * _icdf[_s]);
            }
            if (EC_DIFF) Debug.WriteLine("6l 0x{0:x}", (uint)_this.val);
            if (EC_DIFF) Debug.WriteLine("6m 0x{0:x}", (uint)_this.rng);
            ec_enc_normalize(_this);
        }

        internal static void ec_enc_uint(ec_ctx _this, uint _fl, uint _ft)
        {
            if (EC_DIFF) Debug.WriteLine("1q 0x{0:x}", (uint)_fl);
            if (EC_DIFF) Debug.WriteLine("1r 0x{0:x}", (uint)_ft);
            if (EC_DIFF) Debug.WriteLine("8j 0x{0:x}", (uint)_this.nbits_total);
            uint ft;
            uint fl;
            int ftb;
            /*In order to optimize EC_ILOG(), it is undefined for the value 0.*/
            Inlines.OpusAssert(_ft > 1);
            _ft--;
            ftb = Inlines.EC_ILOG(_ft);
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

        internal static void ec_enc_bits(ec_ctx _this, uint _fl, uint _bits)
        {
            if (EC_DIFF) Debug.WriteLine("1s 0x{0:x}", (uint)_fl);
            if (EC_DIFF) Debug.WriteLine("1t 0x{0:x}", (uint)_bits);
            if (EC_DIFF) Debug.WriteLine("8k 0x{0:x}", (uint)_this.nbits_total);
            uint window;
            int used;
            window = _this.end_window;
            used = _this.nend_bits;
            Inlines.OpusAssert(_bits > 0);

            if (used + _bits > EC_WINDOW_SIZE)
            {
                do
                {
                    _this.error |= ec_write_byte_at_end(_this, (uint)window & EC_SYM_MAX);
                    window >>= EC_SYM_BITS;
                    used -= EC_SYM_BITS;
                }
                while (used >= EC_SYM_BITS);
            }

            window |= (uint)_fl << used;
            used += (int)_bits;
            _this.end_window = window;
            _this.nend_bits = used;
            if (EC_DIFF) Debug.WriteLine("7c 0x{0:x}", (uint)_this.nbits_total);
            if (EC_DIFF) Debug.WriteLine("7d 0x{0:x}", (uint)_bits);
            _this.nbits_total += (int)_bits;
            if (EC_DIFF) Debug.WriteLine("1ta 0x{0:x}", (uint)_this.nbits_total);
            if (EC_DIFF) Debug.WriteLine("6n 0x{0:x}", (uint)_this.end_window);
            if (EC_DIFF) Debug.WriteLine("6o 0x{0:x}", (uint)_this.nend_bits);
        }

        internal static void ec_enc_patch_initial_bits(ec_ctx _this, uint _val, uint _nbits)
        {
            if (EC_DIFF) Debug.WriteLine("1u 0x{0:x}", (uint)_val);
            if (EC_DIFF) Debug.WriteLine("1v 0x{0:x}", (uint)_nbits);
            if (EC_DIFF) Debug.WriteLine("8l 0x{0:x}", (uint)_this.nbits_total);
            int shift;
            uint mask;
            Inlines.OpusAssert(_nbits <= EC_SYM_BITS);
            shift = EC_SYM_BITS - (int)_nbits;
            mask = ((1U << (int)_nbits) - 1) << shift;

            if (_this.offs > 0)
            {
                /*The first byte has been finalized.*/
                _this.buf[0] = (byte)((_this.buf[0] & ~mask) | _val << shift);
                if (EC_DIFF) Debug.WriteLine("6p 0x{0:x}", (uint)_this.buf[0]);
            }
            else if (_this.rem >= 0)
            {
                /*The first byte is still awaiting carry propagation.*/
                _this.rem = (int)(((uint)_this.rem & ~mask) | _val) << shift;
            }
            else if (_this.rng <= (EC_CODE_TOP >> (int)_nbits))
            {
                /*The renormalization loop has never been run.*/
                _this.val = (_this.val & ~((uint)mask << (int)EC_CODE_SHIFT)) |
                 (uint)_val << (int)(EC_CODE_SHIFT + shift);
            }
            else
            {
                /*The encoder hasn't even encoded _nbits of data yet.*/
                _this.error = -1;
            }

            if (EC_DIFF) Debug.WriteLine("6q 0x{0:x}", (uint)_this.rem);
            if (EC_DIFF) Debug.WriteLine("6r 0x{0:x}", (uint)_this.val);
        }

        internal static void ec_enc_shrink(ec_ctx _this, uint _size)
        {
            if (EC_DIFF) Debug.WriteLine("1w 0x{0:x}", (uint)_size);
            if (EC_DIFF) Debug.WriteLine("8m 0x{0:x}", (uint)_this.nbits_total);
            Inlines.OpusAssert(_this.offs + _this.end_offs <= _size);
            //(memmove(_this.buf + _size - _this.end_offs, _this.buf + _this.storage - _this.end_offs, _this.end_offs * sizeof(*(dst))))
            _this.buf.Point(_this.storage - _this.end_offs).MemMove((int)(_this.storage - _size), (int)_this.end_offs);
            _this.storage = _size;
        }

        internal static uint ec_range_bytes(ec_ctx _this)
        {
            return _this.offs;
        }

        internal static Pointer<byte> ec_get_buffer(ec_ctx _this)
        {
            return _this.buf;
        }

        internal static int ec_get_error(ec_ctx _this)
        {
            return _this.error;
        }

        /// <summary>
        /// Returns the number of bits "used" by the encoded or decoded symbols so far.
        /// This same number can be computed in either the encoder or the decoder, and is
        /// suitable for making coding decisions.
        /// This will always be slightly larger than the exact value (e.g., all
        /// rounding error is in the positive direction).
        /// </summary>
        /// <param name="_this"></param>
        /// <returns>The number of bits.</returns>
        internal static int ec_tell(ec_ctx _this)
        {
            int returnVal = _this.nbits_total - Inlines.EC_ILOG(_this.rng);
            if (EC_DIFF) Debug.WriteLine("1ya 0x{0:x}", (uint)_this.rng);
            if (EC_DIFF) Debug.WriteLine("1yb 0x{0:x}", (uint)_this.nbits_total);
            if (EC_DIFF) Debug.WriteLine("1yc 0x{0:x}", (uint)returnVal);
            return returnVal;
        }

        private static readonly uint[] correction = {35733, 38967, 42495, 46340, 50535, 55109, 60097, 65535};

        /// <summary>
        /// This is a faster version of ec_tell_frac() that takes advantage
        /// of the low(1/8 bit) resolution to use just a linear function
        /// followed by a lookup to determine the exact transition thresholds.
        /// FIXME: THIS NEEDS TESTING
        /// </summary>
        /// <param name="_this"></param>
        /// <returns></returns>
        internal static uint ec_tell_frac(ec_ctx _this)
        {
            if (EC_DIFF) Debug.WriteLine("8o 0x{0:x}", (uint)_this.nbits_total);
            int nbits;
            int r;
            int l;
            uint b;
            nbits = _this.nbits_total << BITRES;
            l = Inlines.EC_ILOG(_this.rng);
            r = (int)(_this.rng >> (l - 16));
            b = (uint)((r >> 12) - 8);
            b += (r > correction[b] ? 1u : 0);
            l = (int)((l << 3) + b);
            if (EC_DIFF) Debug.WriteLine("1z 0x{0:x}", (uint)(nbits - l));
            return (uint)(nbits - l);
        }

        internal static void ec_enc_done(ec_ctx _this)
        {
            uint window;
            int used;
            uint msk;
            uint end;
            int l;
            if (EC_DIFF) Debug.WriteLine("8n 0x{0:x}", (uint)_this.nbits_total);
            /*We output the minimum number of bits that ensures that the symbols encoded
               thus far will be decoded correctly regardless of the bits that follow.*/
            l = EC_CODE_BITS - Inlines.EC_ILOG(_this.rng);
            msk = (EC_CODE_TOP - 1) >> l;
            end = (_this.val + msk) & ~msk;

            if ((end | msk) >= _this.val + _this.rng)
            {
                l++;
                msk >>= 1;
                end = (_this.val + msk) & ~msk;
            }

            while (l > 0)
            {
                ec_enc_carry_out(_this, (int)(end >> (int)EC_CODE_SHIFT));
                end = (end << EC_SYM_BITS) & (EC_CODE_TOP - 1);
                l -= EC_SYM_BITS;
            }

            /*If we have a buffered byte flush it into the output buffer.*/
            if (_this.rem >= 0 || _this.ext > 0)
            {
                ec_enc_carry_out(_this, 0);
            }

            /*If we have buffered extra bits, flush them as well.*/
            window = _this.end_window;
            used = _this.nend_bits;

            while (used >= EC_SYM_BITS)
            {
                _this.error |= ec_write_byte_at_end(_this, (uint)window & EC_SYM_MAX);
                window >>= EC_SYM_BITS;
                used -= EC_SYM_BITS;
            }

            /*Clear any excess space and add any remaining extra bits to the last byte.*/
            if (_this.error == 0)
            {
                _this.buf.Point(_this.offs).MemSet(0, _this.storage - _this.offs - _this.end_offs);
                if (used > 0)
                {
                    /*If there's no range coder data at all, give up.*/
                    if (_this.end_offs >= _this.storage)
                    {
                        _this.error = -1;
                    }
                    else
                    {
                        l = -l;
                        /*If we've busted, don't add too many extra bits to the last byte; it
                           would corrupt the range coder data, and that's more important.*/
                        if (_this.offs + _this.end_offs >= _this.storage && l < used)
                        {
                            window = window & ((1U << l) - 1);
                            _this.error = -1;
                        }

                        _this.buf[_this.storage - _this.end_offs - 1] |= (byte)window;
                    }
                }
            }
        }
    }
}
