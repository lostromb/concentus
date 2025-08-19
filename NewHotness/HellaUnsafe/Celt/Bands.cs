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

using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.CELTModeH;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Celt.QuantBands;
using static HellaUnsafe.Celt.VQ;
using System.Runtime.CompilerServices;
using HellaUnsafe.Common;

namespace HellaUnsafe.Celt
{
    internal static unsafe class Bands
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

        internal static unsafe uint celt_lcg_rand(uint seed)
        {
            return 1664525 * seed + 1013904223;
        }

        /* This is a cos() approximation designed to be bit-exact on any platform. Bit exactness
           with this approximation is important because it has an impact on the bit allocation */
        internal static unsafe int bitexact_cos(short x)
        {
            int tmp;
            short x2;
            tmp = (4096 + ((int)(x) * (x))) >> 13;
            ASSERT(tmp <= 32767);
            x2 = (short)tmp;
            x2 = (short)((32767 - x2) + FRAC_MUL16(x2, (-7651 + FRAC_MUL16(x2, (8277 + FRAC_MUL16(-626, x2))))));
            ASSERT(x2 <= 32766);
            return 1 + x2;
        }

        internal static unsafe int bitexact_log2tan(int isin, int icos)
        {
            int lc;
            int ls;
            lc = EC_ILOG(icos);
            ls = EC_ILOG(isin);
            icos <<= 15 - lc;
            isin <<= 15 - ls;
            return (ls - lc) * (1 << 11)
                  + FRAC_MUL16(isin, FRAC_MUL16(isin, -2597) + 7932)
                  - FRAC_MUL16(icos, FRAC_MUL16(icos, -2597) + 7932);
        }

        /* Compute the amplitude (sqrt energy) in each of the bands */
        internal static unsafe void compute_band_energies(in OpusCustomMode* m, in float* X, float* bandE, int end, int C, int LM)
        {
            int i, c, N;
            short* eBands = m->eBands;
            N = m->shortMdctSize << LM;
            c = 0; do
            {
                for (i = 0; i < end; i++)
                {
                    float sum;
                    sum = 1e-27f + celt_inner_prod(&X[c * N + (eBands[i] << LM)], &X[c * N + (eBands[i] << LM)], (eBands[i + 1] - eBands[i]) << LM);
                    bandE[i + c * m->nbEBands] = celt_sqrt(sum);
                    /*printf ("%f ", bandE[i+c*m->nbEBands]);*/
                }
            } while (++c < C);
            /*printf ("\n");*/
        }

        /* Normalise each band such that the energy is one. */
        internal static unsafe void normalise_bands(in OpusCustomMode* m, in float* freq, float* X, in float* bandE, int end, int C, int M)
        {
            int i, c, N;
            short* eBands = m->eBands;
            N = M * m->shortMdctSize;
            c = 0; do
            {
                for (i = 0; i < end; i++)
                {
                    int j;
                    float g = 1.0f / (1e-27f + bandE[i + c * m->nbEBands]);
                    for (j = M * eBands[i]; j < M * eBands[i + 1]; j++)
                        X[j + c * N] = freq[j + c * N] * g;
                }
            } while (++c < C);
        }

        /* De-normalise the energy to produce the synthesis from the unit-energy bands */
        internal static unsafe void denormalise_bands(in OpusCustomMode* m, in float* X,
              float* freq, in float* bandLogE, int start,
              int end, int M, int downsample, int silence)
        {
            int i, N;
            int bound;
            float* f;
            float* x;
            short* eBands = m->eBands;
            N = M * m->shortMdctSize;
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
                j = M * eBands[i];
                band_end = M * eBands[i + 1];
                lg = SATURATE16(ADD32(bandLogE[i], SHL32((float)eMeans[i], 6)));
                g = celt_exp2(MIN32(32.0f, lg));

                /* Be careful of the fixed-point "else" just above when changing this code */
                do
                {
                    *f++ = MULT16_16(*x++, g);
                } while (++j < band_end);
            }
            ASSERT(start <= end);
            OPUS_CLEAR(&freq[bound], N - bound);
        }

        /* This prevents energy collapse for transients with multiple short MDCTs */
        internal static unsafe void anti_collapse(
            in OpusCustomMode* m, float* X_, byte* collapse_masks, int LM, int C, int size,
            int start, int end, in float* logE, in float* prev1logE,
            in float* prev2logE, in int* pulses, uint seed)
        {
            int c, i, j, k;
            for (i = start; i < end; i++)
            {
                int N0;
                float thresh, sqrt_1;
                int depth;

                N0 = m->eBands[i + 1] - m->eBands[i];
                /* depth in 1/8 bits */
                ASSERT(pulses[i] >= 0);
                depth = (int)celt_udiv((uint)(1 + pulses[i]), (uint)(m->eBands[i + 1] - m->eBands[i])) >> LM;

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
                    prev1 = prev1logE[c * m->nbEBands + i];
                    prev2 = prev2logE[c * m->nbEBands + i];
                    if (C == 1)
                    {
                        prev1 = MAX16(prev1, prev1logE[m->nbEBands + i]);
                        prev2 = MAX16(prev2, prev2logE[m->nbEBands + i]);
                    }
                    Ediff = EXTEND32(logE[c * m->nbEBands + i]) - EXTEND32(MIN16(prev1, prev2));
                    Ediff = MAX32(0, Ediff);

                    /* r needs to be multiplied by 2 or 2*sqrt(2) depending on LM because
                       short blocks don't have the same energy as long */
                    r = 2.0f * celt_exp2(-Ediff);
                    if (LM == 3)
                        r *= 1.41421356f;
                    r = MIN16(thresh, r);
                    r = r * sqrt_1;
                    X = X_ + c * size + (m->eBands[i] << LM);
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
        internal static unsafe void compute_channel_weights(float Ex, float Ey, float* w)
        {
            float minE;
            minE = MIN32(Ex, Ey);
            /* Adjustment to make the weights a bit more conservative. */
            Ex = ADD32(Ex, minE / 3);
            Ey = ADD32(Ey, minE / 3);
            w[0] = Ex;
            w[1] = Ey;
        }

        internal static unsafe void intensity_stereo(in OpusCustomMode* m, float* X, in float* Y, in float* bandE, int bandID, int N)
        {
            int i = bandID;
            int j;
            float a1, a2;
            float left, right;
            float norm;
            left = bandE[i];
            right = bandE[i + m->nbEBands];
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

            t = El;
            lgain = celt_rsqrt_norm(t);
            t = Er;
            rgain = celt_rsqrt_norm(t);

            for (j = 0; j < N; j++)
            {
                float r, l;
                /* Apply mid scaling (side is already scaled) */
                l = MULT16_16_P15(mid, X[j]);
                r = Y[j];
                X[j] = MULT16_16(lgain, SUB16(l, r));
                Y[j] = MULT16_16(rgain, ADD16(l, r));
            }
        }

        /* Decide whether we should spread the pulses in the current frame */
        internal static unsafe int spreading_decision(in OpusCustomMode* m, in float* X, int* average,
          int last_decision, int* hf_average, int* tapset_decision, int update_hf,
          int end, int C, int M, in int* spread_weight)
        {
            int i, c, N0;
            int sum = 0, nbBands = 0;
            short* eBands = m->eBands;
            int decision;
            int hf_sum = 0;

            ASSERT(end > 0);

            N0 = M * m->shortMdctSize;

            if (M * (eBands[end] - eBands[end - 1]) <= 8)
                return SPREAD_NONE;

            int* tcount = stackalloc int[3];

            c = 0; do
            {
                for (i = 0; i < end; i++)
                {
                    int j, N, tmp = 0;
                    Unsafe.InitBlock(tcount, 0, sizeof(int) * 3);
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
                    if (i > m->nbEBands - 4)
                        hf_sum += (int)celt_udiv((uint)(32 * (tcount[1] + tcount[0])), (uint)N);
                    tmp = (2 * BOOL2INT(tcount[2] >= N)) + (2 * BOOL2INT(tcount[1] >= N)) + (2 * BOOL2INT(tcount[0] >= N));
                    sum += tmp * spread_weight[i];
                    nbBands += spread_weight[i];
                }
            } while (++c < C);

            if (update_hf != 0)
            {
                if (hf_sum != 0)
                    hf_sum = celt_udiv(hf_sum, C * (4 - m->nbEBands + end));
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
            sum = celt_udiv((int)sum << 8, nbBands);
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
#if FUZZING
            decision = rand() & 0x3;
            *tapset_decision = rand() % 3;
#endif
            return decision;
        }

        /* Indexing table for converting from natural Hadamard to ordery Hadamard
           This is essentially a bit-reversed Gray, on top of which we've added
           an inversion of the order because we want the DC at the end rather than
           the beginning. The lines are for N=2, 4, 8, 16 */
        internal static readonly int* ordery_table = AllocateGlobalArray(new int[] {
               1,  0,
               3,  0,  2,  1,
               7,  0,  4,  3,  6,  1,  5,  2,
              15,  0,  8,  7, 12,  3, 11,  4, 14,  1,  9,  6, 13,  2, 10,  5,
        });

        static void deinterleave_hadamard(float* X, int N0, int stride, int hadamard)
        {
            int i, j;
            int N;
            N = N0 * stride;
            float* tmp = stackalloc float[N];
            ASSERT(stride > 0);
            if (hadamard != 0)
            {
                int* ordery = ordery_table + stride - 2;
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
            int N;
            N = N0 * stride;
            float* tmp = stackalloc float[N];
            if (hadamard != 0)
            {
                int* ordery = ordery_table + stride - 2;
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

        static readonly short[] exp2_table8/*[8]*/ =
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
    }
}
