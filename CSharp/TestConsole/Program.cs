using System;
using System.Diagnostics;
using System.IO;
using Concentus.Common;
using Concentus.Common.CPlusPlus;

namespace ConcentusDemo
{
    public class Program
    {
        private const int input_q = 16;
        private const int output_q = 16;

        private static int functionA(int a, int b, short c)
        {
            return Inlines.silk_SMLAWB(a, b, c);
        }

        private const float f16 = (1 / 65536f);
        private static int functionB(int a, int b, short c)
        {
            //return a + (int)(((long)b * c) >> 16);
            //return a + (int)((float)b * (float)c * f16);
            return a + (int)(((b >> 8) * (c >> 8)));
        }

        public static int ToFixed(float x, int q)
        {
            return (int)((float)(1 << q) * x);
        }

        public static float ToFloat(int x, int q)
        {
            return (float)x / (float)(1 << q);
        }

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

        public static void TestFastMath()
        {
            Random rand = new Random();
            
            //for (int z = 0;  z< 100; z++)
            //{
            //    int a = rand.Next(-1000000, 1000000);
            //    int b = rand.Next(-1000000, 1000000);
            //    short c = (short)rand.Next(-30000, 30000);
            //    Console.WriteLine("{0:F3} (0x{1:x})\t+\t{2:F3} (0x{3:x})\t*\t{4:F3} (0x{5:x})\t=\t{6:F3}", ToFloat(a, input_q), a, ToFloat(b, input_q), b, ToFloat(c, input_q), c, ToFloat(functionA(a, b, c), output_q));
            //    Console.WriteLine("{0:F3} (0x{1:x})\t+\t{2:F3} (0x{3:x})\t*\t{4:F3} (0x{5:x})\t=\t{6:F3}", ToFloat(a, input_q), a, ToFloat(b, input_q), b, ToFloat(c, input_q), c, ToFloat(functionB(a, b, c), output_q));
            //    Console.WriteLine();
            //}

            Stopwatch timerz = new Stopwatch();
            timerz.Start();
            int a = rand.Next(-1000000, 1000000);
            int b = rand.Next(-1000000, 1000000);
            short c = (short)rand.Next(-30000, 30000);
            for (long z = 0; z < 10000000000L; z++)
            {
                
                functionA(a, b, c);
            }
            timerz.Stop();
            Console.WriteLine(1000 * timerz.ElapsedTicks / Stopwatch.Frequency);
            timerz.Restart();
            
            for (long z = 0; z < 10000000000L; z++)
            {
                
                functionB(a, b, c);
            }
            timerz.Stop();
            Console.WriteLine(1000 * timerz.ElapsedTicks / Stopwatch.Frequency);
        }
    }
}
