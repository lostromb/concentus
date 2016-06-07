using Concentus.Celt.Structs;
using Concentus.Common.CPlusPlus;
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

        public void Reset()
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
        public void PartialReset()
        {
            stream_channels = 0;

            bandwidth = 0;
            mode = 0;
            prev_mode = 0;
            frame_size = 0;
            prev_redundancy = 0;
            last_packet_duration = 0;
            rangeFinal = 0;
            SilkDecoder.Reset();
            CeltDecoder.Reset();
        }
    }
}
