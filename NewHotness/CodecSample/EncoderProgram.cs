using Concentus;
using Concentus.Oggfile;
using HellaUnsafe;
using static HellaUnsafe.Opus.Opus_Encoder;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Opus.OpusPrivate;

namespace CodecSample
{
    internal class EncoderProgram
    {
        static void Main(string[] args)
        {
            using (FileStream fileIn = new FileStream(@"D:\Code\concentus\NewHotness\CodecSample\poison.opus", FileMode.Open, FileAccess.Read))
            using (FileStream fileOut = new FileStream(@"D:\Code\concentus\NewHotness\CodecSample\jank.opus", FileMode.Create, FileAccess.Write))
            {
                IOpusEncoder encoder = HellaUnsafeOpusEncoder.Create(48000, 2, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
                encoder.Complexity = 10;
                encoder.UseVBR = true;
                encoder.UseConstrainedVBR = true;
                encoder.Bitrate = 16 * 1024;
                IOpusDecoder decoder = OpusCodecFactory.CreateDecoder(48000, 2);
                OpusOggReadStream readStream = new OpusOggReadStream(
                    decoder,
                    fileIn);
                OpusOggWriteStream writeStream = new OpusOggWriteStream(
                    encoder,
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
