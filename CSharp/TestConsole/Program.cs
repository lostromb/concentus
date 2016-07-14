using System;
using System.Diagnostics;
using System.IO;
using Concentus.Common;

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
            SpeexResampler resampler = SpeexResampler.speex_resampler_init(1, 44100, 16000, 10, null);
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
