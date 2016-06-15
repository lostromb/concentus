using Concentus.Celt.Enums;
using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Celt
{
    public static class mdct
    {
        private const bool TRACE_FILE = false;
        
        /* Forward MDCT trashes the input array */
        public static void clt_mdct_forward_c(mdct_lookup l, Pointer<int> input, Pointer<int> output,
            Pointer<int> window, int overlap, int shift, int stride, int arch)
        {
            int i;
            int N, N2, N4;
            Pointer<int> f;
            Pointer<kiss_fft_cpx> f2;
            kiss_fft_state st = l.kfft[shift];
            Pointer<short> trig;
            int scale;
            
            int scale_shift = st.scale_shift - 1;
            scale = st.scale;

            N = l.n;
            trig = l.trig;
            for (i = 0; i < shift; i++)
            {
                N = N >> 1;
                trig = trig.Point(N);
            }
            N2 = N >> 1;
            N4 = N >> 2;

            f = Pointer.Malloc<int>(N2);
            f2 = Pointer.Malloc<kiss_fft_cpx>(N4);
            for (int c = 0; c < N4; c++)
            {
                f2[c] = new kiss_fft_cpx();
            }

            /* Consider the input to be composed of four blocks: [a, b, c, d] */
            /* Window, shuffle, fold */
            {
                /* Temp pointers to make it really clear to the compiler what we're doing */
                // fixme: can these be boxed? or just removed
                Pointer<int> xp1 = input.Point(overlap >> 1);
                Pointer<int> xp2 = input.Point(N2 - 1 + (overlap >> 1));
                Pointer<int> yp = f;
                Pointer<int> wp1 = window.Point(overlap >> 1);
                Pointer<int> wp2 = window.Point((overlap >> 1) - 1);
                for (i = 0; i < ((overlap + 3) >> 2); i++)
                {
                    /* Real part arranged as -d-cR, Imag part arranged as -b+aR*/
                    yp[0] = Inlines.MULT16_32_Q15(wp2[0], xp1[N2]) + Inlines.MULT16_32_Q15(wp1[0], xp2[0]);
                    if (TRACE_FILE) Debug.WriteLine("13a 0x{0:x}", (uint)yp[0]);
                    yp = yp.Point(1);
                    yp[0] = Inlines.MULT16_32_Q15(wp1[0], xp1[0]) - Inlines.MULT16_32_Q15(wp2[0], xp2[0 - N2]);
                    if (TRACE_FILE) Debug.WriteLine("13b 0x{0:x}", (uint)yp[0]);
                    yp = yp.Point(1);
                    xp1 = xp1.Point(2);
                    xp2 = xp2.Point(-2);
                    wp1 = wp1.Point(2);
                    wp2 = wp2.Point(-2);
                }
                wp1 = window;
                wp2 = window.Point(overlap - 1);
                for (; i < N4 - ((overlap + 3) >> 2); i++)
                {
                    /* Real part arranged as a-bR, Imag part arranged as -c-dR */
                    yp[0] = xp2[0];
                    if (TRACE_FILE) Debug.WriteLine("13c 0x{0:x}", (uint)yp[0]);
                    yp = yp.Point(1);
                    yp[0] = xp1[0];
                    if (TRACE_FILE) Debug.WriteLine("13d 0x{0:x}", (uint)yp[0]);
                    yp = yp.Point(1);
                    xp1 = xp1.Point(2);
                    xp2 = xp2.Point(-2);
                }
                for (; i < N4; i++)
                {
                    /* Real part arranged as a-bR, Imag part arranged as -c-dR */
                    yp[0] = Inlines.MULT16_32_Q15(wp2[0], xp2[0]) - Inlines.MULT16_32_Q15(wp1[0], xp1[0 - N2]);
                    if (TRACE_FILE) Debug.WriteLine("13e 0x{0:x}", (uint)yp[0]);
                    yp = yp.Point(1);
                    yp[0] = Inlines.MULT16_32_Q15(wp2[0], xp1[0]) + Inlines.MULT16_32_Q15(wp1[0], xp2[N2]);
                    if (TRACE_FILE) Debug.WriteLine("13f 0x{0:x}", (uint)yp[0]);
                    yp = yp.Point(1);
                    xp1 = xp1.Point(2);
                    xp2 = xp2.Point(-2);
                    wp1 = wp1.Point(2);
                    wp2 = wp2.Point(-2);
                }
            }
            /* Pre-rotation */
            {
                Pointer<int> yp = f;
                Pointer<short> t = trig;
                for (i = 0; i < N4; i++)
                {
                    kiss_fft_cpx yc = new kiss_fft_cpx(); // [porting note] stack variable
                    short t0, t1;
                    int re, im, yr, yi;
                    t0 = t[i];
                    t1 = t[N4 + i];
                    re = yp[0];
                    if (TRACE_FILE) Debug.WriteLine("13g 0x{0:x}", (uint)yp[0]);
                    yp = yp.Point(1);
                    im = yp[0];
                    if (TRACE_FILE) Debug.WriteLine("13h 0x{0:x}", (uint)yp[0]);
                    yp = yp.Point(1);
                    yr = KissFFT.S_MUL(re, t0) - KissFFT.S_MUL(im, t1);
                    yi = KissFFT.S_MUL(im, t0) + KissFFT.S_MUL(re, t1);
                    yc.r = yr;
                    yc.i = yi;
                    yc.r = Inlines.PSHR32(Inlines.MULT16_32_Q16(scale, yc.r), scale_shift);
                    yc.i = Inlines.PSHR32(Inlines.MULT16_32_Q16(scale, yc.i), scale_shift);
                    f2[st.bitrev[i]].Assign(yc); // fixme: no need for assign()?
                }
            }

            /* N/4 complex FFT, does not downscale anymore */
            KissFFT.opus_fft_impl(st, f2);

            /* Post-rotate */
            {
                /* Temp pointers to make it really clear to the compiler what we're doing */
                Pointer<kiss_fft_cpx> fp = f2;
                Pointer<int> yp1 = output;
                Pointer<int> yp2 = output.Point(stride * (N2 - 1));
                Pointer<short> t = trig;
                for (i = 0; i < N4; i++)
                {
                    int yr, yi;
                    yr = KissFFT.S_MUL(fp[0].i, t[N4 + i]) - KissFFT.S_MUL(fp[0].r, t[i]);
                    yi = KissFFT.S_MUL(fp[0].r, t[N4 + i]) + KissFFT.S_MUL(fp[0].i, t[i]);
                    yp1[0] = yr;
                    yp2[0] = yi;
                    fp = fp.Point(1);
                    yp1 = yp1.Point(2 * stride);
                    yp2 = yp2.Point(0 - (2 * stride));
                }
            }

        }

        public static void clt_mdct_backward_c(mdct_lookup l, Pointer<int> input, Pointer<int> output,
              Pointer<int> window, int overlap, int shift, int stride, int arch)
        {
            int i;
            int N, N2, N4;
            Pointer<short> trig;

            N = l.n;
            trig = l.trig;
            for (i = 0; i < shift; i++)
            {
                N >>= 1;
                trig = trig.Point(N);
            }
            N2 = N >> 1;
            N4 = N >> 2;

            /* Pre-rotate */
            {
                /* Temp pointers to make it really clear to the compiler what we're doing */
                // FIXME: these can probably go away
                Pointer<int> xp1 = input;
                Pointer<int> xp2 = input.Point(stride * (N2 - 1));
                Pointer<int> yp = output.Point(overlap >> 1);
                Pointer<short> t = trig;
                Pointer<short> bitrev = l.kfft[shift].bitrev;
                for (i = 0; i < N4; i++)
                {
                    int rev;
                    int yr, yi;
                    rev = bitrev[0];
                    bitrev = bitrev.Point(1);
                    yr = KissFFT.S_MUL(xp2[0], t[i]) + KissFFT.S_MUL(xp1[0], t[N4 + i]);
                    yi = KissFFT.S_MUL(xp1[0], t[i]) - KissFFT.S_MUL(xp2[0], t[N4 + i]);
                    /* We swap real and imag because we use an FFT instead of an IFFT. */
                    yp[2 * rev + 1] = yr;
                    yp[2 * rev] = yi;
                    /* Storing the pre-rotation directly in the bitrev order. */
                    xp1 = xp1.Point(2 * stride);
                    xp2 = xp2.Point(0 - (2 * stride));
                }
            }

            // FIXME is nfft the right length to use? or n / 2?
            // FIXME super slow janky code, needs to be optimized
            kiss_fft_cpx[] complexArray = kiss_fft_cpx.ConvertInterleavedIntArray(output.Point(overlap >> 1), l.kfft[shift].nfft);

            KissFFT.opus_fft_impl(l.kfft[shift], complexArray.GetPointer());

            kiss_fft_cpx.WriteComplexValuesToInterleavedIntArray(complexArray.GetPointer(), output.Point(overlap >> 1), l.kfft[shift].nfft);

            /* Post-rotate and de-shuffle from both ends of the buffer at once to make
               it in-place. */
            {
                Pointer<int> yp0 = output.Point((overlap >> 1));
                Pointer<int> yp1 = output.Point((overlap >> 1) + N2 - 2);
                Pointer<short> t = trig;

                /* Loop to (N4+1)>>1 to handle odd N4. When N4 is odd, the
                   middle pair will be computed twice. */
                for (i = 0; i < (N4 + 1) >> 1; i++)
                {
                    int re, im, yr, yi;
                    short t0, t1;
                    /* We swap real and imag because we're using an FFT instead of an IFFT. */
                    re = yp0[1];
                    im = yp0[0];
                    t0 = t[i];
                    t1 = t[N4 + i];
                    /* We'd scale up by 2 here, but instead it's done when mixing the windows */
                    yr = KissFFT.S_MUL(re, t0) + KissFFT.S_MUL(im, t1);
                    yi = KissFFT.S_MUL(re, t1) - KissFFT.S_MUL(im, t0);
                    /* We swap real and imag because we're using an FFT instead of an IFFT. */
                    re = yp1[1];
                    im = yp1[0];
                    yp0[0] = yr;
                    yp1[1] = yi;

                    t0 = t[(N4 - i - 1)];
                    t1 = t[(N2 - i - 1)];
                    /* We'd scale up by 2 here, but instead it's done when mixing the windows */
                    yr = KissFFT.S_MUL(re, t0) + KissFFT.S_MUL(im, t1);
                    yi = KissFFT.S_MUL(re, t1) - KissFFT.S_MUL(im, t0);
                    yp1[0] = yr;
                    yp0[1] = yi;
                    yp0 = yp0.Point(2);
                    yp1 = yp1.Point(-2);
                }
            }

            /* Mirror on both sides for TDAC */
            {
                // fixme: remove these temps
                Pointer<int> xp1 = output.Point(overlap - 1);
                Pointer<int> yp1 = output;
                Pointer<int> wp1 = window;
                Pointer<int> wp2 = window.Point(overlap - 1);

                for (i = 0; i < overlap / 2; i++)
                {
                    int x1, x2;
                    x1 = xp1[0];
                    x2 = yp1[0];
                    yp1[0] = Inlines.MULT16_32_Q15(wp2[0], x2) - Inlines.MULT16_32_Q15(wp1[0], x1);
                    yp1 = yp1.Point(1);
                    xp1[0] = Inlines.MULT16_32_Q15(wp1[0], x2) + Inlines.MULT16_32_Q15(wp2[0], x1);
                    xp1 = xp1.Point(-1);
                    wp1 = wp1.Point(1);
                    wp2 = wp2.Point(-1);
                }
            }
        }
    }
}
