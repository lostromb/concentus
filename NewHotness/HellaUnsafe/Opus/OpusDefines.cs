using System;
using System.Collections.Generic;
using System.Text;

namespace HellaUnsafe.Opus
{
    public static class OpusDefines
    {
        public const int OPUS_OK = 0;
        public const int OPUS_BAD_ARG = -1;
        public const int OPUS_BUFFER_TOO_SMALL = -2;
        public const int OPUS_INTERNAL_ERROR = -3;
        public const int OPUS_INVALID_PACKET = -4;
        public const int OPUS_UNIMPLEMENTED = -5;
        public const int OPUS_INVALID_STATE = -6;
        public const int OPUS_ALLOC_FAIL = -7;

        /** These are the actual Encoder CTL ID numbers.
          * They should not be used directly by applications.
          * In general, SETs should be even and GETs should be odd.*/
        public const int OPUS_SET_APPLICATION_REQUEST = 4000;
        public const int OPUS_GET_APPLICATION_REQUEST = 4001;
        public const int OPUS_SET_BITRATE_REQUEST = 4002;
        public const int OPUS_GET_BITRATE_REQUEST = 4003;
        public const int OPUS_SET_MAX_BANDWIDTH_REQUEST = 4004;
        public const int OPUS_GET_MAX_BANDWIDTH_REQUEST = 4005;
        public const int OPUS_SET_VBR_REQUEST = 4006;
        public const int OPUS_GET_VBR_REQUEST = 4007;
        public const int OPUS_SET_BANDWIDTH_REQUEST = 4008;
        public const int OPUS_GET_BANDWIDTH_REQUEST = 4009;
        public const int OPUS_SET_COMPLEXITY_REQUEST = 4010;
        public const int OPUS_GET_COMPLEXITY_REQUEST = 4011;
        public const int OPUS_SET_INBAND_FEC_REQUEST = 4012;
        public const int OPUS_GET_INBAND_FEC_REQUEST = 4013;
        public const int OPUS_SET_PACKET_LOSS_PERC_REQUEST = 4014;
        public const int OPUS_GET_PACKET_LOSS_PERC_REQUEST = 4015;
        public const int OPUS_SET_DTX_REQUEST = 4016;
        public const int OPUS_GET_DTX_REQUEST = 4017;
        public const int OPUS_SET_VBR_CONSTRAINT_REQUEST = 4020;
        public const int OPUS_GET_VBR_CONSTRAINT_REQUEST = 4021;
        public const int OPUS_SET_FORCE_CHANNELS_REQUEST = 4022;
        public const int OPUS_GET_FORCE_CHANNELS_REQUEST = 4023;
        public const int OPUS_SET_SIGNAL_REQUEST = 4024;
        public const int OPUS_GET_SIGNAL_REQUEST = 4025;
        public const int OPUS_GET_LOOKAHEAD_REQUEST = 4027;
        /* public const int OPUS_RESET_STATE 4028 */
        public const int OPUS_GET_SAMPLE_RATE_REQUEST = 4029;
        public const int OPUS_GET_FINAL_RANGE_REQUEST = 4031;
        public const int OPUS_GET_PITCH_REQUEST = 4033;
        public const int OPUS_SET_GAIN_REQUEST = 4034;
        public const int OPUS_GET_GAIN_REQUEST = 4045; /* Should have been 4035 */
        public const int OPUS_SET_LSB_DEPTH_REQUEST = 4036;
        public const int OPUS_GET_LSB_DEPTH_REQUEST = 4037;
        public const int OPUS_GET_LAST_PACKET_DURATION_REQUEST = 4039;
        public const int OPUS_SET_EXPERT_FRAME_DURATION_REQUEST = 4040;
        public const int OPUS_GET_EXPERT_FRAME_DURATION_REQUEST = 4041;
        public const int OPUS_SET_PREDICTION_DISABLED_REQUEST = 4042;
        public const int OPUS_GET_PREDICTION_DISABLED_REQUEST = 4043;
        /* Don't use 4045, it's already taken by OPUS_GET_GAIN_REQUEST */
        public const int OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST = 4046;
        public const int OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST = 4047;
        public const int OPUS_GET_IN_DTX_REQUEST = 4049;

        public const int OPUS_RESET_STATE = 4028;

        public const int OPUS_AUTO = -1000; /**<Auto/default setting @hideinitializer*/
        public const int OPUS_BITRATE_MAX = -1; /**<Maximum bitrate @hideinitializer*/

        /** Best for most VoIP/videoconference applications where listening quality and intelligibility matter most
            * @hideinitializer */
        public const int OPUS_APPLICATION_VOIP = 2048;
        /** Best for broadcast/high-fidelity application where the decoded audio should be as close as possible to the input
            * @hideinitializer */
        public const int OPUS_APPLICATION_AUDIO = 2049;
        /** Only use when lowest-achievable latency is what matters most. Voice-optimized modes cannot be used.
            * @hideinitializer */
        public const int OPUS_APPLICATION_RESTRICTED_LOWDELAY = 2051;

        public const int OPUS_SIGNAL_VOICE = 3001; /**< Signal being encoded is voice */
        public const int OPUS_SIGNAL_MUSIC = 3002; /**< Signal being encoded is music */
        public const int OPUS_BANDWIDTH_NARROWBAND = 1101; /**< 4 kHz bandpass @hideinitializer*/
        public const int OPUS_BANDWIDTH_MEDIUMBAND = 1102; /**< 6 kHz bandpass @hideinitializer*/
        public const int OPUS_BANDWIDTH_WIDEBAND = 1103; /**< 8 kHz bandpass @hideinitializer*/
        public const int OPUS_BANDWIDTH_SUPERWIDEBAND = 1104; /**<12 kHz bandpass @hideinitializer*/
        public const int OPUS_BANDWIDTH_FULLBAND = 1105; /**<20 kHz bandpass @hideinitializer*/

        public const int OPUS_FRAMESIZE_ARG = 5000; /**< Select frame size from the argument (default) */
        public const int OPUS_FRAMESIZE_2_5_MS = 5001; /**< Use 2.5 ms frames */
        public const int OPUS_FRAMESIZE_5_MS = 5002; /**< Use 5 ms frames */
        public const int OPUS_FRAMESIZE_10_MS = 5003; /**< Use 10 ms frames */
        public const int OPUS_FRAMESIZE_20_MS = 5004; /**< Use 20 ms frames */
        public const int OPUS_FRAMESIZE_40_MS = 5005; /**< Use 40 ms frames */
        public const int OPUS_FRAMESIZE_60_MS = 5006; /**< Use 60 ms frames */
        public const int OPUS_FRAMESIZE_80_MS = 5007; /**< Use 80 ms frames */
        public const int OPUS_FRAMESIZE_100_MS = 5008; /**< Use 100 ms frames */
        public const int OPUS_FRAMESIZE_120_MS = 5009; /**< Use 120 ms frames */
    }
}
