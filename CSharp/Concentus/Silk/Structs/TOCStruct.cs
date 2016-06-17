using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Struct for TOC (Table of Contents)
    /// </summary>
    internal class TOCStruct
    {
        /// <summary>
        /// Voice activity for packet
        /// </summary>
        internal int VADFlag = 0;

        /// <summary>
        /// Voice activity for each frame in packet
        /// </summary>
        internal readonly Pointer<int> VADFlags = Pointer.Malloc<int>(SilkConstants.SILK_MAX_FRAMES_PER_PACKET);

        /// <summary>
        /// Flag indicating if packet contains in-band FEC
        /// </summary>
        internal int inbandFECFlag = 0;
    
        internal void Reset()
        {
            VADFlag = 0;
            VADFlags.MemSet(0, SilkConstants.SILK_MAX_FRAMES_PER_PACKET);
            inbandFECFlag = 0;
        }
    }
}
