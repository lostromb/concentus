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

namespace HellaUnsafe.Celt
{
    /*A range encoder.
      See entdec.c and the references for implementation details \cite{Mar79,MNW98}.

      @INPROCEEDINGS{Mar79,
       author="Martin, G.N.N.",
       title="Range encoding: an algorithm for removing redundancy from a digitised
        message",
       booktitle="Video \& Data Recording Conference",
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
       URL="http://www.stanford.edu/class/ee398/handouts/papers/Moffat98ArithmCoding.pdf"
      }*/
    internal static class EntCode
    {
        internal const int CHAR_BIT = 8;
        internal const int EC_WINDOW_SIZE = sizeof(uint) * CHAR_BIT;

        /*The number of bits to use for the range-coded part of unsigned integers.*/
        internal const int EC_UINT_BITS = 8;

        /*The resolution of fractional-precision bit usage measurements, i.e.,
            3 => 1/8th bits.*/
        internal const int BITRES = 3;

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

        /*The entropy encoder/decoder context.
          We use the same structure for both, so that common functions like ec_tell()
           can be used on either one.*/
        internal struct ec_ctx
        {
            /*Buffered input/output.*/
            //byte* buf;
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
        }

        internal static uint ec_range_bytes(in ec_ctx _this)
        {
            return _this.offs;
        }

        // One of the main changes I make in the Concentus codebase is that
        // I eliminate the temporary storage of this buffer pointer. This means
        // the buffer has to be passed around manually to every function that uses
        // it, but it also makes working with the buffer a lot easier if it is a
        // Span<byte> or a byte* pointer that would otherwise require pinning each time.
        // Here, we can pin the buffer at the highest-level opus API and then just
        // reuse that same pointer for the remainder of the operation.

        //internal static uint ec_get_buffer(in ec_ctx _this)
        //{
        //    return _this.buf;
        //}

        internal static int ec_get_error(in ec_ctx _this)
        {
            return _this.error;
        }

        /*Returns the number of bits "used" by the encoded or decoded symbols so far.
          This same number can be computed in either the encoder or the decoder, and is
           suitable for making coding decisions.
          Return: The number of bits.
          This will always be slightly larger than the exact value (e.g., all
           rounding error is in the positive direction).*/
        internal static int ec_tell(in ec_ctx _this)
        {
            return _this.nbits_total - EC_ILOG(_this.rng);
        }

        private static readonly uint[] correction = { 35733, 38967, 42495, 46340, 50535, 55109, 60097, 65535 };

        /*Returns the number of bits "used" by the encoded or decoded symbols so far.
      This same number can be computed in either the encoder or the decoder, and is
       suitable for making coding decisions.
      Return: The number of bits scaled by 2**BITRES.
              This will always be slightly larger than the exact value (e.g., all
               rounding error is in the positive direction).*/
        internal static uint ec_tell_frac(in ec_ctx _this)
        {
            int nbits;
            int r;
            int l;
            uint b;
            nbits = _this.nbits_total << BITRES;
            l = EC_ILOG(_this.rng);
            r = (int)(_this.rng >> (l - 16));
            b = (uint)((r >> 12) - 8);
            b += (r > correction[b] ? 1u : 0);
            l = (int)((l << 3) + b);
            return (uint)(nbits - l);
        }

        internal static uint celt_udiv(uint n, uint d)
        {
            Inlines.ASSERT(d > 0);
            return n / d;
        }

        internal static int celt_sudiv(int n, int d)
        {
            Inlines.ASSERT(d > 0);
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
            return unchecked(a + ((b - a) & ((b < a) ? 0xFFFFFFFFU : 0)));
        }

        /// <summary>
        /// Counts leading zeroes.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        internal static int EC_CLZ(uint x)
        {
            // OPT can use clz intrinsics here if available
            if (x == 0)
                return 0;
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            uint y = x - ((x >> 1) & 0x55555555);
            y = (((y >> 2) & 0x33333333) + (y & 0x33333333));
            y = (((y >> 4) + y) & 0x0f0f0f0f);
            y += (y >> 8);
            y += (y >> 16);
            y = (y & 0x0000003f);
            return (int)(1 - y);
        }

        /// <summary>
        /// returns inverse base-2 log of a value
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        internal static int EC_ILOG(uint x)
        {
            // Implementation 1
            if(x == 0)
                return 1;
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            uint y = x - ((x >> 1) & 0x55555555);
            y = (((y >> 2) & 0x33333333) + (y & 0x33333333));
            y = (((y >> 4) + y) & 0x0f0f0f0f);
            y += (y >> 8);
            y += (y >> 16);
            y = (y & 0x0000003f);
            return (int)y;

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
        }
    }
}
