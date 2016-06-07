using Concentus.Celt;
using Concentus.Celt.Structs;
using Concentus.Common.CPlusPlus;
using Concentus.Opus.Enums;
using Concentus.Silk;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Structs
{
    public class OpusDecoder
    {
        public int channels;
        public int Fs;          /** Sampling rate (at the API level) */
        public readonly silk_DecControlStruct DecControl = new silk_DecControlStruct();
        public int decode_gain;
        public int arch;

        /* Everything beyond this point gets cleared on a reset */
        public int stream_channels;
        public int bandwidth;
        public int mode;
        public int prev_mode;
        public int frame_size;
        public int prev_redundancy;
        public int last_packet_duration;
        public uint rangeFinal;
        public silk_decoder SilkDecoder = new silk_decoder();
        public CELTDecoder CeltDecoder = new CELTDecoder();

        internal void Reset()
        {
            channels = 0;
            Fs = 0;          /** Sampling rate (at the API level) */
            DecControl.Reset();
            decode_gain = 0;
            arch = 0;
            PartialReset();
        }

        /// <summary>
        /// OPUS_DECODER_RESET_START
        /// </summary>
        internal void PartialReset()
        {
            stream_channels = 0;

            bandwidth = 0;
            mode = 0;
            prev_mode = 0;
            frame_size = 0;
            prev_redundancy = 0;
            last_packet_duration = 0;
            rangeFinal = 0;
            // fixme: do these get reset here? I don't think they do because init_celt and init_silk should both call RESET_STATE on their respective states
            //SilkDecoder.Reset();
            //CeltDecoder.Reset();
        }

        public int GetBandwidth()
        {
            return bandwidth;
        }

        public uint GetFinalRange()
        {
            return rangeFinal;
        }

        public void ResetState()
        {
            PartialReset();
            celt_decoder.opus_custom_decoder_ctl(CeltDecoder, OpusControl.OPUS_RESET_STATE);
            dec_API.silk_InitDecoder(SilkDecoder);
            stream_channels = channels;
            frame_size = Fs / 400;
        }

        public int GetSampleRate()
        {
            return Fs;
        }

        public int GetPitch()
        {
            if (prev_mode == OpusMode.MODE_CELT_ONLY)
            {
                BoxedValue<int> value = new BoxedValue<int>();
                celt_decoder.opus_custom_decoder_ctl(CeltDecoder, OpusControl.OPUS_GET_PITCH_REQUEST, value);
                return value.Val;
            }
            else
                return DecControl.prevPitchLag;
        }

        public int GetGain()
        {
            return decode_gain;
        }

        public int SetGain(int gain)
        {
            if (gain < -32768 || gain > 32767)
            {
                return OpusError.OPUS_BAD_ARG;
            }

            decode_gain = gain;
            return OpusError.OPUS_OK;
        }

        public int GetLastPacketDuration()
        {
            return last_packet_duration;
        }
    }
}
