using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common
{
    /*The entropy encoder/decoder context.
      We use the same structure for both, so that common functions like ec_tell()
       can be used on either one.*/
    public class ec_ctx
    {
        /*POINTER to Buffered input/output.*/
        public Pointer<byte> buf;

        /*The size of the buffer.*/
        public uint storage;

        /*The offset at which the last byte containing raw bits was read/written.*/
        public uint end_offs;

        /*Bits that will be read from/written at the end.*/
        public uint end_window;

        /*Number of valid bits in end_window.*/
        public int nend_bits;

        /*The total number of whole bits read/written.
          This does not include partial bits currently in the range coder.*/
        public int nbits_total;

        /*The offset at which the next range coder byte will be read/written.*/
        public uint offs;

        /*The number of values in the current range.*/
        public uint rng;

        /*In the decoder: the difference between the top of the current range and
           the input value, minus one.
          In the encoder: the low end of the current range.*/
        public uint val;

        /*In the decoder: the saved normalization factor from ec_decode().
          In the encoder: the number of oustanding carry propagating symbols.*/
        public uint ext;

        /*A buffered input/output symbol, awaiting carry propagation.*/
        public int rem;

        /*Nonzero if an error occurred.*/
        public int error;

        public ec_ctx()
        {
            Reset();
        }

        public void Reset()
        {
            buf = null;
            storage = 0;
            end_offs = 0;
            end_window = 0;
            nend_bits = 0;
            offs = 0;
            rng = 0;
            val = 0;
            ext = 0;
            rem = 0;
            error = 0;
        }

        public void Assign(ec_ctx other)
        {
            this.buf = other.buf;
            this.storage = other.storage;
            this.end_offs = other.end_offs;
            this.end_window = other.end_window;
            this.nend_bits = other.nend_bits;
            this.nbits_total = other.nbits_total;
            this.offs = other.offs;
            this.rng = other.rng;
            this.val = other.val;
            this.ext = other.ext;
            this.rem = other.rem;
            this.error = other.error;
        }
    };
}
