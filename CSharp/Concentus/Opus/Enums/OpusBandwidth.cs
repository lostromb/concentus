using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Opus.Enums
{
    public static class OpusBandwidth
    {
        public const int OPUS_BANDWIDTH_NARROWBAND = 1101; /**< 4 kHz bandpass @hideinitializer*/
        public const int OPUS_BANDWIDTH_MEDIUMBAND = 1102; /**< 6 kHz bandpass @hideinitializer*/
        public const int OPUS_BANDWIDTH_WIDEBAND = 1103; /**< 8 kHz bandpass @hideinitializer*/
        public const int OPUS_BANDWIDTH_SUPERWIDEBAND = 1104; /**<12 kHz bandpass @hideinitializer*/
        public const int OPUS_BANDWIDTH_FULLBAND = 1105; /**<20 kHz bandpass @hideinitializer*/
    }
}
