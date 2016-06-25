using System;
using System.Diagnostics;
using System.IO;
using Concentus.Common;

namespace ConcentusDemo
{
    public class Program
    {
        private const int input_q = 16;
        private const int output_q = 17;

        private static int functionA(int a, int b)
        {
            return Inlines.MULT16_32_Q15(a, b);
        }

        private static int functionB(int a, int b)
        {
            //return ((a * (b >> 16)) << 1) + ((a * (b & 0xFFFF)) >> 15);

            float af = (float)a / (float)(1 << input_q);
            float bf = (float)b / (float)(1 << input_q);
            return (int)((af * bf) * (float)(1 << output_q));
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
            Random rand = new Random();
            
            for (int c = 0; c < 100; c++)
            {
                int a = rand.Next(-1000000, 1000000);
                int b = rand.Next(-1000000, 1000000);
                Console.WriteLine("{0:F3} (0x{1:x})\t*\t{2:F3} (0x{3:x})\t=\t{4:F3}", ToFloat(a, input_q), a, ToFloat(b, input_q), b, ToFloat(functionA(a, b), output_q));
                Console.WriteLine("{0:F3} (0x{1:x})\t*\t{2:F3} (0x{3:x})\t=\t{4:F3}", ToFloat(a, input_q), a, ToFloat(b, input_q), b, ToFloat(functionB(a, b), output_q));
                Console.WriteLine();
            }

            Stopwatch timerz = new Stopwatch();
            timerz.Start();
            for (int c = 0; c < 10000000; c++)
            {
                int a = rand.Next(-1000000, 1000000);
                int b = rand.Next(-1000000, 1000000);
                functionA(a, b);
            }
            timerz.Stop();
            Console.WriteLine(timerz.ElapsedTicks);
            timerz.Restart();
            for (int c = 0; c < 10000000; c++)
            {
                int a = rand.Next(-1000000, 1000000);
                int b = rand.Next(-1000000, 1000000);
                functionB(a, b);
            }
            timerz.Stop();
            Console.WriteLine(timerz.ElapsedTicks);
        }
    }
}
