using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common.Enums
{
    public static class OpusApplication
    {
        /// <summary>
        /// Best for most VoIP/videoconference applications where listening quality and intelligibility matter most
        /// </summary>
        public static int OPUS_APPLICATION_VOIP = 2048;

        /// <summary>
        /// Best for broadcast/high-fidelity application where the decoded audio should be as close as possible to the input
        /// </summary>
        public static int OPUS_APPLICATION_AUDIO = 2049;

        /// <summary>
        /// Only use when lowest-achievable latency is what matters most. Voice-optimized modes cannot be used.
        /// </summary>
        public static int OPUS_APPLICATION_RESTRICTED_LOWDELAY = 2051;
    }
}
