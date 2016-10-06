using System;
using System.Diagnostics;
using System.IO;
using Concentus.Common;
using Concentus.Common.CPlusPlus;

namespace ConcentusDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            int sourceFreq = 48000;
            int targetFreq = 8000;
            SpeexResampler resampler = SpeexResampler.Create(2, sourceFreq, targetFreq, 10);
            byte[] sineSweep = File.ReadAllBytes(@"C:\Users\lostromb\Documents\Visual Studio 2015\Projects\Durandal\old junk\Sine Sweep Stereo 48k.raw");
            short[] samples = BytesToShorts(sineSweep);
            short[] resampled = new short[(int)((long)samples.Length * (long)targetFreq / (long)sourceFreq)];
            int in_len = samples.Length / 2;
            int out_len = resampled.Length / 2;
            resampler.ProcessInterleaved(samples, 0, ref in_len, resampled, 0, ref out_len);
            byte[] resampledBytes = ShortsToBytes(resampled, 0, out_len * 2);
            File.WriteAllBytes(@"C:\Users\lostromb\Documents\Visual Studio 2015\Projects\Durandal\old junk\Resampled.raw", resampledBytes);
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
