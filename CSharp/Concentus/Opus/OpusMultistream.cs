using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus
{
    internal static class OpusMultistream
    {
        internal static int validate_layout(ChannelLayout layout)
        {
            int i, max_channel;

            max_channel = layout.nb_streams + layout.nb_coupled_streams;
            if (max_channel > 255)
                return 0;
            for (i = 0; i < layout.nb_channels; i++)
            {
                if (layout.mapping[i] >= max_channel && layout.mapping[i] != 255)
                    return 0;
            }
            return 1;
        }


        internal static int get_left_channel(ChannelLayout layout, int stream_id, int prev)
        {
            int i;
            i = (prev < 0) ? 0 : prev + 1;
            for (; i < layout.nb_channels; i++)
            {
                if (layout.mapping[i] == stream_id * 2)
                    return i;
            }
            return -1;
        }

        internal static int get_right_channel(ChannelLayout layout, int stream_id, int prev)
        {
            int i;
            i = (prev < 0) ? 0 : prev + 1;
            for (; i < layout.nb_channels; i++)
            {
                if (layout.mapping[i] == stream_id * 2 + 1)
                    return i;
            }
            return -1;
        }

        internal static int get_mono_channel(ChannelLayout layout, int stream_id, int prev)
        {
            int i;
            i = (prev < 0) ? 0 : prev + 1;
            for (; i < layout.nb_channels; i++)
            {
                if (layout.mapping[i] == stream_id + layout.nb_coupled_streams)
                    return i;
            }
            return -1;
        }
    }
}
