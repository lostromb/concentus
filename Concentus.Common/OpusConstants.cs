using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common
{
    public static class OpusConstants
    {
        /// <summary>
        /// Auto/default setting
        /// </summary>
        public const int OPUS_AUTO = -1000;

        /// <summary>
        /// Maximum bitrate
        /// </summary>
        public const int OPUS_BITRATE_MAX = -1;

        // from analysis.c
        public const int NB_FRAMES = 8;
        public const int NB_TBANDS = 18;
        public const int NB_TOT_BANDS = 21;
        public const int NB_TONAL_SKIP_BANDS = 9;
        public const int ANALYSIS_BUF_SIZE = 720; /* 15 ms at 48 kHz */
        public const int DETECT_SIZE = 200;

        public const int MAX_ENCODER_BUFFER = 480;
    }
}
