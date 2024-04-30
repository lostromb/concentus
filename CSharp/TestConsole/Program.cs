using System;
using System.Diagnostics;
using System.IO;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Structs;
using Concentus.Enums;
using Concentus;

namespace ConcentusDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            FileStream fileIn = new FileStream(@"C:\Code\concentus\AudioData\16Khz Mono.raw", FileMode.Open);
            IOpusEncoder encoder = OpusCodecFactory.CreateEncoder(16000, 1, OpusApplication.OPUS_APPLICATION_AUDIO, Console.Out);
            encoder.Bitrate = (96000);
            encoder.ForceMode = (OpusMode.MODE_CELT_ONLY);
            encoder.SignalType = (OpusSignal.OPUS_SIGNAL_MUSIC);
            encoder.Complexity = (0);

            IOpusDecoder decoder = OpusCodecFactory.CreateDecoder(16000, 1, Console.Out);

            FileStream fileOut = new FileStream(@"C:\Code\concentus\AudioData\test-decoded.raw", FileMode.Create);
            int packetSamples = 960;
            byte[] inBuf = new byte[packetSamples * 2];
            byte[] data_packet = new byte[1275];
            while (fileIn.Length - fileIn.Position >= inBuf.Length)
            {
                int bytesRead = fileIn.Read(inBuf, 0, inBuf.Length);
                short[] pcm = BytesToShorts(inBuf, 0, inBuf.Length);
                int bytesEncoded = encoder.Encode(pcm.AsSpan(), packetSamples, data_packet.AsSpan(), 1275);
                //System.out.println(bytesEncoded + " bytes encoded");

                int samplesDecoded = decoder.Decode(data_packet.AsSpan(0, bytesEncoded), pcm.AsSpan(), packetSamples, false);
                //System.out.println(samplesDecoded + " samples decoded");
                byte[] bytesOut = ShortsToBytes(pcm);
                fileOut.Write(bytesOut, 0, bytesOut.Length);
            }
            
            fileIn.Close();
            fileOut.Close();
        }

        /// <summary>
        /// Converts interleaved byte samples (such as what you get from a capture device)
        /// into linear short samples (that are much easier to work with)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static short[] BytesToShorts(byte[] input)
        {
            return BytesToShorts(input, 0, input.Length);
        }

        /// <summary>
        /// Converts interleaved byte samples (such as what you get from a capture device)
        /// into linear short samples (that are much easier to work with)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static short[] BytesToShorts(byte[] input, int offset, int length)
        {
            short[] processedValues = new short[length / 2];
            for (int c = 0; c < processedValues.Length; c++)
            {
                processedValues[c] = (short)(((int)input[(c * 2) + offset]) << 0);
                processedValues[c] += (short)(((int)input[(c * 2) + 1 + offset]) << 8);
            }

            return processedValues;
        }

        /// <summary>
        /// Converts linear short samples into interleaved byte samples, for writing to a file, waveout device, etc.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] ShortsToBytes(short[] input)
        {
            return ShortsToBytes(input, 0, input.Length);
        }

        /// <summary>
        /// Converts linear short samples into interleaved byte samples, for writing to a file, waveout device, etc.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] ShortsToBytes(short[] input, int offset, int length)
        {
            byte[] processedValues = new byte[length * 2];
            for (int c = 0; c < length; c++)
            {
                processedValues[c * 2] = (byte)(input[c + offset] & 0xFF);
                processedValues[c * 2 + 1] = (byte)((input[c + offset] >> 8) & 0xFF);
            }

            return processedValues;
        }
    }
}
