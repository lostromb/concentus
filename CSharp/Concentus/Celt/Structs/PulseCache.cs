using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    public class PulseCache
    {
        public int size = 0;
        public Pointer<short> index = null;
        public Pointer<byte> bits = null;
        public Pointer<byte> caps = null;

        public void Reset()
        {
            size = 0;
            index = null;
            bits = null;
            caps = null;
        }
    }
}
