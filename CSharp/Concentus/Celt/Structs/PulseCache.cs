using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    internal class PulseCache
    {
        internal int size = 0;
        internal Pointer<short> index = null;
        internal Pointer<byte> bits = null;
        internal Pointer<byte> caps = null;

        internal void Reset()
        {
            size = 0;
            index = null;
            bits = null;
            caps = null;
        }
    }
}
