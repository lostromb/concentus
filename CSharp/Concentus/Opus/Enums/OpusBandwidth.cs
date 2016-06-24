using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Enums
{
    public enum OpusBandwidth
    {
        OPUS_BANDWIDTH_AUTO = -1000, 
        OPUS_BANDWIDTH_NARROWBAND = 1101,
        OPUS_BANDWIDTH_MEDIUMBAND = 1102,
        OPUS_BANDWIDTH_WIDEBAND = 1103,
        OPUS_BANDWIDTH_SUPERWIDEBAND = 1104,
        OPUS_BANDWIDTH_FULLBAND = 1105
    }

    internal static class OpusBandwidthHelpers
    {
        internal static int GetOrdinal(OpusBandwidth bw)
        {
            return (int)bw - (int)OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
        }

        internal static OpusBandwidth MIN(OpusBandwidth a, OpusBandwidth b)
        {
            if ((int)a < (int)b)
                return a;
            return b;
        }

        internal static OpusBandwidth MAX(OpusBandwidth a, OpusBandwidth b)
        {
            if ((int)a > (int)b)
                return a;
            return b;
        }
    }
}
