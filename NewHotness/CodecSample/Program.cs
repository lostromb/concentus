using Concentus;
using Concentus.Oggfile;
using HellaUnsafe;
using static HellaUnsafe.Opus.Opus_Encoder;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Opus.OpusPrivate;

namespace CodecSample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using (FileStream fileIn = new FileStream(@"D:\Code\concentus\NewHotness\CodecSample\poison.opus", FileMode.Open, FileAccess.Read))
            using (FileStream fileOut = new FileStream(@"D:\Code\concentus\NewHotness\CodecSample\jank.opus", FileMode.Create, FileAccess.Write))
            {
                OpusOggReadStream readStream = new OpusOggReadStream(
                    OpusCodecFactory.CreateDecoder(48000, 2),
                    fileIn);
                OpusOggWriteStream writeStream = new OpusOggWriteStream(
                    HellaUnsafeOpusEncoder.Create(48000, 2, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO),
                    fileOut,
                    new OpusTags(),
                    48000);
                while (readStream.HasNextPacket)
                {
                    short[] packet = readStream.DecodeNextPacket();
                    writeStream.WriteSamples(packet, 0, packet.Length);
                }

                writeStream.Finish();
            }
        }
    }
}
