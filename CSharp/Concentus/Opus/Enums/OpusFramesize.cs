using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Enums
{
    public enum OpusFramesize
    {
        /// <summary>
        /// Select frame size from the argument (default)
        /// </summary>
        OPUS_FRAMESIZE_ARG = 5000,

        /// <summary>
        /// Use 2.5 ms frames
        /// </summary>
        OPUS_FRAMESIZE_2_5_MS = 5001,

        /// <summary>
        /// Use 5 ms frames
        /// </summary>
        OPUS_FRAMESIZE_5_MS = 5002,

        /// <summary>
        /// Use 10 ms frames
        /// </summary>
        OPUS_FRAMESIZE_10_MS = 5003,

        /// <summary>
        /// Use 20 ms frames
        /// </summary>
        OPUS_FRAMESIZE_20_MS = 5004,

        /// <summary>
        /// Use 40 ms frames
        /// </summary>
        OPUS_FRAMESIZE_40_MS = 5005,

        /// <summary>
        /// Use 60 ms frames
        /// </summary>
        OPUS_FRAMESIZE_60_MS = 5006,

        /// <summary>
        /// Optimize the frame size dynamically
        /// </summary>
        OPUS_FRAMESIZE_VARIABLE = 5010
    }
}
