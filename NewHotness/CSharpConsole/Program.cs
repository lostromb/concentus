using BenchmarkDotNet.Running;
using HellaUnsafe.Celt;
using HellaUnsafe.Common;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace CSharpConsole
{
    internal static unsafe class Program
    {
        private static InlineArrayOpusEncoder _encoder;


        private static ReadOnlySpan<sbyte> Test3DArray_Data/*[4][2][5]*/ =>
        [
                4,      6,     24,      7,      5,
                0,      0,      2,      0,      0,
                12,     28,     41,     13,     -4,
                -9,     15,     42,     25,     14,
                1,     -2,     62,     41,     -9,
                -10,     37,     65,     -4,      3,
                -6,      4,     66,      7,     -8,
                16,     14,     38,     -3,     33,
        ];

        private static readonly Native3DArray<sbyte> Test3DArray = new Native3DArray<sbyte>(4, 2, 5, Test3DArray_Data);

        internal static int ILog2(uint x)
        {
            return x == 0 ? 1 : 32 - BitOperations.LeadingZeroCount(x);
        }

        public static unsafe void Main(string[] args)
        {
            for (uint c = 0; c < 256; c++)
            {
                int expect = EntCode.EC_ILOG(c);
                int actual = ILog2(c);
                Console.WriteLine($"{c}\t Expect {expect}\tActual {actual}\tMatch? {actual == expect}");
            }

            return;

            Console.WriteLine(Test3DArray[0][0][0]);
            Console.WriteLine(Test3DArray[3][0][3]);
            Console.WriteLine(Test3DArray[0][1][0]);
            Console.WriteLine(Test3DArray[3][1][3]);
            Console.WriteLine(Test3DArray[0][0][7]);

            Span<int> span = stackalloc int[5];
            int* ptr = CRuntime.SpanToPointerDangerous(span);
            for (int c = 0; c < 5; c++)
            {
                Console.WriteLine(ptr[c] + " " + span[c]);
                ptr[c] = c;
                Console.WriteLine(ptr[c] + " " + span[c]);
            }

            //StructRef<FixedBufferSilkEncoder> encoder = new StructRef<FixedBufferSilkEncoder>(new FixedBufferSilkEncoder());
            //fixed (FixedBufferSilkEncoder* enc = &encoder.Value)
            //{
            //    enc->buffer[10] = 10;
            //    //BenchmarkRunner.Run<Benchmarks>();
            //}

            //int size = sizeof(FixedBufferOpusEncoder);
            //Console.WriteLine(size);
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
        public int* ptr;
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
