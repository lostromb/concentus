/*Copyright (c) 2003-2004, Mark Borgerding
  Lots of modifications by Jean-Marc Valin
  Copyright (c) 2005-2007, Xiph.Org Foundation
  Copyright (c) 2008,      Xiph.Org Foundation, CSIRO

  All rights reserved.

  Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright notice,
       this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright notice,
       this list of conditions and the following disclaimer in the
       documentation and/or other materials provided with the distribution.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
  AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
  IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
  ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
  LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
  CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
  SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
  INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
  CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.*/

/* This code is originally from Mark Borgerding's KISS-FFT but has been
   heavily modified to better suit Opus */

using static System.MathF;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Celt.Arch;
using System.Runtime.CompilerServices;

namespace HellaUnsafe.Celt
{
    internal static unsafe class KissFFT
    {
        /* e.g. an fft of length 128 has 4 factors
        as far as kissfft is concerned
         4*4*4*2
         */
        internal const int MAXFACTORS = 8;

        internal unsafe struct kiss_fft_state
        {
            internal int nfft;
            internal float scale;
            internal int shift;
            internal fixed short factors[2 * MAXFACTORS];
            internal short* bitrev;
            internal kiss_twiddle_cpx* twiddles;

            public unsafe kiss_fft_state(int nfft, float scale, int shift, short[] factors, short* bitrev, kiss_twiddle_cpx* twiddles)
            {
                this.nfft = nfft;
                this.scale = scale;
                this.shift = shift;
                fixed (short* dest = this.factors)
                fixed (short* src = factors)
                {
                    Unsafe.CopyBlock(dest, src, (uint)(2 * MAXFACTORS * sizeof(short)));
                }

                this.bitrev = bitrev;
                this.twiddles = twiddles;
            }
        }

        internal struct kiss_fft_cpx
        {
            internal float r;
            internal float i;

            public override string ToString()
            {
                return $"r: {r} i: {i}";
            }
        }

        internal struct kiss_twiddle_cpx
        {
            internal float r;
            internal float i;

            public override string ToString()
            {
                return $"r: {r} i: {i}";
            }
        }

        internal static float S_MUL(float a, float b) { return a * b; }

        private static void C_MUL(ref kiss_fft_cpx m, kiss_fft_cpx a, kiss_fft_cpx b)
        {
            m.r = a.r * b.r - a.i * b.i;
            m.i = a.r * b.i + a.i * b.r;
        }
        private static void C_MUL(ref kiss_fft_cpx m, kiss_fft_cpx a, kiss_twiddle_cpx b)
        {
            m.r = a.r * b.r - a.i * b.i;
            m.i = a.r * b.i + a.i * b.r;
        }

        private static void C_MULC(ref kiss_fft_cpx m, kiss_fft_cpx a, kiss_fft_cpx b)
        {
            m.r = a.r * b.r + a.i * b.i;
            m.i = a.i * b.r - a.r * b.i;
        }

        private static void C_MULBYSCALAR(ref kiss_fft_cpx c, float s)
        {
            c.r *= s;
            c.i *= s;
        }

        private static void C_ADD(ref kiss_fft_cpx res, kiss_fft_cpx a, kiss_fft_cpx b)
        {
            res.r = a.r + b.r; res.i = a.i + b.i;
        }

        private static void C_SUB(ref kiss_fft_cpx res, kiss_fft_cpx a, kiss_fft_cpx b)
        {
            res.r = a.r - b.r; res.i = a.i - b.i;
        }

        private static void C_ADDTO(ref kiss_fft_cpx res, kiss_fft_cpx a)
        {
            res.r += a.r; res.i += a.i;
        }

        private static void C_SUBFROM(ref kiss_fft_cpx res, kiss_fft_cpx a)
        {
            res.r -= a.r; res.i -= a.i;
        }

        private static float KISS_FFT_COS(float phase)
        {
            return Cos(phase);
        }

        private static float KISS_FFT_SIN(float phase)
        {
            return Sin(phase);
        }

        private static float HALF_OF(float x) { return x * 0.5f; }

        private static void kf_cexp(ref kiss_fft_cpx x, float phase)
        {
            x.r = KISS_FFT_COS(phase);
            x.i = KISS_FFT_SIN(phase);
        }

        internal static unsafe void kf_bfly2(
                kiss_fft_cpx* Fout,
                int m,
                int N
            )
        {
            kiss_fft_cpx* Fout2;
            int i;
            float tw;
            tw = QCONST16(0.7071067812f, 15);
            /* We know that m==4 here because the radix-2 is just after a radix-4 */
            ASSERT(m == 4);
            for (i = 0; i < N; i++)
            {
                kiss_fft_cpx t;
                Fout2 = Fout + 4;
                t = Fout2[0];
                C_SUB(ref Fout2[0], Fout[0], t);
                C_ADDTO(ref Fout[0], t);

                t.r = S_MUL(ADD32_ovflw(Fout2[1].r, Fout2[1].i), tw);
                t.i = S_MUL(SUB32_ovflw(Fout2[1].i, Fout2[1].r), tw);
                C_SUB(ref Fout2[1], Fout[1], t);
                C_ADDTO(ref Fout[1], t);

                t.r = Fout2[2].i;
                t.i = -Fout2[2].r;
                C_SUB(ref Fout2[2], Fout[2], t);
                C_ADDTO(ref Fout[2], t);

                t.r = S_MUL(SUB32_ovflw(Fout2[3].i, Fout2[3].r), tw);
                t.i = S_MUL(NEG32_ovflw(ADD32_ovflw(Fout2[3].i, Fout2[3].r)), tw);
                C_SUB(ref Fout2[3], Fout[3], t);
                C_ADDTO(ref Fout[3], t);
                Fout += 8;
            }
        }

        internal static unsafe void kf_bfly4(
                    kiss_fft_cpx* Fout,
                     in int fstride,
                     in kiss_fft_state* st,
                     int m,
                     int N,
                     int mm
                    )
        {
            int i;

            if (m == 1)
            {
                /* Degenerate case where all the twiddles are 1. */
                for (i = 0; i < N; i++)
                {
                    kiss_fft_cpx scratch0 = default;
                    kiss_fft_cpx scratch1 = default;

                    C_SUB(ref scratch0, *Fout, Fout[2]);
                    C_ADDTO(ref *Fout, Fout[2]);
                    C_ADD(ref scratch1, Fout[1], Fout[3]);
                    C_SUB(ref Fout[2], *Fout, scratch1);
                    C_ADDTO(ref *Fout, scratch1);
                    C_SUB(ref scratch1, Fout[1], Fout[3]);

                    Fout[1].r = ADD32_ovflw(scratch0.r, scratch1.i);
                    Fout[1].i = SUB32_ovflw(scratch0.i, scratch1.r);
                    Fout[3].r = SUB32_ovflw(scratch0.r, scratch1.i);
                    Fout[3].i = ADD32_ovflw(scratch0.i, scratch1.r);
                    Fout += 4;
                }
            }
            else
            {
                int j;
                kiss_fft_cpx* scratch = SpanToPointerDangerous(stackalloc kiss_fft_cpx[6]);
                kiss_twiddle_cpx* tw1, tw2, tw3;
                int m2 = 2 * m;
                int m3 = 3 * m;
                kiss_fft_cpx* Fout_beg = Fout;
                for (i = 0; i < N; i++)
                {
                    Fout = Fout_beg + i * mm;
                    tw3 = tw2 = tw1 = st->twiddles;
                    /* m is guaranteed to be a multiple of 4. */
                    for (j = 0; j < m; j++)
                    {
                        C_MUL(ref scratch[0], Fout[m], *tw1);
                        C_MUL(ref scratch[1], Fout[m2], *tw2);
                        C_MUL(ref scratch[2], Fout[m3], *tw3);

                        C_SUB(ref scratch[5], *Fout, scratch[1]);
                        C_ADDTO(ref *Fout, scratch[1]);
                        C_ADD(ref scratch[3], scratch[0], scratch[2]);
                        C_SUB(ref scratch[4], scratch[0], scratch[2]);
                        C_SUB(ref Fout[m2], *Fout, scratch[3]);
                        tw1 += fstride;
                        tw2 += fstride * 2;
                        tw3 += fstride * 3;
                        C_ADDTO(ref *Fout, scratch[3]);

                        Fout[m].r = ADD32_ovflw(scratch[5].r, scratch[4].i);
                        Fout[m].i = SUB32_ovflw(scratch[5].i, scratch[4].r);
                        Fout[m3].r = SUB32_ovflw(scratch[5].r, scratch[4].i);
                        Fout[m3].i = ADD32_ovflw(scratch[5].i, scratch[4].r);
                        ++Fout;
                    }
                }
            }
        }


        internal static unsafe void kf_bfly3(
            kiss_fft_cpx* Fout,
            in int fstride,
            in kiss_fft_state* st,
            int m,
            int N,
            int mm
        )
        {
            int i;
            int k;
            int m2 = 2 * m;
            kiss_twiddle_cpx* tw1, tw2;
            kiss_fft_cpx* scratch = SpanToPointerDangerous(stackalloc kiss_fft_cpx[5]);
            kiss_twiddle_cpx epi3;

            kiss_fft_cpx* Fout_beg = Fout;
            epi3 = st->twiddles[fstride * m];
            for (i = 0; i < N; i++)
            {
                Fout = Fout_beg + i * mm;
                tw1 = tw2 = st->twiddles;
                /* For non-custom modes, m is guaranteed to be a multiple of 4. */
                k = m;
                do
                {

                    C_MUL(ref scratch[1], Fout[m], *tw1);
                    C_MUL(ref scratch[2], Fout[m2], *tw2);

                    C_ADD(ref scratch[3], scratch[1], scratch[2]);
                    C_SUB(ref scratch[0], scratch[1], scratch[2]);
                    tw1 += fstride;
                    tw2 += fstride * 2;

                    Fout[m].r = SUB32_ovflw(Fout->r, HALF_OF(scratch[3].r));
                    Fout[m].i = SUB32_ovflw(Fout->i, HALF_OF(scratch[3].i));

                    C_MULBYSCALAR(ref scratch[0], epi3.i);

                    C_ADDTO(ref *Fout, scratch[3]);

                    Fout[m2].r = ADD32_ovflw(Fout[m].r, scratch[0].i);
                    Fout[m2].i = SUB32_ovflw(Fout[m].i, scratch[0].r);

                    Fout[m].r = SUB32_ovflw(Fout[m].r, scratch[0].i);
                    Fout[m].i = ADD32_ovflw(Fout[m].i, scratch[0].r);

                    ++Fout;
                } while (--k != 0);
            }
        }

        internal static unsafe void kf_bfly5(
                             kiss_fft_cpx* Fout,
                     in int fstride,
                     in kiss_fft_state* st,
                     int m,
                     int N,
                     int mm
                    )
        {
            kiss_fft_cpx* Fout0, Fout1, Fout2, Fout3, Fout4;
            int i, u;
            kiss_fft_cpx* scratch = SpanToPointerDangerous(stackalloc kiss_fft_cpx[13]);
            kiss_twiddle_cpx* tw;
            kiss_twiddle_cpx ya, yb;
            kiss_fft_cpx* Fout_beg = Fout;

            ya = st->twiddles[fstride * m];
            yb = st->twiddles[fstride * 2 * m];
            tw = st->twiddles;

            for (i = 0; i < N; i++)
            {
                Fout = Fout_beg + i * mm;
                Fout0 = Fout;
                Fout1 = Fout0 + m;
                Fout2 = Fout0 + 2 * m;
                Fout3 = Fout0 + 3 * m;
                Fout4 = Fout0 + 4 * m;

                /* For non-custom modes, m is guaranteed to be a multiple of 4. */
                for (u = 0; u < m; ++u)
                {
                    scratch[0] = *Fout0;

                    C_MUL(ref scratch[1], *Fout1, tw[u * fstride]);
                    C_MUL(ref scratch[2], *Fout2, tw[2 * u * fstride]);
                    C_MUL(ref scratch[3], *Fout3, tw[3 * u * fstride]);
                    C_MUL(ref scratch[4], *Fout4, tw[4 * u * fstride]);

                    C_ADD(ref scratch[7], scratch[1], scratch[4]);
                    C_SUB(ref scratch[10], scratch[1], scratch[4]);
                    C_ADD(ref scratch[8], scratch[2], scratch[3]);
                    C_SUB(ref scratch[9], scratch[2], scratch[3]);

                    Fout0->r = ADD32_ovflw(Fout0->r, ADD32_ovflw(scratch[7].r, scratch[8].r));
                    Fout0->i = ADD32_ovflw(Fout0->i, ADD32_ovflw(scratch[7].i, scratch[8].i));

                    scratch[5].r = ADD32_ovflw(scratch[0].r, ADD32_ovflw(S_MUL(scratch[7].r, ya.r), S_MUL(scratch[8].r, yb.r)));
                    scratch[5].i = ADD32_ovflw(scratch[0].i, ADD32_ovflw(S_MUL(scratch[7].i, ya.r), S_MUL(scratch[8].i, yb.r)));

                    scratch[6].r = ADD32_ovflw(S_MUL(scratch[10].i, ya.i), S_MUL(scratch[9].i, yb.i));
                    scratch[6].i = NEG32_ovflw(ADD32_ovflw(S_MUL(scratch[10].r, ya.i), S_MUL(scratch[9].r, yb.i)));

                    C_SUB(ref *Fout1, scratch[5], scratch[6]);
                    C_ADD(ref *Fout4, scratch[5], scratch[6]);

                    scratch[11].r = ADD32_ovflw(scratch[0].r, ADD32_ovflw(S_MUL(scratch[7].r, yb.r), S_MUL(scratch[8].r, ya.r)));
                    scratch[11].i = ADD32_ovflw(scratch[0].i, ADD32_ovflw(S_MUL(scratch[7].i, yb.r), S_MUL(scratch[8].i, ya.r)));
                    scratch[12].r = SUB32_ovflw(S_MUL(scratch[9].i, ya.i), S_MUL(scratch[10].i, yb.i));
                    scratch[12].i = SUB32_ovflw(S_MUL(scratch[10].r, yb.i), S_MUL(scratch[9].r, ya.i));

                    C_ADD(ref *Fout2, scratch[11], scratch[12]);
                    C_SUB(ref *Fout3, scratch[11], scratch[12]);

                    ++Fout0; ++Fout1; ++Fout2; ++Fout3; ++Fout4;
                }
            }
        }

        internal static unsafe void opus_fft_impl(in kiss_fft_state* st, kiss_fft_cpx* fout)
        {
            int m2, m;
            int p;
            int L;
            int* fstride = SpanToPointerDangerous(stackalloc int[MAXFACTORS]);
            int i;
            int shift;

            /* st->shift can be -1 */
            shift = st->shift > 0 ? st->shift : 0;

            fstride[0] = 1;
            L = 0;
            do
            {
                p = st->factors[2 * L];
                m = st->factors[2 * L + 1];
                fstride[L + 1] = fstride[L] * p;
                L++;
            } while (m != 1);
            m = st->factors[2 * L - 1];
            for (i = L - 1; i >= 0; i--)
            {
                if (i != 0)
                    m2 = st->factors[2 * i - 1];
                else
                    m2 = 1;
                switch (st->factors[2 * i])
                {
                    case 2:
                        kf_bfly2(fout, m, fstride[i]);
                        break;
                    case 4:
                        kf_bfly4(fout, fstride[i] << shift, st, m, fstride[i], m2);
                        break;
                    case 3:
                        kf_bfly3(fout, fstride[i] << shift, st, m, fstride[i], m2);
                        break;
                    case 5:
                        kf_bfly5(fout, fstride[i] << shift, st, m, fstride[i], m2);
                        break;
                }
                m = m2;
            }
        }

        internal static unsafe void opus_fft_c(in kiss_fft_state* st, in kiss_fft_cpx* fin, kiss_fft_cpx* fout)
        {
            int i;
            float scale = st->scale;

            ASSERT(fin != fout, "In-place FFT not supported");
            /* Bit-reverse the input */
            for (i = 0; i < st->nfft; i++)
            {
                kiss_fft_cpx x = fin[i];
                fout[st->bitrev[i]].r = MULT16_32_Q16(scale, x.r);
                fout[st->bitrev[i]].i = MULT16_32_Q16(scale, x.i);
            }
            opus_fft_impl(st, fout);
        }


        internal static unsafe void opus_ifft_c(in kiss_fft_state* st, in kiss_fft_cpx* fin, kiss_fft_cpx* fout)
        {
            int i;
            ASSERT(fin != fout, "In-place FFT not supported");
            /* Bit-reverse the input */
            for (i = 0; i < st->nfft; i++)
                fout[st->bitrev[i]] = fin[i];
            for (i = 0; i < st->nfft; i++)
                fout[i].i = -fout[i].i;
            opus_fft_impl(st, fout);
            for (i = 0; i < st->nfft; i++)
                fout[i].i = -fout[i].i;
        }
    }
}
