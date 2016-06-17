using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Enums
{
    internal static class DecoderAPIFlag
    {
        public const int FLAG_DECODE_NORMAL = 0;
        public const int FLAG_PACKET_LOST = 1;
        public const int FLAG_DECODE_LBRR = 2;
    }
}
