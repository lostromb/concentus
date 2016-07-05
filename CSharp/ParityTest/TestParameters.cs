using Concentus.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParityTest
{
    public class TestParameters
    {
        public int Bitrate = 40;
        public int Channels = 2;
        public OpusApplication Application = OpusApplication.OPUS_APPLICATION_AUDIO;
        public int SampleRate = 48000;
        public double FrameSize = 20;
        public int Complexity = 10;
        public int PacketLossPercent = 0;
        public OpusMode ForceMode = OpusMode.MODE_AUTO;
        public bool UseDTX = false;
        public bool UseVBR = false;
        public bool ConstrainedVBR = false;
        public int DecoderSampleRate = 48000;
        public int DecoderChannels = 2;
    }
}
