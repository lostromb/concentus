using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    public class kiss_fft_state
    {
        public int nfft = 0;
        public short scale = 0;
        public int scale_shift = 0;
        public int shift = 0;
        public short[] factors = new short[2 * KissFFT.MAXFACTORS];
        public Pointer<short> bitrev = null;
        public Pointer<kiss_twiddle_cpx> twiddles = null;
    }
}
