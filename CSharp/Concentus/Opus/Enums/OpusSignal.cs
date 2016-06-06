using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Opus.Enums
{
    public static class OpusSignal
    {
        /// <summary>
        /// Signal being encoded is voice
        /// </summary>
        public static int OPUS_SIGNAL_VOICE = 3001;

        /// <summary>
        /// Signal being encoded is music
        /// </summary>
        public static int OPUS_SIGNAL_MUSIC = 3002;
    }
}
