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

using static HellaUnsafe.Old.Celt.EntCode;
using static HellaUnsafe.Old.Celt.Arch;
using static HellaUnsafe.Common.CRuntime;
using System;

namespace HellaUnsafe.Old.Celt
{
    internal static class EntEnc
    {
        internal static unsafe int ec_write_byte(ref ec_ctx _this, in byte* buf, uint _value)
        {
            if (_this.offs + _this.end_offs >= _this.storage) return -1;
            buf[_this.offs++] = (byte)_value;
            return 0;
        }

        internal static unsafe int ec_write_byte_at_end(ref ec_ctx _this, in byte* buf, uint _value)
        {
            if (_this.offs + _this.end_offs >= _this.storage) return -1;
            buf[_this.storage - ++_this.end_offs] = (byte)_value;
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
        internal static unsafe void ec_enc_carry_out(ref ec_ctx _this, in byte* buf, int _c)
        {
            if (_c != EC_SYM_MAX)
            {
                /*No further carry propagation possible, flush buffer.*/
                int carry;
                carry = _c >> EC_SYM_BITS;
                /*Don't output a byte on the first write.
                  This compare should be taken care of by branch-prediction thereafter.*/
                if (_this.rem >= 0) _this.error |= ec_write_byte(ref _this, buf, (uint)(_this.rem + carry));
                if (_this.ext > 0)
                {
                    uint sym;
                    sym = EC_SYM_MAX + (uint)carry & EC_SYM_MAX;
                    do _this.error |= ec_write_byte(ref _this, buf, sym);
                    while (--_this.ext > 0);
                }
                _this.rem = (int)((uint)_c & EC_SYM_MAX);
            }
            else _this.ext++;
        }

        internal static unsafe void ec_enc_normalize(ref ec_ctx _this, in byte* buf)
        {
            /*If the range is too small, output some bits and rescale it.*/
            while (_this.rng <= EC_CODE_BOT)
            {
                ec_enc_carry_out(ref _this, buf, (int)(_this.val >> EC_CODE_SHIFT));
                /*Move the next-to-high-order symbol into the high-order position.*/
                _this.val = _this.val << EC_SYM_BITS & EC_CODE_TOP - 1;
                _this.rng <<= EC_SYM_BITS;
                _this.nbits_total += EC_SYM_BITS;
            }
        }

        /*Initializes the encoder.
          _buf:  The buffer to store output bytes in.
          _size: The size of the buffer, in chars.*/
        internal static unsafe void ec_enc_init(ref ec_ctx _this, uint _size)
        {
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

        /*Encodes a symbol given its frequency information.
          The frequency information must be discernable by the decoder, assuming it
           has read only the previous symbols from the stream.
          It is allowable to change the frequency information, or even the entire
           source alphabet, so long as the decoder can tell from the context of the
           previously encoded information that it is supposed to do so as well.
          _fl: The cumulative frequency of all symbols that come before the one to be
                encoded.
          _fh: The cumulative frequency of all symbols up to and including the one to
                be encoded.
               Together with _fl, this defines the range [_fl,_fh) in which the
                decoded value will fall.
          _ft: The sum of the frequencies of all the symbols*/
        internal static unsafe void ec_encode(ref ec_ctx _this, in byte* buf, uint _fl, uint _fh, uint _ft)
        {
            uint r;
            r = celt_udiv(_this.rng, _ft);
            if (_fl > 0)
            {
                _this.val += _this.rng - IMUL32(r, _ft - _fl);
                _this.rng = IMUL32(r, _fh - _fl);
            }
            else _this.rng -= IMUL32(r, _ft - _fh);
            ec_enc_normalize(ref _this, buf);
        }

        /*Equivalent to ec_encode() with _ft==1<<_bits.*/
        internal static unsafe void ec_encode_bin(ref ec_ctx _this, in byte* buf, uint _fl, uint _fh, uint _bits)
        {
            uint r;
            r = _this.rng >> (int)_bits;
            if (_fl > 0)
            {
                _this.val += _this.rng - IMUL32(r, (1U << (int)_bits) - _fl);
                _this.rng = IMUL32(r, _fh - _fl);
            }
            else _this.rng -= IMUL32(r, (1U << (int)_bits) - _fh);
            ec_enc_normalize(ref _this, buf);
        }

        /* Encode a bit that has a 1/(1<<_logp) probability of being a one */
        internal static unsafe void ec_enc_bit_logp(ref ec_ctx _this, in byte* buf, int _val, uint _logp)
        {
            uint r;
            uint s;
            uint l;
            r = _this.rng;
            l = _this.val;
            s = r >> (int)_logp;
            r -= s;
            if (_val != 0) _this.val = l + r;
            _this.rng = _val != 0 ? s : r;
            ec_enc_normalize(ref _this, buf);
        }

        /*Encodes a symbol given an "inverse" CDF table.
          _s:    The index of the symbol to encode.
          _icdf: The "inverse" CDF, such that symbol _s falls in the range
                  [_s>0?ft-_icdf[_s-1]:0,ft-_icdf[_s]), where ft=1<<_ftb.
                 The values must be monotonically non-increasing, and the last value
                  must be 0.
          _ftb: The number of bits of precision in the cumulative distribution.*/
        internal static unsafe void ec_enc_icdf(ref ec_ctx _this, in byte* buf, int _s, in byte* _icdf, uint _ftb)
        {
            uint r;
            r = _this.rng >> (int)_ftb;
            if (_s > 0)
            {
                _this.val += _this.rng - IMUL32(r, _icdf[_s - 1]);
                _this.rng = r * _icdf[_s - 1] - _icdf[_s];
            }
            else _this.rng -= IMUL32(r, _icdf[_s]);
            ec_enc_normalize(ref _this, buf);
        }

        internal static unsafe void ec_enc_icdf(ref ec_ctx _this, in byte* buf, int _s, ReadOnlySpan<byte> _icdf, uint _ftb)
        {
            uint r;
            r = _this.rng >> (int)_ftb;
            if (_s > 0)
            {
                _this.val += _this.rng - IMUL32(r, _icdf[_s - 1]);
                _this.rng = r * _icdf[_s - 1] - _icdf[_s];
            }
            else _this.rng -= IMUL32(r, _icdf[_s]);
            ec_enc_normalize(ref _this, buf);
        }

        /*Encodes a symbol given an "inverse" CDF table.
          _s:    The index of the symbol to encode.
          _icdf: The "inverse" CDF, such that symbol _s falls in the range
                  [_s>0?ft-_icdf[_s-1]:0,ft-_icdf[_s]), where ft=1<<_ftb.
                 The values must be monotonically non-increasing, and the last value
                  must be 0.
          _ftb: The number of bits of precision in the cumulative distribution.*/
        internal static unsafe void ec_enc_icdf16(ref ec_ctx _this, in byte* buf, int _s, in ushort* _icdf, uint _ftb)
        {
            uint r;
            r = _this.rng >> (int)_ftb;
            if (_s > 0)
            {
                _this.val += _this.rng - IMUL32(r, _icdf[_s - 1]);
                _this.rng = r * _icdf[_s - 1] - _icdf[_s];
            }
            else _this.rng -= IMUL32(r, _icdf[_s]);
            ec_enc_normalize(ref _this, buf);
        }

        /*Encodes a raw unsigned integer in the stream.
          _fl: The integer to encode.
          _ft: The number of integers that can be encoded (one more than the max).
               This must be at least 2, and no more than 2**32-1.*/
        internal static unsafe void ec_enc_uint(ref ec_ctx _this, in byte* buf, uint _fl, uint _ft)
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
                fl = _fl >> ftb;
                ec_encode(ref _this, buf, fl, fl + 1, ft);
                ec_enc_bits(ref _this, buf, _fl & ((uint)1 << ftb) - 1U, (uint)ftb);
            }
            else ec_encode(ref _this, buf, _fl, _fl + 1, _ft + 1);
        }

        /*Encodes a sequence of raw bits in the stream.
          _fl:  The bits to encode.
          _ftb: The number of bits to encode.
                This must be between 1 and 25, inclusive.*/
        internal static unsafe void ec_enc_bits(ref ec_ctx _this, in byte* buf, uint _fl, uint _bits)
        {
            uint window;
            int used;
            window = _this.end_window;
            used = _this.nend_bits;
            ASSERT(_bits > 0);
            if (used + _bits > EC_WINDOW_SIZE)
            {
                do
                {
                    _this.error |= ec_write_byte_at_end(ref _this, buf, window & EC_SYM_MAX);
                    window >>= EC_SYM_BITS;
                    used -= EC_SYM_BITS;
                }
                while (used >= EC_SYM_BITS);
            }
            window |= _fl << used;
            used += (int)_bits;
            _this.end_window = window;
            _this.nend_bits = used;
            _this.nbits_total += (int)_bits;
        }

        /*Overwrites a few bits at the very start of an existing stream, after they
           have already been encoded.
          This makes it possible to have a few flags up front, where it is easy for
           decoders to access them without parsing the whole stream, even if their
           values are not determined until late in the encoding process, without having
           to buffer all the intermediate symbols in the encoder.
          In order for this to work, at least _nbits bits must have already been
           encoded using probabilities that are an exact power of two.
          The encoder can verify the number of encoded bits is sufficient, but cannot
           check this latter condition.
          _val:   The bits to encode (in the least _nbits significant bits).
                  They will be decoded in order from most-significant to least.
          _nbits: The number of bits to overwrite.
                  This must be no more than 8.*/
        internal static unsafe void ec_enc_patch_initial_bits(ref ec_ctx _this, in byte* buf, uint _val, uint _nbits)
        {
            int shift;
            uint mask;
            ASSERT(_nbits <= EC_SYM_BITS);
            shift = EC_SYM_BITS - (int)_nbits;
            mask = (1U << (int)_nbits) - 1 << shift;
            if (_this.offs > 0)
            {
                /*The first byte has been finalized.*/
                buf[0] = (byte)(buf[0] & ~mask | _val << shift);
            }
            else if (_this.rem >= 0)
            {
                /*The first byte is still awaiting carry propagation.*/
                _this.rem = (int)((uint)_this.rem & ~mask | _val) << shift;
            }
            else if (_this.rng <= EC_CODE_TOP >> (int)_nbits)
            {
                /*The renormalization loop has never been run.*/
                _this.val = _this.val & ~(mask << EC_CODE_SHIFT) |
                 _val << EC_CODE_SHIFT + shift;
            }
            /*The encoder hasn't even encoded _nbits of data yet.*/
            else _this.error = -1;
        }

        /*Compacts the data to fit in the target size.
          This moves up the raw bits at the end of the current buffer so they are at
           the end of the new buffer size.
          The caller must ensure that the amount of data that's already been written
           will fit in the new size.
          _size: The number of bytes in the new buffer.
                 This must be large enough to contain the bits already written, and
                  must be no larger than the existing size.*/
        internal static unsafe void ec_enc_shrink(ref ec_ctx _this, in byte* buf, uint _size)
        {
            ASSERT(_this.offs + _this.end_offs <= _size);
            OPUS_MOVE(buf + _size - _this.end_offs,
                buf + _this.storage - _this.end_offs, _this.end_offs);
            _this.storage = _size;
        }

        /*Indicates that there are no more symbols to encode.
          All reamining output bytes are flushed to the output buffer.
          ec_enc_init() must be called before the encoder can be used again.*/
        internal static unsafe void ec_enc_done(ref ec_ctx _this, in byte* buf)
        {
            uint window;
            int used;
            uint msk;
            uint end;
            int l;
            /*We output the minimum number of bits that ensures that the symbols encoded
               thus far will be decoded correctly regardless of the bits that follow.*/
            l = EC_CODE_BITS - EC_ILOG(_this.rng);
            msk = EC_CODE_TOP - 1 >> l;
            end = _this.val + msk & ~msk;
            if ((end | msk) >= _this.val + _this.rng)
            {
                l++;
                msk >>= 1;
                end = _this.val + msk & ~msk;
            }
            while (l > 0)
            {
                ec_enc_carry_out(ref _this, buf, (int)(end >> EC_CODE_SHIFT));
                end = end << EC_SYM_BITS & EC_CODE_TOP - 1;
                l -= EC_SYM_BITS;
            }
            /*If we have a buffered byte flush it into the output buffer.*/
            if (_this.rem >= 0 || _this.ext > 0) ec_enc_carry_out(ref _this, buf, 0);
            /*If we have buffered extra bits, flush them as well.*/
            window = _this.end_window;
            used = _this.nend_bits;
            while (used >= EC_SYM_BITS)
            {
                _this.error |= ec_write_byte_at_end(ref _this, buf, window & EC_SYM_MAX);
                window >>= EC_SYM_BITS;
                used -= EC_SYM_BITS;
            }
            /*Clear any excess space and add any remaining extra bits to the last byte.*/
            if (_this.error == 0)
            {
                OPUS_CLEAR(buf + _this.offs,
                 _this.storage - _this.offs - _this.end_offs);
                if (used > 0)
                {
                    /*If there's no range coder data at all, give up.*/
                    if (_this.end_offs >= _this.storage) _this.error = -1;
                    else
                    {
                        l = -l;
                        /*If we've busted, don't add too many extra bits to the last byte; it
                           would corrupt the range coder data, and that's more important.*/
                        if (_this.offs + _this.end_offs >= _this.storage && l < used)
                        {
                            window &= (1U << l) - 1;
                            _this.error = -1;
                        }

                        buf[_this.storage - _this.end_offs - 1] |= (byte)window;
                    }
                }
            }
        }
    }
}
