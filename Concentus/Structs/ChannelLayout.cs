using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Structs
{
    public class ChannelLayout
    {
        public int nb_channels;
        public int nb_streams;
        public int nb_coupled_streams;
        public Pointer<byte> mapping = Pointer.Malloc<byte>(256);

        public void Reset()
        {
            nb_channels = 0;
            nb_streams = 0;
            nb_coupled_streams = 0;
            mapping.MemSet(0, 256);
        }
    }
}
