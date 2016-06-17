using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Structs
{
    public class OpusMSDecoder
    {
        internal ChannelLayout layout = new ChannelLayout();
        internal OpusDecoder[] decoders = null;

        public OpusMSDecoder(int nb_streams, int nb_coupled_streams)
        {
            decoders = new OpusDecoder[nb_streams];
            for (int c = 0; c < nb_streams; c++)
                decoders[c] = new OpusDecoder();
        }

        public int GetBandwidth()
        {
            if (decoders == null || decoders.Length == 0)
                return OpusError.OPUS_INVALID_STATE;
            return decoders[0].GetBandwidth();
        }

        public int GetSampleRate()
        {
            if (decoders == null || decoders.Length == 0)
                return OpusError.OPUS_INVALID_STATE;
            return decoders[0].GetSampleRate();
        }

        public int GetGain()
        {
            if (decoders == null || decoders.Length == 0)
                return OpusError.OPUS_INVALID_STATE;
            return decoders[0].GetGain();
        }

        public int GetLastPacketDuration()
        {
            if (decoders == null || decoders.Length == 0)
                return OpusError.OPUS_INVALID_STATE;
            return decoders[0].GetLastPacketDuration();
        }

        public uint GetFinalRange()
        {
            uint value = 0;
            for (int s = 0; s < layout.nb_streams; s++)
            {
                value ^= decoders[s].GetFinalRange();
            }
            return value;
        }

        public void ResetState()
        {
            for (int s = 0; s < layout.nb_streams; s++)
            {
                decoders[s].ResetState();
            }
        }

        public OpusDecoder GetMultistreamDecoderState(int streamId)
        {
            return decoders[streamId];
        }

        public void SetGain(int gain)
        {
            for (int s = 0; s < layout.nb_streams; s++)
            {
                decoders[s].SetGain(gain);
            }
        }
    }
}