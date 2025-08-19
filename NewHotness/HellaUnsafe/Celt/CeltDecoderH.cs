using System.Diagnostics;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.Bands;
using static HellaUnsafe.Celt.Celt;
using static HellaUnsafe.Celt.CeltH;
using static HellaUnsafe.Celt.CeltLPC;
using static HellaUnsafe.Celt.CELTModeH;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Celt.MDCT;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Celt.QuantBands;
using static HellaUnsafe.Celt.Rate;
using static HellaUnsafe.Celt.VQ;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Celt
{
    internal static unsafe class CeltDecoderH
    {
        /* The maximum pitch lag to allow in the pitch-based PLC. It's possible to save
           CPU time in the PLC pitch search by making this smaller than MAX_PERIOD. The
           current value corresponds to a pitch of 66.67 Hz. */
        internal const int PLC_PITCH_LAG_MAX = 720;

        /* The minimum pitch lag to allow in the pitch-based PLC. This corresponds to a
            pitch of 480 Hz. */
        internal const int PLC_PITCH_LAG_MIN = 100;

        internal const int DECODE_BUFFER_SIZE = 2048;

        internal const int PLC_UPDATE_FRAMES = 4;
        internal const int PLC_UPDATE_SAMPLES = (PLC_UPDATE_FRAMES * FRAME_SIZE);

        // Alias of CELTDecoder, but for this port we just keep the name OpusCustomDecoder
        internal unsafe struct OpusCustomDecoder
        {
            internal OpusCustomMode* mode;
            internal int overlap;
            internal int channels;
            internal int stream_channels;

            internal int downsample;
            internal int start, end;
            internal int signalling;
            internal int disable_inv;
            internal int complexity;

            /* Everything beyond this point gets cleared on a reset */
            //#define DECODER_RESET_START rng

            internal uint rng;
            internal int error;
            internal int last_pitch_index;
            internal int loss_duration;
            internal int skip_plc;
            internal int postfilter_period;
            internal int postfilter_period_old;
            internal float postfilter_gain;
            internal float postfilter_gain_old;
            internal int postfilter_tapset;
            internal int postfilter_tapset_old;
            internal int prefilter_and_fold;

            internal fixed float preemph_memD[2];

            internal fixed float _decode_mem[1]; /* Size = channels*(DECODE_BUFFER_SIZE+mode->overlap) */
            /* float lpc[],  Size = channels*CELT_LPC_ORDER */
            /* float oldEBands[], Size = 2*mode->nbEBands */
            /* float oldLogE[], Size = 2*mode->nbEBands */
            /* float oldLogE2[], Size = 2*mode->nbEBands */
            /* float backgroundLogE[], Size = 2*mode->nbEBands */
        };

        /* Make basic checks on the CELT state to ensure we don't end
            up writing all over memory. */
        [Conditional("DEBUG")]
        internal static unsafe void validate_celt_decoder(OpusCustomDecoder* st)
        {
            celt_assert(st->mode == opus_custom_mode_create(48000, 960, out _));
            celt_assert(st->overlap == 120);
            celt_assert(st->end <= 21);
            celt_assert(st->channels == 1 || st->channels == 2);
            celt_assert(st->stream_channels == 1 || st->stream_channels == 2);
            celt_assert(st->downsample > 0);
            celt_assert(st->start == 0 || st->start == 17);
            celt_assert(st->start < st->end);
            celt_assert(st->last_pitch_index <= PLC_PITCH_LAG_MAX);
            celt_assert(st->last_pitch_index >= PLC_PITCH_LAG_MIN || st->last_pitch_index == 0);
            celt_assert(st->postfilter_period < MAX_PERIOD);
            celt_assert(st->postfilter_period >= COMBFILTER_MINPERIOD || st->postfilter_period == 0);
            celt_assert(st->postfilter_period_old < MAX_PERIOD);
            celt_assert(st->postfilter_period_old >= COMBFILTER_MINPERIOD || st->postfilter_period_old == 0);
            celt_assert(st->postfilter_tapset <= 2);
            celt_assert(st->postfilter_tapset >= 0);
            celt_assert(st->postfilter_tapset_old <= 2);
            celt_assert(st->postfilter_tapset_old >= 0);
        }

        internal static unsafe int celt_decoder_get_size(int channels)
        {
            OpusCustomMode* mode = opus_custom_mode_create(48000, 960, out _);
            return opus_custom_decoder_get_size(mode, channels);
        }

        internal static unsafe int opus_custom_decoder_get_size(in OpusCustomMode* mode, int channels)
        {
            int size = sizeof(OpusCustomDecoder)
                     + (channels * (DECODE_BUFFER_SIZE + mode->overlap) - 1) * sizeof(float)
                     + channels * CELT_LPC_ORDER * sizeof(float)
                     + 4 * 2 * mode->nbEBands * sizeof(float);
            return size;
        }

        internal static unsafe int celt_decoder_init(OpusCustomDecoder* st, int sampling_rate, int channels)
        {
            int ret;
            ret = opus_custom_decoder_init(st, opus_custom_mode_create(48000, 960, out _), channels);
            if (ret != OPUS_OK)
                return ret;
            st->downsample = resampling_factor(sampling_rate);
            if (st->downsample == 0)
                return OPUS_BAD_ARG;
            else
                return OPUS_OK;
        }

        internal static unsafe int opus_custom_decoder_init(OpusCustomDecoder* st, in OpusCustomMode* mode, int channels)
        {
            if (channels < 0 || channels > 2)
                return OPUS_BAD_ARG;

            if (st == null)
                return OPUS_ALLOC_FAIL;

            OPUS_CLEAR((byte*)st, opus_custom_decoder_get_size(mode, channels));

            st->mode = mode;
            st->overlap = mode->overlap;
            st->stream_channels = st->channels = channels;

            st->downsample = 1;
            st->start = 0;
            st->end = st->mode->effEBands;
            st->signalling = 1;
            st->disable_inv = BOOL2INT(channels == 1);

            opus_custom_decoder_ctl(st, OPUS_RESET_STATE);

            return OPUS_OK;
        }

        /* Special case for stereo with no downsampling and no accumulation. This is
           quite common and we can make it faster by processing both channels in the
           same loop, reducing overhead due to the dependency loop in the IIR filter. */
        internal static unsafe void deemphasis_stereo_simple(float** input, float* pcm, int N, in float coef0,
              float* mem)
        {
            float* x0;
            float* x1;
            float m0, m1;
            int j;
            x0 = input[0];
            x1 = input[1];
            m0 = mem[0];
            m1 = mem[1];
            for (j = 0; j < N; j++)
            {
                float tmp0, tmp1;
                /* Add VERY_SMALL to x[] first to reduce dependency chain. */
                tmp0 = x0[j] + VERY_SMALL + m0;
                tmp1 = x1[j] + VERY_SMALL + m1;
                m0 = MULT16_32_Q15(coef0, tmp0);
                m1 = MULT16_32_Q15(coef0, tmp1);
                pcm[2 * j] = SCALEOUT(SIG2WORD16(tmp0));
                pcm[2 * j + 1] = SCALEOUT(SIG2WORD16(tmp1));
            }
            mem[0] = m0;
            mem[1] = m1;
        }

        internal static unsafe void deemphasis(float** input, float* pcm, int N, int C, int downsample, in float* coef,
              float* mem, int accum)
        {
            int c;
            int Nd;
            int apply_downsampling = 0;
            float coef0;
            /* Short version for common case. */
            if (downsample == 1 && C == 2 && accum == 0)
            {
                deemphasis_stereo_simple(input, pcm, N, coef[0], mem);
                return;
            }
            celt_assert(accum == 0);
            float[] scratch_data = new float[N];
            fixed (float* scratch = scratch_data)
            {
                coef0 = coef[0];
                Nd = N / downsample;
                c = 0; do
                {
                    int j;
                    float* x;
                    float* y;
                    float m = mem[c];
                    x = input[c];
                    y = pcm + c;
                    if (downsample > 1)
                    {
                        /* Shortcut for the standard (non-custom modes) case */
                        for (j = 0; j < N; j++)
                        {
                            float tmp = x[j] + VERY_SMALL + m;
                            m = MULT16_32_Q15(coef0, tmp);
                            scratch[j] = tmp;
                        }
                        apply_downsampling = 1;
                    }
                    else
                    {
                        /* Shortcut for the standard (non-custom modes) case */
                        {
                            for (j = 0; j < N; j++)
                            {
                                float tmp = x[j] + VERY_SMALL + m;
                                m = MULT16_32_Q15(coef0, tmp);
                                y[j * C] = SCALEOUT(SIG2WORD16(tmp));
                            }
                        }
                    }
                    mem[c] = m;

                    if (apply_downsampling != 0)
                    {
                        /* Perform down-sampling */
                        {
                            for (j = 0; j < Nd; j++)
                                y[j * C] = SCALEOUT(SIG2WORD16(scratch[j * downsample]));
                        }
                    }
                } while (++c < C);
            }
        }

        internal static unsafe void celt_synthesis(in OpusCustomMode* mode, float* X, float** out_syn,
                    float* oldBandE, int start, int effEnd, int C, int CC,
                    int isTransient, int LM, int downsample,
                    int silence)
        {
            int c, i;
            int M;
            int b;
            int B;
            int N, NB;
            int shift;
            int nbEBands;
            int overlap;

            overlap = mode->overlap;
            nbEBands = mode->nbEBands;
            N = mode->shortMdctSize << LM;
            float[] freq_data = new float[N];/**< Interleaved signal MDCTs */
            fixed (float* freq = freq_data)
            {
                M = 1 << LM;

                if (isTransient != 0)
                {
                    B = M;
                    NB = mode->shortMdctSize;
                    shift = mode->maxLM;
                }
                else
                {
                    B = 1;
                    NB = mode->shortMdctSize << LM;
                    shift = mode->maxLM - LM;
                }

                if (CC == 2 && C == 1)
                {
                    /* Copying a mono streams to two channels */
                    float* freq2;
                    denormalise_bands(mode, X, freq, oldBandE, start, effEnd, M,
                          downsample, silence);
                    /* Store a temporary copy in the output buffer because the IMDCT destroys its input. */
                    freq2 = out_syn[1] + overlap / 2;
                    OPUS_COPY(freq2, freq, N);
                    for (b = 0; b < B; b++)
                        clt_mdct_backward(&mode->mdct, &freq2[b], out_syn[0] + NB * b, mode->window, overlap, shift, B);
                    for (b = 0; b < B; b++)
                        clt_mdct_backward(&mode->mdct, &freq[b], out_syn[1] + NB * b, mode->window, overlap, shift, B);
                }
                else if (CC == 1 && C == 2)
                {
                    /* Downmixing a stereo stream to mono */
                    float* freq2;
                    freq2 = out_syn[0] + overlap / 2;
                    denormalise_bands(mode, X, freq, oldBandE, start, effEnd, M,
                          downsample, silence);
                    /* Use the output buffer as temp array before downmixing. */
                    denormalise_bands(mode, X + N, freq2, oldBandE + nbEBands, start, effEnd, M,
                          downsample, silence);
                    for (i = 0; i < N; i++)
                        freq[i] = ADD32(HALF32(freq[i]), HALF32(freq2[i]));
                    for (b = 0; b < B; b++)
                        clt_mdct_backward(&mode->mdct, &freq[b], out_syn[0] + NB * b, mode->window, overlap, shift, B);
                }
                else
                {
                    /* Normal case (mono or stereo) */
                    c = 0; do
                    {
                        denormalise_bands(mode, X + c * N, freq, oldBandE + c * nbEBands, start, effEnd, M,
                              downsample, silence);
                        for (b = 0; b < B; b++)
                            clt_mdct_backward(&mode->mdct, &freq[b], out_syn[c] + NB * b, mode->window, overlap, shift, B);
                    } while (++c < CC);
                }
                /* Saturate IMDCT output so that we can't overflow in the pitch postfilter
                   or in the */
                c = 0; do
                {
                    for (i = 0; i < N; i++)
                        out_syn[c][i] = out_syn[c][i];
                } while (++c < CC);
            }
        }

        internal static unsafe void tf_decode(int start, int end, int isTransient, int* tf_res, int LM, ec_ctx* dec)
        {
            int i, curr, tf_select;
            int tf_select_rsv;
            int tf_changed;
            int logp;
            uint budget;
            uint tell;

            budget = dec->storage * 8;
            tell = (uint)ec_tell(dec);
            logp = isTransient != 0 ? 2 : 4;
            tf_select_rsv = BOOL2INT(LM > 0 && tell + logp + 1 <= budget);
            budget = (uint)(budget - tf_select_rsv);
            tf_changed = curr = 0;
            for (i = start; i < end; i++)
            {
                if (tell + logp <= budget)
                {
                    curr ^= ec_dec_bit_logp(dec, (uint)logp);
                    tell = (uint)ec_tell(dec);
                    tf_changed |= curr;
                }
                tf_res[i] = curr;
                logp = isTransient != 0 ? 4 : 5;
            }
            tf_select = 0;
            if (tf_select_rsv != 0 &&
              tf_select_table[LM][4 * isTransient + 0 + tf_changed] !=
              tf_select_table[LM][4 * isTransient + 2 + tf_changed])
            {
                tf_select = ec_dec_bit_logp(dec, 1);
            }
            for (i = start; i < end; i++)
            {
                tf_res[i] = tf_select_table[LM][4 * isTransient + 2 * tf_select + tf_res[i]];
            }
        }

        internal static unsafe int celt_plc_pitch_search(float** decode_mem/*[2]*/, int C)
        {
            int pitch_index;
            float[] lp_pitch_buf_data = new float[DECODE_BUFFER_SIZE >> 1];
            fixed (float* lp_pitch_buf = lp_pitch_buf_data)
            {
                pitch_downsample(decode_mem, lp_pitch_buf,
                      DECODE_BUFFER_SIZE, C);
                pitch_search(lp_pitch_buf + (PLC_PITCH_LAG_MAX >> 1), lp_pitch_buf,
                      DECODE_BUFFER_SIZE - PLC_PITCH_LAG_MAX,
                      PLC_PITCH_LAG_MAX - PLC_PITCH_LAG_MIN, &pitch_index);
                pitch_index = PLC_PITCH_LAG_MAX - pitch_index;
                return pitch_index;
            }
        }

        internal static unsafe void prefilter_and_fold(OpusCustomDecoder* st, int N)
        {
            int c;
            int CC;
            int i;
            int overlap;
            float** decode_mem = stackalloc float*[2];
            OpusCustomMode* mode;
            mode = st->mode;
            overlap = st->overlap;
            CC = st->channels;
            float[] etmp_data = new float[overlap];
            fixed (float* etmp = etmp_data)
            {
                c = 0; do
                {
                    decode_mem[c] = st->_decode_mem + c * (DECODE_BUFFER_SIZE + overlap);
                } while (++c < CC);

                c = 0; do
                {
                    /* Apply the pre-filter to the MDCT overlap for the next frame because
                       the post-filter will be re-applied in the decoder after the MDCT
                       overlap. */
                    comb_filter(etmp, decode_mem[c] + DECODE_BUFFER_SIZE - N,
                       st->postfilter_period_old, st->postfilter_period, overlap,
                       -st->postfilter_gain_old, -st->postfilter_gain,
                       st->postfilter_tapset_old, st->postfilter_tapset, null, 0);

                    /* Simulate TDAC on the concealed audio so that it blends with the
                       MDCT of the next frame. */
                    for (i = 0; i < overlap / 2; i++)
                    {
                        decode_mem[c][DECODE_BUFFER_SIZE - N + i] =
                           MULT16_32_Q15(mode->window[i], etmp[overlap - 1 - i])
                           + MULT16_32_Q15(mode->window[overlap - i - 1], etmp[i]);
                    }
                } while (++c < CC);
            }
        }

        internal static void celt_decode_lost(OpusCustomDecoder* st, int N, int LM)
        {
            int c;
            int i;
            int C = st->channels;
            float** decode_mem = stackalloc float*[2];
            float** out_syn = stackalloc float*[2];
            float* lpc;
            float* oldBandE, oldLogE, oldLogE2, backgroundLogE;
            OpusCustomMode* mode;
            int nbEBands;
            int overlap;
            int start;
            int loss_duration;
            int noise_based;
            short* eBands;

            mode = st->mode;
            nbEBands = mode->nbEBands;
            overlap = mode->overlap;
            eBands = mode->eBands;

            c = 0; do
            {
                decode_mem[c] = st->_decode_mem + c * (DECODE_BUFFER_SIZE + overlap);
                out_syn[c] = decode_mem[c] + DECODE_BUFFER_SIZE - N;
            } while (++c < C);
            lpc = (float*)(st->_decode_mem + (DECODE_BUFFER_SIZE + overlap) * C);
            oldBandE = lpc + C * CELT_LPC_ORDER;
            oldLogE = oldBandE + 2 * nbEBands;
            oldLogE2 = oldLogE + 2 * nbEBands;
            backgroundLogE = oldLogE2 + 2 * nbEBands;

            loss_duration = st->loss_duration;
            start = st->start;
            noise_based = BOOL2INT(loss_duration >= 40 || start != 0 || st->skip_plc != 0);
            if (noise_based != 0)
            {
                /* Noise-based PLC/CNG */
                uint seed;
                int end;
                int effEnd;
                float decay;
                end = st->end;
                effEnd = IMAX(start, IMIN(end, mode->effEBands));

                float[] X_data = new float[C * N];
                fixed (float* X = X_data)   /**< Interleaved normalised MDCTs */
                {
                    c = 0; do
                    {
                        OPUS_MOVE(decode_mem[c], decode_mem[c] + N,
                              DECODE_BUFFER_SIZE - N + overlap);
                    } while (++c < C);

                    if (st->prefilter_and_fold != 0)
                    {
                        prefilter_and_fold(st, N);
                    }

                    /* Energy decay */
                    decay = loss_duration == 0 ? QCONST16(1.5f, DB_SHIFT) : QCONST16(.5f, DB_SHIFT);
                    c = 0; do
                    {
                        for (i = start; i < end; i++)
                            oldBandE[c * nbEBands + i] = MAX16(backgroundLogE[c * nbEBands + i], oldBandE[c * nbEBands + i] - decay);
                    } while (++c < C);
                    seed = st->rng;
                    for (c = 0; c < C; c++)
                    {
                        for (i = start; i < effEnd; i++)
                        {
                            int j;
                            int boffs;
                            int blen;
                            boffs = N * c + (eBands[i] << LM);
                            blen = (eBands[i + 1] - eBands[i]) << LM;
                            for (j = 0; j < blen; j++)
                            {
                                seed = celt_lcg_rand(seed);
                                X[boffs + j] = (float)((int)seed >> 20);
                            }
                            renormalise_vector(X + boffs, blen, Q15ONE);
                        }
                    }
                    st->rng = seed;

                    celt_synthesis(mode, X, out_syn, oldBandE, start, effEnd, C, C, 0, LM, st->downsample, 0);
                    st->prefilter_and_fold = 0;
                    /* Skip regular PLC until we get two consecutive packets. */
                    st->skip_plc = 1;
                }
            }
            else
            {
                int exc_length;
                /* Pitch-based PLC */
                float* window;
                float* exc;
                float fade = Q15ONE;
                int pitch_index;

                if (loss_duration == 0)
                {
                    st->last_pitch_index = pitch_index = celt_plc_pitch_search(decode_mem, C);
                }
                else
                {
                    pitch_index = st->last_pitch_index;
                    fade = QCONST16(.8f, 15);
                }

                /* We want the excitation for 2 pitch periods in order to look for a
                   decaying signal, but we can't get more than MAX_PERIOD. */
                exc_length = IMIN(2 * pitch_index, MAX_PERIOD);

                float[] _exc_data = new float[MAX_PERIOD + CELT_LPC_ORDER];
                float[] fir_tmp_data = new float[exc_length];
                fixed (float* _exc = _exc_data)
                fixed (float* fir_tmp = fir_tmp_data)
                {
                    exc = _exc + CELT_LPC_ORDER;
                    window = mode->window;
                    c = 0; do
                    {
                        float decay;
                        float attenuation;
                        float S1 = 0;
                        float* buf;
                        int extrapolation_offset;
                        int extrapolation_len;
                        int j;

                        buf = decode_mem[c];
                        for (i = 0; i < MAX_PERIOD + CELT_LPC_ORDER; i++)
                            exc[i - CELT_LPC_ORDER] = buf[DECODE_BUFFER_SIZE - MAX_PERIOD - CELT_LPC_ORDER + i];

                        if (loss_duration == 0)
                        {
                            float* ac = stackalloc float[CELT_LPC_ORDER + 1];
                            /* Compute LPC coefficients for the last MAX_PERIOD samples before
                               the first loss so we can work in the excitation-filter domain. */
                            _celt_autocorr(exc, ac, window, overlap,
                                   CELT_LPC_ORDER, MAX_PERIOD);
                            /* Add a noise floor of -40 dB. */
                            ac[0] *= 1.0001f;
                            /* Use lag windowing to stabilize the Levinson-Durbin recursion. */
                            for (i = 1; i <= CELT_LPC_ORDER; i++)
                            {
                                /*ac[i] *= exp(-.5*(2*M_PI*.002*i)*(2*M_PI*.002*i));*/
                                ac[i] -= ac[i] * (0.008f * 0.008f) * i * i;
                            }
                            _celt_lpc(lpc + c * CELT_LPC_ORDER, ac, CELT_LPC_ORDER);
                        }
                        /* Initialize the LPC history with the samples just before the start
                           of the region for which we're computing the excitation. */
                        {
                            /* Compute the excitation for exc_length samples before the loss. We need the copy
                               because celt_fir() cannot filter in-place. */
                            celt_fir(exc + MAX_PERIOD - exc_length, lpc + c * CELT_LPC_ORDER,
                                  fir_tmp, exc_length, CELT_LPC_ORDER);
                            OPUS_COPY(exc + MAX_PERIOD - exc_length, fir_tmp, exc_length);
                        }

                        /* Check if the waveform is decaying, and if so how fast.
                           We do this to avoid adding energy when concealing in a segment
                           with decaying energy. */
                        {
                            float E1 = 1, E2 = 1;
                            int decay_length;
                            decay_length = exc_length >> 1;
                            for (i = 0; i < decay_length; i++)
                            {
                                float e;
                                e = exc[MAX_PERIOD - decay_length + i];
                                E1 += MULT16_16(e, e);
                                e = exc[MAX_PERIOD - 2 * decay_length + i];
                                E2 += MULT16_16(e, e);
                            }
                            E1 = MIN32(E1, E2);
                            decay = celt_sqrt(frac_div32(SHR32(E1, 1), E2));
                        }

                        /* Move the decoder memory one frame to the left to give us room to
                           add the data for the new frame. We ignore the overlap that extends
                           past the end of the buffer, because we aren't going to use it. */
                        OPUS_MOVE(buf, buf + N, DECODE_BUFFER_SIZE - N);

                        /* Extrapolate from the end of the excitation with a period of
                           "pitch_index", scaling down each period by an additional factor of
                           "decay". */
                        extrapolation_offset = MAX_PERIOD - pitch_index;
                        /* We need to extrapolate enough samples to cover a complete MDCT
                           window (including overlap/2 samples on both sides). */
                        extrapolation_len = N + overlap;
                        /* We also apply fading if this is not the first loss. */
                        attenuation = MULT16_16_Q15(fade, decay);
                        for (i = j = 0; i < extrapolation_len; i++, j++)
                        {
                            float tmp;
                            if (j >= pitch_index)
                            {
                                j -= pitch_index;
                                attenuation = MULT16_16_Q15(attenuation, decay);
                            }
                            buf[DECODE_BUFFER_SIZE - N + i] =
                                  SHL32(EXTEND32(MULT16_16_Q15(attenuation,
                                        exc[extrapolation_offset + j])), 0);
                            /* Compute the energy of the previously decoded signal whose
                               excitation we're copying. */
                            tmp = SROUND16(
                                  buf[DECODE_BUFFER_SIZE - MAX_PERIOD - N + extrapolation_offset + j],
                                  0);
                            S1 += SHR32(MULT16_16(tmp, tmp), 10);
                        }
                        {
                            float* lpc_mem = stackalloc float[CELT_LPC_ORDER];
                            /* Copy the last decoded samples (prior to the overlap region) to
                               synthesis filter memory so we can have a continuous signal. */
                            for (i = 0; i < CELT_LPC_ORDER; i++)
                                lpc_mem[i] = SROUND16(buf[DECODE_BUFFER_SIZE - N - 1 - i], 0);
                            /* Apply the synthesis filter to convert the excitation back into
                               the signal domain. */
                            celt_iir(buf + DECODE_BUFFER_SIZE - N, lpc + c * CELT_LPC_ORDER,
                                  buf + DECODE_BUFFER_SIZE - N, extrapolation_len, CELT_LPC_ORDER,
                                  lpc_mem);
                        }

                        /* Check if the synthesis energy is higher than expected, which can
                           happen with the signal changes during our window. If so,
                           attenuate. */
                        {
                            float S2 = 0;
                            for (i = 0; i < extrapolation_len; i++)
                            {
                                float tmp = SROUND16(buf[DECODE_BUFFER_SIZE - N + i], 0);
                                S2 += SHR32(MULT16_16(tmp, tmp), 10);
                            }
                            /* This checks for an "explosion" in the synthesis. */
                            /* The float test is written this way to catch NaNs in the output
                            of the IIR filter at the same time. */
                            if (!(S1 > 0.2f * S2))
                            {
                                for (i = 0; i < extrapolation_len; i++)
                                    buf[DECODE_BUFFER_SIZE - N + i] = 0;
                            }
                            else if (S1 < S2)
                            {
                                float ratio = celt_sqrt(frac_div32(SHR32(S1, 1) + 1, S2 + 1));
                                for (i = 0; i < overlap; i++)
                                {
                                    float tmp_g = Q15ONE
                                          - MULT16_16_Q15(window[i], Q15ONE - ratio);
                                    buf[DECODE_BUFFER_SIZE - N + i] =
                                          MULT16_32_Q15(tmp_g, buf[DECODE_BUFFER_SIZE - N + i]);
                                }
                                for (i = overlap; i < extrapolation_len; i++)
                                {
                                    buf[DECODE_BUFFER_SIZE - N + i] =
                                          MULT16_32_Q15(ratio, buf[DECODE_BUFFER_SIZE - N + i]);
                                }
                            }
                        }

                    } while (++c < C);
                }

                st->prefilter_and_fold = 1;
            }

            /* Saturate to soemthing large to avoid wrap-around. */
            st->loss_duration = IMIN(10000, loss_duration + (1 << LM));
        }

        internal static unsafe int celt_decode_with_ec_dred(OpusCustomDecoder* st, in byte* data,
              int len, float* pcm, int frame_size, ec_ctx* dec, int accum
              )
        {
            int c, i, N;
            int spread_decision;
            int bits;
            ec_ctx _dec;
            float** decode_mem = stackalloc float*[2];
            float** out_syn = stackalloc float*[2];
            float* lpc;
            float* oldBandE, oldLogE, oldLogE2, backgroundLogE;

            int shortBlocks;
            int isTransient;
            int intra_ener;
            int CC = st->channels;
            int LM, M;
            int start;
            int end;
            int effEnd;
            int codedBands;
            int alloc_trim;
            int postfilter_pitch;
            float postfilter_gain;
            int intensity = 0;
            int dual_stereo = 0;
            int total_bits;
            int balance;
            int tell;
            int dynalloc_logp;
            int postfilter_tapset;
            int anti_collapse_rsv;
            int anti_collapse_on = 0;
            int silence;
            int C = st->stream_channels;
            OpusCustomMode* mode;
            int nbEBands;
            int overlap;
            short* eBands;
            float max_background_increase;
            validate_celt_decoder(st);
            mode = st->mode;
            nbEBands = mode->nbEBands;
            overlap = mode->overlap;
            eBands = mode->eBands;
            start = st->start;
            end = st->end;
            frame_size *= st->downsample;

            lpc = (float*)(st->_decode_mem + (DECODE_BUFFER_SIZE + overlap) * CC);
            oldBandE = lpc + CC * CELT_LPC_ORDER;
            oldLogE = oldBandE + 2 * nbEBands;
            oldLogE2 = oldLogE + 2 * nbEBands;
            backgroundLogE = oldLogE2 + 2 * nbEBands;

            {
                for (LM = 0; LM <= mode->maxLM; LM++)
                    if (mode->shortMdctSize << LM == frame_size)
                        break;
                if (LM > mode->maxLM)
                    return OPUS_BAD_ARG;
            }
            M = 1 << LM;

            if (len < 0 || len > 1275 || pcm == null)
                return OPUS_BAD_ARG;

            N = M * mode->shortMdctSize;
            c = 0; do
            {
                decode_mem[c] = st->_decode_mem + c * (DECODE_BUFFER_SIZE + overlap);
                out_syn[c] = decode_mem[c] + DECODE_BUFFER_SIZE - N;
            } while (++c < CC);

            effEnd = end;
            if (effEnd > mode->effEBands)
                effEnd = mode->effEBands;

            if (data == null || len <= 1)
            {
                celt_decode_lost(st, N, LM);
                deemphasis(out_syn, pcm, N, CC, st->downsample, mode->preemph, st->preemph_memD, accum);
                return frame_size / st->downsample;
            }

            /* Check if there are at least two packets received consecutively before
             * turning on the pitch-based PLC */
            if (st->loss_duration == 0) st->skip_plc = 0;

            if (dec == null)
            {
                ec_dec_init(&_dec, (byte*)data, (uint)len);
                dec = &_dec;
            }

            if (C == 1)
            {
                for (i = 0; i < nbEBands; i++)
                    oldBandE[i] = MAX16(oldBandE[i], oldBandE[nbEBands + i]);
            }

            total_bits = len * 8;
            tell = ec_tell(dec);

            if (tell >= total_bits)
                silence = 1;
            else if (tell == 1)
                silence = ec_dec_bit_logp(dec, 15);
            else
                silence = 0;
            if (silence != 0)
            {
                /* Pretend we've read all the remaining bits */
                tell = len * 8;
                dec->nbits_total += tell - ec_tell(dec);
            }

            postfilter_gain = 0;
            postfilter_pitch = 0;
            postfilter_tapset = 0;
            if (start == 0 && tell + 16 <= total_bits)
            {
                if (ec_dec_bit_logp(dec, 1) != 0)
                {
                    int qg, octave;
                    octave = (int)ec_dec_uint(dec, 6);
                    postfilter_pitch = (int)((16 << octave) + ec_dec_bits(dec, (uint)(4 + octave)) - 1);
                    qg = (int)ec_dec_bits(dec, 3);
                    if (ec_tell(dec) + 2 <= total_bits)
                        postfilter_tapset = ec_dec_icdf(dec, tapset_icdf, 2);
                    postfilter_gain = QCONST16(.09375f, 15) * (qg + 1);
                }
                tell = ec_tell(dec);
            }

            if (LM > 0 && tell + 3 <= total_bits)
            {
                isTransient = ec_dec_bit_logp(dec, 3);
                tell = ec_tell(dec);
            }
            else
                isTransient = 0;

            if (isTransient != 0)
                shortBlocks = M;
            else
                shortBlocks = 0;

            /* Decode the global flags (first symbols in the stream) */
            intra_ener = tell + 3 <= total_bits ? ec_dec_bit_logp(dec, 3) : 0;
            /* If recovering from packet loss, make sure we make the energy prediction safe to reduce the
               risk of getting loud artifacts. */
            if (intra_ener == 0 && st->loss_duration != 0)
            {
                c = 0; do
                {
                    float safety = 0;
                    int missing = IMIN(10, st->loss_duration >> LM);
                    if (LM == 0) safety = QCONST16(1.5f, DB_SHIFT);
                    else if (LM == 1) safety = QCONST16(.5f, DB_SHIFT);
                    for (i = start; i < end; i++)
                    {
                        if (oldBandE[c * nbEBands + i] < MAX16(oldLogE[c * nbEBands + i], oldLogE2[c * nbEBands + i]))
                        {
                            /* If energy is going down already, continue the trend. */
                            float slope;
                            float E0, E1, E2;
                            E0 = oldBandE[c * nbEBands + i];
                            E1 = oldLogE[c * nbEBands + i];
                            E2 = oldLogE2[c * nbEBands + i];
                            slope = MAX32(E1 - E0, HALF32(E2 - E0));
                            E0 -= MAX32(0, (1 + missing) * slope);
                            oldBandE[c * nbEBands + i] = MAX32(-QCONST16(20.0f, DB_SHIFT), E0);
                        }
                        else
                        {
                            /* Otherwise take the min of the last frames. */
                            oldBandE[c * nbEBands + i] = MIN16(MIN16(oldBandE[c * nbEBands + i], oldLogE[c * nbEBands + i]), oldLogE2[c * nbEBands + i]);
                        }
                        /* Shorter frames have more natural fluctuations -- play it safe. */
                        oldBandE[c * nbEBands + i] -= safety;
                    }
                } while (++c < 2);
            }

            /* Get band energies */
            unquant_coarse_energy(mode, start, end, oldBandE,
                  intra_ener, dec, C, LM);

            int[] tf_res_data = new int[nbEBands];
            int[] cap_data = new int[nbEBands];
            int[] offsets_data = new int[nbEBands];
            int[] fine_quant_data = new int[nbEBands];
            int[] pulses_data = new int[nbEBands];
            int[] fine_priority_data = new int[nbEBands];
            byte[] collapse_masks_data = new byte[C * nbEBands];
            float[] X_data = new float[C * N]; /**< Interleaved normalised MDCTs */

            fixed (int* tf_res = tf_res_data)
            fixed (int* cap = cap_data)
            fixed (int* offsets = offsets_data)
            fixed (int* fine_quant = fine_quant_data)
            fixed (int* pulses = pulses_data)
            fixed (int* fine_priority = fine_priority_data)
            fixed (byte* collapse_masks = collapse_masks_data)
            fixed (float* X = X_data)
            {
                tf_decode(start, end, isTransient, tf_res, LM, dec);

                tell = ec_tell(dec);
                spread_decision = SPREAD_NORMAL;
                if (tell + 4 <= total_bits)
                    spread_decision = ec_dec_icdf(dec, spread_icdf, 5);

                init_caps(mode, cap, LM, C);

                dynalloc_logp = 6;
                total_bits <<= BITRES;
                tell = (int)ec_tell_frac(dec);
                for (i = start; i < end; i++)
                {
                    int width, quanta;
                    int dynalloc_loop_logp;
                    int boost;
                    width = C * (eBands[i + 1] - eBands[i]) << LM;
                    /* quanta is 6 bits, but no more than 1 bit/sample
                        and no less than 1/8 bit/sample */
                    quanta = IMIN(width << BITRES, IMAX(6 << BITRES, width));
                    dynalloc_loop_logp = dynalloc_logp;
                    boost = 0;
                    while (tell + (dynalloc_loop_logp << BITRES) < total_bits && boost < cap[i])
                    {
                        int flag;
                        flag = ec_dec_bit_logp(dec, (uint)dynalloc_loop_logp);
                        tell = (int)ec_tell_frac(dec);
                        if (flag == 0)
                            break;
                        boost += quanta;
                        total_bits -= quanta;
                        dynalloc_loop_logp = 1;
                    }
                    offsets[i] = boost;
                    /* Making dynalloc more likely */
                    if (boost > 0)
                        dynalloc_logp = IMAX(2, dynalloc_logp - 1);
                }

                
                alloc_trim = tell + (6 << BITRES) <= total_bits ?
                        ec_dec_icdf(dec, trim_icdf, 7) : 5;

                bits = (((int)len * 8) << BITRES) - (int)ec_tell_frac(dec) - 1;
                anti_collapse_rsv = isTransient != 0 && LM >= 2 && bits >= ((LM + 2) << BITRES) ? (1 << BITRES) : 0;
                bits -= anti_collapse_rsv;


                codedBands = clt_compute_allocation(mode, start, end, offsets, cap,
                        alloc_trim, &intensity, &dual_stereo, bits, &balance, pulses,
                        fine_quant, fine_priority, C, LM, dec, 0, 0, 0);

                unquant_fine_energy(mode, start, end, oldBandE, fine_quant, dec, C);

                c = 0; do
                {
                    OPUS_MOVE(decode_mem[c], decode_mem[c] + N, DECODE_BUFFER_SIZE - N + overlap);
                } while (++c < CC);

                /* Decode fixed codebook */
                
                quant_all_bands(0, mode, start, end, X, C == 2 ? X + N : null, collapse_masks,
                        null, pulses, shortBlocks, spread_decision, dual_stereo, intensity, tf_res,
                        len * (8 << BITRES) - anti_collapse_rsv, balance, dec, LM, codedBands, &st->rng, 0,
                        st->disable_inv);

                if (anti_collapse_rsv > 0)
                {
                    anti_collapse_on = (int)ec_dec_bits(dec, 1);
                }

                unquant_energy_finalise(mode, start, end, oldBandE,
                        fine_quant, fine_priority, len * 8 - ec_tell(dec), dec, C);

                if (anti_collapse_on != 0)
                    anti_collapse(mode, X, collapse_masks, LM, C, N,
                            start, end, oldBandE, oldLogE, oldLogE2, pulses, st->rng);

                if (silence != 0)
                {
                    for (i = 0; i < C * nbEBands; i++)
                        oldBandE[i] = -QCONST16(28.0f, DB_SHIFT);
                }
                if (st->prefilter_and_fold != 0)
                {
                    prefilter_and_fold(st, N);
                }
                celt_synthesis(mode, X, out_syn, oldBandE, start, effEnd,
                                C, CC, isTransient, LM, st->downsample, silence);

                c = 0; do
                {
                    st->postfilter_period = IMAX(st->postfilter_period, COMBFILTER_MINPERIOD);
                    st->postfilter_period_old = IMAX(st->postfilter_period_old, COMBFILTER_MINPERIOD);
                    comb_filter(out_syn[c], out_syn[c], st->postfilter_period_old, st->postfilter_period, mode->shortMdctSize,
                            st->postfilter_gain_old, st->postfilter_gain, st->postfilter_tapset_old, st->postfilter_tapset,
                            mode->window, overlap);
                    if (LM != 0)
                        comb_filter(out_syn[c] + mode->shortMdctSize, out_syn[c] + mode->shortMdctSize, st->postfilter_period, postfilter_pitch, N - mode->shortMdctSize,
                                st->postfilter_gain, postfilter_gain, st->postfilter_tapset, postfilter_tapset,
                                mode->window, overlap);

                } while (++c < CC);
                st->postfilter_period_old = st->postfilter_period;
                st->postfilter_gain_old = st->postfilter_gain;
                st->postfilter_tapset_old = st->postfilter_tapset;
                st->postfilter_period = postfilter_pitch;
                st->postfilter_gain = postfilter_gain;
                st->postfilter_tapset = postfilter_tapset;
                if (LM != 0)
                {
                    st->postfilter_period_old = st->postfilter_period;
                    st->postfilter_gain_old = st->postfilter_gain;
                    st->postfilter_tapset_old = st->postfilter_tapset;
                }

                if (C == 1)
                    OPUS_COPY(&oldBandE[nbEBands], oldBandE, nbEBands);

                if (isTransient == 0)
                {
                    OPUS_COPY(oldLogE2, oldLogE, 2 * nbEBands);
                    OPUS_COPY(oldLogE, oldBandE, 2 * nbEBands);
                }
                else
                {
                    for (i = 0; i < 2 * nbEBands; i++)
                        oldLogE[i] = MIN16(oldLogE[i], oldBandE[i]);
                }
                /* In normal circumstances, we only allow the noise floor to increase by
                    up to 2.4 dB/second, but when we're in DTX we give the weight of
                    all missing packets to the update packet. */
                max_background_increase = IMIN(160, st->loss_duration + M) * QCONST16(0.001f, DB_SHIFT);
                for (i = 0; i < 2 * nbEBands; i++)
                    backgroundLogE[i] = MIN16(backgroundLogE[i] + max_background_increase, oldBandE[i]);
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
                } while (++c < 2);
                st->rng = dec->rng;

                deemphasis(out_syn, pcm, N, CC, st->downsample, mode->preemph, st->preemph_memD, accum);
                st->loss_duration = 0;
                st->prefilter_and_fold = 0;
                if (ec_tell(dec) > 8 * len)
                    return OPUS_INTERNAL_ERROR;
                if (ec_get_error(dec) != 0)
                    st->error = 1;
                return frame_size / st->downsample;
            }
        }

        internal static unsafe int celt_decode_with_ec(OpusCustomDecoder* st, in byte* data,
              int len, float* pcm, int frame_size, ec_ctx* dec, int accum)
        {
            return celt_decode_with_ec_dred(st, data, len, pcm, frame_size, dec, accum);
        }

        /// <summary>
        /// Overload with one int parameter (setters)
        /// </summary>
        internal static unsafe int opus_custom_decoder_ctl(OpusCustomDecoder* st, int request, int value)
        {
            switch (request)
            {
                case OPUS_SET_COMPLEXITY_REQUEST:
                    {
                        if (value < 0 || value > 10)
                        {
                            goto bad_arg;
                        }
                        st->complexity = value;
                    }
                    break;
                case CELT_SET_START_BAND_REQUEST:
                    {
                        if (value < 0 || value >= st->mode->nbEBands)
                            goto bad_arg;
                        st->start = value;
                    }
                    break;
                case CELT_SET_END_BAND_REQUEST:
                    {
                        if (value < 1 || value > st->mode->nbEBands)
                            goto bad_arg;
                        st->end = value;
                    }
                    break;
                case CELT_SET_CHANNELS_REQUEST:
                    {
                        if (value < 1 || value > 2)
                            goto bad_arg;
                        st->stream_channels = value;
                    }
                    break;
                case CELT_SET_SIGNALLING_REQUEST:
                    {
                        st->signalling = value;
                    }
                    break;
                case OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST:
                    {
                        if (value < 0 || value > 1)
                        {
                            goto bad_arg;
                        }
                        st->disable_inv = value;
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

        /// <summary>
        /// Overload with one int* parameter (getters)
        /// </summary>
        internal static unsafe int opus_custom_decoder_ctl(OpusCustomDecoder* st, int request, int* value)
        {
            switch (request)
            {
                case OPUS_GET_COMPLEXITY_REQUEST:
                    {
                        if (value == null)
                            goto bad_arg;
                        *value = st->complexity;
                    }
                    break;
                case CELT_GET_AND_CLEAR_ERROR_REQUEST:
                    {
                        if (value == null)
                            goto bad_arg;
                        *value = st->error;
                        st->error = 0;
                    }
                    break;
                case OPUS_GET_LOOKAHEAD_REQUEST:
                    {
                        if (value == null)
                            goto bad_arg;
                        *value = st->overlap / st->downsample;
                    }
                    break;
                case OPUS_GET_PITCH_REQUEST:
                    {
                        if (value == null)
                            goto bad_arg;
                        *value = st->postfilter_period;
                    }
                    break;
                case OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->disable_inv;
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

        /// <summary>
        /// Overload specifically to handle OPUS_GET_FINAL_RANGE_REQUEST
        /// </summary>
        internal static unsafe int opus_custom_decoder_ctl(OpusCustomDecoder* st, int request, uint* value)
        {
            switch (request)
            {
                case OPUS_GET_FINAL_RANGE_REQUEST:
                    {
                        if (value == null)
                            goto bad_arg;
                        *value = st->rng;
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

        /// <summary>
        /// Overload specifically to handle CELT_GET_MODE_REQUEST
        /// </summary>
        internal static unsafe int opus_custom_decoder_ctl(OpusCustomDecoder* st, int request, OpusCustomMode** value)
        {
            switch (request)
            {
                case CELT_GET_MODE_REQUEST:
                    {
                        if (value == null)
                            goto bad_arg;
                        *value = st->mode;
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

        /// <summary>
        /// Overload specifically to handle OPUS_RESET_STATE
        /// </summary>
        internal static unsafe int opus_custom_decoder_ctl(OpusCustomDecoder* st, int request)
        {
            switch (request)
            {
                case OPUS_RESET_STATE:
                    {
                        int i;
                        float* lpc, oldBandE, oldLogE, oldLogE2;
                        lpc = (float*)(st->_decode_mem + (DECODE_BUFFER_SIZE + st->overlap) * st->channels);
                        oldBandE = lpc + st->channels * CELT_LPC_ORDER;
                        oldLogE = oldBandE + 2 * st->mode->nbEBands;
                        oldLogE2 = oldLogE + 2 * st->mode->nbEBands;
                        OPUS_CLEAR((char*)&st->DECODER_RESET_START,
                              opus_custom_decoder_get_size(st->mode, st->channels) -
                              ((char*)&st->DECODER_RESET_START - (char*)st));
                        for (i = 0; i < 2 * st->mode->nbEBands; i++)
                            oldLogE[i] = oldLogE2[i] = -QCONST16(28.0f, DB_SHIFT);
                        st->skip_plc = 1;
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
    }
}
