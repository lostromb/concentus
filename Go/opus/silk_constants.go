package opus

var SilkConstants = struct {
	ENCODER_NUM_CHANNELS                  int
	DECODER_NUM_CHANNELS                  int
	MAX_FRAMES_PER_PACKET                 int
	MIN_TARGET_RATE_BPS                   int
	MAX_TARGET_RATE_BPS                   int
	TARGET_RATE_TAB_SZ                    int
	LBRR_NB_MIN_RATE_BPS                  int
	LBRR_MB_MIN_RATE_BPS                  int
	LBRR_WB_MIN_RATE_BPS                  int
	NB_SPEECH_FRAMES_BEFORE_DTX           int
	MAX_CONSECUTIVE_DTX                   int
	MAX_FS_KHZ                            int
	MAX_API_FS_KHZ                        int
	TYPE_NO_VOICE_ACTIVITY                int
	TYPE_UNVOICED                         int
	TYPE_VOICED                           int
	CODE_INDEPENDENTLY                    int
	CODE_INDEPENDENTLY_NO_LTP_SCALING     int
	CODE_CONDITIONALLY                    int
	STEREO_QUANT_TAB_SIZE                 int
	STEREO_QUANT_SUB_STEPS                int
	STEREO_INTERP_LEN_MS                  int
	STEREO_RATIO_SMOOTH_COEF              float32
	PITCH_EST_MIN_LAG_MS                  int
	PITCH_EST_MAX_LAG_MS                  int
	MAX_NB_SUBFR                          int
	LTP_MEM_LENGTH_MS                     int
	SUB_FRAME_LENGTH_MS                   int
	MAX_SUB_FRAME_LENGTH                  int
	MAX_FRAME_LENGTH_MS                   int
	MAX_FRAME_LENGTH                      int
	LA_PITCH_MS                           int
	LA_PITCH_MAX                          int
	MAX_FIND_PITCH_LPC_ORDER              int
	FIND_PITCH_LPC_WIN_MS                 int
	FIND_PITCH_LPC_WIN_MS_2_SF            int
	FIND_PITCH_LPC_WIN_MAX                int
	LA_SHAPE_MS                           int
	LA_SHAPE_MAX                          int
	SHAPE_LPC_WIN_MAX                     int
	MIN_QGAIN_DB                          int
	MAX_QGAIN_DB                          int
	N_LEVELS_QGAIN                        int
	MAX_DELTA_GAIN_QUANT                  int
	MIN_DELTA_GAIN_QUANT                  int
	OFFSET_VL_Q10                         int16
	OFFSET_VH_Q10                         int16
	OFFSET_UVL_Q10                        int16
	OFFSET_UVH_Q10                        int16
	QUANT_LEVEL_ADJUST_Q10                int
	MAX_LPC_STABILIZE_ITERATIONS          int
	MAX_PREDICTION_POWER_GAIN             float32
	MAX_PREDICTION_POWER_GAIN_AFTER_RESET float32
	SILK_MAX_ORDER_LPC                    int
	MAX_LPC_ORDER                         int
	MIN_LPC_ORDER                         int
	LTP_ORDER                             int
	NB_LTP_CBKS                           int
	USE_HARM_SHAPING                      int
	MAX_SHAPE_LPC_ORDER                   int
	HARM_SHAPE_FIR_TAPS                   int
	MAX_DEL_DEC_STATES                    int
	LTP_BUF_LENGTH                        int
	LTP_MASK                              int
	DECISION_DELAY                        int
	DECISION_DELAY_MASK                   int
	SHELL_CODEC_FRAME_LENGTH              int
	LOG2_SHELL_CODEC_FRAME_LENGTH         int
	MAX_NB_SHELL_BLOCKS                   int
	N_RATE_LEVELS                         int
	SILK_MAX_PULSES                       int
	MAX_MATRIX_SIZE                       int
	NSQ_LPC_BUF_LENGTH                    int
	VAD_N_BANDS                           int
	VAD_INTERNAL_SUBFRAMES_LOG2           int
	VAD_INTERNAL_SUBFRAMES                int
	VAD_NOISE_LEVEL_SMOOTH_COEF_Q16       int
	VAD_NOISE_LEVELS_BIAS                 int
	VAD_NEGATIVE_OFFSET_Q5                int
	VAD_SNR_FACTOR_Q16                    int
	VAD_SNR_SMOOTH_COEF_Q18               int
	LSF_COS_TAB_SZ                        int
	NLSF_W_Q                              int
	NLSF_VQ_MAX_VECTORS                   int
	NLSF_VQ_MAX_SURVIVORS                 int
	NLSF_QUANT_MAX_AMPLITUDE              int
	NLSF_QUANT_MAX_AMPLITUDE_EXT          int
	NLSF_QUANT_LEVEL_ADJ                  float32
	NLSF_QUANT_DEL_DEC_STATES_LOG2        int
	NLSF_QUANT_DEL_DEC_STATES             int
	TRANSITION_TIME_MS                    int
	TRANSITION_NB                         int
	TRANSITION_NA                         int
	TRANSITION_INT_NUM                    int
	TRANSITION_FRAMES                     int
	TRANSITION_INT_STEPS                  int
	BWE_AFTER_LOSS_Q16                    int
	CNG_BUF_MASK_MAX                      int
	CNG_GAIN_SMTH_Q16                     int
	CNG_NLSF_SMTH_Q16                     int
	PE_MAX_FS_KHZ                         int
	PE_MAX_NB_SUBFR                       int
	PE_SUBFR_LENGTH_MS                    int
	PE_LTP_MEM_LENGTH_MS                  int
	PE_MAX_FRAME_LENGTH_MS                int
	PE_MAX_FRAME_LENGTH                   int
	PE_MAX_FRAME_LENGTH_ST_1              int
	PE_MAX_FRAME_LENGTH_ST_2              int
	PE_MAX_LAG_MS                         int
	PE_MIN_LAG_MS                         int
	PE_MAX_LAG                            int
	PE_MIN_LAG                            int
	PE_D_SRCH_LENGTH                      int
	PE_NB_STAGE3_LAGS                     int
	PE_NB_CBKS_STAGE2                     int
	PE_NB_CBKS_STAGE2_EXT                 int
	PE_NB_CBKS_STAGE3_MAX                 int
	PE_NB_CBKS_STAGE3_MID                 int
	PE_NB_CBKS_STAGE3_MIN                 int
	PE_NB_CBKS_STAGE3_10MS                int
	PE_NB_CBKS_STAGE2_10MS                int
	PE_SHORTLAG_BIAS                      float32
	PE_PREVLAG_BIAS                       float32
	PE_FLATCONTOUR_BIAS                   float32
	SILK_PE_MIN_COMPLEX                   int
	SILK_PE_MID_COMPLEX                   int
	SILK_PE_MAX_COMPLEX                   int
	BWE_COEF                              float32
	V_PITCH_GAIN_START_MIN_Q14            int
	V_PITCH_GAIN_START_MAX_Q14            int
	MAX_PITCH_LAG_MS                      int
	RAND_BUF_SIZE                         int
	RAND_BUF_MASK                         int
	LOG2_INV_LPC_GAIN_HIGH_THRES          int
	LOG2_INV_LPC_GAIN_LOW_THRES           int
	PITCH_DRIFT_FAC_Q16                   int
	SILK_RESAMPLER_MAX_FIR_ORDER          int
	SILK_RESAMPLER_MAX_IIR_ORDER          int
	RESAMPLER_DOWN_ORDER_FIR0             int
	RESAMPLER_DOWN_ORDER_FIR1             int
	RESAMPLER_DOWN_ORDER_FIR2             int
	RESAMPLER_ORDER_FIR_12                int
	RESAMPLER_MAX_BATCH_SIZE_MS           int
	RESAMPLER_MAX_FS_KHZ                  int
	RESAMPLER_MAX_BATCH_SIZE_IN           int
	SILK_MAX_FRAMES_PER_PACKET            int
}{
	ENCODER_NUM_CHANNELS:                  2,
	DECODER_NUM_CHANNELS:                  2,
	MAX_FRAMES_PER_PACKET:                 3,
	MIN_TARGET_RATE_BPS:                   5000,
	MAX_TARGET_RATE_BPS:                   80000,
	TARGET_RATE_TAB_SZ:                    8,
	LBRR_NB_MIN_RATE_BPS:                  12000,
	LBRR_MB_MIN_RATE_BPS:                  14000,
	LBRR_WB_MIN_RATE_BPS:                  16000,
	NB_SPEECH_FRAMES_BEFORE_DTX:           10,
	MAX_CONSECUTIVE_DTX:                   20,
	MAX_FS_KHZ:                            16,
	MAX_API_FS_KHZ:                        48,
	TYPE_NO_VOICE_ACTIVITY:                0,
	TYPE_UNVOICED:                         1,
	TYPE_VOICED:                           2,
	CODE_INDEPENDENTLY:                    0,
	CODE_INDEPENDENTLY_NO_LTP_SCALING:     1,
	CODE_CONDITIONALLY:                    2,
	STEREO_QUANT_TAB_SIZE:                 16,
	STEREO_QUANT_SUB_STEPS:                5,
	STEREO_INTERP_LEN_MS:                  8,
	STEREO_RATIO_SMOOTH_COEF:              0.01,
	PITCH_EST_MIN_LAG_MS:                  2,
	PITCH_EST_MAX_LAG_MS:                  18,
	MAX_NB_SUBFR:                          4,
	LTP_MEM_LENGTH_MS:                     20,
	SUB_FRAME_LENGTH_MS:                   5,
	MAX_SUB_FRAME_LENGTH:                  5 * 16,
	MAX_FRAME_LENGTH_MS:                   5 * 4,
	MAX_FRAME_LENGTH:                      (5 * 4) * 16,
	LA_PITCH_MS:                           2,
	LA_PITCH_MAX:                          2 * 16,
	MAX_FIND_PITCH_LPC_ORDER:              16,
	FIND_PITCH_LPC_WIN_MS:                 20 + (2 << 1),
	FIND_PITCH_LPC_WIN_MS_2_SF:            10 + (2 << 1),
	FIND_PITCH_LPC_WIN_MAX:                (20 + (2 << 1)) * 16,
	LA_SHAPE_MS:                           5,
	LA_SHAPE_MAX:                          5 * 16,
	SHAPE_LPC_WIN_MAX:                     15 * 16,
	MIN_QGAIN_DB:                          2,
	MAX_QGAIN_DB:                          88,
	N_LEVELS_QGAIN:                        64,
	MAX_DELTA_GAIN_QUANT:                  36,
	MIN_DELTA_GAIN_QUANT:                  -4,
	OFFSET_VL_Q10:                         32,
	OFFSET_VH_Q10:                         100,
	OFFSET_UVL_Q10:                        100,
	OFFSET_UVH_Q10:                        240,
	QUANT_LEVEL_ADJUST_Q10:                80,
	MAX_LPC_STABILIZE_ITERATIONS:          16,
	MAX_PREDICTION_POWER_GAIN:             1e4,
	MAX_PREDICTION_POWER_GAIN_AFTER_RESET: 1e2,
	SILK_MAX_ORDER_LPC:                    16,
	MAX_LPC_ORDER:                         16,
	MIN_LPC_ORDER:                         10,
	LTP_ORDER:                             5,
	NB_LTP_CBKS:                           3,
	USE_HARM_SHAPING:                      1,
	MAX_SHAPE_LPC_ORDER:                   16,
	HARM_SHAPE_FIR_TAPS:                   3,
	MAX_DEL_DEC_STATES:                    4,
	LTP_BUF_LENGTH:                        512,
	LTP_MASK:                              512 - 1,
	DECISION_DELAY:                        32,
	DECISION_DELAY_MASK:                   32 - 1,
	SHELL_CODEC_FRAME_LENGTH:              16,
	LOG2_SHELL_CODEC_FRAME_LENGTH:         4,
	MAX_NB_SHELL_BLOCKS:                   ((5 * 4) * 16) / 16,
	N_RATE_LEVELS:                         10,
	SILK_MAX_PULSES:                       16,
	MAX_MATRIX_SIZE:                       16,
	NSQ_LPC_BUF_LENGTH:                    32,
	VAD_N_BANDS:                           4,
	VAD_INTERNAL_SUBFRAMES_LOG2:           2,
	VAD_INTERNAL_SUBFRAMES:                1 << 2,
	VAD_NOISE_LEVEL_SMOOTH_COEF_Q16:       1024,
	VAD_NOISE_LEVELS_BIAS:                 50,
	VAD_NEGATIVE_OFFSET_Q5:                128,
	VAD_SNR_FACTOR_Q16:                    45000,
	VAD_SNR_SMOOTH_COEF_Q18:               4096,
	LSF_COS_TAB_SZ:                        128,
	NLSF_W_Q:                              2,
	NLSF_VQ_MAX_VECTORS:                   32,
	NLSF_VQ_MAX_SURVIVORS:                 32,
	NLSF_QUANT_MAX_AMPLITUDE:              4,
	NLSF_QUANT_MAX_AMPLITUDE_EXT:          10,
	NLSF_QUANT_LEVEL_ADJ:                  0.1,
	NLSF_QUANT_DEL_DEC_STATES_LOG2:        2,
	NLSF_QUANT_DEL_DEC_STATES:             1 << 2,
	TRANSITION_TIME_MS:                    5120,
	TRANSITION_NB:                         3,
	TRANSITION_NA:                         2,
	TRANSITION_INT_NUM:                    5,
	TRANSITION_FRAMES:                     5120 / (5 * 4),
	TRANSITION_INT_STEPS:                  (5120 / (5 * 4)) / (5 - 1),
	BWE_AFTER_LOSS_Q16:                    63570,
	CNG_BUF_MASK_MAX:                      255,
	CNG_GAIN_SMTH_Q16:                     4634,
	CNG_NLSF_SMTH_Q16:                     16348,
	PE_MAX_FS_KHZ:                         16,
	PE_MAX_NB_SUBFR:                       4,
	PE_SUBFR_LENGTH_MS:                    5,
	PE_LTP_MEM_LENGTH_MS:                  4 * 5,
	PE_MAX_FRAME_LENGTH_MS:                (4 * 5) + 4*5,
	PE_MAX_FRAME_LENGTH:                   ((4 * 5) + 4*5) * 16,
	PE_MAX_FRAME_LENGTH_ST_1:              (((4 * 5) + 4*5) * 16) >> 2,
	PE_MAX_FRAME_LENGTH_ST_2:              (((4 * 5) + 4*5) * 16) >> 1,
	PE_MAX_LAG_MS:                         18,
	PE_MIN_LAG_MS:                         2,
	PE_MAX_LAG:                            18 * 16,
	PE_MIN_LAG:                            2 * 16,
	PE_D_SRCH_LENGTH:                      24,
	PE_NB_STAGE3_LAGS:                     5,
	PE_NB_CBKS_STAGE2:                     3,
	PE_NB_CBKS_STAGE2_EXT:                 11,
	PE_NB_CBKS_STAGE3_MAX:                 34,
	PE_NB_CBKS_STAGE3_MID:                 24,
	PE_NB_CBKS_STAGE3_MIN:                 16,
	PE_NB_CBKS_STAGE3_10MS:                12,
	PE_NB_CBKS_STAGE2_10MS:                3,
	PE_SHORTLAG_BIAS:                      0.2,
	PE_PREVLAG_BIAS:                       0.2,
	PE_FLATCONTOUR_BIAS:                   0.05,
	SILK_PE_MIN_COMPLEX:                   0,
	SILK_PE_MID_COMPLEX:                   1,
	SILK_PE_MAX_COMPLEX:                   2,
	BWE_COEF:                              0.99,
	V_PITCH_GAIN_START_MIN_Q14:            11469,
	V_PITCH_GAIN_START_MAX_Q14:            15565,
	MAX_PITCH_LAG_MS:                      18,
	RAND_BUF_SIZE:                         128,
	RAND_BUF_MASK:                         128 - 1,
	LOG2_INV_LPC_GAIN_HIGH_THRES:          3,
	LOG2_INV_LPC_GAIN_LOW_THRES:           8,
	PITCH_DRIFT_FAC_Q16:                   655,
	SILK_RESAMPLER_MAX_FIR_ORDER:          36,
	SILK_RESAMPLER_MAX_IIR_ORDER:          6,
	RESAMPLER_DOWN_ORDER_FIR0:             18,
	RESAMPLER_DOWN_ORDER_FIR1:             24,
	RESAMPLER_DOWN_ORDER_FIR2:             36,
	RESAMPLER_ORDER_FIR_12:                8,
	RESAMPLER_MAX_BATCH_SIZE_MS:           10,
	RESAMPLER_MAX_FS_KHZ:                  48,
	RESAMPLER_MAX_BATCH_SIZE_IN:           10 * 48,
	SILK_MAX_FRAMES_PER_PACKET:            3,
}

const (
	ENCODER_NUM_CHANNELS                          = 2
	DECODER_NUM_CHANNELS                          = 2
	MAX_FRAMES_PER_PACKET                         = 3
	MIN_TARGET_RATE_BPS                           = 5000
	MAX_TARGET_RATE_BPS                           = 80000
	TARGET_RATE_TAB_SZ                            = 8
	LBRR_NB_MIN_RATE_BPS                          = 12000
	LBRR_MB_MIN_RATE_BPS                          = 14000
	LBRR_WB_MIN_RATE_BPS                          = 16000
	NB_SPEECH_FRAMES_BEFORE_DTX                   = 10
	MAX_CONSECUTIVE_DTX                           = 20
	MAX_FS_KHZ                                    = 16
	MAX_API_FS_KHZ                                = 48
	TYPE_NO_VOICE_ACTIVITY                        = 0
	TYPE_UNVOICED                                 = 1
	TYPE_VOICED                                   = 2
	CODE_INDEPENDENTLY                            = 0
	CODE_INDEPENDENTLY_NO_LTP_SCALING             = 1
	CODE_CONDITIONALLY                            = 2
	STEREO_QUANT_TAB_SIZE                         = 16
	STEREO_QUANT_SUB_STEPS                        = 5
	STEREO_INTERP_LEN_MS                          = 8
	STEREO_RATIO_SMOOTH_COEF                      = 0.01
	PITCH_EST_MIN_LAG_MS                          = 2
	PITCH_EST_MAX_LAG_MS                          = 18
	MAX_NB_SUBFR                                  = 4
	LTP_MEM_LENGTH_MS                             = 20
	SUB_FRAME_LENGTH_MS                           = 5
	MAX_SUB_FRAME_LENGTH                          = SUB_FRAME_LENGTH_MS * MAX_FS_KHZ
	MAX_FRAME_LENGTH_MS                           = SUB_FRAME_LENGTH_MS * MAX_NB_SUBFR
	MAX_FRAME_LENGTH                              = MAX_FRAME_LENGTH_MS * MAX_FS_KHZ
	LA_PITCH_MS                                   = 2
	LA_PITCH_MAX                                  = LA_PITCH_MS * MAX_FS_KHZ
	MAX_FIND_PITCH_LPC_ORDER                      = 16
	FIND_PITCH_LPC_WIN_MS                         = 20 + (LA_PITCH_MS << 1)
	FIND_PITCH_LPC_WIN_MS_2_SF                    = 10 + (LA_PITCH_MS << 1)
	FIND_PITCH_LPC_WIN_MAX                        = FIND_PITCH_LPC_WIN_MS * MAX_FS_KHZ
	LA_SHAPE_MS                                   = 5
	LA_SHAPE_MAX                                  = LA_SHAPE_MS * MAX_FS_KHZ
	SHAPE_LPC_WIN_MAX                             = 15 * MAX_FS_KHZ
	MIN_QGAIN_DB                                  = 2
	MAX_QGAIN_DB                                  = 88
	N_LEVELS_QGAIN                                = 64
	MAX_DELTA_GAIN_QUANT                          = 36
	MIN_DELTA_GAIN_QUANT                          = -4
	OFFSET_VL_Q10                                 = 32
	OFFSET_VH_Q10                                 = 100
	OFFSET_UVL_Q10                                = 100
	OFFSET_UVH_Q10                                = 240
	QUANT_LEVEL_ADJUST_Q10                        = 80
	MAX_LPC_STABILIZE_ITERATIONS                  = 16
	MAX_PREDICTION_POWER_GAIN                     = 1e4
	MAX_PREDICTION_POWER_GAIN_AFTER_RESET         = 1e2
	SILK_MAX_ORDER_LPC                            = 16
	MAX_LPC_ORDER                                 = 16
	MIN_LPC_ORDER                                 = 10
	LTP_ORDER                                     = 5
	NB_LTP_CBKS                                   = 3
	USE_HARM_SHAPING                              = 1
	MAX_SHAPE_LPC_ORDER                           = 16
	HARM_SHAPE_FIR_TAPS                           = 3
	MAX_DEL_DEC_STATES                            = 4
	LTP_BUF_LENGTH                                = 512
	LTP_MASK                                      = LTP_BUF_LENGTH - 1
	DECISION_DELAY                                = 32
	DECISION_DELAY_MASK                           = DECISION_DELAY - 1
	SHELL_CODEC_FRAME_LENGTH                      = 16
	LOG2_SHELL_CODEC_FRAME_LENGTH                 = 4
	MAX_NB_SHELL_BLOCKS                           = MAX_FRAME_LENGTH / SHELL_CODEC_FRAME_LENGTH
	N_RATE_LEVELS                                 = 10
	SILK_MAX_PULSES                               = 16
	MAX_MATRIX_SIZE                               = MAX_LPC_ORDER
	NSQ_LPC_BUF_LENGTH                            = MAX_LPC_ORDER
	VAD_N_BANDS                                   = 4
	VAD_INTERNAL_SUBFRAMES_LOG2                   = 2
	VAD_INTERNAL_SUBFRAMES                        = 1 << VAD_INTERNAL_SUBFRAMES_LOG2
	VAD_NOISE_LEVEL_SMOOTH_COEF_Q16               = 1024
	VAD_NOISE_LEVELS_BIAS                         = 50
	VAD_NEGATIVE_OFFSET_Q5                        = 128
	VAD_SNR_FACTOR_Q16                            = 45000
	VAD_SNR_SMOOTH_COEF_Q18                       = 4096
	LSF_COS_TAB_SZ                                = 128
	NLSF_W_Q                                      = 2
	NLSF_VQ_MAX_VECTORS                           = 32
	NLSF_VQ_MAX_SURVIVORS                         = 32
	NLSF_QUANT_MAX_AMPLITUDE                      = 4
	NLSF_QUANT_MAX_AMPLITUDE_EXT                  = 10
	NLSF_QUANT_LEVEL_ADJ                  float32 = 0.1
	NLSF_QUANT_DEL_DEC_STATES_LOG2                = 2
	NLSF_QUANT_DEL_DEC_STATES                     = 1 << NLSF_QUANT_DEL_DEC_STATES_LOG2
	TRANSITION_TIME_MS                            = 5120
	TRANSITION_NB                                 = 3
	TRANSITION_NA                                 = 2
	TRANSITION_INT_NUM                            = 5
	TRANSITION_FRAMES                             = TRANSITION_TIME_MS / MAX_FRAME_LENGTH_MS
	TRANSITION_INT_STEPS                          = TRANSITION_FRAMES / (TRANSITION_INT_NUM - 1)
	BWE_AFTER_LOSS_Q16                            = 63570
	CNG_BUF_MASK_MAX                              = 255
	CNG_GAIN_SMTH_Q16                             = 4634
	CNG_NLSF_SMTH_Q16                             = 16348
	PE_MAX_FS_KHZ                                 = 16
	PE_MAX_NB_SUBFR                               = 4
	PE_SUBFR_LENGTH_MS                            = 5
	PE_LTP_MEM_LENGTH_MS                          = 4 * PE_SUBFR_LENGTH_MS
	PE_MAX_FRAME_LENGTH_MS                        = PE_LTP_MEM_LENGTH_MS + PE_MAX_NB_SUBFR*PE_SUBFR_LENGTH_MS
	PE_MAX_FRAME_LENGTH                           = PE_MAX_FRAME_LENGTH_MS * PE_MAX_FS_KHZ
	PE_MAX_FRAME_LENGTH_ST_1                      = PE_MAX_FRAME_LENGTH >> 2
	PE_MAX_FRAME_LENGTH_ST_2                      = PE_MAX_FRAME_LENGTH >> 1
	PE_MAX_LAG_MS                                 = 18
	PE_MIN_LAG_MS                                 = 2
	PE_MAX_LAG                                    = PE_MAX_LAG_MS * PE_MAX_FS_KHZ
	PE_MIN_LAG                                    = PE_MIN_LAG_MS * PE_MAX_FS_KHZ
	PE_D_SRCH_LENGTH                              = 24
	PE_NB_STAGE3_LAGS                             = 5
	PE_NB_CBKS_STAGE2                             = 3
	PE_NB_CBKS_STAGE2_EXT                         = 11
	PE_NB_CBKS_STAGE3_MAX                         = 34
	PE_NB_CBKS_STAGE3_MID                         = 24
	PE_NB_CBKS_STAGE3_MIN                         = 16
	PE_NB_CBKS_STAGE3_10MS                        = 12
	PE_NB_CBKS_STAGE2_10MS                        = 3
	PE_SHORTLAG_BIAS                              = 0.2
	PE_PREVLAG_BIAS                               = 0.2
	PE_FLATCONTOUR_BIAS                           = 0.05
	SILK_PE_MIN_COMPLEX                           = 0
	SILK_PE_MID_COMPLEX                           = 1
	SILK_PE_MAX_COMPLEX                           = 2
	BWE_COEF                                      = 0.99
	V_PITCH_GAIN_START_MIN_Q14                    = 11469
	V_PITCH_GAIN_START_MAX_Q14                    = 15565
	MAX_PITCH_LAG_MS                              = 18
	RAND_BUF_SIZE                                 = 128
	RAND_BUF_MASK                                 = RAND_BUF_SIZE - 1
	LOG2_INV_LPC_GAIN_HIGH_THRES                  = 3
	LOG2_INV_LPC_GAIN_LOW_THRES                   = 8
	PITCH_DRIFT_FAC_Q16                           = 655
	SILK_RESAMPLER_MAX_FIR_ORDER                  = 36
	SILK_RESAMPLER_MAX_IIR_ORDER                  = 6
	RESAMPLER_DOWN_ORDER_FIR0                     = 18
	RESAMPLER_DOWN_ORDER_FIR1                     = 24
	RESAMPLER_DOWN_ORDER_FIR2                     = 36
	RESAMPLER_ORDER_FIR_12                        = 8
	RESAMPLER_MAX_BATCH_SIZE_MS                   = 10
	RESAMPLER_MAX_FS_KHZ                          = 48
	RESAMPLER_MAX_BATCH_SIZE_IN                   = RESAMPLER_MAX_BATCH_SIZE_MS * RESAMPLER_MAX_FS_KHZ
	SILK_MAX_FRAMES_PER_PACKET                    = 3
)
