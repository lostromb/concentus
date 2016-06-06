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
    public class silk_decoder
    {
        public /*readonly*/ Pointer<silk_decoder_state> channel_state = Pointer.Malloc<silk_decoder_state>(SilkConstants.DECODER_NUM_CHANNELS);
        public /*readonly*/ stereo_dec_state sStereo = new stereo_dec_state();
        public int nChannelsAPI = 0;
        public int nChannelsInternal = 0;
        public int prev_decode_only_middle = 0;

        public silk_decoder()
        {
            for (int c = 0; c < SilkConstants.DECODER_NUM_CHANNELS; c++)
            {
                channel_state[c] = new silk_decoder_state();
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
