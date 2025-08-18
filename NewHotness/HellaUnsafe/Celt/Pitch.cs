using System;
using static System.Math;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.CeltLPC;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Celt
{
    internal static class Pitch
    {
        /*We make sure a C version is always available for cases where the overhead of
            vectorization and passing around an arch flag aren't worth it.*/
        internal static unsafe float celt_inner_prod(
            in float* x,
            in float* y,
            int N)
        {
            int i;
            float xy = 0;
            for (i = 0; i < N; i++)
                xy = MAC16_16(xy, x[i], y[i]);
            return xy;
        }

        internal static unsafe void dual_inner_prod(in float* x, in float* y01, in float* y02,
            int N, float* xy1, float* xy2)
        {
            int i;
            float xy01 = 0;
            float xy02 = 0;
            for (i = 0; i < N; i++)
            {
                xy01 = MAC16_16(xy01, x[i], y01[i]);
                xy02 = MAC16_16(xy02, x[i], y02[i]);
            }
            *xy1 = xy01;
            *xy2 = xy02;
        }

        /* OPT: This is the kernel you really want to optimize. It gets used a lot
            by the prefilter and by the PLC. */
        internal static unsafe void xcorr_kernel(float* x, float* y, float* sum/*[4]*/, int len)
        {
            int j;
            float y_0, y_1, y_2, y_3;
            ASSERT(len >= 3);
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

        internal static unsafe void find_best_pitch(float* xcorr, float* y, int len,
                            int max_pitch, int* best_pitch
                            )
        {
            int i, j;
            float Syy = 1;
            Span<float> best_num = stackalloc float[2];
            Span<float> best_den = stackalloc float[2];

            best_num[0] = -1;
            best_num[1] = -1;
            best_den[0] = 0;
            best_den[1] = 0;
            best_pitch[0] = 0;
            best_pitch[1] = 1;
            for (j = 0; j < len; j++)
                Syy = ADD32(Syy, SHR32(MULT16_16(y[j], y[j]), 0));
            for (i = 0; i < max_pitch; i++)
            {
                if (xcorr[i] > 0)
                {
                    float num;
                    float xcorr16;
                    xcorr16 = EXTRACT16(VSHR32(xcorr[i], 0));
                    /* Considering the range of xcorr16, this should avoid both underflows
                       and overflows (inf) when squaring xcorr16 */
                    xcorr16 *= 1e-12f;
                    num = MULT16_16_Q15(xcorr16, xcorr16);
                    if (MULT16_32_Q15(num, best_den[1]) > MULT16_32_Q15(best_num[1], Syy))
                    {
                        if (MULT16_32_Q15(num, best_den[0]) > MULT16_32_Q15(best_num[0], Syy))
                        {
                            best_num[1] = best_num[0];
                            best_den[1] = best_den[0];
                            best_pitch[1] = best_pitch[0];
                            best_num[0] = num;
                            best_den[0] = Syy;
                            best_pitch[0] = i;
                        }
                        else
                        {
                            best_num[1] = num;
                            best_den[1] = Syy;
                            best_pitch[1] = i;
                        }
                    }
                }
                Syy += SHR32(MULT16_16(y[i + len], y[i + len]), 0) - SHR32(MULT16_16(y[i], y[i]), 0);
                Syy = MAX32(1, Syy);
            }
        }

        internal static unsafe void celt_fir5(float* x,
         in float* num,
         int N)
        {
            int i;
            float num0, num1, num2, num3, num4;
            float mem0, mem1, mem2, mem3, mem4;
            num0 = num[0];
            num1 = num[1];
            num2 = num[2];
            num3 = num[3];
            num4 = num[4];
            mem0 = 0;
            mem1 = 0;
            mem2 = 0;
            mem3 = 0;
            mem4 = 0;
            for (i = 0; i < N; i++)
            {
                float sum = SHL32(EXTEND32(x[i]), 0);
                sum = MAC16_16(sum, num0, mem0);
                sum = MAC16_16(sum, num1, mem1);
                sum = MAC16_16(sum, num2, mem2);
                sum = MAC16_16(sum, num3, mem3);
                sum = MAC16_16(sum, num4, mem4);
                mem4 = mem3;
                mem3 = mem2;
                mem2 = mem1;
                mem1 = mem0;
                mem0 = x[i];
                x[i] = ROUND16(sum, 0);
            }
        }


        internal static unsafe void pitch_downsample(float** x, float* x_lp,
              int len, int C)
        {
            int i;
            float* ac = stackalloc float[5];
            float tmp = Q15ONE;
            float* lpc = stackalloc float[4];
            float* lpc2 = stackalloc float[5];
            float c1 = QCONST16(.8f, 15);

            for (i = 1; i < len >> 1; i++)
                x_lp[i] = .25f * x[0][(2 * i - 1)] + .25f * x[0][(2 * i + 1)] + .5f * x[0][2 * i];
            x_lp[0] = .25f * x[0][1] + .5f * x[0][0];
            if (C == 2)
            {
                for (i = 1; i < len >> 1; i++)
                    x_lp[i] += .25f * x[1][(2 * i - 1)] + .25f * x[1][(2 * i + 1)] + .5f * x[1][2 * i];
                x_lp[0] += .25f * x[1][1] + .5f * x[1][0];
            }
            _celt_autocorr(x_lp, ac, null, 0,
                           4, len >> 1);

            /* Noise floor -40 dB */
            ac[0] *= 1.0001f;
            /* Lag windowing */
            for (i = 1; i <= 4; i++)
            {
                /*ac[i] *= exp(-.5*(2*M_PI*.002*i)*(2*M_PI*.002*i));*/
                ac[i] -= ac[i] * (.008f * i) * (.008f * i);
            }

            _celt_lpc(lpc, ac, 4);
            for (i = 0; i < 4; i++)
            {
                tmp = MULT16_16_Q15(QCONST16(.9f, 15), tmp);
                lpc[i] = MULT16_16_Q15(lpc[i], tmp);
            }
            /* Add a zero */
            lpc2[0] = lpc[0] + QCONST16(.8f, 0);
            lpc2[1] = lpc[1] + MULT16_16_Q15(c1, lpc[0]);
            lpc2[2] = lpc[2] + MULT16_16_Q15(c1, lpc[1]);
            lpc2[3] = lpc[3] + MULT16_16_Q15(c1, lpc[2]);
            lpc2[4] = MULT16_16_Q15(c1, lpc[3]);
            celt_fir5(x_lp, lpc2, len >> 1);
        }

        /* Pure C implementation. */
        internal static unsafe void
        celt_pitch_xcorr(in float* _x, in float* _y,
              float* xcorr, int len, int max_pitch)
        {
#if FALSE
            /* This is a simple version of the pitch correlation that should work
                     well on DSPs like Blackfin and TI C5x/C6x */
            int i, j;
            for (i = 0; i < max_pitch; i++)
            {
                float sum = 0;
                for (j = 0; j < len; j++)
                    sum = MAC16_16(sum, _x[j], _y[i + j]);
                xcorr[i] = sum;
            }
#else
            /* Unrolled version of the pitch correlation -- runs faster on x86 and ARM */
            int i;
            /*The EDSP version requires that max_pitch is at least 1, and that _x is
               32-bit aligned.
              Since it's hard to put asserts in assembly, put them here.*/
            ASSERT(max_pitch > 0);
            ASSERT(((nint)_x & 3) == 0);
            float* sum = stackalloc float[4];

            for (i = 0; i < max_pitch - 3; i += 4)
            {
                new Span<float>(sum, 4).Clear();
                xcorr_kernel(_x, _y + i, sum, len);
                xcorr[i] = sum[0];
                xcorr[i + 1] = sum[1];
                xcorr[i + 2] = sum[2];
                xcorr[i + 3] = sum[3];
            }

            /* In case max_pitch isn't a multiple of 4, do non-unrolled version. */
            for (; i < max_pitch; i++)
            {
                xcorr[i] = celt_inner_prod(_x, _y + i, len);
            }
#endif
        }

        internal static unsafe void pitch_search(in float* x_lp, float* y,
                  int len, int max_pitch, int* pitch)
        {
            int i, j;
            int lag;
            int* best_pitch = stackalloc int[2];
            best_pitch[0] = 0;
            best_pitch[1] = 0;
            int offset;

            ASSERT(len > 0);
            ASSERT(max_pitch > 0);
            lag = len + max_pitch;
            float* x_lp4 = stackalloc float[len >> 2];
            float* y_lp4 = stackalloc float[lag >> 2];
            float* xcorr = stackalloc float[max_pitch >> 1];

            /* Downsample by 2 again */
            for (j = 0; j < len >> 2; j++)
                x_lp4[j] = x_lp[2 * j];
            for (j = 0; j < lag >> 2; j++)
                y_lp4[j] = y[2 * j];

            /* Coarse search with 4x decimation */

            celt_pitch_xcorr(x_lp4, y_lp4, xcorr, len >> 2, max_pitch >> 2);

            find_best_pitch(xcorr, y_lp4, len >> 2, max_pitch >> 2, best_pitch);

            /* Finer search with 2x decimation */
            for (i = 0; i < max_pitch >> 1; i++)
            {
                float sum;
                xcorr[i] = 0;
                if (Abs(i - 2 * best_pitch[0]) > 2 && Abs(i - 2 * best_pitch[1]) > 2)
                    continue;

                sum = celt_inner_prod(x_lp, y + i, len >> 1);
                xcorr[i] = MAX32(-1, sum);
            }
            find_best_pitch(xcorr, y, len >> 1, max_pitch >> 1, best_pitch);

            /* Refine by pseudo-interpolation */
            if (best_pitch[0] > 0 && best_pitch[0] < (max_pitch >> 1) - 1)
            {
                float a, b, c;
                a = xcorr[best_pitch[0] - 1];
                b = xcorr[best_pitch[0]];
                c = xcorr[best_pitch[0] + 1];
                if ((c - a) > MULT16_32_Q15(QCONST16(.7f, 15), b - a))
                    offset = 1;
                else if ((a - c) > MULT16_32_Q15(QCONST16(.7f, 15), b - c))
                    offset = -1;
                else
                    offset = 0;
            }
            else
            {
                offset = 0;
            }

            *pitch = 2 * best_pitch[0] - offset;
        }

        internal static float compute_pitch_gain(float xy, float xx, float yy)
        {
            return xy / celt_sqrt(1 + xx * yy);
        }

        private static readonly int[] second_check/*[16]*/ = { 0, 0, 3, 2, 3, 2, 5, 2, 3, 2, 3, 2, 5, 2, 3, 2 };

        internal static unsafe float remove_doubling(float* x, int maxperiod, int minperiod,
          int N, int* T0_, int prev_period, float prev_gain)
        {
            int k, i, T, T0;
            float g, g0;
            float pg;
            float xy, xx, yy, xy2;
            float* xcorr = stackalloc float[3];
            float best_xy, best_yy;
            int offset;
            int minperiod0;

            minperiod0 = minperiod;
            maxperiod /= 2;
            minperiod /= 2;
            *T0_ /= 2;
            prev_period /= 2;
            N /= 2;
            x += maxperiod;
            if (*T0_ >= maxperiod)
                *T0_ = maxperiod - 1;

            T = T0 = *T0_;
            float* yy_lookup = stackalloc float[maxperiod + 1];
            dual_inner_prod(x, x, x - T0, N, &xx, &xy);
            yy_lookup[0] = xx;
            yy = xx;
            for (i = 1; i <= maxperiod; i++)
            {
                yy = yy + MULT16_16(x[-i], x[-i]) - MULT16_16(x[N - i], x[N - i]);
                yy_lookup[i] = MAX32(0, yy);
            }
            yy = yy_lookup[T0];
            best_xy = xy;
            best_yy = yy;
            g = g0 = compute_pitch_gain(xy, xx, yy);
            /* Look for any pitch at T/k */
            for (k = 2; k <= 15; k++)
            {
                int T1, T1b;
                float g1;
                float cont = 0;
                float thresh;
                T1 = celt_udiv(2 * T0 + k, 2 * k);
                if (T1 < minperiod)
                    break;
                /* Look for another strong correlation at T1b */
                if (k == 2)
                {
                    if (T1 + T0 > maxperiod)
                        T1b = T0;
                    else
                        T1b = T0 + T1;
                }
                else
                {
                    T1b = celt_udiv(2 * second_check[k] * T0 + k, 2 * k);
                }
                dual_inner_prod(x, &x[-T1], &x[-T1b], N, &xy, &xy2);
                xy = HALF32(xy + xy2);
                yy = HALF32(yy_lookup[T1] + yy_lookup[T1b]);
                g1 = compute_pitch_gain(xy, xx, yy);
                if (Abs(T1 - prev_period) <= 1)
                    cont = prev_gain;
                else if (Abs(T1 - prev_period) <= 2 && 5 * k * k < T0)
                    cont = HALF16(prev_gain);
                else
                    cont = 0;
                thresh = MAX16(QCONST16(.3f, 15), MULT16_16_Q15(QCONST16(.7f, 15), g0) - cont);
                /* Bias against very high pitch (very short period) to avoid false-positives
                   due to short-term correlation */
                if (T1 < 3 * minperiod)
                    thresh = MAX16(QCONST16(.4f, 15), MULT16_16_Q15(QCONST16(.85f, 15), g0) - cont);
                else if (T1 < 2 * minperiod)
                    thresh = MAX16(QCONST16(.5f, 15), MULT16_16_Q15(QCONST16(.9f, 15), g0) - cont);
                if (g1 > thresh)
                {
                    best_xy = xy;
                    best_yy = yy;
                    T = T1;
                    g = g1;
                }
            }
            best_xy = MAX32(0, best_xy);
            if (best_yy <= best_xy)
                pg = Q15ONE;
            else
                pg = SHR32(frac_div32(best_xy, best_yy + 1), 16);

            for (k = 0; k < 3; k++)
                xcorr[k] = celt_inner_prod(x, x - (T + k - 1), N);
            if ((xcorr[2] - xcorr[0]) > MULT16_32_Q15(QCONST16(.7f, 15), xcorr[1] - xcorr[0]))
                offset = 1;
            else if ((xcorr[0] - xcorr[2]) > MULT16_32_Q15(QCONST16(.7f, 15), xcorr[1] - xcorr[2]))
                offset = -1;
            else
                offset = 0;
            if (pg > g)
                pg = g;
            *T0_ = 2 * T + offset;

            if (*T0_ < minperiod0)
                *T0_ = minperiod0;

            return pg;
        }
    }
}
