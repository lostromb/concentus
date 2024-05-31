using System;
using System.Collections.Generic;
using System.Text;

namespace HellaUnsafe.Silk
{
    internal static class Define
    {
        /* Max number of encoder channels (1/2) */
        internal const int ENCODER_NUM_CHANNELS                    = 2;
        /* Number of decoder channels (1/2) */
        internal const int DECODER_NUM_CHANNELS                    = 2;

        internal const int MAX_FRAMES_PER_PACKET                   = 3;

        /* Limits on bitrate */
        internal const int MIN_TARGET_RATE_BPS                     = 5000;
        internal const int MAX_TARGET_RATE_BPS                     = 80000;

        /* LBRR thresholds */
        internal const int LBRR_NB_MIN_RATE_BPS                    = 12000;
        internal const int LBRR_MB_MIN_RATE_BPS                    = 14000;
        internal const int LBRR_WB_MIN_RATE_BPS                    = 16000;

        /* DTX settings */
        internal const int NB_SPEECH_FRAMES_BEFORE_DTX             = 10;      /* eq 200 ms */
        internal const int MAX_CONSECUTIVE_DTX                     = 20;      /* eq 400 ms */
        internal const float DTX_ACTIVITY_THRESHOLD                  = 0.1f;

        /* VAD decision */
        internal const int VAD_NO_DECISION                         = -1;
        internal const int VAD_NO_ACTIVITY                         = 0;
        internal const int VAD_ACTIVITY                            = 1;

        /* Maximum sampling frequency */
        internal const int MAX_FS_KHZ                              = 16;
        internal const int MAX_API_FS_KHZ                          = 48;

        /* Signal types */
        internal const int TYPE_NO_VOICE_ACTIVITY                  = 0;
        internal const int TYPE_UNVOICED                           = 1;
        internal const int TYPE_VOICED                             = 2;

        /* Conditional coding types */
        internal const int CODE_INDEPENDENTLY                      = 0;
        internal const int CODE_INDEPENDENTLY_NO_LTP_SCALING       = 1;
        internal const int CODE_CONDITIONALLY                      = 2;

        /* Settings for stereo processing */
        internal const int STEREO_QUANT_TAB_SIZE                   = 16;
        internal const int STEREO_QUANT_SUB_STEPS                  = 5;
        internal const int STEREO_INTERP_LEN_MS                    = 8;       /* must be even */
        internal const float STEREO_RATIO_SMOOTH_COEF              = 0.01f;    /* smoothing coef for signal norms and stereo width */

        /* Range of pitch lag estimates */
        internal const int PITCH_EST_MIN_LAG_MS                    = 2;       /* 2 ms -> 500 Hz */
        internal const int PITCH_EST_MAX_LAG_MS                    = 18;      /* 18 ms -> 56 Hz */

        /* Maximum number of subframes */
        internal const int MAX_NB_SUBFR                            = 4;

        /* Number of samples per frame */
        internal const int LTP_MEM_LENGTH_MS                       = 20;
        internal const int SUB_FRAME_LENGTH_MS                     = 5;
        internal const int MAX_SUB_FRAME_LENGTH                    = ( SUB_FRAME_LENGTH_MS * MAX_FS_KHZ );
        internal const int MAX_FRAME_LENGTH_MS                     = ( SUB_FRAME_LENGTH_MS * MAX_NB_SUBFR );
        internal const int MAX_FRAME_LENGTH                        = ( MAX_FRAME_LENGTH_MS * MAX_FS_KHZ );

        /* Milliseconds of lookahead for pitch analysis */
        internal const int LA_PITCH_MS                             = 2;
        internal const int LA_PITCH_MAX                            = ( LA_PITCH_MS * MAX_FS_KHZ );

        /* Order of LPC used in find pitch */
        internal const int MAX_FIND_PITCH_LPC_ORDER                = 16;

        /* Length of LPC window used in find pitch */
        internal const int FIND_PITCH_LPC_WIN_MS                   = ( 20 + (LA_PITCH_MS << 1) );
        internal const int FIND_PITCH_LPC_WIN_MS_2_SF              = ( 10 + (LA_PITCH_MS << 1) );
        internal const int FIND_PITCH_LPC_WIN_MAX                  = ( FIND_PITCH_LPC_WIN_MS * MAX_FS_KHZ );

        /* Milliseconds of lookahead for noise shape analysis */
        internal const int LA_SHAPE_MS                             = 5;
        internal const int LA_SHAPE_MAX                            = ( LA_SHAPE_MS * MAX_FS_KHZ );

        /* Maximum length of LPC window used in noise shape analysis */
        internal const int SHAPE_LPC_WIN_MAX                       = ( 15 * MAX_FS_KHZ );

        /* dB level of lowest gain quantization level */
        internal const int MIN_QGAIN_DB                            = 2;
        /* dB level of highest gain quantization level */
        internal const int MAX_QGAIN_DB                            = 88;
        /* Number of gain quantization levels */
        internal const int N_LEVELS_QGAIN                          = 64;
        /* Max increase in gain quantization index */
        internal const int MAX_DELTA_GAIN_QUANT                    = 36;
        /* Max decrease in gain quantization index */
        internal const int MIN_DELTA_GAIN_QUANT                    = -4;

        /* Quantization offsets (multiples of 4) */
        internal const int OFFSET_VL_Q10                           = 32;
        internal const int OFFSET_VH_Q10                           = 100;
        internal const int OFFSET_UVL_Q10                          = 100;
        internal const int OFFSET_UVH_Q10                          = 240;

        internal const int QUANT_LEVEL_ADJUST_Q10                  = 80;

        /* Maximum numbers of iterations used to stabilize an LPC vector */
        internal const int MAX_LPC_STABILIZE_ITERATIONS            = 16;
        internal const float MAX_PREDICTION_POWER_GAIN             = 1e4f;
        internal const float MAX_PREDICTION_POWER_GAIN_AFTER_RESET = 1e2f;

        internal const int MAX_LPC_ORDER                           = 16;
        internal const int MIN_LPC_ORDER                           = 10;

        /* Find Pred Coef defines */
        internal const int LTP_ORDER                               = 5;

        /* LTP quantization settings */
        internal const int NB_LTP_CBKS                             = 3;

        /* Flag to use harmonic noise shaping */
        internal const int USE_HARM_SHAPING                        = 1;

        /* Max LPC order of noise shaping filters */
        internal const int MAX_SHAPE_LPC_ORDER                     = 24;

        internal const int HARM_SHAPE_FIR_TAPS                     = 3;

        /* Maximum number of delayed decision states */
        internal const int MAX_DEL_DEC_STATES                      = 4;

        internal const int LTP_BUF_LENGTH                          = 512;
        internal const int LTP_MASK                                = ( LTP_BUF_LENGTH - 1 );

        internal const int DECISION_DELAY                          = 40;

        /* Number of subframes for excitation entropy coding */
        internal const int SHELL_CODEC_FRAME_LENGTH                = 16;
        internal const int LOG2_SHELL_CODEC_FRAME_LENGTH           = 4;
        internal const int MAX_NB_SHELL_BLOCKS                     = ( MAX_FRAME_LENGTH / SHELL_CODEC_FRAME_LENGTH );

        /* Number of rate levels, for entropy coding of excitation */
        internal const int N_RATE_LEVELS                           = 10;

        /* Maximum sum of pulses per shell coding frame */
        internal const int SILK_MAX_PULSES                         = 16;

        internal const int MAX_MATRIX_SIZE                         = MAX_LPC_ORDER; /* Max of LPC Order and LTP order */

        internal const int NSQ_LPC_BUF_LENGTH                      = MAX_LPC_ORDER;

        /***************************/
        /* Voice activity detector */
        /***************************/
        internal const int VAD_N_BANDS                             = 4;

        internal const int VAD_INTERNAL_SUBFRAMES_LOG2             = 2;
        internal const int VAD_INTERNAL_SUBFRAMES                  = ( 1 << VAD_INTERNAL_SUBFRAMES_LOG2 );

        internal const int VAD_NOISE_LEVEL_SMOOTH_COEF_Q16         = 1024;    /* Must be <  4096 */
        internal const int VAD_NOISE_LEVELS_BIAS                   = 50;

        /* Sigmoid settings */
        internal const int VAD_NEGATIVE_OFFSET_Q5                  = 128;     /* sigmoid is 0 at -128 */
        internal const int VAD_SNR_FACTOR_Q16                      = 45000;

        /* smoothing for SNR measurement */
        internal const int VAD_SNR_SMOOTH_COEF_Q18                 = 4096;

        /* Size of the piecewise linear cosine approximation table for the LSFs */
        internal const int LSF_COS_TAB_SZ_FIX                      = 128;

        /******************/
        /* NLSF quantizer */
        /******************/
        internal const int NLSF_W_Q                                = 2;
        internal const int NLSF_VQ_MAX_VECTORS                     = 32;
        internal const int NLSF_QUANT_MAX_AMPLITUDE                = 4;
        internal const int NLSF_QUANT_MAX_AMPLITUDE_EXT            = 10;
        internal const float NLSF_QUANT_LEVEL_ADJ                  = 0.1f;
        internal const int NLSF_QUANT_DEL_DEC_STATES_LOG2          = 2;
        internal const int NLSF_QUANT_DEL_DEC_STATES               = ( 1 << NLSF_QUANT_DEL_DEC_STATES_LOG2 );

        /* Transition filtering for mode switching */
        internal const int TRANSITION_TIME_MS                      = 5120;    /* 5120 = 64 * FRAME_LENGTH_MS * ( TRANSITION_INT_NUM - 1 ) = 64*(20*4)*/
        internal const int TRANSITION_NB                           = 3;       /* Hardcoded in tables */
        internal const int TRANSITION_NA                           = 2;       /* Hardcoded in tables */
        internal const int TRANSITION_INT_NUM                      = 5;       /* Hardcoded in tables */
        internal const int TRANSITION_FRAMES                       = ( TRANSITION_TIME_MS / MAX_FRAME_LENGTH_MS );
        internal const int TRANSITION_INT_STEPS                    = ( TRANSITION_FRAMES / (TRANSITION_INT_NUM - 1) );

        /* BWE factors to apply after packet loss */
        internal const int BWE_AFTER_LOSS_Q16                      = 63570;

        /* Defines for CN generation */
        internal const int CNG_BUF_MASK_MAX                        = 255;     /* 2^floor(log2(MAX_FRAME_LENGTH))-1    */
        internal const int CNG_GAIN_SMTH_Q16                       = 4634;    /* 0.25^(1/4)                           */
        internal const int CNG_GAIN_SMTH_THRESHOLD_Q16             = 46396;   /* -3 dB                                */
        internal const int CNG_NLSF_SMTH_Q16                       = 16348;   /* 0.25 */
    }
}
