using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConcentusDemo
{
    public interface IAudioCompressionStream
    {
        byte[] Compress(AudioChunk input);
        byte[] Close();
        string GetEncodeParams();
    }
}
