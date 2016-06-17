using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    internal class CeltMode
    {
        internal int Fs = 0;
        internal int overlap = 0;

        internal int nbEBands = 0;
        internal int effEBands = 0;
        internal int[] preemph = { 0, 0, 0, 0 };

        /// <summary>
        /// Definition for each "pseudo-critical band"
        /// </summary>
        internal Pointer<short> eBands = null;

        internal int maxLM = 0;
        internal int nbShortMdcts = 0;
        internal int shortMdctSize = 0;

        /// <summary>
        /// Number of lines in allocVectors
        /// </summary>
        internal int nbAllocVectors = 0;

        /// <summary>
        /// Number of bits in each band for several rates
        /// </summary>
        internal Pointer<byte> allocVectors = null;
        internal Pointer<short> logN = null;

        internal Pointer<int> window = null;
        internal MDCTLookup mdct = new MDCTLookup();
        internal PulseCache cache = new PulseCache();

        internal CeltMode()
        {
        }

        internal void Reset()
        {
            Fs = 0;
            overlap = 0;
            nbEBands = 0;
            effEBands = 0;
            Arrays.MemSet<int>(preemph, 0);
            eBands = null;
            maxLM = 0;
            nbShortMdcts = 0;
            shortMdctSize = 0;
            nbAllocVectors = 0;
            allocVectors = null;
            logN = null;
            window = null;
            mdct.Reset();
            cache.Reset();
        }
    }
}
