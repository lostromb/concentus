/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2010 Xiph.Org Foundation
   Copyright (c) 2008 Gregory Maxwell
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

using System;
using HellaUnsafe.Common;
using static System.Math;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.Bands;
using static HellaUnsafe.Celt.Celt;
using static HellaUnsafe.Celt.CeltLPC;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.EntEnc;
using static HellaUnsafe.Celt.EntDec;
using static HellaUnsafe.Celt.Laplace;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Celt.MDCT;
using static HellaUnsafe.Celt.Modes;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Celt.Rate;
using static HellaUnsafe.Celt.QuantBands;
using static HellaUnsafe.Celt.VQ;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.OpusDefines;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;

namespace HellaUnsafe.Celt
{
    internal static class CeltEncoder
    {
        internal unsafe struct CeltCustomEncoder
        {
            internal StructRef<CeltCustomMode> mode;     /**< Mode used by the encoder */
            internal int channels;
            internal int stream_channels;

            internal int force_intra;
            internal int clip;
            internal int disable_pf;
            internal int complexity;
            internal int upsample;
            internal int start, end;

            internal int bitrate;
            internal int vbr;
            internal int signalling;
            internal int constrained_vbr;      /* If zero, VBR can do whatever it likes with the rate */
            internal int loss_rate;
            internal int lsb_depth;
            internal int lfe;
            internal int disable_inv;

            /* Everything beyond this point gets cleared on a reset */
            internal uint rng;
            internal int spread_decision;
            internal float delayedIntra;
            internal int tonal_average;
            internal int lastCodedBands;
            internal int hf_average;
            internal int tapset_decision;

            internal int prefilter_period;
            internal float prefilter_gain;
            internal int prefilter_tapset;
            internal int consec_transient;
            internal StructRef<AnalysisInfo> analysis; // Embedded struct: never null!
            internal StructRef<SILKInfo> silk_info; // Embedded struct: never null!

            internal fixed float preemph_memE[2];
            internal fixed float preemph_memD[2];

            /* VBR-related parameters */
            internal int vbr_reservoir;
            internal int vbr_drift;
            internal int vbr_offset;
            internal int vbr_count;
            internal float overlap_max;
            internal float stereo_saving;
            internal int intensity;
            internal float[] energy_mask;
            internal float spec_avg;

            // Like the decoder, there's a big empty array scratch space following the
            // struct here which we represent in C# using a heap array allocated during init().
            internal float[] in_mem; /* Size = channels*mode.overlap */
            /* float prefilter_mem[],  Size = channels*COMBFILTER_MAXPERIOD */
            /* float oldBandE[],     Size = channels*mode.nbEBands */
            /* float oldLogE[],      Size = channels*mode.nbEBands */
            /* float oldLogE2[],     Size = channels*mode.nbEBands */
            /* float energyError[],  Size = channels*mode.nbEBands */
        };

        /// <summary>
        /// Gets the size of the in_mem array for a given encoder mode
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="channels"></param>
        /// <returns></returns>
        internal static int opus_custom_encoder_get_memory_size(in CeltCustomMode mode, int channels)
        {
            return (channels * mode.overlap)    /* float in_mem[channels*mode.overlap]; */
                     + (channels * COMBFILTER_MAXPERIOD) /* float prefilter_mem[channels*COMBFILTER_MAXPERIOD]; */
                     + (4 * channels * mode.nbEBands);  /* float oldBandE[channels*mode.nbEBands]; */
            /* float oldLogE[channels*mode.nbEBands]; */
            /* float oldLogE2[channels*mode.nbEBands]; */
            /* float energyError[channels*mode.nbEBands]; */
        }

        internal static unsafe int opus_custom_encoder_init_arch(ref CeltCustomEncoder st, StructRef<CeltCustomMode> mode,
          int channels)
        {
            if (channels < 0 || channels > 2)
                return OPUS_BAD_ARG;

            st = default;
            //OPUS_CLEAR((char*)st, opus_custom_encoder_get_size(mode, channels));

            st.mode = mode;
            st.stream_channels = st.channels = channels;

            st.upsample = 1;
            st.start = 0;
            st.end = st.mode.Value.effEBands;
            st.signalling = 1;

            st.constrained_vbr = 1;
            st.clip = 1;

            st.bitrate = OPUS_BITRATE_MAX;
            st.vbr = 0;
            st.force_intra = 0;
            st.complexity = 5;
            st.lsb_depth = 24;
            st.analysis = new StructRef<AnalysisInfo>();
            st.silk_info = new StructRef<SILKInfo>();
            st.in_mem = new float[opus_custom_encoder_get_memory_size(mode.Value, channels)];

            opus_custom_encoder_ctl(ref st, OPUS_RESET_STATE);

            return OPUS_OK;
        }

        internal static int celt_encoder_init(ref CeltCustomEncoder st, int sampling_rate, int channels)
        {
            int ret;
            int error;
            ret = opus_custom_encoder_init_arch(ref st,
                    opus_custom_mode_create(48000, 960, out error), channels);
            if (ret != OPUS_OK)
                return ret;
            st.upsample = resampling_factor(sampling_rate);
            return OPUS_OK;
        }

        /* Table of 6*64/x, trained on real data to minimize the average error */
        private static readonly byte[] inv_table = {
         255,255,156,110, 86, 70, 59, 51, 45, 40, 37, 33, 31, 28, 26, 25,
          23, 22, 21, 20, 19, 18, 17, 16, 16, 15, 15, 14, 13, 13, 12, 12,
          12, 12, 11, 11, 11, 10, 10, 10,  9,  9,  9,  9,  9,  9,  8,  8,
           8,  8,  8,  7,  7,  7,  7,  7,  7,  6,  6,  6,  6,  6,  6,  6,
           6,  6,  6,  6,  6,  6,  6,  6,  6,  5,  5,  5,  5,  5,  5,  5,
           5,  5,  5,  5,  5,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,
           4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  3,  3,
           3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  2,
        };

        internal static unsafe int transient_analysis(in float* input, int len, int C,
            float* tf_estimate, int* tf_chan, int allow_weak_transients,
            int* weak_transient)
        {
            int i;
            float mem0, mem1;
            int is_transient = 0;
            int mask_metric = 0;
            int c;
            float tf_max;
            int len2;
            /* Forward masking: 6.7 dB/ms. */
            float forward_decay = QCONST16(.0625f, 15);
            Span<float> tmp = new float[len];

            *weak_transient = 0;
            /* For lower bitrates, let's be more conservative and have a forward masking
               decay of 3.3 dB/ms. This avoids having to code transients at very low
               bitrate (mostly for hybrid), which can result in unstable energy and/or
               partial collapse. */
            if (allow_weak_transients != 0)
            {
                forward_decay = QCONST16(.03125f, 15);
            }
            len2 = len / 2;
            for (c = 0; c < C; c++)
            {
                float mean;
                int unmask = 0;
                float norm;
                float maxE;
                mem0 = 0;
                mem1 = 0;
                /* High-pass filter: (1 - 2*z^-1 + z^-2) / (1 - z^-1 + .5*z^-2) */
                for (i = 0; i < len; i++)
                {
                    float mem00;
                    float x, y;
                    x = SHR32(input[i + c * len], 0);
                    y = ADD32(mem0, x);
                    /* Original code:
                    mem0 = mem1 + y - 2*x;
                    mem1 = x - .5f*y;
                    Modified code to shorten dependency chains: */
                    mem00 = mem0;
                    mem0 = mem0 - x + .5f * mem1;
                    mem1 = x - mem00;
                    tmp[i] = SROUND16(y, 2);
                    /*printf("%f ", tmp[i]);*/
                }
                /*printf("\n");*/
                /* First few samples are bad because we don't propagate the memory */
                tmp.Slice(0, 12).Clear();

                mean = 0;
                mem0 = 0;
                /* Grouping by two to reduce complexity */
                /* Forward pass to compute the post-echo threshold*/
                for (i = 0; i < len2; i++)
                {
                    float x2 = PSHR32(MULT16_16(tmp[2 * i], tmp[2 * i]) + MULT16_16(tmp[2 * i + 1], tmp[2 * i + 1]), 16);
                    mean += x2;
                    mem0 = x2 + (1.0f - forward_decay) * mem0;
                    tmp[i] = forward_decay * mem0;
                }

                mem0 = 0;
                maxE = 0;
                /* Backward pass to compute the pre-echo threshold */
                for (i = len2 - 1; i >= 0; i--)
                {
                    /* Backward masking: 13.9 dB/ms. */
                    mem0 = tmp[i] + 0.875f * mem0;
                    tmp[i] = 0.125f * mem0;
                    maxE = MAX16(maxE, 0.125f * mem0);
                }
                /*for (i=0;i<len2;i++)printf("%f ", tmp[i]/mean);printf("\n");*/

                /* Compute the ratio of the "frame energy" over the harmonic mean of the energy.
                   This essentially corresponds to a bitrate-normalized temporal noise-to-mask
                   ratio */

                /* As a compromise with the old transient detector, frame energy is the
                   geometric mean of the energy and half the max */
                mean = celt_sqrt(mean * maxE * .5f * len2);
                /* Inverse of the mean energy in Q15+6 */
                norm = SHL32(EXTEND32(len2), 6 + 14) / ADD32(EPSILON, SHR32(mean, 1));
                /* Compute harmonic mean discarding the unreliable boundaries
                   The data is smooth, so we only take 1/4th of the samples */
                unmask = 0;
                /* We should never see NaNs here. If we find any, then something really bad happened and we better abort
                   before it does any damage later on. If these asserts are disabled (no hardening), then the table
                   lookup a few lines below (id = ...) is likely to crash dur to an out-of-bounds read. DO NOT FIX
                   that crash on NaN since it could result in a worse issue later on. */
                ASSERT(celt_isnan(tmp[0]) == 0);
                ASSERT(celt_isnan(norm) == 0);
                for (i = 12; i < len2 - 5; i += 4)
                {
                    int id;
                    id = (int)MAX32(0, MIN32(127, (float)Floor(64 * norm * (tmp[i] + EPSILON)))); /* Do not round to nearest */
                    unmask += inv_table[id];
                }
                /*printf("%d\n", unmask);*/
                /* Normalize, compensate for the 1/4th of the sample and the factor of 6 in the inverse table */
                unmask = 64 * unmask * 4 / (6 * (len2 - 17));
                if (unmask > mask_metric)
                {
                    *tf_chan = c;
                    mask_metric = unmask;
                }
            }
            is_transient = mask_metric > 200 ? 1 : 0;
            /* For low bitrates, define "weak transients" that need to be
               handled differently to avoid partial collapse. */
            if (allow_weak_transients != 0 && is_transient != 0 && mask_metric < 600)
            {
                is_transient = 0;
                *weak_transient = 1;
            }
            /* Arbitrary metric for VBR boost */
            tf_max = MAX16(0, celt_sqrt(27 * mask_metric) - 42);
            /* *tf_estimate = 1 + MIN16(1, sqrt(MAX16(0, tf_max-30))/20); */
            *tf_estimate = celt_sqrt(MAX32(0, SHL32(MULT16_16(QCONST16(0.0069f, 14), MIN16(163, tf_max)), 14) - QCONST32(0.139f, 28)));
            /*printf("%d %f\n", tf_max, mask_metric);*/
            /*printf("%d %f %d\n", is_transient, (float)*tf_estimate, tf_max);*/
            return is_transient;
        }

        /* Looks for sudden increases of energy to decide whether we need to patch
           the transient decision */
        internal static unsafe int patch_transient_decision(float* newE, float* oldE, int nbEBands,
              int start, int end, int C)
        {
            int i, c;
            float mean_diff = 0;
            Span<float> spread_old = stackalloc float[26];
            /* Apply an aggressive (-6 dB/Bark) spreading function to the old frame to
               avoid false detection caused by irrelevant bands */
            if (C == 1)
            {
                spread_old[start] = oldE[start];
                for (i = start + 1; i < end; i++)
                    spread_old[i] = MAX16(spread_old[i - 1] - QCONST16(1.0f, DB_SHIFT), oldE[i]);
            }
            else
            {
                spread_old[start] = MAX16(oldE[start], oldE[start + nbEBands]);
                for (i = start + 1; i < end; i++)
                    spread_old[i] = MAX16(spread_old[i - 1] - QCONST16(1.0f, DB_SHIFT),
                                          MAX16(oldE[i], oldE[i + nbEBands]));
            }
            for (i = end - 2; i >= start; i--)
                spread_old[i] = MAX16(spread_old[i], spread_old[i + 1] - QCONST16(1.0f, DB_SHIFT));
            /* Compute mean increase */
            c = 0; do
            {
                for (i = IMAX(2, start); i < end - 1; i++)
                {
                    float x1, x2;
                    x1 = MAX16(0, newE[i + c * nbEBands]);
                    x2 = MAX16(0, spread_old[i]);
                    mean_diff = ADD32(mean_diff, EXTEND32(MAX16(0, SUB16(x1, x2))));
                }
            } while (++c < C);
            mean_diff = DIV32(mean_diff, C * (end - 1 - IMAX(2, start)));
            /*printf("%f %f %d\n", mean_diff, max_diff, count);*/
            return mean_diff > QCONST16(1.0f, DB_SHIFT) ? 1 : 0;
        }

        /** Apply window and compute the MDCT for all sub-frames and
            all channels in a frame */
        internal static unsafe void compute_mdcts(in CeltCustomMode mode, int shortBlocks, float* input,
                          float* output, int C, int CC, int LM, int upsample)
        {
            int overlap = mode.overlap;
            int N;
            int B;
            int shift;
            int i, b, c;
            if (shortBlocks != 0)
            {
                B = shortBlocks;
                N = mode.shortMdctSize;
                shift = mode.maxLM;
            }
            else
            {
                B = 1;
                N = mode.shortMdctSize << LM;
                shift = mode.maxLM - LM;
            }

            fixed (float* modewindow = mode.window)
            {
                c = 0; do
                {
                    for (b = 0; b < B; b++)
                    {
                        /* Interleaving the sub-frames while doing the MDCTs */
                        clt_mdct_forward(mode.mdct, input + c * (B * N + overlap) + b * N,
                                         &output[b + c * N * B], modewindow, overlap, shift, B);
                    }
                } while (++c < CC);
            }

            if (CC == 2 && C == 1)
            {
                for (i = 0; i < B * N; i++)
                    output[i] = ADD32(HALF32(output[i]), HALF32(output[B * N + i]));
            }
            if (upsample != 1)
            {
                c = 0; do
                {
                    int bound = B * N / upsample;
                    for (i = 0; i < bound; i++)
                        output[c * B * N + i] *= upsample;
                    OPUS_CLEAR(&output[c * B * N + bound], B * N - bound);
                } while (++c < C);
            }
        }


        internal static unsafe void celt_preemphasis(in float* pcmp, float* inp,
            int N, int CC, int upsample, in float* coef, float* mem, int clip)
        {
            int i;
            float coef0;
            float m;
            int Nu;

            coef0 = coef[0];
            m = *mem;

            /* Fast path for the normal 48kHz case and no clipping */
            if (coef[1] == 0 && upsample == 1 && clip == 0)
            {
                for (i = 0; i < N; i++)
                {
                    float x;
                    x = SCALEIN(pcmp[CC * i]);
                    /* Apply pre-emphasis */
                    inp[i] = SHL32(x, SIG_SHIFT) - m;
                    m = SHR32(MULT16_16(coef0, x), 15 - SIG_SHIFT);
                }
                *mem = m;
                return;
            }

            Nu = N / upsample;
            if (upsample != 1)
            {
                OPUS_CLEAR(inp, N);
            }
            for (i = 0; i < Nu; i++)
                inp[i * upsample] = SCALEIN(pcmp[CC * i]);

            if (clip != 0)
            {
                /* Clip input to avoid encoding non-portable files */
                for (i = 0; i < Nu; i++)
                    inp[i * upsample] = MAX32(-65536.0f, MIN32(65536.0f, inp[i * upsample]));
            }

            for (i = 0; i < N; i++)
            {
                float x;
                x = inp[i];
                /* Apply pre-emphasis */
                inp[i] = SHL32(x, SIG_SHIFT) - m;
                m = SHR32(MULT16_16(coef0, x), 15 - SIG_SHIFT);
            }
            *mem = m;
        }



        internal static unsafe float l1_metric(in float* tmp, int N, int LM, float bias)
        {
            int i;
            float L1;
            L1 = 0;
            for (i = 0; i < N; i++)
                L1 += EXTEND32(ABS16(tmp[i]));
            /* When in doubt, prefer good freq resolution */
            L1 = MAC16_32_Q15(L1, LM * bias, L1);
            return L1;

        }

        internal static unsafe int tf_analysis(in CeltCustomMode m, int len, int isTransient,
              int* tf_res, int lambda, float* X, int N0, int LM,
              float tf_estimate, int tf_chan, int* importance)
        {
            int i;
            int cost0;
            int cost1;
            int sel;
            int tf_select = 0;
            float bias;

            bias = MULT16_16_Q14(QCONST16(.04f, 15), MAX16(-QCONST16(.25f, 14), QCONST16(.5f, 14) - tf_estimate));
            /*printf("%f ", bias);*/

            Span<int> selcost = stackalloc int[2];
            Span<int> metric_array = new int[len];
            Span<int> path0_array = new int[len];
            Span<int> path1_array = new int[len];
            Span<float> tmp_array = new float[(m.eBands[len] - m.eBands[len - 1]) << LM];
            Span<float> tmp_1_array = new float[(m.eBands[len] - m.eBands[len - 1]) << LM];

            fixed (int* metric = metric_array)
            fixed (int* path0 = path0_array)
            fixed (int* path1 = path1_array)
            fixed (float* tmp = tmp_array)
            fixed (float* tmp_1 = tmp_1_array)
            {

                for (i = 0; i < len; i++)
                {
                    int k, N;
                    int narrow;
                    float L1, best_L1;
                    int best_level = 0;
                    N = (m.eBands[i + 1] - m.eBands[i]) << LM;
                    /* band is too narrow to be split down to LM=-1 */
                    narrow = (m.eBands[i + 1] - m.eBands[i]) == 1 ? 1 : 0;
                    OPUS_COPY(tmp, &X[tf_chan * N0 + (m.eBands[i] << LM)], N);
                    /* Just add the right channel if we're in stereo */
                    /*if (C==2)
                       for (j=0;j<N;j++)
                          tmp[j] = ADD16(SHR16(tmp[j], 1),SHR16(X[N0+j+(m.eBands[i]<<LM)], 1));*/
                    L1 = l1_metric(tmp, N, isTransient != 0 ? LM : 0, bias);
                    best_L1 = L1;
                    /* Check the -1 case for transients */
                    if (isTransient != 0 && narrow == 0)
                    {
                        OPUS_COPY(tmp_1, tmp, N);
                        haar1(tmp_1, N >> LM, 1 << LM);
                        L1 = l1_metric(tmp_1, N, LM + 1, bias);
                        if (L1 < best_L1)
                        {
                            best_L1 = L1;
                            best_level = -1;
                        }
                    }
                    /*printf ("%f ", L1);*/
                    for (k = 0; k < LM + (!(isTransient != 0 || narrow != 0) ? 1 : 0); k++)
                    {
                        int B;

                        if (isTransient != 0)
                            B = (LM - k - 1);
                        else
                            B = k + 1;

                        haar1(tmp, N >> k, 1 << k);

                        L1 = l1_metric(tmp, N, B, bias);

                        if (L1 < best_L1)
                        {
                            best_L1 = L1;
                            best_level = k + 1;
                        }
                    }
                    /*printf ("%d ", isTransient ? LM-best_level : best_level);*/
                    /* metric is in Q1 to be able to select the mid-point (-0.5) for narrower bands */
                    if (isTransient != 0)
                        metric[i] = 2 * best_level;
                    else
                        metric[i] = -2 * best_level;
                    /* For bands that can't be split to -1, set the metric to the half-way point to avoid
                       biasing the decision */
                    if (narrow != 0 && (metric[i] == 0 || metric[i] == -2 * LM))
                        metric[i] -= 1;
                    /*printf("%d ", metric[i]/2 + (!isTransient)*LM);*/
                }
                /*printf("\n");*/
                /* Search for the optimal tf resolution, including tf_select */
                tf_select = 0;
                for (sel = 0; sel < 2; sel++)
                {
                    cost0 = importance[0] * Abs(metric[0] - 2 * tf_select_table[LM][4 * isTransient + 2 * sel + 0]);
                    cost1 = importance[0] * Abs(metric[0] - 2 * tf_select_table[LM][4 * isTransient + 2 * sel + 1]) + (isTransient != 0 ? 0 : lambda);
                    for (i = 1; i < len; i++)
                    {
                        int curr0, curr1;
                        curr0 = IMIN(cost0, cost1 + lambda);
                        curr1 = IMIN(cost0 + lambda, cost1);
                        cost0 = curr0 + importance[i] * Abs(metric[i] - 2 * tf_select_table[LM][4 * isTransient + 2 * sel + 0]);
                        cost1 = curr1 + importance[i] * Abs(metric[i] - 2 * tf_select_table[LM][4 * isTransient + 2 * sel + 1]);
                    }
                    cost0 = IMIN(cost0, cost1);
                    selcost[sel] = cost0;
                }
                /* For now, we're conservative and only allow tf_select=1 for transients.
                 * If tests confirm it's useful for non-transients, we could allow it. */
                if (selcost[1] < selcost[0] && isTransient != 0)
                    tf_select = 1;
                cost0 = importance[0] * Abs(metric[0] - 2 * tf_select_table[LM][4 * isTransient + 2 * tf_select + 0]);
                cost1 = importance[0] * Abs(metric[0] - 2 * tf_select_table[LM][4 * isTransient + 2 * tf_select + 1]) + (isTransient != 0 ? 0 : lambda);
                /* Viterbi forward pass */
                for (i = 1; i < len; i++)
                {
                    int curr0, curr1;
                    int from0, from1;

                    from0 = cost0;
                    from1 = cost1 + lambda;
                    if (from0 < from1)
                    {
                        curr0 = from0;
                        path0[i] = 0;
                    }
                    else
                    {
                        curr0 = from1;
                        path0[i] = 1;
                    }

                    from0 = cost0 + lambda;
                    from1 = cost1;
                    if (from0 < from1)
                    {
                        curr1 = from0;
                        path1[i] = 0;
                    }
                    else
                    {
                        curr1 = from1;
                        path1[i] = 1;
                    }
                    cost0 = curr0 + importance[i] * Abs(metric[i] - 2 * tf_select_table[LM][4 * isTransient + 2 * tf_select + 0]);
                    cost1 = curr1 + importance[i] * Abs(metric[i] - 2 * tf_select_table[LM][4 * isTransient + 2 * tf_select + 1]);
                }
                tf_res[len - 1] = cost0 < cost1 ? 0 : 1;
                /* Viterbi backward pass to check the decisions */
                for (i = len - 2; i >= 0; i--)
                {
                    if (tf_res[i + 1] == 1)
                        tf_res[i] = path1[i + 1];
                    else
                        tf_res[i] = path0[i + 1];
                }
                /*printf("%d %f\n", *tf_sum, tf_estimate);*/
                return tf_select;
            }
        }

        internal static unsafe void tf_encode(int start, int end, int isTransient, int* tf_res, int LM, int tf_select, ref ec_ctx enc, in byte* ecbuf)
        {
            int curr, i;
            int tf_select_rsv;
            int tf_changed;
            int logp;
            uint budget;
            uint tell;
            budget = enc.storage * 8;
            tell = (uint)ec_tell(enc);
            logp = isTransient != 0 ? 2 : 4;
            /* Reserve space to code the tf_select decision. */
            tf_select_rsv = (LM > 0 && tell + logp + 1 <= budget) ? 1 : 0;
            budget -= (uint)tf_select_rsv;
            curr = tf_changed = 0;
            for (i = start; i < end; i++)
            {
                if (tell + logp <= budget)
                {
                    ec_enc_bit_logp(ref enc, ecbuf, tf_res[i] ^ curr, (uint)logp);
                    tell = (uint)ec_tell(enc);
                    curr = tf_res[i];
                    tf_changed |= curr;
                }
                else
                    tf_res[i] = curr;
                logp = isTransient != 0 ? 4 : 5;
            }
            /* Only code tf_select if it would actually make a difference. */
            if (tf_select_rsv != 0 &&
                  tf_select_table[LM][4 * isTransient + 0 + tf_changed] !=
                  tf_select_table[LM][4 * isTransient + 2 + tf_changed])
                ec_enc_bit_logp(ref enc, ecbuf, tf_select, 1);
            else
                tf_select = 0;
            for (i = start; i < end; i++)
                tf_res[i] = tf_select_table[LM][4 * isTransient + 2 * tf_select + tf_res[i]];
            /*for(i=0;i<end;i++)printf("%d ", isTransient ? tf_res[i] : LM+tf_res[i]);printf("\n");*/
        }


        internal static unsafe int alloc_trim_analysis(in CeltCustomMode m, in float* X,
            in float* bandLogE, int end, int LM, int C, int N0,
            ref AnalysisInfo analysis, ref float stereo_saving, float tf_estimate,
            int intensity, float surround_trim, int equiv_rate)
        {
            int i;
            float diff = 0;
            int c;
            int trim_index;
            float trim = QCONST16(5.0f, 8);
            float logXC, logXC2;
            /* At low bitrate, reducing the trim seems to help. At higher bitrates, it's less
               clear what's best, so we're keeping it as it was before, at least for now. */
            if (equiv_rate < 64000)
            {
                trim = QCONST16(4.0f, 8);
            }
            else if (equiv_rate < 80000)
            {
                int frac = (equiv_rate - 64000) >> 10;
                trim = QCONST16(4.0f, 8) + QCONST16(1.0f / 16.0f, 8) * frac;
            }
            if (C == 2)
            {
                float sum = 0; /* Q10 */
                float minXC; /* Q10 */
                /* Compute inter-channel correlation for low frequencies */
                for (i = 0; i < 8; i++)
                {
                    float partial;
                    partial = celt_inner_prod(&X[m.eBands[i] << LM], &X[N0 + (m.eBands[i] << LM)],
                          (m.eBands[i + 1] - m.eBands[i]) << LM);
                    sum = ADD16(sum, EXTRACT16(SHR32(partial, 18)));
                }
                sum = MULT16_16_Q15(QCONST16(1.0f / 8, 15), sum);
                sum = MIN16(QCONST16(1.0f, 10), ABS16(sum));
                minXC = sum;
                for (i = 8; i < intensity; i++)
                {
                    float partial;
                    partial = celt_inner_prod(&X[m.eBands[i] << LM], &X[N0 + (m.eBands[i] << LM)],
                          (m.eBands[i + 1] - m.eBands[i]) << LM);
                    minXC = MIN16(minXC, ABS16(EXTRACT16(SHR32(partial, 18))));
                }
                minXC = MIN16(QCONST16(1.0f, 10), ABS16(minXC));
                /*printf ("%f\n", sum);*/
                /* mid-side savings estimations based on the LF average*/
                logXC = celt_log2(QCONST32(1.001f, 20) - MULT16_16(sum, sum));
                /* mid-side savings estimations based on min correlation */
                logXC2 = MAX16(HALF16(logXC), celt_log2(QCONST32(1.001f, 20) - MULT16_16(minXC, minXC)));

                trim += MAX16(-QCONST16(4.0f, 8), MULT16_16_Q15(QCONST16(.75f, 15), logXC));
                stereo_saving = MIN16(stereo_saving + QCONST16(0.25f, 8), -HALF16(logXC2));
            }

            /* Estimate spectral tilt */
            c = 0; do
            {
                for (i = 0; i < end - 1; i++)
                {
                    diff += bandLogE[i + c * m.nbEBands] * (int)(2 + 2 * i - end);
                }
            } while (++c < C);
            diff /= C * (end - 1);
            /*printf("%f\n", diff);*/
            trim -= MAX32(-QCONST16(2.0f, 8), MIN32(QCONST16(2.0f, 8), SHR32(diff + QCONST16(1.0f, DB_SHIFT), DB_SHIFT - 8) / 6));
            trim -= SHR16(surround_trim, DB_SHIFT - 8);
            trim -= 2 * SHR16(tf_estimate, 14 - 8);
            if (analysis.valid != 0)
            {
                trim -= MAX16(-QCONST16(2.0f, 8), MIN16(QCONST16(2.0f, 8),
                      (float)(QCONST16(2.0f, 8) * (analysis.tonality_slope + .05f))));
            }

            trim_index = (int)Floor(.5f + trim);
            trim_index = IMAX(0, IMIN(10, trim_index));
            /*printf("%d\n", trim_index);*/
            return trim_index;
        }

        internal static unsafe int stereo_analysis(in CeltCustomMode m, in float* X,
            int LM, int N0)
        {
            int i;
            int thetas;
            float sumLR = EPSILON, sumMS = EPSILON;

            /* Use the L1 norm to model the entropy of the L/R signal vs the M/S signal */
            for (i = 0; i < 13; i++)
            {
                int j;
                for (j = m.eBands[i] << LM; j < m.eBands[i + 1] << LM; j++)
                {
                    float L, R, M, S;
                    /* We cast to 32-bit first because of the -32768 case */
                    L = EXTEND32(X[j]);
                    R = EXTEND32(X[N0 + j]);
                    M = ADD32(L, R);
                    S = SUB32(L, R);
                    sumLR = ADD32(sumLR, ADD32(ABS32(L), ABS32(R)));
                    sumMS = ADD32(sumMS, ADD32(ABS32(M), ABS32(S)));
                }
            }
            sumMS = MULT16_32_Q15(QCONST16(0.707107f, 15), sumMS);
            thetas = 13;
            /* We don't need thetas for lower bands with LM<=1 */
            if (LM <= 1)
                thetas -= 8;
            return (MULT16_32_Q15((m.eBands[13] << (LM + 1)) + thetas, sumMS)
                  > MULT16_32_Q15(m.eBands[13] << (LM + 1), sumLR)) ? 1 : 0;
        }

        private static void MSWAP(ref float a, ref float b)
        {
            float tmp = a;
            a = b;
            b = tmp;
        }

        internal static unsafe float median_of_5(in float* x)
        {
            float t0, t1, t2, t3, t4;
            t2 = x[2];
            if (x[0] > x[1])
            {
                t0 = x[1];
                t1 = x[0];
            }
            else
            {
                t0 = x[0];
                t1 = x[1];
            }
            if (x[3] > x[4])
            {
                t3 = x[4];
                t4 = x[3];
            }
            else
            {
                t3 = x[3];
                t4 = x[4];
            }
            if (t0 > t3)
            {
                MSWAP(ref t0, ref t3);
                MSWAP(ref t1, ref t4);
            }
            if (t2 > t1)
            {
                if (t1 < t3)
                    return MIN16(t2, t3);
                else
                    return MIN16(t4, t1);
            }
            else
            {
                if (t2 < t3)
                    return MIN16(t1, t3);
                else
                    return MIN16(t2, t4);
            }
        }

        internal static unsafe float median_of_3(in float* x)
        {
            float t0, t1, t2;
            if (x[0] > x[1])
            {
                t0 = x[1];
                t1 = x[0];
            }
            else
            {
                t0 = x[0];
                t1 = x[1];
            }
            t2 = x[2];
            if (t1 < t2)
                return t1;
            else if (t0 < t2)
                return t2;
            else
                return t0;
        }

        internal static unsafe float dynalloc_analysis(in float* bandLogE, in float* bandLogE2, in float* oldBandE,
            int nbEBands, int start, int end, int C, int* offsets, int lsb_depth, in short* logN,
            int isTransient, int vbr, int constrained_vbr, in short* eBands, int LM,
            int effectiveBytes, out int tot_boost_, int lfe, float* surround_dynalloc,
            ref AnalysisInfo analysis, int* importance, int* spread_weight)
        {
            int i, c;
            int tot_boost = 0;
            float maxDepth;
            Span<float> follower_array = new float[C * nbEBands];
            Span<float> noise_floor_array = new float[C * nbEBands];
            Span<float> bandLogE3_array = stackalloc float[nbEBands];
            Span<float> mask_array = stackalloc float[nbEBands];
            Span<float> sig_array = stackalloc float[nbEBands];
            fixed (float* follower = follower_array)
            fixed (float* noise_floor = noise_floor_array)
            fixed (float* bandLogE3 = bandLogE3_array)
            fixed (float* mask = mask_array)
            fixed (float* sig = sig_array)
            {
                OPUS_CLEAR(offsets, nbEBands);
                /* Dynamic allocation code */
                maxDepth = -QCONST16(31.9f, DB_SHIFT);
                for (i = 0; i < end; i++)
                {
                    /* Noise floor must take into account eMeans, the depth, the width of the bands
                       and the preemphasis filter (approx. square of bark band ID) */
                    noise_floor[i] = MULT16_16(QCONST16(0.0625f, DB_SHIFT), logN[i])
                          + QCONST16(.5f, DB_SHIFT) + SHL16(9 - lsb_depth, DB_SHIFT) - SHL16(eMeans[i], 6)
                          + MULT16_16(QCONST16(.0062f, DB_SHIFT), (i + 5) * (i + 5));
                }
                c = 0; do
                {
                    for (i = 0; i < end; i++)
                        maxDepth = MAX16(maxDepth, bandLogE[c * nbEBands + i] - noise_floor[i]);
                } while (++c < C);

                /* Compute a really simple masking model to avoid taking into account completely masked
                    bands when computing the spreading decision. */
                for (i = 0; i < end; i++)
                    mask[i] = bandLogE[i] - noise_floor[i];
                if (C == 2)
                {
                    for (i = 0; i < end; i++)
                        mask[i] = MAX16(mask[i], bandLogE[nbEBands + i] - noise_floor[i]);
                }
                OPUS_COPY(sig, mask, end);
                for (i = 1; i < end; i++)
                    mask[i] = MAX16(mask[i], mask[i - 1] - QCONST16(2.0f, DB_SHIFT));
                for (i = end - 2; i >= 0; i--)
                    mask[i] = MAX16(mask[i], mask[i + 1] - QCONST16(3.0f, DB_SHIFT));
                for (i = 0; i < end; i++)
                {
                    /* Compute SMR: Mask is never more than 72 dB below the peak and never below the noise floor.*/
                    float smr = sig[i] - MAX16(MAX16(0, maxDepth - QCONST16(12.0f, DB_SHIFT)), mask[i]);
                    /* Clamp SMR to make sure we're not shifting by something negative or too large. */
                    int shift = IMIN(5, IMAX(0, -(int)Floor(.5f + smr)));
                    spread_weight[i] = 32 >> shift;
                }
                /*for (i=0;i<end;i++)
                    printf("%d ", spread_weight[i]);
                printf("\n");*/

                /* Make sure that dynamic allocation can't make us bust the budget.
                   We enable the feature starting at 24 kb/s for 20-ms frames
                   and 96 kb/s for 2.5 ms frames.  */
                if (effectiveBytes >= (30 + 5 * LM) && lfe == 0)
                {
                    int last = 0;
                    c = 0; do
                    {
                        float offset;
                        float tmp;
                        float* f;
                        OPUS_COPY(bandLogE3, &bandLogE2[c * nbEBands], end);
                        if (LM == 0)
                        {
                            /* For 2.5 ms frames, the first 8 bands have just one bin, so the
                               energy is highly unreliable (high variance). For that reason,
                               we take the max with the previous energy so that at least 2 bins
                               are getting used. */
                            for (i = 0; i < IMIN(8, end); i++) bandLogE3[i] = MAX16(bandLogE2[c * nbEBands + i], oldBandE[c * nbEBands + i]);
                        }
                        f = &follower[c * nbEBands];
                        f[0] = bandLogE3[0];
                        for (i = 1; i < end; i++)
                        {
                            /* The last band to be at least 3 dB higher than the previous one
                               is the last we'll consider. Otherwise, we run into problems on
                               bandlimited signals. */
                            if (bandLogE3[i] > bandLogE3[i - 1] + QCONST16(.5f, DB_SHIFT))
                                last = i;
                            f[i] = MIN16(f[i - 1] + QCONST16(1.5f, DB_SHIFT), bandLogE3[i]);
                        }
                        for (i = last - 1; i >= 0; i--)
                            f[i] = MIN16(f[i], MIN16(f[i + 1] + QCONST16(2.0f, DB_SHIFT), bandLogE3[i]));

                        /* Combine with a median filter to avoid dynalloc triggering unnecessarily.
                           The "offset" value controls how conservative we are -- a higher offset
                           reduces the impact of the median filter and makes dynalloc use more bits. */
                        offset = QCONST16(1.0f, DB_SHIFT);
                        for (i = 2; i < end - 2; i++)
                            f[i] = MAX16(f[i], median_of_5(&bandLogE3[i - 2]) - offset);
                        tmp = median_of_3(&bandLogE3[0]) - offset;
                        f[0] = MAX16(f[0], tmp);
                        f[1] = MAX16(f[1], tmp);
                        tmp = median_of_3(&bandLogE3[end - 3]) - offset;
                        f[end - 2] = MAX16(f[end - 2], tmp);
                        f[end - 1] = MAX16(f[end - 1], tmp);

                        for (i = 0; i < end; i++)
                            f[i] = MAX16(f[i], noise_floor[i]);
                    } while (++c < C);
                    if (C == 2)
                    {
                        for (i = start; i < end; i++)
                        {
                            /* Consider 24 dB "cross-talk" */
                            follower[nbEBands + i] = MAX16(follower[nbEBands + i], follower[i] - QCONST16(4.0f, DB_SHIFT));
                            follower[i] = MAX16(follower[i], follower[nbEBands + i] - QCONST16(4.0f, DB_SHIFT));
                            follower[i] = HALF16(MAX16(0, bandLogE[i] - follower[i]) + MAX16(0, bandLogE[nbEBands + i] - follower[nbEBands + i]));
                        }
                    }
                    else
                    {
                        for (i = start; i < end; i++)
                        {
                            follower[i] = MAX16(0, bandLogE[i] - follower[i]);
                        }
                    }
                    for (i = start; i < end; i++)
                        follower[i] = MAX16(follower[i], surround_dynalloc[i]);
                    for (i = start; i < end; i++)
                    {
                        importance[i] = (int)Floor(.5f + 13 * celt_exp2(MIN16(follower[i], QCONST16(4.0f, DB_SHIFT))));
                    }
                    /* For non-transient CBR/CVBR frames, halve the dynalloc contribution */
                    if ((vbr == 0 || constrained_vbr != 0) && isTransient == 0)
                    {
                        for (i = start; i < end; i++)
                            follower[i] = HALF16(follower[i]);
                    }
                    for (i = start; i < end; i++)
                    {
                        if (i < 8)
                            follower[i] *= 2;
                        if (i >= 12)
                            follower[i] = HALF16(follower[i]);
                    }
                    if (analysis.valid != 0)
                    {
                        for (i = start; i < IMIN(LEAK_BANDS, end); i++)
                            follower[i] = follower[i] + QCONST16(1.0f / 64.0f, DB_SHIFT) * analysis.leak_boost[i];
                    }
                    for (i = start; i < end; i++)
                    {
                        int width;
                        int boost;
                        int boost_bits;

                        follower[i] = MIN16(follower[i], QCONST16(4, DB_SHIFT));

                        width = C * (eBands[i + 1] - eBands[i]) << LM;
                        if (width < 6)
                        {
                            boost = (int)SHR32(EXTEND32(follower[i]), DB_SHIFT);
                            boost_bits = boost * width << BITRES;
                        }
                        else if (width > 48)
                        {
                            boost = (int)SHR32(EXTEND32(follower[i]) * 8, DB_SHIFT);
                            boost_bits = (boost * width << BITRES) / 8;
                        }
                        else
                        {
                            boost = (int)SHR32(EXTEND32(follower[i]) * width / 6, DB_SHIFT);
                            boost_bits = boost * 6 << BITRES;
                        }
                        /* For CBR and non-transient CVBR frames, limit dynalloc to 2/3 of the bits */
                        if ((vbr == 0 || (constrained_vbr != 0 && isTransient == 0))
                              && (tot_boost + boost_bits) >> BITRES >> 3 > 2 * effectiveBytes / 3)
                        {
                            int cap = ((2 * effectiveBytes / 3) << BITRES << 3);
                            offsets[i] = cap - tot_boost;
                            tot_boost = cap;
                            break;
                        }
                        else
                        {
                            offsets[i] = boost;
                            tot_boost += boost_bits;
                        }
                    }
                }
                else
                {
                    for (i = start; i < end; i++)
                        importance[i] = 13;
                }

                tot_boost_ = tot_boost;
                return maxDepth;
            }
        }


        internal static unsafe int run_prefilter(ref CeltCustomEncoder st, float* input, float* prefilter_mem, int CC, int N,
            int prefilter_tapset, out int pitch, out float gain, out int qgain, int enabled, int nbAvailableBytes, ref AnalysisInfo analysis)
        {
            int c;
            int pitch_index;
            float gain1;
            float pf_threshold;
            int pf_on;
            int qg;
            int overlap;

            ref CeltCustomMode mode = ref st.mode.Value;
            overlap = mode.overlap;
            float*[] pre_array = new float*[2];
            float[] _pre_array = new float[CC * (N + COMBFILTER_MAXPERIOD)];
            fixed(float** pre = pre_array)
            fixed (float* _pre = _pre_array)
            {
                pre[0] = _pre;
                pre[1] = _pre + (N + COMBFILTER_MAXPERIOD);

                c = 0; do
                {
                    OPUS_COPY(pre[c], prefilter_mem + c * COMBFILTER_MAXPERIOD, COMBFILTER_MAXPERIOD);
                    OPUS_COPY(pre[c] + COMBFILTER_MAXPERIOD, input + c * (N + overlap) + overlap, N);
                } while (++c < CC);

                if (enabled != 0)
                {
                    Span<float> pitch_array = new float[(COMBFILTER_MAXPERIOD + N) >> 1];
                    fixed (float* pitch_buf = pitch_array)
                    {
                        pitch_downsample(pre, pitch_buf, COMBFILTER_MAXPERIOD + N, CC);
                        /* Don't search for the fir last 1.5 octave of the range because
                           there's too many false-positives due to short-term correlation */
                        pitch_search(pitch_buf + (COMBFILTER_MAXPERIOD >> 1), pitch_buf, N,
                              COMBFILTER_MAXPERIOD - 3 * COMBFILTER_MINPERIOD, &pitch_index);
                        pitch_index = COMBFILTER_MAXPERIOD - pitch_index;

                        gain1 = remove_doubling(pitch_buf, COMBFILTER_MAXPERIOD, COMBFILTER_MINPERIOD,
                              N, &pitch_index, st.prefilter_period, st.prefilter_gain);
                        if (pitch_index > COMBFILTER_MAXPERIOD - 2)
                            pitch_index = COMBFILTER_MAXPERIOD - 2;
                        gain1 = MULT16_16_Q15(QCONST16(.7f, 15), gain1);
                        /*printf("%d %d %f %f\n", pitch_change, pitch_index, gain1, st.analysis.tonality);*/
                        if (st.loss_rate > 2)
                            gain1 = HALF32(gain1);
                        if (st.loss_rate > 4)
                            gain1 = HALF32(gain1);
                        if (st.loss_rate > 8)
                            gain1 = 0;
                    }
                }
                else
                {
                    gain1 = 0;
                    pitch_index = COMBFILTER_MINPERIOD;
                }
                if (analysis.valid != 0)
                    gain1 = (float)(gain1 * analysis.max_pitch_ratio);
                /* Gain threshold for enabling the prefilter/postfilter */
                pf_threshold = QCONST16(.2f, 15);

                /* Adjusting the threshold based on rate and continuity */
                if (Abs(pitch_index - st.prefilter_period) * 10 > pitch_index)
                    pf_threshold += QCONST16(.2f, 15);
                if (nbAvailableBytes < 25)
                    pf_threshold += QCONST16(.1f, 15);
                if (nbAvailableBytes < 35)
                    pf_threshold += QCONST16(.1f, 15);
                if (st.prefilter_gain > QCONST16(.4f, 15))
                    pf_threshold -= QCONST16(.1f, 15);
                if (st.prefilter_gain > QCONST16(.55f, 15))
                    pf_threshold -= QCONST16(.1f, 15);

                /* Hard threshold at 0.2 */
                pf_threshold = MAX16(pf_threshold, QCONST16(.2f, 15));
                if (gain1 < pf_threshold)
                {
                    gain1 = 0;
                    pf_on = 0;
                    qg = 0;
                }
                else
                {
                    /*This block is not gated by a total bits check only because
                      of the nbAvailableBytes check above.*/
                    if (ABS16(gain1 - st.prefilter_gain) < QCONST16(.1f, 15))
                        gain1 = st.prefilter_gain;

                    qg = (int)Floor(.5f + gain1 * 32 / 3) - 1;
                    qg = IMAX(0, IMIN(7, qg));
                    gain1 = QCONST16(0.09375f, 15) * (qg + 1);
                    pf_on = 1;
                }
                /*printf("%d %f\n", pitch_index, gain1);*/

                fixed (float* stin_mem = st.in_mem)
                fixed (float* modewindow = mode.window)
                {
                    c = 0; do
                    {
                        int offset = mode.shortMdctSize - overlap;
                        st.prefilter_period = IMAX(st.prefilter_period, COMBFILTER_MINPERIOD);
                        OPUS_COPY(input + c * (N + overlap), stin_mem + c * (overlap), overlap);
                        if (offset != 0)
                            comb_filter(input + c * (N + overlap) + overlap, pre[c] + COMBFILTER_MAXPERIOD,
                                  st.prefilter_period, st.prefilter_period, offset, -st.prefilter_gain, -st.prefilter_gain,
                                  st.prefilter_tapset, st.prefilter_tapset, null, 0);

                        comb_filter(input + c * (N + overlap) + overlap + offset, pre[c] + COMBFILTER_MAXPERIOD + offset,
                              st.prefilter_period, pitch_index, N - offset, -st.prefilter_gain, -gain1,
                              st.prefilter_tapset, prefilter_tapset, modewindow, overlap);
                        OPUS_COPY(stin_mem + c * (overlap), input + c * (N + overlap) + N, overlap);

                        if (N > COMBFILTER_MAXPERIOD)
                        {
                            OPUS_COPY(prefilter_mem + c * COMBFILTER_MAXPERIOD, pre[c] + N, COMBFILTER_MAXPERIOD);
                        }
                        else
                        {
                            OPUS_MOVE(prefilter_mem + c * COMBFILTER_MAXPERIOD, prefilter_mem + c * COMBFILTER_MAXPERIOD + N, COMBFILTER_MAXPERIOD - N);
                            OPUS_COPY(prefilter_mem + c * COMBFILTER_MAXPERIOD + COMBFILTER_MAXPERIOD - N, pre[c] + COMBFILTER_MAXPERIOD, N);
                        }
                    } while (++c < CC);
                }

                gain = gain1;
                pitch = pitch_index;
                qgain = qg;
                return pf_on;
            }
        }

        internal static unsafe int compute_vbr(in CeltCustomMode mode, ref AnalysisInfo analysis, int base_target,
            int LM, int bitrate, int lastCodedBands, int C, int intensity,
            int constrained_vbr, float stereo_saving, int tot_boost,
            float tf_estimate, int pitch_change, float maxDepth,
            int lfe, int has_surround_mask, float surround_masking,
            float temporal_vbr)
        {
            /* The target rate in 8th bits per frame */
            int target;
            int coded_bins;
            int coded_bands;
            float tf_calibration;
            int nbEBands;

            nbEBands = mode.nbEBands;
            fixed (short* eBands = mode.eBands)
            {
                coded_bands = lastCodedBands != 0 ? lastCodedBands : nbEBands;
                coded_bins = eBands[coded_bands] << LM;
                if (C == 2)
                    coded_bins += eBands[IMIN(intensity, coded_bands)] << LM;

                target = base_target;

                /*printf("%f %f %f %f %d %d ", st.analysis.activity, st.analysis.tonality, tf_estimate, st.stereo_saving, tot_boost, coded_bands);*/
                if (analysis.valid != 0 && analysis.activity < .4f)
                    target -= (int)((coded_bins << BITRES) * (.4f - analysis.activity));
                /* Stereo savings */
                if (C == 2)
                {
                    int coded_stereo_bands;
                    int coded_stereo_dof;
                    float max_frac;
                    coded_stereo_bands = IMIN(intensity, coded_bands);
                    coded_stereo_dof = (eBands[coded_stereo_bands] << LM) - coded_stereo_bands;
                    /* Maximum fraction of the bits we can save if the signal is mono. */
                    max_frac = DIV32_16(MULT16_16(QCONST16(0.8f, 15), coded_stereo_dof), coded_bins);
                    stereo_saving = MIN16(stereo_saving, QCONST16(1.0f, 8));
                    /*printf("%d %d %d ", coded_stereo_dof, coded_bins, tot_boost);*/
                    target -= (int)MIN32(MULT16_32_Q15(max_frac, target),
                                    SHR32(MULT16_16(stereo_saving - QCONST16(0.1f, 8), (coded_stereo_dof << BITRES)), 8));
                }
                /* Boost the rate according to dynalloc (minus the dynalloc average for calibration). */
                target += tot_boost - (19 << LM);
                /* Apply transient boost, compensating for average boost. */
                tf_calibration = QCONST16(0.044f, 14);
                target += (int)SHL32(MULT16_32_Q15(tf_estimate - tf_calibration, target), 1);

                /* Apply tonality boost */
                if (analysis.valid != 0 && lfe == 0)
                {
                    int tonal_target;
                    float tonal;

                    /* Tonality boost (compensating for the average). */
                    tonal = MAX16(0.0f, analysis.tonality - .15f) - 0.12f;
                    tonal_target = target + (int)((coded_bins << BITRES) * 1.2f * tonal);
                    if (pitch_change != 0)
                        tonal_target += (int)((coded_bins << BITRES) * .8f);
                    /*printf("%f %f ", analysis.tonality, tonal);*/
                    target = tonal_target;
                }

                if (has_surround_mask != 0 && lfe == 0)
                {
                    int surround_target = target + (int)SHR32(MULT16_16(surround_masking, coded_bins << BITRES), DB_SHIFT);
                    /*printf("%f %d %d %d %d %d %d ", surround_masking, coded_bins, st.end, st.intensity, surround_target, target, st.bitrate);*/
                    target = IMAX(target / 4, surround_target);
                }

                {
                    int floor_depth;
                    int bins;
                    bins = eBands[nbEBands - 2] << LM;
                    /*floor_depth = SHR32(MULT16_16((C*bins<<BITRES),celt_log2(SHL32(MAX16(1,sample_max),13))), DB_SHIFT);*/
                    floor_depth = (int)SHR32(MULT16_16((C * bins << BITRES), maxDepth), DB_SHIFT);
                    floor_depth = IMAX(floor_depth, target >> 2);
                    target = IMIN(target, floor_depth);
                    /*printf("%f %d\n", maxDepth, floor_depth);*/
                }

                /* Make VBR less aggressive for constrained VBR because we can't keep a higher bitrate
                   for long. Needs tuning. */
                if ((has_surround_mask == 0 || lfe != 0) && constrained_vbr != 0)
                {
                    target = base_target + (int)MULT16_32_Q15(QCONST16(0.67f, 15), target - base_target);
                }

                if (has_surround_mask == 0 && tf_estimate < QCONST16(.2f, 14))
                {
                    float amount;
                    float tvbr_factor;
                    amount = MULT16_16_Q15(QCONST16(.0000031f, 30), IMAX(0, IMIN(32000, 96000 - bitrate)));
                    tvbr_factor = SHR32(MULT16_16(temporal_vbr, amount), DB_SHIFT);
                    target += (int)MULT16_32_Q15(tvbr_factor, target);
                }

                /* Don't allow more than doubling the rate */
                target = IMIN(2 * base_target, target);

                return target;
            }
        }

        private static readonly float[] intensity_thresholds =
            /* 0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19  20  off*/
            {  1, 2, 3, 4, 5, 6, 7, 8,16,24,36,44,50,56,62,67,72,79,88,106,134};
        private static readonly float[] intensity_histeresis =
            {  1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 3, 3, 4, 5, 6,  8, 8};

        internal static unsafe int celt_encode_with_ec(
            ref CeltCustomEncoder st, in float* pcm, int frame_size, byte* compressed,
            int nbCompressedBytes, StructRef<ec_ctx> enc_ref)
        {
            int i, c, N;
            int bits;
            float* prefilter_mem;
            float* oldBandE, oldLogE, oldLogE2, energyError;
            int shortBlocks = 0;
            int isTransient = 0;
            int CC = st.channels;
            int C = st.stream_channels;
            int LM, M;
            int tf_select;
            int nbFilledBytes, nbAvailableBytes;
            int start;
            int end;
            int effEnd;
            int codedBands;
            int alloc_trim;
            int pitch_index = COMBFILTER_MINPERIOD;
            float gain1 = 0;
            int dual_stereo = 0;
            int effectiveBytes;
            int dynalloc_logp;
            int vbr_rate;
            int total_bits;
            int total_boost;
            int balance;
            int tell;
            int tell0_frac;
            int prefilter_tapset = 0;
            int pf_on;
            int anti_collapse_rsv;
            int anti_collapse_on = 0;
            int silence = 0;
            int tf_chan = 0;
            float tf_estimate;
            int pitch_change = 0;
            int tot_boost;
            float sample_max;
            float maxDepth;
            ref CeltCustomMode mode = ref st.mode.Value;
            int nbEBands;
            int overlap;
            int secondMdct;
            int signalBandwidth;
            int transient_got_disabled = 0;
            float surround_masking = 0;
            float temporal_vbr = 0;
            float surround_trim = 0;
            int equiv_rate;
            int hybrid;
            int weak_transient = 0;
            int enable_tf_analysis;

            if (nbCompressedBytes < 2 || pcm == null)
            {
                return OPUS_BAD_ARG;
            }

            for (LM = 0; LM <= mode.maxLM; LM++)
                if (mode.shortMdctSize << LM == frame_size)
                    break;
            if (LM > mode.maxLM)
            {
                return OPUS_BAD_ARG;
            }

            nbEBands = mode.nbEBands;
            overlap = mode.overlap;
            start = st.start;
            end = st.end;
            hybrid = start != 0 ? 1 : 0;
            tf_estimate = 0;
            frame_size *= st.upsample;
            M = 1 << LM;
            N = M * mode.shortMdctSize;

            Span<float> freq_array = new float[CC * N]; /**< Interleaved signal MDCTs */
            Span<float> bandE_array = new float[nbEBands * CC];
            Span<float> bandLogE_array = new float[nbEBands * CC];
            Span<float> bandLogE2_array = new float[C * nbEBands];
            Span<float> input_array = new float[CC * (N + overlap)];
            Span<float> surround_dynalloc_array = new float[C * nbEBands];
            Span<float> X_array = new float[C * N]; /**< Interleaved normalised MDCTs */
            Span<int> offsets_array = new int[nbEBands];
            Span<int> importance_array = new int[nbEBands];
            Span<int> spread_weight_array = new int[nbEBands];
            Span<int> tf_res_array = new int[nbEBands];
            Span<float> error_array = new float[C * nbEBands];
            Span<int> cap_array = new int[nbEBands];
            Span<int> fine_quant_array = new int[nbEBands];
            Span<int> pulses_array = new int[nbEBands];
            Span<int> fine_priority_array = new int[nbEBands];
            Span<byte> collapse_masks_array = new byte[C * nbEBands];
            fixed (float* freq = freq_array)
            fixed (float* bandE = bandE_array)
            fixed (float* bandLogE = bandLogE_array)
            fixed (float* bandLogE2 = bandLogE2_array)
            fixed (float* input = input_array)
            fixed (float* surround_dynalloc = surround_dynalloc_array)
            fixed (float* X = X_array)
            fixed (int* offsets = offsets_array)
            fixed (int* importance = importance_array)
            fixed (int* spread_weight = spread_weight_array)
            fixed (int* tf_res = tf_res_array)
            fixed (float* error = error_array)
            fixed (int* cap = cap_array)
            fixed (int* fine_quant = fine_quant_array)
            fixed (int* pulses = pulses_array)
            fixed (int* fine_priority = fine_priority_array)
            fixed (byte* collapse_masks = collapse_masks_array)
            fixed (short* eBands = mode.eBands)
            fixed (float* st_in_mem = st.in_mem)
            fixed (float* st_preemph_memE = st.preemph_memE)
            fixed (float* mode_preemph = mode.preemph)
            {
                prefilter_mem = st_in_mem + CC * (overlap);
                oldBandE = (float*)(st_in_mem + CC * (overlap + COMBFILTER_MAXPERIOD));
                oldLogE = oldBandE + CC * nbEBands;
                oldLogE2 = oldLogE + CC * nbEBands;
                energyError = oldLogE2 + CC * nbEBands;

                if (enc_ref == null)
                {
                    tell0_frac = tell = 1;
                    nbFilledBytes = 0;
                }
                else
                {
                    tell0_frac = (int)ec_tell_frac(enc_ref.Value);
                    tell = ec_tell(enc_ref.Value);
                    nbFilledBytes = (tell + 4) >> 3;
                }

                ASSERT(st.signalling == 0);

                /* Can't produce more than 1275 output bytes */
                nbCompressedBytes = IMIN(nbCompressedBytes, 1275);
                nbAvailableBytes = nbCompressedBytes - nbFilledBytes;

                if (st.vbr != 0 && st.bitrate != OPUS_BITRATE_MAX)
                {
                    int den = mode.Fs >> BITRES;
                    vbr_rate = (st.bitrate * frame_size + (den >> 1)) / den;
                    effectiveBytes = vbr_rate >> (3 + BITRES);
                }
                else
                {
                    int tmp;
                    vbr_rate = 0;
                    tmp = st.bitrate * frame_size;
                    if (tell > 1)
                        tmp += tell * mode.Fs;
                    if (st.bitrate != OPUS_BITRATE_MAX)
                    {
                        nbCompressedBytes = IMAX(2, IMIN(nbCompressedBytes,
                              (tmp + 4 * mode.Fs) / (8 * mode.Fs) - (st.signalling != 0 ? 1 : 0)));
                        ec_enc_shrink(ref enc_ref.Value, compressed, (uint)nbCompressedBytes);
                    }
                    effectiveBytes = nbCompressedBytes - nbFilledBytes;
                }
                equiv_rate = ((int)nbCompressedBytes * 8 * 50 << (3 - LM)) - (40 * C + 20) * ((400 >> LM) - 50);
                if (st.bitrate != OPUS_BITRATE_MAX)
                    equiv_rate = IMIN(equiv_rate, st.bitrate - (40 * C + 20) * ((400 >> LM) - 50));

                if (enc_ref == null)
                {
                    enc_ref = new StructRef<ec_ctx>();
                    ec_enc_init(ref enc_ref.Value, (uint)nbCompressedBytes);
                }

                ref ec_ctx enc = ref enc_ref.Value;

                if (vbr_rate > 0)
                {
                    /* Computes the max bit-rate allowed in VBR mode to avoid violating the
                        target rate and buffering.
                       We must do this up front so that bust-prevention logic triggers
                        correctly if we don't have enough bits. */
                    if (st.constrained_vbr != 0)
                    {
                        int vbr_bound;
                        int max_allowed;
                        /* We could use any multiple of vbr_rate as bound (depending on the
                            delay).
                           This is clamped to ensure we use at least two bytes if the encoder
                            was entirely empty, but to allow 0 in hybrid mode. */
                        vbr_bound = vbr_rate;
                        max_allowed = IMIN(IMAX(tell == 1 ? 2 : 0,
                              (vbr_rate + vbr_bound - st.vbr_reservoir) >> (BITRES + 3)),
                              nbAvailableBytes);
                        if (max_allowed < nbAvailableBytes)
                        {
                            nbCompressedBytes = nbFilledBytes + max_allowed;
                            nbAvailableBytes = max_allowed;
                            ec_enc_shrink(ref enc, compressed, (uint)nbCompressedBytes);
                        }
                    }
                }
                total_bits = nbCompressedBytes * 8;

                effEnd = end;
                if (effEnd > mode.effEBands)
                    effEnd = mode.effEBands;

                sample_max = MAX32(st.overlap_max, celt_maxabs16(pcm, C * (N - overlap) / st.upsample));
                st.overlap_max = celt_maxabs16(pcm + C * (N - overlap) / st.upsample, C * overlap / st.upsample);
                sample_max = MAX32(sample_max, st.overlap_max);
                silence = (sample_max <= (float)1 / (1 << st.lsb_depth)) ? 1 : 0;
                if (tell == 1)
                    ec_enc_bit_logp(ref enc, compressed, silence, 15);
                else
                    silence = 0;
                if (silence != 0)
                {
                    /*In VBR mode there is no need to send more than the minimum. */
                    if (vbr_rate > 0)
                    {
                        effectiveBytes = nbCompressedBytes = IMIN(nbCompressedBytes, nbFilledBytes + 2);
                        total_bits = nbCompressedBytes * 8;
                        nbAvailableBytes = 2;
                        ec_enc_shrink(ref enc, compressed, (uint)nbCompressedBytes);
                    }
                    /* Pretend we've filled all the remaining bits with zeros
                          (that's what the initialiser did anyway) */
                    tell = nbCompressedBytes * 8;
                    enc.nbits_total += tell - ec_tell(enc);
                }
                c = 0; do
                {
                    int need_clip = 0;
                    need_clip = (st.clip != 0 && sample_max > 65536.0f) ? 1 : 0;
                    celt_preemphasis(pcm + c, input + c * (N + overlap) + overlap, N, CC, st.upsample,
                                mode_preemph, st_preemph_memE + c, need_clip);
                } while (++c < CC);

                /* Find pitch period and gain */
                {
                    int enabled;
                    int qg;
                    enabled = (((st.lfe != 0 && nbAvailableBytes > 3) || nbAvailableBytes > 12 * C) && hybrid == 0 && silence == 0 && st.disable_pf == 0
                          && st.complexity >= 5) ? 1 : 0;

                    prefilter_tapset = st.tapset_decision;
                    pf_on = run_prefilter(ref st, input, prefilter_mem, CC, N, prefilter_tapset, out pitch_index, out gain1, out qg, enabled, nbAvailableBytes, ref st.analysis.Value);
                    if ((gain1 > QCONST16(.4f, 15) || st.prefilter_gain > QCONST16(.4f, 15)) && (st.analysis.Value.valid == 0 || st.analysis.Value.tonality > .3)
                          && (pitch_index > 1.26 * st.prefilter_period || pitch_index < .79 * st.prefilter_period))
                        pitch_change = 1;
                    if (pf_on == 0)
                    {
                        if (hybrid == 0 && tell + 16 <= total_bits)
                            ec_enc_bit_logp(ref enc, compressed, 0, 1);
                    }
                    else
                    {
                        /*This block is not gated by a total bits check only because
                          of the nbAvailableBytes check above.*/
                        int octave;
                        ec_enc_bit_logp(ref enc, compressed, 1, 1);
                        pitch_index += 1;
                        octave = EC_ILOG((uint)pitch_index) - 5;
                        ec_enc_uint(ref enc, compressed, (uint)octave, 6);
                        ec_enc_bits(ref enc, compressed, (uint)(pitch_index - (16 << octave)), (uint)(4 + octave));
                        pitch_index -= 1;
                        ec_enc_bits(ref enc, compressed, (uint)qg, 3);
                        ec_enc_icdf(ref enc, compressed, prefilter_tapset, tapset_icdf, 2);
                    }
                }

                isTransient = 0;
                shortBlocks = 0;
                if (st.complexity >= 1 && st.lfe == 0)
                {
                    /* Reduces the likelihood of energy instability on fricatives at low bitrate
                       in hybrid mode. It seems like we still want to have real transients on vowels
                       though (small SILK quantization offset value). */
                    int allow_weak_transients = (hybrid != 0 && effectiveBytes < 15 && st.silk_info.Value.signalType != 2) ? 1 : 0;
                    isTransient = transient_analysis(input, N + overlap, CC,
                          &tf_estimate, &tf_chan, allow_weak_transients, &weak_transient);
                }
                if (LM > 0 && ec_tell(enc) + 3 <= total_bits)
                {
                    if (isTransient != 0)
                        shortBlocks = M;
                }
                else
                {
                    isTransient = 0;
                    transient_got_disabled = 1;
                }

                secondMdct = (shortBlocks != 0 && st.complexity >= 8) ? 1 : 0;
                if (secondMdct != 0)
                {
                    compute_mdcts(mode, 0, input, freq, C, CC, LM, st.upsample);
                    compute_band_energies(mode, freq, bandE, effEnd, C, LM);
                    amp2Log2(mode, effEnd, end, bandE, bandLogE2, C);
                    for (c = 0; c < C; c++)
                    {
                        for (i = 0; i < end; i++)
                            bandLogE2[nbEBands * c + i] += HALF16(SHL16(LM, DB_SHIFT));
                    }
                }

                compute_mdcts(mode, shortBlocks, input, freq, C, CC, LM, st.upsample);
                /* This should catch any NaN in the CELT input. Since we're not supposed to see any (they're filtered
                   at the Opus layer), just abort. */
                ASSERT(celt_isnan(freq[0]) == 0 && (C == 1 || celt_isnan(freq[N]) == 0));
                if (CC == 2 && C == 1)
                    tf_chan = 0;
                compute_band_energies(mode, freq, bandE, effEnd, C, LM);

                if (st.lfe != 0)
                {
                    for (i = 2; i < end; i++)
                    {
                        bandE[i] = IMIN(bandE[i], MULT16_32_Q15(QCONST16(1e-4f, 15), bandE[0]));
                        bandE[i] = MAX32(bandE[i], EPSILON);
                    }
                }
                amp2Log2(mode, effEnd, end, bandE, bandLogE, C);

                OPUS_CLEAR(surround_dynalloc, end);
                /* This computes how much masking takes place between surround channels */
                if (hybrid == 0 && st.energy_mask != null && st.lfe == 0)
                {
                    int mask_end;
                    int midband;
                    int count_dynalloc;
                    float mask_avg = 0;
                    float diff = 0;
                    int count = 0;
                    mask_end = IMAX(2, st.lastCodedBands);
                    for (c = 0; c < C; c++)
                    {
                        for (i = 0; i < mask_end; i++)
                        {
                            float mask;
                            mask = MAX16(MIN16(st.energy_mask[nbEBands * c + i],
                                   QCONST16(.25f, DB_SHIFT)), -QCONST16(2.0f, DB_SHIFT));
                            if (mask > 0)
                                mask = HALF16(mask);
                            mask_avg += MULT16_16(mask, eBands[i + 1] - eBands[i]);
                            count += eBands[i + 1] - eBands[i];
                            diff += MULT16_16(mask, 1 + 2 * i - mask_end);
                        }
                    }
                    ASSERT(count > 0);
                    mask_avg = DIV32_16(mask_avg, count);
                    mask_avg += QCONST16(.2f, DB_SHIFT);
                    diff = diff * 6 / (C * (mask_end - 1) * (mask_end + 1) * mask_end);
                    /* Again, being conservative */
                    diff = HALF32(diff);
                    diff = MAX32(MIN32(diff, QCONST32(.031f, DB_SHIFT)), -QCONST32(.031f, DB_SHIFT));
                    /* Find the band that's in the middle of the coded spectrum */
                    for (midband = 0; eBands[midband + 1] < eBands[mask_end] / 2; midband++) ;
                    count_dynalloc = 0;
                    for (i = 0; i < mask_end; i++)
                    {
                        float lin;
                        float unmask;
                        lin = mask_avg + diff * (i - midband);
                        if (C == 2)
                            unmask = MAX16(st.energy_mask[i], st.energy_mask[nbEBands + i]);
                        else
                            unmask = st.energy_mask[i];
                        unmask = MIN16(unmask, QCONST16(.0f, DB_SHIFT));
                        unmask -= lin;
                        if (unmask > QCONST16(.25f, DB_SHIFT))
                        {
                            surround_dynalloc[i] = unmask - QCONST16(.25f, DB_SHIFT);
                            count_dynalloc++;
                        }
                    }
                    if (count_dynalloc >= 3)
                    {
                        /* If we need dynalloc in many bands, it's probably because our
                           initial masking rate was too low. */
                        mask_avg += QCONST16(.25f, DB_SHIFT);
                        if (mask_avg > 0)
                        {
                            /* Something went really wrong in the original calculations,
                               disabling masking. */
                            mask_avg = 0;
                            diff = 0;
                            OPUS_CLEAR(surround_dynalloc, mask_end);
                        }
                        else
                        {
                            for (i = 0; i < mask_end; i++)
                                surround_dynalloc[i] = MAX16(0, surround_dynalloc[i] - QCONST16(.25f, DB_SHIFT));
                        }
                    }
                    mask_avg += QCONST16(.2f, DB_SHIFT);
                    /* Convert to 1/64th units used for the trim */
                    surround_trim = 64 * diff;
                    /*printf("%d %d ", mask_avg, surround_trim);*/
                    surround_masking = mask_avg;
                }
                /* Temporal VBR (but not for LFE) */
                if (st.lfe == 0)
                {
                    float follow = -QCONST16(10.0f, DB_SHIFT);
                    float frame_avg = 0;
                    float offset = shortBlocks != 0 ? HALF16(SHL16(LM, DB_SHIFT)) : 0;
                    for (i = start; i < end; i++)
                    {
                        follow = MAX16(follow - QCONST16(1.0f, DB_SHIFT), bandLogE[i] - offset);
                        if (C == 2)
                            follow = MAX16(follow, bandLogE[i + nbEBands] - offset);
                        frame_avg += follow;
                    }
                    frame_avg /= (end - start);
                    temporal_vbr = SUB16(frame_avg, st.spec_avg);
                    temporal_vbr = MIN16(QCONST16(3.0f, DB_SHIFT), MAX16(-QCONST16(1.5f, DB_SHIFT), temporal_vbr));
                    st.spec_avg += MULT16_16_Q15(QCONST16(.02f, 15), temporal_vbr);
                }
                /*for (i=0;i<21;i++)
                   printf("%f ", bandLogE[i]);
                printf("\n");*/

                if (secondMdct == 0)
                {
                    OPUS_COPY(bandLogE2, bandLogE, C * nbEBands);
                }

                /* Last chance to catch any transient we might have missed in the
                   time-domain analysis */
                if (LM > 0 && ec_tell(enc) + 3 <= total_bits && isTransient == 0 && st.complexity >= 5 && st.lfe == 0 && hybrid == 0)
                {
                    if (patch_transient_decision(bandLogE, oldBandE, nbEBands, start, end, C) != 0)
                    {
                        isTransient = 1;
                        shortBlocks = M;
                        compute_mdcts(mode, shortBlocks, input, freq, C, CC, LM, st.upsample);
                        compute_band_energies(mode, freq, bandE, effEnd, C, LM);
                        amp2Log2(mode, effEnd, end, bandE, bandLogE, C);
                        /* Compensate for the scaling of short vs long mdcts */
                        for (c = 0; c < C; c++)
                        {
                            for (i = 0; i < end; i++)
                                bandLogE2[nbEBands * c + i] += HALF16(SHL16(LM, DB_SHIFT));
                        }
                        tf_estimate = QCONST16(.2f, 14);
                    }
                }

                if (LM > 0 && ec_tell(enc) + 3 <= total_bits)
                    ec_enc_bit_logp(ref enc, compressed, isTransient, 3);

                /* Band normalisation */
                normalise_bands(mode, freq, X, bandE, effEnd, C, M);

                enable_tf_analysis = (effectiveBytes >= 15 * C && hybrid == 0 && st.complexity >= 2 && st.lfe == 0) ? 1 : 0;

                fixed (short* modelogN = mode.logN)
                {
                    maxDepth = dynalloc_analysis(bandLogE, bandLogE2, oldBandE, nbEBands, start, end, C, offsets,
                          st.lsb_depth, modelogN, isTransient, st.vbr, st.constrained_vbr,
                          eBands, LM, effectiveBytes, out tot_boost, st.lfe, surround_dynalloc, ref st.analysis.Value, importance, spread_weight);
                }

                /* Disable variable tf resolution for hybrid and at very low bitrate */
                if (enable_tf_analysis != 0)
                {
                    int lambda;
                    lambda = IMAX(80, 20480 / effectiveBytes + 2);
                    tf_select = tf_analysis(mode, effEnd, isTransient, tf_res, lambda, X, N, LM, tf_estimate, tf_chan, importance);
                    for (i = effEnd; i < end; i++)
                        tf_res[i] = tf_res[effEnd - 1];
                }
                else if (hybrid != 0 && weak_transient != 0)
                {
                    /* For weak transients, we rely on the fact that improving time resolution using
                       TF on a long window is imperfect and will not result in an energy collapse at
                       low bitrate. */
                    for (i = 0; i < end; i++)
                        tf_res[i] = 1;
                    tf_select = 0;
                }
                else if (hybrid != 0 && effectiveBytes < 15 && st.silk_info.Value.signalType != 2)
                {
                    /* For low bitrate hybrid, we force temporal resolution to 5 ms rather than 2.5 ms. */
                    for (i = 0; i < end; i++)
                        tf_res[i] = 0;
                    tf_select = isTransient;
                }
                else
                {
                    for (i = 0; i < end; i++)
                        tf_res[i] = isTransient;
                    tf_select = 0;
                }

                c = 0;
                do
                {
                    for (i = start; i < end; i++)
                    {
                        /* When the energy is stable, slightly bias energy quantization towards
                           the previous error to make the gain more stable (a constant offset is
                           better than fluctuations). */
                        if (ABS32(SUB32(bandLogE[i + c * nbEBands], oldBandE[i + c * nbEBands])) < QCONST16(2.0f, DB_SHIFT))
                        {
                            bandLogE[i + c * nbEBands] -= MULT16_16_Q15(energyError[i + c * nbEBands], QCONST16(0.25f, 15));
                        }
                    }
                } while (++c < C);
                quant_coarse_energy(mode, start, end, effEnd, bandLogE,
                      oldBandE, (uint)total_bits, error, ref enc, compressed,
                      C, LM, nbAvailableBytes, st.force_intra,
                      ref st.delayedIntra, st.complexity >= 4 ? 1: 0, st.loss_rate, st.lfe);

                tf_encode(start, end, isTransient, tf_res, LM, tf_select, ref enc, compressed);

                if (ec_tell(enc) + 4 <= total_bits)
                {
                    if (st.lfe != 0)
                    {
                        st.tapset_decision = 0;
                        st.spread_decision = SPREAD_NORMAL;
                    }
                    else if (hybrid != 0)
                    {
                        if (st.complexity == 0)
                            st.spread_decision = SPREAD_NONE;
                        else if (isTransient != 0)
                            st.spread_decision = SPREAD_NORMAL;
                        else
                            st.spread_decision = SPREAD_AGGRESSIVE;
                    }
                    else if (shortBlocks != 0 || st.complexity < 3 || nbAvailableBytes < 10 * C)
                    {
                        if (st.complexity == 0)
                            st.spread_decision = SPREAD_NONE;
                        else
                            st.spread_decision = SPREAD_NORMAL;
                    }
                    else
                    {
                        {
                            st.spread_decision = spreading_decision(mode, X,
                                  ref st.tonal_average, st.spread_decision, ref st.hf_average,
                                  ref st.tapset_decision, (pf_on != 0 && shortBlocks == 0) ? 1 : 0, effEnd, C, M, spread_weight);
                        }
                        /*printf("%d %d\n", st.tapset_decision, st.spread_decision);*/
                        /*printf("%f %d %f %d\n\n", st.analysis.tonality, st.spread_decision, st.analysis.tonality_slope, st.tapset_decision);*/
                    }

                    ec_enc_icdf(ref enc, compressed, st.spread_decision, spread_icdf, 5);
                }

                /* For LFE, everything interesting is in the first band */
                if (st.lfe != 0)
                    offsets[0] = IMIN(8, effectiveBytes / 3);
                init_caps(mode, cap, LM, C);

                dynalloc_logp = 6;
                total_bits <<= BITRES;
                total_boost = 0;
                tell = (int)ec_tell_frac(enc);
                for (i = start; i < end; i++)
                {
                    int width, quanta;
                    int dynalloc_loop_logp;
                    int boost;
                    int j;
                    width = C * (eBands[i + 1] - eBands[i]) << LM;
                    /* quanta is 6 bits, but no more than 1 bit/sample
                       and no less than 1/8 bit/sample */
                    quanta = IMIN(width << BITRES, IMAX(6 << BITRES, width));
                    dynalloc_loop_logp = dynalloc_logp;
                    boost = 0;
                    for (j = 0; tell + (dynalloc_loop_logp << BITRES) < total_bits - total_boost
                          && boost < cap[i]; j++)
                    {
                        int flag;
                        flag = (j < offsets[i]) ? 1 : 0;
                        ec_enc_bit_logp(ref enc, compressed, flag, (uint)dynalloc_loop_logp);
                        tell = (int)ec_tell_frac(enc);
                        if (flag == 0)
                            break;
                        boost += quanta;
                        total_boost += quanta;
                        dynalloc_loop_logp = 1;
                    }
                    /* Making dynalloc more likely */
                    if (j != 0)
                        dynalloc_logp = IMAX(2, dynalloc_logp - 1);
                    offsets[i] = boost;
                }

                if (C == 2)
                {
                    /* Always use MS for 2.5 ms frames until we can do a better analysis */
                    if (LM != 0)
                        dual_stereo = stereo_analysis(mode, X, LM, N);
                    fixed (float* thresholds = intensity_thresholds)
                    fixed (float* histeresis = intensity_histeresis)
                    {
                        st.intensity = hysteresis_decision((float)(equiv_rate / 1000),
                              thresholds, histeresis, 21, st.intensity);
                        st.intensity = IMIN(end, IMAX(start, st.intensity));
                    }
                }

                alloc_trim = 5;
                if (tell + (6 << BITRES) <= total_bits - total_boost)
                {
                    if (start > 0 || st.lfe != 0)
                    {
                        st.stereo_saving = 0;
                        alloc_trim = 5;
                    }
                    else
                    {
                        alloc_trim = alloc_trim_analysis(mode, X, bandLogE,
                           end, LM, C, N, ref st.analysis.Value, ref st.stereo_saving, tf_estimate,
                           st.intensity, surround_trim, equiv_rate);
                    }
                    ec_enc_icdf(ref enc, compressed, alloc_trim, trim_icdf, 7);
                    tell = (int)ec_tell_frac(enc);
                }

                /* Variable bitrate */
                if (vbr_rate > 0)
                {
                    float alpha;
                    int delta;
                    /* The target rate in 8th bits per frame */
                    int target, base_target;
                    int min_allowed;
                    int lm_diff = mode.maxLM - LM;

                    /* Don't attempt to use more than 510 kb/s, even for frames smaller than 20 ms.
                       The CELT allocator will just not be able to use more than that anyway. */
                    nbCompressedBytes = IMIN(nbCompressedBytes, 1275 >> (3 - LM));
                    if (hybrid == 0)
                    {
                        base_target = vbr_rate - ((40 * C + 20) << BITRES);
                    }
                    else
                    {
                        base_target = IMAX(0, vbr_rate - ((9 * C + 4) << BITRES));
                    }

                    if (st.constrained_vbr != 0)
                        base_target += (st.vbr_offset >> lm_diff);

                    if (hybrid == 0)
                    {
                        target = compute_vbr(mode, ref st.analysis.Value, base_target, LM, equiv_rate,
                           st.lastCodedBands, C, st.intensity, st.constrained_vbr,
                           st.stereo_saving, tot_boost, tf_estimate, pitch_change, maxDepth,
                           st.lfe, st.energy_mask != null ? 1 : 0, surround_masking,
                           temporal_vbr);
                    }
                    else
                    {
                        target = base_target;
                        /* Tonal frames (offset<100) need more bits than noisy (offset>100) ones. */
                        if (st.silk_info.Value.offset < 100) target += 12 << BITRES >> (3 - LM);
                        if (st.silk_info.Value.offset > 100) target -= 18 << BITRES >> (3 - LM);
                        /* Boosting bitrate on transients and vowels with significant temporal
                           spikes. */
                        target += (int)MULT16_16_Q14(tf_estimate - QCONST16(.25f, 14), (50 << BITRES));
                        /* If we have a strong transient, let's make sure it has enough bits to code
                           the first two bands, so that it can use folding rather than noise. */
                        if (tf_estimate > QCONST16(.7f, 14))
                            target = IMAX(target, 50 << BITRES);
                    }
                    /* The current offset is removed from the target and the space used
                       so far is added*/
                    target = target + tell;
                    /* In VBR mode the frame size must not be reduced so much that it would
                        result in the encoder running out of bits.
                       The margin of 2 bytes ensures that none of the bust-prevention logic
                        in the decoder will have triggered so far. */
                    min_allowed = ((tell + total_boost + (1 << (BITRES + 3)) - 1) >> (BITRES + 3)) + 2;
                    /* Take into account the 37 bits we need to have left in the packet to
                       signal a redundant frame in hybrid mode. Creating a shorter packet would
                       create an entropy coder desync. */
                    if (hybrid != 0)
                        min_allowed = IMAX(min_allowed, (tell0_frac + (37 << BITRES) + total_boost + (1 << (BITRES + 3)) - 1) >> (BITRES + 3));

                    nbAvailableBytes = (target + (1 << (BITRES + 2))) >> (BITRES + 3);
                    nbAvailableBytes = IMAX(min_allowed, nbAvailableBytes);
                    nbAvailableBytes = IMIN(nbCompressedBytes, nbAvailableBytes);

                    /* By how much did we "miss" the target on that frame */
                    delta = target - vbr_rate;

                    target = nbAvailableBytes << (BITRES + 3);

                    /*If the frame is silent we don't adjust our drift, otherwise
                      the encoder will shoot to very high rates after hitting a
                      span of silence, but we do allow the bitres to refill.
                      This means that we'll undershoot our target in CVBR/VBR modes
                      on files with lots of silence. */
                    if (silence != 0)
                    {
                        nbAvailableBytes = 2;
                        target = 2 * 8 << BITRES;
                        delta = 0;
                    }

                    if (st.vbr_count < 970)
                    {
                        st.vbr_count++;
                        alpha = celt_rcp(SHL32(EXTEND32(st.vbr_count + 20), 16));
                    }
                    else
                        alpha = QCONST16(.001f, 15);
                    /* How many bits have we used in excess of what we're allowed */
                    if (st.constrained_vbr != 0)
                        st.vbr_reservoir += target - vbr_rate;
                    /*printf ("%d\n", st.vbr_reservoir);*/

                    /* Compute the offset we need to apply in order to reach the target */
                    if (st.constrained_vbr != 0)
                    {
                        st.vbr_drift += (int)MULT16_32_Q15(alpha, (delta * (1 << lm_diff)) - st.vbr_offset - st.vbr_drift);
                        st.vbr_offset = -st.vbr_drift;
                    }
                    /*printf ("%d\n", st.vbr_drift);*/

                    if (st.constrained_vbr != 0 && st.vbr_reservoir < 0)
                    {
                        /* We're under the min value -- increase rate */
                        int adjust = (-st.vbr_reservoir) / (8 << BITRES);
                        /* Unless we're just coding silence */
                        nbAvailableBytes += silence != 0 ? 0 : adjust;
                        st.vbr_reservoir = 0;
                        /*printf ("+%d\n", adjust);*/
                    }
                    nbCompressedBytes = IMIN(nbCompressedBytes, nbAvailableBytes);
                    /*printf("%d\n", nbCompressedBytes*50*8);*/
                    /* This moves the raw bits to take into account the new compressed size */
                    ec_enc_shrink(ref enc, compressed, (uint)nbCompressedBytes);
                }

                /* Bit allocation */

                /* bits =           packet size                    - where we are - safety*/
                bits = (((int)nbCompressedBytes * 8) << BITRES) - (int)ec_tell_frac(enc) - 1;
                anti_collapse_rsv = isTransient != 0 && LM >= 2 && bits >= ((LM + 2) << BITRES) ? (1 << BITRES) : 0;
                bits -= anti_collapse_rsv;
                signalBandwidth = end - 1;
                if (st.analysis.Value.valid != 0)
                {
                    int min_bandwidth;
                    if (equiv_rate < (int)32000 * C)
                        min_bandwidth = 13;
                    else if (equiv_rate < (int)48000 * C)
                        min_bandwidth = 16;
                    else if (equiv_rate < (int)60000 * C)
                        min_bandwidth = 18;
                    else if (equiv_rate < (int)80000 * C)
                        min_bandwidth = 19;
                    else
                        min_bandwidth = 20;
                    signalBandwidth = IMAX(st.analysis.Value.bandwidth, min_bandwidth);
                }
                if (st.lfe != 0)
                    signalBandwidth = 1;
                codedBands = clt_compute_allocation(mode, start, end, offsets, cap,
                      alloc_trim, ref st.intensity, ref dual_stereo, bits, out balance, pulses,
                      fine_quant, fine_priority, C, LM, ref enc, compressed, 1, st.lastCodedBands, signalBandwidth);
                if (st.lastCodedBands != 0)
                    st.lastCodedBands = IMIN(st.lastCodedBands + 1, IMAX(st.lastCodedBands - 1, codedBands));
                else
                    st.lastCodedBands = codedBands;

                quant_fine_energy(mode, start, end, oldBandE, error, fine_quant, ref enc, compressed, C);

                /* Residual quantisation */
                quant_all_bands(1, st.mode, start, end, X, C == 2 ? X + N : null, collapse_masks,
                      bandE, pulses, shortBlocks, st.spread_decision,
                      dual_stereo, st.intensity, tf_res, nbCompressedBytes * (8 << BITRES) - anti_collapse_rsv,
                      balance, enc_ref, compressed, LM, codedBands, ref st.rng, st.complexity, st.disable_inv);

                if (anti_collapse_rsv > 0)
                {
                    anti_collapse_on = st.consec_transient < 2 ? 1 : 0;
                    ec_enc_bits(ref enc, compressed, (uint)anti_collapse_on, 1);
                }
                quant_energy_finalise(mode, start, end, oldBandE, error, fine_quant, fine_priority, nbCompressedBytes * 8 - ec_tell(enc), ref enc, compressed, C);
                OPUS_CLEAR(energyError, nbEBands * CC);
                c = 0;
                do
                {
                    for (i = start; i < end; i++)
                    {
                        energyError[i + c * nbEBands] = MAX16(-QCONST16(0.5f, 15), MIN16(QCONST16(0.5f, 15), error[i + c * nbEBands]));
                    }
                } while (++c < C);

                if (silence != 0)
                {
                    for (i = 0; i < C * nbEBands; i++)
                        oldBandE[i] = -QCONST16(28.0f, DB_SHIFT);
                }

                st.prefilter_period = pitch_index;
                st.prefilter_gain = gain1;
                st.prefilter_tapset = prefilter_tapset;

                if (CC == 2 && C == 1)
                {
                    OPUS_COPY(&oldBandE[nbEBands], oldBandE, nbEBands);
                }

                if (isTransient == 0)
                {
                    OPUS_COPY(oldLogE2, oldLogE, CC * nbEBands);
                    OPUS_COPY(oldLogE, oldBandE, CC * nbEBands);
                }
                else
                {
                    for (i = 0; i < CC * nbEBands; i++)
                        oldLogE[i] = MIN16(oldLogE[i], oldBandE[i]);
                }
                /* In case start or end were to change */
                c = 0; do
                {
                    for (i = 0; i < start; i++)
                    {
                        oldBandE[c * nbEBands + i] = 0;
                        oldLogE[c * nbEBands + i] = oldLogE2[c * nbEBands + i] = -QCONST16(28.0f, DB_SHIFT);
                    }
                    for (i = end; i < nbEBands; i++)
                    {
                        oldBandE[c * nbEBands + i] = 0;
                        oldLogE[c * nbEBands + i] = oldLogE2[c * nbEBands + i] = -QCONST16(28.0f, DB_SHIFT);
                    }
                } while (++c < CC);

                if (isTransient != 0 || transient_got_disabled != 0)
                    st.consec_transient++;
                else
                    st.consec_transient = 0;
                st.rng = enc.rng;

                /* If there's any room left (can only happen for very high rates),
                   it's already filled with zeros */
                ec_enc_done(ref enc, compressed);

                if (ec_get_error(enc) != 0)
                    return OPUS_INTERNAL_ERROR;
                else
                    return nbCompressedBytes;
            }
        }

        internal static int opus_custom_encoder_ctl(
            ref CeltCustomEncoder st, int request, int value)
        {
            switch (request)
            {
                case OPUS_SET_COMPLEXITY_REQUEST:
                    {
                        if (value < 0 || value > 10)
                            goto bad_arg;
                        st.complexity = value;
                    }
                    break;
                case CELT_SET_START_BAND_REQUEST:
                    {
                        if (value < 0 || value >= st.mode.Value.nbEBands)
                            goto bad_arg;
                        st.start = value;
                    }
                    break;
                case CELT_SET_END_BAND_REQUEST:
                    {
                        if (value < 1 || value > st.mode.Value.nbEBands)
                            goto bad_arg;
                        st.end = value;
                    }
                    break;
                case CELT_SET_PREDICTION_REQUEST:
                    {
                        if (value < 0 || value > 2)
                            goto bad_arg;
                        st.disable_pf = value <= 1 ? 1 : 0;
                        st.force_intra = value == 0 ? 1 : 0;
                    }
                    break;
                case OPUS_SET_PACKET_LOSS_PERC_REQUEST:
                    {
                        if (value < 0 || value > 100)
                            goto bad_arg;
                        st.loss_rate = value;
                    }
                    break;
                case OPUS_SET_VBR_CONSTRAINT_REQUEST:
                    {
                        st.constrained_vbr = value;
                    }
                    break;
                case OPUS_SET_VBR_REQUEST:
                    {
                        st.vbr = value;
                    }
                    break;
                case OPUS_SET_BITRATE_REQUEST:
                    {
                        if (value <= 500 && value != OPUS_BITRATE_MAX)
                            goto bad_arg;
                        value = IMIN(value, 260000 * st.channels);
                        st.bitrate = value;
                    }
                    break;
                case CELT_SET_CHANNELS_REQUEST:
                    {
                        if (value < 1 || value > 2)
                            goto bad_arg;
                        st.stream_channels = value;
                    }
                    break;
                case OPUS_SET_LSB_DEPTH_REQUEST:
                    {
                        if (value < 8 || value > 24)
                            goto bad_arg;
                        st.lsb_depth = value;
                    }
                    break;
                case OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST:
                    {
                        if (value < 0 || value > 1)
                        {
                            goto bad_arg;
                        }
                        st.disable_inv = value;
                    }
                    break;
                case CELT_SET_SIGNALLING_REQUEST:
                    {
                        st.signalling = value;
                    }
                    break;
                case OPUS_SET_LFE_REQUEST:
                    {
                        st.lfe = value;
                    }
                    break;
                default:
                    goto bad_request;
            }

            return OPUS_OK;
            bad_arg:
            return OPUS_BAD_ARG;
            bad_request:
            return OPUS_UNIMPLEMENTED;
        }

        internal static int opus_custom_encoder_ctl(
            ref CeltCustomEncoder st, int request, out int value)
        {
            switch (request)
            {
                case OPUS_GET_LSB_DEPTH_REQUEST:
                    {
                        value = st.lsb_depth;
                    }
                    break;
                case OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST:
                    {
                        value = st.disable_inv;
                    }
                    break;
                default:
                    goto bad_request;
            }

            return OPUS_OK;
            bad_request:
            value = 0;
            return OPUS_UNIMPLEMENTED;
        }

        internal static unsafe int opus_custom_encoder_ctl(ref CeltCustomEncoder st, int request)
        {
            switch (request)
            {
                case OPUS_RESET_STATE:
                    {
                        int i;
                        float* oldBandE, oldLogE, oldLogE2;
                        fixed (float* st_in_mem = st.in_mem)
                        fixed (float* memE = st.preemph_memE)
                        fixed (float* memD = st.preemph_memE)
                        {
                            oldBandE = (float*)(st_in_mem + st.channels * (st.mode.Value.overlap + COMBFILTER_MAXPERIOD));
                            oldLogE = oldBandE + st.channels * st.mode.Value.nbEBands;
                            oldLogE2 = oldLogE + st.channels * st.mode.Value.nbEBands;
                            
                            // This clears every value past ENCODER_RESET START
                            st.rng = 0;
                            st.spread_decision = 0;
                            st.delayedIntra = 0;
                            st.tonal_average = 0;
                            st.lastCodedBands = 0;
                            st.hf_average = 0;
                            st.tapset_decision = 0;
                            st.prefilter_period = 0;
                            st.prefilter_gain = 0;
                            st.prefilter_tapset = 0;
                            st.consec_transient = 0;
                            st.analysis.Value = new AnalysisInfo();
                            st.silk_info.Value = new SILKInfo();
                            OPUS_CLEAR(memE, 2);
                            OPUS_CLEAR(memD, 2);
                            OPUS_CLEAR(st_in_mem, opus_custom_encoder_get_memory_size(st.mode.Value, st.channels));
                            st.vbr_reservoir = 0;
                            st.vbr_drift = 0;
                            st.vbr_offset = 0;
                            st.vbr_count = 0;
                            st.overlap_max = 0;
                            st.stereo_saving = 0;
                            st.intensity = 0;
                            st.energy_mask = null;
                            st.spec_avg = 0;
                            ///////

                            for (i = 0; i < st.channels * st.mode.Value.nbEBands; i++)
                                oldLogE[i] = oldLogE2[i] = -QCONST16(28.0f, DB_SHIFT);
                        }

                        st.vbr_offset = 0;
                        st.delayedIntra = 1;
                        st.spread_decision = SPREAD_NORMAL;
                        st.tonal_average = 256;
                        st.hf_average = 0;
                        st.tapset_decision = 0;
                    }
                    break;
                default:
                    goto bad_request;
            }

            return OPUS_OK;
            bad_request:
            return OPUS_UNIMPLEMENTED;
        }

        internal static int opus_custom_encoder_ctl(
            ref CeltCustomEncoder st, int request, ref AnalysisInfo value)
        {
            switch (request)
            {
                case CELT_SET_ANALYSIS_REQUEST:
                    {
                        st.analysis.Value.Assign(ref value);
                    }
                    break;
                default:
                    goto bad_request;
            }

            return OPUS_OK;
            bad_request:
            return OPUS_UNIMPLEMENTED;
        }

        internal static int opus_custom_encoder_ctl(
            ref CeltCustomEncoder st, int request, ref SILKInfo value)
        {
            switch (request)
            {
                case CELT_SET_SILK_INFO_REQUEST:
                    {
                        st.silk_info.Value.Assign(ref value);
                    }
                    break;
                default:
                    goto bad_request;
            }

            return OPUS_OK;
            bad_request:
            return OPUS_UNIMPLEMENTED;
        }

        internal static int opus_custom_encoder_ctl(
            ref CeltCustomEncoder st, int request, out StructRef<CeltCustomMode> value)
        {
            switch (request)
            {
                case CELT_GET_MODE_REQUEST:
                    {
                        value = st.mode;
                    }
                    break;
                default:
                    goto bad_request;
            }

            return OPUS_OK;
            bad_request:
            value = null;
            return OPUS_UNIMPLEMENTED;
        }

        internal static int opus_custom_encoder_ctl(
            ref CeltCustomEncoder st, int request, out uint value)
        {
            switch (request)
            {

                case OPUS_GET_FINAL_RANGE_REQUEST:
                    {
                        value = st.rng;
                    }
                    break;
                default:
                    goto bad_request;
            }

            return OPUS_OK;
            bad_request:
            value = 0;
            return OPUS_UNIMPLEMENTED;
        }

        internal static int opus_custom_encoder_ctl(
            ref CeltCustomEncoder st, int request, float[] value)
        {
            switch (request)
            {
                case OPUS_SET_ENERGY_MASK_REQUEST:
                    {
                        st.energy_mask = value;
                    }
                    break;
                default:
                    goto bad_request;
            }

            return OPUS_OK;
            bad_request:
            return OPUS_UNIMPLEMENTED;
        }
    }
}
