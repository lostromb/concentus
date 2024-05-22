using BenchmarkDotNet.Running;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace CSharpConsole
{
    internal static class Program
    {
        private static InlineArrayOpusEncoder _encoder;

        public static unsafe void Main(string[] args)
        {
            _encoder = new InlineArrayOpusEncoder();
            for (int c = 0; c < 10; c++)
            {
                Encode(ref _encoder);
                Console.WriteLine("FramesEncoded is now " + _encoder.framesEncoded);
            }
        }

        public static unsafe void Encode(ref InlineArrayOpusEncoder encoder)
        {
            encoder.buffer[encoder.framesEncoded % 5] = encoder.framesEncoded + 1;
            InlineArrayOpusEncoder clone = encoder;
            MemoryMarshal.CreateSpan(ref clone.buffer[0], 128).Fill(10);
            encoder.framesEncoded++;
            ref InlineArrayOpusEncoder aNewRef = ref encoder;
            aNewRef.silkEncoder.silkFramesEncoded++;
        }
    }

    internal unsafe struct FixedBufferOpusEncoder
    {
        public int framesEncoded;
        public FixedBufferSilkEncoder silkEncoder;
        public fixed int buffer[128];
    }

    internal unsafe struct FixedBufferSilkEncoder
    {
        public int silkFramesEncoded;
        public fixed int buffer[128];
    }

    internal struct ArrayRefOpusEncoder
    {
        public int framesEncoded;
        public ArrayRefSilkEncoder silkEncoder;
        public int[] buffer;
    }

    internal struct ArrayRefSilkEncoder
    {
        public int silkFramesEncoded;
        public int[] buffer;
    }

    [System.Runtime.CompilerServices.InlineArray(128)]
    public struct BufferInt128
    {
        private int _element;
    }

    internal struct InlineArrayOpusEncoder
    {
        public int framesEncoded;
        public InlineArraySilkEncoder silkEncoder;
        public BufferInt128 buffer;
    }

    internal struct InlineArraySilkEncoder
    {
        public int silkFramesEncoded;
        public BufferInt128 buffer;
    }
}
