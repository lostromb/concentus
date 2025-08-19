using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.CELTModeH;
using static HellaUnsafe.Celt.CeltH;
using static HellaUnsafe.Celt.Celt;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Celt.CeltLPC;
using static HellaUnsafe.Celt.Bands;
using static HellaUnsafe.Celt.MDCT;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.QuantBands;
using static HellaUnsafe.Celt.VQ;
using static HellaUnsafe.Celt.StaticModes;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Opus.OpusDefines;

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
            internal int arch;

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
    }
}
