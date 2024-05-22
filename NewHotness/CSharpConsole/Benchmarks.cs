using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CSharpConsole
{
    public class Benchmarks
    {
        private ArrayRefOpusEncoder arrayRefEncoder;
        private FixedBufferOpusEncoder _fixedBufferEncoder;
        private InlineArrayOpusEncoder _inlineArrayEncoder;

        public Benchmarks()
        {
            arrayRefEncoder = new ArrayRefOpusEncoder();
            arrayRefEncoder.buffer = new int[128];
            arrayRefEncoder.silkEncoder.buffer = new int[128];
            _fixedBufferEncoder = new FixedBufferOpusEncoder();
            _inlineArrayEncoder = new InlineArrayOpusEncoder();
        }

        [Benchmark]
        public void FixedBufferPinning()
        {
            EncoderFixedBuffer(ref _fixedBufferEncoder);
        }

        [Benchmark]
        public void HeapArrayRef()
        {
            EncodeArrayRef(ref arrayRefEncoder);
        }

        [Benchmark]
        public void InlineArray()
        {
            EncodeInlineArray(ref _inlineArrayEncoder);
        }

        private static unsafe void EncoderFixedBuffer(ref FixedBufferOpusEncoder encoder)
        {
            fixed (int* ptr = encoder.silkEncoder.buffer)
            {
                Span<int> buf = new Span<int>(ptr, 128);
                DoSomethingWithSpan(buf);
            }
        }

        private static void EncodeArrayRef(ref ArrayRefOpusEncoder encoder)
        {
            Span<int> buf = encoder.silkEncoder.buffer.AsSpan();
            DoSomethingWithSpan(buf);
        }

        private static void EncodeInlineArray(ref InlineArrayOpusEncoder encoder)
        {
            Span<int> buf = MemoryMarshal.CreateSpan(ref encoder.silkEncoder.buffer[0], 128);
            DoSomethingWithSpan(buf);
        }

        private static void DoSomethingWithSpan(Span<int> data)
        {
            data.Fill(128);
        }
    }
}
