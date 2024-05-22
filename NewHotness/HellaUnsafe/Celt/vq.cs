﻿using System;

namespace HellaUnsafe.Celt
{
    internal static class vq
    {
        internal static unsafe void exp_rotation1(float* X, int len, int stride, float c, float s)
        {
            int i;
            float ms;
            float* Xptr;
            Xptr = X;
            ms = Inlines.NEG16(s);
            for (i = 0; i < len - stride; i++)
            {
                float x1, x2;
                x1 = Xptr[0];
                x2 = Xptr[stride];
                Xptr[stride] = (Inlines.PSHR32(Inlines.MAC16_16(Inlines.MULT16_16(c, x2), s, x1), 15));
                *Xptr++ = (Inlines.PSHR32(Inlines.MAC16_16(Inlines.MULT16_16(c, x1), ms, x2), 15));
            }
            Xptr = &X[len - 2 * stride - 1];
            for (i = len - 2 * stride - 1; i >= 0; i--)
            {
                float x1, x2;
                x1 = Xptr[0];
                x2 = Xptr[stride];
                Xptr[stride] = (Inlines.PSHR32(Inlines.MAC16_16(Inlines.MULT16_16(c, x2), s, x1), 15));
                *Xptr-- = (Inlines.PSHR32(Inlines.MAC16_16(Inlines.MULT16_16(c, x1), ms, x2), 15));
            }
        }

        private static readonly int[] SPREAD_FACTOR = new int[] { 15, 10, 5 };

        internal static unsafe void exp_rotation(float* X, int len, int dir, int stride, int K, int spread)
        {
            int i;
            float c, s;
            float gain, theta;
            int stride2 = 0;
            int factor;

            if (2 * K >= len || spread == SPREAD_NONE)
                return;
            factor = SPREAD_FACTOR[spread - 1];

            gain = Inlines.celt_div((float)Inlines.MULT16_16(Inlines.Q15_ONE, len), (float)(len + factor * K));
            theta = Inlines.HALF16(Inlines.MULT16_16_Q15(gain, gain));

            c = Inlines.celt_cos_norm(Inlines.EXTEND32(theta));
            s = Inlines.celt_cos_norm(Inlines.EXTEND32(Inlines.SUB16(Inlines.Q15ONE, theta))); /*  sin(theta) */

            if (len >= 8 * stride)
            {
                stride2 = 1;
                /* This is just a simple (equivalent) way of computing sqrt(len/stride) with rounding.
                   It's basically incrementing long as (stride2+0.5)^2 < len/stride. */
                while ((stride2 * stride2 + stride2) * stride + (stride >> 2) < len)
                    stride2++;
            }
            /*NOTE: As a minor optimization, we could be passing around log2(B), not B, for both this and for
               extract_collapse_mask().*/
            len = (int)Inlines.celt_udiv((uint)len, (uint)stride);
            for (i = 0; i < stride; i++)
            {
                if (dir < 0)
                {
                    if (stride2 != 0)
                        exp_rotation1(X + i * len, len, stride2, s, c);
                    exp_rotation1(X + i * len, len, 1, c, s);
                }
                else
                {
                    exp_rotation1(X + i * len, len, 1, c, -s);
                    if (stride2 != 0)
                        exp_rotation1(X + i * len, len, stride2, s, -c);
                }
            }
        }

        internal static unsafe void normalise_residual(Span<int> iy, float* X, int N, float Ryy, float gain)
        {
            int i;
            const int k = 0;
            float t;
            float g;

            t = Inlines.VSHR32(Ryy, 2 * (k - 7));
            g = Inlines.MULT16_16_P15(Inlines.celt_rsqrt_norm(t), gain);

            i = 0;
            do
                X[i] = Inlines.EXTRACT16(Inlines.PSHR32(Inlines.MULT16_16(g, iy[i]), k + 1));
            while (++i < N);
        }

        internal static unsafe uint extract_collapse_mask(Span<int> iy, int N, int B)
        {
            uint collapse_mask;
            int N0;
            int i;
            if (B <= 1)
                return 1;
            /*NOTE: As a minor optimization, we could be passing around log2(B), not B, for both this and for
               exp_rotation().*/
            N0 = (int)Inlines.celt_udiv((uint)N, (uint)B);
            collapse_mask = 0;
            i = 0; do
            {
                int j;
                int tmp = 0;
                j = 0; do
                {
                    tmp |= iy[i * N0 + j];
                } while (++j < N0);
                collapse_mask |= (tmp != 0) << i;
            } while (++i < B);
            return collapse_mask;
        }

        internal static unsafe float op_pvq_search_c(float* X, int* iy, int K, int N, int unused_arch)
        {
            const int rshift = 0;
            int i, j;
            int pulsesLeft;
            float sum;
            float xy;
            float yy;

            Span<float> y = new float[N];
            Span<int> signx = new int[N];

            /* Get rid of the sign */
            sum = 0;
            j = 0; do
            {
                signx[j] = X[j] < 0;
                /* OPT: Make sure the compiler doesn't use a branch on ABS16(). */
                X[j] = Inlines.ABS16(X[j]);
                iy[j] = 0;
                y[j] = 0;
            } while (++j < N);

            xy = yy = 0;

            pulsesLeft = K;

            /* Do a pre-search by projecting on the pyramid */
            if (K > (N >> 1))
            {
                float rcp;
                j = 0; do
                {
                    sum += X[j];
                } while (++j < N);

                /* If X is too small, just replace it with a pulse at 0 */
                /* Prevents infinities and NaNs from causing too many pulses
                   to be allocated. 64 is an approximation of infinity here. */
                if (!(sum > Inlines.EPSILON && sum < 64))
                {
                    X[0] = 1.0f;
                    j = 1; do
                        X[j] = 0;
                    while (++j < N);
                    sum = 1.0f;
                }
                /* Using K+e with e < 1 guarantees we cannot get more than K pulses. */
                rcp = Inlines.EXTRACT16(Inlines.MULT16_32_Q16(K + 0.8f, Inlines.celt_rcp(sum)));
                j = 0; do
                {
                    iy[j] = (int)Math.Floor(rcp * X[j]);
                    y[j] = (float)iy[j];
                    yy = Inlines.MAC16_16(yy, y[j], y[j]);
                    xy = Inlines.MAC16_16(xy, X[j], y[j]);
                    y[j] *= 2;
                    pulsesLeft -= iy[j];
                } while (++j < N);
            }

            Inlines.ASSERT(pulsesLeft >= 0);

            /* This should never happen, but just in case it does (e.g. on silence)
               we fill the first bin with pulses. */
            if (pulsesLeft > N + 3)
            {
                float tmp = (float)pulsesLeft;
                yy = Inlines.MAC16_16(yy, tmp, tmp);
                yy = Inlines.MAC16_16(yy, tmp, y[0]);
                iy[0] += pulsesLeft;
                pulsesLeft = 0;
            }

            for (i = 0; i < pulsesLeft; i++)
            {
                float Rxy, Ryy;
                int best_id;
                float best_num;
                float best_den;
                best_id = 0;
                /* The squared magnitude term gets added anyway, so we might as well
                   add it outside the loop */
                yy = Inlines.ADD16(yy, 1);

                /* Calculations for position 0 are out of the loop, in part to reduce
                   mispredicted branches (since the if condition is usually false)
                   in the loop. */
                /* Temporary sums of the new pulse(s) */
                Rxy = Inlines.EXTRACT16(Inlines.SHR32(Inlines.ADD32(xy, Inlines.EXTEND32(X[0])), rshift));
                /* We're multiplying y[j] by two so we don't have to do it here */
                Ryy = Inlines.ADD16(yy, y[0]);

                /* Approximate score: we maximise Rxy/sqrt(Ryy) (we're guaranteed that
                   Rxy is positive because the sign is pre-computed) */
                Rxy = Inlines.MULT16_16_Q15(Rxy, Rxy);
                best_den = Ryy;
                best_num = Rxy;
                j = 1;
                do
                {
                    /* Temporary sums of the new pulse(s) */
                    Rxy = Inlines.EXTRACT16(Inlines.SHR32(Inlines.ADD32(xy, Inlines.EXTEND32(X[j])), rshift));
                    /* We're multiplying y[j] by two so we don't have to do it here */
                    Ryy = Inlines.ADD16(yy, y[j]);

                    /* Approximate score: we maximise Rxy/sqrt(Ryy) (we're guaranteed that
                       Rxy is positive because the sign is pre-computed) */
                    Rxy = Inlines.MULT16_16_Q15(Rxy, Rxy);
                    /* The idea is to check for num/den >= best_num/best_den, but that way
                       we can do it without any division */
                    /* OPT: It's not clear whether a cmov is faster than a branch here
                       since the condition is more often false than true and using
                       a cmov introduces data dependencies across iterations. The optimal
                       choice may be architecture-dependent. */
                    if ((Inlines.MULT16_16(best_den, Rxy) > Inlines.MULT16_16(Ryy, best_num)))
                    {
                        best_den = Ryy;
                        best_num = Rxy;
                        best_id = j;
                    }
                } while (++j < N);

                /* Updating the sums of the new pulse(s) */
                xy = Inlines.ADD32(xy, Inlines.EXTEND32(X[best_id]));
                /* We're multiplying y[j] by two so we don't have to do it here */
                yy = Inlines.ADD16(yy, y[best_id]);

                /* Only now that we've made the final choice, update y/iy */
                /* Multiplying y[j] by 2 so we don't have to do it everywhere else */
                y[best_id] += 2;
                iy[best_id]++;
            }

            /* Put the original sign back */
            j = 0;
            do
            {
                /*iy[j] = signx[j] ? -iy[j] : iy[j];*/
                /* OPT: The is more likely to be compiled without a branch than the code above
                   but has the same performance otherwise. */
                iy[j] = (iy[j] ^ -signx[j]) + signx[j];
            } while (++j < N);

            return yy;
        }

        internal static unsafe uint alg_quant(float* X, int N, int K, int spread, int B, ec_enc* enc,
            float gain, int resynth, int arch)
        {
            Span<int> iy;
            float yy;
            uint collapse_mask;

            Inlines.ASSERT(K > 0, "alg_quant() needs at least one pulse");
            Inlines.ASSERT(N > 1, "alg_quant() needs at least two dimensions");

            /* Covers vectorization by up to 4. */
            iy = new int[N * 3];

            exp_rotation(X, N, 1, B, K, spread);

            yy = op_pvq_search(X, iy, K, N, arch);

            encode_pulses(iy, N, K, enc);

            if (resynth != 0)
            {
                normalise_residual(iy, X, N, yy, gain);
                exp_rotation(X, N, -1, B, K, spread);
            }

            collapse_mask = extract_collapse_mask(iy, N, B);
            return collapse_mask;
        }

        /** Decode pulse vector and combine the result with the pitch vector to produce
    the final normalised signal in the current band. */
        internal static unsafe uint alg_unquant(float* X, int N, int K, int spread, int B,
              ec_dec* dec, float gain)
        {
            float Ryy;
            uint collapse_mask;
            Span<int> iy;

            Inlines.ASSERT(K > 0, "alg_unquant() needs at least one pulse");
            Inlines.ASSERT(N > 1, "alg_unquant() needs at least two dimensions");
            iy = new int[N];
            Ryy = decode_pulses(iy, N, K, dec);
            normalise_residual(iy, X, N, Ryy, gain);
            exp_rotation(X, N, -1, B, K, spread);
            collapse_mask = extract_collapse_mask(iy, N, B);
            return collapse_mask;
        }

        internal static unsafe void renormalise_vector(float* X, int N, float gain, int arch)
        {
            const int k = 0;
            int i;
            float E;
            float g;
            float t;
            float* xptr;
            E = Inlines.EPSILON + celt_inner_prod(X, X, N, arch);
            t = Inlines.VSHR32(E, 2 * (k - 7));
            g = Inlines.MULT16_16_P15(Inlines.celt_rsqrt_norm(t), gain);

            xptr = X;
            for (i = 0; i < N; i++)
            {
                *xptr = Inlines.EXTRACT16(Inlines.PSHR32(Inlines.MULT16_16(g, *xptr), k + 1));
                xptr++;
            }
            /*return celt_sqrt(E);*/
        }

        internal static unsafe int stereo_itheta(in float* X, in float* Y, int stereo, int N, int arch)
        {
            int i;
            int itheta;
            float mid, side;
            float Emid, Eside;

            Emid = Eside = Inlines.EPSILON;
            if (stereo != 0)
            {
                for (i = 0; i < N; i++)
                {
                    float m, s;
                    m = Inlines.ADD16(Inlines.SHR16(X[i], 1), Inlines.SHR16(Y[i], 1));
                    s = Inlines.SUB16(Inlines.SHR16(X[i], 1), Inlines.SHR16(Y[i], 1));
                    Emid = Inlines.MAC16_16(Emid, m, m);
                    Eside = Inlines.MAC16_16(Eside, s, s);
                }
            }
            else
            {
                Emid += celt_inner_prod(X, X, N, arch);
                Eside += celt_inner_prod(Y, Y, N, arch);
            }
            mid = Inlines.celt_sqrt(Emid);
            side = Inlines.celt_sqrt(Eside);
            itheta = (int)Math.Floor(.5f + 16384 * 0.63662f * Inlines.fast_atan2f(side, mid));

            return itheta;
        }
    }
}
