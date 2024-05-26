﻿/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2008 Xiph.Org Foundation
   Written by Jean-Marc Valin */
/*
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using HellaUnsafe.Common;
using static HellaUnsafe.Celt.KissFFT;
using static HellaUnsafe.Celt.Arch;

namespace HellaUnsafe.Celt
{
    /* This is a simple MDCT implementation that uses a N/4 complex FFT
       to do most of the work. It should be relatively straightforward to
       plug in pretty much and FFT here.

       This replaces the Vorbis FFT (and uses the exact same API), which
       was a bit too messy and that was ending up duplicating code
       (might as well use the same FFT everywhere).

       The algorithm is similar to (and inspired from) Fabrice Bellard's
       MDCT implementation in FFMPEG, but has differences in signs, ordering
       and scaling in many places.
    */
    internal static class MDCT
    {
        internal unsafe struct mdct_lookup
        {
            internal int n;
            internal int maxshift;
            internal StructRef<kiss_fft_state>[] kfft;
            internal float[] trig;
        }

        internal static unsafe void clt_mdct_forward_c(
            in mdct_lookup l, float* input, float* output,
        in float* window, int overlap, int shift, int stride, int arch)
        {
            int i;
            int N, N2, N4;
            Span<float> f_span;
            Span<kiss_fft_cpx> f2_span;
            ref kiss_fft_state st = ref l.kfft[shift].Value;
            float* trig;
            float scale;
            scale = st.scale;

            N = l.n;
            fixed (float* trig_fixed = l.trig)
            {
                trig = trig_fixed;
                for (i = 0; i < shift; i++)
                {
                    N >>= 1;
                    trig += N;
                }
                N2 = N >> 1;
                N4 = N >> 2;

                f_span = new float[N2];
                f2_span = new kiss_fft_cpx[N4];

                fixed (float* f = f_span)
                fixed (kiss_fft_cpx* f2 = f2_span)
                {
                    /* Consider the input to be composed of four blocks: [a, b, c, d] */
                    /* Window, shuffle, fold */
                    {
                        /* Temp pointers to make it really clear to the compiler what we're doing */
                        float* xp1 = input + (overlap >> 1);
                        float* xp2 = input + N2 - 1 + (overlap >> 1);
                        float* yp = f;
                        float* wp1 = window + (overlap >> 1);
                        float* wp2 = window + (overlap >> 1) - 1;
                        for (i = 0; i < ((overlap + 3) >> 2); i++)
                        {
                            /* Real part arranged as -d-cR, Imag part arranged as -b+aR*/
                            *yp++ = MULT16_32_Q15(*wp2, xp1[N2]) + MULT16_32_Q15(*wp1, *xp2);
                            *yp++ = MULT16_32_Q15(*wp1, *xp1) - MULT16_32_Q15(*wp2, xp2[-N2]);
                            xp1 += 2;
                            xp2 -= 2;
                            wp1 += 2;
                            wp2 -= 2;
                        }
                        wp1 = window;
                        wp2 = window + overlap - 1;
                        for (; i < N4 - ((overlap + 3) >> 2); i++)
                        {
                            /* Real part arranged as a-bR, Imag part arranged as -c-dR */
                            *yp++ = *xp2;
                            *yp++ = *xp1;
                            xp1 += 2;
                            xp2 -= 2;
                        }
                        for (; i < N4; i++)
                        {
                            /* Real part arranged as a-bR, Imag part arranged as -c-dR */
                            *yp++ = -MULT16_32_Q15(*wp1, xp1[-N2]) + MULT16_32_Q15(*wp2, *xp2);
                            *yp++ = MULT16_32_Q15(*wp2, *xp1) + MULT16_32_Q15(*wp1, xp2[N2]);
                            xp1 += 2;
                            xp2 -= 2;
                            wp1 += 2;
                            wp2 -= 2;
                        }
                    }
                    /* Pre-rotation */
                    {
                        float* yp = f;
                        float* t = &trig[0];
                        for (i = 0; i < N4; i++)
                        {
                            kiss_fft_cpx yc;
                            float t0, t1;
                            float re, im, yr, yi;
                            t0 = t[i];
                            t1 = t[N4 + i];
                            re = *yp++;
                            im = *yp++;
                            yr = S_MUL(re, t0) - S_MUL(im, t1);
                            yi = S_MUL(im, t0) + S_MUL(re, t1);
                            yc.r = yr;
                            yc.i = yi;
                            yc.r = MULT16_32_Q16(scale, yc.r);
                            yc.i = MULT16_32_Q16(scale, yc.i);
                            f2[st.bitrev[i]] = yc;
                        }
                    }

                    /* N/4 complex FFT, does not downscale anymore */
                    opus_fft_impl(st, f2);

                    /* Post-rotate */
                    {
                        /* Temp pointers to make it really clear to the compiler what we're doing */
                        kiss_fft_cpx* fp = f2;
                        float* yp1 = output;
                        float* yp2 = output + stride * (N2 - 1);
                        float* t = &trig[0];
                        /* Temp pointers to make it really clear to the compiler what we're doing */
                        for (i = 0; i < N4; i++)
                        {
                            float yr, yi;
                            yr = S_MUL(fp->i, t[N4 + i]) - S_MUL(fp->r, t[i]);
                            yi = S_MUL(fp->r, t[N4 + i]) + S_MUL(fp->i, t[i]);
                            *yp1 = yr;
                            *yp2 = yi;
                            fp++;
                            yp1 += 2 * stride;
                            yp2 -= 2 * stride;
                        }
                    }
                }
            }
        }

        internal static unsafe void clt_mdct_backward_c(
            in mdct_lookup l, float* input, float* output,
            in float* window, int overlap, int shift, int stride, int arch)
        {
            int i;
            int N, N2, N4;
            float* trig;

            fixed (float* trig_fixed = l.trig)
            fixed (short* bitrev_fixed = l.kfft[shift].Value.bitrev)
            {
                N = l.n;
                trig = trig_fixed;
                for (i = 0; i < shift; i++)
                {
                    N >>= 1;
                    trig += N;
                }
                N2 = N >> 1;
                N4 = N >> 2;

                /* Pre-rotate */
                {
                    /* Temp pointers to make it really clear to the compiler what we're doing */
                    float* xp1 = input;
                    float* xp2 = input + stride * (N2 - 1);
                    float* yp = output + (overlap >> 1);
                    float* t = &trig[0];
                    short* bitrev = bitrev_fixed;
                    for (i = 0; i < N4; i++)
                    {
                        int rev;
                        float yr, yi;
                        rev = *bitrev++;
                        yr = ADD32_ovflw(S_MUL(*xp2, t[i]), S_MUL(*xp1, t[N4 + i]));
                        yi = SUB32_ovflw(S_MUL(*xp1, t[i]), S_MUL(*xp2, t[N4 + i]));
                        /* We swap real and imag because we use an FFT instead of an IFFT. */
                        yp[2 * rev + 1] = yr;
                        yp[2 * rev] = yi;
                        /* Storing the pre-rotation directly in the bitrev order. */
                        xp1 += 2 * stride;
                        xp2 -= 2 * stride;
                    }
                }

                opus_fft_impl(l.kfft[shift].Value, (kiss_fft_cpx*)(output + (overlap >> 1)));

                /* Post-rotate and de-shuffle from both ends of the buffer at once to make
                   it in-place. */
                {
                    float* yp0 = output + (overlap >> 1);
                    float* yp1 = output + (overlap >> 1) + N2 - 2;
                    float* t = &trig[0];
                    /* Loop to (N4+1)>>1 to handle odd N4. When N4 is odd, the
                       middle pair will be computed twice. */
                    for (i = 0; i < (N4 + 1) >> 1; i++)
                    {
                        float re, im, yr, yi;
                        float t0, t1;
                        /* We swap real and imag because we're using an FFT instead of an IFFT. */
                        re = yp0[1];
                        im = yp0[0];
                        t0 = t[i];
                        t1 = t[N4 + i];
                        /* We'd scale up by 2 here, but instead it's done when mixing the windows */
                        yr = ADD32_ovflw(S_MUL(re, t0), S_MUL(im, t1));
                        yi = SUB32_ovflw(S_MUL(re, t1), S_MUL(im, t0));
                        /* We swap real and imag because we're using an FFT instead of an IFFT. */
                        re = yp1[1];
                        im = yp1[0];
                        yp0[0] = yr;
                        yp1[1] = yi;

                        t0 = t[(N4 - i - 1)];
                        t1 = t[(N2 - i - 1)];
                        /* We'd scale up by 2 here, but instead it's done when mixing the windows */
                        yr = ADD32_ovflw(S_MUL(re, t0), S_MUL(im, t1));
                        yi = SUB32_ovflw(S_MUL(re, t1), S_MUL(im, t0));
                        yp1[0] = yr;
                        yp0[1] = yi;
                        yp0 += 2;
                        yp1 -= 2;
                    }
                }

                /* Mirror on both sides for TDAC */
                {
                    float* xp1 = output + overlap - 1;
                    float* yp1 = output;
                    float* wp1 = window;
                    float* wp2 = window + overlap - 1;

                    for (i = 0; i < overlap / 2; i++)
                    {
                        float x1, x2;
                        x1 = *xp1;
                        x2 = *yp1;
                        *yp1++ = SUB32_ovflw(MULT16_32_Q15(*wp2, x2), MULT16_32_Q15(*wp1, x1));
                        *xp1-- = ADD32_ovflw(MULT16_32_Q15(*wp1, x2), MULT16_32_Q15(*wp2, x1));
                        wp1++;
                        wp2--;
                    }
                }
            }
        }
    }
}
