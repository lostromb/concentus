using System;
using System.Collections.Generic;
using System.Text;

namespace HellaUnsafe.Opus
{
    internal static class OpusDefines
    {
        internal const int OPUS_OK = 0;
        internal const int OPUS_BAD_ARG = -1;
        internal const int OPUS_BUFFER_TOO_SMALL = -2;
        internal const int OPUS_INTERNAL_ERROR = -3;
        internal const int OPUS_INVALID_PACKET = -4;
        internal const int OPUS_UNIMPLEMENTED = -5;
        internal const int OPUS_INVALID_STATE = -6;
        internal const int OPUS_ALLOC_FAIL = -7;

        /** These are the actual Encoder CTL ID numbers.
          * They should not be used directly by applications.
          * In general, SETs should be even and GETs should be odd.*/
        internal const int OPUS_SET_APPLICATION_REQUEST         = 4000;
        internal const int OPUS_GET_APPLICATION_REQUEST         = 4001;
        internal const int OPUS_SET_BITRATE_REQUEST             = 4002;
        internal const int OPUS_GET_BITRATE_REQUEST             = 4003;
        internal const int OPUS_SET_MAX_BANDWIDTH_REQUEST       = 4004;
        internal const int OPUS_GET_MAX_BANDWIDTH_REQUEST       = 4005;
        internal const int OPUS_SET_VBR_REQUEST                 = 4006;
        internal const int OPUS_GET_VBR_REQUEST                 = 4007;
        internal const int OPUS_SET_BANDWIDTH_REQUEST           = 4008;
        internal const int OPUS_GET_BANDWIDTH_REQUEST           = 4009;
        internal const int OPUS_SET_COMPLEXITY_REQUEST          = 4010;
        internal const int OPUS_GET_COMPLEXITY_REQUEST          = 4011;
        internal const int OPUS_SET_INBAND_FEC_REQUEST          = 4012;
        internal const int OPUS_GET_INBAND_FEC_REQUEST          = 4013;
        internal const int OPUS_SET_PACKET_LOSS_PERC_REQUEST    = 4014;
        internal const int OPUS_GET_PACKET_LOSS_PERC_REQUEST    = 4015;
        internal const int OPUS_SET_DTX_REQUEST                 = 4016;
        internal const int OPUS_GET_DTX_REQUEST                 = 4017;
        internal const int OPUS_SET_VBR_CONSTRAINT_REQUEST      = 4020;
        internal const int OPUS_GET_VBR_CONSTRAINT_REQUEST      = 4021;
        internal const int OPUS_SET_FORCE_CHANNELS_REQUEST      = 4022;
        internal const int OPUS_GET_FORCE_CHANNELS_REQUEST      = 4023;
        internal const int OPUS_SET_SIGNAL_REQUEST              = 4024;
        internal const int OPUS_GET_SIGNAL_REQUEST              = 4025;
        internal const int OPUS_GET_LOOKAHEAD_REQUEST           = 4027;
        /* internal const int OPUS_RESET_STATE 4028 */
        internal const int OPUS_GET_SAMPLE_RATE_REQUEST         = 4029;
        internal const int OPUS_GET_FINAL_RANGE_REQUEST         = 4031;
        internal const int OPUS_GET_PITCH_REQUEST               = 4033;
        internal const int OPUS_SET_GAIN_REQUEST                = 4034;
        internal const int OPUS_GET_GAIN_REQUEST                = 4045; /* Should have been 4035 */
        internal const int OPUS_SET_LSB_DEPTH_REQUEST           = 4036;
        internal const int OPUS_GET_LSB_DEPTH_REQUEST           = 4037;
        internal const int OPUS_GET_LAST_PACKET_DURATION_REQUEST = 4039;
        internal const int OPUS_SET_EXPERT_FRAME_DURATION_REQUEST = 4040;
        internal const int OPUS_GET_EXPERT_FRAME_DURATION_REQUEST = 4041;
        internal const int OPUS_SET_PREDICTION_DISABLED_REQUEST = 4042;
        internal const int OPUS_GET_PREDICTION_DISABLED_REQUEST = 4043;
        /* Don't use 4045, it's already taken by OPUS_GET_GAIN_REQUEST */
        internal const int OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST = 4046;
        internal const int OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST = 4047;
        internal const int OPUS_GET_IN_DTX_REQUEST              = 4049;
        internal const int OPUS_SET_DRED_DURATION_REQUEST = 4050;
        internal const int OPUS_GET_DRED_DURATION_REQUEST = 4051;
        internal const int OPUS_SET_DNN_BLOB_REQUEST = 4052;
        /*internal const int OPUS_GET_DNN_BLOB_REQUEST 4053 */

        internal const int OPUS_RESET_STATE = 4028;

        internal const int OPUS_AUTO                           = 1000; /**<Auto/default setting @hideinitializer*/
        internal const int OPUS_BITRATE_MAX                       = -1; /**<Maximum bitrate @hideinitializer*/

        /** Best for most VoIP/videoconference applications where listening quality and intelligibility matter most
            * @hideinitializer */
        internal const int OPUS_APPLICATION_VOIP                = 2048;
        /** Best for broadcast/high-fidelity application where the decoded audio should be as close as possible to the input
            * @hideinitializer */
        internal const int OPUS_APPLICATION_AUDIO               = 2049;
        /** Only use when lowest-achievable latency is what matters most. Voice-optimized modes cannot be used.
            * @hideinitializer */
        internal const int OPUS_APPLICATION_RESTRICTED_LOWDELAY = 2051;

        internal const int OPUS_SIGNAL_VOICE                    = 3001; /**< Signal being encoded is voice */
        internal const int OPUS_SIGNAL_MUSIC                    = 3002; /**< Signal being encoded is music */
        internal const int OPUS_BANDWIDTH_NARROWBAND            = 1101; /**< 4 kHz bandpass @hideinitializer*/
        internal const int OPUS_BANDWIDTH_MEDIUMBAND            = 1102; /**< 6 kHz bandpass @hideinitializer*/
        internal const int OPUS_BANDWIDTH_WIDEBAND              = 1103; /**< 8 kHz bandpass @hideinitializer*/
        internal const int OPUS_BANDWIDTH_SUPERWIDEBAND         = 1104; /**<12 kHz bandpass @hideinitializer*/
        internal const int OPUS_BANDWIDTH_FULLBAND              = 1105; /**<20 kHz bandpass @hideinitializer*/

        internal const int OPUS_FRAMESIZE_ARG                   = 5000; /**< Select frame size from the argument (default) */
        internal const int OPUS_FRAMESIZE_2_5_MS                = 5001; /**< Use 2.5 ms frames */
        internal const int OPUS_FRAMESIZE_5_MS                  = 5002; /**< Use 5 ms frames */
        internal const int OPUS_FRAMESIZE_10_MS                 = 5003; /**< Use 10 ms frames */
        internal const int OPUS_FRAMESIZE_20_MS                 = 5004; /**< Use 20 ms frames */
        internal const int OPUS_FRAMESIZE_40_MS                 = 5005; /**< Use 40 ms frames */
        internal const int OPUS_FRAMESIZE_60_MS                 = 5006; /**< Use 60 ms frames */
        internal const int OPUS_FRAMESIZE_80_MS                 = 5007; /**< Use 80 ms frames */
        internal const int OPUS_FRAMESIZE_100_MS                = 5008; /**< Use 100 ms frames */
        internal const int OPUS_FRAMESIZE_120_MS                = 5009; /**< Use 120 ms frames */

        internal const int MODE_SILK_ONLY = 1000;
        internal const int MODE_HYBRID = 1001;
        internal const int MODE_CELT_ONLY = 1002;

        internal const int OPUS_SET_VOICE_RATIO_REQUEST = 11018;
        internal const int OPUS_GET_VOICE_RATIO_REQUEST = 11019;
    }
}
