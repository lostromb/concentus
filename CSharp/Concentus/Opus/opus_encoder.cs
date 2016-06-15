using Concentus.Celt;
using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Opus;
using Concentus.Opus.Enums;
using Concentus.Silk;
using Concentus.Silk.Structs;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus
{
    public static class opus_encoder
    {
        /** Initializes a previously allocated encoder state
  * The memory pointed to by st must be at least the size returned by opus_encoder_get_size().
  * This is intended for applications which use their own allocator instead of malloc.
  * @see opus_encoder_create(),opus_encoder_get_size()
  * To reset a previously initialized state, use the #OPUS_RESET_STATE CTL.
  * @param [in] st <tt>OpusEncoder*</tt>: Encoder state
  * @param [in] Fs <tt>opus_int32</tt>: Sampling rate of input signal (Hz)
 *                                      This must be one of 8000, 12000, 16000,
 *                                      24000, or 48000.
  * @param [in] channels <tt>int</tt>: Number of channels (1 or 2) in input signal
  * @param [in] application <tt>int</tt>: Coding mode (OPUS_APPLICATION_VOIP/OPUS_APPLICATION_AUDIO/OPUS_APPLICATION_RESTRICTED_LOWDELAY)
  * @retval #OPUS_OK Success or @ref opus_errorcodes
  */
        public static int opus_encoder_init(OpusEncoder st, int Fs, int channels, int application)
        {
            silk_encoder silk_enc;
            CELTEncoder celt_enc;
            int err;
            int ret;

            if ((Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000) || (channels != 1 && channels != 2) ||
                 (application != OpusApplication.OPUS_APPLICATION_VOIP && application != OpusApplication.OPUS_APPLICATION_AUDIO
                 && application != OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY))
                return OpusError.OPUS_BAD_ARG;

            st.Reset();
            /* Create SILK encoder */
            silk_enc = st.SilkEncoder;
            celt_enc = st.CeltEncoder;

            st.stream_channels = st.channels = channels;

            st.Fs = Fs;

            st.arch = 0;

            ret = enc_API.silk_InitEncoder(silk_enc, st.arch, st.silk_mode);
            if (ret != 0) return OpusError.OPUS_INTERNAL_ERROR;

            /* default SILK parameters */
            st.silk_mode.nChannelsAPI = channels;
            st.silk_mode.nChannelsInternal = channels;
            st.silk_mode.API_sampleRate = st.Fs;
            st.silk_mode.maxInternalSampleRate = 16000;
            st.silk_mode.minInternalSampleRate = 8000;
            st.silk_mode.desiredInternalSampleRate = 16000;
            st.silk_mode.payloadSize_ms = 20;
            st.silk_mode.bitRate = 25000;
            st.silk_mode.packetLossPercentage = 0;
            st.silk_mode.complexity = 9;
            st.silk_mode.useInBandFEC = 0;
            st.silk_mode.useDTX = 0;
            st.silk_mode.useCBR = 0;
            st.silk_mode.reducedDependency = 0;

            /* Create CELT encoder */
            /* Initialize CELT encoder */
            err = celt_encoder.celt_encoder_init(celt_enc, Fs, channels, st.arch);
            if (err != OpusError.OPUS_OK) return OpusError.OPUS_INTERNAL_ERROR;

            celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_SET_SIGNALLING_REQUEST, 0);
            celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_SET_COMPLEXITY_REQUEST, st.silk_mode.complexity);

            st.use_vbr = 1;
            /* Makes constrained VBR the default (safer for real-time use) */
            st.vbr_constraint = 1;
            st.user_bitrate_bps = OpusConstants.OPUS_AUTO;
            st.bitrate_bps = 3000 + Fs * channels;
            st.application = application;
            st.signal_type = OpusConstants.OPUS_AUTO;
            st.user_bandwidth = OpusConstants.OPUS_AUTO;
            st.max_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;
            st.force_channels = OpusConstants.OPUS_AUTO;
            st.user_forced_mode = OpusConstants.OPUS_AUTO;
            st.voice_ratio = -1;
            st.encoder_buffer = st.Fs / 100;
            st.lsb_depth = 24;
            st.variable_duration = OpusFramesize.OPUS_FRAMESIZE_ARG;

            /* Delay compensation of 4 ms (2.5 ms for SILK's extra look-ahead
               + 1.5 ms for SILK resamplers and stereo prediction) */
            st.delay_compensation = st.Fs / 250;

            st.hybrid_stereo_width_Q14 = 1 << 14;
            st.prev_HB_gain = CeltConstants.Q15ONE;
            st.variable_HP_smth2_Q15 = Inlines.silk_LSHIFT(Inlines.silk_lin2log(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ), 8);
            st.first = 1;
            st.mode = OpusMode.MODE_HYBRID;
            st.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;

#if ENABLE_ANALYSIS
            analysis.tonality_analysis_init(st.analysis);
#endif

            return OpusError.OPUS_OK;
        }

        public static byte gen_toc(int mode, int framerate, int bandwidth, int channels)
        {
            int period;
            byte toc;
            period = 0;
            while (framerate < 400)
            {
                framerate <<= 1;
                period++;
            }
            if (mode == OpusMode.MODE_SILK_ONLY)
            {
                toc = Inlines.CHOP8U((bandwidth - OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND) << 5);
                toc |= Inlines.CHOP8U((period - 2) << 3);
            }
            else if (mode == OpusMode.MODE_CELT_ONLY)
            {
                int tmp = bandwidth - OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND;
                if (tmp < 0)
                    tmp = 0;
                toc = 0x80;
                toc |= Inlines.CHOP8U(tmp << 5);
                toc |= Inlines.CHOP8U(period << 3);
            }
            else /* Hybrid */
            {
                toc = 0x60;
                toc |= Inlines.CHOP8U((bandwidth - OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND) << 4);
                toc |= Inlines.CHOP8U((period - 2) << 3);
            }
            toc |= Inlines.CHOP8U((channels == 2 ? 1 : 0) << 2);
            return toc;
        }

        public static void hp_cutoff(Pointer<int> input, int cutoff_Hz, Pointer<int> output, Pointer<int> hp_mem, int len, int channels, int Fs)
        {
            Pointer<int> B_Q28 = Pointer.Malloc<int>(3);
            Pointer<int> A_Q28 = Pointer.Malloc<int>(2);
            int Fc_Q19, r_Q28, r_Q22;

            Inlines.OpusAssert(cutoff_Hz <= int.MaxValue / Inlines.SILK_FIX_CONST(1.5f * 3.14159f / 1000, 19));
            Fc_Q19 = Inlines.silk_DIV32_16(Inlines.silk_SMULBB(Inlines.SILK_FIX_CONST(1.5f * 3.14159f / 1000, 19), cutoff_Hz), Fs / 1000);
            Inlines.OpusAssert(Fc_Q19 > 0 && Fc_Q19 < 32768);

            r_Q28 = Inlines.SILK_FIX_CONST(1.0f, 28) - Inlines.silk_MUL(Inlines.SILK_FIX_CONST(0.92f, 9), Fc_Q19);

            /* b = r * [ 1; -2; 1 ]; */
            /* a = [ 1; -2 * r * ( 1 - 0.5 * Fc^2 ); r^2 ]; */
            B_Q28[0] = r_Q28;
            B_Q28[1] = Inlines.silk_LSHIFT(-r_Q28, 1);
            B_Q28[2] = r_Q28;

            /* -r * ( 2 - Fc * Fc ); */
            r_Q22 = Inlines.silk_RSHIFT(r_Q28, 6);
            A_Q28[0] = Inlines.silk_SMULWW(r_Q22, Inlines.silk_SMULWW(Fc_Q19, Fc_Q19) - Inlines.SILK_FIX_CONST(2.0f, 22));
            A_Q28[1] = Inlines.silk_SMULWW(r_Q22, r_Q22);

            Filters.silk_biquad_alt(input, B_Q28, A_Q28, hp_mem, output, len, channels);
            if (channels == 2)
            {
                Filters.silk_biquad_alt(input.Point(1), B_Q28, A_Q28, hp_mem.Point(2), output.Point(1), len, channels);
            }
        }

        public static void dc_reject(Pointer<int> input, int cutoff_Hz, Pointer<int> output, Pointer<int> hp_mem, int len, int channels, int Fs)
        {
            int c, i;
            int shift;

            /* Approximates -round(log2(4.*cutoff_Hz/Fs)) */
            shift = Inlines.celt_ilog2(Fs / (cutoff_Hz * 3));
            for (c = 0; c < channels; c++)
            {
                for (i = 0; i < len; i++)
                {
                    int x, tmp, y;
                    x = Inlines.SHL32(Inlines.EXTEND32(input[channels * i + c]), 15);
                    /* First stage */
                    tmp = x - hp_mem[2 * c];
                    hp_mem[2 * c] = hp_mem[2 * c] + Inlines.PSHR32(x - hp_mem[2 * c], shift);
                    /* Second stage */
                    y = tmp - hp_mem[2 * c + 1];
                    hp_mem[2 * c + 1] = hp_mem[2 * c + 1] + Inlines.PSHR32(tmp - hp_mem[2 * c + 1], shift);
                    output[channels * i + c] = Inlines.EXTRACT16(Inlines.SATURATE(Inlines.PSHR32(y, 15), 32767));
                }
            }
        }

        public static void stereo_fade(Pointer<int> input, Pointer<int> output, int g1, int g2,
                int overlap48, int frame_size, int channels, Pointer<int> window, int Fs)
        {
            int i;
            int overlap;
            int inc;
            inc = 48000 / Fs;
            overlap = overlap48 / inc;
            g1 = CeltConstants.Q15ONE - g1;
            g2 = CeltConstants.Q15ONE - g2;
            for (i = 0; i < overlap; i++)
            {
                int diff;
                int g, w;
                w = Inlines.MULT16_16_Q15(window[i * inc], window[i * inc]);
                g = Inlines.SHR32(Inlines.MAC16_16(Inlines.MULT16_16(w, g2),
                      CeltConstants.Q15ONE - w, g1), 15);
                diff = Inlines.EXTRACT16(Inlines.HALF32((int)input[i * channels] - (int)input[i * channels + 1]));
                diff = Inlines.MULT16_16_Q15(g, diff);
                output[i * channels] = output[i * channels] - diff;
                output[i * channels + 1] = output[i * channels + 1] + diff;
            }
            for (; i < frame_size; i++)
            {
                int diff;
                diff = Inlines.EXTRACT16(Inlines.HALF32((int)input[i * channels] - (int)input[i * channels + 1]));
                diff = Inlines.MULT16_16_Q15(g2, diff);
                output[i * channels] = output[i * channels] - diff;
                output[i * channels + 1] = output[i * channels + 1] + diff;
            }
        }

        public static void gain_fade(Pointer<int> input, Pointer<int> output, int g1, int g2,
                int overlap48, int frame_size, int channels, Pointer<int> window, int Fs)
        {
            int i;
            int inc;
            int overlap;
            int c;
            inc = 48000 / Fs;
            overlap = overlap48 / inc;
            if (channels == 1)
            {
                for (i = 0; i < overlap; i++)
                {
                    int g, w;
                    w = Inlines.MULT16_16_Q15(window[i * inc], window[i * inc]);
                    g = Inlines.SHR32(Inlines.MAC16_16(Inlines.MULT16_16(w, g2),
                          CeltConstants.Q15ONE - w, g1), 15);
                    output[i] = Inlines.MULT16_16_Q15(g, input[i]);
                }
            }
            else {
                for (i = 0; i < overlap; i++)
                {
                    int g, w;
                    w = Inlines.MULT16_16_Q15(window[i * inc], window[i * inc]);
                    g = Inlines.SHR32(Inlines.MAC16_16(Inlines.MULT16_16(w, g2),
                                    CeltConstants.Q15ONE - w, g1), 15);
                    output[i * 2] = Inlines.MULT16_16_Q15(g, input[i * 2]);
                    output[i * 2 + 1] = Inlines.MULT16_16_Q15(g, input[i * 2 + 1]);
                }
            }
            c = 0; do
            {
                for (i = overlap; i < frame_size; i++)
                {
                    output[i * channels + c] = Inlines.MULT16_16_Q15(g2, input[i * channels + c]);
                }
            }
            while (++c < channels);
        }

        /** Allocates and initializes an encoder state.
 * There are three coding modes:
 *
 * @ref OPUS_APPLICATION_VOIP gives best quality at a given bitrate for voice
 *    signals. It enhances the  input signal by high-pass filtering and
 *    emphasizing formants and harmonics. Optionally  it includes in-band
 *    forward error correction to protect against packet loss. Use this
 *    mode for typical VoIP applications. Because of the enhancement,
 *    even at high bitrates the output may sound different from the input.
 *
 * @ref OPUS_APPLICATION_AUDIO gives best quality at a given bitrate for most
 *    non-voice signals like music. Use this mode for music and mixed
 *    (music/voice) content, broadcast, and applications requiring less
 *    than 15 ms of coding delay.
 *
 * @ref OPUS_APPLICATION_RESTRICTED_LOWDELAY configures low-delay mode that
 *    disables the speech-optimized mode in exchange for slightly reduced delay.
 *    This mode can only be set on an newly initialized or freshly reset encoder
 *    because it changes the codec delay.
 *
 * This is useful when the caller knows that the speech-optimized modes will not be needed (use with caution).
 * @param [in] Fs <tt>opus_int32</tt>: Sampling rate of input signal (Hz)
 *                                     This must be one of 8000, 12000, 16000,
 *                                     24000, or 48000.
 * @param [in] channels <tt>int</tt>: Number of channels (1 or 2) in input signal
 * @param [in] application <tt>int</tt>: Coding mode (@ref OPUS_APPLICATION_VOIP/@ref OPUS_APPLICATION_AUDIO/@ref OPUS_APPLICATION_RESTRICTED_LOWDELAY)
 * @param [out] error <tt>int*</tt>: @ref opus_errorcodes
 * @note Regardless of the sampling rate and number channels selected, the Opus encoder
 * can switch to a lower audio bandwidth or number of channels if the bitrate
 * selected is too low. This also means that it is safe to always use 48 kHz stereo input
 * and let the encoder optimize the encoding.
 */
        public static OpusEncoder opus_encoder_create(int Fs, int channels, int application, BoxedValue<int> error)
        {
            int ret;
            OpusEncoder st;
            if ((Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000) || (channels != 1 && channels != 2) ||
                (application != OpusApplication.OPUS_APPLICATION_VOIP && application != OpusApplication.OPUS_APPLICATION_AUDIO
                && application != OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY))
            {
                if (error != null)
                    error.Val = OpusError.OPUS_BAD_ARG;
                return null;
            }
            st = new OpusEncoder();
            if (st == null)
            {
                if (error != null)
                    error.Val = OpusError.OPUS_ALLOC_FAIL;
                return null;
            }
            ret = opus_encoder_init(st, Fs, channels, application);
            if (error != null)
                error.Val = ret;
            if (ret != OpusError.OPUS_OK)
            {
                st = null;
            }
            return st;
        }

        public static int user_bitrate_to_bitrate(OpusEncoder st, int frame_size, int max_data_bytes)
        {
            if (frame_size == 0)
            {
                frame_size = st.Fs / 400;
            }
            if (st.user_bitrate_bps == OpusConstants.OPUS_AUTO)
                return 60 * st.Fs / frame_size + st.Fs * st.channels;
            else if (st.user_bitrate_bps == OpusConstants.OPUS_BITRATE_MAX)
                return max_data_bytes * 8 * st.Fs / frame_size;
            else
                return st.user_bitrate_bps;
        }

        /* Don't use more than 60 ms for the frame size analysis */
        private const int MAX_DYNAMIC_FRAMESIZE = 24;

        /* Estimates how much the bitrate will be boosted based on the sub-frame energy */
        public static float transient_boost(Pointer<float> E, Pointer<float> E_1, int LM, int maxM)
        {
            int i;
            int M;
            float sumE = 0, sumE_1 = 0;
            float metric;

            M = Inlines.IMIN(maxM, (1 << LM) + 1);
            for (i = 0; i < M; i++)
            {
                sumE += E[i];
                sumE_1 += E_1[i];
            }
            metric = sumE * sumE_1 / (M * M);
            /*if (LM==3)
               printf("%f\n", metric);*/
            /*return metric>10 ? 1 : 0;*/
            /*return Inlines.MAX16(0,1-exp(-.25*(metric-2.)));*/
            return Inlines.MIN16(1, (float)Math.Sqrt(Inlines.MAX16(0, .05f * (metric - 2))));
        }

        /* Viterbi decoding trying to find the best frame size combination using look-ahead

           State numbering:
            0: unused
            1:  2.5 ms
            2:  5 ms (#1)
            3:  5 ms (#2)
            4: 10 ms (#1)
            5: 10 ms (#2)
            6: 10 ms (#3)
            7: 10 ms (#4)
            8: 20 ms (#1)
            9: 20 ms (#2)
           10: 20 ms (#3)
           11: 20 ms (#4)
           12: 20 ms (#5)
           13: 20 ms (#6)
           14: 20 ms (#7)
           15: 20 ms (#8)
        */
        public static int transient_viterbi(Pointer<float> E, Pointer<float> E_1, int N, int frame_cost, int rate)
        {
            int i;
            Pointer<Pointer<float>> cost = Arrays.InitTwoDimensionalArrayPointer<float>(MAX_DYNAMIC_FRAMESIZE, 16);
            Pointer<Pointer<int>> states = Arrays.InitTwoDimensionalArrayPointer<int>(MAX_DYNAMIC_FRAMESIZE, 16);
            float best_cost;
            int best_state;
            float factor;
            /* Take into account that we damp VBR in the 32 kb/s to 64 kb/s range. */
            if (rate < 80)
                factor = 0;
            else if (rate > 160)
                factor = 1;
            else
                factor = (rate - 80.0f) / 80.0f;
            /* Makes variable framesize less aggressive at lower bitrates, but I can't
               find any valid theoretical justification for this (other than it seems
               to help) */
            for (i = 0; i < 16; i++)
            {
                /* Impossible state */
                states[0][i] = -1;
                cost[0][i] = 1e10f;
            }
            for (i = 0; i < 4; i++)
            {
                cost[0][1 << i] = (frame_cost + rate * (1 << i)) * (1 + factor * transient_boost(E, E_1, i, N + 1));
                states[0][1 << i] = i;
            }
            for (i = 1; i < N; i++)
            {
                int j;

                /* Follow continuations */
                for (j = 2; j < 16; j++)
                {
                    cost[i][j] = cost[i - 1][j - 1];
                    states[i][j] = j - 1;
                }

                /* New frames */
                for (j = 0; j < 4; j++)
                {
                    int k;
                    float min_cost;
                    float curr_cost;
                    states[i][1 << j] = 1;
                    min_cost = cost[i - 1][1];
                    for (k = 1; k < 4; k++)
                    {
                        float tmp = cost[i - 1][(1 << (k + 1)) - 1];
                        if (tmp < min_cost)
                        {
                            states[i][1 << j] = (1 << (k + 1)) - 1;
                            min_cost = tmp;
                        }
                    }
                    curr_cost = (frame_cost + rate * (1 << j)) * (1 + factor * transient_boost(E.Point(i), E_1.Point(i), j, N - i + 1));
                    cost[i][1 << j] = min_cost;
                    /* If part of the frame is outside the analysis window, only count part of the cost */
                    if (N - i < (1 << j))
                        cost[i][1 << j] += curr_cost * (float)(N - i) / (1 << j);
                    else
                        cost[i][1 << j] += curr_cost;
                }
            }

            best_state = 1;
            best_cost = cost[N - 1][1];
            /* Find best end state (doesn't force a frame to end at N-1) */
            for (i = 2; i < 16; i++)
            {
                if (cost[N - 1][i] < best_cost)
                {
                    best_cost = cost[N - 1][i];
                    best_state = i;
                }
            }

            /* Follow transitions back */
            for (i = N - 1; i >= 0; i--)
            {
                /*printf("%d ", best_state);*/
                best_state = states[i][best_state];
            }
            /*printf("%d\n", best_state);*/
            return best_state;
        }

        public static int optimize_framesize<T>(Pointer<T> x, int len, int C, int Fs,
                        int bitrate, int tonality, Pointer<float> mem, int buffering,
                        downmix_func_def.downmix_func<T> downmix)
        {
            int N;
            int i;
            float[] e = new float[MAX_DYNAMIC_FRAMESIZE + 4];
            float[] e_1 = new float[MAX_DYNAMIC_FRAMESIZE + 3];
            int memx;
            int bestLM = 0;
            int subframe;
            int pos;
            int offset;
            Pointer<int> sub;

            subframe = Fs / 400;
            sub = Pointer.Malloc<int>(subframe);
            e[0] = mem[0];
            e_1[0] = 1.0f / (CeltConstants.EPSILON + mem[0]);
            if (buffering != 0)
            {
                /* Consider the CELT delay when not in restricted-lowdelay */
                /* We assume the buffering is between 2.5 and 5 ms */
                offset = 2 * subframe - buffering;
                Inlines.OpusAssert(offset >= 0 && offset <= subframe);
                len -= offset;
                e[1] = mem[1];
                e_1[1] = 1.0f / (CeltConstants.EPSILON + mem[1]);
                e[2] = mem[2];
                e_1[2] = 1.0f / (CeltConstants.EPSILON + mem[2]);
                pos = 3;
            }
            else {
                pos = 1;
                offset = 0;
            }
            N = Inlines.IMIN(len / subframe, MAX_DYNAMIC_FRAMESIZE);
            /* Just silencing a warning, it's really initialized later */
            memx = 0;
            for (i = 0; i < N; i++)
            {
                float tmp;
                int tmpx;
                int j;
                tmp = CeltConstants.EPSILON;

                downmix(x, sub, subframe, i * subframe + offset, 0, -2, C);
                if (i == 0)
                    memx = sub[0];
                for (j = 0; j < subframe; j++)
                {
                    tmpx = sub[j];
                    tmp += (tmpx - memx) * (float)(tmpx - memx);
                    memx = tmpx;
                }
                e[i + pos] = tmp;
                e_1[i + pos] = 1.0f / tmp;
            }
            /* Hack to get 20 ms working with APPLICATION_AUDIO
               The real problem is that the corresponding memory needs to use 1.5 ms
               from this frame and 1 ms from the next frame */
            e[i + pos] = e[i + pos - 1];
            if (buffering != 0)
                N = Inlines.IMIN(MAX_DYNAMIC_FRAMESIZE, N + 2);
            bestLM = transient_viterbi(e.GetPointer(), e_1.GetPointer(), N, (int)((1.0f + .5f * tonality) * (60 * C + 40)), bitrate / 400);
            mem[0] = e[1 << bestLM];
            if (buffering != 0)
            {
                mem[1] = e[(1 << bestLM) + 1];
                mem[2] = e[(1 << bestLM) + 2];
            }
            return bestLM;
        }

        public static void downmix_float(Pointer<float> x, Pointer<int> sub, int subframe, int offset, int c1, int c2, int C)
        {
            int scale;
            int j;
            for (j = 0; j < subframe; j++)
                sub[j] = Inlines.FLOAT2INT16(x[(j + offset) * C + c1]);
            if (c2 > -1)
            {
                for (j = 0; j < subframe; j++)
                    sub[j] += Inlines.FLOAT2INT16(x[(j + offset) * C + c2]);
            }
            else if (c2 == -2)
            {
                int c;
                for (c = 1; c < C; c++)
                {
                    for (j = 0; j < subframe; j++)
                        sub[j] += Inlines.FLOAT2INT16(x[(j + offset) * C + c]);
                }
            }
            scale = (1 << CeltConstants.SIG_SHIFT);
            if (C == -2)
                scale /= C;
            else
                scale /= 2;
            for (j = 0; j < subframe; j++)
                sub[j] *= scale;
        }

        public static void downmix_int(Pointer<short> x, Pointer<int> sub, int subframe, int offset, int c1, int c2, int C)
        {
            int scale;
            int j;
            for (j = 0; j < subframe; j++)
                sub[j] = x[(j + offset) * C + c1];
            if (c2 > -1)
            {
                for (j = 0; j < subframe; j++)
                    sub[j] += x[(j + offset) * C + c2];
            }
            else if (c2 == -2)
            {
                int c;
                for (c = 1; c < C; c++)
                {
                    for (j = 0; j < subframe; j++)
                        sub[j] += x[(j + offset) * C + c];
                }
            }
            scale = (1 << CeltConstants.SIG_SHIFT);
            if (C == -2)
                scale /= C;
            else
                scale /= 2;
            for (j = 0; j < subframe; j++)
                sub[j] *= scale;
        }

        public static int frame_size_select(int frame_size, int variable_duration, int Fs)
        {
            int new_size;
            if (frame_size < Fs / 400)
                return -1;
            if (variable_duration == OpusFramesize.OPUS_FRAMESIZE_ARG)
                new_size = frame_size;
            else if (variable_duration == OpusFramesize.OPUS_FRAMESIZE_VARIABLE)
                new_size = Fs / 50;
            else if (variable_duration >= OpusFramesize.OPUS_FRAMESIZE_2_5_MS && variable_duration <= OpusFramesize.OPUS_FRAMESIZE_60_MS)
                new_size = Inlines.IMIN(3 * Fs / 50, (Fs / 400) << (variable_duration - OpusFramesize.OPUS_FRAMESIZE_2_5_MS));
            else
                return -1;
            if (new_size > frame_size)
                return -1;
            if (400 * new_size != Fs && 200 * new_size != Fs && 100 * new_size != Fs &&
                     50 * new_size != Fs && 25 * new_size != Fs && 50 * new_size != 3 * Fs)
                return -1;
            return new_size;
        }

        public static int compute_frame_size<T>(Pointer<T> analysis_pcm, int frame_size,
              int variable_duration, int C, int Fs, int bitrate_bps,
              int delay_compensation, downmix_func_def.downmix_func<T> downmix
#if ENABLE_ANALYSIS
              , Pointer<float> subframe_mem
#endif
              )
        {
#if ENABLE_ANALYSIS
            if (variable_duration == OpusFramesize.OPUS_FRAMESIZE_VARIABLE && frame_size >= Fs / 200)
            {
                int LM = 3;
                LM = optimize_framesize(analysis_pcm, frame_size, C, Fs, bitrate_bps,
                      0, subframe_mem, delay_compensation, downmix);
                while ((Fs / 400 << LM) > frame_size)
                    LM--;
                frame_size = (Fs / 400 << LM);
            }
            else
#endif
            {
                frame_size = frame_size_select(frame_size, variable_duration, Fs);
            }

            if (frame_size < 0)
                return -1;
            return frame_size;
        }

        public static int compute_stereo_width(Pointer<int> pcm, int frame_size, int Fs, StereoWidthState mem)
        {
            int corr;
            int ldiff;
            int width;
            int xx, xy, yy;
            int sqrt_xx, sqrt_yy;
            int qrrt_xx, qrrt_yy;
            int frame_rate;
            int i;
            int short_alpha;

            frame_rate = Fs / frame_size;
            // fixme ghetto order of ops
            short_alpha = CeltConstants.Q15ONE - 25 * CeltConstants.Q15ONE / Inlines.IMAX(50, frame_rate);
            xx = xy = yy = 0;
            for (i = 0; i < frame_size; i += 4)
            {
                int pxx = 0;
                int pxy = 0;
                int pyy = 0;
                int x, y;
                x = pcm[2 * i];
                y = pcm[2 * i + 1];
                pxx = Inlines.SHR32(Inlines.MULT16_16(x, x), 2);
                pxy = Inlines.SHR32(Inlines.MULT16_16(x, y), 2);
                pyy = Inlines.SHR32(Inlines.MULT16_16(y, y), 2);
                x = pcm[2 * i + 2];
                y = pcm[2 * i + 3];
                pxx += Inlines.SHR32(Inlines.MULT16_16(x, x), 2);
                pxy += Inlines.SHR32(Inlines.MULT16_16(x, y), 2);
                pyy += Inlines.SHR32(Inlines.MULT16_16(y, y), 2);
                x = pcm[2 * i + 4];
                y = pcm[2 * i + 5];
                pxx += Inlines.SHR32(Inlines.MULT16_16(x, x), 2);
                pxy += Inlines.SHR32(Inlines.MULT16_16(x, y), 2);
                pyy += Inlines.SHR32(Inlines.MULT16_16(y, y), 2);
                x = pcm[2 * i + 6];
                y = pcm[2 * i + 7];
                pxx += Inlines.SHR32(Inlines.MULT16_16(x, x), 2);
                pxy += Inlines.SHR32(Inlines.MULT16_16(x, y), 2);
                pyy += Inlines.SHR32(Inlines.MULT16_16(y, y), 2);

                xx += Inlines.SHR32(pxx, 10);
                xy += Inlines.SHR32(pxy, 10);
                yy += Inlines.SHR32(pyy, 10);
            }

            mem.XX += Inlines.MULT16_32_Q15(short_alpha, xx - mem.XX);
            mem.XY += Inlines.MULT16_32_Q15(short_alpha, xy - mem.XY);
            mem.YY += Inlines.MULT16_32_Q15(short_alpha, yy - mem.YY);
            mem.XX = Inlines.MAX32(0, mem.XX);
            mem.XY = Inlines.MAX32(0, mem.XY);
            mem.YY = Inlines.MAX32(0, mem.YY);
            if (Inlines.MAX32(mem.XX, mem.YY) > Inlines.QCONST16(8e-4f, 18))
            {
                sqrt_xx = Inlines.celt_sqrt(mem.XX);
                sqrt_yy = Inlines.celt_sqrt(mem.YY);
                qrrt_xx = Inlines.celt_sqrt(sqrt_xx);
                qrrt_yy = Inlines.celt_sqrt(sqrt_yy);
                /* Inter-channel correlation */
                mem.XY = Inlines.MIN32(mem.XY, sqrt_xx * sqrt_yy);
                corr = Inlines.SHR32(Inlines.frac_div32(mem.XY, CeltConstants.EPSILON + Inlines.MULT16_16(sqrt_xx, sqrt_yy)), 16);
                /* Approximate loudness difference */
                ldiff = CeltConstants.Q15ONE * Inlines.ABS16(qrrt_xx - qrrt_yy) / (CeltConstants.EPSILON + qrrt_xx + qrrt_yy);
                width = Inlines.MULT16_16_Q15(Inlines.celt_sqrt(Inlines.QCONST32(1.0f, 30) - Inlines.MULT16_16(corr, corr)), ldiff);
                /* Smoothing over one second */
                mem.smoothed_width += (width - mem.smoothed_width) / frame_rate;
                /* Peak follower */
                mem.max_follower = Inlines.MAX16(mem.max_follower - Inlines.QCONST16(.02f, 15) / frame_rate, mem.smoothed_width);
            }
            else {
                width = 0;
                corr = CeltConstants.Q15ONE;
                ldiff = 0;
            }
            /*printf("%f %f %f %f %f ", corr/(float)1.0f, ldiff/(float)1.0f, width/(float)1.0f, mem.smoothed_width/(float)1.0f, mem.max_follower/(float)1.0f);*/
            return Inlines.EXTRACT16(Inlines.MIN32(CeltConstants.Q15ONE, 20 * mem.max_follower));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T">The storage type of analysis_pcm, either short or float</typeparam>
        /// <param name="st"></param>
        /// <param name="pcm"></param>
        /// <param name="frame_size"></param>
        /// <param name="data"></param>
        /// <param name="out_data_bytes"></param>
        /// <param name="lsb_depth"></param>
        /// <param name="analysis_pcm"></param>
        /// <param name="analysis_size"></param>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <param name="analysis_channels"></param>
        /// <param name="downmix"></param>
        /// <param name="float_api"></param>
        /// <returns></returns>
        public static int opus_encode_native<T>(OpusEncoder st, Pointer<int> pcm, int frame_size,
                        Pointer<byte> data, int out_data_bytes, int lsb_depth,
                        Pointer<T> analysis_pcm, int analysis_size, int c1, int c2,
                        int analysis_channels, downmix_func_def.downmix_func<T> downmix, int float_api)
        {
            silk_encoder silk_enc; // porting note: pointer
            CELTEncoder celt_enc; // porting note: pointer
            int i;
            int ret = 0;
            int nBytes;
            ec_ctx enc = new ec_ctx(); // porting note: stack var
            int bytes_target;
            int prefill = 0;
            int start_band = 0;
            int redundancy = 0;
            int redundancy_bytes = 0; /* Number of bytes to use for redundancy frame */
            int celt_to_silk = 0;
            Pointer<int> pcm_buf;
            int nb_compr_bytes;
            int to_celt = 0;
            uint redundant_rng = 0;
            int cutoff_Hz, hp_freq_smth1;
            int voice_est; /* Probability of voice in Q7 */
            int equiv_rate;
            int delay_compensation;
            int frame_rate;
            int max_rate; /* Max bitrate we're allowed to use */
            int curr_bandwidth;
            int HB_gain;
            int max_data_bytes; /* Max number of bytes we're allowed to use */
            int total_buffer;
            int stereo_width;
            CELTMode celt_mode; // porting note: pointer
#if ENABLE_ANALYSIS
            AnalysisInfo analysis_info = new AnalysisInfo(); // porting note: stack var
            int analysis_read_pos_bak = -1;
            int analysis_read_subframe_bak = -1;
#endif
            Pointer<int> tmp_prefill;

            max_data_bytes = Inlines.IMIN(1276, out_data_bytes);

            st.rangeFinal = 0;
            if ((st.variable_duration == 0 && 400 * frame_size != st.Fs && 200 * frame_size != st.Fs && 100 * frame_size != st.Fs &&
                 50 * frame_size != st.Fs && 25 * frame_size != st.Fs && 50 * frame_size != 3 * st.Fs)
                 || (400 * frame_size < st.Fs)
                 || max_data_bytes <= 0
                 )
            {
                return OpusError.OPUS_BAD_ARG;
            }

            silk_enc = st.SilkEncoder;
            celt_enc = st.CeltEncoder;
            if (st.application == OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY)
                delay_compensation = 0;
            else
                delay_compensation = st.delay_compensation;

            lsb_depth = Inlines.IMIN(lsb_depth, st.lsb_depth);
            BoxedValue<CELTMode> boxedMode = new BoxedValue<CELTMode>();
            celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_GET_MODE_REQUEST, boxedMode);
            celt_mode = boxedMode.Val;
#if ENABLE_ANALYSIS
            analysis_info.valid = 0;
            if (st.silk_mode.complexity >= 7 && st.Fs == 48000)
            {
                analysis_read_pos_bak = st.analysis.read_pos;
                analysis_read_subframe_bak = st.analysis.read_subframe;
                analysis.run_analysis<T>(st.analysis, celt_mode, analysis_pcm, analysis_size, frame_size,
                      c1, c2, analysis_channels, st.Fs,
                      lsb_depth, downmix, analysis_info);
            }
#endif

            st.voice_ratio = -1;

#if ENABLE_ANALYSIS
            st.detected_bandwidth = 0;
            if (analysis_info.valid != 0)
            {
                int analysis_bandwidth;
                if (st.signal_type == OpusConstants.OPUS_AUTO)
                    st.voice_ratio = (int)Math.Floor(.5f + 100 * (1 - analysis_info.music_prob));

                analysis_bandwidth = analysis_info.bandwidth;
                if (analysis_bandwidth <= 12)
                    st.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
                else if (analysis_bandwidth <= 14)
                    st.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND;
                else if (analysis_bandwidth <= 16)
                    st.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
                else if (analysis_bandwidth <= 18)
                    st.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
                else
                    st.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;
            }
#endif

            if (st.channels == 2 && st.force_channels != 1)
                stereo_width = compute_stereo_width(pcm, frame_size, st.Fs, st.width_mem);
            else
                stereo_width = 0;
            total_buffer = delay_compensation;
            st.bitrate_bps = user_bitrate_to_bitrate(st, frame_size, max_data_bytes);

            frame_rate = st.Fs / frame_size;
            if (max_data_bytes < 3 || st.bitrate_bps < 3 * frame_rate * 8
               || (frame_rate < 50 && (max_data_bytes * frame_rate < 300 || st.bitrate_bps < 2400)))
            {
                /*If the space is too low to do something useful, emit 'PLC' frames.*/
                int tocmode = st.mode;
                int bw = st.bandwidth == 0 ? OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND : st.bandwidth;
                if (tocmode == 0)
                    tocmode = OpusMode.MODE_SILK_ONLY;
                if (frame_rate > 100)
                    tocmode = OpusMode.MODE_CELT_ONLY;
                if (frame_rate < 50)
                    tocmode = OpusMode.MODE_SILK_ONLY;
                if (tocmode == OpusMode.MODE_SILK_ONLY && bw > OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)
                    bw = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
                else if (tocmode == OpusMode.MODE_CELT_ONLY && bw == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
                    bw = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
                else if (bw <= OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND)
                    bw = OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
                data[0] = gen_toc(tocmode, frame_rate, bw, st.stream_channels);

                return 1;
            }
            if (st.use_vbr == 0)
            {
                int cbrBytes;
                cbrBytes = Inlines.IMIN((st.bitrate_bps + 4 * frame_rate) / (8 * frame_rate), max_data_bytes);
                st.bitrate_bps = cbrBytes * (8 * frame_rate);
                max_data_bytes = cbrBytes;
            }
            max_rate = frame_rate * max_data_bytes * 8;

            /* Equivalent 20-ms rate for mode/channel/bandwidth decisions */
            equiv_rate = st.bitrate_bps - (40 * st.channels + 20) * (st.Fs / frame_size - 50);

            if (st.signal_type == OpusSignal.OPUS_SIGNAL_VOICE)
                voice_est = 127;
            else if (st.signal_type == OpusSignal.OPUS_SIGNAL_MUSIC)
                voice_est = 0;
            else if (st.voice_ratio >= 0)
            {
                voice_est = st.voice_ratio * 327 >> 8;
                /* For AUDIO, never be more than 90% confident of having speech */
                if (st.application == OpusApplication.OPUS_APPLICATION_AUDIO)
                    voice_est = Inlines.IMIN(voice_est, 115);
            }
            else if (st.application == OpusApplication.OPUS_APPLICATION_VOIP)
                voice_est = 115;
            else
                voice_est = 48;

            if (st.force_channels != OpusConstants.OPUS_AUTO && st.channels == 2)
            {
                st.stream_channels = st.force_channels;
            }
            else {
#if FUZZING
        /* Random mono/stereo decision */
        if (st.channels == 2 && (new Random().Next() & 0x1F) == 0)
            st.stream_channels = 3 - st.stream_channels;
#else
                /* Rate-dependent mono-stereo decision */
                if (st.channels == 2)
                {
                    int stereo_threshold;
                    stereo_threshold = Tables.stereo_music_threshold + ((voice_est * voice_est * (Tables.stereo_voice_threshold - Tables.stereo_music_threshold)) >> 14);
                    if (st.stream_channels == 2)
                        stereo_threshold -= 1000;
                    else
                        stereo_threshold += 1000;
                    st.stream_channels = (equiv_rate > stereo_threshold) ? 2 : 1;
                }
                else {
                    st.stream_channels = st.channels;
                }
#endif
            }
            equiv_rate = st.bitrate_bps - (40 * st.stream_channels + 20) * (st.Fs / frame_size - 50);

            /* Mode selection depending on application and signal type */
            if (st.application == OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY)
            {
                st.mode = OpusMode.MODE_CELT_ONLY;
            }
            else if (st.user_forced_mode == OpusConstants.OPUS_AUTO)
            {
#if FUZZING
        /* Random mode switching */
        if ((new Random().Next() & 0xF) == 0)
        {
            if ((new Random().Next() & 0x1) == 0)
                st.mode = OpusMode.MODE_CELT_ONLY;
            else
                st.mode = OpusMode.MODE_SILK_ONLY;
        }
        else {
            if (st.prev_mode == OpusMode.MODE_CELT_ONLY)
                st.mode = OpusMode.MODE_CELT_ONLY;
            else
                st.mode = OpusMode.MODE_SILK_ONLY;
        }
#else
                int mode_voice, mode_music;
                int threshold;

                /* Interpolate based on stereo width */
                mode_voice = (int)(Inlines.MULT16_32_Q15(CeltConstants.Q15ONE - stereo_width, Tables.mode_thresholds[0][0])
                      + Inlines.MULT16_32_Q15(stereo_width, Tables.mode_thresholds[1][0]));
                mode_music = (int)(Inlines.MULT16_32_Q15(CeltConstants.Q15ONE - stereo_width, Tables.mode_thresholds[1][1])
                      + Inlines.MULT16_32_Q15(stereo_width, Tables.mode_thresholds[1][1]));
                /* Interpolate based on speech/music probability */
                threshold = mode_music + ((voice_est * voice_est * (mode_voice - mode_music)) >> 14);
                /* Bias towards SILK for VoIP because of some useful features */
                if (st.application == OpusApplication.OPUS_APPLICATION_VOIP)
                    threshold += 8000;

                /*printf("%f %d\n", stereo_width/(float)1.0f, threshold);*/
                /* Hysteresis */
                if (st.prev_mode == OpusMode.MODE_CELT_ONLY)
                    threshold -= 4000;
                else if (st.prev_mode > 0)
                    threshold += 4000;

                st.mode = (equiv_rate >= threshold) ? OpusMode.MODE_CELT_ONLY : OpusMode.MODE_SILK_ONLY;

                /* When FEC is enabled and there's enough packet loss, use SILK */
                if (st.silk_mode.useInBandFEC != 0 && st.silk_mode.packetLossPercentage > (128 - voice_est) >> 4)
                    st.mode = OpusMode.MODE_SILK_ONLY;
                /* When encoding voice and DTX is enabled, set the encoder to SILK mode (at least for now) */
                if (st.silk_mode.useDTX != 0 && voice_est > 100)
                    st.mode = OpusMode.MODE_SILK_ONLY;
#endif
            }
            else {
                st.mode = st.user_forced_mode;
            }

            /* Override the chosen mode to make sure we meet the requested frame size */
            if (st.mode != OpusMode.MODE_CELT_ONLY && frame_size < st.Fs / 100)
                st.mode = OpusMode.MODE_CELT_ONLY;
            if (st.lfe != 0)
                st.mode = OpusMode.MODE_CELT_ONLY;
            /* If max_data_bytes represents less than 8 kb/s, switch to CELT-only mode */
            if (max_data_bytes < (frame_rate > 50 ? 12000 : 8000) * frame_size / (st.Fs * 8))
                st.mode = OpusMode.MODE_CELT_ONLY;

            if (st.stream_channels == 1 && st.prev_channels == 2 && st.silk_mode.toMono == 0
                  && st.mode != OpusMode.MODE_CELT_ONLY && st.prev_mode != OpusMode.MODE_CELT_ONLY)
            {
                /* Delay stereo.mono transition by two frames so that SILK can do a smooth downmix */
                st.silk_mode.toMono = 1;
                st.stream_channels = 2;
            }
            else {
                st.silk_mode.toMono = 0;
            }

            if (st.prev_mode > 0 &&
                ((st.mode != OpusMode.MODE_CELT_ONLY && st.prev_mode == OpusMode.MODE_CELT_ONLY) ||
            (st.mode == OpusMode.MODE_CELT_ONLY && st.prev_mode != OpusMode.MODE_CELT_ONLY)))
            {
                redundancy = 1;
                celt_to_silk = (st.mode != OpusMode.MODE_CELT_ONLY) ? 1 : 0;
                if (celt_to_silk == 0)
                {
                    /* Switch to SILK/hybrid if frame size is 10 ms or more*/
                    if (frame_size >= st.Fs / 100)
                    {
                        st.mode = st.prev_mode;
                        to_celt = 1;
                    }
                    else {
                        redundancy = 0;
                    }
                }
            }
            /* For the first frame at a new SILK bandwidth */
            if (st.silk_bw_switch != 0)
            {
                redundancy = 1;
                celt_to_silk = 1;
                st.silk_bw_switch = 0;
                prefill = 1;
            }

            if (redundancy != 0)
            {
                /* Fair share of the max size allowed */
                redundancy_bytes = Inlines.IMIN(257, max_data_bytes * (int)(st.Fs / 200) / (frame_size + st.Fs / 200));
                /* For VBR, target the actual bitrate (subject to the limit above) */
                if (st.use_vbr != 0)
                    redundancy_bytes = Inlines.IMIN(redundancy_bytes, st.bitrate_bps / 1600);
            }

            if (st.mode != OpusMode.MODE_CELT_ONLY && st.prev_mode == OpusMode.MODE_CELT_ONLY)
            {
                silk_EncControlStruct dummy = new silk_EncControlStruct();
                enc_API.silk_InitEncoder(silk_enc, st.arch, dummy);
                prefill = 1;
            }

            /* Automatic (rate-dependent) bandwidth selection */
            if (st.mode == OpusMode.MODE_CELT_ONLY || st.first != 0 || st.silk_mode.allowBandwidthSwitch != 0)
            {
                Pointer<int> voice_bandwidth_thresholds;
                Pointer<int> music_bandwidth_thresholds;
                int[] bandwidth_thresholds = new int[8];
                int bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;
                int equiv_rate2;

                equiv_rate2 = equiv_rate;
                if (st.mode != OpusMode.MODE_CELT_ONLY)
                {
                    /* Adjust the threshold +/- 10% depending on complexity */
                    equiv_rate2 = equiv_rate2 * (45 + st.silk_mode.complexity) / 50;
                    /* CBR is less efficient by ~1 kb/s */
                    if (st.use_vbr == 0)
                        equiv_rate2 -= 1000;
                }
                if (st.channels == 2 && st.force_channels != 1)
                {
                    voice_bandwidth_thresholds = Tables.stereo_voice_bandwidth_thresholds.GetPointer();
                    music_bandwidth_thresholds = Tables.stereo_music_bandwidth_thresholds.GetPointer();
                }
                else {
                    voice_bandwidth_thresholds = Tables.mono_voice_bandwidth_thresholds.GetPointer();
                    music_bandwidth_thresholds = Tables.mono_music_bandwidth_thresholds.GetPointer();
                }
                /* Interpolate bandwidth thresholds depending on voice estimation */
                for (i = 0; i < 8; i++)
                {
                    bandwidth_thresholds[i] = music_bandwidth_thresholds[i]
                             + ((voice_est * voice_est * (voice_bandwidth_thresholds[i] - music_bandwidth_thresholds[i])) >> 14);
                }
                do
                {
                    int threshold, hysteresis;
                    threshold = bandwidth_thresholds[2 * (bandwidth - OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)];
                    hysteresis = bandwidth_thresholds[2 * (bandwidth - OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND) + 1];
                    if (st.first == 0)
                    {
                        if (st.bandwidth >= bandwidth)
                            threshold -= hysteresis;
                        else
                            threshold += hysteresis;
                    }
                    if (equiv_rate2 >= threshold)
                        break;
                } while (--bandwidth > OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND);
                st.bandwidth = bandwidth;
                /* Prevents any transition to SWB/FB until the SILK layer has fully
                   switched to WB mode and turned the variable LP filter off */
                if (st.first == 0 && st.mode != OpusMode.MODE_CELT_ONLY && st.silk_mode.inWBmodeWithoutVariableLP == 0 && st.bandwidth > OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)
                    st.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
            }

            if (st.bandwidth > st.max_bandwidth)
                st.bandwidth = st.max_bandwidth;

            if (st.user_bandwidth != OpusConstants.OPUS_AUTO)
                st.bandwidth = st.user_bandwidth;

            /* This prevents us from using hybrid at unsafe CBR/max rates */
            if (st.mode != OpusMode.MODE_CELT_ONLY && max_rate < 15000)
            {
                st.bandwidth = Inlines.IMIN(st.bandwidth, OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND);
            }

            /* Prevents Opus from wasting bits on frequencies that are above
               the Nyquist rate of the input signal */
            if (st.Fs <= 24000 && st.bandwidth > OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND)
                st.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
            if (st.Fs <= 16000 && st.bandwidth > OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)
                st.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
            if (st.Fs <= 12000 && st.bandwidth > OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
                st.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND;
            if (st.Fs <= 8000 && st.bandwidth > OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND)
                st.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
            /* Use detected bandwidth to reduce the encoded bandwidth. */
            if (st.detected_bandwidth != 0 && st.user_bandwidth == OpusConstants.OPUS_AUTO)
            {
                int min_detected_bandwidth;
                /* Makes bandwidth detection more conservative just in case the detector
                   gets it wrong when we could have coded a high bandwidth transparently.
                   When operating in SILK/hybrid mode, we don't go below wideband to avoid
                   more complicated switches that require redundancy. */
                if (equiv_rate <= 18000 * st.stream_channels && st.mode == OpusMode.MODE_CELT_ONLY)
                    min_detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
                else if (equiv_rate <= 24000 * st.stream_channels && st.mode == OpusMode.MODE_CELT_ONLY)
                    min_detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND;
                else if (equiv_rate <= 30000 * st.stream_channels)
                    min_detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
                else if (equiv_rate <= 44000 * st.stream_channels)
                    min_detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
                else
                    min_detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;

                st.detected_bandwidth = Inlines.IMAX(st.detected_bandwidth, min_detected_bandwidth);
                st.bandwidth = Inlines.IMIN(st.bandwidth, st.detected_bandwidth);
            }
            celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_SET_LSB_DEPTH_REQUEST, lsb_depth);

            /* CELT mode doesn't support mediumband, use wideband instead */
            if (st.mode == OpusMode.MODE_CELT_ONLY && st.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
                st.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
            if (st.lfe != 0)
                st.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;

            /* Can't support higher than wideband for >20 ms frames */
            if (frame_size > st.Fs / 50 && (st.mode == OpusMode.MODE_CELT_ONLY || st.bandwidth > OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND))
            {
                Pointer<byte> tmp_data;
                int nb_frames;
                int bak_mode, bak_bandwidth, bak_channels, bak_to_mono;
                OpusRepacketizer rp; // porting note: pointer
                int bytes_per_frame;
                int repacketize_len;

#if ENABLE_ANALYSIS
                if (analysis_read_pos_bak != -1)
                {
                    st.analysis.read_pos = analysis_read_pos_bak;
                    st.analysis.read_subframe = analysis_read_subframe_bak;
                }
#endif

                nb_frames = frame_size > st.Fs / 25 ? 3 : 2;
                bytes_per_frame = Inlines.IMIN(1276, (out_data_bytes - 3) / nb_frames);

                tmp_data = Pointer.Malloc<byte>(nb_frames * bytes_per_frame);

                rp = new OpusRepacketizer();
                repacketizer.opus_repacketizer_init(rp);

                bak_mode = st.user_forced_mode;
                bak_bandwidth = st.user_bandwidth;
                bak_channels = st.force_channels;

                st.user_forced_mode = st.mode;
                st.user_bandwidth = st.bandwidth;
                st.force_channels = st.stream_channels;
                bak_to_mono = st.silk_mode.toMono;

                if (bak_to_mono != 0)
                    st.force_channels = 1;
                else
                    st.prev_channels = st.stream_channels;
                for (i = 0; i < nb_frames; i++)
                {
                    int tmp_len;
                    st.silk_mode.toMono = 0;
                    /* When switching from SILK/Hybrid to CELT, only ask for a switch at the last frame */
                    if (to_celt != 0 && i == nb_frames - 1)
                        st.user_forced_mode = OpusMode.MODE_CELT_ONLY;
                    tmp_len = opus_encode_native(st, pcm.Point(i * (st.channels * st.Fs / 50)), st.Fs / 50,
                          tmp_data.Point(i * bytes_per_frame), bytes_per_frame, lsb_depth,
                          null, 0, c1, c2, analysis_channels, downmix, float_api);
                    if (tmp_len < 0)
                    {

                        return OpusError.OPUS_INTERNAL_ERROR;
                    }
                    ret = repacketizer.opus_repacketizer_cat(rp, tmp_data.Point(i * bytes_per_frame), tmp_len);
                    if (ret < 0)
                    {

                        return OpusError.OPUS_INTERNAL_ERROR;
                    }
                }
                if (st.use_vbr != 0)
                    repacketize_len = out_data_bytes;
                else
                    repacketize_len = Inlines.IMIN(3 * st.bitrate_bps / (3 * 8 * 50 / nb_frames), out_data_bytes);
                ret = repacketizer.opus_repacketizer_out_range_impl(rp, 0, nb_frames, data, repacketize_len, 0, (st.use_vbr == 0) ? 1 : 0);
                if (ret < 0)
                {
                    return OpusError.OPUS_INTERNAL_ERROR;
                }
                st.user_forced_mode = bak_mode;
                st.user_bandwidth = bak_bandwidth;
                st.force_channels = bak_channels;
                st.silk_mode.toMono = bak_to_mono;

                return ret;
            }
            curr_bandwidth = st.bandwidth;

            /* Chooses the appropriate mode for speech
               *NEVER* switch to/from CELT-only mode here as this will invalidate some assumptions */
            if (st.mode == OpusMode.MODE_SILK_ONLY && curr_bandwidth > OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)
                st.mode = OpusMode.MODE_HYBRID;
            if (st.mode == OpusMode.MODE_HYBRID && curr_bandwidth <= OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)
                st.mode = OpusMode.MODE_SILK_ONLY;

            /* printf("%d %d %d %d\n", st.bitrate_bps, st.stream_channels, st.mode, curr_bandwidth); */
            bytes_target = Inlines.IMIN(max_data_bytes - redundancy_bytes, st.bitrate_bps * frame_size / (st.Fs * 8)) - 1;

            data = data.Point(1);

            EntropyCoder.ec_enc_init(enc, data, (uint)(max_data_bytes - 1));

            pcm_buf = Pointer.Malloc<int>((total_buffer + frame_size) * st.channels);
            st.delay_buffer.Point((st.encoder_buffer - total_buffer) * st.channels).MemCopyTo(pcm_buf, total_buffer * st.channels);

            if (st.mode == OpusMode.MODE_CELT_ONLY)
                hp_freq_smth1 = Inlines.silk_LSHIFT(Inlines.silk_lin2log(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ), 8);
            else
                hp_freq_smth1 = silk_enc.state_Fxx[0].sCmn.variable_HP_smth1_Q15;

            st.variable_HP_smth2_Q15 = Inlines.silk_SMLAWB(st.variable_HP_smth2_Q15,
                  hp_freq_smth1 - st.variable_HP_smth2_Q15, Inlines.SILK_FIX_CONST(TuningParameters.VARIABLE_HP_SMTH_COEF2, 16));

            /* convert from log scale to Hertz */
            cutoff_Hz = Inlines.silk_log2lin(Inlines.silk_RSHIFT(st.variable_HP_smth2_Q15, 8));

            if (st.application == OpusApplication.OPUS_APPLICATION_VOIP)
            {
                hp_cutoff(pcm, cutoff_Hz, pcm_buf.Point(total_buffer * st.channels), st.hp_mem, frame_size, st.channels, st.Fs);
            }
            else {
                dc_reject(pcm, 3, pcm_buf.Point(total_buffer * st.channels), st.hp_mem, frame_size, st.channels, st.Fs);
            }

            /* SILK processing */
            HB_gain = CeltConstants.Q15ONE;
            if (st.mode != OpusMode.MODE_CELT_ONLY)
            {
                int total_bitRate, celt_rate;
                Pointer<short> pcm_silk = Pointer.Malloc<short>(st.channels * frame_size);

                /* Distribute bits between SILK and CELT */
                total_bitRate = 8 * bytes_target * frame_rate;
                if (st.mode == OpusMode.MODE_HYBRID)
                {
                    int HB_gain_ref;
                    /* Base rate for SILK */
                    st.silk_mode.bitRate = st.stream_channels * (5000 + 1000 * ((st.Fs == 100 ? 1 : 0) * frame_size));
                    if (curr_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND)
                    {
                        /* SILK gets 2/3 of the remaining bits */
                        st.silk_mode.bitRate += (total_bitRate - st.silk_mode.bitRate) * 2 / 3;
                    }
                    else { /* FULLBAND */
                           /* SILK gets 3/5 of the remaining bits */
                        st.silk_mode.bitRate += (total_bitRate - st.silk_mode.bitRate) * 3 / 5;
                    }
                    /* Don't let SILK use more than 80% */
                    if (st.silk_mode.bitRate > total_bitRate * 4 / 5)
                    {
                        st.silk_mode.bitRate = total_bitRate * 4 / 5;
                    }
                    if (st.energy_masking == null)
                    {
                        /* Increasingly attenuate high band when it gets allocated fewer bits */
                        celt_rate = total_bitRate - st.silk_mode.bitRate;
                        HB_gain_ref = (curr_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND) ? 3000 : 3600;
                        HB_gain = Inlines.SHL32((int)celt_rate, 9) / Inlines.SHR32((int)celt_rate + st.stream_channels * HB_gain_ref, 6);
                        HB_gain = HB_gain < CeltConstants.Q15ONE * 6 / 7 ? HB_gain + CeltConstants.Q15ONE / 7 : CeltConstants.Q15ONE;
                    }
                }
                else {
                    /* SILK gets all bits */
                    st.silk_mode.bitRate = total_bitRate;
                }

                /* Surround masking for SILK */
                if (st.energy_masking != null && st.use_vbr != 0 && st.lfe == 0)
                {
                    int mask_sum = 0;
                    int masking_depth;
                    int rate_offset;
                    int c;
                    int end = 17;
                    short srate = 16000;
                    if (st.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND)
                    {
                        end = 13;
                        srate = 8000;
                    }
                    else if (st.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
                    {
                        end = 15;
                        srate = 12000;
                    }
                    for (c = 0; c < st.channels; c++)
                    {
                        for (i = 0; i < end; i++)
                        {
                            int mask;
                            mask = Inlines.MAX16(Inlines.MIN16(st.energy_masking[21 * c + i],
                                   Inlines.QCONST16(.5f, 10)), -Inlines.QCONST16(2.0f, 10));
                            if (mask > 0)
                                mask = Inlines.HALF16(mask);
                            mask_sum += mask;
                        }
                    }
                    /* Conservative rate reduction, we cut the masking in half */
                    masking_depth = mask_sum / end * st.channels;
                    masking_depth += Inlines.QCONST16(.2f, 10);
                    rate_offset = (int)Inlines.PSHR32(Inlines.MULT16_16(srate, masking_depth), 10);
                    rate_offset = Inlines.MAX32(rate_offset, -2 * st.silk_mode.bitRate / 3);
                    /* Split the rate change between the SILK and CELT part for hybrid. */
                    if (st.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND || st.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_FULLBAND)
                        st.silk_mode.bitRate += 3 * rate_offset / 5;
                    else
                        st.silk_mode.bitRate += rate_offset;
                    bytes_target += rate_offset * frame_size / (8 * st.Fs);
                }

                st.silk_mode.payloadSize_ms = 1000 * frame_size / st.Fs;
                st.silk_mode.nChannelsAPI = st.channels;
                st.silk_mode.nChannelsInternal = st.stream_channels;
                if (curr_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND)
                {
                    st.silk_mode.desiredInternalSampleRate = 8000;
                }
                else if (curr_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
                {
                    st.silk_mode.desiredInternalSampleRate = 12000;
                }
                else {
                    Inlines.OpusAssert(st.mode == OpusMode.MODE_HYBRID || curr_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND);
                    st.silk_mode.desiredInternalSampleRate = 16000;
                }
                if (st.mode == OpusMode.MODE_HYBRID)
                {
                    /* Don't allow bandwidth reduction at lowest bitrates in hybrid mode */
                    st.silk_mode.minInternalSampleRate = 16000;
                }
                else {
                    st.silk_mode.minInternalSampleRate = 8000;
                }

                if (st.mode == OpusMode.MODE_SILK_ONLY)
                {
                    int effective_max_rate = max_rate;
                    st.silk_mode.maxInternalSampleRate = 16000;
                    if (frame_rate > 50)
                        effective_max_rate = effective_max_rate * 2 / 3;
                    if (effective_max_rate < 13000)
                    {
                        st.silk_mode.maxInternalSampleRate = 12000;
                        st.silk_mode.desiredInternalSampleRate = Inlines.IMIN(12000, st.silk_mode.desiredInternalSampleRate);
                    }
                    if (effective_max_rate < 9600)
                    {
                        st.silk_mode.maxInternalSampleRate = 8000;
                        st.silk_mode.desiredInternalSampleRate = Inlines.IMIN(8000, st.silk_mode.desiredInternalSampleRate);
                    }
                }
                else {
                    st.silk_mode.maxInternalSampleRate = 16000;
                }

                st.silk_mode.useCBR = st.use_vbr == 0 ? 1 : 0;

                /* Call SILK encoder for the low band */
                nBytes = Inlines.IMIN(1275, max_data_bytes - 1 - redundancy_bytes);

                st.silk_mode.maxBits = nBytes * 8;
                /* Only allow up to 90% of the bits for hybrid mode*/
                if (st.mode == OpusMode.MODE_HYBRID)
                    st.silk_mode.maxBits = (int)st.silk_mode.maxBits * 9 / 10;
                if (st.silk_mode.useCBR != 0)
                {
                    st.silk_mode.maxBits = (st.silk_mode.bitRate * frame_size / (st.Fs * 8)) * 8;
                    /* Reduce the initial target to make it easier to reach the CBR rate */
                    st.silk_mode.bitRate = Inlines.IMAX(1, st.silk_mode.bitRate - 2000);
                }

                if (prefill != 0)
                {
                    BoxedValue<int> zero = new BoxedValue<int>(0);
                    int prefill_offset;

                    /* Use a smooth onset for the SILK prefill to avoid the encoder trying to encode
                       a discontinuity. The exact location is what we need to avoid leaving any "gap"
                       in the audio when mixing with the redundant CELT frame. Here we can afford to
                       overwrite st.delay_buffer because the only thing that uses it before it gets
                       rewritten is tmp_prefill[] and even then only the part after the ramp really
                       gets used (rather than sent to the encoder and discarded) */
                    prefill_offset = st.channels * (st.encoder_buffer - st.delay_compensation - st.Fs / 400);
                    gain_fade(st.delay_buffer.Point(prefill_offset), st.delay_buffer.Point(prefill_offset),
                          0, CeltConstants.Q15ONE, celt_mode.overlap, st.Fs / 400, st.channels, celt_mode.window, st.Fs);
                    st.delay_buffer.MemSet(0, prefill_offset);

                    // fixme: wasteful conversion here; need to normalize the PCM path to use int16 exclusively
                    for (i = 0; i < st.encoder_buffer * st.channels; i++)
                    {
                        pcm_silk[i] = (short)(st.delay_buffer[i]);
                    }

                    enc_API.silk_Encode(silk_enc, st.silk_mode, pcm_silk, st.encoder_buffer, null, zero, 1);
                }

                for (i = 0; i < frame_size * st.channels; i++)
                {
                    pcm_silk[i] = (short)(pcm_buf[total_buffer * st.channels + i]);
                }

                BoxedValue<int> boxed_silkBytes = new BoxedValue<int>(nBytes);
                ret = enc_API.silk_Encode(silk_enc, st.silk_mode, pcm_silk, frame_size, enc, boxed_silkBytes, 0);
                nBytes = boxed_silkBytes.Val;

                if (ret != 0)
                {
                    /*fprintf (stderr, "SILK encode error: %d\n", ret);*/
                    /* Handle error */

                    return OpusError.OPUS_INTERNAL_ERROR;
                }
                if (nBytes == 0)
                {
                    st.rangeFinal = 0;
                    data[-1] = gen_toc(st.mode, st.Fs / frame_size, curr_bandwidth, st.stream_channels);

                    return 1;
                }
                /* Extract SILK internal bandwidth for signaling in first byte */
                if (st.mode == OpusMode.MODE_SILK_ONLY)
                {
                    if (st.silk_mode.internalSampleRate == 8000)
                    {
                        curr_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
                    }
                    else if (st.silk_mode.internalSampleRate == 12000)
                    {
                        curr_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND;
                    }
                    else if (st.silk_mode.internalSampleRate == 16000)
                    {
                        curr_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
                    }
                }
                else
                {
                    Inlines.OpusAssert(st.silk_mode.internalSampleRate == 16000);
                }

                st.silk_mode.opusCanSwitch = st.silk_mode.switchReady;
                if (st.silk_mode.opusCanSwitch != 0)
                {
                    redundancy = 1;
                    celt_to_silk = 0;
                    st.silk_bw_switch = 1;
                }
            }

            /* CELT processing */
            {
                int endband = 21;

                switch (curr_bandwidth)
                {
                    case OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND:
                        endband = 13;
                        break;
                    case OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND:
                    case OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND:
                        endband = 17;
                        break;
                    case OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND:
                        endband = 19;
                        break;
                    case OpusBandwidth.OPUS_BANDWIDTH_FULLBAND:
                        endband = 21;
                        break;
                }
                celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_SET_END_BAND_REQUEST, endband);
                celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_SET_CHANNELS_REQUEST, st.stream_channels);
            }
            celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_SET_BITRATE_REQUEST, OpusConstants.OPUS_BITRATE_MAX);
            if (st.mode != OpusMode.MODE_SILK_ONLY)
            {
                int celt_pred = 2;
                celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_SET_VBR_REQUEST, 0);
                /* We may still decide to disable prediction later */
                if (st.silk_mode.reducedDependency != 0)
                    celt_pred = 0;
                celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_SET_PREDICTION_REQUEST, celt_pred);

                if (st.mode == OpusMode.MODE_HYBRID)
                {
                    int len;

                    len = (EntropyCoder.ec_tell(enc) + 7) >> 3;
                    if (redundancy != 0)
                        len += st.mode == OpusMode.MODE_HYBRID ? 3 : 1;
                    if (st.use_vbr != 0)
                    {
                        nb_compr_bytes = len + bytes_target - (st.silk_mode.bitRate * frame_size) / (8 * st.Fs);
                    }
                    else {
                        /* check if SILK used up too much */
                        nb_compr_bytes = len > bytes_target ? len : bytes_target;
                    }
                }
                else {
                    if (st.use_vbr != 0)
                    {
                        int bonus = 0;
#if ENABLE_ANALYSIS
                        if (st.variable_duration == OpusFramesize.OPUS_FRAMESIZE_VARIABLE && frame_size != st.Fs / 50)
                        {
                            bonus = (60 * st.stream_channels + 40) * (st.Fs / frame_size - 50);
                            if (analysis_info.valid != 0)
                                bonus = (int)(bonus * (1.0f + .5f * analysis_info.tonality));
                        }
#endif
                        celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_SET_VBR_REQUEST, (1));
                        celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_SET_VBR_CONSTRAINT_REQUEST, (st.vbr_constraint));
                        celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_SET_BITRATE_REQUEST, (st.bitrate_bps + bonus));
                        nb_compr_bytes = max_data_bytes - 1 - redundancy_bytes;
                    }
                    else {
                        nb_compr_bytes = bytes_target;
                    }
                }

            }
            else {
                nb_compr_bytes = 0;
            }


            tmp_prefill = Pointer.Malloc<int>(st.channels * st.Fs / 400);
            if (st.mode != OpusMode.MODE_SILK_ONLY && st.mode != st.prev_mode && st.prev_mode > 0)
            {
                st.delay_buffer.Point((st.encoder_buffer - total_buffer - st.Fs / 400) * st.channels).MemCopyTo(tmp_prefill, st.channels * st.Fs / 400);
            }

            if (st.channels * (st.encoder_buffer - (frame_size + total_buffer)) > 0)
            {
                st.delay_buffer.Point(st.channels * frame_size).MemMoveTo(st.delay_buffer, st.channels * (st.encoder_buffer - frame_size - total_buffer));

                pcm_buf.MemCopyTo(st.delay_buffer.Point(st.channels * (st.encoder_buffer - frame_size - total_buffer)), (frame_size + total_buffer) * st.channels);
            }
            else
            {
                pcm_buf.Point((frame_size + total_buffer - st.encoder_buffer) * st.channels).MemCopyTo(st.delay_buffer, st.encoder_buffer * st.channels);
            }

            /* gain_fade() and stereo_fade() need to be after the buffer copying
               because we don't want any of this to affect the SILK part */
            if (st.prev_HB_gain < CeltConstants.Q15ONE || HB_gain < CeltConstants.Q15ONE)
            {
                gain_fade(pcm_buf, pcm_buf,
                      st.prev_HB_gain, HB_gain, celt_mode.overlap, frame_size, st.channels, celt_mode.window, st.Fs);
            }
            
            st.prev_HB_gain = HB_gain;
            if (st.mode != OpusMode.MODE_HYBRID || st.stream_channels == 1)
                st.silk_mode.stereoWidth_Q14 = Inlines.IMIN((1 << 14), 2 * Inlines.IMAX(0, equiv_rate - 30000));
            if (st.energy_masking == null && st.channels == 2)
            {
                /* Apply stereo width reduction (at low bitrates) */
                if (st.hybrid_stereo_width_Q14 < (1 << 14) || st.silk_mode.stereoWidth_Q14 < (1 << 14))
                {
                    int g1, g2;
                    g1 = st.hybrid_stereo_width_Q14;
                    g2 = (int)(st.silk_mode.stereoWidth_Q14);
                    g1 = g1 == 16384 ? CeltConstants.Q15ONE : Inlines.SHL16(g1, 1);
                    g2 = g2 == 16384 ? CeltConstants.Q15ONE : Inlines.SHL16(g2, 1);
                    stereo_fade(pcm_buf, pcm_buf, g1, g2, celt_mode.overlap,
                          frame_size, st.channels, celt_mode.window, st.Fs);
                    st.hybrid_stereo_width_Q14 = Inlines.CHOP16(st.silk_mode.stereoWidth_Q14);
                }
            }

            if (st.mode != OpusMode.MODE_CELT_ONLY && EntropyCoder.ec_tell(enc) + 17 + 20 * ((st.mode == OpusMode.MODE_HYBRID) ? 1 : 0) <= 8 * (max_data_bytes - 1))
            {
                /* For SILK mode, the redundancy is inferred from the length */
                if (st.mode == OpusMode.MODE_HYBRID && (redundancy != 0 || EntropyCoder.ec_tell(enc) + 37 <= 8 * nb_compr_bytes))
                    EntropyCoder.ec_enc_bit_logp(enc, redundancy, 12);
                if (redundancy != 0)
                {
                    int max_redundancy;
                    EntropyCoder.ec_enc_bit_logp(enc, celt_to_silk, 1);
                    if (st.mode == OpusMode.MODE_HYBRID)
                        max_redundancy = (max_data_bytes - 1) - nb_compr_bytes;
                    else
                        max_redundancy = (max_data_bytes - 1) - ((EntropyCoder.ec_tell(enc) + 7) >> 3);
                    /* Target the same bit-rate for redundancy as for the rest,
                       up to a max of 257 bytes */
                    redundancy_bytes = Inlines.IMIN(max_redundancy, st.bitrate_bps / 1600);
                    redundancy_bytes = Inlines.IMIN(257, Inlines.IMAX(2, redundancy_bytes));
                    if (st.mode == OpusMode.MODE_HYBRID)
                        EntropyCoder.ec_enc_uint(enc, (uint)(redundancy_bytes - 2), 256);
                }
            }
            else {
                redundancy = 0;
            }

            if (redundancy == 0)
            {
                st.silk_bw_switch = 0;
                redundancy_bytes = 0;
            }
            if (st.mode != OpusMode.MODE_CELT_ONLY) start_band = 17;

            if (st.mode == OpusMode.MODE_SILK_ONLY)
            {
                ret = (EntropyCoder.ec_tell(enc) + 7) >> 3;
                EntropyCoder.ec_enc_done(enc);
                nb_compr_bytes = ret;
            }
            else {
                nb_compr_bytes = Inlines.IMIN((max_data_bytes - 1) - redundancy_bytes, nb_compr_bytes);
                EntropyCoder.ec_enc_shrink(enc, (uint)nb_compr_bytes);
            }

#if ENABLE_ANALYSIS
            if (redundancy != 0 || st.mode != OpusMode.MODE_SILK_ONLY)
                celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_SET_ANALYSIS_REQUEST, (analysis_info));
#endif
            /* 5 ms redundant frame for CELT.SILK */
            if (redundancy != 0 && celt_to_silk != 0)
            {
                int err;
                celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_SET_START_BAND_REQUEST, (0));
                celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_SET_VBR_REQUEST, (0));
                err = celt_encoder.celt_encode_with_ec(celt_enc, pcm_buf, st.Fs / 200, data.Point(nb_compr_bytes), redundancy_bytes, null);
                if (err < 0)
                {
                    return OpusError.OPUS_INTERNAL_ERROR;
                }

                BoxedValue<uint> boxed_redundant_rng = new BoxedValue<uint>(redundant_rng);
                celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_GET_FINAL_RANGE_REQUEST, boxed_redundant_rng);
                redundant_rng = boxed_redundant_rng.Val;
                celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_RESET_STATE);
            }

            celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_SET_START_BAND_REQUEST, (start_band));

            if (st.mode != OpusMode.MODE_SILK_ONLY)
            {
                if (st.mode != st.prev_mode && st.prev_mode > 0)
                {
                    byte[] dummy = new byte[2];
                    celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_RESET_STATE);

                    /* Prefilling */
                    celt_encoder.celt_encode_with_ec(celt_enc, tmp_prefill, st.Fs / 400, dummy.GetPointer(), 2, null);
                    celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_SET_PREDICTION_REQUEST, (0));
                }
                /* If false, we already busted the budget and we'll end up with a "PLC packet" */
                if (EntropyCoder.ec_tell(enc) <= 8 * nb_compr_bytes)
                {
                    ret = celt_encoder.celt_encode_with_ec(celt_enc, pcm_buf, frame_size, null, nb_compr_bytes, enc);
                    if (ret < 0)
                    {
                        return OpusError.OPUS_INTERNAL_ERROR;
                    }
                }
            }

            /* 5 ms redundant frame for SILK.CELT */
            if (redundancy != 0 && celt_to_silk == 0)
            {
                int err;
                byte[] dummy = new byte[2];
                int N2, N4;
                N2 = st.Fs / 200;
                N4 = st.Fs / 400;

                celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_RESET_STATE);
                celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_SET_START_BAND_REQUEST, (0));
                celt_encoder.opus_custom_encoder_ctl(celt_enc, CeltControl.CELT_SET_PREDICTION_REQUEST, (0));

                /* NOTE: We could speed this up slightly (at the expense of code size) by just adding a function that prefills the buffer */
                celt_encoder.celt_encode_with_ec(celt_enc, pcm_buf.Point(st.channels * (frame_size - N2 - N4)), N4, dummy.GetPointer(), 2, null);

                err = celt_encoder.celt_encode_with_ec(celt_enc, pcm_buf.Point(st.channels * (frame_size - N2)), N2, data.Point(nb_compr_bytes), redundancy_bytes, null);
                if (err < 0)
                {
                    return OpusError.OPUS_INTERNAL_ERROR;
                }
                BoxedValue<uint> boxed_redundant_rng = new BoxedValue<uint>(redundant_rng);
                celt_encoder.opus_custom_encoder_ctl(celt_enc, OpusControl.OPUS_GET_FINAL_RANGE_REQUEST, (boxed_redundant_rng));
                redundant_rng = boxed_redundant_rng.Val;
            }

            /* Signalling the mode in the first byte */
            data = data.Point(-1);
            data[0] = gen_toc(st.mode, st.Fs / frame_size, curr_bandwidth, st.stream_channels);

            st.rangeFinal = enc.rng ^ redundant_rng;

            if (to_celt != 0)
                st.prev_mode = OpusMode.MODE_CELT_ONLY;
            else
                st.prev_mode = st.mode;
            st.prev_channels = st.stream_channels;
            st.prev_framesize = frame_size;

            st.first = 0;

            /* In the unlikely case that the SILK encoder busted its target, tell
               the decoder to call the PLC */
            if (EntropyCoder.ec_tell(enc) > (max_data_bytes - 1) * 8)
            {
                if (max_data_bytes < 2)
                {
                    return OpusError.OPUS_BUFFER_TOO_SMALL;
                }
                data[1] = 0;
                ret = 1;
                st.rangeFinal = 0;
            }
            else if (st.mode == OpusMode.MODE_SILK_ONLY && redundancy == 0)
            {
                /*When in LPC only mode it's perfectly
                  reasonable to strip off trailing zero bytes as
                  the required range decoder behavior is to
                  fill these in. This can't be done when the MDCT
                  modes are used because the decoder needs to know
                  the actual length for allocation purposes.*/
                while (ret > 2 && data[ret] == 0) ret--;
            }
            /* Count ToC and redundancy */
            ret += 1 + redundancy_bytes;
            if (st.use_vbr == 0)
            {
                if (repacketizer.opus_packet_pad(data, ret, max_data_bytes) != OpusError.OPUS_OK)
                {
                    return OpusError.OPUS_INTERNAL_ERROR;
                }
                ret = max_data_bytes;
            }

            return ret;
        }

        /** Encodes an Opus frame.
  * @param [in] st <tt>OpusEncoder*</tt>: Encoder state
  * @param [in] pcm <tt>opus_int16*</tt>: Input signal (interleaved if 2 channels). length is frame_size*channels*sizeof(opus_int16)
  * @param [in] frame_size <tt>int</tt>: Number of samples per channel in the
  *                                      input signal.
  *                                      This must be an Opus frame size for
  *                                      the encoder's sampling rate.
  *                                      For example, at 48 kHz the permitted
  *                                      values are 120, 240, 480, 960, 1920,
  *                                      and 2880.
  *                                      Passing in a duration of less than
  *                                      10 ms (480 samples at 48 kHz) will
  *                                      prevent the encoder from using the LPC
  *                                      or hybrid modes.
  * @param [out] data <tt>unsigned char*</tt>: Output payload.
  *                                            This must contain storage for at
  *                                            least \a max_data_bytes.
  * @param [in] max_data_bytes <tt>opus_int32</tt>: Size of the allocated
  *                                                 memory for the output
  *                                                 payload. This may be
  *                                                 used to impose an upper limit on
  *                                                 the instant bitrate, but should
  *                                                 not be used as the only bitrate
  *                                                 control. Use #OPUS_SET_BITRATE to
  *                                                 control the bitrate.
  * @returns The length of the encoded packet (in bytes) on success or a
  *          negative error code (see @ref opus_errorcodes) on failure.
  */
        public static int opus_encode(OpusEncoder st, Pointer<short> pcm, int analysis_frame_size,
              Pointer<byte> data, int out_data_bytes)
        {
            int i;
            int frame_size;
            int delay_compensation;
            if (st.application == OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY)
                delay_compensation = 0;
            else
                delay_compensation = st.delay_compensation;

            frame_size = compute_frame_size(pcm, analysis_frame_size,
                  st.variable_duration, st.channels, st.Fs, st.bitrate_bps,
                  delay_compensation, downmix_int
#if ENABLE_ANALYSIS
                  , st.analysis.subframe_mem
#endif
                  );

            // fixme: does this belong here?
            Pointer<int> input = Pointer.Malloc<int>(frame_size * st.channels);
            for (i = 0; i < frame_size * st.channels; i++)
                input[i] = (int)pcm[i];

            return opus_encode_native<short>(st, input, frame_size, data, out_data_bytes, 16,
                                     pcm, analysis_frame_size, 0, -2, st.channels, downmix_int, 0);
        }

        /** Encodes an Opus frame from floating point input.
  * @param [in] st <tt>OpusEncoder*</tt>: Encoder state
  * @param [in] pcm <tt>float*</tt>: Input in float format (interleaved if 2 channels), with a normal range of +/-1.0.
  *          Samples with a range beyond +/-1.0 are supported but will
  *          be clipped by decoders using the integer API and should
  *          only be used if it is known that the far end supports
  *          extended dynamic range.
  *          length is frame_size*channels*sizeof(float)
  * @param [in] frame_size <tt>int</tt>: Number of samples per channel in the
  *                                      input signal.
  *                                      This must be an Opus frame size for
  *                                      the encoder's sampling rate.
  *                                      For example, at 48 kHz the permitted
  *                                      values are 120, 240, 480, 960, 1920,
  *                                      and 2880.
  *                                      Passing in a duration of less than
  *                                      10 ms (480 samples at 48 kHz) will
  *                                      prevent the encoder from using the LPC
  *                                      or hybrid modes.
  * @param [out] data <tt>unsigned char*</tt>: Output payload.
  *                                            This must contain storage for at
  *                                            least \a max_data_bytes.
  * @param [in] max_data_bytes <tt>opus_int32</tt>: Size of the allocated
  *                                                 memory for the output
  *                                                 payload. This may be
  *                                                 used to impose an upper limit on
  *                                                 the instant bitrate, but should
  *                                                 not be used as the only bitrate
  *                                                 control. Use #OPUS_SET_BITRATE to
  *                                                 control the bitrate.
  * @returns The length of the encoded packet (in bytes) on success or a
  *          negative error code (see @ref opus_errorcodes) on failure.
  */
        public static int opus_encode_float(OpusEncoder st, Pointer<float> pcm, int analysis_frame_size,
                              Pointer<byte> data, int max_data_bytes)
        {
            int i, ret;
            int frame_size;
            int delay_compensation;
            Pointer<int> input;

            if (st.application == OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY)
                delay_compensation = 0;
            else
                delay_compensation = st.delay_compensation;

            frame_size = compute_frame_size(pcm, analysis_frame_size,
                  st.variable_duration, st.channels, st.Fs, st.bitrate_bps,
                  delay_compensation, downmix_float
#if ENABLE_ANALYSIS
                  , st.analysis.subframe_mem
#endif
                  );

            input = Pointer.Malloc<int>(frame_size * st.channels);

            for (i = 0; i < frame_size * st.channels; i++)
                input[i] = Inlines.FLOAT2INT16(pcm[i]);

            ret = opus_encode_native(st, input, frame_size, data, max_data_bytes, 16,
                                     pcm, analysis_frame_size, 0, -2, st.channels, downmix_float, 1);
            return ret;
        }
    }
}
