using BenchmarkDotNet.Attributes;
using HellaUnsafe.Celt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace CSharpConsole
{
    //[MediumRunJob]
    //[MemoryDiagnoser]
    public class Benchmarks
    {
        internal static int silk_ADD_SAT32_baseline(int a32, int b32)
        {
            int res = (unchecked(((uint)(a32) + (uint)(b32)) & 0x80000000) == 0 ?
                ((((a32) & (b32)) & 0x80000000) != 0 ? int.MinValue : (a32) + (b32)) :
                ((((a32) | (b32)) & 0x80000000) == 0 ? int.MaxValue : (a32) + (b32)));
            return res;
        }

        internal static int silk_ADD_SAT32_fast(int a32, int b32)
        {
            long res = (long)a32 + b32;
            return res < int.MinValue ? int.MinValue : (res > int.MaxValue ? int.MaxValue : (int)res);
        }

        private static int[] a = new int[10000];
        private static int[] b = new int[10000];
        private static int[] result = new int[10000];

        [GlobalSetup]
        public void GlobalSetup()
        {
            Random rand = new Random();
            for (int c = 0; c < a.Length; c++)
            {
                a[c] = rand.Next(int.MinValue, int.MaxValue);
                b[c] = rand.Next(int.MinValue, int.MaxValue);
            }
        }

        [Benchmark(Baseline = true)]
        public void Baseline()
        {
            for (int c = 0; c < a.Length; c++)
            {
                result[c] = silk_ADD_SAT32_baseline(a[c], b[c]);
            }
        }

        [Benchmark]
        public void Test()
        {
            for (int c = 0; c < a.Length; c++)
            {
                result[c] = silk_ADD_SAT32_fast(a[c], b[c]);
            }
        }
    }
}
