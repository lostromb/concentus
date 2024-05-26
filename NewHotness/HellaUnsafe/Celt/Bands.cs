/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2009 Xiph.Org Foundation
   Copyright (c) 2008-2009 Gregory Maxwell
   Written by Jean-Marc Valin and Gregory Maxwell */
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

using HellaUnsafe.Common;
using System;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.EntEnc;
using static HellaUnsafe.Celt.EntDec;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Celt.Modes;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Celt.Rate;
using static HellaUnsafe.Celt.QuantBands;
using static HellaUnsafe.Celt.VQ;
using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Celt
{
    internal static class Bands
    {
        internal const int SPREAD_NONE = 0;
        internal const int SPREAD_LIGHT = 1;
        internal const int SPREAD_NORMAL = 2;
        internal const int SPREAD_AGGRESSIVE = 3;

        internal static unsafe int hysteresis_decision(float val, in float* thresholds, in float* hysteresis, int N, int prev)
        {
            int i;
            for (i = 0; i < N; i++)
            {
                if (val < thresholds[i])
                    break;
            }
            if (i > prev && val < thresholds[prev] + hysteresis[prev])
                i = prev;
            if (i < prev && val > thresholds[prev - 1] - hysteresis[prev - 1])
                i = prev;
            return i;
        }

        internal static uint celt_lcg_rand(uint seed)
        {
            return 1664525 * seed + 1013904223;
        }

        /* This is a cos() approximation designed to be bit-exact on any platform. Bit exactness
         with this approximation is important because it has an impact on the bit allocation */
        internal static int bitexact_cos(short x)
        {
            int tmp;
            int x2;
            tmp = (4096 + ((int)(x) * (x))) >> 13;
            ASSERT(tmp <= 32767);
            x2 = tmp;
            x2 = (32767 - x2) + FRAC_MUL16(x2, (-7651 + FRAC_MUL16(x2, (8277 + FRAC_MUL16(-626, x2)))));
            ASSERT(x2 <= 32766);
            return 1 + x2;
        }

        internal static int bitexact_log2tan(int isin, int icos)
        {
            int lc;
            int ls;
            lc = EC_ILOG((uint)icos);
            ls = EC_ILOG((uint)isin);
            icos <<= 15 - lc;
            isin <<= 15 - ls;
            return (ls - lc) * (1 << 11)
                  + FRAC_MUL16(isin, FRAC_MUL16(isin, -2597) + 7932)
                  - FRAC_MUL16(icos, FRAC_MUL16(icos, -2597) + 7932);
        }

        internal static unsafe void compute_band_energies(in CELTMode m, in float* X, float* bandE, int end, int C, int LM)
        {
            int i, c, N;
            short[] eBands = m.eBands;
            N = m.shortMdctSize << LM;
            c = 0; do
            {
                for (i = 0; i < end; i++)
                {
                    float sum;
                    sum = 1e-27f + celt_inner_prod(&X[c * N + (eBands[i] << LM)], &X[c * N + (eBands[i] << LM)], (eBands[i + 1] - eBands[i]) << LM);
                    bandE[i + c * m.nbEBands] = celt_sqrt(sum);
                    /*printf ("%f ", bandE[i+c*m.nbEBands]);*/
                }
            } while (++c < C);
            /*printf ("\n");*/
        }

        internal static unsafe void normalise_bands(in CELTMode m, in float* freq, float* X, in float* bandE, int end, int C, int M)
        {
            int i, c, N;
            short[] eBands = m.eBands;
            N = M * m.shortMdctSize;
            c = 0; do
            {
                for (i = 0; i < end; i++)
                {
                    int j;
                    float g = 1.0f / (1e-27f + bandE[i + c * m.nbEBands]);
                    for (j = M * eBands[i]; j < M * eBands[i + 1]; j++)
                        X[j + c * N] = freq[j + c * N] * g;
                }
            } while (++c < C);
        }

        /* De-normalise the energy to produce the synthesis from the unit-energy bands */
        internal static unsafe void denormalise_bands(in CELTMode m, in float* X,
            float* freq, in float* bandLogE, int start,
            int end, int M, int downsample, int silence)
        {
            int i, N;
            int bound;
            float* f;
            float* x;
            short[] eBands = m.eBands;
            N = M * m.shortMdctSize;
            bound = M * eBands[end];
            if (downsample != 1)
                bound = IMIN(bound, N / downsample);
            if (silence != 0)
            {
                bound = 0;
                start = end = 0;
            }
            f = freq;
            x = X + M * eBands[start];
            for (i = 0; i < M * eBands[start]; i++)
                *f++ = 0;
            for (i = start; i < end; i++)
            {
                int j, band_end;
                float g;
                float lg;
                int shift = 0;
                j = M * eBands[i];
                band_end = M * eBands[i + 1];
                lg = SATURATE16(ADD32(bandLogE[i], SHL32((float)eMeans[i], 6)));
                g = celt_exp2(MIN32(32.0f, lg));
                /* Be careful of the fixed-point "else" just above when changing this code */
                do
                {
                    *f++ = SHR32(MULT16_16(*x++, g), shift);
                } while (++j < band_end);
            }
            ASSERT(start <= end);
            OPUS_CLEAR(&freq[bound], N - bound);
        }

        internal static unsafe void anti_collapse(
            in CELTMode m, float* X_, byte* collapse_masks, int LM, int C, int size,
          int start, int end, in float* logE, in float* prev1logE,
          in float* prev2logE, in int* pulses, uint seed)
        {
            int c, i, j, k;
            for (i = start; i < end; i++)
            {
                int N0;
                float thresh, sqrt_1;
                int depth;

                N0 = m.eBands[i + 1] - m.eBands[i];
                /* depth in 1/8 bits */
                ASSERT(pulses[i] >= 0);
                depth = celt_sudiv(1 + pulses[i], (m.eBands[i + 1] - m.eBands[i])) >> LM;

                thresh = .5f * celt_exp2(-.125f * depth);
                sqrt_1 = celt_rsqrt(N0 << LM);

                c = 0; do
                {
                    float* X;
                    float prev1;
                    float prev2;
                    float Ediff;
                    float r;
                    int renormalize = 0;
                    prev1 = prev1logE[c * m.nbEBands + i];
                    prev2 = prev2logE[c * m.nbEBands + i];
                    if (C == 1)
                    {
                        prev1 = MAX16(prev1, prev1logE[m.nbEBands + i]);
                        prev2 = MAX16(prev2, prev2logE[m.nbEBands + i]);
                    }
                    Ediff = EXTEND32(logE[c * m.nbEBands + i]) - EXTEND32(MIN16(prev1, prev2));
                    Ediff = MAX32(0, Ediff);

                    /* r needs to be multiplied by 2 or 2*sqrt(2) depending on LM because
                       short blocks don't have the same energy as long */
                    r = 2.0f * celt_exp2(-Ediff);
                    if (LM == 3)
                        r *= 1.41421356f;
                    r = MIN16(thresh, r);
                    r = r * sqrt_1;
                    X = X_ + c * size + (m.eBands[i] << LM);
                    for (k = 0; k < 1 << LM; k++)
                    {
                        /* Detect collapse */
                        if ((collapse_masks[i * C + c] & 1 << k) == 0)
                        {
                            /* Fill with noise */
                            for (j = 0; j < N0; j++)
                            {
                                seed = celt_lcg_rand(seed);
                                X[(j << LM) + k] = ((seed & 0x8000) != 0 ? r : -r);
                            }
                            renormalize = 1;
                        }
                    }
                    /* We just added some energy, so we need to renormalise */
                    if (renormalize != 0)
                        renormalise_vector(X, N0 << LM, Q15ONE);
                } while (++c < C);
            }
        }

        /* Compute the weights to use for optimizing normalized distortion across
           channels. We use the amplitude to weight square distortion, which means
           that we use the square root of the value we would have been using if we
           wanted to minimize the MSE in the non-normalized domain. This roughly
           corresponds to some quick-and-dirty perceptual experiments I ran to
           measure inter-aural masking (there doesn't seem to be any published data
           on the topic). */
        static void compute_channel_weights(float Ex, float Ey, ref float w_0, ref float w_1)
        {
            float minE;
            int shift = 0;
            minE = MIN32(Ex, Ey);
            /* Adjustment to make the weights a bit more conservative. */
            Ex = ADD32(Ex, minE / 3);
            Ey = ADD32(Ey, minE / 3);
            w_0 = VSHR32(Ex, shift);
            w_1 = VSHR32(Ey, shift);
        }

        internal static unsafe void intensity_stereo(
            in CELTMode m, float* X, in float* Y, ReadOnlySpan<float> bandE, int bandID, int N)
        {
            int i = bandID;
            int j;
            float a1, a2;
            float left, right;
            float norm;
            int shift = 0;
            left = VSHR32(bandE[i], shift);
            right = VSHR32(bandE[i + m.nbEBands], shift);
            norm = EPSILON + celt_sqrt(EPSILON + MULT16_16(left, left) + MULT16_16(right, right));
            a1 = DIV32_16(SHL32(EXTEND32(left), 14), norm);
            a2 = DIV32_16(SHL32(EXTEND32(right), 14), norm);
            for (j = 0; j < N; j++)
            {
                float r, l;
                l = X[j];
                r = Y[j];
                X[j] = EXTRACT16(SHR32(MAC16_16(MULT16_16(a1, l), a2, r), 14));
                /* Side is not encoded, no need to calculate */
            }
        }

        internal static unsafe void stereo_split(float* X, float* Y, int N)
        {
            int j;
            for (j = 0; j < N; j++)
            {
                float r, l;
                l = MULT16_16(QCONST16(.70710678f, 15), X[j]);
                r = MULT16_16(QCONST16(.70710678f, 15), Y[j]);
                X[j] = EXTRACT16(SHR32(ADD32(l, r), 15));
                Y[j] = EXTRACT16(SHR32(SUB32(r, l), 15));
            }
        }

        internal static unsafe void stereo_merge(float* X, float* Y, float mid, int N)
        {
            int j;
            float xp = 0, side = 0;
            float El, Er;
            float mid2;
            int kl = 0, kr = 0;
            float t, lgain, rgain;

            /* Compute the norm of X+Y and X-Y as |X|^2 + |Y|^2 +/- sum(xy) */
            dual_inner_prod(Y, X, Y, N, &xp, &side);
            /* Compensating for the mid normalization */
            xp = MULT16_32_Q15(mid, xp);
            /* mid and side are in Q15, not Q14 like X and Y */
            mid2 = SHR16(mid, 1);
            El = MULT16_16(mid2, mid2) + side - 2 * xp;
            Er = MULT16_16(mid2, mid2) + side + 2 * xp;
            if (Er < QCONST32(6e-4f, 28) || El < QCONST32(6e-4f, 28))
            {
                OPUS_COPY(Y, X, N);
                return;
            }

            t = VSHR32(El, (kl - 7) << 1);
            lgain = celt_rsqrt_norm(t);
            t = VSHR32(Er, (kr - 7) << 1);
            rgain = celt_rsqrt_norm(t);

            for (j = 0; j < N; j++)
            {
                float r, l;
                /* Apply mid scaling (side is already scaled) */
                l = MULT16_16_P15(mid, X[j]);
                r = Y[j];
                X[j] = EXTRACT16(PSHR32(MULT16_16(lgain, SUB16(l, r)), kl + 1));
                Y[j] = EXTRACT16(PSHR32(MULT16_16(rgain, ADD16(l, r)), kr + 1));
            }
        }

        /* Decide whether we should spread the pulses in the current frame */
        internal static unsafe int spreading_decision(in CELTMode m, in float* X, int* average,
          int last_decision, int* hf_average, int* tapset_decision, int update_hf,
          int end, int C, int M, in int* spread_weight)
        {
            int i, c, N0;
            int sum = 0, nbBands = 0;
            short[] eBands = m.eBands;
            int decision;
            int hf_sum = 0;

            ASSERT(end > 0);

            N0 = M * m.shortMdctSize;

            if (M * (eBands[end] - eBands[end - 1]) <= 8)
                return SPREAD_NONE;
            c = 0; do
            {
                for (i = 0; i < end; i++)
                {
                    int j, N, tmp = 0;
                    Span<int> tcount = stackalloc int[3];
                    float* x = X + M * eBands[i] + c * N0;
                    N = M * (eBands[i + 1] - eBands[i]);
                    if (N <= 8)
                        continue;
                    /* Compute rough CDF of |x[j]| */
                    for (j = 0; j < N; j++)
                    {
                        float x2N; /* Q13 */

                        x2N = MULT16_16(MULT16_16_Q15(x[j], x[j]), N);
                        if (x2N < QCONST16(0.25f, 13))
                            tcount[0]++;
                        if (x2N < QCONST16(0.0625f, 13))
                            tcount[1]++;
                        if (x2N < QCONST16(0.015625f, 13))
                            tcount[2]++;
                    }

                    /* Only include four last bands (8 kHz and up) */
                    if (i > m.nbEBands - 4)
                        hf_sum += celt_sudiv(32 * (tcount[1] + tcount[0]), N);
                    tmp = (2 * tcount[2] >= N ? 1 : 0) + (2 * tcount[1] >= N ? 1 : 0) + (2 * tcount[0] >= N ? 1 : 0);
                    sum += tmp * spread_weight[i];
                    nbBands += spread_weight[i];
                }
            } while (++c < C);

            if (update_hf != 0)
            {
                if (hf_sum != 0)
                    hf_sum = celt_sudiv(hf_sum, C * (4 - m.nbEBands + end));
                *hf_average = (*hf_average + hf_sum) >> 1;
                hf_sum = *hf_average;
                if (*tapset_decision == 2)
                    hf_sum += 4;
                else if (*tapset_decision == 0)
                    hf_sum -= 4;
                if (hf_sum > 22)
                    *tapset_decision = 2;
                else if (hf_sum > 18)
                    *tapset_decision = 1;
                else
                    *tapset_decision = 0;
            }
            /*printf("%d %d %d\n", hf_sum, *hf_average, *tapset_decision);*/
            ASSERT(nbBands > 0); /* end has to be non-zero */
            ASSERT(sum >= 0);
            sum = celt_sudiv((int)sum << 8, nbBands);
            /* Recursive averaging */
            sum = (sum + *average) >> 1;
            *average = sum;
            /* Hysteresis */
            sum = (3 * sum + (((3 - last_decision) << 7) + 64) + 2) >> 2;
            if (sum < 80)
            {
                decision = SPREAD_AGGRESSIVE;
            }
            else if (sum < 256)
            {
                decision = SPREAD_NORMAL;
            }
            else if (sum < 384)
            {
                decision = SPREAD_LIGHT;
            }
            else
            {
                decision = SPREAD_NONE;
            }
            return decision;
        }

        /* Indexing table for converting from natural Hadamard to ordery Hadamard
           This is essentially a bit-reversed Gray, on top of which we've added
           an inversion of the order because we want the DC at the end rather than
           the beginning. The lines are for N=2, 4, 8, 16 */
        internal static readonly int[] ordery_table = {
               1,  0,
               3,  0,  2,  1,
               7,  0,  4,  3,  6,  1,  5,  2,
              15,  0,  8,  7, 12,  3, 11,  4, 14,  1,  9,  6, 13,  2, 10,  5,
        };

        internal static unsafe void deinterleave_hadamard(float* X, int N0, int stride, int hadamard)
        {
            int i, j;
            Span<float> tmp;
            int N;
            N = N0 * stride;
            tmp = new float[N];
            ASSERT(stride > 0);
            if (hadamard != 0)
            {
                ReadOnlySpan<int> ordery = ordery_table.AsSpan(stride - 2);
                for (i = 0; i < stride; i++)
                {
                    for (j = 0; j < N0; j++)
                        tmp[ordery[i] * N0 + j] = X[j * stride + i];
                }
            }
            else
            {
                for (i = 0; i < stride; i++)
                    for (j = 0; j < N0; j++)
                        tmp[i * N0 + j] = X[j * stride + i];
            }
            OPUS_COPY(X, tmp, N);
        }

        internal static unsafe void interleave_hadamard(float* X, int N0, int stride, int hadamard)
        {
            int i, j;
            Span<float> tmp;
            int N;
            N = N0 * stride;
            tmp = new float[N];
            if (hadamard != 0)
            {
                ReadOnlySpan<int> ordery = ordery_table.AsSpan(stride - 2);
                for (i = 0; i < stride; i++)
                    for (j = 0; j < N0; j++)
                        tmp[j * stride + i] = X[ordery[i] * N0 + j];
            }
            else
            {
                for (i = 0; i < stride; i++)
                    for (j = 0; j < N0; j++)
                        tmp[j * stride + i] = X[i * N0 + j];
            }
            OPUS_COPY(X, tmp, N);
        }

        internal static unsafe void haar1(float* X, int N0, int stride)
        {
            int i, j;
            N0 >>= 1;
            for (i = 0; i < stride; i++)
                for (j = 0; j < N0; j++)
                {
                    float tmp1, tmp2;
                    tmp1 = MULT16_16(QCONST16(.70710678f, 15), X[stride * 2 * j + i]);
                    tmp2 = MULT16_16(QCONST16(.70710678f, 15), X[stride * (2 * j + 1) + i]);
                    X[stride * 2 * j + i] = EXTRACT16(PSHR32(ADD32(tmp1, tmp2), 15));
                    X[stride * (2 * j + 1) + i] = EXTRACT16(PSHR32(SUB32(tmp1, tmp2), 15));
                }
        }

        internal static readonly short[] exp2_table8 =
               {16384, 17866, 19483, 21247, 23170, 25267, 27554, 30048};

        internal static unsafe int compute_qn(int N, int b, int offset, int pulse_cap, int stereo)
        {

            int qn, qb;
            int N2 = 2 * N - 1;
            if (stereo != 0 && N == 2)
                N2--;
            /* The upper limit ensures that in a stereo split with itheta==16384, we'll
                always have enough bits left over to code at least one pulse in the
                side; otherwise it would collapse, since it doesn't get folded. */
            qb = celt_sudiv(b + N2 * offset, N2);
            qb = IMIN(b - pulse_cap - (4 << BITRES), qb);

            qb = IMIN(8 << BITRES, qb);

            if (qb < (1 << BITRES >> 1))
            {
                qn = 1;
            }
            else
            {
                qn = exp2_table8[qb & 0x7] >> (14 - (qb >> BITRES));
                qn = (qn + 1) >> 1 << 1;
            }
            ASSERT(qn <= 256);
            return qn;
        }

        internal struct band_ctx
        {
            internal int encode;
            internal int resynth;
            internal StructRef<CELTMode> m;
            internal int i;
            internal int intensity;
            internal int spread;
            internal int tf_change;
            internal StructRef<ec_ctx> ec;
            internal int remaining_bits;
            internal float[] bandE;
            internal uint seed;
            internal int theta_round;
            internal int disable_inv;
            internal int avoid_split_noise;
        };

        internal struct split_ctx
        {
            internal int inv;
            internal int imid;
            internal int iside;
            internal int delta;
            internal int itheta;
            internal int qalloc;
        };

        internal static unsafe void compute_theta(ref band_ctx ctx, ref split_ctx sctx, in byte* ecbuf,
          float* X, float* Y, int N, int* b, int B, int B0,
          int LM,
          int stereo, int* fill)
        {
            int qn;
            int itheta = 0;
            int delta;
            int imid, iside;
            int qalloc;
            int pulse_cap;
            int offset;
            int tell;
            int inv = 0;
            int encode;
            int i;
            int intensity;
            ref ec_ctx ec = ref ctx.ec.Value;
            float[] bandE;

            encode = ctx.encode;
            ref CELTMode m = ref ctx.m.Value;
            i = ctx.i;
            intensity = ctx.intensity;
            bandE = ctx.bandE;

            /* Decide on the resolution to give to the split parameter theta */
            pulse_cap = m.logN[i] + LM * (1 << BITRES);
            offset = (pulse_cap >> 1) - (stereo != 0 && N == 2 ? QTHETA_OFFSET_TWOPHASE : QTHETA_OFFSET);
            qn = compute_qn(N, *b, offset, pulse_cap, stereo);
            if (stereo != 0 && i >= intensity)
                qn = 1;
            if (encode != 0)
            {
                /* theta is the atan() of the ratio between the (normalized)
                   side and mid. With just that parameter, we can re-scale both
                   mid and side because we know that 1) they have unit norm and
                   2) they are orthogonal. */
                itheta = stereo_itheta(X, Y, stereo, N);
            }
            tell = (int)ec_tell_frac(ec);
            if (qn != 1)
            {
                if (encode != 0)
                {
                    if (stereo == 0 || ctx.theta_round == 0)
                    {
                        itheta = (itheta * (int)qn + 8192) >> 14;
                        if (stereo == 0 && ctx.avoid_split_noise != 0 && itheta > 0 && itheta < qn)
                        {
                            /* Check if the selected value of theta will cause the bit allocation
                               to inject noise on one side. If so, make sure the energy of that side
                               is zero. */
                            int unquantized = celt_sudiv((int)itheta * 16384, qn);
                            imid = bitexact_cos((short)unquantized);
                            iside = bitexact_cos((short)(16384 - unquantized));
                            delta = FRAC_MUL16((N - 1) << 7, bitexact_log2tan(iside, imid));
                            if (delta > *b)
                                itheta = qn;
                            else if (delta < -*b)
                                itheta = 0;
                        }
                    }
                    else
                    {
                        int down;
                        /* Bias quantization towards itheta=0 and itheta=16384. */
                        int bias = itheta > 8192 ? 32767 / qn : -32767 / qn;
                        down = IMIN(qn - 1, IMAX(0, (itheta * (int)qn + bias) >> 14));
                        if (ctx.theta_round < 0)
                            itheta = down;
                        else
                            itheta = down + 1;
                    }
                }
                /* Entropy coding of the angle. We use a uniform pdf for the
                   time split, a step for stereo, and a triangular one for the rest. */
                if (stereo != 0 && N > 2)
                {
                    int p0 = 3;
                    int x = itheta;
                    int x0 = qn / 2;
                    uint ft = (uint)(p0 * (x0 + 1) + x0);
                    /* Use a probability of p0 up to itheta=8192 and then use 1 after */
                    if (encode != 0)
                    {
                        ec_encode(ref ec, ecbuf,
                            (uint)(x <= x0 ? p0 * x : (x - 1 - x0) + (x0 + 1) * p0),
                            (uint)(x <= x0 ? p0 * (x + 1) : (x - x0) + (x0 + 1) * p0),
                            ft);
                    }
                    else
                    {
                        int fs;
                        fs = (int)ec_decode(ref ec, ft);
                        if (fs < (x0 + 1) * p0)
                            x = fs / p0;
                        else
                            x = x0 + 1 + (fs - (x0 + 1) * p0);
                        ec_dec_update(ref ec, ecbuf,
                            (uint)(x <= x0 ? p0 * x : (x - 1 - x0) + (x0 + 1) * p0),
                            (uint)(x <= x0 ? p0 * (x + 1) : (x - x0) + (x0 + 1) * p0),
                            ft);
                        itheta = x;
                    }
                }
                else if (B0 > 1 || stereo != 0)
                {
                    /* Uniform pdf */
                    if (encode != 0)
                        ec_enc_uint(ref ec, ecbuf, (uint)itheta, (uint)qn + 1);
                    else
                        itheta = (int)ec_dec_uint(ref ec, ecbuf, (uint)qn + 1);
                }
                else
                {
                    int fs = 1, ft;
                    ft = ((qn >> 1) + 1) * ((qn >> 1) + 1);
                    if (encode != 0)
                    {
                        int fl;

                        fs = itheta <= (qn >> 1) ? itheta + 1 : qn + 1 - itheta;
                        fl = itheta <= (qn >> 1) ? itheta * (itheta + 1) >> 1 :
                         ft - ((qn + 1 - itheta) * (qn + 2 - itheta) >> 1);

                        ec_encode(ref ec, ecbuf, (uint)fl, (uint)(fl + fs), (uint)ft);
                    }
                    else
                    {
                        /* Triangular pdf */
                        int fl = 0;
                        int fm;
                        fm = (int)ec_decode(ref ec, (uint)ft);

                        if (fm < ((qn >> 1) * ((qn >> 1) + 1) >> 1))
                        {
                            itheta = (int)((isqrt32(8 * (uint)fm + 1) - 1) >> 1);
                            fs = itheta + 1;
                            fl = itheta * (itheta + 1) >> 1;
                        }
                        else
                        {
                            itheta = (int)((2 * (qn + 1)
                             - isqrt32(8 * (uint)(ft - fm - 1) + 1)) >> 1);
                            fs = qn + 1 - itheta;
                            fl = ft - ((qn + 1 - itheta) * (qn + 2 - itheta) >> 1);
                        }

                        ec_dec_update(ref ec, ecbuf, (uint)fl, (uint)(fl + fs), (uint)ft);
                    }
                }
                ASSERT(itheta >= 0);
                itheta = celt_sudiv((int)itheta * 16384, qn);
                if (encode != 0 && stereo != 0)
                {
                    if (itheta == 0)
                        intensity_stereo(m, X, Y, bandE, i, N);
                    else
                        stereo_split(X, Y, N);
                }
                /* NOTE: Renormalising X and Y *may* help fixed-point a bit at very high rate.
                         Let's do that at higher complexity */
            }
            else if (stereo != 0)
            {
                if (encode != 0)
                {
                    inv = (itheta > 8192 && ctx.disable_inv == 0) ? 1 : 0;
                    if (inv != 0)
                    {
                        int j;
                        for (j = 0; j < N; j++)
                            Y[j] = -Y[j];
                    }
                    intensity_stereo(m, X, Y, bandE, i, N);
                }
                if (*b > 2 << BITRES && ctx.remaining_bits > 2 << BITRES)
                {
                    if (encode != 0)
                        ec_enc_bit_logp(ref ec, ecbuf, inv, 2);
                    else
                        inv = ec_dec_bit_logp(ref ec, ecbuf, 2);
                }
                else
                    inv = 0;
                /* inv flag override to avoid problems with downmixing. */
                if (ctx.disable_inv != 0)
                    inv = 0;
                itheta = 0;
            }
            qalloc = (int)ec_tell_frac(ec) - tell;
            *b -= qalloc;

            if (itheta == 0)
            {
                imid = 32767;
                iside = 0;
                *fill &= (1 << B) - 1;
                delta = -16384;
            }
            else if (itheta == 16384)
            {
                imid = 0;
                iside = 32767;
                *fill &= ((1 << B) - 1) << B;
                delta = 16384;
            }
            else
            {
                imid = bitexact_cos((short)itheta);
                iside = bitexact_cos((short)(16384 - itheta));
                /* This is the mid vs side allocation that minimizes squared error
                   in that band. */
                delta = FRAC_MUL16((N - 1) << 7, bitexact_log2tan(iside, imid));
            }

            sctx.inv = inv;
            sctx.imid = imid;
            sctx.iside = iside;
            sctx.delta = delta;
            sctx.itheta = itheta;
            sctx.qalloc = qalloc;
        }

        internal static unsafe uint quant_band_n1(ref band_ctx ctx, in byte* ecbuf, float* X, float* Y,
            float* lowband_out)
        {
            int c;
            int stereo;
            float* x = X;
            int encode;
            ref ec_ctx ec = ref ctx.ec.Value;

            encode = ctx.encode;

            stereo = Y != null ? 1 : 0;
            c = 0; do
            {
                uint sign = 0;
                if (ctx.remaining_bits >= 1 << BITRES)
                {
                    if (encode != 0)
                    {
                        sign = x[0] < 0 ? 1U : 0;
                        ec_enc_bits(ref ec, ecbuf, sign, 1U);
                    }
                    else
                    {
                        sign = ec_dec_bits(ref ec, ecbuf, 1U);
                    }
                    ctx.remaining_bits -= 1 << BITRES;
                }
                if (ctx.resynth != 0)
                    x[0] = sign != 0 ? -NORM_SCALING : NORM_SCALING;
                x = Y;
            } while (++c < 1 + stereo);
            if (lowband_out != null)
                lowband_out[0] = SHR16(X[0], 4);
            return 1;
        }

        /* This function is responsible for encoding and decoding a mono partition.
           It can split the band in two and transmit the energy difference with
           the two half-bands. It can be called recursively so bands can end up being
           split in 8 parts. */
        internal static unsafe uint quant_partition(ref band_ctx ctx, in byte* ecbuf, float* X,
              int N, int b, int B, float* lowband,
              int LM,
              float gain, int fill)
        {
            ReadOnlySpan<byte> cache;
            int q;
            int curr_bits;
            int imid = 0, iside = 0;
            int B0 = B;
            float mid = 0, side = 0;
            uint cm = 0;
            float* Y = null;
            int encode;
            ref CELTMode m = ref ctx.m.Value;
            int i;
            int spread;
            ref ec_ctx ec = ref ctx.ec.Value;

            encode = ctx.encode;
            i = ctx.i;
            spread = ctx.spread;

            /* If we need 1.5 more bit than we can produce, split the band in two. */
            cache = m.cache.bits.AsSpan(m.cache.index[(LM + 1) * m.nbEBands + i]);
            if (LM != -1 && b > cache[cache[0]] + 12 && N > 2)
            {
                int mbits, sbits, delta;
                int itheta;
                int qalloc;
                split_ctx sctx = default;
                float* next_lowband2 = null;
                int rebalance;

                N >>= 1;
                Y = X + N;
                LM -= 1;
                if (B == 1)
                    fill = (fill & 1) | (fill << 1);
                B = (B + 1) >> 1;

                compute_theta(ref ctx, ref sctx, ecbuf, X, Y, N, &b, B, B0, LM, 0, &fill);
                imid = sctx.imid;
                iside = sctx.iside;
                delta = sctx.delta;
                itheta = sctx.itheta;
                qalloc = sctx.qalloc;
                mid = (1.0f / 32768) * imid;
                side = (1.0f / 32768) * iside;

                /* Give more bits to low-energy MDCTs than they would otherwise deserve */
                if (B0 > 1 && (itheta & 0x3fff) != 0)
                {
                    if (itheta > 8192)
                        /* Rough approximation for pre-echo masking */
                        delta -= delta >> (4 - LM);
                    else
                        /* Corresponds to a forward-masking slope of 1.5 dB per 10 ms */
                        delta = IMIN(0, delta + (N << BITRES >> (5 - LM)));
                }
                mbits = IMAX(0, IMIN(b, (b - delta) / 2));
                sbits = b - mbits;
                ctx.remaining_bits -= qalloc;

                if (lowband != null)
                    next_lowband2 = lowband + N; /* >32-bit split case */

                rebalance = ctx.remaining_bits;
                if (mbits >= sbits)
                {
                    cm = quant_partition(ref ctx, ecbuf, X, N, mbits, B, lowband, LM,
                          MULT16_16_P15(gain, mid), fill);
                    rebalance = mbits - (rebalance - ctx.remaining_bits);
                    if (rebalance > 3 << BITRES && itheta != 0)
                        sbits += rebalance - (3 << BITRES);
                    cm |= quant_partition(ref ctx, ecbuf, Y, N, sbits, B, next_lowband2, LM,
                          MULT16_16_P15(gain, side), fill >> B) << (B0 >> 1);
                }
                else
                {
                    cm = quant_partition(ref ctx, ecbuf, Y, N, sbits, B, next_lowband2, LM,
                          MULT16_16_P15(gain, side), fill >> B) << (B0 >> 1);
                    rebalance = sbits - (rebalance - ctx.remaining_bits);
                    if (rebalance > 3 << BITRES && itheta != 16384)
                        mbits += rebalance - (3 << BITRES);
                    cm |= quant_partition(ref ctx, ecbuf, X, N, mbits, B, lowband, LM,
                          MULT16_16_P15(gain, mid), fill);
                }
            }
            else
            {
                /* This is the basic no-split case */
                q = bits2pulses(m, i, LM, b);
                curr_bits = pulses2bits(m, i, LM, q);
                ctx.remaining_bits -= curr_bits;

                /* Ensures we can never bust the budget */
                while (ctx.remaining_bits < 0 && q > 0)
                {
                    ctx.remaining_bits += curr_bits;
                    q--;
                    curr_bits = pulses2bits(m, i, LM, q);
                    ctx.remaining_bits -= curr_bits;
                }

                if (q != 0)
                {
                    int K = get_pulses(q);

                    /* Finally do the actual quantization */
                    if (encode != 0)
                    {
                        cm = alg_quant(X, N, K, spread, B, ref ec, ecbuf, gain, ctx.resynth);
                    }
                    else
                    {
                        cm = alg_unquant(X, N, K, spread, B, ref ec, ecbuf, gain);
                    }
                }
                else
                {
                    /* If there's no pulse, fill the band anyway */
                    int j;
                    if (ctx.resynth != 0)
                    {
                        uint cm_mask;
                        /* B can be as large as 16, so this shift might overflow an int on a
                           16-bit platform; use a long to get defined behavior.*/
                        cm_mask = (uint)(1UL << B) - 1;
                        fill &= (int)cm_mask;
                        if (fill == 0)
                        {
                            OPUS_CLEAR(X, N);
                        }
                        else
                        {
                            if (lowband == null)
                            {
                                /* Noise */
                                for (j = 0; j < N; j++)
                                {
                                    ctx.seed = celt_lcg_rand(ctx.seed);
                                    X[j] = (float)((int)ctx.seed >> 20);
                                }
                                cm = cm_mask;
                            }
                            else
                            {
                                /* Folded spectrum */
                                for (j = 0; j < N; j++)
                                {
                                    float tmp;
                                    ctx.seed = celt_lcg_rand(ctx.seed);
                                    /* About 48 dB below the "normal" folding level */
                                    tmp = QCONST16(1.0f / 256, 10);
                                    tmp = ((ctx.seed) & 0x8000) != 0 ? tmp : -tmp;
                                    X[j] = lowband[j] + tmp;
                                }
                                cm = (uint)fill;
                            }
                            renormalise_vector(X, N, gain);
                        }
                    }
                }
            }

            return cm;
        }

        internal static readonly byte[] bit_interleave_table ={
            0,1,1,1,2,3,3,3,2,3,3,3,2,3,3,3
        };

        internal static readonly byte[] bit_deinterleave_table ={
               0x00,0x03,0x0C,0x0F,0x30,0x33,0x3C,0x3F,
               0xC0,0xC3,0xCC,0xCF,0xF0,0xF3,0xFC,0xFF
         };

        /* This function is responsible for encoding and decoding a band for the mono case. */
        internal static unsafe uint quant_band(ref band_ctx ctx, in byte* ecbuf, float* X,
          int N, int b, int B, float* lowband,
          int LM, float* lowband_out,
          float gain, float* lowband_scratch, int fill)
        {
            int N0 = N;
            int N_B = N;
            int N_B0;
            int B0 = B;
            int time_divide = 0;
            int recombine = 0;
            int longBlocks;
            uint cm = 0;
            int k;
            int encode;
            int tf_change;

            encode = ctx.encode;
            tf_change = ctx.tf_change;

            longBlocks = B0 == 1 ? 1 : 0;

            N_B = celt_sudiv(N_B, B);

            /* Special case for one sample */
            if (N == 1)
            {
                return quant_band_n1(ref ctx, ecbuf, X, null, lowband_out);
            }

            if (tf_change > 0)
                recombine = tf_change;
            /* Band recombining to increase frequency resolution */

            if (lowband_scratch != null &&
                lowband != null &&
                (recombine != 0 || ((N_B & 1) == 0 && tf_change < 0) || B0 > 1))
            {
                OPUS_COPY(lowband_scratch, lowband, N);
                lowband = lowband_scratch;
            }

            for (k = 0; k < recombine; k++)
            {
                if (encode != 0)
                    haar1(X, N >> k, 1 << k);
                if (lowband != null)
                    haar1(lowband, N >> k, 1 << k);
                fill = bit_interleave_table[fill & 0xF] | bit_interleave_table[fill >> 4] << 2;
            }
            B >>= recombine;
            N_B <<= recombine;

            /* Increasing the time resolution */
            while ((N_B & 1) == 0 && tf_change < 0)
            {
                if (encode != 0)
                    haar1(X, N_B, B);
                if (lowband != null)
                    haar1(lowband, N_B, B);
                fill |= fill << B;
                B <<= 1;
                N_B >>= 1;
                time_divide++;
                tf_change++;
            }
            B0 = B;
            N_B0 = N_B;

            /* Reorganize the samples in time order instead of frequency order */
            if (B0 > 1)
            {
                if (encode != 0)
                    deinterleave_hadamard(X, N_B >> recombine, B0 << recombine, longBlocks);
                if (lowband != null)
                    deinterleave_hadamard(lowband, N_B >> recombine, B0 << recombine, longBlocks);
            }

            cm = quant_partition(ref ctx, ecbuf, X, N, b, B, lowband, LM, gain, fill);

            /* This code is used by the decoder and by the resynthesis-enabled encoder */
            if (ctx.resynth != 0)
            {
                /* Undo the sample reorganization going from time order to frequency order */
                if (B0 > 1)
                    interleave_hadamard(X, N_B >> recombine, B0 << recombine, longBlocks);

                /* Undo time-freq changes that we did earlier */
                N_B = N_B0;
                B = B0;
                for (k = 0; k < time_divide; k++)
                {
                    B >>= 1;
                    N_B <<= 1;
                    cm |= cm >> B;
                    haar1(X, N_B, B);
                }

                for (k = 0; k < recombine; k++)
                {
                    cm = bit_deinterleave_table[cm];
                    haar1(X, N0 >> k, 1 << k);
                }
                B <<= recombine;

                /* Scale output for later folding */
                if (lowband_out != null)
                {
                    int j;
                    float n;
                    n = celt_sqrt(SHL32(EXTEND32(N0), 22));
                    for (j = 0; j < N0; j++)
                        lowband_out[j] = MULT16_16_Q15(n, X[j]);
                }
                cm &= (uint)((1 << B) - 1);
            }
            return cm;
        }


        /* This function is responsible for encoding and decoding a band for the stereo case. */
        internal static unsafe uint quant_band_stereo(ref band_ctx ctx, in byte* ecbuf, float* X, float* Y,
              int N, int b, int B, float* lowband,
              int LM, float* lowband_out,
              float* lowband_scratch, int fill)
        {
            int imid = 0, iside = 0;
            int inv = 0;
            float mid = 0, side = 0;
            uint cm = 0;
            int mbits, sbits, delta;
            int itheta;
            int qalloc;
            split_ctx sctx = default;
            int orig_fill;
            int encode;
            ref ec_ctx ec = ref ctx.ec.Value;

            encode = ctx.encode;

            /* Special case for one sample */
            if (N == 1)
            {
                return quant_band_n1(ref ctx, ecbuf, X, Y, lowband_out);
            }

            orig_fill = fill;

            compute_theta(ref ctx, ref sctx, ecbuf, X, Y, N, &b, B, B, LM, 1, &fill);
            inv = sctx.inv;
            imid = sctx.imid;
            iside = sctx.iside;
            delta = sctx.delta;
            itheta = sctx.itheta;
            qalloc = sctx.qalloc;
            mid = (1.0f / 32768) * imid;
            side = (1.0f / 32768) * iside;

            /* This is a special case for N=2 that only works for stereo and takes
               advantage of the fact that mid and side are orthogonal to encode
               the side with just one bit. */
            if (N == 2)
            {
                int c;
                uint sign = 0;
                float* x2;
                float* y2;
                mbits = b;
                sbits = 0;
                /* Only need one bit for the side. */
                if (itheta != 0 && itheta != 16384)
                    sbits = 1 << BITRES;
                mbits -= sbits;
                c = itheta > 8192 ? 1 : 0;
                ctx.remaining_bits -= qalloc + sbits;

                x2 = c != 0 ? Y : X;
                y2 = c != 0 ? X : Y;
                if (sbits != 0)
                {
                    if (encode != 0)
                    {
                        /* Here we only need to encode a sign for the side. */
                        sign = (x2[0] * y2[1] - x2[1] * y2[0] < 0) ? 1U : 0;
                        ec_enc_bits(ref ec, ecbuf, sign, 1);
                    }
                    else
                    {
                        sign = ec_dec_bits(ref ec, ecbuf, 1);
                    }
                }
                sign = 1 - 2 * sign;
                /* We use orig_fill here because we want to fold the side, but if
                   itheta==16384, we'll have cleared the low bits of fill. */
                cm = quant_band(ref ctx, ecbuf, x2, N, mbits, B, lowband, LM, lowband_out, Q15ONE,
                      lowband_scratch, orig_fill);
                /* We don't split N=2 bands, so cm is either 1 or 0 (for a fold-collapse),
                   and there's no need to worry about mixing with the other channel. */
                y2[0] = -sign * x2[1];
                y2[1] = sign * x2[0];
                if (ctx.resynth != 0)
                {
                    float tmp;
                    X[0] = MULT16_16_Q15(mid, X[0]);
                    X[1] = MULT16_16_Q15(mid, X[1]);
                    Y[0] = MULT16_16_Q15(side, Y[0]);
                    Y[1] = MULT16_16_Q15(side, Y[1]);
                    tmp = X[0];
                    X[0] = SUB16(tmp, Y[0]);
                    Y[0] = ADD16(tmp, Y[0]);
                    tmp = X[1];
                    X[1] = SUB16(tmp, Y[1]);
                    Y[1] = ADD16(tmp, Y[1]);
                }
            }
            else
            {
                /* "Normal" split code */
                int rebalance;

                mbits = IMAX(0, IMIN(b, (b - delta) / 2));
                sbits = b - mbits;
                ctx.remaining_bits -= qalloc;

                rebalance = ctx.remaining_bits;
                if (mbits >= sbits)
                {
                    /* In stereo mode, we do not apply a scaling to the mid because we need the normalized
                       mid for folding later. */
                    cm = quant_band(ref ctx, ecbuf, X, N, mbits, B, lowband, LM, lowband_out, Q15ONE,
                          lowband_scratch, fill);
                    rebalance = mbits - (rebalance - ctx.remaining_bits);
                    if (rebalance > 3 << BITRES && itheta != 0)
                        sbits += rebalance - (3 << BITRES);

                    /* For a stereo split, the high bits of fill are always zero, so no
                       folding will be done to the side. */
                    cm |= quant_band(ref ctx, ecbuf, Y, N, sbits, B, null, LM, null, side, null, fill >> B);
                }
                else
                {
                    /* For a stereo split, the high bits of fill are always zero, so no
                       folding will be done to the side. */
                    cm = quant_band(ref ctx, ecbuf, Y, N, sbits, B, null, LM, null, side, null, fill >> B);
                    rebalance = sbits - (rebalance - ctx.remaining_bits);
                    if (rebalance > 3 << BITRES && itheta != 16384)
                        mbits += rebalance - (3 << BITRES);
                    /* In stereo mode, we do not apply a scaling to the mid because we need the normalized
                       mid for folding later. */
                    cm |= quant_band(ref ctx, ecbuf, X, N, mbits, B, lowband, LM, lowband_out, Q15ONE,
                          lowband_scratch, fill);
                }
            }


            /* This code is used by the decoder and by the resynthesis-enabled encoder */
            if (ctx.resynth != 0)
            {
                if (N != 2)
                    stereo_merge(X, Y, mid, N);
                if (inv != 0)
                {
                    int j;
                    for (j = 0; j < N; j++)
                        Y[j] = -Y[j];
                }
            }
            return cm;
        }

        internal static unsafe void special_hybrid_folding(ref CELTMode m, float* norm, float* norm2, int start, int M, int dual_stereo)
        {
            int n1, n2;
            short[] eBands = m.eBands;
            n1 = M * (eBands[start + 1] - eBands[start]);
            n2 = M * (eBands[start + 2] - eBands[start + 1]);
            /* Duplicate enough of the first band folding data to be able to fold the second band.
               Copies no data for CELT-only mode. */
            OPUS_COPY(&norm[n1], &norm[2 * n1 - n2], n2 - n1);
            if (dual_stereo != 0)
                OPUS_COPY(&norm2[n1], &norm2[2 * n1 - n2], n2 - n1);
        }

        internal static unsafe void quant_all_bands(
            int encode, StructRef<CELTMode> celtMode, int start, int end,
              float* X_, float* Y_, byte* collapse_masks,
              in float* bandE, int* pulses, int shortBlocks, int spread,
              int dual_stereo, int intensity, int* tf_res, int total_bits,
              int balance, StructRef<ec_ctx> ec_ref, in byte* ecbuf, int LM, int codedBands,
              uint* seed, int complexity, int disable_inv)
        {
            int i;
            int remaining_bits;
            ref CELTMode m = ref celtMode.Value;
            ref ec_ctx ec = ref ec_ref.Value;
            ReadOnlySpan<short> eBands = m.eBands;
            float* norm;
            float* norm2;
            float* _norm;
            float* _lowband_scratch;
            float* X_save;
            float* Y_save;
            float* X_save2;
            float* Y_save2;
            float* norm_save2;
            int resynth_alloc;
            float* lowband_scratch;
            int B;
            int M;
            int lowband_offset;
            int update_lowband = 1;
            int C = Y_ != null ? 2 : 1;
            int norm_offset;
            int theta_rdo = (encode != 0 && Y_ != null && dual_stereo == 0 && complexity >= 8) ? 1 : 0;
            int resynth = (encode == 0 || theta_rdo != 0) ? 1 : 0;
            band_ctx ctx;

            M = 1 << LM;
            B = shortBlocks != 0 ? M : 1;
            norm_offset = M * eBands[start];
            /* No need to allocate norm for the last band because we don't need an
               output in that band. */
            _norm = new float[C * (M * eBands[m.nbEBands - 1] - norm_offset)];
            norm = _norm;
            norm2 = norm.Slice(M * eBands[m.nbEBands - 1] - norm_offset);

            /* For decoding, we can use the last band as scratch space because we don't need that
               scratch space for the last band and we don't care about the data there until we're
               decoding the last band. */
            if (encode != 0 && resynth != 0)
                resynth_alloc = M * (eBands[m.nbEBands] - eBands[m.nbEBands - 1]);
            else
                resynth_alloc = 0;
            ALLOC(_lowband_scratch, resynth_alloc, float);
            if (encode != 0 && resynth != 0)
                lowband_scratch = _lowband_scratch;
            else
                lowband_scratch = X_ + M * eBands[m.effEBands - 1];
            ALLOC(X_save, resynth_alloc, float);
            ALLOC(Y_save, resynth_alloc, float);
            ALLOC(X_save2, resynth_alloc, float);
            ALLOC(Y_save2, resynth_alloc, float);
            ALLOC(norm_save2, resynth_alloc, float);

            lowband_offset = 0;
            ctx.bandE = bandE;
            ctx.ec = ec_ref;
            ctx.encode = encode;
            ctx.intensity = intensity;
            ctx.m = celtMode;
            ctx.seed = *seed;
            ctx.spread = spread;
            ctx.disable_inv = disable_inv;
            ctx.resynth = resynth;
            ctx.theta_round = 0;
            /* Avoid injecting noise in the first band on transients. */
            ctx.avoid_split_noise = B > 1 ? 1 : 0;
            for (i = start; i < end; i++)
            {
                int tell;
                int b;
                int N;
                int curr_balance;
                int effective_lowband = -1;
                float* X;
                float* Y;
                int tf_change = 0;
                uint x_cm;
                uint y_cm;
                int last;

                ctx.i = i;
                last = (i == end - 1) ? 1 : 0;

                X = X_ + M * eBands[i];
                if (Y_ != null)
                    Y = Y_ + M * eBands[i];
                else
                    Y = null;
                N = M * eBands[i + 1] - M * eBands[i];
                ASSERT(N > 0);
                tell = (int)ec_tell_frac(ec);

                /* Compute how many bits we want to allocate to this band */
                if (i != start)
                    balance -= tell;
                remaining_bits = total_bits - tell - 1;
                ctx.remaining_bits = remaining_bits;
                if (i <= codedBands - 1)
                {
                    curr_balance = celt_sudiv(balance, IMIN(3, codedBands - i));
                    b = IMAX(0, IMIN(16383, IMIN(remaining_bits + 1, pulses[i] + curr_balance)));
                }
                else
                {
                    b = 0;
                }

                if (resynth != 0 && (M * eBands[i] - N >= M * eBands[start] || i == start + 1) && (update_lowband != 0 || lowband_offset == 0))
                    lowband_offset = i;
                if (i == start + 1)
                    special_hybrid_folding(ref m, norm, norm2, start, M, dual_stereo);

                tf_change = tf_res[i];
                ctx.tf_change = tf_change;
                if (i >= m.effEBands)
                {
                    X = norm;
                    if (Y_ != null)
                        Y = norm;
                    lowband_scratch = null;
                }
                if (last != 0 && theta_rdo == 0)
                    lowband_scratch = null;

                /* Get a conservative estimate of the collapse_mask's for the bands we're
                   going to be folding from. */
                if (lowband_offset != 0 && (spread != SPREAD_AGGRESSIVE || B > 1 || tf_change < 0))
                {
                    int fold_start;
                    int fold_end;
                    int fold_i;
                    /* This ensures we never repeat spectral content within one band */
                    effective_lowband = IMAX(0, M * eBands[lowband_offset] - norm_offset - N);
                    fold_start = lowband_offset;
                    while (M * eBands[--fold_start] > effective_lowband + norm_offset) ;
                    fold_end = lowband_offset - 1;
                    while (++fold_end < i && M * eBands[fold_end] < effective_lowband + norm_offset + N) ;

                    x_cm = y_cm = 0;
                    fold_i = fold_start; do
                    {
                        x_cm |= collapse_masks[fold_i * C + 0];
                        y_cm |= collapse_masks[fold_i * C + C - 1];
                    } while (++fold_i < fold_end);
                }
                /* Otherwise, we'll be using the LCG to fold, so all blocks will (almost
                   always) be non-zero. */
                else
                    x_cm = y_cm = (uint)((1 << B) - 1);

                if (dual_stereo != 0 && i == intensity)
                {
                    int j;

                    /* Switch off dual stereo to do intensity. */
                    dual_stereo = 0;
                    if (resynth != 0)
                        for (j = 0; j < M * eBands[i] - norm_offset; j++)
                            norm[j] = HALF32(norm[j] + norm2[j]);
                }
                if (dual_stereo != 0)
                {
                    x_cm = quant_band(ref ctx, ecbuf, X, N, b / 2, B,
                          effective_lowband != -1 ? norm + effective_lowband : null, LM,
                          last != 0 ? null : norm + M * eBands[i] - norm_offset, Q15ONE, lowband_scratch, (int)x_cm);
                    y_cm = quant_band(ref ctx, ecbuf, Y, N, b / 2, B,
                          effective_lowband != -1 ? norm2 + effective_lowband : null, LM,
                          last != 0 ? null : norm2 + M * eBands[i] - norm_offset, Q15ONE, lowband_scratch, (int)y_cm);
                }
                else
                {
                    if (Y != null)
                    {
                        if (theta_rdo != 0 && i < intensity)
                        {
                            ec_ctx ec_save, ec_save2;
                            band_ctx ctx_save, ctx_save2;
                            float dist0, dist1;
                            uint cm, cm2;
                            int nstart_bytes, nend_bytes, save_bytes;
                            byte* bytes_buf;
                            byte[] bytes_save = new byte[1275];
                            float w_0 = 0;
                            float w_1 = 0;
                            compute_channel_weights(bandE[i], bandE[i + m.nbEBands], ref w_0, ref w_1);
                            /* Make a copy. */
                            cm = x_cm | y_cm;
                            ec_save = ec;
                            ctx_save = ctx;
                            OPUS_COPY(X_save, X, N);
                            OPUS_COPY(Y_save, Y, N);
                            /* Encode and round down. */
                            ctx.theta_round = -1;
                            x_cm = quant_band_stereo(ref ctx, ecbuf, X, Y, N, b, B,
                                  effective_lowband != -1 ? norm + effective_lowband : null, LM,
                                  last != 0 ? null : norm + M * eBands[i] - norm_offset, lowband_scratch, (int)cm);
                            dist0 = MULT16_32_Q15(w_0, celt_inner_prod(X_save, X, N)) + MULT16_32_Q15(w_1, celt_inner_prod(Y_save, Y, N));

                            /* Save first result. */
                            cm2 = x_cm;
                            ec_save2 = ec;
                            ctx_save2 = ctx;
                            OPUS_COPY(X_save2, X, N);
                            OPUS_COPY(Y_save2, Y, N);
                            if (last == 0)
                                OPUS_COPY(norm_save2, norm + M * eBands[i] - norm_offset, N);
                            nstart_bytes = (int)ec_save.offs;
                            nend_bytes = (int)ec_save.storage;
                            bytes_buf = ec_save.buf + nstart_bytes;
                            save_bytes = nend_bytes - nstart_bytes;
                            OPUS_COPY(bytes_save, bytes_buf, save_bytes);

                            /* Restore */
                            ec = ec_save;
                            ctx = ctx_save;
                            OPUS_COPY(X, X_save, N);
                            OPUS_COPY(Y, Y_save, N);
                            if (i == start + 1)
                                special_hybrid_folding(ref m, norm, norm2, start, M, dual_stereo);
                            /* Encode and round up. */
                            ctx.theta_round = 1;
                            x_cm = quant_band_stereo(ref ctx, X, Y, N, b, B,
                                  effective_lowband != -1 ? norm + effective_lowband : null, LM,
                                  last != 0 ? null : norm + M * eBands[i] - norm_offset, lowband_scratch, cm);
                            dist1 = MULT16_32_Q15(w_0, celt_inner_prod(X_save, X, N)) + MULT16_32_Q15(w_1, celt_inner_prod(Y_save, Y, N));
                            if (dist0 >= dist1)
                            {
                                x_cm = cm2;
                                ec = ec_save2;
                                ctx = ctx_save2;
                                OPUS_COPY(X, X_save2, N);
                                OPUS_COPY(Y, Y_save2, N);
                                if (last == 0)
                                    OPUS_COPY(norm + M * eBands[i] - norm_offset, norm_save2, N);
                                OPUS_COPY(bytes_buf, bytes_save, save_bytes);
                            }
                        }
                        else
                        {
                            ctx.theta_round = 0;
                            x_cm = quant_band_stereo(ref ctx, ecbuf, X, Y, N, b, B,
                                  effective_lowband != -1 ? norm + effective_lowband : null, LM,
                                  last != 0 ? null : norm + M * eBands[i] - norm_offset, lowband_scratch, (int)(x_cm | y_cm));
                        }
                    }
                    else
                    {
                        x_cm = quant_band(ref ctx, ecbuf, X, N, b, B,
                              effective_lowband != -1 ? norm + effective_lowband : null, LM,
                              last != 0 ? null : norm + M * eBands[i] - norm_offset, Q15ONE, lowband_scratch, (int)(x_cm | y_cm));
                    }
                    y_cm = x_cm;
                }
                collapse_masks[i * C + 0] = (byte)x_cm;
                collapse_masks[i * C + C - 1] = (byte)y_cm;
                balance += pulses[i] + tell;

                /* Update the folding position only as long as we have 1 bit/sample depth. */
                update_lowband = b > (N << BITRES) ? 1 : 0;
                /* We only need to avoid noise on a split for the first band. After that, we
                   have folding. */
                ctx.avoid_split_noise = 0;
            }

            *seed = ctx.seed;
        }
    }
}
