﻿/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2011 Xiph.Org Foundation
   Copyright (c) 2003-2004, Mark Borgerding
   Modified from KISS-FFT by Jean-Marc Valin
   Ported to C# by Logan Stromberg

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

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

/* This code is originally from Mark Borgerding's KISS-FFT but has been
   heavily modified to better suit Opus */

#if !UNSAFE

namespace Concentus.Celt
{
    using Concentus.Celt.Enums;
    using Concentus.Celt.Structs;
    using Concentus.Common;
    using Concentus.Common.CPlusPlus;
    using System;
    using System.Diagnostics;
    using System.Threading;
    internal static class KissFFT
    {
        //internal const int SAMP_MAX = 2147483647;
        //internal const int SAMP_MIN = 0 - SAMP_MAX;
        //internal const int TWID_MAX = 32767;
        //internal const int TRIG_UPSCALE = 1;

        internal const int MAXFACTORS = 8;
        
        internal static int S_MUL(int a, int b)
        {
            return Inlines.MULT16_32_Q15(b, a);
        }

        internal static int S_MUL(int a, short b)
        {
            return Inlines.MULT16_32_Q15(b, a);
        }

        internal static int HALF_OF(int x)
        {
            return x >> 1;
        }

        internal static void kf_bfly2(Span<int> Fout, int fout_ptr, int m, int N)
        {
            int Fout2;
            int i;
            {
                short tw;
                tw = ((short)(0.5 + (0.7071067812f) * (((int)1) << (15))))/*Inlines.QCONST16(0.7071067812f, 15)*/;
                /* We know that m==4 here because the radix-2 is just after a radix-4 */
                Inlines.OpusAssert(m == 4);
                for (i = 0; i < N; i++)
                {
                    int t_r, t_i;
                    Fout2 = fout_ptr + 8;
                    t_r = Fout[Fout2 + 0];
                    t_i = Fout[Fout2 + 1];
                    Fout[Fout2 + 0] = Fout[fout_ptr + 0] - t_r;
                    Fout[Fout2 + 1] = Fout[fout_ptr + 1] - t_i;
                    Fout[fout_ptr + 0] += t_r;
                    Fout[fout_ptr + 1] += t_i;

                    t_r = S_MUL(Fout[Fout2 + 2] + Fout[Fout2 + 3], tw);
                    t_i = S_MUL(Fout[Fout2 + 3] - Fout[Fout2 + 2], tw);
                    Fout[Fout2 + 2] = Fout[fout_ptr + 2] - t_r;
                    Fout[Fout2 + 3] = Fout[fout_ptr + 3] - t_i;
                    Fout[fout_ptr + 2] += t_r;
                    Fout[fout_ptr + 3] += t_i;

                    t_r = Fout[Fout2 + 5];
                    t_i = 0 - Fout[Fout2 + 4];
                    Fout[Fout2 + 4] = Fout[fout_ptr + 4] - t_r;
                    Fout[Fout2 + 5] = Fout[fout_ptr + 5] - t_i;
                    Fout[fout_ptr + 4] += t_r;
                    Fout[fout_ptr + 5] += t_i;

                    t_r = S_MUL(Fout[Fout2 + 7] - Fout[Fout2 + 6], tw);
                    t_i = S_MUL(0 - Fout[Fout2 + 7] - Fout[Fout2 + 6], tw);
                    Fout[Fout2 + 6] = Fout[fout_ptr + 6] - t_r;
                    Fout[Fout2 + 7] = Fout[fout_ptr + 7] - t_i;
                    Fout[fout_ptr + 6] += t_r;
                    Fout[fout_ptr + 7] += t_i;

                    fout_ptr += 16;
                }
            }
        }

        internal static void kf_bfly4(
                     Span<int> Fout,
                     int fout_ptr,
                     int fstride,
                     FFTState st,
                     int m,
                     int N,
                     int mm)
        {
            int i;

            if (m == 1)
            {
                /* Degenerate case where all the twiddles are 1. */
                int scratch0, scratch1, scratch2, scratch3;
                for (i = 0; i < N; i++)
                {
                    scratch0 = Fout[fout_ptr + 0] - Fout[fout_ptr + 4];
                    scratch1 = Fout[fout_ptr + 1] - Fout[fout_ptr + 5];
                    Fout[fout_ptr + 0] += Fout[fout_ptr + 4];
                    Fout[fout_ptr + 1] += Fout[fout_ptr + 5];
                    scratch2 = Fout[fout_ptr + 2] + Fout[fout_ptr + 6];
                    scratch3 = Fout[fout_ptr + 3] + Fout[fout_ptr + 7];
                    Fout[fout_ptr + 4] = Fout[fout_ptr + 0] - scratch2;
                    Fout[fout_ptr + 5] = Fout[fout_ptr + 1] - scratch3;
                    Fout[fout_ptr + 0] += scratch2;
                    Fout[fout_ptr + 1] += scratch3;
                    scratch2 = Fout[fout_ptr + 2] - Fout[fout_ptr + 6];
                    scratch3 = Fout[fout_ptr + 3] - Fout[fout_ptr + 7];
                    Fout[fout_ptr + 2] = scratch0 + scratch3;
                    Fout[fout_ptr + 3] = scratch1 - scratch2;
                    Fout[fout_ptr + 6] = scratch0 - scratch3;
                    Fout[fout_ptr + 7] = scratch1 + scratch2;
                    fout_ptr += 8;
                }
            }
            else
            {
                int j;
                int scratch0, scratch1, scratch2, scratch3, scratch4, scratch5, scratch6, scratch7, scratch8, scratch9, scratch10, scratch11;
                int tw1, tw2, tw3;
                int Fout_beg = fout_ptr;
                for (i = 0; i < N; i++)
                {
                    fout_ptr = Fout_beg + 2 * i * mm;
                    int m1 = fout_ptr + (2 * m);
                    int m2 = fout_ptr + (4 * m);
                    int m3 = fout_ptr + (6 * m);
                    tw3 = tw2 = tw1 = 0;
                    /* m is guaranteed to be a multiple of 4. */
                    for (j = 0; j < m; j++)
                    {
                        scratch0 = (S_MUL(Fout[m1], st.twiddles[tw1    ]) - S_MUL(Fout[m1 + 1], st.twiddles[tw1 + 1]));
                        scratch1 = (S_MUL(Fout[m1], st.twiddles[tw1 + 1]) + S_MUL(Fout[m1 + 1], st.twiddles[tw1]));
                        scratch2 = (S_MUL(Fout[m2], st.twiddles[tw2    ]) - S_MUL(Fout[m2 + 1], st.twiddles[tw2 + 1]));
                        scratch3 = (S_MUL(Fout[m2], st.twiddles[tw2 + 1]) + S_MUL(Fout[m2 + 1], st.twiddles[tw2]));
                        scratch4 = (S_MUL(Fout[m3], st.twiddles[tw3    ]) - S_MUL(Fout[m3 + 1], st.twiddles[tw3 + 1]));
                        scratch5 = (S_MUL(Fout[m3], st.twiddles[tw3 + 1]) + S_MUL(Fout[m3 + 1], st.twiddles[tw3]));
                        scratch10 = Fout[fout_ptr] - scratch2;
                        scratch11 = Fout[fout_ptr + 1] - scratch3;
                        Fout[fout_ptr] += scratch2;
                        Fout[fout_ptr + 1] += scratch3;
                        scratch6 = scratch0 + scratch4;
                        scratch7 = scratch1 + scratch5;
                        scratch8 = scratch0 - scratch4;
                        scratch9 = scratch1 - scratch5;
                        Fout[m2] = Fout[fout_ptr] - scratch6;
                        Fout[m2 + 1] = Fout[fout_ptr + 1] - scratch7;
                        tw1 += fstride * 2;
                        tw2 += fstride * 4;
                        tw3 += fstride * 6;
                        Fout[fout_ptr] += scratch6;
                        Fout[fout_ptr + 1] += scratch7;
                        Fout[m1] = scratch10 + scratch9;
                        Fout[m1 + 1] = scratch11 - scratch8;
                        Fout[m3] = scratch10 - scratch9;
                        Fout[m3 + 1] = scratch11 + scratch8;
                        fout_ptr += 2;
                        m1 += 2;
                        m2 += 2;
                        m3 += 2;
                    }
                }
            }
        }

        internal static void kf_bfly3(
                     Span<int> Fout,
                     int fout_ptr,
                     int fstride,
                     FFTState st,
                     int m,
                     int N,
                     int mm
                    )
        {
            int i;
            int k;
            int m1 = 2 * m;
            int m2 = 4 * m;
            int tw1, tw2;
            int scratch0, scratch1, scratch2, scratch3, scratch4, scratch5, scratch6, scratch7;

            int Fout_beg = fout_ptr;

            for (i = 0; i < N; i++)
            {
                fout_ptr = Fout_beg + 2 * i * mm;
                tw1 = tw2 = 0;
                /* For non-custom modes, m is guaranteed to be a multiple of 4. */
                k = m;
                do
                {
                    scratch2 = (S_MUL(Fout[fout_ptr + m1], st.twiddles[tw1]) - S_MUL(Fout[fout_ptr + m1 + 1], st.twiddles[tw1 + 1]));
                    scratch3 = (S_MUL(Fout[fout_ptr + m1], st.twiddles[tw1 + 1]) + S_MUL(Fout[fout_ptr + m1 + 1], st.twiddles[tw1]));
                    scratch4 = (S_MUL(Fout[fout_ptr + m2], st.twiddles[tw2]) - S_MUL(Fout[fout_ptr + m2 + 1], st.twiddles[tw2 + 1]));
                    scratch5 = (S_MUL(Fout[fout_ptr + m2], st.twiddles[tw2 + 1]) + S_MUL(Fout[fout_ptr + m2 + 1], st.twiddles[tw2]));

                    scratch6 = scratch2 + scratch4;
                    scratch7 = scratch3 + scratch5;
                    scratch0 = scratch2 - scratch4;
                    scratch1 = scratch3 - scratch5;

                    tw1 += fstride * 2;
                    tw2 += fstride * 4;

                    Fout[fout_ptr + m1] = Fout[fout_ptr + 0] - HALF_OF(scratch6);
                    Fout[fout_ptr + m1 + 1] = Fout[fout_ptr + 1] - HALF_OF(scratch7);

                    scratch0 = S_MUL(scratch0, -28378);
                    scratch1 = S_MUL(scratch1, -28378);

                    Fout[fout_ptr + 0] += scratch6;
                    Fout[fout_ptr + 1] += scratch7;

                    Fout[fout_ptr + m2] = Fout[fout_ptr + m1] + scratch1;
                    Fout[fout_ptr + m2 + 1] = Fout[fout_ptr + m1 + 1] - scratch0;

                    Fout[fout_ptr + m1] -= scratch1;
                    Fout[fout_ptr + m1 + 1] += scratch0;

                    fout_ptr += 2;
                } while ((--k) != 0);
            }
        }

        internal static void kf_bfly5(
                     Span<int> Fout,
                     int fout_ptr,
                     int fstride,
                     FFTState st,
                     int m,
                     int N,
                     int mm
                    )
        {
            int Fout0, Fout1, Fout2, Fout3, Fout4;
            int i, u;
            int scratch0, scratch1, scratch2, scratch3, scratch4, scratch5,
                scratch6, scratch7, scratch8, scratch9, scratch10, scratch11,
                scratch12,scratch13, scratch14, scratch15, scratch16, scratch17,
                scratch18, scratch19, scratch20, scratch21, scratch22, scratch23,
                scratch24, scratch25;

            int Fout_beg = fout_ptr;

            short ya_r = 10126;
            short ya_i = -31164;
            short yb_r = -26510;
            short yb_i = -19261;
            int tw1, tw2, tw3, tw4;

            for (i = 0; i < N; i++)
            {
                tw1 = tw2 = tw3 = tw4 = 0;
                fout_ptr = Fout_beg + 2 * i * mm;
                Fout0 = fout_ptr;
                Fout1 = fout_ptr + (2 * m);
                Fout2 = fout_ptr + (4 * m);
                Fout3 = fout_ptr + (6 * m);
                Fout4 = fout_ptr + (8 * m);

                /* For non-custom modes, m is guaranteed to be a multiple of 4. */
                for (u = 0; u < m; ++u)
                {
                    scratch0 = Fout[Fout0 + 0];
                    scratch1 = Fout[Fout0 + 1];

                    scratch2 = (S_MUL(Fout[Fout1 + 0], st.twiddles[tw1]) -     S_MUL(Fout[Fout1 + 1], st.twiddles[tw1 + 1]));
                    scratch3 = (S_MUL(Fout[Fout1 + 0], st.twiddles[tw1 + 1]) + S_MUL(Fout[Fout1 + 1], st.twiddles[tw1]));
                    scratch4 = (S_MUL(Fout[Fout2 + 0], st.twiddles[tw2]) -     S_MUL(Fout[Fout2 + 1], st.twiddles[tw2 + 1]));
                    scratch5 = (S_MUL(Fout[Fout2 + 0], st.twiddles[tw2 + 1]) + S_MUL(Fout[Fout2 + 1], st.twiddles[tw2]));
                    scratch6 = (S_MUL(Fout[Fout3 + 0], st.twiddles[tw3]) -     S_MUL(Fout[Fout3 + 1], st.twiddles[tw3 + 1]));
                    scratch7 = (S_MUL(Fout[Fout3 + 0], st.twiddles[tw3 + 1]) + S_MUL(Fout[Fout3 + 1], st.twiddles[tw3]));
                    scratch8 = (S_MUL(Fout[Fout4 + 0], st.twiddles[tw4]) -     S_MUL(Fout[Fout4 + 1], st.twiddles[tw4 + 1]));
                    scratch9 = (S_MUL(Fout[Fout4 + 0], st.twiddles[tw4 + 1]) + S_MUL(Fout[Fout4 + 1], st.twiddles[tw4]));

                    tw1 += (2 * fstride);
                    tw2 += (4 * fstride);
                    tw3 += (6 * fstride);
                    tw4 += (8 * fstride);

                    scratch14 = scratch2 + scratch8;
                    scratch15 = scratch3 + scratch9;
                    scratch20 = scratch2 - scratch8;
                    scratch21 = scratch3 - scratch9;
                    scratch16 = scratch4 + scratch6;
                    scratch17 = scratch5 + scratch7;
                    scratch18 = scratch4 - scratch6;
                    scratch19 = scratch5 - scratch7;

                    Fout[Fout0 + 0] += scratch14 + scratch16;
                    Fout[Fout0 + 1] += scratch15 + scratch17;

                    scratch10 = scratch0 + S_MUL(scratch14, ya_r) + S_MUL(scratch16, yb_r);
                    scratch11 = scratch1 + S_MUL(scratch15, ya_r) + S_MUL(scratch17, yb_r);

                    scratch12 = S_MUL(scratch21, ya_i) + S_MUL(scratch19, yb_i);
                    scratch13 = 0 - S_MUL(scratch20, ya_i) - S_MUL(scratch18, yb_i);

                    Fout[Fout1 + 0] = scratch10 - scratch12;
                    Fout[Fout1 + 1] = scratch11 - scratch13;
                    Fout[Fout4 + 0] = scratch10 + scratch12;
                    Fout[Fout4 + 1] = scratch11 + scratch13;

                    scratch22 = scratch0 + S_MUL(scratch14, yb_r) + S_MUL(scratch16, ya_r);
                    scratch23 = scratch1 + S_MUL(scratch15, yb_r) + S_MUL(scratch17, ya_r);
                    scratch24 = 0 - S_MUL(scratch21, yb_i) + S_MUL(scratch19, ya_i);
                    scratch25 = S_MUL(scratch20, yb_i) - S_MUL(scratch18, ya_i);

                    Fout[Fout2 + 0] = scratch22 + scratch24;
                    Fout[Fout2 + 1] = scratch23 + scratch25;
                    Fout[Fout3 + 0] = scratch22 - scratch24;
                    Fout[Fout3 + 1] = scratch23 - scratch25;

                    Fout0 += 2;
                    Fout1 += 2;
                    Fout2 += 2;
                    Fout3 += 2;
                    Fout4 += 2;
                }
            }
        }

        internal static void opus_fft_impl(FFTState st, Span<int> fout, int fout_ptr)
        {
            int m2, m;
            int p;
            int L;
            int[] fstride = new int[MAXFACTORS];
            int i;
            int shift;

            /* st.shift can be -1 */
            shift = st.shift > 0 ? st.shift : 0;

            fstride[0] = 1;
            L = 0;
            do
            {
                p = st.factors[2 * L];
                m = st.factors[2 * L + 1];
                fstride[L + 1] = fstride[L] * p;
                L++;
            } while (m != 1);

            m = st.factors[2 * L - 1];
            for (i = L - 1; i >= 0; i--)
            {
                if (i != 0)
                    m2 = st.factors[2 * i - 1];
                else
                    m2 = 1;
                switch (st.factors[2 * i])
                {
                    case 2:
                        kf_bfly2(fout, fout_ptr, m, fstride[i]);
                        break;
                    case 4:
                        kf_bfly4(fout, fout_ptr, fstride[i] << shift, st, m, fstride[i], m2);
                        break;
                    case 3:
                        kf_bfly3(fout, fout_ptr, fstride[i] << shift, st, m, fstride[i], m2);
                        break;
                    case 5:
                        kf_bfly5(fout, fout_ptr, fstride[i] << shift, st, m, fstride[i], m2);
                        break;
                }
                m = m2;
            }
        }

        internal static void opus_fft(FFTState st, int[] fin, int[] fout)
        {
            int i;
            /* Allows us to scale with MULT16_32_Q16() */
            int scale_shift = st.scale_shift - 1;
            short scale = st.scale;

            Inlines.OpusAssert(fin != fout, "In-place FFT not supported");

            /* Bit-reverse the input */
            for (i = 0; i < st.nfft; i++)
            {
                fout[(2 * st.bitrev[i])] = Inlines.SHR32(Inlines.MULT16_32_Q16(scale, fin[(2 * i)]), scale_shift);
                fout[(2 * st.bitrev[i] + 1)] = Inlines.SHR32(Inlines.MULT16_32_Q16(scale, fin[(2 * i) + 1]), scale_shift);
            }

            opus_fft_impl(st, fout, 0);
        }


        //internal static void opus_ifft(FFTState st, Pointer<int> fin, Pointer<int> fout)
        //{
        //    int i;
        //    Inlines.OpusAssert(fin != fout, "In-place iFFT not supported");

        //    /* Bit-reverse the input */
        //    for (i = 0; i < st.nfft * 2; i++)
        //    {
        //        fout[st.bitrev[i]] = fin[i];
        //    }

        //    for (i = 1; i < st.nfft * 2; i += 2)
        //    {
        //        fout[i] = -fout[i];
        //    }

        //    opus_fft_impl(st, fout.Data, fout.Offset);

        //    for (i = 1; i < st.nfft * 2; i += 2)
        //        fout[i] = -fout[i];
        //}
    }
}

#endif