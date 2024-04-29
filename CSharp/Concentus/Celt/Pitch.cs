﻿/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2011 Xiph.Org Foundation
   Originally written by Jean-Marc Valin, Gregory Maxwell, Koen Vos,
   Timothy B. Terriberry, and the Opus open-source contributors
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

namespace Concentus.Celt
{
    using Concentus.Celt.Enums;
    using Concentus.Celt.Structs;
    using Concentus.Common;
    using Concentus.Common.CPlusPlus;
    using System;
    using System.Diagnostics;

    internal static class Pitch
    {
        internal static void find_best_pitch(Span<int> xcorr, Span<int> y, int len,
                                    int max_pitch, int[]best_pitch,
                                    int yshift, int maxcorr
                                    )
        {
            int i, j;
            int Syy = 1;
            int best_num_0;
            int best_num_1;
            int best_den_0;
            int best_den_1;
            int xshift = Inlines.celt_ilog2(maxcorr) - 14;

            best_num_0 = -1;
            best_num_1 = -1;
            best_den_0 = 0;
            best_den_1 = 0;
            best_pitch[0] = 0;
            best_pitch[1] = 1;
            for (j = 0; j < len; j++)
                Syy = Inlines.ADD32(Syy, Inlines.SHR32(Inlines.MULT16_16(y[j], y[j]), yshift));
            for (i = 0; i < max_pitch; i++)
            {
                if (xcorr[i] > 0)
                {
                    int num;
                    int xcorr16;
                    xcorr16 = Inlines.EXTRACT16(Inlines.VSHR32(xcorr[i], xshift));
                    num = Inlines.MULT16_16_Q15((xcorr16), (xcorr16));
                    if (Inlines.MULT16_32_Q15(num, best_den_1) > Inlines.MULT16_32_Q15(best_num_1, Syy))
                    {
                        if (Inlines.MULT16_32_Q15(num, best_den_0) > Inlines.MULT16_32_Q15(best_num_0, Syy))
                        {
                            best_num_1 = best_num_0;
                            best_den_1 = best_den_0;
                            best_pitch[1] = best_pitch[0];
                            best_num_0 = num;
                            best_den_0 = Syy;
                            best_pitch[0] = i;
                        }
                        else
                        {
                            best_num_1 = num;
                            best_den_1 = Syy;
                            best_pitch[1] = i;
                        }
                    }
                }

                Syy += Inlines.SHR32(Inlines.MULT16_16(y[i + len], y[i + len]), yshift) - Inlines.SHR32(Inlines.MULT16_16(y[i], y[i]), yshift);
                Syy = Inlines.MAX32(1, Syy);
            }
        }

        internal static void celt_fir5(int[] x,
                int[] num,
                int[] y,
                int N,
                int[] mem)
        {
            int i;
            int num0, num1, num2, num3, num4;
            int mem0, mem1, mem2, mem3, mem4;
            num0 = num[0];
            num1 = num[1];
            num2 = num[2];
            num3 = num[3];
            num4 = num[4];
            mem0 = mem[0];
            mem1 = mem[1];
            mem2 = mem[2];
            mem3 = mem[3];
            mem4 = mem[4];
            for (i = 0; i < N; i++)
            {
                int sum = Inlines.SHL32(Inlines.EXTEND32(x[i]), CeltConstants.SIG_SHIFT);
                sum = Inlines.MAC16_16(sum, num0, (mem0));
                sum = Inlines.MAC16_16(sum, num1, (mem1));
                sum = Inlines.MAC16_16(sum, num2, (mem2));
                sum = Inlines.MAC16_16(sum, num3, (mem3));
                sum = Inlines.MAC16_16(sum, num4, (mem4));
                mem4 = mem3;
                mem3 = mem2;
                mem2 = mem1;
                mem1 = mem0;
                mem0 = x[i];
                y[i] = Inlines.ROUND16(sum, CeltConstants.SIG_SHIFT);
            }
            mem[0] = (mem0);
            mem[1] = (mem1);
            mem[2] = (mem2);
            mem[3] = (mem3);
            mem[4] = (mem4);
        }


        internal static void pitch_downsample(int[][] x, int[] x_lp, int len, int C)
        {
            int i;
            int[] ac = new int[5];
            int tmp = CeltConstants.Q15ONE;
            int[] lpc = new int[4];
            int[] mem = new int[] { 0, 0, 0, 0, 0 };
            int[] lpc2 = new int[5];
            int c1 = ((short)(0.5 + (0.8f) * (((int)1) << (15))))/*Inlines.QCONST16(0.8f, 15)*/;

            int shift;
            int maxabs = Inlines.celt_maxabs32(x[0], 0, len);
            if (C == 2)
            {
                int maxabs_1 = Inlines.celt_maxabs32(x[1], 0, len);
                maxabs = Inlines.MAX32(maxabs, maxabs_1);
            }
            if (maxabs < 1)
                maxabs = 1;
            shift = Inlines.celt_ilog2(maxabs) - 10;
            if (shift < 0)
                shift = 0;
            if (C == 2)
                shift++;

            int halflen = len >> 1; // cached for performance
            for (i = 1; i < halflen; i++)
            {
                x_lp[i] = (Inlines.SHR32(Inlines.HALF32(Inlines.HALF32(x[0][(2 * i - 1)] + x[0][(2 * i + 1)]) + x[0][2 * i]), shift));
            }

            x_lp[0] = (Inlines.SHR32(Inlines.HALF32(Inlines.HALF32(x[0][1]) + x[0][0]), shift));

            if (C == 2)
            {
                for (i = 1; i < halflen; i++)
                    x_lp[i] += (Inlines.SHR32(Inlines.HALF32(Inlines.HALF32(x[1][(2 * i - 1)] + x[1][(2 * i + 1)]) + x[1][2 * i]), shift));
                x_lp[0] += (Inlines.SHR32(Inlines.HALF32(Inlines.HALF32(x[1][1]) + x[1][0]), shift));
            }

            Autocorrelation._celt_autocorr(x_lp, ac, null, 0, 4, halflen);

            /* Noise floor -40 dB */
            ac[0] += Inlines.SHR32(ac[0], 13);
            /* Lag windowing */
            for (i = 1; i <= 4; i++)
            {
                /*ac[i] *= exp(-.5*(2*M_PI*.002*i)*(2*M_PI*.002*i));*/
                ac[i] -= Inlines.MULT16_32_Q15((2 * i * i), ac[i]);
            }

            CeltLPC.celt_lpc(lpc, ac, 4);
            for (i = 0; i < 4; i++)
            {
                tmp = Inlines.MULT16_16_Q15(((short)(0.5 + (.9f) * (((int)1) << (15))))/*Inlines.QCONST16(.9f, 15)*/, tmp);
                lpc[i] = Inlines.MULT16_16_Q15(lpc[i], tmp);
            }
            /* Add a zero */
            lpc2[0] = (lpc[0] + ((short)(0.5 + (0.8f) * (((int)1) << (CeltConstants.SIG_SHIFT))))/*Inlines.QCONST16(0.8f, CeltConstants.SIG_SHIFT)*/);
            lpc2[1] = (lpc[1] + Inlines.MULT16_16_Q15(c1, lpc[0]));
            lpc2[2] = (lpc[2] + Inlines.MULT16_16_Q15(c1, lpc[1]));
            lpc2[3] = (lpc[3] + Inlines.MULT16_16_Q15(c1, lpc[2]));
            lpc2[4] = Inlines.MULT16_16_Q15(c1, lpc[3]);

            celt_fir5(x_lp, lpc2, x_lp, halflen, mem);
        }

        // Fixme: remove pointers and optimize
        internal static void pitch_search(Span<int> x_lp, int x_lp_ptr, int[] y,
                  int len, int max_pitch, out int pitch)
        {
            int i, j;
            int lag;
            int[] best_pitch = new int[] { 0, 0 };
            int maxcorr;
            int xmax, ymax;
            int shift = 0;
            int offset;

            Inlines.OpusAssert(len > 0);
            Inlines.OpusAssert(max_pitch > 0);
            lag = len + max_pitch;

            int[] x_lp4 = new int[len >> 2];
            int[] y_lp4 = new int[lag >> 2];
            int[] xcorr = new int[max_pitch >> 1];

            /* Downsample by 2 again */
            for (j = 0; j < len >> 2; j++)
                x_lp4[j] = x_lp[x_lp_ptr + (2 * j)];
            for (j = 0; j < lag >> 2; j++)
                y_lp4[j] = y[2 * j];

            xmax = Inlines.celt_maxabs32(x_lp4, len >> 2);
            ymax = Inlines.celt_maxabs32(y_lp4, lag >> 2);
            shift = Inlines.celt_ilog2(Inlines.MAX32(1, Inlines.MAX32(xmax, ymax))) - 11;
            if (shift > 0)
            {
                for (j = 0; j < len >> 2; j++)
                    x_lp4[j] = Inlines.SHR16(x_lp4[j], shift);
                for (j = 0; j < lag >> 2; j++)
                    y_lp4[j] = Inlines.SHR16(y_lp4[j], shift);
                /* Use double the shift for a MAC */
                shift *= 2;
            }
            else {
                shift = 0;
            }

            /* Coarse search with 4x decimation */
            maxcorr =  CeltPitchXCorr.pitch_xcorr(x_lp4, 0, y_lp4, 0, xcorr, len >> 2, max_pitch >> 2);

            find_best_pitch(xcorr, y_lp4, len >> 2, max_pitch >> 2, best_pitch, 0, maxcorr);

            /* Finer search with 2x decimation */
            maxcorr = 1;
            for (i = 0; i < max_pitch >> 1; i++)
            {
                int sum;
                xcorr[i] = 0;
                if (Inlines.abs(i - 2 * best_pitch[0]) > 2 && Inlines.abs(i - 2 * best_pitch[1]) > 2)
                {
                    continue;
                }
                sum = 0;
                for (j = 0; j < len >> 1; j++)
                    sum += Inlines.SHR32(Inlines.MULT16_16(x_lp[x_lp_ptr + j], y[i + j]), shift);
                
                xcorr[i] = Inlines.MAX32(-1, sum);
                maxcorr = Inlines.MAX32(maxcorr, sum);
            }
            find_best_pitch(xcorr, y, len >> 1, max_pitch >> 1, best_pitch, shift + 1, maxcorr);

            /* Refine by pseudo-interpolation */
            if (best_pitch[0] > 0 && best_pitch[0] < (max_pitch >> 1) - 1)
            {
                int a, b, c;
                a = xcorr[best_pitch[0] - 1];
                b = xcorr[best_pitch[0]];
                c = xcorr[best_pitch[0] + 1];
                if ((c - a) > Inlines.MULT16_32_Q15(((short)(0.5 + (.7f) * (((int)1) << (15))))/*Inlines.QCONST16(.7f, 15)*/, b - a))
                {
                    offset = 1;
                }
                else if ((a - c) > Inlines.MULT16_32_Q15(((short)(0.5 + (.7f) * (((int)1) << (15))))/*Inlines.QCONST16(.7f, 15)*/, b - c))
                {
                    offset = -1;
                }
                else
                {
                    offset = 0;
                }
            }
            else
            {
                offset = 0;
            }

            pitch = 2 * best_pitch[0] - offset;
        }

        private static readonly int[] second_check = { 0, 0, 3, 2, 3, 2, 5, 2, 3, 2, 3, 2, 5, 2, 3, 2 };

        internal static int remove_doubling(int[] x, int maxperiod, int minperiod,
            int N, ref int T0_, int prev_period, int prev_gain)
        {
            int k, i, T, T0;
            int g, g0;
            int pg;
            int yy, xx, xy, xy2;
            Span<int> xcorr = new int[3];
            int best_xy, best_yy;
            int offset;
            int minperiod0 = minperiod;
            maxperiod /= 2;
            minperiod /= 2;
            T0_ /= 2;
            prev_period /= 2;
            N /= 2;
            int x_ptr = maxperiod;
            if (T0_ >= maxperiod)
                T0_ = maxperiod - 1;

            T = T0 = T0_;
            Span<int> yy_lookup = new int[maxperiod + 1];
            Kernels.dual_inner_prod(x.AsSpan().Slice(x_ptr), x.AsSpan().Slice(x_ptr), x.AsSpan().Slice(x_ptr - T0), N, out xx, out xy);

            yy_lookup[0] = xx;
            yy = xx;
            for (i = 1; i <= maxperiod; i++)
            {
                int xi = x_ptr - i;
                yy = yy + Inlines.MULT16_16(x[xi], x[xi]) - Inlines.MULT16_16(x[xi + N], x[xi + N]);
                yy_lookup[i] = Inlines.MAX32(0, yy);
            }
            yy = yy_lookup[T0];
            best_xy = xy;
            best_yy = yy;

            {
                int x2y2;
                int sh, t;
                x2y2 = 1 + Inlines.HALF32(Inlines.MULT32_32_Q31(xx, yy));
                sh = Inlines.celt_ilog2(x2y2) >> 1;
                t = Inlines.VSHR32(x2y2, 2 * (sh - 7));
                g = (Inlines.VSHR32(Inlines.MULT16_32_Q15(Inlines.celt_rsqrt_norm(t), xy), sh + 1));
                g0 = g;
            }

            /* Look for any pitch at T/k */
            for (k = 2; k <= 15; k++)
            {
                int T1, T1b;
                int g1;
                int cont = 0;
                int thresh;
                T1 = Inlines.celt_udiv(2 * T0 + k, 2 * k);
                if (T1 < minperiod)
                {
                    break;
                }

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
                    T1b = Inlines.celt_udiv(2 * second_check[k] * T0 + k, 2 * k);
                }

                Kernels.dual_inner_prod(x.AsSpan().Slice(x_ptr), x.AsSpan().Slice(x_ptr - T1), x.AsSpan().Slice(x_ptr - T1b), N, out xy, out xy2);
                
                xy += xy2;
                yy = yy_lookup[T1] + yy_lookup[T1b];

                {
                    int x2y2;
                    int sh, t;
                    x2y2 = 1 + Inlines.MULT32_32_Q31(xx, yy);
                    sh = Inlines.celt_ilog2(x2y2) >> 1;
                    t = Inlines.VSHR32(x2y2, 2 * (sh - 7));
                    g1 = (Inlines.VSHR32(Inlines.MULT16_32_Q15(Inlines.celt_rsqrt_norm(t), xy), sh + 1));
                }

                if (Inlines.abs(T1 - prev_period) <= 1)
                    cont = prev_gain;
                else if (Inlines.abs(T1 - prev_period) <= 2 && 5 * k * k < T0)
                {
                    cont = Inlines.HALF16(prev_gain);
                }
                else
                {
                    cont = 0;
                }
                thresh = Inlines.MAX16(((short)(0.5 + (.3f) * (((int)1) << (15))))/*Inlines.QCONST16(.3f, 15)*/, (Inlines.MULT16_16_Q15(((short)(0.5 + (.7f) * (((int)1) << (15))))/*Inlines.QCONST16(.7f, 15)*/, g0) - cont));

                /* Bias against very high pitch (very short period) to avoid false-positives
                   due to short-term correlation */
                if (T1 < 3 * minperiod)
                {
                    thresh = Inlines.MAX16(((short)(0.5 + (.4f) * (((int)1) << (15))))/*Inlines.QCONST16(.4f, 15)*/, (Inlines.MULT16_16_Q15(((short)(0.5 + (.85f) * (((int)1) << (15))))/*Inlines.QCONST16(.85f, 15)*/, g0) - cont));
                }
                else if (T1 < 2 * minperiod)
                {
                    thresh = Inlines.MAX16(((short)(0.5 + (.5f) * (((int)1) << (15))))/*Inlines.QCONST16(.5f, 15)*/, (Inlines.MULT16_16_Q15(((short)(0.5 + (.9f) * (((int)1) << (15))))/*Inlines.QCONST16(.9f, 15)*/, g0) - cont));
                }
                if (g1 > thresh)
                {
                    best_xy = xy;
                    best_yy = yy;
                    T = T1;
                    g = g1;
                }
            }

            best_xy = Inlines.MAX32(0, best_xy);
            if (best_yy <= best_xy)
            {
                pg = CeltConstants.Q15ONE;
            }
            else
            {
                pg = (Inlines.SHR32(Inlines.frac_div32(best_xy, best_yy + 1), 16));
            }

            for (k = 0; k < 3; k++)
            {
                xcorr[k] = Kernels.celt_inner_prod(x.AsSpan().Slice(x_ptr), x.AsSpan().Slice(x_ptr - (T + k - 1)), N);
            }

            if ((xcorr[2] - xcorr[0]) > Inlines.MULT16_32_Q15(((short)(0.5 + (.7f) * (((int)1) << (15))))/*Inlines.QCONST16(.7f, 15)*/, xcorr[1] - xcorr[0]))
            {
                offset = 1;
            }
            else if ((xcorr[0] - xcorr[2]) > Inlines.MULT16_32_Q15(((short)(0.5 + (.7f) * (((int)1) << (15))))/*Inlines.QCONST16(.7f, 15)*/, xcorr[1] - xcorr[2]))
            {
                offset = -1;
            }
            else
            {
                offset = 0;
            }

            if (pg > g)
            {
                pg = g;
            }

            T0_ = 2 * T + offset;

            if (T0_ < minperiod0)
            {
                T0_ = minperiod0;
            }

            return pg;
        }

    }
}
