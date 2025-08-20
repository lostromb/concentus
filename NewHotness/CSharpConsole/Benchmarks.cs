using BenchmarkDotNet.Attributes;
using HellaUnsafe.Celt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CSharpConsole
{
    [MediumRunJob]
    [MemoryDiagnoser]
    public class Benchmarks
    {
        [GlobalSetup]
        public void GlobalSetup()
        {
        }

        [Benchmark]
        public void FixedBufferPinning()
        {
        }

        [Benchmark]
        public void HeapArrayRef()
        {
        }
    }
}
