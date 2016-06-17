using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Enums
{
    public enum OpusSignal
    {
        OPUS_SIGNAL_AUTO = -1000,

        /// <summary>
        /// Signal being encoded is voice
        /// </summary>
        OPUS_SIGNAL_VOICE = 3001,

        /// <summary>
        /// Signal being encoded is music
        /// </summary>
        OPUS_SIGNAL_MUSIC = 3002
    }
}
