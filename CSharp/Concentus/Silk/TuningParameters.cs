using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    public class TuningParameters
    {
        /* Decay time for EntropyCoder.BITREServoir */
        public const int BITRESERVOIR_DECAY_TIME_MS = 500;

        /*******************/
        /* Pitch estimator */
        /*******************/

        /* Level of noise floor for whitening filter LPC analysis in pitch analysis */
        public const float FIND_PITCH_WHITE_NOISE_FRACTION = 1e-3f;

        /* Bandwidth expansion for whitening filter in pitch analysis */
        public const float FIND_PITCH_BANDWIDTH_EXPANSION = 0.99f;

        /*********************/
        /* Linear prediction */
        /*********************/

        /* LPC analysis regularization */
        public const float FIND_LPC_COND_FAC = 1e-5f;

        /* LTP analysis defines */
        public const float FIND_LTP_COND_FAC = 1e-5f;
        public const float LTP_DAMPING = 0.05f;
        public const float LTP_SMOOTHING = 0.1f;

        /* LTP quantization settings */
        public const float MU_LTP_QUANT_NB = 0.03f;
        public const float MU_LTP_QUANT_MB = 0.025f;
        public const float MU_LTP_QUANT_WB = 0.02f;

        /* Max cumulative LTP gain */
        public const float MAX_SUM_LOG_GAIN_DB = 250.0f;

        /***********************/
        /* High pass filtering */
        /***********************/

        /* Smoothing parameters for low end of pitch frequency range estimation */
        public const float VARIABLE_HP_SMTH_COEF1 = 0.1f;
        public const float VARIABLE_HP_SMTH_COEF2 = 0.015f;
        public const float VARIABLE_HP_MAX_DELTA_FREQ = 0.4f;

        /* Min and max cut-off frequency values (-3 dB points) */
        public const int VARIABLE_HP_MIN_CUTOFF_HZ = 60;
        public const int VARIABLE_HP_MAX_CUTOFF_HZ = 100;

        /***********/
        /* Various */
        /***********/

        /* VAD threshold */
        public const float SPEECH_ACTIVITY_DTX_THRES = 0.05f;

        /* Speech Activity LBRR enable threshold */
        public const float LBRR_SPEECH_ACTIVITY_THRES = 0.3f;

        /*************************/
        /* Perceptual parameters */
        /*************************/

        /* reduction in coding SNR during low speech activity */
        public const float BG_SNR_DECR_dB = 2.0f;

        /* factor for reducing quantization noise during voiced speech */
        public const float HARM_SNR_INCR_dB = 2.0f;

        /* factor for reducing quantization noise for unvoiced sparse signals */
        public const float SPARSE_SNR_INCR_dB = 2.0f;

        /* threshold for sparseness measure above which to use lower quantization offset during unvoiced */
        public const float SPARSENESS_THRESHOLD_QNT_OFFSET = 0.75f;

        /* warping control */
        public const float WARPING_MULTIPLIER = 0.015f;

        /* fraction added to first autocorrelation value */
        public const float SHAPE_WHITE_NOISE_FRACTION = 5e-5f;

        /* noise shaping filter chirp factor */
        public const float BANDWIDTH_EXPANSION = 0.95f;

        /* difference between chirp factors for analysis and synthesis noise shaping filters at low bitrates */
        public const float LOW_RATE_BANDWIDTH_EXPANSION_DELTA = 0.01f;

        /* extra harmonic boosting (signal shaping) at low bitrates */
        public const float LOW_RATE_HARMONIC_BOOST = 0.1f;

        /* extra harmonic boosting (signal shaping) for noisy input signals */
        public const float LOW_INPUT_QUALITY_HARMONIC_BOOST = 0.1f;

        /* harmonic noise shaping */
        public const float HARMONIC_SHAPING = 0.3f;

        /* extra harmonic noise shaping for high bitrates or noisy input */
        public const float HIGH_RATE_OR_LOW_QUALITY_HARMONIC_SHAPING = 0.2f;

        /* parameter for shaping noise towards higher frequencies */
        public const float HP_NOISE_COEF = 0.25f;

        /* parameter for shaping noise even more towards higher frequencies during voiced speech */
        public const float HARM_HP_NOISE_COEF = 0.35f;

        /* parameter for applying a high-pass tilt to the input signal */
        public const float INPUT_TILT = 0.05f;

        /* parameter for extra high-pass tilt to the input signal at high rates */
        public const float HIGH_RATE_INPUT_TILT = 0.1f;

        /* parameter for reducing noise at the very low frequencies */
        public const float LOW_FREQ_SHAPING = 4.0f;

        /* less reduction of noise at the very low frequencies for signals with low SNR at low frequencies */
        public const float LOW_QUALITY_LOW_FREQ_SHAPING_DECR = 0.5f;

        /* subframe smoothing coefficient for HarmBoost, HarmShapeGain, Tilt (lower . more smoothing) */
        public const float SUBFR_SMTH_COEF = 0.4f;

        /* parameters defining the R/D tradeoff in the residual quantizer */
        public const float LAMBDA_OFFSET = 1.2f;
        public const float LAMBDA_SPEECH_ACT = -0.2f;
        public const float LAMBDA_DELAYED_DECISIONS = -0.05f;
        public const float LAMBDA_INPUT_QUALITY = -0.1f;
        public const float LAMBDA_CODING_QUALITY = -0.2f;
        public const float LAMBDA_QUANT_OFFSET = 0.8f;

        /* Compensation in bitrate calculations for 10 ms modes */
        public const int REDUCE_BITRATE_10_MS_BPS = 2200;

        /* Maximum time before allowing a bandwidth transition */
        public const int MAX_BANDWIDTH_SWITCH_DELAY_MS = 5000;
    }
}
