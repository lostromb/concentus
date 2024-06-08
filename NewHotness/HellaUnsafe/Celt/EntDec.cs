/* Copyright (c) 2001-2011 Timothy B. Terriberry
   Copyright (c) 2008-2009 Xiph.Org Foundation */
/*
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.EntCode;

namespace HellaUnsafe.Celt
{
    internal static class EntDec
    {
        internal static unsafe int ec_read_byte(in ec_ctx* _this, in byte* buf)
        {
            return _this->offs < _this->storage ? buf[_this->offs++] : 0;
        }

        internal static unsafe int ec_read_byte_from_end(in ec_ctx* _this, in byte* buf)
        {
            return _this->end_offs < _this->storage ?
             buf[_this->storage - ++(_this->end_offs)] : 0;
        }

        /*Normalizes the contents of val and rng so that rng lies entirely in the
           high-order symbol.*/
        internal static unsafe void ec_dec_normalize(in ec_ctx* _this, in byte* buf)
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
                _this->rem = ec_read_byte(_this, buf);
                /*Take the rest of the bits we need from this new symbol.*/
                sym = (sym << EC_SYM_BITS | _this->rem) >> (EC_SYM_BITS - EC_CODE_EXTRA);
                /*And subtract them from val, capped to be less than EC_CODE_TOP.*/
                _this->val = (uint)((_this->val << EC_SYM_BITS) + (EC_SYM_MAX & ~sym)) & (EC_CODE_TOP - 1);
            }
        }

        internal static unsafe void ec_dec_init(in ec_ctx* _this, in byte* buf, uint _storage)
        {
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
            _this->rem = ec_read_byte(_this, buf);
            _this->val = _this->rng - 1 - (uint)(_this->rem >> (EC_SYM_BITS - EC_CODE_EXTRA));
            _this->error = 0;
            /*Normalize the interval.*/
            ec_dec_normalize(_this, buf);
        }

        internal static unsafe uint ec_decode(in ec_ctx* _this, uint _ft)
        {
            uint s;
            _this->ext = celt_udiv(_this->rng, _ft);
            s = (uint)(_this->val / _this->ext);
            return _ft - EC_MINI(s + 1, _ft);
        }

        internal static unsafe uint ec_decode_bin(in ec_ctx* _this, uint _bits)
        {
            uint s;
            _this->ext = _this->rng >> (int)_bits;
            s = (uint)(_this->val / _this->ext);
            return (1U << (int)_bits) - EC_MINI(s + 1U, 1U << (int)_bits);
        }

        internal static unsafe void ec_dec_update(in ec_ctx* _this, in byte* buf, uint _fl, uint _fh, uint _ft)
        {
            uint s;
            s = IMUL32(_this->ext, _ft - _fh);
            _this->val -= s;
            _this->rng = _fl > 0 ? IMUL32(_this->ext, _fh - _fl) : _this->rng - s;
            ec_dec_normalize(_this, buf);
        }

        /*The probability of having a "one" is 1/(1<<_logp).*/
        internal static unsafe int ec_dec_bit_logp(in ec_ctx* _this, in byte* buf, uint _logp)
        {
            uint r;
            uint d;
            uint s;
            int ret;
            r = _this->rng;
            d = _this->val;
            s = r >> (int)_logp;
            ret = d < s ? 1 : 0;
            if (ret == 0) _this->val = d - s;
            _this->rng = ret != 0 ? s : r - s;
            ec_dec_normalize(_this, buf);
            return ret;
        }

        internal static unsafe int ec_dec_icdf(in ec_ctx* _this, in byte* buf, in byte* _icdf, uint _ftb)
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
            ec_dec_normalize(_this, buf);
            return ret;
        }

        internal static unsafe int ec_dec_icdf(in ec_ctx* _this, in byte* buf, ReadOnlySpan<byte> _icdf, uint _ftb)
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
            ec_dec_normalize(_this, buf);
            return ret;
        }

        internal static unsafe int ec_dec_icdf16(in ec_ctx* _this, in byte* buf, in ushort* _icdf, uint _ftb)
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
            ec_dec_normalize(_this, buf);
            return ret;
        }

        internal static unsafe uint ec_dec_uint(in ec_ctx* _this, in byte* buf, uint _ft)
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
                ec_dec_update(_this, buf, s, s + 1, ft);
                t = (uint)s << ftb | ec_dec_bits(_this, buf, (uint)ftb);
                if (t <= _ft) return t;
                _this->error = 1;
                return _ft;
            }
            else
            {
                _ft++;
                s = ec_decode(_this, (uint)_ft);
                ec_dec_update(_this, buf, s, s + 1, (uint)_ft);
                return s;
            }
        }

        internal static unsafe uint ec_dec_bits(in ec_ctx* _this, in byte* buf, uint _bits)
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
                    window |= (uint)ec_read_byte_from_end(_this, buf) << available;
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
    }
}
