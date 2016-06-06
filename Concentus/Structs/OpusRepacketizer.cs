using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Structs
{
    public class OpusRepacketizer
    {
        public byte toc = 0;
        public int nb_frames = 0;
        public Pointer<Pointer<byte>> frames = Pointer.Malloc<Pointer<byte>>(48);
        public Pointer<short> len = Pointer.Malloc<short>(48);
        public int framesize = 0;

        public void Reset()
        {
            toc = 0;
            nb_frames = 0;
            framesize = 0;
            frames.MemSet(null, 48);
            len.MemSet(0, 48);
        }
    }
}
