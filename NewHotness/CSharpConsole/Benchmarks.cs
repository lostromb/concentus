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
        internal static unsafe int float2int_sse(float value)
        {
            return Sse.ConvertToInt32(Sse.LoadScalarVector128(&value));
        }

        internal static unsafe int float2int_round(float value)
        {
            return (int)MathF.Round(value);
        }

        internal static unsafe int float2int_raw(float value)
        {
            return (int) value;
        }

        [GlobalSetup]
        public void GlobalSetup()
        {
        }

        [Benchmark]
        public void Naive()
        {
            float inc2 = 10;
            for (float x = -50; x < 50; x += 0.01f)
            {
                inc2 += float2int_raw(x);
            }
        }

        [Benchmark]
        public void Round()
        {
            float inc2 = 10;
            for (float x = -50; x < 50; x += 0.01f)
            {
                inc2 += float2int_round(x);
            }
        }

        [Benchmark]
        public void SSE()
        {
            float inc2 = 10;
            for (float x = -50; x < 50; x += 0.01f)
            {
                inc2 += float2int_sse(x);
            }
        }
    }
}
