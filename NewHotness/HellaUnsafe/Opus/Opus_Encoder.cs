using HellaUnsafe.Celt;
using HellaUnsafe.Common;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.CeltEncoderH;
using static HellaUnsafe.Celt.CeltH;
using static HellaUnsafe.Celt.CELTModeH;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.KissFFT;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Celt.MDCT;
using static HellaUnsafe.Celt.Bands;
using static HellaUnsafe.Celt.CWRS;
using static HellaUnsafe.Celt.Laplace;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.Analysis;
using static HellaUnsafe.Opus.MLP;
using static HellaUnsafe.Opus.MLPData;
using static HellaUnsafe.Opus.Opus;
using static HellaUnsafe.Opus.Repacketizer;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Opus.OpusPrivate;
using static HellaUnsafe.Silk.Control;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.EncAPI;
using static HellaUnsafe.Silk.Float.FloatCast;
using static HellaUnsafe.Silk.Float.SigProcFLP;
using static HellaUnsafe.Silk.Float.StructsFLP;
using static HellaUnsafe.Silk.Inlines;
using static HellaUnsafe.Silk.Log2Lin;
using static HellaUnsafe.Silk.Lin2Log;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.TuningParameters;

namespace HellaUnsafe.Opus
{
    internal static unsafe class Opus_Encoder
    {
        private const int MAX_ENCODER_BUFFER = 480;
        private const float PSEUDO_SNR_THRESHOLD = 316.23f;    /* 10^(25/10) */

        internal unsafe struct StereoWidthState
        {
            internal float XX, XY, YY;
            internal float smoothed_width;
            internal float max_follower;
        }

        internal unsafe struct OpusEncoder
        {
            internal int celt_enc_offset;
            internal int silk_enc_offset;
            internal silk_EncControlStruct silk_mode;
            internal int application;
            internal int channels;
            internal int delay_compensation;
            internal int force_channels;
            internal int signal_type;
            internal int user_bandwidth;
            internal int max_bandwidth;
            internal int user_forced_mode;
            internal int voice_ratio;
            internal int Fs;
            internal int use_vbr;
            internal int vbr_constraint;
            internal int variable_duration;
            internal int bitrate_bps;
            internal int user_bitrate_bps;
            internal int lsb_depth;
            internal int encoder_buffer;
            internal int lfe;
            internal int use_dtx;                 /* general DTX for both SILK and CELT */
            internal int fec_config;
            internal TonalityAnalysisState analysis;

            /// <summary>
            /// The number of bytes from the start of the encoder struct to clear from on reset
            /// </summary>
            // #define OPUS_ENCODER_RESET_START stream_channels
            internal int OPUS_ENCODER_RESET_START => (int)Unsafe.ByteOffset(ref celt_enc_offset, ref stream_channels);

            /* Everything beyond this point gets cleared on a reset */
            internal int stream_channels;
            internal short hybrid_stereo_width_Q14;
            internal int variable_HP_smth2_Q15;
            internal float prev_HB_gain;
            internal fixed float hp_mem[4];
            internal int mode;
            internal int prev_mode;
            internal int prev_channels;
            internal int prev_framesize;
            internal int bandwidth;
            /* Bandwidth determined automatically from the rate (before any other adjustment) */
            internal int auto_bandwidth;
            internal int silk_bw_switch;
            /* Sampling rate (at the API level) */
            internal int first;
            internal float* energy_masking;
            internal StereoWidthState width_mem;
            internal fixed float delay_buffer[MAX_ENCODER_BUFFER * 2];
            internal int detected_bandwidth;
            internal int nb_no_activity_ms_Q1;
            internal float peak_signal_energy;
            internal int nonfinal_frame; /* current frame is not the final in a packet */
            internal uint rangeFinal;
        };

        /* Transition tables for the voice and music. First column is the
           middle (memoriless) threshold. The second column is the hysteresis
           (difference with the middle) */
        internal static readonly int* mono_voice_bandwidth_thresholds = AllocateGlobalArray<int>(8, new int[] {
                 9000,  700, /* NB<->MB */
                 9000,  700, /* MB<->WB */
                13500, 1000, /* WB<->SWB */
                14000, 2000, /* SWB<->FB */
        });

        internal static readonly int* mono_music_bandwidth_thresholds = AllocateGlobalArray<int>(8, new int[] {
                 9000,  700, /* NB<->MB */
                 9000,  700, /* MB<->WB */
                11000, 1000, /* WB<->SWB */
                12000, 2000, /* SWB<->FB */
        });

        internal static readonly int* stereo_voice_bandwidth_thresholds = AllocateGlobalArray<int>(8, new int[] {
                 9000,  700, /* NB<->MB */
                 9000,  700, /* MB<->WB */
                13500, 1000, /* WB<->SWB */
                14000, 2000, /* SWB<->FB */
        });

        internal static readonly int* stereo_music_bandwidth_thresholds = AllocateGlobalArray<int>(8, new int[] {
                 9000,  700, /* NB<->MB */
                 9000,  700, /* MB<->WB */
                11000, 1000, /* WB<->SWB */
                12000, 2000, /* SWB<->FB */
        });

        /* Threshold bit-rates for switching between mono and stereo */
        internal const int stereo_voice_threshold = 19000;
        internal const int stereo_music_threshold = 17000;

        /* Threshold bit-rate for switching between SILK/hybrid and CELT-only */
        internal static readonly Native2DArray<int> mode_thresholds = new Native2DArray<int>(2, 2, new int[] {
              /* voice */ /* music */
                64000,      10000, /* mono */
                44000,      10000, /* stereo */
        });

        internal static readonly int* fec_thresholds = AllocateGlobalArray<int>(10, new int[] {
                12000, 1000, /* NB */
                14000, 1000, /* MB */
                16000, 1000, /* WB */
                20000, 1000, /* SWB */
                22000, 1000, /* FB */
        });

        internal static unsafe int opus_encoder_get_size(int channels)
        {
            int silkEncSizeBytes, celtEncSizeBytes;
            int ret;
            if (channels < 1 || channels > 2)
                return 0;
            ret = silk_Get_Encoder_Size(&silkEncSizeBytes);
            if (ret != 0)
                return 0;
            silkEncSizeBytes = align(silkEncSizeBytes);
            celtEncSizeBytes = celt_encoder_get_size(channels);
            return align(sizeof(OpusEncoder)) + silkEncSizeBytes + celtEncSizeBytes;
        }

        internal static unsafe int opus_encoder_init(OpusEncoder* st, int Fs, int channels, int application)
        {
            void* silk_enc;
            OpusCustomEncoder* celt_enc;
            int err;
            int ret, silkEncSizeBytes;

            if ((Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000) || (channels != 1 && channels != 2) ||
                 (application != OPUS_APPLICATION_VOIP && application != OPUS_APPLICATION_AUDIO
                 && application != OPUS_APPLICATION_RESTRICTED_LOWDELAY))
                return OPUS_BAD_ARG;

            OPUS_CLEAR((byte*)st, opus_encoder_get_size(channels));
            /* Create SILK encoder */
            ret = silk_Get_Encoder_Size(&silkEncSizeBytes);
            if (ret != 0)
                return OPUS_BAD_ARG;
            silkEncSizeBytes = align(silkEncSizeBytes);
            st->silk_enc_offset = align(sizeof(OpusEncoder));
            st->celt_enc_offset = st->silk_enc_offset + silkEncSizeBytes;
            silk_enc = (char*)st + st->silk_enc_offset;
            celt_enc = (OpusCustomEncoder*)((byte*)st + st->celt_enc_offset);

            st->stream_channels = st->channels = channels;

            st->Fs = Fs;

            ret = silk_InitEncoder(silk_enc, &st->silk_mode);
            if (ret != 0) return OPUS_INTERNAL_ERROR;

            /* default SILK parameters */
            st->silk_mode.nChannelsAPI = channels;
            st->silk_mode.nChannelsInternal = channels;
            st->silk_mode.API_sampleRate = st->Fs;
            st->silk_mode.maxInternalSampleRate = 16000;
            st->silk_mode.minInternalSampleRate = 8000;
            st->silk_mode.desiredInternalSampleRate = 16000;
            st->silk_mode.payloadSize_ms = 20;
            st->silk_mode.bitRate = 25000;
            st->silk_mode.packetLossPercentage = 0;
            st->silk_mode.complexity = 9;
            st->silk_mode.useInBandFEC = 0;
            st->silk_mode.useDRED = 0;
            st->silk_mode.useDTX = 0;
            st->silk_mode.useCBR = 0;
            st->silk_mode.reducedDependency = 0;

            /* Create CELT encoder */
            /* Initialize CELT encoder */
            err = celt_encoder_init(celt_enc, Fs, channels);
            if (err != OPUS_OK) return OPUS_INTERNAL_ERROR;

            opus_custom_encoder_ctl(celt_enc, CELT_SET_SIGNALLING_REQUEST, 0);
            opus_custom_encoder_ctl(celt_enc, OPUS_SET_COMPLEXITY_REQUEST, st->silk_mode.complexity);

            st->use_vbr = 1;
            /* Makes constrained VBR the default (safer for real-time use) */
            st->vbr_constraint = 1;
            st->user_bitrate_bps = OPUS_AUTO;
            st->bitrate_bps = 3000 + Fs * channels;
            st->application = application;
            st->signal_type = OPUS_AUTO;
            st->user_bandwidth = OPUS_AUTO;
            st->max_bandwidth = OPUS_BANDWIDTH_FULLBAND;
            st->force_channels = OPUS_AUTO;
            st->user_forced_mode = OPUS_AUTO;
            st->voice_ratio = -1;
            st->encoder_buffer = st->Fs / 100;
            st->lsb_depth = 24;
            st->variable_duration = OPUS_FRAMESIZE_ARG;

            /* Delay compensation of 4 ms (2.5 ms for SILK's extra look-ahead
               + 1.5 ms for SILK resamplers and stereo prediction) */
            st->delay_compensation = st->Fs / 250;

            st->hybrid_stereo_width_Q14 = 1 << 14;
            st->prev_HB_gain = Q15ONE;
            st->variable_HP_smth2_Q15 = silk_LSHIFT(silk_lin2log(VARIABLE_HP_MIN_CUTOFF_HZ), 8);
            st->first = 1;
            st->mode = MODE_HYBRID;
            st->bandwidth = OPUS_BANDWIDTH_FULLBAND;

            tonality_analysis_init(&st->analysis, st->Fs);
            st->analysis.application = st->application;

            return OPUS_OK;
        }

        internal static unsafe byte gen_toc(int mode, int framerate, int bandwidth, int channels)
        {
            int period;
            byte toc = 0;
            period = 0;
            while (framerate < 400)
            {
                framerate <<= 1;
                period++;
            }
            if (mode == MODE_SILK_ONLY)
            {
                toc = (byte)((bandwidth - OPUS_BANDWIDTH_NARROWBAND) << 5);
                toc |= (byte)((period - 2) << 3);
            }
            else if (mode == MODE_CELT_ONLY)
            {
                int tmp = bandwidth - OPUS_BANDWIDTH_MEDIUMBAND;
                if (tmp < 0)
                    tmp = 0;
                toc = 0x80;
                toc |= (byte)(tmp << 5);
                toc |= (byte)(period << 3);
            }
            else /* Hybrid */
            {
                toc = 0x60;
                toc |= (byte)((bandwidth - OPUS_BANDWIDTH_SUPERWIDEBAND) << 4);
                toc |= (byte)((period - 2) << 3);
            }
            toc |= (byte)(BOOL2INT(channels == 2) << 2);
            return toc;
        }

        internal static unsafe void silk_biquad_float(
            in float* input,            /* I:    Input signal                   */
            in int* B_Q28,         /* I:    MA coefficients [3]            */
            in int* A_Q28,         /* I:    AR coefficients [2]            */
            float* S,             /* I/O:  State vector [2]               */
            float* output,           /* O:    Output signal                  */
            in int len,            /* I:    Signal length (must be even)   */
            int stride
        )
        {
            /* DIRECT FORM II TRANSPOSED (uses 2 element state vector) */
            int k;
            float vout;
            float inval;
            float* A = stackalloc float[2];
            float* B = stackalloc float[3];

            A[0] = (float)(A_Q28[0] * (1.0f / ((int)1 << 28)));
            A[1] = (float)(A_Q28[1] * (1.0f / ((int)1 << 28)));
            B[0] = (float)(B_Q28[0] * (1.0f / ((int)1 << 28)));
            B[1] = (float)(B_Q28[1] * (1.0f / ((int)1 << 28)));
            B[2] = (float)(B_Q28[2] * (1.0f / ((int)1 << 28)));

            /* Negate A_Q28 values and split in two parts */

            for (k = 0; k < len; k++)
            {
                /* S[ 0 ], S[ 1 ]: Q12 */
                inval = input[k * stride];
                vout = S[0] + B[0] * inval;

                S[0] = S[1] - vout * A[0] + B[1] * inval;

                S[1] = -vout * A[1] + B[2] * inval + VERY_SMALL;

                /* Scale back to Q0 and saturate */
                output[k * stride] = vout;
            }
        }

        internal static unsafe void hp_cutoff(in float* input, int cutoff_Hz, float* output, float* hp_mem, int len, int channels, int Fs)
        {
            int* B_Q28 = stackalloc int[3];
            int* A_Q28 = stackalloc int[2];
            int Fc_Q19, r_Q28, r_Q22;

            silk_assert(cutoff_Hz <= silk_int32_MAX / SILK_FIX_CONST(1.5 * 3.14159 / 1000, 19));
            Fc_Q19 = silk_DIV32_16(silk_SMULBB(SILK_FIX_CONST(1.5 * 3.14159 / 1000, 19), cutoff_Hz), Fs / 1000);
            silk_assert(Fc_Q19 > 0 && Fc_Q19 < 32768);

            r_Q28 = SILK_FIX_CONST(1.0, 28) - silk_MUL(SILK_FIX_CONST(0.92, 9), Fc_Q19);

            /* b = r * [ 1; -2; 1 ]; */
            /* a = [ 1; -2 * r * ( 1 - 0.5 * Fc^2 ); r^2 ]; */
            B_Q28[0] = r_Q28;
            B_Q28[1] = silk_LSHIFT(-r_Q28, 1);
            B_Q28[2] = r_Q28;

            /* -r * ( 2 - Fc * Fc ); */
            r_Q22 = silk_RSHIFT(r_Q28, 6);
            A_Q28[0] = silk_SMULWW(r_Q22, silk_SMULWW(Fc_Q19, Fc_Q19) - SILK_FIX_CONST(2.0, 22));
            A_Q28[1] = silk_SMULWW(r_Q22, r_Q22);

            silk_biquad_float(input, B_Q28, A_Q28, hp_mem, output, len, channels);
            if (channels == 2)
            {
                silk_biquad_float(input + 1, B_Q28, A_Q28, hp_mem + 2, output + 1, len, channels);
            }
        }

        internal static unsafe void dc_reject(in float* input, int cutoff_Hz, float* output, float* hp_mem, int len, int channels, int Fs)
        {
            int i;
            float coef, coef2;
            coef = 6.3f * cutoff_Hz / Fs;
            coef2 = 1 - coef;
            if (channels == 2)
            {
                float m0, m2;
                m0 = hp_mem[0];
                m2 = hp_mem[2];
                for (i = 0; i < len; i++)
                {
                    float x0, x1, out0, out1;
                    x0 = input[2 * i + 0];
                    x1 = input[2 * i + 1];
                    out0 = x0 - m0;
                    out1 = x1 - m2;
                    m0 = coef * x0 + VERY_SMALL + coef2 * m0;
                    m2 = coef * x1 + VERY_SMALL + coef2 * m2;
                    output[2 * i + 0] = out0;
                    output[2 * i + 1] = out1;
                }
                hp_mem[0] = m0;
                hp_mem[2] = m2;
            }
            else
            {
                float m0;
                m0 = hp_mem[0];
                for (i = 0; i < len; i++)
                {
                    float x, y;
                    x = input[i];
                    y = x - m0;
                    m0 = coef * x + VERY_SMALL + coef2 * m0;
                    output[i] = y;
                }
                hp_mem[0] = m0;
            }
        }

        internal static unsafe void stereo_fade(in float* input, float* output, float g1, float g2,
                int overlap48, int frame_size, int channels, in float* window, int Fs)
        {
            int i;
            int overlap;
            int inc;
            inc = 48000 / Fs;
            overlap = overlap48 / inc;
            g1 = Q15ONE - g1;
            g2 = Q15ONE - g2;
            for (i = 0; i < overlap; i++)
            {
                float diff;
                float g, w;
                w = MULT16_16_Q15(window[i * inc], window[i * inc]);
                g = SHR32(MAC16_16(MULT16_16(w, g2),
                      Q15ONE - w, g1), 15);
                diff = EXTRACT16(HALF32((float)input[i * channels] - (float)input[i * channels + 1]));
                diff = MULT16_16_Q15(g, diff);
                output[i * channels] = output[i * channels] - diff;
                output[i * channels + 1] = output[i * channels + 1] + diff;
            }
            for (; i < frame_size; i++)
            {
                float diff;
                diff = EXTRACT16(HALF32((float)input[i * channels] - (float)input[i * channels + 1]));
                diff = MULT16_16_Q15(g2, diff);
                output[i * channels] = output[i * channels] - diff;
                output[i * channels + 1] = output[i * channels + 1] + diff;
            }
        }

        internal static unsafe void gain_fade(in float* input, float* output, float g1, float g2,
                int overlap48, int frame_size, int channels, in float* window, int Fs)
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
                    float g, w;
                    w = MULT16_16_Q15(window[i * inc], window[i * inc]);
                    g = SHR32(MAC16_16(MULT16_16(w, g2),
                          Q15ONE - w, g1), 15);
                    output[i] = MULT16_16_Q15(g, input[i]);
                }
            }
            else
            {
                for (i = 0; i < overlap; i++)
                {
                    float g, w;
                    w = MULT16_16_Q15(window[i * inc], window[i * inc]);
                    g = SHR32(MAC16_16(MULT16_16(w, g2),
                          Q15ONE - w, g1), 15);
                    output[i * 2] = MULT16_16_Q15(g, input[i * 2]);
                    output[i * 2 + 1] = MULT16_16_Q15(g, input[i * 2 + 1]);
                }
            }
            c = 0; do
            {
                for (i = overlap; i < frame_size; i++)
                {
                    output[i * channels + c] = MULT16_16_Q15(g2, input[i * channels + c]);
                }
            }
            while (++c < channels);
        }

        internal static unsafe OpusEncoder* opus_encoder_create(int Fs, int channels, int application, int* error)
        {
            int ret;
            OpusEncoder* st;
            if ((Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000) || (channels != 1 && channels != 2) ||
                (application != OPUS_APPLICATION_VOIP && application != OPUS_APPLICATION_AUDIO
                && application != OPUS_APPLICATION_RESTRICTED_LOWDELAY))
            {
                if (error != null)
                    *error = OPUS_BAD_ARG;
                return null;
            }
            st = (OpusEncoder*)opus_alloc(opus_encoder_get_size(channels));
            if (st == null)
            {
                if (error != null)
                    *error = OPUS_ALLOC_FAIL;
                return null;
            }
            ret = opus_encoder_init(st, Fs, channels, application);
            if (error != null)
                *error = ret;
            if (ret != OPUS_OK)
            {
                opus_free(st);
                st = null;
            }
            return st;
        }

        internal static unsafe int user_bitrate_to_bitrate(OpusEncoder* st, int frame_size, int max_data_bytes)
        {
            if (frame_size == 0) frame_size = st->Fs / 400;
            if (st->user_bitrate_bps == OPUS_AUTO)
                return 60 * st->Fs / frame_size + st->Fs * st->channels;
            else if (st->user_bitrate_bps == OPUS_BITRATE_MAX)
                return max_data_bytes * 8 * st->Fs / frame_size;
            else
                return st->user_bitrate_bps;
        }

        private static float PCM2VAL(float x)
        {
            return SCALEIN(x);
        }

        internal static unsafe void downmix_float(in void* _x, float* y, int subframe, int offset, int c1, int c2, int C)
        {
            float* x;
            int j;

            x = (float*)_x;
            for (j = 0; j < subframe; j++)
                y[j] = PCM2VAL(x[(j + offset) * C + c1]);
            if (c2 > -1)
            {
                for (j = 0; j < subframe; j++)
                    y[j] += PCM2VAL(x[(j + offset) * C + c2]);
            }
            else if (c2 == -2)
            {
                int c;
                for (c = 1; c < C; c++)
                {
                    for (j = 0; j < subframe; j++)
                        y[j] += PCM2VAL(x[(j + offset) * C + c]);
                }
            }
        }

        internal static unsafe void downmix_int(in void* _x, float* y, int subframe, int offset, int c1, int c2, int C)
        {
            short* x;
            int j;

            x = (short*)_x;
            for (j = 0; j < subframe; j++)
                y[j] = x[(j + offset) * C + c1];
            if (c2 > -1)
            {
                for (j = 0; j < subframe; j++)
                    y[j] += x[(j + offset) * C + c2];
            }
            else if (c2 == -2)
            {
                int c;
                for (c = 1; c < C; c++)
                {
                    for (j = 0; j < subframe; j++)
                        y[j] += x[(j + offset) * C + c];
                }
            }
        }

        internal static unsafe int frame_size_select(int frame_size, int variable_duration, int Fs)
        {
            int new_size;
            if (frame_size < Fs / 400)
                return -1;
            if (variable_duration == OPUS_FRAMESIZE_ARG)
                new_size = frame_size;
            else if (variable_duration >= OPUS_FRAMESIZE_2_5_MS && variable_duration <= OPUS_FRAMESIZE_120_MS)
            {
                if (variable_duration <= OPUS_FRAMESIZE_40_MS)
                    new_size = (Fs / 400) << (variable_duration - OPUS_FRAMESIZE_2_5_MS);
                else
                    new_size = (variable_duration - OPUS_FRAMESIZE_2_5_MS - 2) * Fs / 50;
            }
            else
                return -1;
            if (new_size > frame_size)
                return -1;
            if (400 * new_size != Fs && 200 * new_size != Fs && 100 * new_size != Fs &&
                 50 * new_size != Fs && 25 * new_size != Fs && 50 * new_size != 3 * Fs &&
                 50 * new_size != 4 * Fs && 50 * new_size != 5 * Fs && 50 * new_size != 6 * Fs)
                return -1;
            return new_size;
        }

        internal static unsafe float compute_stereo_width(in float* pcm, int frame_size, int Fs, StereoWidthState* mem)
        {
            float xx, xy, yy;
            float sqrt_xx, sqrt_yy;
            float qrrt_xx, qrrt_yy;
            int frame_rate;
            int i;
            float short_alpha;

            frame_rate = Fs / frame_size;
            short_alpha = Q15ONE - MULT16_16(25, Q15ONE) / IMAX(50, frame_rate);
            xx = xy = yy = 0;
            /* Unroll by 4. The frame size is always a multiple of 4 *except* for
               2.5 ms frames at 12 kHz. Since this setting is very rare (and very
               stupid), we just discard the last two samples. */
            for (i = 0; i < frame_size - 3; i += 4)
            {
                float pxx = 0;
                float pxy = 0;
                float pyy = 0;
                float x, y;
                x = pcm[2 * i];
                y = pcm[2 * i + 1];
                pxx = SHR32(MULT16_16(x, x), 2);
                pxy = SHR32(MULT16_16(x, y), 2);
                pyy = SHR32(MULT16_16(y, y), 2);
                x = pcm[2 * i + 2];
                y = pcm[2 * i + 3];
                pxx += SHR32(MULT16_16(x, x), 2);
                pxy += SHR32(MULT16_16(x, y), 2);
                pyy += SHR32(MULT16_16(y, y), 2);
                x = pcm[2 * i + 4];
                y = pcm[2 * i + 5];
                pxx += SHR32(MULT16_16(x, x), 2);
                pxy += SHR32(MULT16_16(x, y), 2);
                pyy += SHR32(MULT16_16(y, y), 2);
                x = pcm[2 * i + 6];
                y = pcm[2 * i + 7];
                pxx += SHR32(MULT16_16(x, x), 2);
                pxy += SHR32(MULT16_16(x, y), 2);
                pyy += SHR32(MULT16_16(y, y), 2);

                xx += SHR32(pxx, 10);
                xy += SHR32(pxy, 10);
                yy += SHR32(pyy, 10);
            }
            if (!(xx < 1e9f) || celt_isnan(xx) != 0 || !(yy < 1e9f) || celt_isnan(yy) != 0)
            {
                xy = xx = yy = 0;
            }
            mem->XX += MULT16_32_Q15(short_alpha, xx - mem->XX);
            mem->XY += MULT16_32_Q15(short_alpha, xy - mem->XY);
            mem->YY += MULT16_32_Q15(short_alpha, yy - mem->YY);
            mem->XX = MAX32(0, mem->XX);
            mem->XY = MAX32(0, mem->XY);
            mem->YY = MAX32(0, mem->YY);
            if (MAX32(mem->XX, mem->YY) > QCONST16(8e-4f, 18))
            {
                float corr;
                float ldiff;
                float width;
                sqrt_xx = celt_sqrt(mem->XX);
                sqrt_yy = celt_sqrt(mem->YY);
                qrrt_xx = celt_sqrt(sqrt_xx);
                qrrt_yy = celt_sqrt(sqrt_yy);
                /* Inter-channel correlation */
                mem->XY = MIN32(mem->XY, sqrt_xx * sqrt_yy);
                corr = SHR32(frac_div32(mem->XY, EPSILON + MULT16_16(sqrt_xx, sqrt_yy)), 16);
                /* Approximate loudness difference */
                ldiff = MULT16_16(Q15ONE, ABS16(qrrt_xx - qrrt_yy)) / (EPSILON + qrrt_xx + qrrt_yy);
                width = MULT16_16_Q15(celt_sqrt(QCONST32(1.0f, 30) - MULT16_16(corr, corr)), ldiff);
                /* Smoothing over one second */
                mem->smoothed_width += (width - mem->smoothed_width) / frame_rate;
                /* Peak follower */
                mem->max_follower = MAX16(mem->max_follower - QCONST16(.02f, 15) / frame_rate, mem->smoothed_width);
            }
            /*printf("%f %f %f %f %f ", corr/(float)Q15ONE, ldiff/(float)Q15ONE, width/(float)Q15ONE, mem->smoothed_width/(float)Q15ONE, mem->max_follower/(float)Q15ONE);*/
            return EXTRACT16(MIN32(Q15ONE, MULT16_16(20, mem->max_follower)));
        }

        internal static unsafe int decide_fec(int useInBandFEC, int PacketLoss_perc, int last_fec, int mode, int* bandwidth, int rate)
        {
            int orig_bandwidth;
            if (useInBandFEC == 0 || PacketLoss_perc == 0 || mode == MODE_CELT_ONLY)
                return 0;
            orig_bandwidth = *bandwidth;
            for (; ; )
            {
                int hysteresis;
                int LBRR_rate_thres_bps;
                /* Compute threshold for using FEC at the current bandwidth setting */
                LBRR_rate_thres_bps = fec_thresholds[2 * (*bandwidth - OPUS_BANDWIDTH_NARROWBAND)];
                hysteresis = fec_thresholds[2 * (*bandwidth - OPUS_BANDWIDTH_NARROWBAND) + 1];
                if (last_fec == 1) LBRR_rate_thres_bps -= hysteresis;
                if (last_fec == 0) LBRR_rate_thres_bps += hysteresis;
                LBRR_rate_thres_bps = silk_SMULWB(silk_MUL(LBRR_rate_thres_bps,
                      125 - silk_min(PacketLoss_perc, 25)), SILK_FIX_CONST(0.01, 16));
                /* If loss <= 5%, we look at whether we have enough rate to enable FEC.
                   If loss > 5%, we decrease the bandwidth until we can enable FEC. */
                if (rate > LBRR_rate_thres_bps)
                    return 1;
                else if (PacketLoss_perc <= 5)
                    return 0;
                else if (*bandwidth > OPUS_BANDWIDTH_NARROWBAND)
                    (*bandwidth)--;
                else
                    break;
            }
            /* Couldn't find any bandwidth to enable FEC, keep original bandwidth. */
            *bandwidth = orig_bandwidth;
            return 0;
        }

        internal static readonly Native2DArray<int> rate_table = new Native2DArray<int>(7, 5, new int[] {
            /*  |total| |-------- SILK------------|
                        |-- No FEC -| |--- FEC ---|
                         10ms   20ms   10ms   20ms */
                 0,     0,     0,     0,     0,
               12000, 10000, 10000, 11000, 11000,
               16000, 13500, 13500, 15000, 15000,
               20000, 16000, 16000, 18000, 18000,
               24000, 18000, 18000, 21000, 21000,
               32000, 22000, 22000, 28000, 28000,
               64000, 38000, 38000, 50000, 50000
        });

        internal static unsafe int compute_silk_rate_for_hybrid(int rate, int bandwidth, int frame20ms, int vbr, int fec, int channels)
        {
            int entry;
            int i;
            int N;
            int silk_rate;

            /* Do the allocation per-channel. */
            rate /= channels;
            entry = 1 + frame20ms + 2 * fec;
            N = rate_table.size_of / sizeof(int);
            for (i = 1; i < N; i++)
            {
                if (rate_table[i][0] > rate) break;
            }
            if (i == N)
            {
                silk_rate = rate_table[i - 1][entry];
                /* For now, just give 50% of the extra bits to SILK. */
                silk_rate += (rate - rate_table[i - 1][0]) / 2;
            }
            else
            {
                int lo, hi, x0, x1;
                lo = rate_table[i - 1][entry];
                hi = rate_table[i][entry];
                x0 = rate_table[i - 1][0];
                x1 = rate_table[i][0];
                silk_rate = (lo * (x1 - rate) + hi * (rate - x0)) / (x1 - x0);
            }
            if (vbr == 0)
            {
                /* Tiny boost to SILK for CBR. We should probably tune this better. */
                silk_rate += 100;
            }
            if (bandwidth == OPUS_BANDWIDTH_SUPERWIDEBAND)
                silk_rate += 300;
            silk_rate *= channels;
            /* Small adjustment for stereo (calibrated for 32 kb/s, haven't tried other bitrates). */
            if (channels == 2 && rate >= 12000)
                silk_rate -= 1000;
            return silk_rate;
        }

        /* Returns the equivalent bitrate corresponding to 20 ms frames,
           complexity 10 VBR operation. */
        internal static unsafe int compute_equiv_rate(int bitrate, int channels,
            int frame_rate, int vbr, int mode, int complexity, int loss)
        {
            int equiv;
            equiv = bitrate;
            /* Take into account overhead from smaller frames. */
            if (frame_rate > 50)
                equiv -= (40 * channels + 20) * (frame_rate - 50);
            /* CBR is about a 8% penalty for both SILK and CELT. */
            if (vbr == 0)
                equiv -= equiv / 12;
            /* Complexity makes about 10% difference (from 0 to 10) in general. */
            equiv = equiv * (90 + complexity) / 100;
            if (mode == MODE_SILK_ONLY || mode == MODE_HYBRID)
            {
                /* SILK complexity 0-1 uses the non-delayed-decision NSQ, which
                   costs about 20%. */
                if (complexity < 2)
                    equiv = equiv * 4 / 5;
                equiv -= equiv * loss / (6 * loss + 10);
            }
            else if (mode == MODE_CELT_ONLY)
            {
                /* CELT complexity 0-4 doesn't have the pitch filter, which costs
                   about 10%. */
                if (complexity < 5)
                    equiv = equiv * 9 / 10;
            }
            else
            {
                /* Mode not known yet */
                /* Half the SILK loss*/
                equiv -= equiv * loss / (12 * loss + 20);
            }
            return equiv;
        }

        internal static unsafe int is_digital_silence(in float* pcm, int frame_size, int channels, int lsb_depth)
        {
            int silence = 0;
            float sample_max = 0;
            sample_max = celt_maxabs16(pcm, frame_size * channels);
            silence = BOOL2INT(sample_max <= (float)1 / (1 << lsb_depth));
            return silence;
        }

        internal static unsafe float compute_frame_energy(in float* pcm, int frame_size, int channels)
        {
            int len = frame_size * channels;
            return celt_inner_prod(pcm, pcm, len) / len;
        }

        internal static unsafe int decide_dtx_mode(int activity,            /* indicates if this frame contains speech/music */
                           int* nb_no_activity_ms_Q1,    /* number of consecutive milliseconds with no activity, in Q1 */
                           int frame_size_ms_Q1          /* number of miliseconds in this update, in Q1 */
                           )
        {
            if (activity == 0)
            {
                /* The number of consecutive DTX frames should be within the allowed bounds.
                   Note that the allowed bound is defined in the SILK headers and assumes 20 ms
                   frames. As this function can be called with any frame length, a conversion to
                   milliseconds is done before the comparisons. */
                (*nb_no_activity_ms_Q1) += frame_size_ms_Q1;
                if (*nb_no_activity_ms_Q1 > NB_SPEECH_FRAMES_BEFORE_DTX * 20 * 2)
                {
                    if (*nb_no_activity_ms_Q1 <= (NB_SPEECH_FRAMES_BEFORE_DTX + MAX_CONSECUTIVE_DTX) * 20 * 2)
                        /* Valid frame for DTX! */
                        return 1;
                    else
                        (*nb_no_activity_ms_Q1) = NB_SPEECH_FRAMES_BEFORE_DTX * 20 * 2;
                }
            }
            else
                (*nb_no_activity_ms_Q1) = 0;

            return 0;
        }

        internal static unsafe int compute_redundancy_bytes(int max_data_bytes, int bitrate_bps, int frame_rate, int channels)
        {
            int redundancy_bytes_cap;
            int redundancy_bytes;
            int redundancy_rate;
            int base_bits;
            int available_bits;
            base_bits = (40 * channels + 20);

            /* Equivalent rate for 5 ms frames. */
            redundancy_rate = bitrate_bps + base_bits * (200 - frame_rate);
            /* For VBR, further increase the bitrate if we can afford it. It's pretty short
               and we'll avoid artefacts. */
            redundancy_rate = 3 * redundancy_rate / 2;
            redundancy_bytes = redundancy_rate / 1600;

            /* Compute the max rate we can use given CBR or VBR with cap. */
            available_bits = max_data_bytes * 8 - 2 * base_bits;
            redundancy_bytes_cap = (available_bits * 240 / (240 + 48000 / frame_rate) + base_bits) / 8;
            redundancy_bytes = IMIN(redundancy_bytes, redundancy_bytes_cap);
            /* It we can't get enough bits for redundancy to be worth it, rely on the decoder PLC. */
            if (redundancy_bytes > 4 + 8 * channels)
                redundancy_bytes = IMIN(257, redundancy_bytes);
            else
                redundancy_bytes = 0;
            return redundancy_bytes;
        }

        internal static unsafe int opus_encode_native(OpusEncoder* st, in float* pcm, int frame_size,
                byte* data, int out_data_bytes, int lsb_depth,
                in void* analysis_pcm, int analysis_size, int c1, int c2,
                int analysis_channels, downmix_func downmix, int float_api)
        {
            void* silk_enc;
            OpusCustomEncoder* celt_enc;
            int i;
            int ret = 0;
            int prefill = 0;
            int redundancy = 0;
            int celt_to_silk = 0;
            int to_celt = 0;
            int voice_est; /* Probability of voice in Q7 */
            int equiv_rate;
            int frame_rate;
            int max_rate; /* Max bitrate we're allowed to use */
            int curr_bandwidth;
            int max_data_bytes; /* Max number of bytes we're allowed to use */
            int cbr_bytes = -1;
            float stereo_width;
            OpusCustomMode* celt_mode;
            AnalysisInfo analysis_info = default;
            int analysis_read_pos_bak = -1;
            int analysis_read_subframe_bak = -1;
            int is_silence = 0;

            max_data_bytes = IMIN(1276, out_data_bytes);

            st->rangeFinal = 0;
            if (frame_size <= 0 || max_data_bytes <= 0)
            {
                return OPUS_BAD_ARG;
            }

            /* Cannot encode 100 ms in 1 byte */
            if (max_data_bytes == 1 && st->Fs == (frame_size * 10))
            {
                return OPUS_BUFFER_TOO_SMALL;
            }

            silk_enc = (char*)st + st->silk_enc_offset;
            celt_enc = (OpusCustomEncoder*)((char*)st + st->celt_enc_offset);

            lsb_depth = IMIN(lsb_depth, st->lsb_depth);

            opus_custom_encoder_ctl(celt_enc, CELT_GET_MODE_REQUEST, &celt_mode);
            analysis_info.valid = 0;
            if (st->silk_mode.complexity >= 7 && st->Fs >= 16000)
            {
                is_silence = is_digital_silence(pcm, frame_size, st->channels, lsb_depth);
                analysis_read_pos_bak = st->analysis.read_pos;
                analysis_read_subframe_bak = st->analysis.read_subframe;
                run_analysis(&st->analysis, celt_mode, analysis_pcm, analysis_size, frame_size,
                      c1, c2, analysis_channels, st->Fs,
                      lsb_depth, downmix, &analysis_info);

                /* Track the peak signal energy */
                if (is_silence == 0 && analysis_info.activity_probability > DTX_ACTIVITY_THRESHOLD)
                    st->peak_signal_energy = MAX32(MULT16_32_Q15(QCONST16(0.999f, 15), st->peak_signal_energy),
                          compute_frame_energy(pcm, frame_size, st->channels));
            }
            else if (st->analysis.initialized != 0)
            {
                tonality_analysis_reset(&st->analysis);
            }

            /* Reset voice_ratio if this frame is not silent or if analysis is disabled.
             * Otherwise, preserve voice_ratio from the last non-silent frame */
            if (is_silence == 0)
                st->voice_ratio = -1;

            st->detected_bandwidth = 0;
            if (analysis_info.valid != 0)
            {
                int analysis_bandwidth;
                if (st->signal_type == OPUS_AUTO)
                {
                    float prob;
                    if (st->prev_mode == 0)
                        prob = analysis_info.music_prob;
                    else if (st->prev_mode == MODE_CELT_ONLY)
                        prob = analysis_info.music_prob_max;
                    else
                        prob = analysis_info.music_prob_min;
                    st->voice_ratio = (int)floor(.5f + 100 * (1 - prob));
                }

                analysis_bandwidth = analysis_info.bandwidth;
                if (analysis_bandwidth <= 12)
                    st->detected_bandwidth = OPUS_BANDWIDTH_NARROWBAND;
                else if (analysis_bandwidth <= 14)
                    st->detected_bandwidth = OPUS_BANDWIDTH_MEDIUMBAND;
                else if (analysis_bandwidth <= 16)
                    st->detected_bandwidth = OPUS_BANDWIDTH_WIDEBAND;
                else if (analysis_bandwidth <= 18)
                    st->detected_bandwidth = OPUS_BANDWIDTH_SUPERWIDEBAND;
                else
                    st->detected_bandwidth = OPUS_BANDWIDTH_FULLBAND;
            }

            if (st->channels == 2 && st->force_channels != 1)
                stereo_width = compute_stereo_width(pcm, frame_size, st->Fs, &st->width_mem);
            else
                stereo_width = 0;
            st->bitrate_bps = user_bitrate_to_bitrate(st, frame_size, max_data_bytes);

            frame_rate = st->Fs / frame_size;
            if (st->use_vbr == 0)
            {
                /* Multiply by 12 to make sure the division is exact. */
                int frame_rate12 = 12 * st->Fs / frame_size;
                /* We need to make sure that "int" values always fit in 16 bits. */
                cbr_bytes = IMIN((12 * st->bitrate_bps / 8 + frame_rate12 / 2) / frame_rate12, max_data_bytes);
                st->bitrate_bps = cbr_bytes * (int)frame_rate12 * 8 / 12;
                /* Make sure we provide at least one byte to avoid failing. */
                max_data_bytes = IMAX(1, cbr_bytes);
            }
            if (max_data_bytes < 3 || st->bitrate_bps < 3 * frame_rate * 8
               || (frame_rate < 50 && (max_data_bytes * frame_rate < 300 || st->bitrate_bps < 2400)))
            {
                /*If the space is too low to do something useful, emit 'PLC' frames.*/
                int tocmode = st->mode;
                int bw = st->bandwidth == 0 ? OPUS_BANDWIDTH_NARROWBAND : st->bandwidth;
                int packet_code = 0;
                int num_multiframes = 0;

                if (tocmode == 0)
                    tocmode = MODE_SILK_ONLY;
                if (frame_rate > 100)
                    tocmode = MODE_CELT_ONLY;
                /* 40 ms -> 2 x 20 ms if in CELT_ONLY or HYBRID mode */
                if (frame_rate == 25 && tocmode != MODE_SILK_ONLY)
                {
                    frame_rate = 50;
                    packet_code = 1;
                }

                /* >= 60 ms frames */
                if (frame_rate <= 16)
                {
                    /* 1 x 60 ms, 2 x 40 ms, 2 x 60 ms */
                    if (out_data_bytes == 1 || (tocmode == MODE_SILK_ONLY && frame_rate != 10))
                    {
                        tocmode = MODE_SILK_ONLY;

                        packet_code = BOOL2INT(frame_rate <= 12);
                        frame_rate = frame_rate == 12 ? 25 : 16;
                    }
                    else
                    {
                        num_multiframes = 50 / frame_rate;
                        frame_rate = 50;
                        packet_code = 3;
                    }
                }

                if (tocmode == MODE_SILK_ONLY && bw > OPUS_BANDWIDTH_WIDEBAND)
                    bw = OPUS_BANDWIDTH_WIDEBAND;
                else if (tocmode == MODE_CELT_ONLY && bw == OPUS_BANDWIDTH_MEDIUMBAND)
                    bw = OPUS_BANDWIDTH_NARROWBAND;
                else if (tocmode == MODE_HYBRID && bw <= OPUS_BANDWIDTH_SUPERWIDEBAND)
                    bw = OPUS_BANDWIDTH_SUPERWIDEBAND;

                data[0] = gen_toc(tocmode, frame_rate, bw, st->stream_channels);
                data[0] |= (byte)packet_code;

                ret = packet_code <= 1 ? 1 : 2;

                max_data_bytes = IMAX(max_data_bytes, ret);

                if (packet_code == 3)
                    data[1] = (byte)num_multiframes;

                if (st->use_vbr == 0)
                {
                    ret = opus_packet_pad(data, ret, max_data_bytes);
                    if (ret == OPUS_OK)
                        ret = max_data_bytes;
                    else
                        ret = OPUS_INTERNAL_ERROR;
                }

                return ret;
            }
            max_rate = frame_rate * max_data_bytes * 8;

            /* Equivalent 20-ms rate for mode/channel/bandwidth decisions */
            equiv_rate = compute_equiv_rate(st->bitrate_bps, st->channels, st->Fs / frame_size,
                  st->use_vbr, 0, st->silk_mode.complexity, st->silk_mode.packetLossPercentage);

            if (st->signal_type == OPUS_SIGNAL_VOICE)
                voice_est = 127;
            else if (st->signal_type == OPUS_SIGNAL_MUSIC)
                voice_est = 0;
            else if (st->voice_ratio >= 0)
            {
                voice_est = st->voice_ratio * 327 >> 8;
                /* For AUDIO, never be more than 90% confident of having speech */
                if (st->application == OPUS_APPLICATION_AUDIO)
                    voice_est = IMIN(voice_est, 115);
            }
            else if (st->application == OPUS_APPLICATION_VOIP)
                voice_est = 115;
            else
                voice_est = 48;

            if (st->force_channels != OPUS_AUTO && st->channels == 2)
            {
                st->stream_channels = st->force_channels;
            }
            else
            {
                /* Rate-dependent mono-stereo decision */
                if (st->channels == 2)
                {
                    int stereo_threshold;
                    stereo_threshold = stereo_music_threshold + ((voice_est * voice_est * (stereo_voice_threshold - stereo_music_threshold)) >> 14);
                    if (st->stream_channels == 2)
                        stereo_threshold -= 1000;
                    else
                        stereo_threshold += 1000;
                    st->stream_channels = (equiv_rate > stereo_threshold) ? 2 : 1;
                }
                else
                {
                    st->stream_channels = st->channels;
                }
            }
            /* Update equivalent rate for channels decision. */
            equiv_rate = compute_equiv_rate(st->bitrate_bps, st->stream_channels, st->Fs / frame_size,
                  st->use_vbr, 0, st->silk_mode.complexity, st->silk_mode.packetLossPercentage);

            /* Allow SILK DTX if DTX is enabled but the generalized DTX cannot be used,
               e.g. because of the complexity setting or sample rate. */
            st->silk_mode.useDTX = BOOL2INT(st->use_dtx != 0 && !(analysis_info.valid != 0 || is_silence != 0));

            /* Mode selection depending on application and signal type */
            if (st->application == OPUS_APPLICATION_RESTRICTED_LOWDELAY)
            {
                st->mode = MODE_CELT_ONLY;
            }
            else if (st->user_forced_mode == OPUS_AUTO)
            {
                int mode_voice, mode_music;
                int threshold;

                /* Interpolate based on stereo width */
                mode_voice = (int)(MULT16_32_Q15(Q15ONE - stereo_width, mode_thresholds[0][0])
                      + MULT16_32_Q15(stereo_width, mode_thresholds[1][0]));
                mode_music = (int)(MULT16_32_Q15(Q15ONE - stereo_width, mode_thresholds[1][1])
                      + MULT16_32_Q15(stereo_width, mode_thresholds[1][1]));
                /* Interpolate based on speech/music probability */
                threshold = mode_music + ((voice_est * voice_est * (mode_voice - mode_music)) >> 14);
                /* Bias towards SILK for VoIP because of some useful features */
                if (st->application == OPUS_APPLICATION_VOIP)
                    threshold += 8000;

                /*printf("%f %d\n", stereo_width/(float)Q15ONE, threshold);*/
                /* Hysteresis */
                if (st->prev_mode == MODE_CELT_ONLY)
                    threshold -= 4000;
                else if (st->prev_mode > 0)
                    threshold += 4000;

                st->mode = (equiv_rate >= threshold) ? MODE_CELT_ONLY : MODE_SILK_ONLY;

                /* When FEC is enabled and there's enough packet loss, use SILK.
                   Unless the FEC is set to 2, in which case we don't switch to SILK if we're confident we have music. */
                if (st->silk_mode.useInBandFEC != 0 && st->silk_mode.packetLossPercentage > (128 - voice_est) >> 4 && (st->fec_config != 2 || voice_est > 25))
                    st->mode = MODE_SILK_ONLY;
                /* When encoding voice and DTX is enabled but the generalized DTX cannot be used,
                   use SILK in order to make use of its DTX. */
                if (st->silk_mode.useDTX != 0 && voice_est > 100)
                    st->mode = MODE_SILK_ONLY;

                /* If max_data_bytes represents less than 6 kb/s, switch to CELT-only mode */
                if (max_data_bytes < (frame_rate > 50 ? 9000 : 6000) * frame_size / (st->Fs * 8))
                    st->mode = MODE_CELT_ONLY;
            }
            else
            {
                st->mode = st->user_forced_mode;
            }

            /* Override the chosen mode to make sure we meet the requested frame size */
            if (st->mode != MODE_CELT_ONLY && frame_size < st->Fs / 100)
                st->mode = MODE_CELT_ONLY;
            if (st->lfe != 0)
                st->mode = MODE_CELT_ONLY;

            if (st->prev_mode > 0 &&
                ((st->mode != MODE_CELT_ONLY && st->prev_mode == MODE_CELT_ONLY) ||
            (st->mode == MODE_CELT_ONLY && st->prev_mode != MODE_CELT_ONLY)))
            {
                redundancy = 1;
                celt_to_silk = BOOL2INT(st->mode != MODE_CELT_ONLY);
                if (celt_to_silk == 0)
                {
                    /* Switch to SILK/hybrid if frame size is 10 ms or more*/
                    if (frame_size >= st->Fs / 100)
                    {
                        st->mode = st->prev_mode;
                        to_celt = 1;
                    }
                    else
                    {
                        redundancy = 0;
                    }
                }
            }

            /* When encoding multiframes, we can ask for a switch to CELT only in the last frame. This switch
             * is processed above as the requested mode shouldn't interrupt stereo->mono transition. */
            if (st->stream_channels == 1 && st->prev_channels == 2 && st->silk_mode.toMono == 0
                  && st->mode != MODE_CELT_ONLY && st->prev_mode != MODE_CELT_ONLY)
            {
                /* Delay stereo->mono transition by two frames so that SILK can do a smooth downmix */
                st->silk_mode.toMono = 1;
                st->stream_channels = 2;
            }
            else
            {
                st->silk_mode.toMono = 0;
            }

            /* Update equivalent rate with mode decision. */
            equiv_rate = compute_equiv_rate(st->bitrate_bps, st->stream_channels, st->Fs / frame_size,
                  st->use_vbr, st->mode, st->silk_mode.complexity, st->silk_mode.packetLossPercentage);

            if (st->mode != MODE_CELT_ONLY && st->prev_mode == MODE_CELT_ONLY)
            {
                silk_EncControlStruct dummy;
                silk_InitEncoder(silk_enc, &dummy);
                prefill = 1;
            }

            /* Automatic (rate-dependent) bandwidth selection */
            if (st->mode == MODE_CELT_ONLY || st->first != 0 || st->silk_mode.allowBandwidthSwitch != 0)
            {
                int* voice_bandwidth_thresholds, music_bandwidth_thresholds;
                int* bandwidth_thresholds = stackalloc int[8];
                int bandwidth = OPUS_BANDWIDTH_FULLBAND;

                if (st->channels == 2 && st->force_channels != 1)
                {
                    voice_bandwidth_thresholds = stereo_voice_bandwidth_thresholds;
                    music_bandwidth_thresholds = stereo_music_bandwidth_thresholds;
                }
                else
                {
                    voice_bandwidth_thresholds = mono_voice_bandwidth_thresholds;
                    music_bandwidth_thresholds = mono_music_bandwidth_thresholds;
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
                    threshold = bandwidth_thresholds[2 * (bandwidth - OPUS_BANDWIDTH_MEDIUMBAND)];
                    hysteresis = bandwidth_thresholds[2 * (bandwidth - OPUS_BANDWIDTH_MEDIUMBAND) + 1];
                    if (st->first == 0)
                    {
                        if (st->auto_bandwidth >= bandwidth)
                            threshold -= hysteresis;
                        else
                            threshold += hysteresis;
                    }
                    if (equiv_rate >= threshold)
                        break;
                } while (--bandwidth > OPUS_BANDWIDTH_NARROWBAND);
                /* We don't use mediumband anymore, except when explicitly requested or during
                   mode transitions. */
                if (bandwidth == OPUS_BANDWIDTH_MEDIUMBAND)
                    bandwidth = OPUS_BANDWIDTH_WIDEBAND;
                st->bandwidth = st->auto_bandwidth = bandwidth;
                /* Prevents any transition to SWB/FB until the SILK layer has fully
                   switched to WB mode and turned the variable LP filter off */
                if (st->first == 0 && st->mode != MODE_CELT_ONLY && st->silk_mode.inWBmodeWithoutVariableLP == 0 && st->bandwidth > OPUS_BANDWIDTH_WIDEBAND)
                    st->bandwidth = OPUS_BANDWIDTH_WIDEBAND;
            }

            if (st->bandwidth > st->max_bandwidth)
                st->bandwidth = st->max_bandwidth;

            if (st->user_bandwidth != OPUS_AUTO)
                st->bandwidth = st->user_bandwidth;

            /* This prevents us from using hybrid at unsafe CBR/max rates */
            if (st->mode != MODE_CELT_ONLY && max_rate < 15000)
            {
                st->bandwidth = IMIN(st->bandwidth, OPUS_BANDWIDTH_WIDEBAND);
            }

            /* Prevents Opus from wasting bits on frequencies that are above
               the Nyquist rate of the input signal */
            if (st->Fs <= 24000 && st->bandwidth > OPUS_BANDWIDTH_SUPERWIDEBAND)
                st->bandwidth = OPUS_BANDWIDTH_SUPERWIDEBAND;
            if (st->Fs <= 16000 && st->bandwidth > OPUS_BANDWIDTH_WIDEBAND)
                st->bandwidth = OPUS_BANDWIDTH_WIDEBAND;
            if (st->Fs <= 12000 && st->bandwidth > OPUS_BANDWIDTH_MEDIUMBAND)
                st->bandwidth = OPUS_BANDWIDTH_MEDIUMBAND;
            if (st->Fs <= 8000 && st->bandwidth > OPUS_BANDWIDTH_NARROWBAND)
                st->bandwidth = OPUS_BANDWIDTH_NARROWBAND;
            /* Use detected bandwidth to reduce the encoded bandwidth. */
            if (st->detected_bandwidth != 0 && st->user_bandwidth == OPUS_AUTO)
            {
                int min_detected_bandwidth;
                /* Makes bandwidth detection more conservative just in case the detector
                   gets it wrong when we could have coded a high bandwidth transparently.
                   When operating in SILK/hybrid mode, we don't go below wideband to avoid
                   more complicated switches that require redundancy. */
                if (equiv_rate <= 18000 * st->stream_channels && st->mode == MODE_CELT_ONLY)
                    min_detected_bandwidth = OPUS_BANDWIDTH_NARROWBAND;
                else if (equiv_rate <= 24000 * st->stream_channels && st->mode == MODE_CELT_ONLY)
                    min_detected_bandwidth = OPUS_BANDWIDTH_MEDIUMBAND;
                else if (equiv_rate <= 30000 * st->stream_channels)
                    min_detected_bandwidth = OPUS_BANDWIDTH_WIDEBAND;
                else if (equiv_rate <= 44000 * st->stream_channels)
                    min_detected_bandwidth = OPUS_BANDWIDTH_SUPERWIDEBAND;
                else
                    min_detected_bandwidth = OPUS_BANDWIDTH_FULLBAND;

                st->detected_bandwidth = IMAX(st->detected_bandwidth, min_detected_bandwidth);
                st->bandwidth = IMIN(st->bandwidth, st->detected_bandwidth);
            }
            st->silk_mode.LBRR_coded = decide_fec(st->silk_mode.useInBandFEC, st->silk_mode.packetLossPercentage,
                  st->silk_mode.LBRR_coded, st->mode, &st->bandwidth, equiv_rate);
            opus_custom_encoder_ctl(celt_enc, OPUS_SET_LSB_DEPTH_REQUEST, lsb_depth);

            /* CELT mode doesn't support mediumband, use wideband instead */
            if (st->mode == MODE_CELT_ONLY && st->bandwidth == OPUS_BANDWIDTH_MEDIUMBAND)
                st->bandwidth = OPUS_BANDWIDTH_WIDEBAND;
            if (st->lfe != 0)
                st->bandwidth = OPUS_BANDWIDTH_NARROWBAND;

            curr_bandwidth = st->bandwidth;

            /* Chooses the appropriate mode for speech
               *NEVER* switch to/from CELT-only mode here as this will invalidate some assumptions */
            if (st->mode == MODE_SILK_ONLY && curr_bandwidth > OPUS_BANDWIDTH_WIDEBAND)
                st->mode = MODE_HYBRID;
            if (st->mode == MODE_HYBRID && curr_bandwidth <= OPUS_BANDWIDTH_WIDEBAND)
                st->mode = MODE_SILK_ONLY;

            /* Can't support higher than >60 ms frames, and >20 ms when in Hybrid or CELT-only modes */
            if ((frame_size > st->Fs / 50 && (st->mode != MODE_SILK_ONLY)) || frame_size > 3 * st->Fs / 50)
            {
                int enc_frame_size;
                int nb_frames;
                int max_header_bytes;
                int repacketize_len;
                int max_len_sum;
                int tot_size = 0;
                byte* curr_data;
                int tmp_len;
                int dtx_count = 0;

                if (st->mode == MODE_SILK_ONLY)
                {
                    if (frame_size == 2 * st->Fs / 25)  /* 80 ms -> 2x 40 ms */
                        enc_frame_size = st->Fs / 25;
                    else if (frame_size == 3 * st->Fs / 25)  /* 120 ms -> 2x 60 ms */
                        enc_frame_size = 3 * st->Fs / 50;
                    else                            /* 100 ms -> 5x 20 ms */
                        enc_frame_size = st->Fs / 50;
                }
                else
                    enc_frame_size = st->Fs / 50;

                nb_frames = frame_size / enc_frame_size;

                if (analysis_read_pos_bak != -1)
                {
                    /* Reset analysis position to the beginning of the first frame so we
                       can use it one frame at a time. */
                    st->analysis.read_pos = analysis_read_pos_bak;
                    st->analysis.read_subframe = analysis_read_subframe_bak;
                }

                /* Worst cases:
                 * 2 frames: Code 2 with different compressed sizes
                 * >2 frames: Code 3 VBR */
                max_header_bytes = nb_frames == 2 ? 3 : (2 + (nb_frames - 1) * 2);

                if (st->use_vbr != 0 || st->user_bitrate_bps == OPUS_BITRATE_MAX)
                    repacketize_len = out_data_bytes;
                else
                {
                    celt_assert(cbr_bytes >= 0);
                    repacketize_len = IMIN(cbr_bytes, out_data_bytes);
                }
                max_len_sum = nb_frames + repacketize_len - max_header_bytes;

                // ALLOC(rp, 1, OpusRepacketizer);
                OpusRepacketizer rp = default; // PORTING NOTE - this was previously ALLOCed, but I think it can just be a stack variable
                byte[] tmp_data_data = new byte[max_len_sum];
                fixed (byte* tmp_data = tmp_data_data)
                {
                    curr_data = tmp_data;
                    opus_repacketizer_init(&rp);

                    int bak_to_mono = st->silk_mode.toMono;
                    if (bak_to_mono != 0)
                        st->force_channels = 1;
                    else
                        st->prev_channels = st->stream_channels;

                    for (i = 0; i < nb_frames; i++)
                    {
                        int first_frame;
                        int frame_to_celt;
                        int frame_redundancy;
                        int curr_max;
                        /* Attempt DRED encoding until we have a non-DTX frame. In case of DTX refresh,
                           that allows for DRED not to be in the first frame. */
                        first_frame = BOOL2INT((i == 0) || (i == dtx_count));
                        st->silk_mode.toMono = 0;
                        st->nonfinal_frame = BOOL2INT(i < (nb_frames - 1));

                        /* When switching from SILK/Hybrid to CELT, only ask for a switch at the last frame */
                        frame_to_celt = BOOL2INT(to_celt != 0 && i == nb_frames - 1);
                        frame_redundancy = BOOL2INT(redundancy != 0 && (frame_to_celt != 0 || (to_celt == 0 && i == 0)));

                        curr_max = IMIN(3 * st->bitrate_bps / (3 * 8 * st->Fs / enc_frame_size), max_len_sum / nb_frames);
                        curr_max = IMIN(max_len_sum - tot_size, curr_max);
                        if (analysis_read_pos_bak != -1)
                        {
                            is_silence = is_digital_silence(pcm, frame_size, st->channels, lsb_depth);
                            /* Get analysis for current frame. */
                            tonality_get_info(&st->analysis, &analysis_info, enc_frame_size);
                        }

                        tmp_len = opus_encode_frame_native(st, pcm + i * (st->channels * enc_frame_size), enc_frame_size, curr_data, curr_max, float_api, first_frame,
                        &analysis_info,
                        is_silence,
                                  frame_redundancy, celt_to_silk, prefill,
                                  equiv_rate, frame_to_celt
                            );
                        if (tmp_len < 0)
                        {
                            return OPUS_INTERNAL_ERROR;
                        }
                        else if (tmp_len == 1)
                        {
                            dtx_count++;
                        }
                        ret = opus_repacketizer_cat(&rp, curr_data, tmp_len);

                        if (ret < 0)
                        {
                            return OPUS_INTERNAL_ERROR;
                        }
                        tot_size += tmp_len;
                        curr_data += tmp_len;
                    }
                    ret = opus_repacketizer_out_range_impl(&rp, 0, nb_frames, data, repacketize_len, 0, BOOL2INT(st->use_vbr == 0 && (dtx_count != nb_frames)), null, 0);
                    if (ret < 0)
                    {
                        ret = OPUS_INTERNAL_ERROR;
                    }
                    st->silk_mode.toMono = bak_to_mono;
                    return ret;
                }
            }
            else
            {
                ret = opus_encode_frame_native(st, pcm, frame_size, data, max_data_bytes, float_api, 1,
                          &analysis_info,
                          is_silence,
                          redundancy, celt_to_silk, prefill,
                          equiv_rate, to_celt
                    );
                return ret;
            }
        }

        internal static unsafe int opus_encode_frame_native(OpusEncoder* st, in float* pcm, int frame_size,
                byte* data, int max_data_bytes,
                int float_api, int first_frame,
                AnalysisInfo* analysis_info, int is_silence,
                int redundancy, int celt_to_silk, int prefill,
                int equiv_rate, int to_celt)
        {
            void* silk_enc;
            OpusCustomEncoder* celt_enc;
            OpusCustomMode* celt_mode;
            int i;
            int ret = 0;
            int nBytes;
            ec_ctx enc;
            int bytes_target;
            int start_band = 0;
            int redundancy_bytes = 0; /* Number of bytes to use for redundancy frame */
            int nb_compr_bytes;
            uint redundant_rng = 0;
            int cutoff_Hz;
            int hp_freq_smth1;
            float HB_gain;
            int apply_padding;
            int frame_rate;
            int curr_bandwidth;
            int delay_compensation;
            int total_buffer;
            int activity = VAD_NO_DECISION;

            st->rangeFinal = 0;
            silk_enc = (char*)st + st->silk_enc_offset;
            celt_enc = (OpusCustomEncoder*)((char*)st + st->celt_enc_offset);
            opus_custom_encoder_ctl(celt_enc, CELT_GET_MODE_REQUEST, &celt_mode);
            curr_bandwidth = st->bandwidth;
            if (st->application == OPUS_APPLICATION_RESTRICTED_LOWDELAY)
                delay_compensation = 0;
            else
                delay_compensation = st->delay_compensation;
            total_buffer = delay_compensation;

            frame_rate = st->Fs / frame_size;

            if (is_silence != 0)
            {
                activity = BOOL2INT(!(is_silence != 0));
            }
            else if (analysis_info->valid != 0)
            {
                activity = BOOL2INT(analysis_info->activity_probability >= DTX_ACTIVITY_THRESHOLD);
                if (activity == 0)
                {
                    /* Mark as active if this noise frame is sufficiently loud */
                    float noise_energy = compute_frame_energy(pcm, frame_size, st->channels);
                    activity = BOOL2INT(st->peak_signal_energy < (PSEUDO_SNR_THRESHOLD * noise_energy));
                }
            }

            /* For the first frame at a new SILK bandwidth */
            if (st->silk_bw_switch != 0)
            {
                redundancy = 1;
                celt_to_silk = 1;
                st->silk_bw_switch = 0;
                /* Do a prefill without resetting the sampling rate control. */
                prefill = 2;
            }

            /* If we decided to go with CELT, make sure redundancy is off, no matter what
               we decided earlier. */
            if (st->mode == MODE_CELT_ONLY)
                redundancy = 0;

            if (redundancy != 0)
            {
                redundancy_bytes = compute_redundancy_bytes(max_data_bytes, st->bitrate_bps, frame_rate, st->stream_channels);
                if (redundancy_bytes == 0)
                    redundancy = 0;
            }

            /* printf("%d %d %d %d\n", st->bitrate_bps, st->stream_channels, st->mode, curr_bandwidth); */
            bytes_target = IMIN(max_data_bytes - redundancy_bytes, st->bitrate_bps * frame_size / (st->Fs * 8)) - 1;

            data += 1;

            ec_enc_init(&enc, data, (uint)(max_data_bytes - 1));

            float[] pcm_buf_data = new float[(total_buffer + frame_size) * st->channels];
            fixed (float* pcm_buf = pcm_buf_data)
            {
                OPUS_COPY(pcm_buf, &st->delay_buffer[(st->encoder_buffer - total_buffer) * st->channels], total_buffer * st->channels);

                if (st->mode == MODE_CELT_ONLY)
                    hp_freq_smth1 = silk_LSHIFT(silk_lin2log(VARIABLE_HP_MIN_CUTOFF_HZ), 8);
                else
                    hp_freq_smth1 = ((silk_encoder*)silk_enc)->state_Fxx[0].sCmn.variable_HP_smth1_Q15;

                st->variable_HP_smth2_Q15 = silk_SMLAWB(st->variable_HP_smth2_Q15,
                      hp_freq_smth1 - st->variable_HP_smth2_Q15, SILK_FIX_CONST(VARIABLE_HP_SMTH_COEF2, 16));

                /* convert from log scale to Hertz */
                cutoff_Hz = silk_log2lin(silk_RSHIFT(st->variable_HP_smth2_Q15, 8));

                if (st->application == OPUS_APPLICATION_VOIP)
                {
                    hp_cutoff(pcm, cutoff_Hz, &pcm_buf[total_buffer * st->channels], st->hp_mem, frame_size, st->channels, st->Fs);
                }
                else
                {
                    dc_reject(pcm, 3, &pcm_buf[total_buffer * st->channels], st->hp_mem, frame_size, st->channels, st->Fs);
                }
                if (float_api != 0)
                {
                    float sum;
                    sum = celt_inner_prod(&pcm_buf[total_buffer * st->channels], &pcm_buf[total_buffer * st->channels], frame_size * st->channels);
                    /* This should filter out both NaNs and ridiculous signals that could
                       cause NaNs further down. */
                    if (!(sum < 1e9f) || celt_isnan(sum) != 0)
                    {
                        OPUS_CLEAR(&pcm_buf[total_buffer * st->channels], frame_size * st->channels);
                        st->hp_mem[0] = st->hp_mem[1] = st->hp_mem[2] = st->hp_mem[3] = 0;
                    }
                }

                /* SILK processing */
                HB_gain = Q15ONE;
                if (st->mode != MODE_CELT_ONLY)
                {
                    int total_bitRate, celt_rate;
                    short[] pcm_silk_data = new short[st->channels * frame_size];
                    fixed (short* pcm_silk = pcm_silk_data)
                    {
                        /* Distribute bits between SILK and CELT */
                        total_bitRate = 8 * bytes_target * frame_rate;
                        if (st->mode == MODE_HYBRID)
                        {
                            /* Base rate for SILK */
                            st->silk_mode.bitRate = compute_silk_rate_for_hybrid(total_bitRate,
                                  curr_bandwidth, BOOL2INT(st->Fs == 50 * frame_size), st->use_vbr, st->silk_mode.LBRR_coded,
                                  st->stream_channels);
                            if (st->energy_masking == null)
                            {
                                /* Increasingly attenuate high band when it gets allocated fewer bits */
                                celt_rate = total_bitRate - st->silk_mode.bitRate;
                                HB_gain = Q15ONE - SHR32(celt_exp2(-celt_rate * QCONST16(1.0f / 1024, 10)), 1);
                            }
                        }
                        else
                        {
                            /* SILK gets all bits */
                            st->silk_mode.bitRate = total_bitRate;
                        }

                        /* Surround masking for SILK */
                        if (st->energy_masking != null && st->use_vbr != 0 && st->lfe == 0)
                        {
                            float mask_sum = 0;
                            float masking_depth;
                            int rate_offset;
                            int c;
                            int end = 17;
                            short srate = 16000;
                            if (st->bandwidth == OPUS_BANDWIDTH_NARROWBAND)
                            {
                                end = 13;
                                srate = 8000;
                            }
                            else if (st->bandwidth == OPUS_BANDWIDTH_MEDIUMBAND)
                            {
                                end = 15;
                                srate = 12000;
                            }
                            for (c = 0; c < st->channels; c++)
                            {
                                for (i = 0; i < end; i++)
                                {
                                    float mask;
                                    mask = MAX16(MIN16(st->energy_masking[21 * c + i],
                                           QCONST16(.5f, DB_SHIFT)), -QCONST16(2.0f, DB_SHIFT));
                                    if (mask > 0)
                                        mask = HALF16(mask);
                                    mask_sum += mask;
                                }
                            }
                            /* Conservative rate reduction, we cut the masking in half */
                            masking_depth = mask_sum / end * st->channels;
                            masking_depth += QCONST16(.2f, DB_SHIFT);
                            rate_offset = (int)PSHR32(MULT16_16(srate, masking_depth), DB_SHIFT);
                            rate_offset = MAX32(rate_offset, -2 * st->silk_mode.bitRate / 3);
                            /* Split the rate change between the SILK and CELT part for hybrid. */
                            if (st->bandwidth == OPUS_BANDWIDTH_SUPERWIDEBAND || st->bandwidth == OPUS_BANDWIDTH_FULLBAND)
                                st->silk_mode.bitRate += 3 * rate_offset / 5;
                            else
                                st->silk_mode.bitRate += rate_offset;
                        }

                        st->silk_mode.payloadSize_ms = 1000 * frame_size / st->Fs;
                        st->silk_mode.nChannelsAPI = st->channels;
                        st->silk_mode.nChannelsInternal = st->stream_channels;
                        if (curr_bandwidth == OPUS_BANDWIDTH_NARROWBAND)
                        {
                            st->silk_mode.desiredInternalSampleRate = 8000;
                        }
                        else if (curr_bandwidth == OPUS_BANDWIDTH_MEDIUMBAND)
                        {
                            st->silk_mode.desiredInternalSampleRate = 12000;
                        }
                        else
                        {
                            celt_assert(st->mode == MODE_HYBRID || curr_bandwidth == OPUS_BANDWIDTH_WIDEBAND);
                            st->silk_mode.desiredInternalSampleRate = 16000;
                        }
                        if (st->mode == MODE_HYBRID)
                        {
                            /* Don't allow bandwidth reduction at lowest bitrates in hybrid mode */
                            st->silk_mode.minInternalSampleRate = 16000;
                        }
                        else
                        {
                            st->silk_mode.minInternalSampleRate = 8000;
                        }

                        st->silk_mode.maxInternalSampleRate = 16000;
                        if (st->mode == MODE_SILK_ONLY)
                        {
                            int effective_max_rate = frame_rate * max_data_bytes * 8;
                            if (frame_rate > 50)
                                effective_max_rate = effective_max_rate * 2 / 3;
                            if (effective_max_rate < 8000)
                            {
                                st->silk_mode.maxInternalSampleRate = 12000;
                                st->silk_mode.desiredInternalSampleRate = IMIN(12000, st->silk_mode.desiredInternalSampleRate);
                            }
                            if (effective_max_rate < 7000)
                            {
                                st->silk_mode.maxInternalSampleRate = 8000;
                                st->silk_mode.desiredInternalSampleRate = IMIN(8000, st->silk_mode.desiredInternalSampleRate);
                            }
                        }

                        st->silk_mode.useCBR = BOOL2INT(st->use_vbr == 0);

                        /* Call SILK encoder for the low band */

                        /* Max bits for SILK, counting ToC, redundancy bytes, and optionally redundancy. */
                        st->silk_mode.maxBits = (max_data_bytes - 1) * 8;
                        if (redundancy != 0 && redundancy_bytes >= 2)
                        {
                            /* Counting 1 bit for redundancy position and 20 bits for flag+size (only for hybrid). */
                            st->silk_mode.maxBits -= redundancy_bytes * 8 + 1;
                            if (st->mode == MODE_HYBRID)
                                st->silk_mode.maxBits -= 20;
                        }
                        if (st->silk_mode.useCBR != 0)
                        {
                            /* When we're in CBR mode, but we have non-SILK data to encode, switch SILK to VBR with cap to
                               save on complexity. Any variations will be absorbed by CELT and/or DRED and we can still
                               produce a constant bitrate without wasting bits. */
                            if (st->mode == MODE_HYBRID)
                            {
                                /* Allow SILK to steal up to 25% of the remaining bits */
                                short other_bits = (short)IMAX(0, st->silk_mode.maxBits - st->silk_mode.bitRate * frame_size / st->Fs);
                                st->silk_mode.maxBits = IMAX(0, st->silk_mode.maxBits - other_bits * 3 / 4);
                                st->silk_mode.useCBR = 0;
                            }
                        }
                        else
                        {
                            /* Constrained VBR. */
                            if (st->mode == MODE_HYBRID)
                            {
                                /* Compute SILK bitrate corresponding to the max total bits available */
                                int maxBitRate = compute_silk_rate_for_hybrid(st->silk_mode.maxBits * st->Fs / frame_size,
                                      curr_bandwidth, BOOL2INT(st->Fs == 50 * frame_size), st->use_vbr, st->silk_mode.LBRR_coded,
                                      st->stream_channels);
                                st->silk_mode.maxBits = maxBitRate * frame_size / st->Fs;
                            }
                        }

                        if (prefill != 0)
                        {
                            int zero = 0;
                            int prefill_offset;
                            /* Use a smooth onset for the SILK prefill to avoid the encoder trying to encode
                               a discontinuity. The exact location is what we need to avoid leaving any "gap"
                               in the audio when mixing with the redundant CELT frame. Here we can afford to
                               overwrite st->delay_buffer because the only thing that uses it before it gets
                               rewritten is tmp_prefill[] and even then only the part after the ramp really
                               gets used (rather than sent to the encoder and discarded) */
                            prefill_offset = st->channels * (st->encoder_buffer - st->delay_compensation - st->Fs / 400);
                            gain_fade(st->delay_buffer + prefill_offset, st->delay_buffer + prefill_offset,
                                  0, Q15ONE, celt_mode->overlap, st->Fs / 400, st->channels, celt_mode->window, st->Fs);
                            OPUS_CLEAR(st->delay_buffer, prefill_offset);
                            for (i = 0; i < st->encoder_buffer * st->channels; i++)
                                pcm_silk[i] = FLOAT2INT16(st->delay_buffer[i]);
                            silk_Encode(silk_enc, &st->silk_mode, pcm_silk, st->encoder_buffer, null, &zero, prefill, activity);
                            /* Prevent a second switch in the real encode call. */
                            st->silk_mode.opusCanSwitch = 0;
                        }

                        for (i = 0; i < frame_size * st->channels; i++)
                            pcm_silk[i] = FLOAT2INT16(pcm_buf[total_buffer * st->channels + i]);
                        ret = silk_Encode(silk_enc, &st->silk_mode, pcm_silk, frame_size, &enc, &nBytes, 0, activity);
                        if (ret != 0)
                        {
                            /*fprintf (stderr, "SILK encode error: %d\n", ret);*/
                            /* Handle error */
                            return OPUS_INTERNAL_ERROR;
                        }

                        /* Extract SILK internal bandwidth for signaling in first byte */
                        if (st->mode == MODE_SILK_ONLY)
                        {
                            if (st->silk_mode.internalSampleRate == 8000)
                            {
                                curr_bandwidth = OPUS_BANDWIDTH_NARROWBAND;
                            }
                            else if (st->silk_mode.internalSampleRate == 12000)
                            {
                                curr_bandwidth = OPUS_BANDWIDTH_MEDIUMBAND;
                            }
                            else if (st->silk_mode.internalSampleRate == 16000)
                            {
                                curr_bandwidth = OPUS_BANDWIDTH_WIDEBAND;
                            }
                        }
                        else
                        {
                            celt_assert(st->silk_mode.internalSampleRate == 16000);
                        }

                        st->silk_mode.opusCanSwitch = BOOL2INT(st->silk_mode.switchReady != 0 && st->nonfinal_frame == 0);

                        if (nBytes == 0)
                        {
                            st->rangeFinal = 0;
                            data[-1] = gen_toc(st->mode, st->Fs / frame_size, curr_bandwidth, st->stream_channels);
                            return 1;
                        }

                        /* FIXME: How do we allocate the redundancy for CBR? */
                        if (st->silk_mode.opusCanSwitch != 0)
                        {
                            redundancy_bytes = compute_redundancy_bytes(max_data_bytes, st->bitrate_bps, frame_rate, st->stream_channels);
                            redundancy = BOOL2INT(redundancy_bytes != 0);
                            celt_to_silk = 0;
                            st->silk_bw_switch = 1;
                        }
                    }
                }

                /* CELT processing */
                {
                    int endband = 21;

                    switch (curr_bandwidth)
                    {
                        case OPUS_BANDWIDTH_NARROWBAND:
                            endband = 13;
                            break;
                        case OPUS_BANDWIDTH_MEDIUMBAND:
                        case OPUS_BANDWIDTH_WIDEBAND:
                            endband = 17;
                            break;
                        case OPUS_BANDWIDTH_SUPERWIDEBAND:
                            endband = 19;
                            break;
                        case OPUS_BANDWIDTH_FULLBAND:
                            endband = 21;
                            break;
                    }
                    opus_custom_encoder_ctl(celt_enc, CELT_SET_END_BAND_REQUEST, (endband));
                    opus_custom_encoder_ctl(celt_enc, CELT_SET_CHANNELS_REQUEST, (st->stream_channels));
                }
                opus_custom_encoder_ctl(celt_enc, OPUS_SET_BITRATE_REQUEST, (OPUS_BITRATE_MAX));
                if (st->mode != MODE_SILK_ONLY)
                {
                    int celt_pred = 2; // PORTING NOTE: original code had this as a float for some reason?
                    /* We may still decide to disable prediction later */
                    if (st->silk_mode.reducedDependency != 0)
                        celt_pred = 0;
                    opus_custom_encoder_ctl(celt_enc, CELT_SET_PREDICTION_REQUEST, (celt_pred));
                }

                float[] tmp_prefill_data = new float[st->channels * st->Fs / 400];
                fixed (float* tmp_prefill = tmp_prefill_data)
                {
                    if (st->mode != MODE_SILK_ONLY && st->mode != st->prev_mode && st->prev_mode > 0)
                    {
                        OPUS_COPY(tmp_prefill, &st->delay_buffer[(st->encoder_buffer - total_buffer - st->Fs / 400) * st->channels], st->channels * st->Fs / 400);
                    }

                    if (st->channels * (st->encoder_buffer - (frame_size + total_buffer)) > 0)
                    {
                        OPUS_MOVE(st->delay_buffer, &st->delay_buffer[st->channels * frame_size], st->channels * (st->encoder_buffer - frame_size - total_buffer));
                        OPUS_COPY(&st->delay_buffer[st->channels * (st->encoder_buffer - frame_size - total_buffer)],
                              &pcm_buf[0],
                              (frame_size + total_buffer) * st->channels);
                    }
                    else
                    {
                        OPUS_COPY(st->delay_buffer, &pcm_buf[(frame_size + total_buffer - st->encoder_buffer) * st->channels], st->encoder_buffer * st->channels);
                    }
                    /* gain_fade() and stereo_fade() need to be after the buffer copying
                       because we don't want any of this to affect the SILK part */
                    if (st->prev_HB_gain < Q15ONE || HB_gain < Q15ONE)
                    {
                        gain_fade(pcm_buf, pcm_buf,
                              st->prev_HB_gain, HB_gain, celt_mode->overlap, frame_size, st->channels, celt_mode->window, st->Fs);
                    }
                    st->prev_HB_gain = HB_gain;
                    if (st->mode != MODE_HYBRID || st->stream_channels == 1)
                    {
                        if (equiv_rate > 32000)
                            st->silk_mode.stereoWidth_Q14 = 16384;
                        else if (equiv_rate < 16000)
                            st->silk_mode.stereoWidth_Q14 = 0;
                        else
                            st->silk_mode.stereoWidth_Q14 = 16384 - 2048 * (int)(32000 - equiv_rate) / (equiv_rate - 14000);
                    }
                    if (st->energy_masking == null && st->channels == 2)
                    {
                        /* Apply stereo width reduction (at low bitrates) */
                        if (st->hybrid_stereo_width_Q14 < (1 << 14) || st->silk_mode.stereoWidth_Q14 < (1 << 14))
                        {
                            float g1, g2;
                            g1 = st->hybrid_stereo_width_Q14;
                            g2 = (float)(st->silk_mode.stereoWidth_Q14);
                            g1 *= (1.0f / 16384);
                            g2 *= (1.0f / 16384);
                            stereo_fade(pcm_buf, pcm_buf, g1, g2, celt_mode->overlap,
                                  frame_size, st->channels, celt_mode->window, st->Fs);
                            st->hybrid_stereo_width_Q14 = (short)st->silk_mode.stereoWidth_Q14;
                        }
                    }

                    if (st->mode != MODE_CELT_ONLY && ec_tell(&enc) + 17 + 20 * BOOL2INT(st->mode == MODE_HYBRID) <= 8 * (max_data_bytes - 1))
                    {
                        /* For SILK mode, the redundancy is inferred from the length */
                        if (st->mode == MODE_HYBRID)
                            ec_enc_bit_logp(&enc, redundancy, 12);
                        if (redundancy != 0)
                        {
                            int max_redundancy;
                            ec_enc_bit_logp(&enc, celt_to_silk, 1);
                            if (st->mode == MODE_HYBRID)
                            {
                                /* Reserve the 8 bits needed for the redundancy length,
                                   and at least a few bits for CELT if possible */
                                max_redundancy = (max_data_bytes - 1) - ((ec_tell(&enc) + 8 + 3 + 7) >> 3);
                            }
                            else
                                max_redundancy = (max_data_bytes - 1) - ((ec_tell(&enc) + 7) >> 3);
                            /* Target the same bit-rate for redundancy as for the rest,
                               up to a max of 257 bytes */
                            redundancy_bytes = IMIN(max_redundancy, redundancy_bytes);
                            redundancy_bytes = IMIN(257, IMAX(2, redundancy_bytes));
                            if (st->mode == MODE_HYBRID)
                                ec_enc_uint(&enc, (uint)(redundancy_bytes - 2), 256);
                        }
                    }
                    else
                    {
                        redundancy = 0;
                    }

                    if (redundancy == 0)
                    {
                        st->silk_bw_switch = 0;
                        redundancy_bytes = 0;
                    }
                    if (st->mode != MODE_CELT_ONLY) start_band = 17;

                    if (st->mode == MODE_SILK_ONLY)
                    {
                        ret = (ec_tell(&enc) + 7) >> 3;
                        ec_enc_done(&enc);
                        nb_compr_bytes = ret;
                    }
                    else
                    {
                        nb_compr_bytes = (max_data_bytes - 1) - redundancy_bytes;
                        ec_enc_shrink(&enc, (uint)nb_compr_bytes);
                    }

                    if (redundancy != 0 || st->mode != MODE_SILK_ONLY)
                        opus_custom_encoder_ctl(celt_enc, CELT_SET_ANALYSIS_REQUEST, (analysis_info));
                    if (st->mode == MODE_HYBRID)
                    {
                        SILKInfo info;
                        info.signalType = st->silk_mode.signalType;
                        info.offset = st->silk_mode.offset;
                        opus_custom_encoder_ctl(celt_enc, CELT_SET_SILK_INFO_REQUEST, (&info));
                    }

                    /* 5 ms redundant frame for CELT->SILK */
                    if (redundancy != 0 && celt_to_silk != 0)
                    {
                        int err;
                        opus_custom_encoder_ctl(celt_enc, CELT_SET_START_BAND_REQUEST, (0));
                        opus_custom_encoder_ctl(celt_enc, OPUS_SET_VBR_REQUEST, (0));
                        opus_custom_encoder_ctl(celt_enc, OPUS_SET_BITRATE_REQUEST, (OPUS_BITRATE_MAX));
                        err = celt_encode_with_ec(celt_enc, pcm_buf, st->Fs / 200, data + nb_compr_bytes, redundancy_bytes, null);
                        if (err < 0)
                        {
                            return OPUS_INTERNAL_ERROR;
                        }
                        opus_custom_encoder_ctl(celt_enc, OPUS_GET_FINAL_RANGE_REQUEST, (&redundant_rng));
                        opus_custom_encoder_ctl(celt_enc, OPUS_RESET_STATE);
                    }

                    opus_custom_encoder_ctl(celt_enc, CELT_SET_START_BAND_REQUEST, (start_band));

                    if (st->mode != MODE_SILK_ONLY)
                    {
                        opus_custom_encoder_ctl(celt_enc, OPUS_SET_VBR_REQUEST, (st->use_vbr));
                        if (st->mode == MODE_HYBRID)
                        {
                            if (st->use_vbr != 0)
                            {
                                opus_custom_encoder_ctl(celt_enc, OPUS_SET_BITRATE_REQUEST, (st->bitrate_bps - st->silk_mode.bitRate));
                                opus_custom_encoder_ctl(celt_enc, OPUS_SET_VBR_CONSTRAINT_REQUEST, (0));
                            }
                        }
                        else
                        {
                            if (st->use_vbr != 0)
                            {
                                opus_custom_encoder_ctl(celt_enc, OPUS_SET_VBR_REQUEST, (1));
                                opus_custom_encoder_ctl(celt_enc, OPUS_SET_VBR_CONSTRAINT_REQUEST, (st->vbr_constraint));
                                opus_custom_encoder_ctl(celt_enc, OPUS_SET_BITRATE_REQUEST, (st->bitrate_bps));
                            }
                        }
                        if (st->mode != st->prev_mode && st->prev_mode > 0)
                        {
                            byte* dummy = stackalloc byte[2];
                            opus_custom_encoder_ctl(celt_enc, OPUS_RESET_STATE);

                            /* Prefilling */
                            celt_encode_with_ec(celt_enc, tmp_prefill, st->Fs / 400, dummy, 2, null);
                            opus_custom_encoder_ctl(celt_enc, CELT_SET_PREDICTION_REQUEST, (0));
                        }
                        /* If false, we already busted the budget and we'll end up with a "PLC frame" */
                        if (ec_tell(&enc) <= 8 * nb_compr_bytes)
                        {
                            ret = celt_encode_with_ec(celt_enc, pcm_buf, frame_size, null, nb_compr_bytes, &enc);
                            if (ret < 0)
                            {
                                return OPUS_INTERNAL_ERROR;
                            }
                            /* Put CELT->SILK redundancy data in the right place. */
                            if (redundancy != 0 && celt_to_silk != 0 && st->mode == MODE_HYBRID && nb_compr_bytes != ret)
                            {
                                OPUS_MOVE(data + ret, data + nb_compr_bytes, redundancy_bytes);
                                nb_compr_bytes = ret + redundancy_bytes;
                            }
                        }
                    }

                    /* 5 ms redundant frame for SILK->CELT */
                    if (redundancy != 0 && celt_to_silk == 0)
                    {
                        int err;
                        byte* dummy = stackalloc byte[2];
                        int N2, N4;
                        N2 = st->Fs / 200;
                        N4 = st->Fs / 400;

                        opus_custom_encoder_ctl(celt_enc, OPUS_RESET_STATE);
                        opus_custom_encoder_ctl(celt_enc, CELT_SET_START_BAND_REQUEST, (0));
                        opus_custom_encoder_ctl(celt_enc, CELT_SET_PREDICTION_REQUEST, (0));
                        opus_custom_encoder_ctl(celt_enc, OPUS_SET_VBR_REQUEST, (0));
                        opus_custom_encoder_ctl(celt_enc, OPUS_SET_BITRATE_REQUEST, (OPUS_BITRATE_MAX));

                        if (st->mode == MODE_HYBRID)
                        {
                            /* Shrink packet to what the encoder actually used. */
                            nb_compr_bytes = ret;
                            ec_enc_shrink(&enc, (uint)nb_compr_bytes);
                        }
                        /* NOTE: We could speed this up slightly (at the expense of code size) by just adding a function that prefills the buffer */
                        celt_encode_with_ec(celt_enc, pcm_buf + st->channels * (frame_size - N2 - N4), N4, dummy, 2, null);

                        err = celt_encode_with_ec(celt_enc, pcm_buf + st->channels * (frame_size - N2), N2, data + nb_compr_bytes, redundancy_bytes, null);
                        if (err < 0)
                        {
                            return OPUS_INTERNAL_ERROR;
                        }
                        opus_custom_encoder_ctl(celt_enc, OPUS_GET_FINAL_RANGE_REQUEST, &redundant_rng);
                    }

                    /* Signalling the mode in the first byte */
                    data--;
                    data[0] = gen_toc(st->mode, st->Fs / frame_size, curr_bandwidth, st->stream_channels);

                    st->rangeFinal = enc.rng ^ redundant_rng;

                    if (to_celt != 0)
                        st->prev_mode = MODE_CELT_ONLY;
                    else
                        st->prev_mode = st->mode;
                    st->prev_channels = st->stream_channels;
                    st->prev_framesize = frame_size;

                    st->first = 0;

                    /* DTX decision */
                    if (st->use_dtx != 0 && (analysis_info->valid != 0 || is_silence != 0))
                    {
                        if (decide_dtx_mode(activity, &st->nb_no_activity_ms_Q1, 2 * 1000 * frame_size / st->Fs) != 0)
                        {
                            st->rangeFinal = 0;
                            data[0] = gen_toc(st->mode, st->Fs / frame_size, curr_bandwidth, st->stream_channels);
                            return 1;
                        }
                    }
                    else
                    {
                        st->nb_no_activity_ms_Q1 = 0;
                    }

                    /* In the unlikely case that the SILK encoder busted its target, tell
                       the decoder to call the PLC */
                    if (ec_tell(&enc) > (max_data_bytes - 1) * 8)
                    {
                        if (max_data_bytes < 2)
                        {
                            return OPUS_BUFFER_TOO_SMALL;
                        }
                        data[1] = 0;
                        ret = 1;
                        st->rangeFinal = 0;
                    }
                    else if (st->mode == MODE_SILK_ONLY && redundancy == 0)
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
                    apply_padding = BOOL2INT(!(st->use_vbr != 0));
                    if (apply_padding != 0)
                    {
                        if (opus_packet_pad(data, ret, max_data_bytes) != OPUS_OK)
                        {
                            return OPUS_INTERNAL_ERROR;
                        }
                        ret = max_data_bytes;
                    }
                    return ret;
                }
            }
        }

        internal static unsafe int opus_encode(OpusEncoder* st, in short* pcm, int analysis_frame_size,
            byte* data, int max_data_bytes)
        {
            int i, ret;
            int frame_size;

            frame_size = frame_size_select(analysis_frame_size, st->variable_duration, st->Fs);
            if (frame_size <= 0)
            {
                return OPUS_BAD_ARG;
            }

            float[] input_data = new float[frame_size * st->channels];
            fixed (float* input = input_data)
            {
                for (i = 0; i < frame_size * st->channels; i++)
                    input[i] = (1.0f / 32768) * pcm[i];
                ret = opus_encode_native(st, input, frame_size, data, max_data_bytes, 16,
                                         pcm, analysis_frame_size, 0, -2, st->channels, downmix_int, 0);
                return ret;
            }
        }
        internal static unsafe int opus_encode_float(OpusEncoder* st, in float* pcm, int analysis_frame_size,
                      byte* data, int out_data_bytes)
        {
            int frame_size;
            frame_size = frame_size_select(analysis_frame_size, st->variable_duration, st->Fs);
            return opus_encode_native(st, pcm, frame_size, data, out_data_bytes, 24,
                                      pcm, analysis_frame_size, 0, -2, st->channels, downmix_float, 1);
        }

        /// <summary>
        /// Override for int parameter (most setters)
        /// </summary>
        internal static unsafe int opus_encoder_ctl(OpusEncoder* st, int request, int value)
        {
            int ret = OPUS_OK;
            OpusCustomEncoder* celt_enc = (OpusCustomEncoder*)((char*)st + st->celt_enc_offset);
            switch (request)
            {
                case OPUS_SET_APPLICATION_REQUEST:
                    {
                        if ((value != OPUS_APPLICATION_VOIP && value != OPUS_APPLICATION_AUDIO
                                && value != OPUS_APPLICATION_RESTRICTED_LOWDELAY)
                            || (st->first == 0 && st->application != value))
                        {
                            ret = OPUS_BAD_ARG;
                            break;
                        }
                        st->application = value;
                        st->analysis.application = value;
                    }
                    break;
                case OPUS_SET_BITRATE_REQUEST:
                    {
                        if (value != OPUS_AUTO && value != OPUS_BITRATE_MAX)
                        {
                            if (value <= 0)
                                goto bad_arg;
                            else if (value <= 500)
                                value = 500;
                            else if (value > (int)300000 * st->channels)
                                value = (int)300000 * st->channels;
                        }
                        st->user_bitrate_bps = value;
                    }
                    break;
                case OPUS_SET_FORCE_CHANNELS_REQUEST:
                    {
                        if ((value < 1 || value > st->channels) && value != OPUS_AUTO)
                        {
                            goto bad_arg;
                        }
                        st->force_channels = value;
                    }
                    break;
                case OPUS_SET_MAX_BANDWIDTH_REQUEST:
                    {
                        if (value < OPUS_BANDWIDTH_NARROWBAND || value > OPUS_BANDWIDTH_FULLBAND)
                        {
                            goto bad_arg;
                        }
                        st->max_bandwidth = value;
                        if (st->max_bandwidth == OPUS_BANDWIDTH_NARROWBAND)
                        {
                            st->silk_mode.maxInternalSampleRate = 8000;
                        }
                        else if (st->max_bandwidth == OPUS_BANDWIDTH_MEDIUMBAND)
                        {
                            st->silk_mode.maxInternalSampleRate = 12000;
                        }
                        else
                        {
                            st->silk_mode.maxInternalSampleRate = 16000;
                        }
                    }
                    break;
                case OPUS_SET_BANDWIDTH_REQUEST:
                    {
                        if ((value < OPUS_BANDWIDTH_NARROWBAND || value > OPUS_BANDWIDTH_FULLBAND) && value != OPUS_AUTO)
                        {
                            goto bad_arg;
                        }
                        st->user_bandwidth = value;
                        if (st->user_bandwidth == OPUS_BANDWIDTH_NARROWBAND)
                        {
                            st->silk_mode.maxInternalSampleRate = 8000;
                        }
                        else if (st->user_bandwidth == OPUS_BANDWIDTH_MEDIUMBAND)
                        {
                            st->silk_mode.maxInternalSampleRate = 12000;
                        }
                        else
                        {
                            st->silk_mode.maxInternalSampleRate = 16000;
                        }
                    }
                    break;
                case OPUS_SET_DTX_REQUEST:
                    {
                        if (value < 0 || value > 1)
                        {
                            goto bad_arg;
                        }
                        st->use_dtx = value;
                    }
                    break;
                case OPUS_SET_COMPLEXITY_REQUEST:
                    {
                        if (value < 0 || value > 10)
                        {
                            goto bad_arg;
                        }
                        st->silk_mode.complexity = value;
                        opus_custom_encoder_ctl(celt_enc, OPUS_SET_COMPLEXITY_REQUEST, (value));
                    }
                    break;
                case OPUS_SET_INBAND_FEC_REQUEST:
                    {
                        if (value < 0 || value > 2)
                        {
                            goto bad_arg;
                        }
                        st->fec_config = value;
                        st->silk_mode.useInBandFEC = BOOL2INT(value != 0);
                    }
                    break;
                case OPUS_SET_PACKET_LOSS_PERC_REQUEST:
                    {
                        if (value < 0 || value > 100)
                        {
                            goto bad_arg;
                        }
                        st->silk_mode.packetLossPercentage = value;
                        opus_custom_encoder_ctl(celt_enc, OPUS_SET_PACKET_LOSS_PERC_REQUEST, (value));
                    }
                    break;
                case OPUS_SET_VBR_REQUEST:
                    {
                        if (value < 0 || value > 1)
                        {
                            goto bad_arg;
                        }
                        st->use_vbr = value;
                        st->silk_mode.useCBR = 1 - value;
                    }
                    break;
                case OPUS_SET_VOICE_RATIO_REQUEST:
                    {
                        if (value < -1 || value > 100)
                        {
                            goto bad_arg;
                        }
                        st->voice_ratio = value;
                    }
                    break;
                case OPUS_SET_VBR_CONSTRAINT_REQUEST:
                    {
                        if (value < 0 || value > 1)
                        {
                            goto bad_arg;
                        }
                        st->vbr_constraint = value;
                    }
                    break;
                case OPUS_SET_SIGNAL_REQUEST:
                    {
                        if (value != OPUS_AUTO && value != OPUS_SIGNAL_VOICE && value != OPUS_SIGNAL_MUSIC)
                        {
                            goto bad_arg;
                        }
                        st->signal_type = value;
                    }
                    break;
                case OPUS_SET_LSB_DEPTH_REQUEST:
                    {
                        if (value < 8 || value > 24)
                        {
                            goto bad_arg;
                        }
                        st->lsb_depth = value;
                    }
                    break;
                case OPUS_SET_EXPERT_FRAME_DURATION_REQUEST:
                    {
                        if (value != OPUS_FRAMESIZE_ARG && value != OPUS_FRAMESIZE_2_5_MS &&
                            value != OPUS_FRAMESIZE_5_MS && value != OPUS_FRAMESIZE_10_MS &&
                            value != OPUS_FRAMESIZE_20_MS && value != OPUS_FRAMESIZE_40_MS &&
                            value != OPUS_FRAMESIZE_60_MS && value != OPUS_FRAMESIZE_80_MS &&
                            value != OPUS_FRAMESIZE_100_MS && value != OPUS_FRAMESIZE_120_MS)
                        {
                            goto bad_arg;
                        }
                        st->variable_duration = value;
                    }
                    break;
                case OPUS_SET_LFE_REQUEST:
                    {
                        st->lfe = value;
                        ret = opus_custom_encoder_ctl(celt_enc, OPUS_SET_LFE_REQUEST, (value));
                    }
                    break;
                case OPUS_SET_PREDICTION_DISABLED_REQUEST:
                    {
                        if (value > 1 || value < 0)
                            goto bad_arg;
                        st->silk_mode.reducedDependency = value;
                    }
                    break;
                case OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST:
                    {
                        if (value < 0 || value > 1)
                        {
                            goto bad_arg;
                        }
                        opus_custom_encoder_ctl(celt_enc, OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST, (value));
                    }
                    break;
                case OPUS_SET_FORCE_MODE_REQUEST:
                    {
                        if ((value < MODE_SILK_ONLY || value > MODE_CELT_ONLY) && value != OPUS_AUTO)
                        {
                            goto bad_arg;
                        }
                        st->user_forced_mode = value;
                    }
                    break;
                default:
                    /* fprintf(stderr, "unknown opus_encoder_ctl() request: %d", request);*/
                    ret = OPUS_UNIMPLEMENTED;
                    break;
            }
            return ret;
        bad_arg:
            return OPUS_BAD_ARG;
        }

        /// <summary>
        /// Override for int* parameter (most getters)
        /// </summary>
        internal static unsafe int opus_encoder_ctl(OpusEncoder* st, int request, int* value)
        {
            int ret = OPUS_OK;
            OpusCustomEncoder* celt_enc = (OpusCustomEncoder*)((char*)st + st->celt_enc_offset);
            switch (request)
            {
                case OPUS_GET_APPLICATION_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->application;
                    }
                    break;
                case OPUS_GET_BITRATE_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = user_bitrate_to_bitrate(st, st->prev_framesize, 1276);
                    }
                    break;
                case OPUS_GET_FORCE_CHANNELS_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->force_channels;
                    }
                    break;
                case OPUS_GET_MAX_BANDWIDTH_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->max_bandwidth;
                    }
                    break;
                case OPUS_GET_BANDWIDTH_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->bandwidth;
                    }
                    break;
                case OPUS_GET_DTX_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->use_dtx;
                    }
                    break;
                case OPUS_GET_COMPLEXITY_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->silk_mode.complexity;
                    }
                    break;
                case OPUS_GET_INBAND_FEC_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->fec_config;
                    }
                    break;
                case OPUS_GET_PACKET_LOSS_PERC_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->silk_mode.packetLossPercentage;
                    }
                    break;
                case OPUS_GET_VBR_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->use_vbr;
                    }
                    break;
                case OPUS_GET_VOICE_RATIO_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->voice_ratio;
                    }
                    break;
                case OPUS_GET_VBR_CONSTRAINT_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->vbr_constraint;
                    }
                    break;
                case OPUS_GET_SIGNAL_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->signal_type;
                    }
                    break;
                case OPUS_GET_LOOKAHEAD_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->Fs / 400;
                        if (st->application != OPUS_APPLICATION_RESTRICTED_LOWDELAY)
                            *value += st->delay_compensation;
                    }
                    break;
                case OPUS_GET_SAMPLE_RATE_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->Fs;
                    }
                    break;
                case OPUS_GET_LSB_DEPTH_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->lsb_depth;
                    }
                    break;
                case OPUS_GET_EXPERT_FRAME_DURATION_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->variable_duration;
                    }
                    break;
                case OPUS_GET_PREDICTION_DISABLED_REQUEST:
                    {
                        if (value == null)
                            goto bad_arg;
                        *value = st->silk_mode.reducedDependency;
                    }
                    break;
                case OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        opus_custom_encoder_ctl(celt_enc, OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST, (value));
                    }
                    break;
                case OPUS_GET_IN_DTX_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        if (st->silk_mode.useDTX != 0 && (st->prev_mode == MODE_SILK_ONLY || st->prev_mode == MODE_HYBRID))
                        {
                            /* DTX determined by Silk. */
                            silk_encoder* silk_enc = (silk_encoder*)(void*)((char*)st + st->silk_enc_offset);
                            *value = BOOL2INT(silk_enc->state_Fxx[0].sCmn.noSpeechCounter >= NB_SPEECH_FRAMES_BEFORE_DTX);
                            /* Stereo: check second channel unless only the middle channel was encoded. */
                            if (*value == 1 && st->silk_mode.nChannelsInternal == 2 && silk_enc->prev_decode_only_middle == 0)
                            {
                                *value = BOOL2INT(silk_enc->state_Fxx[1].sCmn.noSpeechCounter >= NB_SPEECH_FRAMES_BEFORE_DTX);
                            }
                        }
                        else if (st->use_dtx != 0)
                        {
                            /* DTX determined by Opus. */
                            *value = BOOL2INT(st->nb_no_activity_ms_Q1 >= NB_SPEECH_FRAMES_BEFORE_DTX * 20 * 2);
                        }
                        else
                        {
                            *value = 0;
                        }
                    }
                    break;
                default:
                    /* fprintf(stderr, "unknown opus_encoder_ctl() request: %d", request);*/
                    ret = OPUS_UNIMPLEMENTED;
                    break;
            }
            return ret;
        bad_arg:
            return OPUS_BAD_ARG;
        }

        /// <summary>
        /// Override for OPUS_GET_FINAL_RANGE_REQUEST
        /// </summary>
        internal static unsafe int opus_encoder_ctl(OpusEncoder* st, int request, uint* value)
        {
            int ret = OPUS_OK;
            OpusCustomEncoder* celt_enc = (OpusCustomEncoder*)((char*)st + st->celt_enc_offset);
            switch (request)
            {
                case OPUS_GET_FINAL_RANGE_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->rangeFinal;
                    }
                    break;
                default:
                    /* fprintf(stderr, "unknown opus_encoder_ctl() request: %d", request);*/
                    ret = OPUS_UNIMPLEMENTED;
                    break;
            }
            return ret;
        bad_arg:
            return OPUS_BAD_ARG;
        }

        /// <summary>
        /// Override for OPUS_SET_ENERGY_MASK_REQUEST
        /// </summary>
        internal static unsafe int opus_encoder_ctl(OpusEncoder* st, int request, float* value)
        {
            int ret = OPUS_OK;
            OpusCustomEncoder* celt_enc = (OpusCustomEncoder*)((char*)st + st->celt_enc_offset);
            switch (request)
            {
                case OPUS_SET_ENERGY_MASK_REQUEST:
                    {
                        st->energy_masking = value;
                        ret = opus_custom_encoder_ctl(celt_enc, OPUS_SET_ENERGY_MASK_REQUEST, (value));
                    }
                    break;
                default:
                    /* fprintf(stderr, "unknown opus_encoder_ctl() request: %d", request);*/
                    ret = OPUS_UNIMPLEMENTED;
                    break;
            }
            return ret;
        }

        /// <summary>
        /// Override for OPUS_RESET_STATE
        /// </summary>
        internal static unsafe int opus_encoder_ctl(OpusEncoder* st, int request)
        {
            int ret = OPUS_OK;
            OpusCustomEncoder* celt_enc = (OpusCustomEncoder*)((char*)st + st->celt_enc_offset);
            switch (request)
            {
                case OPUS_RESET_STATE:
                    {
                        void* silk_enc;
                        silk_EncControlStruct dummy;
                        silk_enc = (char*)st + st->silk_enc_offset;
                        tonality_analysis_reset(&st->analysis);

                        OPUS_CLEAR(
                            ((byte*)st) + st->OPUS_ENCODER_RESET_START,
                            sizeof(OpusEncoder) - st->OPUS_ENCODER_RESET_START);

                        opus_custom_encoder_ctl(celt_enc, OPUS_RESET_STATE);
                        silk_InitEncoder(silk_enc, &dummy);
                        st->stream_channels = st->channels;
                        st->hybrid_stereo_width_Q14 = 1 << 14;
                        st->prev_HB_gain = Q15ONE;
                        st->first = 1;
                        st->mode = MODE_HYBRID;
                        st->bandwidth = OPUS_BANDWIDTH_FULLBAND;
                        st->variable_HP_smth2_Q15 = silk_LSHIFT(silk_lin2log(VARIABLE_HP_MIN_CUTOFF_HZ), 8);
                    }
                    break;
                default:
                    /* fprintf(stderr, "unknown opus_encoder_ctl() request: %d", request);*/
                    ret = OPUS_UNIMPLEMENTED;
                    break;
            }
            return ret;
        }

        /// <summary>
        /// Override for CELT_GET_MODE_REQUEST
        /// </summary>
        internal static unsafe int opus_encoder_ctl(OpusEncoder* st, int request, OpusCustomMode** value)
        {
            int ret = OPUS_OK;
            OpusCustomEncoder* celt_enc = (OpusCustomEncoder*)((char*)st + st->celt_enc_offset);
            switch (request)
            {
                case CELT_GET_MODE_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        ret = opus_custom_encoder_ctl(celt_enc, CELT_GET_MODE_REQUEST, (value));
                    }
                    break;
                default:
                    /* fprintf(stderr, "unknown opus_encoder_ctl() request: %d", request);*/
                    ret = OPUS_UNIMPLEMENTED;
                    break;
            }
            return ret;
        bad_arg:
            return OPUS_BAD_ARG;
        }

        internal static unsafe void opus_encoder_destroy(OpusEncoder* st)
        {
            opus_free(st);
        }
    }
}
