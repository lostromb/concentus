using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    public class mdct_lookup
    {
        public int n = 0;

        public int maxshift = 0;

        // [porting note] these are pointers to static states defined in tables.cs
        public kiss_fft_state[] kfft = new kiss_fft_state[4];

        public Pointer<short> trig = null;

        public mdct_lookup()
        {
        }

        public void Reset()
        {
            n = 0;
            maxshift = 0;
            kfft = new kiss_fft_state[4];
            trig = null;
        }
    }
}
