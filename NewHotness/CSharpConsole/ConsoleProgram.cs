
using BenchmarkDotNet.Running;
using System.Runtime.Intrinsics.X86;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.Opus_Decoder;
using static HellaUnsafe.Opus.Opus_Encoder;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Opus.OpusPrivate;

namespace CSharpConsole
{
    internal static unsafe class ConsoleProgram
    {
        

        public static unsafe void Main(string[] args)
        {
            int param_bitrate = 16 * 1024;
            int param_channels = 1;
            int param_application = OPUS_APPLICATION_AUDIO;
            int param_signal = OPUS_SIGNAL_MUSIC;
            int param_sample_rate = 48000;
            int param_frame_size = OPUS_FRAMESIZE_60_MS;
            int param_complexity = 8;
            int param_force_mode = OPUS_AUTO;
            int param_use_dtx = 1;
            int param_use_vbr = 1;
            int param_use_contrained_vbr = 1;
            int param_packet_loss_percent = 0;

            string fileNameBase = "D:\\Code\\concentus\\AudioData\\";
            string fileName;
            switch (param_sample_rate)
            {
                case 8000:
                    fileName = fileNameBase + "8Khz " + ((param_channels == 1) ? "Mono.raw" : "Stereo.raw");
                    break;
                case 12000:
                    fileName = fileNameBase + "12Khz " + ((param_channels == 1) ? "Mono.raw" : "Stereo.raw");
                    break;
                case 16000:
                    fileName = fileNameBase + "16Khz " + ((param_channels == 1) ? "Mono.raw" : "Stereo.raw");
                    break;
                case 24000:
                    fileName = fileNameBase + "24Khz " + ((param_channels == 1) ? "Mono.raw" : "Stereo.raw");
                    break;
                case 48000:
                    fileName = fileNameBase + "48Khz " + ((param_channels == 1) ? "Mono.raw" : "Stereo.raw");
                    break;
                default:
                    throw new Exception("Invalid file name");
            }

            using (FileStream fileIn = new FileStream(fileName, FileMode.Open))
            {
                int error;
                OpusEncoder* encoder = opus_encoder_create(param_sample_rate, param_channels, param_application, &error);
                OpusDecoder* decoder = opus_decoder_create(param_sample_rate, param_channels, &error);
                if (param_bitrate > 0)
                {
                    opus_encoder_ctl(encoder, OPUS_SET_BITRATE_REQUEST, param_bitrate);
                }
                opus_encoder_ctl(encoder, OPUS_SET_COMPLEXITY_REQUEST, param_complexity);
                opus_encoder_ctl(encoder, OPUS_SET_DTX_REQUEST, param_use_dtx);
                opus_encoder_ctl(encoder, OPUS_SET_SIGNAL_REQUEST, param_signal);
                if (param_packet_loss_percent > 0)
                {
                    opus_encoder_ctl(encoder, OPUS_SET_PACKET_LOSS_PERC_REQUEST, param_packet_loss_percent);
                    opus_encoder_ctl(encoder, OPUS_SET_INBAND_FEC_REQUEST, 1);
                }
                if (param_force_mode != OPUS_AUTO)
                {
                    opus_encoder_ctl(encoder, OPUS_SET_FORCE_MODE_REQUEST, param_force_mode);
                }
                opus_encoder_ctl(encoder, OPUS_SET_VBR_REQUEST, param_use_vbr);
                opus_encoder_ctl(encoder, OPUS_SET_VBR_CONSTRAINT_REQUEST, param_use_contrained_vbr);

                int packetSamplesPerChannel = -1;
                switch (param_frame_size)
                {
                    case OPUS_FRAMESIZE_2_5_MS:
                        packetSamplesPerChannel = ((param_sample_rate * 2) + (param_sample_rate >> 1)) / 1000;
                        break;
                    case OPUS_FRAMESIZE_5_MS:
                        packetSamplesPerChannel = param_sample_rate * 5 / 1000;
                        break;
                    case OPUS_FRAMESIZE_10_MS:
                        packetSamplesPerChannel = param_sample_rate * 10 / 1000;
                        break;
                    case OPUS_FRAMESIZE_20_MS:
                        packetSamplesPerChannel = param_sample_rate * 20 / 1000;
                        break;
                    case OPUS_FRAMESIZE_40_MS:
                        packetSamplesPerChannel = param_sample_rate * 40 / 1000;
                        break;
                    case OPUS_FRAMESIZE_60_MS:
                        packetSamplesPerChannel = param_sample_rate * 60 / 1000;
                        break;
                    case OPUS_FRAMESIZE_80_MS:
                        packetSamplesPerChannel = param_sample_rate * 80 / 1000;
                        break;
                    case OPUS_FRAMESIZE_100_MS:
                        packetSamplesPerChannel = param_sample_rate * 100 / 1000;
                        break;
                    case OPUS_FRAMESIZE_120_MS:
                        packetSamplesPerChannel = param_sample_rate * 120 / 1000;
                        break;
                }

                int inputBufLength = packetSamplesPerChannel * param_channels * sizeof(short);
                byte* inAudioByte = stackalloc byte[inputBufLength];
                byte* outPacket = stackalloc byte[1275];
                short* inAudioSamples = (short*)inAudioByte;
                Console.Write("NAIL TEST START\r\n");
                int frame = 0;
                while (true)
                {
                    int bytesRead = fileIn.Read(new Span<byte>(inAudioByte, inputBufLength));
                    if (bytesRead < inputBufLength)
                    {
                        break;
                    }

                    int errorOrLength;
                    errorOrLength = opus_encode(encoder, inAudioSamples, packetSamplesPerChannel, outPacket, 1275);
                    Console.WriteLine("ENCODE: " + errorOrLength);

                    if (errorOrLength > 0)
                    {
                        NailTest_PrintByteArray(outPacket, errorOrLength);
                        errorOrLength = opus_decode(decoder, outPacket, errorOrLength, inAudioSamples, packetSamplesPerChannel, 0);
                        Console.WriteLine("DECODE: " + errorOrLength);
                    }

                    frame++;
                }

                opus_encoder_destroy(encoder);
                opus_decoder_destroy(decoder);
            }
        }
    }
}
