using HellaUnsafe.Opus;

namespace ParityTest
{
    public class TestParameters
    {
        public int Bitrate = 40;
        public int Channels = 2;
        public int Application = OpusDefines.OPUS_APPLICATION_AUDIO;
        public int Signal = OpusDefines.OPUS_SIGNAL_MUSIC;
        public int SampleRate = 48000;
        public double FrameSize = 20;
        public int Complexity = 10;
        public int PacketLossPercent = 0;
        public int ForceMode = OpusDefines.OPUS_AUTO;
        public bool UseDTX = false;
        public bool UseVBR = false;
        public bool ConstrainedVBR = false;
        public int DecoderSampleRate = 48000;
        public int DecoderChannels = 2;
    }
}
