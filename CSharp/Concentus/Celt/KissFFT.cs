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
    public static class KissFFT
    {
        private const bool TRACE_FILE = false;
        
        // #define kiss_fft_scalar opus_int32
        // #define kiss_twiddle_scalar opus_int16
        public const int SAMP_MAX = 2147483647;
        public const int SAMP_MIN = 0 - SAMP_MAX;
        public const int TWID_MAX = 32767;
        public const int TRIG_UPSCALE = 1;

        public const int MAXFACTORS = 8;

        /*
          Explanation of macros dealing with complex math:

           C_MUL(m,a,b)         : m = a*b
           C_FIXDIV( c , div )  : if a fixed point impl., c /= div. noop otherwise
           C_SUB( res, a,b)     : res = a - b
           C_SUBFROM( res , a)  : res -= a
           C_ADDTO( res , a)    : res += a
         * */

        public static int S_MUL(short a, short b)
        {
            return Inlines.MULT16_32_Q15(b, a);
        }

        public static int S_MUL(int a, short b)
        {
            return Inlines.MULT16_32_Q15(b, a);
        }

        public static int S_MUL(int a, int b)
        {
            return Inlines.MULT16_32_Q15(b, a);
        }

        public static void C_MUL(kiss_fft_cpx m, kiss_fft_cpx a, kiss_twiddle_cpx b)
        {
            (m).r = Inlines.SUB32(S_MUL((a).r, (b).r), S_MUL((a).i, (b).i));
            (m).i = Inlines.ADD32(S_MUL((a).r, (b).i), S_MUL((a).i, (b).r));
        }

        public static void C_MULC(kiss_fft_cpx m, kiss_fft_cpx a, kiss_fft_cpx b)
        {
            (m).r = Inlines.ADD32(S_MUL((a).r, (b).r), S_MUL((a).i, (b).i));
            (m).i = Inlines.SUB32(S_MUL((a).i, (b).r), S_MUL((a).r, (b).i));
        }

        public static void C_MULC(kiss_twiddle_cpx m, kiss_twiddle_cpx a, kiss_twiddle_cpx b)
        {
            (m).r = Inlines.CHOP16(Inlines.ADD16(S_MUL((a).r, (b).r), S_MUL((a).i, (b).i)));
            (m).i = Inlines.CHOP16(Inlines.SUB16(S_MUL((a).i, (b).r), S_MUL((a).r, (b).i)));
        }

        public static void C_MUL4(kiss_fft_cpx m, kiss_fft_cpx a, kiss_twiddle_cpx b)
        {
            C_MUL(m, a, b);
        }

        //public static void DIVSCALAR(int x, int k)
        //{
        //    x = S_MUL(x, (TWID_MAX - ((k) >> 1)) / (k) + 1);
        //}

        //public static void C_FIXDIV(kiss_fft_cpx c, int div)
        //{
        //    DIVSCALAR((c).r, div);
        //    DIVSCALAR((c).i, div);
        //}

        public static void C_MULBYSCALAR(kiss_fft_cpx c, int s)
        {
            (c).r = S_MUL((c).r, s);
            (c).i = S_MUL((c).i, s);
        }

        public static void C_MULBYSCALAR(kiss_twiddle_cpx c, int s)
        {
            (c).r = Inlines.CHOP16(S_MUL((c).r, s));
            (c).i = Inlines.CHOP16(S_MUL((c).i, s));
        }

        [Obsolete]
        public static void CHECK_OVERFLOW_OP(int a, int op, int b)
        {
        }

        // complex add
        public static void C_ADD(kiss_fft_cpx res, kiss_fft_cpx a, kiss_fft_cpx b)
        {
            res.r = a.r + b.r;
            res.i = a.i + b.i;
        }

        // complex subtract
        public static void C_SUB(kiss_fft_cpx res, kiss_fft_cpx a, kiss_fft_cpx b)
        {
            res.r = a.r - b.r;
            res.i = a.i - b.i;
        }

        // complex add
        public static void C_ADDTO(kiss_fft_cpx res, kiss_fft_cpx a)
        {
            res.r += a.r;
            res.i += a.i;
        }

        // complex add
        public static void C_SUBFROM(kiss_fft_cpx res, kiss_fft_cpx a)
        {
            res.r -= a.r;
            res.i -= a.i;
        }

        public static int HALF_OF(int x)
        {
            return x >> 1;
        }

        public static short HALF_OF(short x)
        {
            return (short)(x >> 1);
        }

        public static void kf_bfly2(Pointer<kiss_fft_cpx> Fout, int m, int N)
        {
            Pointer<kiss_fft_cpx> Fout2;
            int i;
            {
                short tw;
                tw = Inlines.QCONST16(0.7071067812f, 15);
                /* We know that m==4 here because the radix-2 is just after a radix-4 */
                Inlines.OpusAssert(m == 4);
                for (i = 0; i < N; i++)
                {
                    kiss_fft_cpx t = new kiss_fft_cpx();
                    Fout2 = Fout.Point(4);
                    t.Assign(Fout2[0]);
                    if (TRACE_FILE) Debug.WriteLine("14a1 0x{0:x} 0x{1:x}", (uint)t.r, (uint)t.i);
                    C_SUB(Fout2[0], Fout[0], t);
                    C_ADDTO(Fout[0], t);

                    t.r = S_MUL(Fout2[1].r + Fout2[1].i, tw);
                    t.i = S_MUL(Fout2[1].i - Fout2[1].r, tw);
                    if (TRACE_FILE) Debug.WriteLine("14a2 0x{0:x} 0x{1:x}", (uint)t.r, (uint)t.i);
                    C_SUB(Fout2[1], Fout[1], t);
                    C_ADDTO(Fout[1], t);

                    t.r = Fout2[2].i;
                    t.i = -Fout2[2].r;
                    if (TRACE_FILE) Debug.WriteLine("14a3 0x{0:x} 0x{1:x}", (uint)t.r, (uint)t.i);
                    C_SUB(Fout2[2], Fout[2], t);
                    C_ADDTO(Fout[2], t);

                    t.r = S_MUL(Fout2[3].i - Fout2[3].r, tw);
                    t.i = S_MUL(-Fout2[3].i - Fout2[3].r, tw);
                    if (TRACE_FILE) Debug.WriteLine("14a4 0x{0:x} 0x{1:x}", (uint)t.r, (uint)t.i);
                    C_SUB(Fout2[3], Fout[3], t);
                    C_ADDTO(Fout[3], t);
                    Fout = Fout.Point(8);
                }
            }
        }

        public static void kf_bfly4(
                     Pointer<kiss_fft_cpx> Fout,
                     int fstride,
                     kiss_fft_state st,
                     int m,
                     int N,
                     int mm)
        {
            int i;

            if (m == 1)
            {
                /* Degenerate case where all the twiddles are 1. */
                for (i = 0; i < N; i++)
                {
                    kiss_fft_cpx scratch0 = new kiss_fft_cpx();
                    kiss_fft_cpx scratch1 = new kiss_fft_cpx();

                    C_SUB(scratch0, Fout[0], Fout[2]);
                    C_ADDTO(Fout[0], Fout[2]);
                    C_ADD(scratch1, Fout[1], Fout[3]);
                    C_SUB(Fout[2], Fout[0], scratch1);
                    C_ADDTO(Fout[0], scratch1);
                    C_SUB(scratch1, Fout[1], Fout[3]);

                    Fout[1].r = scratch0.r + scratch1.i;
                    Fout[1].i = scratch0.i - scratch1.r;
                    Fout[3].r = scratch0.r - scratch1.i;
                    Fout[3].i = scratch0.i + scratch1.r;
                    if (TRACE_FILE) Debug.WriteLine("14b1 0x{0:x} 0x{1:x}", (uint)Fout[1].r, (uint)Fout[1].i);
                    if (TRACE_FILE) Debug.WriteLine("14b2 0x{0:x} 0x{1:x}", (uint)Fout[3].r, (uint)Fout[3].i);
                    Fout = Fout.Point(4);
                }
            }
            else
            {
                int j;
                kiss_fft_cpx[] scratch = new kiss_fft_cpx[6];
                for (int c = 0; c < 6; c++)
                {
                    scratch[c] = new kiss_fft_cpx();
                }

                Pointer<kiss_twiddle_cpx> tw1, tw2, tw3;
                int m2 = 2 * m;
                int m3 = 3 * m;
                Pointer<kiss_fft_cpx> Fout_beg = Fout;
                for (i = 0; i < N; i++)
                {
                    Fout = Fout_beg.Point(i * mm);
                    tw3 = tw2 = tw1 = st.twiddles;
                    /* m is guaranteed to be a multiple of 4. */
                    for (j = 0; j < m; j++)
                    {
                        C_MUL(scratch[0], Fout[m], tw1[0]);
                        C_MUL(scratch[1], Fout[m2], tw2[0]);
                        C_MUL(scratch[2], Fout[m3], tw3[0]);

                        C_SUB(scratch[5], Fout[0], scratch[1]);
                        C_ADDTO(Fout[0], scratch[1]);
                        C_ADD(scratch[3], scratch[0], scratch[2]);
                        C_SUB(scratch[4], scratch[0], scratch[2]);
                        C_SUB(Fout[m2], Fout[0], scratch[3]);
                        tw1 = tw1.Point(fstride);
                        tw2 = tw2.Point(fstride * 2);
                        tw3 = tw3.Point(fstride * 3);
                        C_ADDTO(Fout[0], scratch[3]);
                        if (TRACE_FILE) Debug.WriteLine("14c 0x{0:x} 0x{1:x}", (uint)scratch[0].r, (uint)scratch[0].i);
                        Fout[m].r = scratch[5].r + scratch[4].i;
                        Fout[m].i = scratch[5].i - scratch[4].r;
                        Fout[m3].r = scratch[5].r - scratch[4].i;
                        Fout[m3].i = scratch[5].i + scratch[4].r;
                        Fout = Fout.Point(1);
                    }
                }
            }
        }

        public static void kf_bfly3(
                     Pointer<kiss_fft_cpx> Fout,
                     int fstride,
                     kiss_fft_state st,
                     int m,
                     int N,
                     int mm
                    )
        {
            int i;
            int k;
            int m2 = 2 * m;
            Pointer<kiss_twiddle_cpx> tw1, tw2;
            kiss_fft_cpx[] scratch = new kiss_fft_cpx[5]; //opus bug  fixme: #5 never used
            for (int c = 0; c < 5; c++)
            {
                scratch[c] = new kiss_fft_cpx();
            }

            // fixme: potential unnecessary allocation?
            kiss_twiddle_cpx epi3 = new kiss_twiddle_cpx();
            
            Pointer<kiss_fft_cpx> Fout_beg = Fout;
            epi3.r = -16384; // opus bug fixme: never used
            epi3.i = -28378;

            for (i = 0; i < N; i++)
            {
                Fout = Fout_beg.Point(i * mm);
                tw1 = tw2 = st.twiddles;
                /* For non-custom modes, m is guaranteed to be a multiple of 4. */
                k = m;
                do
                {

                    C_MUL(scratch[1], Fout[m], tw1[0]);
                    C_MUL(scratch[2], Fout[m2], tw2[0]);

                    C_ADD(scratch[3], scratch[1], scratch[2]);
                    C_SUB(scratch[0], scratch[1], scratch[2]);
                    tw1 = tw1.Point(fstride);
                    tw2 = tw2.Point(fstride * 2);

                    Fout[m].r = Fout[0].r - HALF_OF(scratch[3].r);
                    Fout[m].i = Fout[0].i - HALF_OF(scratch[3].i);

                    C_MULBYSCALAR(scratch[0], epi3.i);

                    C_ADDTO(Fout[0], scratch[3]);

                    Fout[m2].r = Fout[m].r + scratch[0].i;
                    Fout[m2].i = Fout[m].i - scratch[0].r;

                    Fout[m].r -= scratch[0].i;
                    Fout[m].i += scratch[0].r;
                    if (TRACE_FILE) Debug.WriteLine("14d 0x{0:x} 0x{1:x}", (uint)scratch[0].r, (uint)scratch[0].i);

                    Fout = Fout.Point(1);
                } while ((--k) != 0);
            }
        }

        public static void kf_bfly5(
                     Pointer<kiss_fft_cpx> Fout,
                     int fstride,
                     kiss_fft_state st,
                     int m,
                     int N,
                     int mm
                    )
        {
            Pointer<kiss_fft_cpx> Fout0, Fout1, Fout2, Fout3, Fout4;
            int i, u;
            kiss_fft_cpx[] scratch = new kiss_fft_cpx[13];
            for (int c = 0; c < 13; c++)
            {
                scratch[c] = new kiss_fft_cpx();
            }

            Pointer<kiss_twiddle_cpx> tw;
            // fixme: slow performance on these temp variables (I enforced copy-on-assign to ensure parity)
            kiss_twiddle_cpx ya = new kiss_twiddle_cpx();
            kiss_twiddle_cpx yb = new kiss_twiddle_cpx();
            Pointer<kiss_fft_cpx> Fout_beg = Fout;

            ya.r = 10126;
            ya.i = -31164;
            yb.r = -26510;
            yb.i = -19261;
            tw = st.twiddles;

            for (i = 0; i < N; i++)
            {
                Fout = Fout_beg.Point(i * mm);
                Fout0 = Fout;
                Fout1 = Fout0.Point(m);
                Fout2 = Fout0.Point(2 * m);
                Fout3 = Fout0.Point(3 * m);
                Fout4 = Fout0.Point(4 * m);

                /* For non-custom modes, m is guaranteed to be a multiple of 4. */
                for (u = 0; u < m; ++u)
                {
                    scratch[0].Assign(Fout0[0]);

                    C_MUL(scratch[1], Fout1[0], tw[u * fstride]);
                    C_MUL(scratch[2], Fout2[0], tw[2 * u * fstride]);
                    C_MUL(scratch[3], Fout3[0], tw[3 * u * fstride]);
                    C_MUL(scratch[4], Fout4[0], tw[4 * u * fstride]);

                    C_ADD(scratch[7], scratch[1], scratch[4]);
                    C_SUB(scratch[10], scratch[1], scratch[4]);
                    C_ADD(scratch[8], scratch[2], scratch[3]);
                    C_SUB(scratch[9], scratch[2], scratch[3]);

                    Fout0[0].r += scratch[7].r + scratch[8].r;
                    Fout0[0].i += scratch[7].i + scratch[8].i;

                    scratch[5].r = scratch[0].r + S_MUL(scratch[7].r, ya.r) + S_MUL(scratch[8].r, yb.r);
                    scratch[5].i = scratch[0].i + S_MUL(scratch[7].i, ya.r) + S_MUL(scratch[8].i, yb.r);

                    scratch[6].r = S_MUL(scratch[10].i, ya.i) + S_MUL(scratch[9].i, yb.i);
                    scratch[6].i = 0 - S_MUL(scratch[10].r, ya.i) - S_MUL(scratch[9].r, yb.i);

                    C_SUB(Fout1[0], scratch[5], scratch[6]);
                    C_ADD(Fout4[0], scratch[5], scratch[6]);

                    scratch[11].r = scratch[0].r + S_MUL(scratch[7].r, yb.r) + S_MUL(scratch[8].r, ya.r);
                    scratch[11].i = scratch[0].i + S_MUL(scratch[7].i, yb.r) + S_MUL(scratch[8].i, ya.r);
                    scratch[12].r = 0 - S_MUL(scratch[10].i, yb.i) + S_MUL(scratch[9].i, ya.i);
                    scratch[12].i = S_MUL(scratch[10].r, yb.i) - S_MUL(scratch[9].r, ya.i);

                    C_ADD(Fout2[0], scratch[11], scratch[12]);
                    C_SUB(Fout3[0], scratch[11], scratch[12]);

                    Fout0 = Fout0.Point(1);
                    Fout1 = Fout1.Point(1);
                    Fout2 = Fout2.Point(1);
                    Fout3 = Fout3.Point(1);
                    Fout4 = Fout4.Point(1);
                    if (TRACE_FILE) Debug.WriteLine("14e 0x{0:x} 0x{1:x}", (uint)scratch[0].r, (uint)scratch[0].i);
                }
            }
        }

        public static void opus_fft_impl(kiss_fft_state st, Pointer<kiss_fft_cpx> fout)
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

        public static void opus_fft_c(kiss_fft_state st, Pointer<kiss_fft_cpx> fin, Pointer<kiss_fft_cpx> fout)
        {
            int i;
            /* Allows us to scale with MULT16_32_Q16() */
            int scale_shift = st.scale_shift - 1;
            short scale = st.scale;

            Inlines.OpusAssert(fin != fout, "In-place FFT not supported");

            /* Bit-reverse the input */
            for (i = 0; i < st.nfft; i++)
            {
                kiss_fft_cpx x = fin[i];
                fout[st.bitrev[i]].r = Inlines.SHR32(Inlines.MULT16_32_Q16(scale, x.r), scale_shift);
                fout[st.bitrev[i]].i = Inlines.SHR32(Inlines.MULT16_32_Q16(scale, x.i), scale_shift);
            }

            opus_fft_impl(st, fout);
        }


        public static void opus_ifft_c(kiss_fft_state st, Pointer<kiss_fft_cpx> fin, Pointer<kiss_fft_cpx> fout)
        {
            int i;
            Inlines.OpusAssert(fin != fout, "In-place iFFT not supported");

            /* Bit-reverse the input */
            for (i = 0; i < st.nfft; i++)
            {
                fout[st.bitrev[i]] = fin[i];
            }

            for (i = 0; i < st.nfft; i++)
            {
                fout[i].i = -fout[i].i;
            }

            opus_fft_impl(st, fout);

            for (i = 0; i < st.nfft; i++)
                fout[i].i = -fout[i].i;
        }
    }
}
