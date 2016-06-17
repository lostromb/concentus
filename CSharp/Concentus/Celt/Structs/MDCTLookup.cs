using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    internal class MDCTLookup
    {
        internal int n = 0;

        internal int maxshift = 0;

        // [porting note] these are pointers to static states defined in tables.cs
        internal FFTState[] kfft = new FFTState[4];

        internal Pointer<short> trig = null;

        internal MDCTLookup()
        {
        }

        internal void Reset()
        {
            n = 0;
            maxshift = 0;
            kfft = new FFTState[4];
            trig = null;
        }
    }
}
