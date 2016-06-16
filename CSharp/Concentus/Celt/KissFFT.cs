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
        public const int SAMP_MAX = 2147483647;
        public const int SAMP_MIN = 0 - SAMP_MAX;
        public const int TWID_MAX = 32767;
        public const int TRIG_UPSCALE = 1;

        public const int MAXFACTORS = 8;
        
        public static int S_MUL(int a, int b)
        {
            return Inlines.MULT16_32_Q15(b, a);
        }

        public static int S_MUL(int a, short b)
        {
            return Inlines.MULT16_32_Q15(b, a);
        }

        public static int HALF_OF(int x)
        {
            return x >> 1;
        }

        public static void kf_bfly2(Pointer<int> Fout, int m, int N)
        {
            Pointer<int> Fout2;
            int i;
            {
                short tw;
                tw = Inlines.QCONST16(0.7071067812f, 15);
                /* We know that m==4 here because the radix-2 is just after a radix-4 */
                Inlines.OpusAssert(m == 4);
                for (i = 0; i < N; i++)
                {
                    int t_r, t_i;
                    Fout2 = Fout.Point(8);
                    t_r = Fout2[0];
                    t_i = Fout2[1];
                    Fout2[0] = Fout[0] - t_r;
                    Fout2[1] = Fout[1] - t_i;
                    Fout[0] += t_r;
                    Fout[1] += t_i;

                    t_r = S_MUL(Fout2[2] + Fout2[3], tw);
                    t_i = S_MUL(Fout2[3] - Fout2[2], tw);
                    Fout2[2] = Fout[2] - t_r;
                    Fout2[3] = Fout[3] - t_i;
                    Fout[2] += t_r;
                    Fout[3] += t_i;

                    t_r = Fout2[5];
                    t_i = -Fout2[4];
                    Fout2[4] = Fout[4] - t_r;
                    Fout2[5] = Fout[5] - t_i;
                    Fout[4] += t_r;
                    Fout[5] += t_i;

                    t_r = S_MUL(Fout2[7] - Fout2[6], tw);
                    t_i = S_MUL(-Fout2[7] - Fout2[6], tw);
                    Fout2[6] = Fout[6] - t_r;
                    Fout2[7] = Fout[7] - t_i;
                    Fout[6] += t_r;
                    Fout[7] += t_i;

                    Fout = Fout.Point(16);
                }
            }
        }

        public static void kf_bfly4(
                     Pointer<int> Fout,
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
                    int[] scratch = new int[4];
                    scratch[0] = Fout[0] - Fout[4];
                    scratch[1] = Fout[1] - Fout[5];
                    Fout[0] += Fout[4];
                    Fout[1] += Fout[5];
                    scratch[2] = Fout[2] + Fout[6];
                    scratch[3] = Fout[3] + Fout[7];
                    Fout[4] = Fout[0] - scratch[2];
                    Fout[5] = Fout[1] - scratch[3];
                    Fout[0] += scratch[2];
                    Fout[1] += scratch[3];
                    scratch[2] = Fout[2] - Fout[6];
                    scratch[3] = Fout[3] - Fout[7];
                    Fout[2] = scratch[0] + scratch[3];
                    Fout[3] = scratch[1] - scratch[2];
                    Fout[6] = scratch[0] - scratch[3];
                    Fout[7] = scratch[1] + scratch[2];
                    Fout = Fout.Point(8);
                }
            }
            else
            {
                int j;
                int[] scratch = new int[12];
                Pointer<kiss_twiddle_cpx> tw1, tw2, tw3;
                int m2 = 2 * m;
                int m3 = 3 * m;
                Pointer<int> Fout_beg = Fout;
                for (i = 0; i < N; i++)
                {
                    Fout = Fout_beg.Point(2 * i * mm);
                    tw3 = tw2 = tw1 = st.twiddles;
                    /* m is guaranteed to be a multiple of 4. */
                    for (j = 0; j < m; j++)
                    {
                        scratch[0] = Inlines.SUB32(S_MUL(Fout[2 * m],  tw1[0].r), S_MUL(Fout[2 * m + 1], tw1[0].i));
                        scratch[1] = Inlines.ADD32(S_MUL(Fout[2 * m],  tw1[0].i), S_MUL(Fout[2 * m + 1], tw1[0].r));
                        scratch[2] = Inlines.SUB32(S_MUL(Fout[2 * m2], tw2[0].r), S_MUL(Fout[2 * m2 + 1], tw2[0].i));
                        scratch[3] = Inlines.ADD32(S_MUL(Fout[2 * m2], tw2[0].i), S_MUL(Fout[2 * m2 + 1], tw2[0].r));
                        scratch[4] = Inlines.SUB32(S_MUL(Fout[2 * m3], tw3[0].r), S_MUL(Fout[2 * m3 + 1], tw3[0].i));
                        scratch[5] = Inlines.ADD32(S_MUL(Fout[2 * m3], tw3[0].i), S_MUL(Fout[2 * m3 + 1], tw3[0].r));
                        
                        scratch[10] = Fout[0] - scratch[2];
                        scratch[11] = Fout[1] - scratch[3];
                        Fout[0] += scratch[2];
                        Fout[1] += scratch[3];
                        scratch[6] = scratch[0] + scratch[4];
                        scratch[7] = scratch[1] + scratch[5];
                        scratch[8] = scratch[0] - scratch[4];
                        scratch[9] = scratch[1] - scratch[5];
                        Fout[2 * m2] = Fout[0] - scratch[6];
                        Fout[2 * m2 + 1] = Fout[1] - scratch[7];
                        tw1 = tw1.Point(fstride);
                        tw2 = tw2.Point(fstride * 2);
                        tw3 = tw3.Point(fstride * 3);
                        Fout[0] += scratch[6];
                        Fout[1] += scratch[7];
                        Fout[2 * m] = scratch[10] + scratch[9];
                        Fout[2 * m + 1] = scratch[11] - scratch[8];
                        Fout[2 * m3] = scratch[10] - scratch[9];
                        Fout[2 * m3 + 1] = scratch[11] + scratch[8];
                        Fout = Fout.Point(2);
                    }
                }
            }
        }

        public static void kf_bfly3(
                     Pointer<int> Fout,
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
            int[] scratch = new int[8]; //opus bug  fixme: #5 never used

            // fixme: potential unnecessary allocation?
            kiss_twiddle_cpx epi3 = new kiss_twiddle_cpx();
            
            Pointer<int> Fout_beg = Fout;
            epi3.r = -16384; // opus bug fixme: never used
            epi3.i = -28378;

            for (i = 0; i < N; i++)
            {
                Fout = Fout_beg.Point(2 * i * mm);
                tw1 = tw2 = st.twiddles;
                /* For non-custom modes, m is guaranteed to be a multiple of 4. */
                k = m;
                do
                {
                    scratch[2] = Inlines.SUB32(S_MUL(Fout[2 * m],  tw1[0].r), S_MUL(Fout[2 * m + 1], tw1[0].i));
                    scratch[3] = Inlines.ADD32(S_MUL(Fout[2 * m],  tw1[0].i), S_MUL(Fout[2 * m + 1], tw1[0].r));
                    scratch[4] = Inlines.SUB32(S_MUL(Fout[2 * m2], tw2[0].r), S_MUL(Fout[2 * m2 + 1], tw2[0].i));
                    scratch[5] = Inlines.ADD32(S_MUL(Fout[2 * m2], tw2[0].i), S_MUL(Fout[2 * m2 + 1], tw2[0].r));

                    scratch[6] = scratch[2] + scratch[4];
                    scratch[7] = scratch[3] + scratch[5];
                    scratch[0] = scratch[2] - scratch[4];
                    scratch[1] = scratch[3] - scratch[5];
                    tw1 = tw1.Point(fstride);
                    tw2 = tw2.Point(fstride * 2);

                    Fout[2 * m] = Fout[0] - HALF_OF(scratch[6]);
                    Fout[2 * m + 1] = Fout[1] - HALF_OF(scratch[7]);

                    scratch[0] = S_MUL(scratch[0], epi3.i);
                    scratch[1] = S_MUL(scratch[1], epi3.i);

                    Fout[0] += scratch[6];
                    Fout[1] += scratch[7];

                    Fout[2 * m2] = Fout[2 * m] + scratch[1];
                    Fout[2 * m2 + 1] = Fout[2 * m + 1] - scratch[0];

                    Fout[2 * m] -= scratch[1];
                    Fout[2 * m + 1] += scratch[0];

                    Fout = Fout.Point(2);
                } while ((--k) != 0);
            }
        }

        public static void kf_bfly5(
                     Pointer<int> Fout,
                     int fstride,
                     kiss_fft_state st,
                     int m,
                     int N,
                     int mm
                    )
        {
            Pointer<int> Fout0, Fout1, Fout2, Fout3, Fout4;
            int i, u;
            int[] scratch = new int[26];

            Pointer<kiss_twiddle_cpx> tw;
            // fixme: slow performance on these temp variables (I enforced copy-on-assign to ensure parity)
            kiss_twiddle_cpx ya = new kiss_twiddle_cpx();
            kiss_twiddle_cpx yb = new kiss_twiddle_cpx();
            Pointer<int> Fout_beg = Fout;

            ya.r = 10126;
            ya.i = -31164;
            yb.r = -26510;
            yb.i = -19261;
            tw = st.twiddles;

            for (i = 0; i < N; i++)
            {
                Fout = Fout_beg.Point(2 * i * mm);
                Fout0 = Fout;
                Fout1 = Fout0.Point(2 * m);
                Fout2 = Fout0.Point(4 * m);
                Fout3 = Fout0.Point(6 * m);
                Fout4 = Fout0.Point(8 * m);

                /* For non-custom modes, m is guaranteed to be a multiple of 4. */
                for (u = 0; u < m; ++u)
                {
                    scratch[0] = Fout0[0];
                    scratch[1] = Fout0[1];

                    scratch[2] = Inlines.SUB32(S_MUL(Fout1[0], tw[u * fstride].r),     S_MUL(Fout1[1], tw[u * fstride].i));
                    scratch[3] = Inlines.ADD32(S_MUL(Fout1[0], tw[u * fstride].i),     S_MUL(Fout1[1], tw[u * fstride].r));
                    scratch[4] = Inlines.SUB32(S_MUL(Fout2[0], tw[2 * u * fstride].r), S_MUL(Fout2[1], tw[2 * u * fstride].i));
                    scratch[5] = Inlines.ADD32(S_MUL(Fout2[0], tw[2 * u * fstride].i), S_MUL(Fout2[1], tw[2 * u * fstride].r));
                    scratch[6] = Inlines.SUB32(S_MUL(Fout3[0], tw[3 * u * fstride].r), S_MUL(Fout3[1], tw[3 * u * fstride].i));
                    scratch[7] = Inlines.ADD32(S_MUL(Fout3[0], tw[3 * u * fstride].i), S_MUL(Fout3[1], tw[3 * u * fstride].r));
                    scratch[8] = Inlines.SUB32(S_MUL(Fout4[0], tw[4 * u * fstride].r), S_MUL(Fout4[1], tw[4 * u * fstride].i));
                    scratch[9] = Inlines.ADD32(S_MUL(Fout4[0], tw[4 * u * fstride].i), S_MUL(Fout4[1], tw[4 * u * fstride].r));

                    scratch[14] = scratch[2] + scratch[8];
                    scratch[15] = scratch[3] + scratch[9];
                    scratch[20] = scratch[2] - scratch[8];
                    scratch[21] = scratch[3] - scratch[9];
                    scratch[16] = scratch[4] + scratch[6];
                    scratch[17] = scratch[5] + scratch[7];
                    scratch[18] = scratch[4] - scratch[6];
                    scratch[19] = scratch[5] - scratch[7];

                    Fout0[0] += scratch[14] + scratch[16];
                    Fout0[1] += scratch[15] + scratch[17];

                    scratch[10] = scratch[0] + S_MUL(scratch[14], ya.r) + S_MUL(scratch[16], yb.r);
                    scratch[11] = scratch[1] + S_MUL(scratch[15], ya.r) + S_MUL(scratch[17], yb.r);

                    scratch[12] = S_MUL(scratch[21], ya.i) + S_MUL(scratch[19], yb.i);
                    scratch[13] = 0 - S_MUL(scratch[20], ya.i) - S_MUL(scratch[18], yb.i);

                    Fout1[0] = scratch[10] - scratch[12];
                    Fout1[1] = scratch[11] - scratch[13];
                    Fout4[0] = scratch[10] + scratch[12];
                    Fout4[1] = scratch[11] + scratch[13];

                    scratch[22] = scratch[0] + S_MUL(scratch[14], yb.r) + S_MUL(scratch[16], ya.r);
                    scratch[23] = scratch[1] + S_MUL(scratch[15], yb.r) + S_MUL(scratch[17], ya.r);
                    scratch[24] = 0 - S_MUL(scratch[21], yb.i) + S_MUL(scratch[19], ya.i);
                    scratch[25] = S_MUL(scratch[20], yb.i) - S_MUL(scratch[18], ya.i);

                    Fout2[0] = scratch[22] + scratch[24];
                    Fout2[1] = scratch[23] + scratch[25];
                    Fout3[0] = scratch[22] - scratch[24];
                    Fout3[1] = scratch[23] - scratch[25];

                    Fout0 = Fout0.Point(2);
                    Fout1 = Fout1.Point(2);
                    Fout2 = Fout2.Point(2);
                    Fout3 = Fout3.Point(2);
                    Fout4 = Fout4.Point(2);
                }
            }
        }

        public static void opus_fft_impl(kiss_fft_state st, Pointer<int> fout)
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

        public static void opus_fft_c(kiss_fft_state st, Pointer<int> fin, Pointer<int> fout)
        {
            int i;
            /* Allows us to scale with MULT16_32_Q16() */
            int scale_shift = st.scale_shift - 1;
            short scale = st.scale;

            Inlines.OpusAssert(fin != fout, "In-place FFT not supported");

            /* Bit-reverse the input */
            for (i = 0; i < st.nfft; i++)
            {
                fout[2 * st.bitrev[i]] = Inlines.SHR32(Inlines.MULT16_32_Q16(scale, fin[2 * i]), scale_shift);
                fout[2 * st.bitrev[i] + 1] = Inlines.SHR32(Inlines.MULT16_32_Q16(scale, fin[2 * i + 1]), scale_shift);
            }

            opus_fft_impl(st, fout);
        }


        public static void opus_ifft_c(kiss_fft_state st, Pointer<int> fin, Pointer<int> fout)
        {
            int i;
            Inlines.OpusAssert(fin != fout, "In-place iFFT not supported");

            /* Bit-reverse the input */
            for (i = 0; i < st.nfft * 2; i++)
            {
                fout[st.bitrev[i]] = fin[i];
            }

            for (i = 1; i < st.nfft * 2; i += 2)
            {
                fout[i] = -fout[i];
            }

            opus_fft_impl(st, fout);

            for (i = 1; i < st.nfft * 2; i += 2)
                fout[i] = -fout[i];
        }
    }
}
