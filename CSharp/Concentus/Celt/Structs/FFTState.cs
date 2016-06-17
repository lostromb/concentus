using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    internal class FFTState
    {
        internal int nfft = 0;
        internal short scale = 0;
        internal int scale_shift = 0;
        internal int shift = 0;
        internal short[] factors = new short[2 * KissFFT.MAXFACTORS];
        internal Pointer<short> bitrev = null;
        internal short[] twiddles = null;
    }
}
