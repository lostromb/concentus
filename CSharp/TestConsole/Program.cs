using System;
using System.Diagnostics;
using System.IO;
using Concentus.Common;

namespace ConcentusDemo
{
    public class Program
    {
        private static int functionA(int val)
        {
            return Inlines.EC_CLZ((uint)val);
        }

        private static int functionB(int val)
        {
            return Inlines.EC_CLZ((uint)val);
        }

        public static void Main(string[] args)
        {
            for (int c = 0; c < 1000000; c += 10000)
            {
                Console.WriteLine("{0}\t{1}", functionA(c), functionB(c));
            }

            Stopwatch timerz = new Stopwatch();
            timerz.Start();
            for (int c = 0; c < 1000000; c++)
            {
                functionA(c);
            }
            timerz.Stop();
            Console.WriteLine(timerz.ElapsedTicks);
            timerz.Restart();
            for (int c = 0; c < 1000000; c++)
            {
                functionB(c);
            }
            timerz.Stop();
            Console.WriteLine(timerz.ElapsedTicks);
        }
    }
}
