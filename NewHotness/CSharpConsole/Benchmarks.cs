using BenchmarkDotNet.Attributes;
using HellaUnsafe.Celt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using static HellaUnsafe.Common.CRuntime;

namespace CSharpConsole
{
    //[MediumRunJob]
    //[MemoryDiagnoser]
    public unsafe class Benchmarks
    {
        internal static float MAC16_16(float c, float a, float b) { return c + a * b; }
        //internal static float MAC16_16(float c, float a, float b) { return MathF.FusedMultiplyAdd(a, b, c); }

        internal static unsafe void xcorr_kernel_c_naive(float* x, float* y, float* sum/*[4]*/, int len)
        {
            for (int j = 0; j < len - 3; j++)
            {
                float tmp = *x++;
                sum[0] += (tmp * y[0]);
                sum[1] += (tmp * y[1]);
                sum[2] += (tmp * y[2]);
                sum[3] += (tmp * y[3]);
                y++;
            }
        }

        internal static unsafe void xcorr_kernel_c_unrolled(float* x, float* y, float* sum/*[4]*/, int len)
        {
            int j;
            float y_0, y_1, y_2, y_3;
            y_3 = 0; /* gcc doesn't realize that y_3 can't be used uninitialized */
            y_0 = *y++;
            y_1 = *y++;
            y_2 = *y++;
            for (j = 0; j < len - 3; j += 4)
            {
                float tmp;
                tmp = *x++;
                y_3 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_0);
                sum[1] = MAC16_16(sum[1], tmp, y_1);
                sum[2] = MAC16_16(sum[2], tmp, y_2);
                sum[3] = MAC16_16(sum[3], tmp, y_3);
                tmp = *x++;
                y_0 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_1);
                sum[1] = MAC16_16(sum[1], tmp, y_2);
                sum[2] = MAC16_16(sum[2], tmp, y_3);
                sum[3] = MAC16_16(sum[3], tmp, y_0);
                tmp = *x++;
                y_1 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_2);
                sum[1] = MAC16_16(sum[1], tmp, y_3);
                sum[2] = MAC16_16(sum[2], tmp, y_0);
                sum[3] = MAC16_16(sum[3], tmp, y_1);
                tmp = *x++;
                y_2 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_3);
                sum[1] = MAC16_16(sum[1], tmp, y_0);
                sum[2] = MAC16_16(sum[2], tmp, y_1);
                sum[3] = MAC16_16(sum[3], tmp, y_2);
            }
            if (j++ < len)
            {
                float tmp = *x++;
                y_3 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_0);
                sum[1] = MAC16_16(sum[1], tmp, y_1);
                sum[2] = MAC16_16(sum[2], tmp, y_2);
                sum[3] = MAC16_16(sum[3], tmp, y_3);
            }
            if (j++ < len)
            {
                float tmp = *x++;
                y_0 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_1);
                sum[1] = MAC16_16(sum[1], tmp, y_2);
                sum[2] = MAC16_16(sum[2], tmp, y_3);
                sum[3] = MAC16_16(sum[3], tmp, y_0);
            }
            if (j < len)
            {
                float tmp = *x++;
                y_1 = *y++;
                sum[0] = MAC16_16(sum[0], tmp, y_2);
                sum[1] = MAC16_16(sum[1], tmp, y_3);
                sum[2] = MAC16_16(sum[2], tmp, y_0);
                sum[3] = MAC16_16(sum[3], tmp, y_1);
            }
        }

        internal static unsafe void xcorr_kernel_vector(float* x, float* y, float* sum/*[4]*/, int len)
        {
            int vectorEnd = len - 3 - ((len - 3) % Vector<float>.Count);
            int idx = 0;
            Vector<float> sum0, sum1, sum2, sum3;
            sum0 = sum1 = sum2 = sum3 = Vector<float>.Zero;
            while (idx < vectorEnd)
            {
                idx += Vector<float>.Count;
                Vector<float> xVec = new Vector<float>(new ReadOnlySpan<float>(x + idx, Vector<float>.Count));
                sum0 = Vector.Add(sum0, Vector.Multiply(
                    xVec,
                    new Vector<float>(new ReadOnlySpan<float>(y + idx, Vector<float>.Count))));
                sum1 = Vector.Add(sum1, Vector.Multiply(
                    xVec,
                    new Vector<float>(new ReadOnlySpan<float>(y + idx + 1, Vector<float>.Count))));
                sum2 = Vector.Add(sum2, Vector.Multiply(
                    xVec,
                    new Vector<float>(new ReadOnlySpan<float>(y + idx + 2, Vector<float>.Count))));
                sum3 = Vector.Add(sum3, Vector.Multiply(
                    xVec,
                    new Vector<float>(new ReadOnlySpan<float>(y + idx + 3, Vector<float>.Count))));
                idx += Vector<float>.Count;
            }

            // fixme handle the residual

            sum[0] = Vector.Dot(sum0, Vector<float>.One);
            sum[1] = Vector.Dot(sum1, Vector<float>.One);
            sum[2] = Vector.Dot(sum2, Vector<float>.One);
            sum[3] = Vector.Dot(sum3, Vector<float>.One);
        }


        internal static readonly int* avx_mask = AllocateGlobalArray<int>(15, new int[]
            { -1, -1, -1, -1, -1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0 });

        internal static unsafe void xcorr_kernel_avx(float* x, float* y, float* sum/*[4]*/, int len)
        {
            Vector256<float> xsum0, xsum1, xsum2, xsum3, xsum4, xsum5, xsum6, xsum7;
            xsum7 = xsum6 = xsum5 = xsum4 = xsum3 = xsum2 = xsum1 = xsum0 = Vector256<float>.Zero;
            int i;
            Vector256<float> x0;
            /* Compute 8 inner products using partial sums. */
            for (i = 0; i < len - 7; i += 8)
            {
                x0 = Avx.LoadVector256(x + i);
                xsum0 = Fma.MultiplyAdd(x0, Avx.LoadVector256(y + i), xsum0);
                xsum1 = Fma.MultiplyAdd(x0, Avx.LoadVector256(y + i + 1), xsum1);
                xsum2 = Fma.MultiplyAdd(x0, Avx.LoadVector256(y + i + 2), xsum2);
                xsum3 = Fma.MultiplyAdd(x0, Avx.LoadVector256(y + i + 3), xsum3);
                xsum4 = Fma.MultiplyAdd(x0, Avx.LoadVector256(y + i + 4), xsum4);
                xsum5 = Fma.MultiplyAdd(x0, Avx.LoadVector256(y + i + 5), xsum5);
                xsum6 = Fma.MultiplyAdd(x0, Avx.LoadVector256(y + i + 6), xsum6);
                xsum7 = Fma.MultiplyAdd(x0, Avx.LoadVector256(y + i + 7), xsum7);
            }
            if (i != len)
            {
                Vector256<float> m = Vector256.AsSingle(Avx.LoadVector256(avx_mask + 7 + i - len));
                x0 = Avx.MaskLoad(x + i, m);
                xsum0 = Fma.MultiplyAdd(x0, Avx.MaskLoad(y + i, m), xsum0);
                xsum1 = Fma.MultiplyAdd(x0, Avx.MaskLoad(y + i + 1, m), xsum1);
                xsum2 = Fma.MultiplyAdd(x0, Avx.MaskLoad(y + i + 2, m), xsum2);
                xsum3 = Fma.MultiplyAdd(x0, Avx.MaskLoad(y + i + 3, m), xsum3);
                xsum4 = Fma.MultiplyAdd(x0, Avx.MaskLoad(y + i + 4, m), xsum4);
                xsum5 = Fma.MultiplyAdd(x0, Avx.MaskLoad(y + i + 5, m), xsum5);
                xsum6 = Fma.MultiplyAdd(x0, Avx.MaskLoad(y + i + 6, m), xsum6);
                xsum7 = Fma.MultiplyAdd(x0, Avx.MaskLoad(y + i + 7, m), xsum7);
            }
            /* 8 horizontal adds. */
            /* Compute [0 4] [1 5] [2 6] [3 7] */
            xsum0 = Avx.Add(Avx.Permute2x128(xsum0, xsum4, 2 << 4), Avx.Permute2x128(xsum0, xsum4, 1 | (3 << 4)));
            xsum1 = Avx.Add(Avx.Permute2x128(xsum1, xsum5, 2 << 4), Avx.Permute2x128(xsum1, xsum5, 1 | (3 << 4)));
            xsum2 = Avx.Add(Avx.Permute2x128(xsum2, xsum6, 2 << 4), Avx.Permute2x128(xsum2, xsum6, 1 | (3 << 4)));
            xsum3 = Avx.Add(Avx.Permute2x128(xsum3, xsum7, 2 << 4), Avx.Permute2x128(xsum3, xsum7, 1 | (3 << 4)));
            /* Compute [0 1 4 5] [2 3 6 7] */
            xsum0 = Avx.HorizontalAdd(xsum0, xsum1);
            xsum1 = Avx.HorizontalAdd(xsum2, xsum3);
            /* Compute [0 1 2 3 4 5 6 7] */
            xsum0 = Avx.HorizontalAdd(xsum0, xsum1);
            Avx.Store(sum, xsum0);
        }

        private static float[] a = new float[10000];
        private static float[] b = new float[10000];

        [GlobalSetup]
        public void GlobalSetup()
        {
            Random rand = new Random();
            for (int c = 0; c < a.Length; c++)
            {
                a[c] = (float)rand.NextDouble();
                b[c] = (float)rand.NextDouble();
            }
        }

        [Benchmark(Baseline = true)]
        public void XCorrCUnrolled()
        {
            float* sum = stackalloc float[16];
            new Span<float>(sum, 16).Fill(0);
            fixed (float* aPtr = a)
            fixed (float* bPtr = b)
            {
                xcorr_kernel_c_unrolled(aPtr, bPtr, sum, a.Length);
            }
        }

        [Benchmark]
        public void XCorrCNaive()
        {
            float* sum = stackalloc float[16];
            new Span<float>(sum, 16).Fill(0);
            fixed (float* aPtr = a)
            fixed (float* bPtr = b)
            {
                xcorr_kernel_c_naive(aPtr, bPtr, sum, a.Length);
            }
        }

        [Benchmark]
        public void XCorrAVX()
        {
            float* sum = stackalloc float[16];
            new Span<float>(sum, 16).Fill(0);
            fixed (float* aPtr = a)
            fixed (float* bPtr = b)
            {
                xcorr_kernel_avx(aPtr, bPtr, sum, a.Length);
            }
        }

        [Benchmark]
        public void XCorrVector()
        {
            float* sum = stackalloc float[16];
            new Span<float>(sum, 16).Fill(0);
            fixed (float* aPtr = a)
            fixed (float* bPtr = b)
            {
                xcorr_kernel_vector(aPtr, bPtr, sum, a.Length);
            }
        }
    }
}
