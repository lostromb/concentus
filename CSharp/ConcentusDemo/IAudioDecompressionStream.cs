using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConcentusDemo
{
    public interface IAudioDecompressionStream
    {
        AudioChunk Decompress(byte[] input);
        AudioChunk Close();
    }
}
