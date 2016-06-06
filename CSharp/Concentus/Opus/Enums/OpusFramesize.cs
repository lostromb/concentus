using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Opus.Enums
{
    public static class OpusFramesize
    {
        public const int OPUS_FRAMESIZE_ARG = 5000; /**< Select frame size from the argument (default) */
        public const int OPUS_FRAMESIZE_2_5_MS = 5001; /**< Use 2.5 ms frames */
        public const int OPUS_FRAMESIZE_5_MS = 5002; /**< Use 5 ms frames */
        public const int OPUS_FRAMESIZE_10_MS = 5003; /**< Use 10 ms frames */
        public const int OPUS_FRAMESIZE_20_MS = 5004; /**< Use 20 ms frames */
        public const int OPUS_FRAMESIZE_40_MS = 5005; /**< Use 40 ms frames */
        public const int OPUS_FRAMESIZE_60_MS = 5006; /**< Use 60 ms frames */
        public const int OPUS_FRAMESIZE_VARIABLE = 5010; /**< Optimize the frame size dynamically */
    }
}
