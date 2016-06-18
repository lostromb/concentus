using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConcentusDemo
{
    public interface IOpusCodec
    {
        void SetBitrate(int bitrate);
        void SetComplexity(int complexity);
        byte[] Compress(AudioChunk input);
        AudioChunk Decompress(byte[] input);
    }
}
