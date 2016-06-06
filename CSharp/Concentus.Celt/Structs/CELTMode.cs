using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    public class CELTMode
    {
        public int Fs = 0;
        public int overlap = 0;

        public int nbEBands = 0;
        public int effEBands = 0;
        public int[] preemph = { 0, 0, 0, 0 };

        /// <summary>
        /// Definition for each "pseudo-critical band"
        /// </summary>
        public Pointer<short> eBands = null;

        public int maxLM = 0;
        public int nbShortMdcts = 0;
        public int shortMdctSize = 0;

        /// <summary>
        /// Number of lines in allocVectors
        /// </summary>
        public int nbAllocVectors = 0;

        /// <summary>
        /// Number of bits in each band for several rates
        /// </summary>
        public Pointer<byte> allocVectors = null;
        public Pointer<short> logN = null;

        public Pointer<int> window = null;
        public mdct_lookup mdct = new mdct_lookup();
        public PulseCache cache = new PulseCache();

        public CELTMode()
        {
        }

        public void Reset()
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
