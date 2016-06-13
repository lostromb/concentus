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
    /** @defgroup opus_decoder Opus Decoder
  * @{
  *
  * @brief This page describes the process and functions used to decode Opus.
  *
  * The decoding process also starts with creating a decoder
  * state. This can be done with:
  * @code
  * int          error;
  * OpusDecoder *dec;
  * dec = opus_decoder_create(Fs, channels, &error);
  * @endcode
  * where
  * @li Fs is the sampling rate and must be 8000, 12000, 16000, 24000, or 48000
  * @li channels is the number of channels (1 or 2)
  * @li error will hold the error code in case of failure (or #OPUS_OK on success)
  * @li the return value is a newly created decoder state to be used for decoding
  *
  * While opus_decoder_create() allocates memory for the state, it's also possible
  * to initialize pre-allocated memory:
  * @code
  * int          size;
  * int          error;
  * OpusDecoder *dec;
  * size = opus_decoder_get_size(channels);
  * dec = malloc(size);
  * error = opus_decoder_init(dec, Fs, channels);
  * @endcode
  * where opus_decoder_get_size() returns the required size for the decoder state. Note that
  * future versions of this code may change the size, so no assuptions should be made about it.
  *
  * The decoder state is always continuous in memory and only a shallow copy is sufficient
  * to copy it (e.g. memcpy())
  *
  * To decode a frame, opus_decode() or opus_decode_float() must be called with a packet of compressed audio data:
  * @code
  * frame_size = opus_decode(dec, packet, len, decoded, max_size, 0);
  * @endcode
  * where
  *
  * @li packet is the byte array containing the compressed data
  * @li len is the exact number of bytes contained in the packet
  * @li decoded is the decoded audio data in opus_int16 (or float for opus_decode_float())
  * @li max_size is the max duration of the frame in samples (per channel) that can fit into the decoded_frame array
  *
  * opus_decode() and opus_decode_float() return the number of samples (per channel) decoded from the packet.
  * If that value is negative, then an error has occurred. This can occur if the packet is corrupted or if the audio
  * buffer is too small to hold the decoded audio.
  *
  * Opus is a stateful codec with overlapping blocks and as a result Opus
  * packets are not coded independently of each other. Packets must be
  * passed into the decoder serially and in the correct order for a correct
  * decode. Lost packets can be replaced with loss concealment by calling
  * the decoder with a null pointer and zero length for the missing packet.
  *
  * A single codec state may only be accessed from a single thread at
  * a time and any required locking must be performed by the caller. Separate
  * streams must be decoded with separate decoder states and can be decoded
  * in parallel unless the library was compiled with NONTHREADSAFE_PSEUDOSTACK
  * defined.
  *
  */
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
