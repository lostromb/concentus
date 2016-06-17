using Concentus.Celt.Enums;
using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Opus;
using Concentus.Opus.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Celt
{
    public static class celt_encoder
    {
        private const bool TRACE_FILE = false;

        public static int opus_custom_encoder_init_arch(CELTEncoder st, CELTMode mode,
                                                 int channels)
        {
            if (channels < 0 || channels > 2)
                return OpusError.OPUS_BAD_ARG;

            if (st == null || mode == null)
                return OpusError.OPUS_ALLOC_FAIL;

            st.Reset();

            st.mode = mode;
            st.stream_channels = st.channels = channels;

            st.upsample = 1;
            st.start = 0;
            st.end = st.mode.effEBands;
            st.signalling = 1;

            st.constrained_vbr = 1;
            st.clip = 1;

            st.bitrate = OpusConstants.OPUS_BITRATE_MAX;
            st.vbr = 0;
            st.force_intra = 0;
            st.complexity = 5;
            st.lsb_depth = 24;

            // fixme is this necessary if we just call encoder_ctrl right there anyways?
            st.in_mem = Pointer.Malloc<int>(channels * mode.overlap);
            st.prefilter_mem = Pointer.Malloc<int>(channels * CeltConstants.COMBFILTER_MAXPERIOD);
            st.oldBandE = Pointer.Malloc<int>(channels * mode.nbEBands);
            st.oldLogE = Pointer.Malloc<int>(channels * mode.nbEBands);
            st.oldLogE2 = Pointer.Malloc<int>(channels * mode.nbEBands);

            opus_custom_encoder_ctl(st, OpusControl.OPUS_RESET_STATE);

            return OpusError.OPUS_OK;
        }

        public static int celt_encoder_init(CELTEncoder st, int sampling_rate, int channels)
        {
            int ret;
            ret = opus_custom_encoder_init_arch(st, modes.opus_custom_mode_create(48000, 960, null), channels);
            if (ret != OpusError.OPUS_OK)
                return ret;
            st.upsample = celt.resampling_factor(sampling_rate);
            return OpusError.OPUS_OK;
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

        public static int transient_analysis(Pointer<int> input, int len, int C,
                                  BoxedValue<int> tf_estimate, BoxedValue<int> tf_chan)
        {
            int i;
            Pointer<int> tmp;
            int mem0, mem1;
            int is_transient = 0;
            int mask_metric = 0;
            int c;
            int tf_max;
            int len2;

            tmp = Pointer.Malloc<int>(len);

            len2 = len / 2;
            for (c = 0; c < C; c++)
            {
                int mean;
                int unmask = 0;
                int norm;
                int maxE;
                mem0 = 0;
                mem1 = 0;
                /* High-pass filter: (1 - 2*z^-1 + z^-2) / (1 - z^-1 + .5*z^-2) */
                for (i = 0; i < len; i++)
                {
                    int x, y;
                    x = Inlines.SHR32(input[i + c * len], CeltConstants.SIG_SHIFT);
                    y = Inlines.ADD32(mem0, x);
                    mem0 = mem1 + y - Inlines.SHL32(x, 1);
                    mem1 = x - Inlines.SHR32(y, 1);
                    tmp[i] = Inlines.EXTRACT16(Inlines.SHR32(y, 2));
                    /*printf("%f ", tmp[i]);*/
                }
                /*printf("\n");*/
                /* First few samples are bad because we don't propagate the memory */
                tmp.MemSet(0, 12);

                /* Normalize tmp to max range */
                {
                    int shift = 0;
                    shift = 14 - Inlines.celt_ilog2(1 + Inlines.celt_maxabs32(tmp, len));
                    if (shift != 0)
                    {
                        for (i = 0; i < len; i++)
                            tmp[i] = Inlines.SHL16(tmp[i], shift);
                    }
                }

                mean = 0;
                mem0 = 0;
                /* Grouping by two to reduce complexity */
                /* Forward pass to compute the post-echo threshold*/
                for (i = 0; i < len2; i++)
                {
                    int x2 = (Inlines.PSHR32(Inlines.MULT16_16(tmp[2 * i], tmp[2 * i]) + Inlines.MULT16_16(tmp[2 * i + 1], tmp[2 * i + 1]), 16));
                    mean += x2;
                    tmp[i] = (mem0 + Inlines.PSHR32(x2 - mem0, 4));
                    mem0 = tmp[i];
                }

                mem0 = 0;
                maxE = 0;
                /* Backward pass to compute the pre-echo threshold */
                for (i = len2 - 1; i >= 0; i--)
                {
                    /* FIXME: Use PSHR16() instead */
                    tmp[i] = (mem0 + Inlines.PSHR32(tmp[i] - mem0, 3));
                    mem0 = tmp[i];
                    maxE = Inlines.MAX16(maxE, (mem0));
                }
                /*for (i=0;i<len2;i++)printf("%f ", tmp[i]/mean);printf("\n");*/

                /* Compute the ratio of the "frame energy" over the harmonic mean of the energy.
                   This essentially corresponds to a bitrate-normalized temporal noise-to-mask
                   ratio */

                /* As a compromise with the old transient detector, frame energy is the
                   geometric mean of the energy and half the max */
                /* Costs two sqrt() to avoid overflows */
                mean = Inlines.MULT16_16(Inlines.celt_sqrt(mean), Inlines.celt_sqrt(Inlines.MULT16_16(maxE, len2 >> 1)));
                /* Inverse of the mean energy in Q15+6 */
                norm = Inlines.SHL32((len2), 6 + 14) / Inlines.ADD32(CeltConstants.EPSILON, Inlines.SHR32(mean, 1));
                /* Compute harmonic mean discarding the unreliable boundaries
                   The data is smooth, so we only take 1/4th of the samples */
                unmask = 0;
                for (i = 12; i < len2 - 5; i += 4)
                {
                    int id;
                    id = Inlines.MAX32(0, Inlines.MIN32(127, Inlines.MULT16_32_Q15((tmp[i] + CeltConstants.EPSILON), norm))); /* Do not round to nearest */
                    unmask += inv_table[id];
                }
                /*printf("%d\n", unmask);*/
                /* Normalize, compensate for the 1/4th of the sample and the factor of 6 in the inverse table */
                unmask = 64 * unmask * 4 / (6 * (len2 - 17));
                if (unmask > mask_metric)
                {
                    tf_chan.Val = c;
                    mask_metric = unmask;
                }
            }
            is_transient = mask_metric > 200 ? 1 : 0;

            /* Arbitrary metric for VBR boost */
            tf_max = Inlines.MAX16(0, (Inlines.celt_sqrt(27 * mask_metric) - 42));
            /* *tf_estimate = 1 + Inlines.MIN16(1, sqrt(Inlines.MAX16(0, tf_max-30))/20); */
            tf_estimate.Val = (Inlines.celt_sqrt(Inlines.MAX32(0, Inlines.SHL32(Inlines.MULT16_16(Inlines.QCONST16(0.0069f, 14), Inlines.MIN16(163, tf_max)), 14) - Inlines.QCONST32(0.139f, 28))));
            /*printf("%d %f\n", tf_max, mask_metric);*/

#if FUZZING
            is_transient = new Random().Next() & 0x1;
#endif
            /*printf("%d %f %d\n", is_transient, (float)*tf_estimate, tf_max);*/
            return is_transient;
        }

        /* Looks for sudden increases of energy to decide whether we need to patch
           the transient decision */
        public static int patch_transient_decision(Pointer<int> newE, Pointer<int> oldE, int nbEBands,
              int start, int end, int C)
        {
            int i, c;
            int mean_diff = 0;
            int[] spread_old = new int[26];
            /* Apply an aggressive (-6 dB/Bark) spreading function to the old frame to
               avoid false detection caused by irrelevant bands */
            if (C == 1)
            {
                spread_old[start] = oldE[start];
                for (i = start + 1; i < end; i++)
                    spread_old[i] = Inlines.MAX16((spread_old[i - 1] - Inlines.QCONST16(1.0f, CeltConstants.DB_SHIFT)), oldE[i]);
            }
            else {
                spread_old[start] = Inlines.MAX16(oldE[start], oldE[start + nbEBands]);
                for (i = start + 1; i < end; i++)
                    spread_old[i] = Inlines.MAX16((spread_old[i - 1] - Inlines.QCONST16(1.0f, CeltConstants.DB_SHIFT)),
                                          Inlines.MAX16(oldE[i], oldE[i + nbEBands]));
            }
            for (i = end - 2; i >= start; i--)
                spread_old[i] = Inlines.MAX16(spread_old[i], (spread_old[i + 1] - Inlines.QCONST16(1.0f, CeltConstants.DB_SHIFT)));
            /* Compute mean increase */
            c = 0; do
            {
                for (i = Inlines.IMAX(2, start); i < end - 1; i++)
                {
                    int x1, x2;
                    x1 = Inlines.MAX16(0, newE[i + c * nbEBands]);
                    x2 = Inlines.MAX16(0, spread_old[i]);
                    mean_diff = Inlines.ADD32(mean_diff, (Inlines.MAX16(0, Inlines.SUB16(x1, x2))));
                }
            } while (++c < C);
            mean_diff = Inlines.DIV32(mean_diff, C * (end - 1 - Inlines.IMAX(2, start)));
            /*printf("%f %f %d\n", mean_diff, max_diff, count);*/
            return (mean_diff > Inlines.QCONST16(1.0f, CeltConstants.DB_SHIFT)) ? 1 : 0;
        }

        /** Apply window and compute the MDCT for all sub-frames and
            all channels in a frame */
        public static void compute_mdcts(CELTMode mode, int shortBlocks, Pointer<int> input,
                                  Pointer<int> output, int C, int CC, int LM, int upsample)
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
            else {
                B = 1;
                N = mode.shortMdctSize << LM;
                shift = mode.maxLM - LM;
            }
            c = 0;
            do
            {
                for (b = 0; b < B; b++)
                {
                    /* Interleaving the sub-frames while doing the MDCTs */
                    mdct.clt_mdct_forward_c(
                        mode.mdct,
                        input.Point((c * ((B * N) + overlap)) + (b * N)),
                        output.Point(b + c * N * B),
                        mode.window,
                        overlap,
                        shift,
                        B);
                }
            } while (++c < CC);

            if (CC == 2 && C == 1)
            {
                for (i = 0; i < B * N; i++)
                {
                    output[i] = Inlines.ADD32(Inlines.HALF32(output[i]), Inlines.HALF32(output[B * N + i]));
                }
            }
            if (upsample != 1)
            {
                c = 0;
                do
                {
                    int bound = B * N / upsample;
                    for (i = 0; i < bound; i++)
                        output[c * B * N + i] *= upsample;
                    output.Point(c * B * N + bound).MemSet(0, B * N - bound);
                } while (++c < C);
            }
        }

        public static void celt_preemphasis(Pointer<int> pcmp, Pointer<int> inp,
                                int N, int CC, int upsample, Pointer<int> coef, BoxedValue<int> mem, int clip)
        {
            int i;
            int coef0;
            int m;
            int Nu;

            coef0 = coef[0];
            m = mem.Val;

            /* Fast path for the normal 48kHz case and no clipping */
            if (coef[1] == 0 && upsample == 1 && clip == 0)
            {
                for (i = 0; i < N; i++)
                {
                    int x;
                    x = Inlines.SCALEIN(pcmp[CC * i]);
                    /* Apply pre-emphasis */
                    inp[i] = Inlines.SHL32(x, CeltConstants.SIG_SHIFT) - m;
                    m = Inlines.SHR32(Inlines.MULT16_16(coef0, x), 15 - CeltConstants.SIG_SHIFT);
                }
                mem.Val = m;
                return;
            }

            Nu = N / upsample;
            if (upsample != 1)
            {
                inp.MemSet(0, N);
            }
            for (i = 0; i < Nu; i++)
                inp[i * upsample] = Inlines.SCALEIN(pcmp[CC * i]);


            for (i = 0; i < N; i++)
            {
                int x;
                x = (inp[i]);
                /* Apply pre-emphasis */
                inp[i] = Inlines.SHL32(x, CeltConstants.SIG_SHIFT) - m;
                m = Inlines.SHR32(Inlines.MULT16_16(coef0, x), 15 - CeltConstants.SIG_SHIFT);
            }

            mem.Val = m;
        }

        public static int l1_metric(Pointer<int> tmp, int N, int LM, int bias)
        {
            int i;
            int L1;
            L1 = 0;
            for (i = 0; i < N; i++)
            {
                L1 += Inlines.EXTEND32(Inlines.ABS32(tmp[i]));
            }

            /* When in doubt, prefer good freq resolution */
            L1 = Inlines.MAC16_32_Q15(L1, (LM * bias), (L1));
            return L1;

        }

        public static int tf_analysis(CELTMode m, int len, int isTransient,
              Pointer<int> tf_res, int lambda, Pointer<int> X, int N0, int LM,
              BoxedValue<int> tf_sum, int tf_estimate, int tf_chan)
        {
            int i;
            Pointer<int> metric;
            int cost0;
            int cost1;
            Pointer<int> path0;
            Pointer<int> path1;
            Pointer<int> tmp;
            Pointer<int> tmp_1;
            int sel;
            int[] selcost = new int[2];
            int tf_select = 0;
            int bias;


            bias = Inlines.MULT16_16_Q14(Inlines.QCONST16(.04f, 15), Inlines.MAX16((short)(0 - Inlines.QCONST16(.25f, 14)), (Inlines.QCONST16(.5f, 14) - tf_estimate)));
            /*printf("%f ", bias);*/

            metric = Pointer.Malloc<int>(len);
            tmp = Pointer.Malloc<int>((m.eBands[len] - m.eBands[len - 1]) << LM);
            tmp_1 = Pointer.Malloc<int>((m.eBands[len] - m.eBands[len - 1]) << LM);
            path0 = Pointer.Malloc<int>(len);
            path1 = Pointer.Malloc<int>(len);

            tf_sum.Val = 0;
            for (i = 0; i < len; i++)
            {
                int k, N;
                int narrow;
                int L1, best_L1;
                int best_level = 0;
                N = (m.eBands[i + 1] - m.eBands[i]) << LM;
                /* band is too narrow to be split down to LM=-1 */
                narrow = ((m.eBands[i + 1] - m.eBands[i]) == 1) ? 1 : 0;
                X.Point(tf_chan * N0 + (m.eBands[i] << LM)).MemCopyTo(tmp, N);
                /* Just add the right channel if we're in stereo */
                /*if (C==2)
                   for (j=0;j<N;j++)
                      tmp[j] = ADD16(SHR16(tmp[j], 1),SHR16(X[N0+j+(m.eBands[i]<<LM)], 1));*/
                L1 = l1_metric(tmp, N, isTransient != 0 ? LM : 0, bias);
                best_L1 = L1;
                /* Check the -1 case for transients */
                if (isTransient != 0 && narrow == 0)
                {
                    tmp.MemCopyTo(tmp_1, N);
                    bands.haar1(tmp_1, N >> LM, 1 << LM);
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

                    bands.haar1(tmp, N >> k, 1 << k);

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
                tf_sum.Val += (isTransient != 0 ? LM : 0) - metric[i] / 2;
                /* For bands that can't be split to -1, set the metric to the half-way point to avoid
                   biasing the decision */
                if (narrow != 0 && (metric[i] == 0 || metric[i] == -2 * LM))
                    metric[i] -= 1;
                /*printf("%d ", metric[i]);*/
            }
            /*printf("\n");*/
            /* Search for the optimal tf resolution, including tf_select */
            tf_select = 0;
            for (sel = 0; sel < 2; sel++)
            {
                cost0 = 0;
                cost1 = isTransient != 0 ? 0 : lambda;
                for (i = 1; i < len; i++)
                {
                    int curr0, curr1;
                    curr0 = Inlines.IMIN(cost0, cost1 + lambda);
                    curr1 = Inlines.IMIN(cost0 + lambda, cost1);
                    cost0 = curr0 + Inlines.abs(metric[i] - 2 * Tables.tf_select_table[LM][4 * isTransient + 2 * sel + 0]);
                    cost1 = curr1 + Inlines.abs(metric[i] - 2 * Tables.tf_select_table[LM][4 * isTransient + 2 * sel + 1]);
                }
                cost0 = Inlines.IMIN(cost0, cost1);
                selcost[sel] = cost0;
            }
            /* For now, we're conservative and only allow tf_select=1 for transients.
             * If tests confirm it's useful for non-transients, we could allow it. */
            if (selcost[1] < selcost[0] && isTransient != 0)
                tf_select = 1;
            cost0 = 0;
            cost1 = isTransient != 0 ? 0 : lambda;
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
                else {
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
                else {
                    curr1 = from1;
                    path1[i] = 1;
                }
                cost0 = curr0 + Inlines.abs(metric[i] - 2 * Tables.tf_select_table[LM][4 * isTransient + 2 * tf_select + 0]);
                cost1 = curr1 + Inlines.abs(metric[i] - 2 * Tables.tf_select_table[LM][4 * isTransient + 2 * tf_select + 1]);
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

#if FUZZING
            Random rand = new Random();
            tf_select = rand.Next() & 0x1;
            tf_res[0] = rand.Next() & 0x1;
            for (i = 1; i < len; i++)
            {
                tf_res[i] = tf_res[i - 1] ^ ((rand.Next() & 0xF) == 0 ? 1 : 0);
            }
#endif
            return tf_select;
        }

        public static void tf_encode(int start, int end, int isTransient, Pointer<int> tf_res, int LM, int tf_select, ec_ctx enc)
        {
            int curr, i;
            int tf_select_rsv;
            int tf_changed;
            int logp;
            uint budget;
            uint tell;
            budget = enc.storage * 8;
            tell = (uint)EntropyCoder.ec_tell(enc);
            logp = isTransient != 0 ? 2 : 4;
            /* Reserve space to code the tf_select decision. */
            tf_select_rsv = (LM > 0 && tell + logp + 1 <= budget) ? 1 : 0;
            budget -= (uint)tf_select_rsv;
            curr = tf_changed = 0;
            for (i = start; i < end; i++)
            {
                if (tell + logp <= budget)
                {
                    EntropyCoder.ec_enc_bit_logp(enc, tf_res[i] ^ curr, (uint)logp);
                    tell = (uint)EntropyCoder.ec_tell(enc);
                    curr = tf_res[i];
                    tf_changed |= curr;
                }
                else
                    tf_res[i] = curr;
                logp = isTransient != 0 ? 4 : 5;
            }
            /* Only code tf_select if it would actually make a difference. */
            if (tf_select_rsv != 0 &&
                  Tables.tf_select_table[LM][4 * isTransient + 0 + tf_changed] !=
                  Tables.tf_select_table[LM][4 * isTransient + 2 + tf_changed])
                EntropyCoder.ec_enc_bit_logp(enc, tf_select, 1);
            else
                tf_select = 0;
            for (i = start; i < end; i++)
                tf_res[i] = Tables.tf_select_table[LM][4 * isTransient + 2 * tf_select + tf_res[i]];
            /*for(i=0;i<end;i++)printf("%d ", isTransient ? tf_res[i] : LM+tf_res[i]);printf("\n");*/
        }


        public static int alloc_trim_analysis(CELTMode m, Pointer<int> X,
              Pointer<int> bandLogE, int end, int LM, int C, int N0,
              AnalysisInfo analysis, BoxedValue<int> stereo_saving, int tf_estimate,
              int intensity, int surround_trim)
        {
            int i;
            int diff = 0;
            int c;
            int trim_index;
            int trim = Inlines.QCONST16(5.0f, 8);
            int logXC, logXC2;
            if (C == 2)
            {
                int sum = 0; /* Q10 */
                int minXC; /* Q10 */
                           /* Compute inter-channel correlation for low frequencies */
                for (i = 0; i < 8; i++)
                {
                    int partial;
                    partial = celt_inner_prod.celt_inner_prod_c(X.Point(m.eBands[i] << LM), X.Point(N0 + (m.eBands[i] << LM)),
                          (m.eBands[i + 1] - m.eBands[i]) << LM);
                    sum = Inlines.ADD16(sum, Inlines.EXTRACT16(Inlines.SHR32(partial, 18)));
                }
                sum = Inlines.MULT16_16_Q15(Inlines.QCONST16(1.0f / 8, 15), sum);
                sum = Inlines.MIN16(Inlines.QCONST16(1.0f, 10), Inlines.ABS32(sum));
                minXC = sum;
                for (i = 8; i < intensity; i++)
                {
                    int partial;
                    partial = celt_inner_prod.celt_inner_prod_c(X.Point(m.eBands[i] << LM), X.Point(N0 + (m.eBands[i] << LM)),
                          (m.eBands[i + 1] - m.eBands[i]) << LM);
                    minXC = Inlines.MIN16(minXC, Inlines.ABS16(Inlines.EXTRACT16(Inlines.SHR32(partial, 18))));
                }
                minXC = Inlines.MIN16(Inlines.QCONST16(1.0f, 10), Inlines.ABS32(minXC));
                /*printf ("%f\n", sum);*/
                /* mid-side savings estimations based on the LF average*/
                logXC = Inlines.celt_log2(Inlines.QCONST32(1.001f, 20) - Inlines.MULT16_16(sum, sum));
                /* mid-side savings estimations based on min correlation */
                logXC2 = Inlines.MAX16(Inlines.HALF16(logXC), Inlines.celt_log2(Inlines.QCONST32(1.001f, 20) - Inlines.MULT16_16(minXC, minXC)));
                /* Compensate for Q20 vs Q14 input and convert output to Q8 */
                logXC = (Inlines.PSHR32(logXC - Inlines.QCONST16(6.0f, CeltConstants.DB_SHIFT), CeltConstants.DB_SHIFT - 8));
                logXC2 = (Inlines.PSHR32(logXC2 - Inlines.QCONST16(6.0f, CeltConstants.DB_SHIFT), CeltConstants.DB_SHIFT - 8));

                trim += Inlines.MAX16((0 - Inlines.QCONST16(4.0f, 8)), Inlines.MULT16_16_Q15(Inlines.QCONST16(.75f, 15), logXC));
                stereo_saving.Val = Inlines.MIN16((stereo_saving.Val + Inlines.QCONST16(0.25f, 8)), (0 - Inlines.HALF16(logXC2)));
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
            trim -= Inlines.MAX16(Inlines.NEG16(Inlines.QCONST16(2.0f, 8)), Inlines.MIN16(Inlines.QCONST16(2.0f, 8), (Inlines.SHR16((diff + Inlines.QCONST16(1.0f, CeltConstants.DB_SHIFT)), CeltConstants.DB_SHIFT - 8) / 6)));
            trim -= Inlines.SHR16(surround_trim, CeltConstants.DB_SHIFT - 8);
            trim = (trim - 2 * Inlines.SHR16(tf_estimate, 14 - 8));
#if ENABLE_ANALYSIS
            if (analysis.valid != 0)
            {
                trim -= Inlines.MAX16(-Inlines.QCONST16(2.0f, 8), Inlines.MIN16(Inlines.QCONST16(2.0f, 8),
                      (int)(Inlines.QCONST16(2.0f, 8) * (analysis.tonality_slope + .05f))));
            }
#endif
            trim_index = Inlines.PSHR32(trim, 8);
            trim_index = Inlines.IMAX(0, Inlines.IMIN(10, trim_index));
            /*printf("%d\n", trim_index);*/
#if FUZZING
            trim_index = new Random().Next() % 11;
#endif
            return trim_index;
        }

        public static int stereo_analysis(CELTMode m, Pointer<int> X,
              int LM, int N0)
        {
            int i;
            int thetas;
            int sumLR = CeltConstants.EPSILON, sumMS = CeltConstants.EPSILON;

            /* Use the L1 norm to model the entropy of the L/R signal vs the M/S signal */
            for (i = 0; i < 13; i++)
            {
                int j;
                for (j = m.eBands[i] << LM; j < m.eBands[i + 1] << LM; j++)
                {
                    int L, R, M, S;
                    /* We cast to 32-bit first because of the -32768 case */
                    L = Inlines.EXTEND32(X[j]);
                    R = Inlines.EXTEND32(X[N0 + j]);
                    M = Inlines.ADD32(L, R);
                    S = Inlines.SUB32(L, R);
                    sumLR = Inlines.ADD32(sumLR, Inlines.ADD32(Inlines.ABS32(L), Inlines.ABS32(R)));
                    sumMS = Inlines.ADD32(sumMS, Inlines.ADD32(Inlines.ABS32(M), Inlines.ABS32(S)));
                }
            }
            sumMS = Inlines.MULT16_32_Q15(Inlines.QCONST16(0.707107f, 15), sumMS);
            thetas = 13;
            /* We don't need thetas for lower bands with LM<=1 */
            if (LM <= 1)
                thetas -= 8;
            return (Inlines.MULT16_32_Q15(((m.eBands[13] << (LM + 1)) + thetas), sumMS)
                  > Inlines.MULT16_32_Q15((m.eBands[13] << (LM + 1)), sumLR)) ? 1 : 0;
        }

        public static int median_of_5(Pointer<int> x)
        {
            int t0, t1, t2, t3, t4;
            t2 = x[2];
            if (x[0] > x[1])
            {
                t0 = x[1];
                t1 = x[0];
            }
            else {
                t0 = x[0];
                t1 = x[1];
            }
            if (x[3] > x[4])
            {
                t3 = x[4];
                t4 = x[3];
            }
            else {
                t3 = x[3];
                t4 = x[4];
            }
            if (t0 > t3)
            {
                // swap the pairs
                int tmp = t3;
                t3 = t0;
                t0 = tmp;
                tmp = t4;
                t4 = t1;
                t1 = tmp;
            }
            if (t2 > t1)
            {
                if (t1 < t3)
                    return Inlines.MIN16(t2, t3);
                else
                    return Inlines.MIN16(t4, t1);
            }
            else {
                if (t2 < t3)
                    return Inlines.MIN16(t1, t3);
                else
                    return Inlines.MIN16(t2, t4);
            }
        }

        public static int median_of_3(Pointer<int> x)
        {
            int t0, t1, t2;
            if (x[0] > x[1])
            {
                t0 = x[1];
                t1 = x[0];
            }
            else {
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

        public static int dynalloc_analysis(Pointer<int> bandLogE, Pointer<int> bandLogE2,
              int nbEBands, int start, int end, int C, Pointer<int> offsets, int lsb_depth, Pointer<short> logN,
              int isTransient, int vbr, int constrained_vbr, Pointer<short> eBands, int LM,
              int effectiveBytes, BoxedValue<int> tot_boost_, int lfe, Pointer<int> surround_dynalloc)
        {
            int i, c;
            int tot_boost = 0;
            int maxDepth;
            Pointer<int> follower = Pointer.Malloc<int>(C * nbEBands);
            Pointer<int> noise_floor = Pointer.Malloc<int>(C * nbEBands);

            offsets.MemSet(0, nbEBands);
            /* Dynamic allocation code */
            maxDepth = (0 - Inlines.QCONST16(31.9f, CeltConstants.DB_SHIFT));
            for (i = 0; i < end; i++)
            {
                /* Noise floor must take into account eMeans, the depth, the width of the bands
                   and the preemphasis filter (approx. square of bark band ID) */
                noise_floor[i] = (Inlines.MULT16_16(Inlines.QCONST16(0.0625f, CeltConstants.DB_SHIFT), logN[i])
                      + Inlines.QCONST16(.5f, CeltConstants.DB_SHIFT) + Inlines.SHL16((9 - lsb_depth), CeltConstants.DB_SHIFT) - Inlines.SHL16(Tables.eMeans[i], 6)
                      + Inlines.MULT16_16(Inlines.QCONST16(0.0062f, CeltConstants.DB_SHIFT), (i + 5) * (i + 5)));
            }
            c = 0; do
            {
                for (i = 0; i < end; i++)
                    maxDepth = Inlines.MAX16(maxDepth, (bandLogE[c * nbEBands + i] - noise_floor[i]));
            } while (++c < C);
            /* Make sure that dynamic allocation can't make us bust the budget */
            if (effectiveBytes > 50 && LM >= 1 && lfe == 0)
            {
                int last = 0;
                c = 0; do
                {
                    int offset;
                    int tmp;
                    Pointer<int> f = follower.Point(c * nbEBands);
                    f[0] = bandLogE2[c * nbEBands];
                    for (i = 1; i < end; i++)
                    {
                        /* The last band to be at least 3 dB higher than the previous one
                           is the last we'll consider. Otherwise, we run into problems on
                           bandlimited signals. */
                        if (bandLogE2[c * nbEBands + i] > bandLogE2[c * nbEBands + i - 1] + Inlines.QCONST16(0.5f, CeltConstants.DB_SHIFT))
                            last = i;
                        f[i] = Inlines.MIN16((f[i - 1] + Inlines.QCONST16(1.5f, CeltConstants.DB_SHIFT)), bandLogE2[c * nbEBands + i]);
                    }
                    for (i = last - 1; i >= 0; i--)
                        f[i] = Inlines.MIN16(f[i], Inlines.MIN16((f[i + 1] + Inlines.QCONST16(2.0f, CeltConstants.DB_SHIFT)), bandLogE2[c * nbEBands + i]));

                    /* Combine with a median filter to avoid dynalloc triggering unnecessarily.
                       The "offset" value controls how conservative we are -- a higher offset
                       reduces the impact of the median filter and makes dynalloc use more bits. */
                    offset = Inlines.QCONST16(1.0f, CeltConstants.DB_SHIFT);
                    for (i = 2; i < end - 2; i++)
                        f[i] = Inlines.MAX16(f[i], median_of_5(bandLogE2.Point(c * nbEBands + i - 2)) - offset);
                    tmp = median_of_3(bandLogE2.Point(c * nbEBands)) - offset;
                    f[0] = Inlines.MAX16(f[0], tmp);
                    f[1] = Inlines.MAX16(f[1], tmp);
                    tmp = median_of_3(bandLogE2.Point(c * nbEBands + end - 3)) - offset;
                    f[end - 2] = Inlines.MAX16(f[end - 2], tmp);
                    f[end - 1] = Inlines.MAX16(f[end - 1], tmp);

                    for (i = 0; i < end; i++)
                        f[i] = Inlines.MAX16(f[i], noise_floor[i]);
                } while (++c < C);
                if (C == 2)
                {
                    for (i = start; i < end; i++)
                    {
                        /* Consider 24 dB "cross-talk" */
                        follower[nbEBands + i] = Inlines.MAX16(follower[nbEBands + i], follower[i] - Inlines.QCONST16(4.0f, CeltConstants.DB_SHIFT));
                        follower[i] = Inlines.MAX16(follower[i], follower[nbEBands + i] - Inlines.QCONST16(4.0f, CeltConstants.DB_SHIFT));
                        follower[i] = Inlines.HALF16(Inlines.MAX16(0, bandLogE[i] - follower[i]) + Inlines.MAX16(0, bandLogE[nbEBands + i] - follower[nbEBands + i]));
                    }
                }
                else {
                    for (i = start; i < end; i++)
                    {
                        follower[i] = Inlines.MAX16(0, bandLogE[i] - follower[i]);
                    }
                }
                for (i = start; i < end; i++)
                    follower[i] = Inlines.MAX16(follower[i], surround_dynalloc[i]);
                /* For non-transient CBR/CVBR frames, halve the dynalloc contribution */
                if ((vbr == 0 || constrained_vbr != 0) && isTransient == 0)
                {
                    for (i = start; i < end; i++)
                        follower[i] = Inlines.HALF16(follower[i]);
                }
                for (i = start; i < end; i++)
                {
                    int width;
                    int boost;
                    int boost_bits;

                    if (i < 8)
                        follower[i] *= 2;
                    if (i >= 12)
                        follower[i] = Inlines.HALF16(follower[i]);
                    follower[i] = Inlines.MIN16(follower[i], Inlines.QCONST16(4, CeltConstants.DB_SHIFT));

                    width = C * (eBands[i + 1] - eBands[i]) << LM;
                    if (width < 6)
                    {
                        boost = (int)Inlines.SHR32((follower[i]), CeltConstants.DB_SHIFT);
                        boost_bits = boost * width << EntropyCoder.BITRES;
                    }
                    else if (width > 48)
                    {
                        boost = (int)Inlines.SHR32((follower[i]) * 8, CeltConstants.DB_SHIFT);
                        boost_bits = (boost * width << EntropyCoder.BITRES) / 8;
                    }
                    else {
                        boost = (int)Inlines.SHR32((follower[i]) * width / 6, CeltConstants.DB_SHIFT);
                        boost_bits = boost * 6 << EntropyCoder.BITRES;
                    }
                    /* For CBR and non-transient CVBR frames, limit dynalloc to 1/4 of the bits */
                    if ((vbr == 0 || (constrained_vbr != 0 && isTransient == 0))
                          && (tot_boost + boost_bits) >> EntropyCoder.BITRES >> 3 > effectiveBytes / 4)
                    {
                        int cap = ((effectiveBytes / 4) << EntropyCoder.BITRES << 3);
                        offsets[i] = cap - tot_boost;
                        tot_boost = cap;
                        break;
                    }
                    else {
                        offsets[i] = boost;
                        tot_boost += boost_bits;
                    }
                }
            }

            tot_boost_.Val = tot_boost;

            return maxDepth;
        }


        public static int run_prefilter(CELTEncoder st, Pointer<int> input, Pointer<int> prefilter_mem, int CC, int N,
              int prefilter_tapset, BoxedValue<int> pitch, BoxedValue<int> gain, BoxedValue<int> qgain, int enabled, int nbAvailableBytes)
        {
            int c;
            Pointer<int> _pre;
            Pointer<Pointer<int>> pre = Pointer.Malloc<Pointer<int>>(2);
            CELTMode mode; // [porting note] pointer
            BoxedValue<int> pitch_index = new BoxedValue<int>();
            int gain1;
            int pf_threshold;
            int pf_on;
            int qg;
            int overlap;

            mode = st.mode;
            overlap = mode.overlap;
            _pre = Pointer.Malloc<int>(CC * (N + CeltConstants.COMBFILTER_MAXPERIOD));

            pre[0] = _pre;
            pre[1] = _pre.Point(N + CeltConstants.COMBFILTER_MAXPERIOD);

            c = 0;
            do
            {
                prefilter_mem.Point(c * CeltConstants.COMBFILTER_MAXPERIOD).MemCopyTo(pre[c], CeltConstants.COMBFILTER_MAXPERIOD);
                input.Point(c * (N + overlap) + overlap).MemCopyTo(pre[c].Point(CeltConstants.COMBFILTER_MAXPERIOD), N);
            } while (++c < CC);

            if (enabled != 0)
            {
                Pointer<int> pitch_buf = Pointer.Malloc<int>((CeltConstants.COMBFILTER_MAXPERIOD + N) >> 1);

                Concentus.Celt.pitch.pitch_downsample(pre, pitch_buf, CeltConstants.COMBFILTER_MAXPERIOD + N, CC);
                /* Don't search for the fir last 1.5 octave of the range because
                   there's too many false-positives due to short-term correlation */
                Concentus.Celt.pitch.pitch_search(pitch_buf.Point(CeltConstants.COMBFILTER_MAXPERIOD >> 1), pitch_buf, N,
                      CeltConstants.COMBFILTER_MAXPERIOD - 3 * CeltConstants.COMBFILTER_MINPERIOD, pitch_index);
                pitch_index.Val = CeltConstants.COMBFILTER_MAXPERIOD - pitch_index.Val;
                gain1 = Concentus.Celt.pitch.remove_doubling(pitch_buf, CeltConstants.COMBFILTER_MAXPERIOD, CeltConstants.COMBFILTER_MINPERIOD,
                      N, pitch_index, st.prefilter_period, st.prefilter_gain);
                if (pitch_index.Val > CeltConstants.COMBFILTER_MAXPERIOD - 2)
                    pitch_index.Val = CeltConstants.COMBFILTER_MAXPERIOD - 2;
                gain1 = Inlines.MULT16_16_Q15(Inlines.QCONST16(.7f, 15), gain1);
                /*printf("%d %d %f %f\n", pitch_change, pitch_index, gain1, st.analysis.tonality);*/
                if (st.loss_rate > 2)
                    gain1 = Inlines.HALF32(gain1);
                if (st.loss_rate > 4)
                    gain1 = Inlines.HALF32(gain1);
                if (st.loss_rate > 8)
                    gain1 = 0;
            }
            else {
                gain1 = 0;
                pitch_index.Val = CeltConstants.COMBFILTER_MINPERIOD;
            }

            /* Gain threshold for enabling the prefilter/postfilter */
            pf_threshold = Inlines.QCONST16(.2f, 15);

            /* Adjusting the threshold based on rate and continuity */
            if (Inlines.abs(pitch_index.Val - st.prefilter_period) * 10 > pitch_index.Val)
                pf_threshold += Inlines.QCONST16(.2f, 15);
            if (nbAvailableBytes < 25)
                pf_threshold += Inlines.QCONST16(.1f, 15);
            if (nbAvailableBytes < 35)
                pf_threshold += Inlines.QCONST16(.1f, 15);
            if (st.prefilter_gain > Inlines.QCONST16(.4f, 15))
                pf_threshold -= Inlines.QCONST16(.1f, 15);
            if (st.prefilter_gain > Inlines.QCONST16(.55f, 15))
                pf_threshold -= Inlines.QCONST16(.1f, 15);

            /* Hard threshold at 0.2 */
            pf_threshold = Inlines.MAX16(pf_threshold, Inlines.QCONST16(.2f, 15));

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
                if (Inlines.ABS32(gain1 - st.prefilter_gain) < Inlines.QCONST16(.1f, 15))
                    gain1 = st.prefilter_gain;

                qg = ((gain1 + 1536) >> 10) / 3 - 1;
                qg = Inlines.IMAX(0, Inlines.IMIN(7, qg));
                gain1 = Inlines.QCONST16(0.09375f, 15) * (qg + 1);
                pf_on = 1;
            }
            /*printf("%d %f\n", pitch_index, gain1);*/

            c = 0;
            do
            {
                int offset = mode.shortMdctSize - overlap;
                st.prefilter_period = Inlines.IMAX(st.prefilter_period, CeltConstants.COMBFILTER_MINPERIOD);
                st.in_mem.Point(c * overlap).MemCopyTo(input.Point(c * (N + overlap)), overlap);
                if (offset != 0)
                {
                    celt.comb_filter(input.Point(c * (N + overlap) + overlap), pre[c].Point(CeltConstants.COMBFILTER_MAXPERIOD),
                          st.prefilter_period, st.prefilter_period, offset, -st.prefilter_gain, -st.prefilter_gain,
                          st.prefilter_tapset, st.prefilter_tapset, null, 0);
                }

                celt.comb_filter(input.Point(c * (N + overlap) + overlap + offset), pre[c].Point(CeltConstants.COMBFILTER_MAXPERIOD + offset),
                      st.prefilter_period, pitch_index.Val, N - offset, -st.prefilter_gain, -gain1,
                      st.prefilter_tapset, prefilter_tapset, mode.window, overlap);
                input.Point(c * (N + overlap) + N).MemCopyTo(st.in_mem.Point(c * overlap), overlap);

                if (N > CeltConstants.COMBFILTER_MAXPERIOD)
                {
                    pre[c].Point(N).MemMoveTo(prefilter_mem.Point(c * CeltConstants.COMBFILTER_MAXPERIOD), CeltConstants.COMBFILTER_MAXPERIOD);
                }
                else
                {
                    prefilter_mem.Point(c * CeltConstants.COMBFILTER_MAXPERIOD + N).MemMoveTo(prefilter_mem.Point(c * CeltConstants.COMBFILTER_MAXPERIOD), CeltConstants.COMBFILTER_MAXPERIOD - N);
                    pre[c].Point(CeltConstants.COMBFILTER_MAXPERIOD).MemMoveTo(prefilter_mem.Point(c * CeltConstants.COMBFILTER_MAXPERIOD + CeltConstants.COMBFILTER_MAXPERIOD - N), N);
                }
            } while (++c < CC);


            gain.Val = gain1;
            pitch.Val = pitch_index.Val;
            qgain.Val = qg;
            return pf_on;
        }

        public static int compute_vbr(CELTMode mode, AnalysisInfo analysis, int base_target,
              int LM, int bitrate, int lastCodedBands, int C, int intensity,
              int constrained_vbr, int stereo_saving, int tot_boost,
              int tf_estimate, int pitch_change, int maxDepth,
              int variable_duration, int lfe, int has_surround_mask, int surround_masking,
              int temporal_vbr)
        {
            /* The target rate in 8th bits per frame */
            int target;
            int coded_bins;
            int coded_bands;
            int tf_calibration;
            int nbEBands;
            Pointer<short> eBands;

            nbEBands = mode.nbEBands;
            eBands = mode.eBands;

            coded_bands = lastCodedBands != 0 ? lastCodedBands : nbEBands;
            coded_bins = eBands[coded_bands] << LM;
            if (C == 2)
                coded_bins += eBands[Inlines.IMIN(intensity, coded_bands)] << LM;

            target = base_target;
#if ENABLE_ANALYSIS
            if (analysis.valid != 0 && analysis.activity < .4)
                target -= (int)((coded_bins << EntropyCoder.BITRES) * (.4f - analysis.activity));
#endif
            /* Stereo savings */
            if (C == 2)
            {
                int coded_stereo_bands;
                int coded_stereo_dof;
                int max_frac;
                coded_stereo_bands = Inlines.IMIN(intensity, coded_bands);
                coded_stereo_dof = (eBands[coded_stereo_bands] << LM) - coded_stereo_bands;
                /* Maximum fraction of the bits we can save if the signal is mono. */
                max_frac = Inlines.DIV32_16(Inlines.MULT16_16(Inlines.QCONST16(0.8f, 15), coded_stereo_dof), coded_bins);
                stereo_saving = Inlines.MIN16(stereo_saving, Inlines.QCONST16(1.0f, 8));
                /*printf("%d %d %d ", coded_stereo_dof, coded_bins, tot_boost);*/
                target -= (int)Inlines.MIN32(Inlines.MULT16_32_Q15(max_frac, target),
                                Inlines.SHR32(Inlines.MULT16_16(stereo_saving - Inlines.QCONST16(0.1f, 8), (coded_stereo_dof << EntropyCoder.BITRES)), 8));
            }
            /* Boost the rate according to dynalloc (minus the dynalloc average for calibration). */
            target += tot_boost - (16 << LM);
            /* Apply transient boost, compensating for average boost. */
            tf_calibration = variable_duration == OpusFramesize.OPUS_FRAMESIZE_VARIABLE ?
                             Inlines.QCONST16(0.02f, 14) : Inlines.QCONST16(0.04f, 14);
            target += (int)Inlines.SHL32(Inlines.MULT16_32_Q15(tf_estimate - tf_calibration, target), 1);

#if ENABLE_ANALYSIS
            /* Apply tonality boost */
            if (analysis.valid != 0 && lfe == 0)
            {
                int tonal_target;
                float tonal;

                /* Tonality boost (compensating for the average). */
                tonal = Inlines.MAX16(0, analysis.tonality - .15f) - 0.09f;
                tonal_target = target + (int)((coded_bins << EntropyCoder.BITRES) * 1.2f * tonal);
                if (pitch_change != 0)
                    tonal_target += (int)((coded_bins << EntropyCoder.BITRES) * .8f);
                target = tonal_target;
            }
#endif

            if (has_surround_mask != 0 && lfe == 0)
            {
                int surround_target = target + (int)Inlines.SHR32(Inlines.MULT16_16(surround_masking, coded_bins << EntropyCoder.BITRES), CeltConstants.DB_SHIFT);
                /*printf("%f %d %d %d %d %d %d ", surround_masking, coded_bins, st.end, st.intensity, surround_target, target, st.bitrate);*/
                target = Inlines.IMAX(target / 4, surround_target);
            }

            {
                int floor_depth;
                int bins;
                bins = eBands[nbEBands - 2] << LM;
                /*floor_depth = Inlines.SHR32(Inlines.MULT16_16((C*bins<<BITRES),celt_log2(Inlines.SHL32(Inlines.MAX16(1,sample_max),13))), CeltConstants.DB_SHIFT);*/
                floor_depth = (int)Inlines.SHR32(Inlines.MULT16_16((C * bins << EntropyCoder.BITRES), maxDepth), CeltConstants.DB_SHIFT);
                floor_depth = Inlines.IMAX(floor_depth, target >> 2);
                target = Inlines.IMIN(target, floor_depth);
                /*printf("%f %d\n", maxDepth, floor_depth);*/
            }

            if ((has_surround_mask == 0 || lfe != 0) && (constrained_vbr != 0 || bitrate < 64000))
            {
                int rate_factor;
                rate_factor = Inlines.MAX16(0, (bitrate - 32000));
                if (constrained_vbr != 0)
                    rate_factor = Inlines.MIN16(rate_factor, Inlines.QCONST16(0.67f, 15));
                target = base_target + (int)Inlines.MULT16_32_Q15(rate_factor, target - base_target);
                }

            if (has_surround_mask == 0 && tf_estimate < Inlines.QCONST16(.2f, 14))
            {
                int amount;
                int tvbr_factor;
                amount = Inlines.MULT16_16_Q15(Inlines.QCONST16(.0000031f, 30), Inlines.IMAX(0, Inlines.IMIN(32000, 96000 - bitrate)));
                tvbr_factor = Inlines.SHR32(Inlines.MULT16_16(temporal_vbr, amount), CeltConstants.DB_SHIFT);
                target += (int)Inlines.MULT16_32_Q15(tvbr_factor, target);
            }

            /* Don't allow more than doubling the rate */
            target = Inlines.IMIN(2 * base_target, target);

            return target;
        }

        public static int celt_encode_with_ec(CELTEncoder st, Pointer<int> pcm, int frame_size, Pointer<byte> compressed, int nbCompressedBytes, ec_ctx enc)
        {
            int i, c, N;
            int bits;
            Pointer<int> input;
            Pointer<int> freq;
            Pointer<int> X;
            Pointer<int> bandE;
            Pointer<int> bandLogE;
            Pointer<int> bandLogE2;
            Pointer<int> fine_quant;
            Pointer<int> error;
            Pointer<int> pulses;
            Pointer<int> cap;
            Pointer<int> offsets;
            Pointer<int> fine_priority;
            Pointer<int> tf_res;
            Pointer<byte> collapse_masks;
            Pointer<int> prefilter_mem;
            Pointer<int> oldBandE, oldLogE, oldLogE2;
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
            int tf_sum;
            int alloc_trim;
            int pitch_index = CeltConstants.COMBFILTER_MINPERIOD;
            int gain1 = 0;
            int dual_stereo = 0;
            int effectiveBytes;
            int dynalloc_logp;
            int vbr_rate;
            int total_bits;
            int total_boost;
            int balance;
            int tell;
            int prefilter_tapset = 0;
            int pf_on;
            int anti_collapse_rsv;
            int anti_collapse_on = 0;
            int silence = 0;
            int tf_chan = 0;
            int tf_estimate;
            int pitch_change = 0;
            int tot_boost;
            int sample_max;
            int maxDepth;
            CELTMode mode;
            int nbEBands;
            int overlap;
            Pointer<short> eBands;
            int secondMdct;
            int signalBandwidth;
            int transient_got_disabled = 0;
            int surround_masking = 0;
            int temporal_vbr = 0;
            int surround_trim = 0;
            int equiv_rate = 510000;
            Pointer<int> surround_dynalloc;

            mode = st.mode;
            nbEBands = mode.nbEBands;
            overlap = mode.overlap;
            eBands = mode.eBands;
            start = st.start;
            end = st.end;
            tf_estimate = 0;
            if (nbCompressedBytes < 2 || pcm == null)
            {
                return OpusError.OPUS_BAD_ARG;
            }

            frame_size *= st.upsample;
            for (LM = 0; LM <= mode.maxLM; LM++)
                if (mode.shortMdctSize << LM == frame_size)
                    break;
            if (LM > mode.maxLM)
            {
                return OpusError.OPUS_BAD_ARG;
            }
            M = 1 << LM;
            N = M * mode.shortMdctSize;

            // fixme: can remove these temp pointers?
            prefilter_mem = st.prefilter_mem;
            oldBandE = st.oldBandE;
            oldLogE = st.oldLogE;
            oldLogE2 = st.oldLogE2;

            if (enc == null)
            {
                tell = 1;
                nbFilledBytes = 0;
            }
            else {
                tell = EntropyCoder.ec_tell(enc);
                nbFilledBytes = (tell + 4) >> 3;
            }

            Inlines.OpusAssert(st.signalling == 0);

            /* Can't produce more than 1275 output bytes */
            nbCompressedBytes = Inlines.IMIN(nbCompressedBytes, 1275);
            nbAvailableBytes = nbCompressedBytes - nbFilledBytes;

            if (st.vbr != 0 && st.bitrate != OpusConstants.OPUS_BITRATE_MAX)
            {
                int den = mode.Fs >> EntropyCoder.BITRES;
                vbr_rate = (st.bitrate * frame_size + (den >> 1)) / den;
                effectiveBytes = vbr_rate >> (3 + EntropyCoder.BITRES);
            }
            else {
                int tmp;
                vbr_rate = 0;
                tmp = st.bitrate * frame_size;
                if (tell > 1)
                    tmp += tell;
                if (st.bitrate != OpusConstants.OPUS_BITRATE_MAX)
                    nbCompressedBytes = Inlines.IMAX(2, Inlines.IMIN(nbCompressedBytes,
                          (tmp + 4 * mode.Fs) / (8 * mode.Fs) - (st.signalling != 0 ? 1 : 0))); // fixme - this used weird syntax originally, double-check it
                effectiveBytes = nbCompressedBytes;
            }
            if (st.bitrate != OpusConstants.OPUS_BITRATE_MAX)
                equiv_rate = st.bitrate - (40 * C + 20) * ((400 >> LM) - 50);

            if (enc == null)
            {
                enc = new ec_ctx();
                EntropyCoder.ec_enc_init(enc, compressed, (uint)nbCompressedBytes);
            }

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
                    max_allowed = Inlines.IMIN(Inlines.IMAX(tell == 1 ? 2 : 0,
                          (vbr_rate + vbr_bound - st.vbr_reservoir) >> (EntropyCoder.BITRES + 3)),
                          nbAvailableBytes);
                    if (max_allowed < nbAvailableBytes)
                    {
                        nbCompressedBytes = nbFilledBytes + max_allowed;
                        nbAvailableBytes = max_allowed;
                        EntropyCoder.ec_enc_shrink(enc, (uint)nbCompressedBytes);
                    }
                }
            }
            total_bits = nbCompressedBytes * 8;

            effEnd = end;
            if (effEnd > mode.effEBands)
                effEnd = mode.effEBands;

            input = Pointer.Malloc<int>(CC * (N + overlap));

            sample_max = Inlines.MAX32(st.overlap_max, Inlines.celt_maxabs32(pcm, C * (N - overlap) / st.upsample));
            st.overlap_max = Inlines.celt_maxabs32(pcm.Point(C * (N - overlap) / st.upsample), C * overlap / st.upsample);
            sample_max = Inlines.MAX32(sample_max, st.overlap_max);
            silence = (sample_max == 0) ? 1 : 0;
#if FUZZING
            if ((new Random().Next() & 0x3F) == 0)
                silence = 1;
#endif
            if (tell == 1)
                EntropyCoder.ec_enc_bit_logp(enc, silence, 15);
            else
                silence = 0;
            if (silence != 0)
            {
                /*In VBR mode there is no need to send more than the minimum. */
                if (vbr_rate > 0)
                {
                    effectiveBytes = nbCompressedBytes = Inlines.IMIN(nbCompressedBytes, nbFilledBytes + 2);
                    total_bits = nbCompressedBytes * 8;
                    nbAvailableBytes = 2;
                    EntropyCoder.ec_enc_shrink(enc, (uint)nbCompressedBytes);
                }
                /* Pretend we've filled all the remaining bits with zeros
                      (that's what the initialiser did anyway) */
                tell = nbCompressedBytes * 8;
                enc.nbits_total += tell - EntropyCoder.ec_tell(enc);
            }
            c = 0;
            do
            {
                int need_clip = 0;
                BoxedValue<int> preemph_mem_boxed = new BoxedValue<int>(st.preemph_memE[c]);
                celt_preemphasis(pcm.Point(c), input.Point(c * (N + overlap) + overlap), N, CC, st.upsample,
                            mode.preemph.GetPointer(), preemph_mem_boxed, need_clip);
                st.preemph_memE[c] = preemph_mem_boxed.Val;
            } while (++c < CC);

            /* Find pitch period and gain */
            {
                int enabled;
                int qg;
                enabled = (((st.lfe != 0 && nbAvailableBytes > 3) || nbAvailableBytes > 12 * C) && start == 0 && silence == 0 && st.disable_pf == 0
                      && st.complexity >= 5 && !(st.consec_transient != 0 && LM != 3 && st.variable_duration == OpusFramesize.OPUS_FRAMESIZE_VARIABLE)) ? 1 : 0;

                prefilter_tapset = st.tapset_decision;
                BoxedValue<int> boxed_pitch_index = new BoxedValue<int>(pitch_index);
                BoxedValue<int> boxed_gain1 = new BoxedValue<int>(gain1);
                BoxedValue<int> boxed_qg = new BoxedValue<int>();
                pf_on = run_prefilter(st, input, prefilter_mem, CC, N, prefilter_tapset, boxed_pitch_index, boxed_gain1, boxed_qg, enabled, nbAvailableBytes);
                pitch_index = boxed_pitch_index.Val;
                gain1 = boxed_gain1.Val;
                qg = boxed_qg.Val;

                if ((gain1 > Inlines.QCONST16(.4f, 15) || st.prefilter_gain > Inlines.QCONST16(.4f, 15)) && (st.analysis.valid == 0 || st.analysis.tonality > .3)
                      && (pitch_index > 1.26 * st.prefilter_period || pitch_index < .79 * st.prefilter_period))
                    pitch_change = 1;
                if (pf_on == 0)
                {
                    if (start == 0 && tell + 16 <= total_bits)
                        EntropyCoder.ec_enc_bit_logp(enc, 0, 1);
                }
                else {
                    /*This block is not gated by a total bits check only because
                      of the nbAvailableBytes check above.*/
                    int octave;
                    EntropyCoder.ec_enc_bit_logp(enc, 1, 1);
                    pitch_index += 1;
                    octave = Inlines.EC_ILOG((uint)pitch_index) - 5;
                    EntropyCoder.ec_enc_uint(enc, (uint)octave, 6);
                    EntropyCoder.ec_enc_bits(enc, (uint)(pitch_index - (16 << octave)), (uint)(4 + octave));
                    pitch_index -= 1;
                    EntropyCoder.ec_enc_bits(enc, (uint)qg, 3);
                    EntropyCoder.ec_enc_icdf(enc, prefilter_tapset, Tables.tapset_icdf.GetPointer(), 2);
                }
            }

            isTransient = 0;
            shortBlocks = 0;
            if (st.complexity >= 1 && st.lfe == 0)
            {
                BoxedValue<int> boxed_tf_estimate = new BoxedValue<int>(tf_estimate);
                BoxedValue<int> boxed_tf_chan = new BoxedValue<int>(tf_chan);
                isTransient = transient_analysis(input, N + overlap, CC,
                      boxed_tf_estimate, boxed_tf_chan);
                tf_estimate = boxed_tf_estimate.Val;
                tf_chan = boxed_tf_chan.Val;
            }

            if (LM > 0 && EntropyCoder.ec_tell(enc) + 3 <= total_bits)
            {
                if (isTransient != 0)
                    shortBlocks = M;
            }
            else {
                isTransient = 0;
                transient_got_disabled = 1;
            }

            freq = Pointer.Malloc<int>(CC * N); /**< Interleaved signal MDCTs */
            bandE = Pointer.Malloc<int>(nbEBands * CC);
            bandLogE = Pointer.Malloc<int>(nbEBands * CC);

            secondMdct = (shortBlocks != 0 && st.complexity >= 8) ? 1 : 0;
            bandLogE2 = Pointer.Malloc<int>(C * nbEBands);
            bandLogE2.MemSet(0, C * nbEBands); // FIXME: TEMPORARY
            if (secondMdct != 0)
            {
                compute_mdcts(mode, 0, input, freq, C, CC, LM, st.upsample);
                bands.compute_band_energies(mode, freq, bandE, effEnd, C, LM);
                quant_bands.amp2Log2(mode, effEnd, end, bandE, bandLogE2, C);
                for (i = 0; i < C * nbEBands; i++)
                {
                    bandLogE2[i] += Inlines.HALF16(Inlines.SHL16(LM, CeltConstants.DB_SHIFT));
                }
            }

            compute_mdcts(mode, shortBlocks, input, freq, C, CC, LM, st.upsample);
            if (CC == 2 && C == 1)
                tf_chan = 0;
            bands.compute_band_energies(mode, freq, bandE, effEnd, C, LM);

            if (st.lfe != 0)
            {
                for (i = 2; i < end; i++)
                {
                    bandE[i] = Inlines.IMIN(bandE[i], Inlines.MULT16_32_Q15(Inlines.QCONST16(1e-4f, 15), bandE[0]));
                    bandE[i] = Inlines.MAX32(bandE[i], CeltConstants.EPSILON);
                }
            }

            quant_bands.amp2Log2(mode, effEnd, end, bandE, bandLogE, C);

            surround_dynalloc = Pointer.Malloc<int>(C * nbEBands);
            surround_dynalloc.MemSet(0, end);
            /* This computes how much masking takes place between surround channels */
            if (start == 0 && st.energy_mask != null && st.lfe == 0)
            {
                int mask_end;
                int midband;
                int count_dynalloc;
                int mask_avg = 0;
                int diff = 0;
                int count = 0;
                mask_end = Inlines.IMAX(2, st.lastCodedBands);
                for (c = 0; c < C; c++)
                {
                    for (i = 0; i < mask_end; i++)
                    {
                        int mask;
                        mask = Inlines.MAX16(Inlines.MIN16(st.energy_mask[nbEBands * c + i],
                               Inlines.QCONST16(.25f, CeltConstants.DB_SHIFT)), -Inlines.QCONST16(2.0f, CeltConstants.DB_SHIFT));
                        if (mask > 0)
                            mask = Inlines.HALF16(mask);
                        mask_avg += Inlines.MULT16_16(mask, eBands[i + 1] - eBands[i]);
                        count += eBands[i + 1] - eBands[i];
                        diff += Inlines.MULT16_16(mask, 1 + 2 * i - mask_end);
                    }
                }
                Inlines.OpusAssert(count > 0);
                mask_avg = Inlines.DIV32_16(mask_avg, count);
                mask_avg += Inlines.QCONST16(.2f, CeltConstants.DB_SHIFT);
                diff = diff * 6 / (C * (mask_end - 1) * (mask_end + 1) * mask_end);
                /* Again, being conservative */
                diff = Inlines.HALF32(diff);
                diff = Inlines.MAX32(Inlines.MIN32(diff, Inlines.QCONST32(.031f, CeltConstants.DB_SHIFT)), 0 - Inlines.QCONST32(.031f, CeltConstants.DB_SHIFT));
                /* Find the band that's in the middle of the coded spectrum */
                for (midband = 0; eBands[midband + 1] < eBands[mask_end] / 2; midband++) ;
                count_dynalloc = 0;
                for (i = 0; i < mask_end; i++)
                {
                    int lin;
                    int unmask;
                    lin = mask_avg + diff * (i - midband);
                    if (C == 2)
                        unmask = Inlines.MAX16(st.energy_mask[i], st.energy_mask[nbEBands + i]);
                    else
                        unmask = st.energy_mask[i];
                    unmask = Inlines.MIN16(unmask, Inlines.QCONST16(.0f, CeltConstants.DB_SHIFT));
                    unmask -= lin;
                    if (unmask > Inlines.QCONST16(.25f, CeltConstants.DB_SHIFT))
                    {
                        surround_dynalloc[i] = unmask - Inlines.QCONST16(.25f, CeltConstants.DB_SHIFT);
                        count_dynalloc++;
                    }
                }
                if (count_dynalloc >= 3)
                {
                    /* If we need dynalloc in many bands, it's probably because our
                       initial masking rate was too low. */
                    mask_avg += Inlines.QCONST16(.25f, CeltConstants.DB_SHIFT);
                    if (mask_avg > 0)
                    {
                        /* Something went really wrong in the original calculations,
                           disabling masking. */
                        mask_avg = 0;
                        diff = 0;
                        surround_dynalloc.MemSet(0, mask_end);
                    }
                    else {
                        for (i = 0; i < mask_end; i++)
                            surround_dynalloc[i] = Inlines.MAX16(0, surround_dynalloc[i] - Inlines.QCONST16(.25f, CeltConstants.DB_SHIFT));
                    }
                }
                mask_avg += Inlines.QCONST16(.2f, CeltConstants.DB_SHIFT);
                /* Convert to 1/64th units used for the trim */
                surround_trim = 64 * diff;
                /*printf("%d %d ", mask_avg, surround_trim);*/
                surround_masking = mask_avg;
            }
            /* Temporal VBR (but not for LFE) */
            if (st.lfe == 0)
            {
                int follow = -Inlines.QCONST16(10.0f, CeltConstants.DB_SHIFT);
                int frame_avg = 0;
                int offset = shortBlocks != 0 ? Inlines.HALF16(Inlines.SHL16(LM, CeltConstants.DB_SHIFT)) : 0;
                for (i = start; i < end; i++)
                {
                    follow = Inlines.MAX16(follow - Inlines.QCONST16(1.0f, CeltConstants.DB_SHIFT), bandLogE[i] - offset);
                    if (C == 2)
                        follow = Inlines.MAX16(follow, bandLogE[i + nbEBands] - offset);
                    frame_avg += follow;
                }
                frame_avg /= (end - start);
                temporal_vbr = Inlines.SUB16(frame_avg, st.spec_avg);
                temporal_vbr = Inlines.MIN16(Inlines.QCONST16(3.0f, CeltConstants.DB_SHIFT), Inlines.MAX16(-Inlines.QCONST16(1.5f, CeltConstants.DB_SHIFT), temporal_vbr));
                st.spec_avg += Inlines.CHOP16(Inlines.MULT16_16_Q15(Inlines.QCONST16(.02f, 15), temporal_vbr));
            }
            /*for (i=0;i<21;i++)
               printf("%f ", bandLogE[i]);
            printf("\n");*/

            if (secondMdct == 0)
            {
                bandLogE.MemCopyTo(bandLogE2, C * nbEBands);
            }

            /* Last chance to catch any transient we might have missed in the
               time-domain analysis */
            if (LM > 0 && EntropyCoder.ec_tell(enc) + 3 <= total_bits && isTransient == 0 && st.complexity >= 5 && st.lfe == 0)
            {
                if (patch_transient_decision(bandLogE, oldBandE, nbEBands, start, end, C) != 0)
                {
                    isTransient = 1;
                    shortBlocks = M;
                    compute_mdcts(mode, shortBlocks, input, freq, C, CC, LM, st.upsample);
                    bands.compute_band_energies(mode, freq, bandE, effEnd, C, LM);
                    quant_bands.amp2Log2(mode, effEnd, end, bandE, bandLogE, C);
                    /* Compensate for the scaling of short vs long mdcts */
                    for (i = 0; i < C * nbEBands; i++)
                        bandLogE2[i] += Inlines.HALF16(Inlines.SHL16(LM, CeltConstants.DB_SHIFT));
                    tf_estimate = Inlines.QCONST16(.2f, 14);
                }
            }

            if (LM > 0 && EntropyCoder.ec_tell(enc) + 3 <= total_bits)
                EntropyCoder.ec_enc_bit_logp(enc, isTransient, 3);

            X = Pointer.Malloc<int>(C * N);         /**< Interleaved normalised MDCTs */

            /* Band normalisation */
            bands.normalise_bands(mode, freq, X, bandE, effEnd, C, M);

            tf_res = Pointer.Malloc<int>(nbEBands);
            /* Disable variable tf resolution for hybrid and at very low bitrate */
            if (effectiveBytes >= 15 * C && start == 0 && st.complexity >= 2 && st.lfe == 0)
            {
                int lambda;
                if (effectiveBytes < 40)
                    lambda = 12;
                else if (effectiveBytes < 60)
                    lambda = 6;
                else if (effectiveBytes < 100)
                    lambda = 4;
                else
                    lambda = 3;
                lambda *= 2;
                BoxedValue<int> boxed_tf_sum = new BoxedValue<int>();
                tf_select = tf_analysis(mode, effEnd, isTransient, tf_res, lambda, X, N, LM, boxed_tf_sum, tf_estimate, tf_chan);
                tf_sum = boxed_tf_sum.Val;

                for (i = effEnd; i < end; i++)
                    tf_res[i] = tf_res[effEnd - 1];
            }
            else {
                tf_sum = 0;
                for (i = 0; i < end; i++)
                    tf_res[i] = isTransient;
                tf_select = 0;
            }

            error = Pointer.Malloc<int>(C * nbEBands);
            BoxedValue<int> boxed_delayedIntra = new BoxedValue<int>(st.delayedIntra);
            quant_bands.quant_coarse_energy(mode, start, end, effEnd, bandLogE,
                  oldBandE, (uint)total_bits, error, enc,
                  C, LM, nbAvailableBytes, st.force_intra,
                  boxed_delayedIntra, st.complexity >= 4 ? 1 : 0, st.loss_rate, st.lfe);
            st.delayedIntra = boxed_delayedIntra.Val;

            tf_encode(start, end, isTransient, tf_res, LM, tf_select, enc);

            if (EntropyCoder.ec_tell(enc) + 4 <= total_bits)
            {
                if (st.lfe != 0)
                {
                    st.tapset_decision = 0;
                    st.spread_decision = Spread.SPREAD_NORMAL;
                }
                else if (shortBlocks != 0 || st.complexity < 3 || nbAvailableBytes < 10 * C || start != 0)
                {
                    if (st.complexity == 0)
                        st.spread_decision = Spread.SPREAD_NONE;
                    else
                        st.spread_decision = Spread.SPREAD_NORMAL;
                }
                else
                {
                    {
                        BoxedValue<int> boxed_tonal_average = new BoxedValue<int>(st.tonal_average);
                        BoxedValue<int> boxed_hf_average = new BoxedValue<int>(st.hf_average);
                        BoxedValue<int> boxed_tapset_decision = new BoxedValue<int>(st.tapset_decision);
                        st.spread_decision = bands.spreading_decision(mode, X,
                              boxed_tonal_average, st.spread_decision, boxed_hf_average,
                              boxed_tapset_decision, (pf_on != 0 && shortBlocks == 0) ? 1 : 0, effEnd, C, M);
                        st.tonal_average = boxed_tonal_average.Val;
                        st.hf_average = boxed_hf_average.Val;
                        st.tapset_decision = boxed_tapset_decision.Val;
                    }

                    /*printf("%d %d\n", st.tapset_decision, st.spread_decision);*/
                    /*printf("%f %d %f %d\n\n", st.analysis.tonality, st.spread_decision, st.analysis.tonality_slope, st.tapset_decision);*/
                }
                EntropyCoder.ec_enc_icdf(enc, st.spread_decision, Tables.spread_icdf.GetPointer(), 5);
            }

            offsets = Pointer.Malloc<int>(nbEBands);

            BoxedValue<int> boxed_tot_boost = new BoxedValue<int>();
            maxDepth = dynalloc_analysis(bandLogE, bandLogE2, nbEBands, start, end, C, offsets,
                  st.lsb_depth, mode.logN, isTransient, st.vbr, st.constrained_vbr,
                  eBands, LM, effectiveBytes, boxed_tot_boost, st.lfe, surround_dynalloc);
            tot_boost = boxed_tot_boost.Val;

            /* For LFE, everything interesting is in the first band */
            if (st.lfe != 0)
                offsets[0] = Inlines.IMIN(8, effectiveBytes / 3);
            cap = Pointer.Malloc<int>(nbEBands);
            celt.init_caps(mode, cap, LM, C);

            dynalloc_logp = 6;
            total_bits <<= EntropyCoder.BITRES;
            total_boost = 0;
            tell = (int)EntropyCoder.ec_tell_frac(enc);
            for (i = start; i < end; i++)
            {
                int width, quanta;
                int dynalloc_loop_logp;
                int boost;
                int j;
                width = C * (eBands[i + 1] - eBands[i]) << LM;
                /* quanta is 6 bits, but no more than 1 bit/sample
                   and no less than 1/8 bit/sample */
                quanta = Inlines.IMIN(width << EntropyCoder.BITRES, Inlines.IMAX(6 << EntropyCoder.BITRES, width));
                dynalloc_loop_logp = dynalloc_logp;
                boost = 0;
                for (j = 0; tell + (dynalloc_loop_logp << EntropyCoder.BITRES) < total_bits - total_boost
                      && boost < cap[i]; j++)
                {
                    int flag;
                    flag = j < offsets[i] ? 1 : 0;
                    EntropyCoder.ec_enc_bit_logp(enc, flag, (uint)dynalloc_loop_logp);
                    tell = (int)EntropyCoder.ec_tell_frac(enc);
                    if (flag == 0)
                        break;
                    boost += quanta;
                    total_boost += quanta;
                    dynalloc_loop_logp = 1;
                }
                /* Making dynalloc more likely */
                if (j != 0)
                    dynalloc_logp = Inlines.IMAX(2, dynalloc_logp - 1);
                offsets[i] = boost;
            }

            if (C == 2)
            {
                // fixme move these to static
                int[] intensity_thresholds =
                  /* 0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19  20  off*/
                  {  1, 2, 3, 4, 5, 6, 7, 8,16,24,36,44,50,56,62,67,72,79,88,106,134};
                int[] intensity_histeresis =
                  {  1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 3, 3, 4, 5, 6,  8, 8};

                /* Always use MS for 2.5 ms frames until we can do a better analysis */
                if (LM != 0)
                    dual_stereo = stereo_analysis(mode, X, LM, N);

                st.intensity = bands.hysteresis_decision((int)(equiv_rate / 1000),
                      intensity_thresholds.GetPointer(), intensity_histeresis.GetPointer(), 21, st.intensity);
                st.intensity = Inlines.IMIN(end, Inlines.IMAX(start, st.intensity));
            }

            alloc_trim = 5;
            if (tell + (6 << EntropyCoder.BITRES) <= total_bits - total_boost)
            {
                if (st.lfe != 0)
                {
                    alloc_trim = 5;
                }
                else
                {
                    BoxedValue<int> boxed_stereo_saving = new BoxedValue<int>(st.stereo_saving);
                    alloc_trim = alloc_trim_analysis(mode, X, bandLogE,
                       end, LM, C, N, st.analysis, boxed_stereo_saving, tf_estimate,
                       st.intensity, surround_trim);
                    st.stereo_saving = boxed_stereo_saving.Val;
                }
                EntropyCoder.ec_enc_icdf(enc, alloc_trim, Tables.trim_icdf.GetPointer(), 7);
                tell = (int)EntropyCoder.ec_tell_frac(enc);
            }

            /* Variable bitrate */
            if (vbr_rate > 0)
            {
                int alpha;
                int delta;
                /* The target rate in 8th bits per frame */
                int target, base_target;
                int min_allowed;
                int lm_diff = mode.maxLM - LM;

                /* Don't attempt to use more than 510 kb/s, even for frames smaller than 20 ms.
                   The CELT allocator will just not be able to use more than that anyway. */
                nbCompressedBytes = Inlines.IMIN(nbCompressedBytes, 1275 >> (3 - LM));
                base_target = vbr_rate - ((40 * C + 20) << EntropyCoder.BITRES);

                if (st.constrained_vbr != 0)
                    base_target += (st.vbr_offset >> lm_diff);

                target = compute_vbr(mode, st.analysis, base_target, LM, equiv_rate,
                      st.lastCodedBands, C, st.intensity, st.constrained_vbr,
                      st.stereo_saving, tot_boost, tf_estimate, pitch_change, maxDepth,
                      st.variable_duration, st.lfe, st.energy_mask != null ? 1 : 0, surround_masking,
                      temporal_vbr);

                /* The current offset is removed from the target and the space used
                   so far is added*/
                target = target + tell;
                /* In VBR mode the frame size must not be reduced so much that it would
                    result in the encoder running out of bits.
                   The margin of 2 bytes ensures that none of the bust-prevention logic
                    in the decoder will have triggered so far. */
                min_allowed = ((tell + total_boost + (1 << (EntropyCoder.BITRES + 3)) - 1) >> (EntropyCoder.BITRES + 3)) + 2 - nbFilledBytes;

                nbAvailableBytes = (target + (1 << (EntropyCoder.BITRES + 2))) >> (EntropyCoder.BITRES + 3);
                nbAvailableBytes = Inlines.IMAX(min_allowed, nbAvailableBytes);
                nbAvailableBytes = Inlines.IMIN(nbCompressedBytes, nbAvailableBytes + nbFilledBytes) - nbFilledBytes;

                /* By how much did we "miss" the target on that frame */
                delta = target - vbr_rate;

                target = nbAvailableBytes << (EntropyCoder.BITRES + 3);

                /*If the frame is silent we don't adjust our drift, otherwise
                  the encoder will shoot to very high rates after hitting a
                  span of silence, but we do allow the bitres to refill.
                  This means that we'll undershoot our target in CVBR/VBR modes
                  on files with lots of silence. */
                if (silence != 0)
                {
                    nbAvailableBytes = 2;
                    target = 2 * 8 << EntropyCoder.BITRES;
                    delta = 0;
                }

                if (st.vbr_count < 970)
                {
                    st.vbr_count++;
                    alpha = Inlines.celt_rcp(Inlines.SHL32((st.vbr_count + 20), 16));
                }
                else
                    alpha = Inlines.QCONST16(.001f, 15);
                /* How many bits have we used in excess of what we're allowed */
                if (st.constrained_vbr != 0)
                    st.vbr_reservoir += target - vbr_rate;
                /*printf ("%d\n", st.vbr_reservoir);*/

                /* Compute the offset we need to apply in order to reach the target */
                if (st.constrained_vbr != 0)
                {
                    st.vbr_drift += (int)Inlines.MULT16_32_Q15(alpha, (delta * (1 << lm_diff)) - st.vbr_offset - st.vbr_drift);
                    st.vbr_offset = -st.vbr_drift;
                }
                /*printf ("%d\n", st.vbr_drift);*/

                if (st.constrained_vbr != 0 && st.vbr_reservoir < 0)
                {
                    /* We're under the min value -- increase rate */
                    int adjust = (-st.vbr_reservoir) / (8 << EntropyCoder.BITRES);
                    /* Unless we're just coding silence */
                    nbAvailableBytes += silence != 0 ? 0 : adjust;
                    st.vbr_reservoir = 0;
                    /*printf ("+%d\n", adjust);*/
                }
                nbCompressedBytes = Inlines.IMIN(nbCompressedBytes, nbAvailableBytes + nbFilledBytes);
                /*printf("%d\n", nbCompressedBytes*50*8);*/
                /* This moves the raw bits to take into account the new compressed size */
                EntropyCoder.ec_enc_shrink(enc, (uint)nbCompressedBytes);
            }

            /* Bit allocation */
            fine_quant = Pointer.Malloc<int>(nbEBands);
            pulses = Pointer.Malloc<int>(nbEBands);
            fine_priority = Pointer.Malloc<int>(nbEBands);

            /* bits =    packet size                                     - where we are                        - safety*/
            bits = (((int)nbCompressedBytes * 8) << EntropyCoder.BITRES) - (int)EntropyCoder.ec_tell_frac(enc) - 1;
            anti_collapse_rsv = isTransient != 0 && LM >= 2 && bits >= ((LM + 2) << EntropyCoder.BITRES) ? (1 << EntropyCoder.BITRES) : 0;
            bits -= anti_collapse_rsv;
            signalBandwidth = end - 1;

#if ENABLE_ANALYSIS
            if (st.analysis.valid != 0)
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
                signalBandwidth = Inlines.IMAX(st.analysis.bandwidth, min_bandwidth);
            }
#endif

            if (st.lfe != 0)
            {
                signalBandwidth = 1;
            }

            BoxedValue<int> boxed_intensity = new BoxedValue<int>(st.intensity);
            BoxedValue<int> boxed_dual_stereo = new BoxedValue<int>(dual_stereo);
            BoxedValue<int> boxed_balance = new BoxedValue<int>();
            codedBands = rate.compute_allocation(mode, start, end, offsets, cap,
                  alloc_trim, boxed_intensity, boxed_dual_stereo, bits, boxed_balance, pulses,
                  fine_quant, fine_priority, C, LM, enc, 1, st.lastCodedBands, signalBandwidth);
            st.intensity = boxed_intensity.Val;
            dual_stereo = boxed_dual_stereo.Val;
            balance = boxed_balance.Val;

            if (st.lastCodedBands != 0)
                st.lastCodedBands = Inlines.IMIN(st.lastCodedBands + 1, Inlines.IMAX(st.lastCodedBands - 1, codedBands));
            else
                st.lastCodedBands = codedBands;

            quant_bands.quant_fine_energy(mode, start, end, oldBandE, error, fine_quant, enc, C);

            /* Residual quantisation */
            collapse_masks = Pointer.Malloc<byte>(C * nbEBands);
            BoxedValue<uint> boxed_rng = new BoxedValue<uint>(st.rng);
            bands.quant_all_bands(1, mode, start, end, X, C == 2 ? X.Point(N) : null, collapse_masks,
                  bandE, pulses, shortBlocks, st.spread_decision,
                  dual_stereo, st.intensity, tf_res, nbCompressedBytes * (8 << EntropyCoder.BITRES) - anti_collapse_rsv,
                  balance, enc, LM, codedBands, boxed_rng);
            st.rng = boxed_rng.Val;

            if (anti_collapse_rsv > 0)
            {
                anti_collapse_on = (st.consec_transient < 2) ? 1 : 0;
#if FUZZING
                anti_collapse_on = new Random().Next() & 0x1;
#endif
                EntropyCoder.ec_enc_bits(enc, (uint)anti_collapse_on, 1);
            }

            quant_bands.quant_energy_finalise(mode, start, end, oldBandE, error, fine_quant, fine_priority, nbCompressedBytes * 8 - (int)EntropyCoder.ec_tell(enc), enc, C);

            if (silence != 0)
            {
                for (i = 0; i < C * nbEBands; i++)
                    oldBandE[i] = -Inlines.QCONST16(28.0f, CeltConstants.DB_SHIFT);
            }

            st.prefilter_period = pitch_index;
            st.prefilter_gain = gain1;
            st.prefilter_tapset = prefilter_tapset;

            if (CC == 2 && C == 1)
            {
                oldBandE.MemCopyTo(oldBandE.Point(nbEBands), nbEBands);
            }

            if (isTransient == 0)
            {
                oldLogE.MemCopyTo(oldLogE2, CC * nbEBands);
                oldBandE.MemCopyTo(oldLogE, CC * nbEBands);
            }
            else
            {
                for (i = 0; i < CC * nbEBands; i++)
                {
                    oldLogE[i] = Inlines.MIN16(oldLogE[i], oldBandE[i]);
                }
            }

            /* In case start or end were to change */
            c = 0;
            do
            {
                for (i = 0; i < start; i++)
                {
                    oldBandE[c * nbEBands + i] = 0;
                    oldLogE[c * nbEBands + i] = oldLogE2[c * nbEBands + i] = -Inlines.QCONST16(28.0f, CeltConstants.DB_SHIFT);
                }
                for (i = end; i < nbEBands; i++)
                {
                    oldBandE[c * nbEBands + i] = 0;
                    oldLogE[c * nbEBands + i] = oldLogE2[c * nbEBands + i] = -Inlines.QCONST16(28.0f, CeltConstants.DB_SHIFT);
                }
            } while (++c < CC);

            if (isTransient != 0 || transient_got_disabled != 0)
                st.consec_transient++;
            else
                st.consec_transient = 0;
            st.rng = enc.rng;

            /* If there's any room left (can only happen for very high rates),
               it's already filled with zeros */
            EntropyCoder.ec_enc_done(enc);


            if (EntropyCoder.ec_get_error(enc) != 0)
                return OpusError.OPUS_INTERNAL_ERROR;
            else
                return nbCompressedBytes;
        }


        public static int opus_custom_encoder_ctl(CELTEncoder st, int request, params object[] vargs)
        {
            switch (request)
            {
                case OpusControl.OPUS_SET_COMPLEXITY_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value < 0 || value > 10)
                            return OpusError.OPUS_BAD_ARG;
                        st.complexity = value;
                    }
                    break;
                case CeltControl.CELT_SET_START_BAND_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value < 0 || value >= st.mode.nbEBands)
                            return OpusError.OPUS_BAD_ARG;
                        st.start = value;
                    }
                    break;
                case CeltControl.CELT_SET_END_BAND_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value < 1 || value > st.mode.nbEBands)
                            return OpusError.OPUS_BAD_ARG;
                        st.end = value;
                    }
                    break;
                case CeltControl.CELT_SET_PREDICTION_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value < 0 || value > 2)
                            return OpusError.OPUS_BAD_ARG;
                        st.disable_pf = (value <= 1) ? 1 : 0;
                        st.force_intra = (value == 0) ? 1 : 0;
                    }
                    break;
                case OpusControl.OPUS_SET_PACKET_LOSS_PERC_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value < 0 || value > 100)
                            return OpusError.OPUS_BAD_ARG;
                        st.loss_rate = value;
                    }
                    break;
                case OpusControl.OPUS_SET_VBR_CONSTRAINT_REQUEST:
                    {
                        int value = (int)vargs[0];
                        st.constrained_vbr = value;
                    }
                    break;
                case OpusControl.OPUS_SET_VBR_REQUEST:
                    {
                        int value = (int)vargs[0];
                        st.vbr = value;
                    }
                    break;
                case OpusControl.OPUS_SET_BITRATE_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value <= 500 && value != OpusConstants.OPUS_BITRATE_MAX)
                            return OpusError.OPUS_BAD_ARG;
                        value = Inlines.IMIN(value, 260000 * st.channels);
                        st.bitrate = value;
                    }
                    break;
                case CeltControl.CELT_SET_CHANNELS_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value < 1 || value > 2)
                            return OpusError.OPUS_BAD_ARG;
                        st.stream_channels = value;
                    }
                    break;
                case OpusControl.OPUS_SET_LSB_DEPTH_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value < 8 || value > 24)
                            return OpusError.OPUS_BAD_ARG;
                        st.lsb_depth = value;
                    }
                    break;
                case OpusControl.OPUS_GET_LSB_DEPTH_REQUEST:
                    {
                        BoxedValue<int> value = (BoxedValue<int>)vargs[0];
                        value.Val = st.lsb_depth;
                    }
                    break;
                case OpusControl.OPUS_SET_EXPERT_FRAME_DURATION_REQUEST:
                    {
                        int value = (int)vargs[0];
                        st.variable_duration = value;
                    }
                    break;
                case OpusControl.OPUS_RESET_STATE:
                    {
                        int i;

                        // Fixme make sure this works
                        ///OPUS_CLEAR((char*)&st.ENCODER_RESET_START,
                        ///opus_custom_encoder_get_size(st.mode, st.channels) -
                        ///((char*)&st.ENCODER_RESET_START - (char*)st));
                        st.PartialReset();

                        // We have to reconstitute the dynamic buffers here. fixme: this could be better implemented
                        st.in_mem = Pointer.Malloc<int>(st.channels * st.mode.overlap);
                        st.prefilter_mem = Pointer.Malloc<int>(st.channels * CeltConstants.COMBFILTER_MAXPERIOD);
                        st.oldBandE = Pointer.Malloc<int>(st.channels * st.mode.nbEBands);
                        st.oldLogE = Pointer.Malloc<int>(st.channels * st.mode.nbEBands);
                        st.oldLogE2 = Pointer.Malloc<int>(st.channels * st.mode.nbEBands);

                        for (i = 0; i < st.channels * st.mode.nbEBands; i++)
                        {
                            st.oldLogE[i] = st.oldLogE2[i] = -Inlines.QCONST16(28.0f, CeltConstants.DB_SHIFT);
                        }
                        st.vbr_offset = 0;
                        st.delayedIntra = 1;
                        st.spread_decision = Spread.SPREAD_NORMAL;
                        st.tonal_average = 256;
                        st.hf_average = 0;
                        st.tapset_decision = 0;
                    }
                    break;
                case CeltControl.CELT_SET_SIGNALLING_REQUEST:
                    {
                        int value = (int)vargs[0];
                        st.signalling = value;
                    }
                    break;
                case CeltControl.CELT_SET_ANALYSIS_REQUEST:
                    {
                        AnalysisInfo info = (AnalysisInfo)vargs[0];
                        if (info == null)
                            return OpusError.OPUS_BAD_ARG;

                        st.analysis.Assign(info);
                    }
                    break;
                case CeltControl.CELT_GET_MODE_REQUEST:
                    {
                        BoxedValue<CELTMode> value = (BoxedValue<CELTMode>)vargs[0];
                        if (value == null)
                            return OpusError.OPUS_BAD_ARG;
                        value.Val = st.mode;
                    }
                    break;
                case OpusControl.OPUS_GET_FINAL_RANGE_REQUEST:
                    {
                        BoxedValue<uint> value = (BoxedValue<uint>)vargs[0];
                        if (value == null)
                            return OpusError.OPUS_BAD_ARG;
                        value.Val = st.rng;
                    }
                    break;
                case CeltControl.OPUS_SET_LFE_REQUEST:
                    {
                        int value = (int)vargs[0];
                        st.lfe = value;
                    }
                    break;
                case CeltControl.OPUS_SET_ENERGY_MASK_REQUEST:
                    {
                        Pointer<int> value = (Pointer<int>)vargs[0];
                        st.energy_mask = value;
                    }
                    break;
                default:
                    return OpusError.OPUS_UNIMPLEMENTED;
            }
            return OpusError.OPUS_OK;
        }
    }
}
