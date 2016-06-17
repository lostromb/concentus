using Concentus.Common.CPlusPlus;
using Concentus.Silk.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Decoder super struct
    /// </summary>
    public class SilkDecoder
    {
        public readonly Pointer<SilkChannelDecoder> channel_state = Pointer.Malloc<SilkChannelDecoder>(SilkConstants.DECODER_NUM_CHANNELS);
        public readonly StereoDecodeState sStereo = new StereoDecodeState();
        public int nChannelsAPI = 0;
        public int nChannelsInternal = 0;
        public int prev_decode_only_middle = 0;

        public SilkDecoder()
        {
            for (int c = 0; c < SilkConstants.DECODER_NUM_CHANNELS; c++)
            {
                channel_state[c] = new SilkChannelDecoder();
            }
        }

        public void Reset()
        {
            for (int c = 0; c < SilkConstants.DECODER_NUM_CHANNELS; c++)
            {
                channel_state[c].Reset();
            }
            sStereo.Reset();
            nChannelsAPI = 0;
            nChannelsInternal = 0;
            prev_decode_only_middle = 0;
        }
    }
}
