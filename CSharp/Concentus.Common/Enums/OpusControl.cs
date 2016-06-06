using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common.Enums
{
    public static class OpusControl
    {
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
        public const int OPUS_GET_GAIN_REQUEST = 4045;
        public const int OPUS_SET_LSB_DEPTH_REQUEST = 4036;
        public const int OPUS_GET_LSB_DEPTH_REQUEST = 4037;
        public const int OPUS_GET_LAST_PACKET_DURATION_REQUEST = 4039;
        public const int OPUS_SET_EXPERT_FRAME_DURATION_REQUEST = 4040;
        public const int OPUS_GET_EXPERT_FRAME_DURATION_REQUEST = 4041;
        public const int OPUS_SET_PREDICTION_DISABLED_REQUEST = 4042;
        public const int OPUS_GET_PREDICTION_DISABLED_REQUEST = 4043;

        /// <summary>
        /// Resets the codec state to be equivalent to a freshly initialized state.
        /// This should be called when switching streams in order to prevent
        /// the back to back decoding from giving different results from
        /// one at a time decoding.
        /// </summary>
        public const int OPUS_RESET_STATE = 4028;

        public const int OPUS_SET_VOICE_RATIO_REQUEST = 11018;
        public const int OPUS_GET_VOICE_RATIO_REQUEST = 11019;
        public const int OPUS_SET_FORCE_MODE_REQUEST = 11002;
    }
}
