using Concentus.Celt;
using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Enums;
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
        internal int channels;
        internal int Fs;          /** Sampling rate (at the API level) */
        internal readonly DecControlState DecControl = new DecControlState();
        internal int decode_gain;

        /* Everything beyond this point gets cleared on a reset */
        internal int stream_channels;
        internal int bandwidth;
        internal OpusMode mode;
        internal OpusMode prev_mode;
        internal int frame_size;
        internal int prev_redundancy;
        internal int last_packet_duration;
        internal uint rangeFinal;
        internal SilkDecoder SilkDecoder = new SilkDecoder();
        internal CeltDecoder CeltDecoder = new CeltDecoder();

        internal void Reset()
        {
            channels = 0;
            Fs = 0;          /** Sampling rate (at the API level) */
            DecControl.Reset();
            decode_gain = 0;
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

        /** Initializes a previously allocated decoder state.
  * The state must be at least the size returned by opus_decoder_get_size().
  * This is intended for applications which use their own allocator instead of malloc. @see opus_decoder_create,opus_decoder_get_size
  * To reset a previously initialized state, use the #OPUS_RESET_STATE CTL.
  * @param [in] st <tt>OpusDecoder*</tt>: Decoder state.
  * @param [in] Fs <tt>opus_int32</tt>: Sampling rate to decode to (Hz).
  *                                     This must be one of 8000, 12000, 16000,
  *                                     24000, or 48000.
  * @param [in] channels <tt>int</tt>: Number of channels (1 or 2) to decode
  * @retval #OPUS_OK Success or @ref opus_errorcodes
  */
        internal int opus_decoder_init(int Fs, int channels)
        {
            SilkDecoder silk_dec;
            CeltDecoder celt_dec;
            int ret;

            if ((Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000)
             || (channels != 1 && channels != 2))
                return OpusError.OPUS_BAD_ARG;
            this.Reset();

            /* Initialize SILK encoder */
            silk_dec = this.SilkDecoder;
            celt_dec = this.CeltDecoder;
            this.stream_channels = this.channels = channels;

            this.Fs = Fs;
            this.DecControl.API_sampleRate = this.Fs;
            this.DecControl.nChannelsAPI = this.channels;

            /* Reset decoder */
            ret = DecodeAPI.silk_InitDecoder(silk_dec);
            if (ret != 0) return OpusError.OPUS_INTERNAL_ERROR;

            /* Initialize CELT decoder */
            ret = celt_decoder.celt_decoder_init(celt_dec, Fs, channels);
            if (ret != OpusError.OPUS_OK)
                return OpusError.OPUS_INTERNAL_ERROR;

            celt_decoder.opus_custom_decoder_ctl(celt_dec, CeltControl.CELT_SET_SIGNALLING_REQUEST, 0);

            this.prev_mode = 0;
            this.frame_size = Fs / 400;
            return OpusError.OPUS_OK;
        }

        /** Allocates and initializes a decoder state.
  * @param [in] Fs <tt>opus_int32</tt>: Sample rate to decode at (Hz).
  *                                     This must be one of 8000, 12000, 16000,
  *                                     24000, or 48000.
  * @param [in] channels <tt>int</tt>: Number of channels (1 or 2) to decode
  * @param [out] error <tt>int*</tt>: #OPUS_OK Success or @ref opus_errorcodes
  *
  * Internally Opus stores data at 48000 Hz, so that should be the default
  * value for Fs. However, the decoder can efficiently decode to buffers
  * at 8, 12, 16, and 24 kHz so if for some reason the caller cannot use
  * data at the full sample rate, or knows the compressed data doesn't
  * use the full frequency range, it can request decoding at a reduced
  * rate. Likewise, the decoder is capable of filling in either mono or
  * interleaved stereo pcm buffers, at the caller's request.
  */
        public static OpusDecoder Create(int Fs, int channels, BoxedValue<int> error)
        {
            int ret;
            OpusDecoder st; // porting note: pointer
            if ((Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000)
             || (channels != 1 && channels != 2))
            {
                if (error != null)
                    error.Val = OpusError.OPUS_BAD_ARG;
                return null;
            }
            st = new OpusDecoder();
            if (st == null)
            {
                if (error != null)
                    error.Val = OpusError.OPUS_ALLOC_FAIL;
                return null;
            }
            ret = st.opus_decoder_init(Fs, channels);
            if (error != null)
                error.Val = ret;
            if (ret != OpusError.OPUS_OK)
            {
                st = null;
            }
            return st;
        }
        
        internal int opus_decode_frame(Pointer<byte> data,
      int len, Pointer<short> pcm, int frame_size, int decode_fec)
        {
            SilkDecoder silk_dec;
            CeltDecoder celt_dec;
            int i, silk_ret = 0, celt_ret = 0;
            EntropyCoder dec = new EntropyCoder(); // porting note: stack var
            int silk_frame_size;
            int pcm_silk_size;
            Pointer<short> pcm_silk;
            int pcm_transition_silk_size;
            Pointer<short> pcm_transition_silk;
            int pcm_transition_celt_size;
            Pointer<short> pcm_transition_celt;
            Pointer<short> pcm_transition = null;
            int redundant_audio_size;
            Pointer<short> redundant_audio;

            int audiosize;
            OpusMode mode;
            int transition = 0;
            int start_band;
            int redundancy = 0;
            int redundancy_bytes = 0;
            int celt_to_silk = 0;
            int c;
            int F2_5, F5, F10, F20;
            Pointer<int> window;
            uint redundant_rng = 0;
            int celt_accum;

            silk_dec = this.SilkDecoder;
            celt_dec = this.CeltDecoder;
            F20 = this.Fs / 50;
            F10 = F20 >> 1;
            F5 = F10 >> 1;
            F2_5 = F5 >> 1;
            if (frame_size < F2_5)
            {

                return OpusError.OPUS_BUFFER_TOO_SMALL;
            }
            /* Limit frame_size to avoid excessive stack allocations. */
            frame_size = Inlines.IMIN(frame_size, this.Fs / 25 * 3);
            /* Payloads of 1 (2 including ToC) or 0 trigger the PLC/DTX */
            if (len <= 1)
            {
                data = null;
                /* In that case, don't conceal more than what the ToC says */
                frame_size = Inlines.IMIN(frame_size, this.frame_size);
            }
            if (data != null)
            {
                audiosize = this.frame_size;
                mode = this.mode;
                dec.dec_init(data, (uint)len);
            }
            else {
                audiosize = frame_size;
                mode = this.prev_mode;

                if (mode == 0)
                {
                    /* If we haven't got any packet yet, all we can do is return zeros */
                    for (i = 0; i < audiosize * this.channels; i++)
                        pcm[i] = 0;

                    return audiosize;
                }

                /* Avoids trying to run the PLC on sizes other than 2.5 (CELT), 5 (CELT),
                   10, or 20 (e.g. 12.5 or 30 ms). */
                if (audiosize > F20)
                {
                    do
                    {
                        int ret = opus_decode_frame(null, 0, pcm, Inlines.IMIN(audiosize, F20), 0);
                        if (ret < 0)
                        {

                            return ret;
                        }
                        pcm = pcm.Point(ret * this.channels);
                        audiosize -= ret;
                    } while (audiosize > 0);

                    return frame_size;
                }
                else if (audiosize < F20)
                {
                    if (audiosize > F10)
                        audiosize = F10;
                    else if (mode != OpusMode.MODE_SILK_ONLY && audiosize > F5 && audiosize < F10)
                        audiosize = F5;
                }
            }

            /* In fixed-point, we can tell CELT to do the accumulation on top of the
               SILK PCM buffer. This saves some stack space. */
            celt_accum = ((mode != OpusMode.MODE_CELT_ONLY) && (frame_size >= F10)) ? 1 : 0;

            pcm_transition_silk_size = 0;
            pcm_transition_celt_size = 0;
            if (data != null && this.prev_mode > 0 && (
                (mode == OpusMode.MODE_CELT_ONLY && this.prev_mode != OpusMode.MODE_CELT_ONLY && (this.prev_redundancy == 0))
             || (mode != OpusMode.MODE_CELT_ONLY && this.prev_mode == OpusMode.MODE_CELT_ONLY))
               )
            {
                transition = 1;
                /* Decide where to allocate the stack memory for pcm_transition */
                if (mode == OpusMode.MODE_CELT_ONLY)
                    pcm_transition_celt_size = F5 * this.channels;
                else
                    pcm_transition_silk_size = F5 * this.channels;
            }
            pcm_transition_celt = Pointer.Malloc<short>(pcm_transition_celt_size);
            if (transition != 0 && mode == OpusMode.MODE_CELT_ONLY)
            {
                pcm_transition = pcm_transition_celt;
                opus_decode_frame(null, 0, pcm_transition, Inlines.IMIN(F5, audiosize), 0);
            }
            if (audiosize > frame_size)
            {
                /*fprintf(stderr, "PCM buffer too small: %d vs %d (mode = %d)\n", audiosize, frame_size, mode);*/

                return OpusError.OPUS_BAD_ARG;
            }
            else {
                frame_size = audiosize;
            }

            /* Don't allocate any memory when in CELT-only mode */
            pcm_silk_size = (mode != OpusMode.MODE_CELT_ONLY && (celt_accum == 0)) ? Inlines.IMAX(F10, frame_size) * this.channels : 0;
            pcm_silk = Pointer.Malloc<short>(pcm_silk_size);

            /* SILK processing */
            if (mode != OpusMode.MODE_CELT_ONLY)
            {
                int lost_flag, decoded_samples;
                Pointer<short> pcm_ptr;

                if (celt_accum != 0)
                    pcm_ptr = pcm;
                else
                    pcm_ptr = pcm_silk;

                if (this.prev_mode == OpusMode.MODE_CELT_ONLY)
                    DecodeAPI.silk_InitDecoder(silk_dec);

                /* The SILK PLC cannot produce frames of less than 10 ms */
                this.DecControl.payloadSize_ms = Inlines.IMAX(10, 1000 * audiosize / this.Fs);

                if (data != null)
                {
                    this.DecControl.nChannelsInternal = this.stream_channels;
                    if (mode == OpusMode.MODE_SILK_ONLY)
                    {
                        if (this.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND)
                        {
                            this.DecControl.internalSampleRate = 8000;
                        }
                        else if (this.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
                        {
                            this.DecControl.internalSampleRate = 12000;
                        }
                        else if (this.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)
                        {
                            this.DecControl.internalSampleRate = 16000;
                        }
                        else {
                            this.DecControl.internalSampleRate = 16000;
                            Inlines.OpusAssert(false);
                        }
                    }
                    else {
                        /* Hybrid mode */
                        this.DecControl.internalSampleRate = 16000;
                    }
                }

                lost_flag = data == null ? 1 : 2 * decode_fec;
                decoded_samples = 0;
                do
                {
                    /* Call SILK decoder */
                    int first_frame = (decoded_samples == 0) ? 1 : 0;
                    BoxedValue<int> boxed_frame_size = new BoxedValue<int>();
                    silk_ret = DecodeAPI.silk_Decode(silk_dec, this.DecControl,
                                            lost_flag, first_frame, dec, pcm_ptr, boxed_frame_size);
                    silk_frame_size = boxed_frame_size.Val;
                    if (silk_ret != 0)
                    {
                        if (lost_flag != 0)
                        {
                            /* PLC failure should not be fatal */
                            silk_frame_size = frame_size;
                            for (i = 0; i < frame_size * this.channels; i++)
                                pcm_ptr[i] = 0;
                        }
                        else {

                            return OpusError.OPUS_INTERNAL_ERROR;
                        }
                    }
                    pcm_ptr = pcm_ptr.Point(silk_frame_size * this.channels);
                    decoded_samples += silk_frame_size;
                } while (decoded_samples < frame_size);
            }

            start_band = 0;
            if (decode_fec == 0 && mode != OpusMode.MODE_CELT_ONLY && data != null
             && dec.tell() + 17 + 20 * (this.mode == OpusMode.MODE_HYBRID ? 1 : 0) <= 8 * len)
            {
                /* Check if we have a redundant 0-8 kHz band */
                if (mode == OpusMode.MODE_HYBRID)
                    redundancy = dec.dec_bit_logp(12);
                else
                    redundancy = 1;
                if (redundancy != 0)
                {
                    celt_to_silk = dec.dec_bit_logp(1);
                    /* redundancy_bytes will be at least two, in the non-hybrid
                       case due to the ec_tell() check above */
                    redundancy_bytes = mode == OpusMode.MODE_HYBRID ?
                                      (int)dec.dec_uint(256) + 2 :
                                      len - ((dec.tell() + 7) >> 3);
                    len -= redundancy_bytes;
                    /* This is a sanity check. It should never happen for a valid
                       packet, so the exact behaviour is not normative. */
                    if (len * 8 < dec.tell())
                    {
                        len = 0;
                        redundancy_bytes = 0;
                        redundancy = 0;
                    }
                    /* Shrink decoder because of raw bits */
                    dec.storage = (uint)(dec.storage - redundancy_bytes);
                }
            }
            if (mode != OpusMode.MODE_CELT_ONLY)
                start_band = 17;

            {
                int endband = 21;

                switch (this.bandwidth)
                {
                    case OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND:
                        endband = 13;
                        break;
                    case OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND:
                    case OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND:
                        endband = 17;
                        break;
                    case OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND:
                        endband = 19;
                        break;
                    case OpusBandwidth.OPUS_BANDWIDTH_FULLBAND:
                        endband = 21;
                        break;
                }
                celt_decoder.opus_custom_decoder_ctl(celt_dec, CeltControl.CELT_SET_END_BAND_REQUEST, endband);
                celt_decoder.opus_custom_decoder_ctl(celt_dec, CeltControl.CELT_SET_CHANNELS_REQUEST, this.stream_channels);
            }

            if (redundancy != 0)
            {
                transition = 0;
                pcm_transition_silk_size = 0;
            }

            pcm_transition_silk = Pointer.Malloc<short>(pcm_transition_silk_size);

            if (transition != 0 && mode != OpusMode.MODE_CELT_ONLY)
            {
                pcm_transition = pcm_transition_silk;
                opus_decode_frame(null, 0, pcm_transition, Inlines.IMIN(F5, audiosize), 0);
            }

            /* Only allocation memory for redundancy if/when needed */
            redundant_audio_size = redundancy != 0 ? F5 * this.channels : 0;
            redundant_audio = Pointer.Malloc<short>(redundant_audio_size);

            /* 5 ms redundant frame for CELT.SILK*/
            if (redundancy != 0 && celt_to_silk != 0)
            {
                celt_decoder.opus_custom_decoder_ctl(celt_dec, CeltControl.CELT_SET_START_BAND_REQUEST, 0);
                celt_decoder.celt_decode_with_ec(celt_dec, data.Point(len), redundancy_bytes,
                                    redundant_audio, F5, null, 0);
                BoxedValue<uint> boxed_finalrange = new BoxedValue<uint>();
                celt_decoder.opus_custom_decoder_ctl(celt_dec, OpusControl.OPUS_GET_FINAL_RANGE_REQUEST, boxed_finalrange);
                redundant_rng = boxed_finalrange.Val;
            }

            /* MUST be after PLC */
            celt_decoder.opus_custom_decoder_ctl(celt_dec, CeltControl.CELT_SET_START_BAND_REQUEST, start_band);

            if (mode != OpusMode.MODE_SILK_ONLY)
            {
                int celt_frame_size = Inlines.IMIN(F20, frame_size);
                /* Make sure to discard any previous CELT state */
                if (mode != this.prev_mode && this.prev_mode > 0 && this.prev_redundancy == 0)
                    celt_decoder.opus_custom_decoder_ctl(celt_dec, OpusControl.OPUS_RESET_STATE);
                /* Decode CELT */
                celt_ret = celt_decoder.celt_decode_with_ec(celt_dec, decode_fec != 0 ? null : data,
                                               len, pcm, celt_frame_size, dec, celt_accum);
            }
            else
            {
                // fixme: make this static
                byte[] silence = { 0xFF, 0xFF };
                if (celt_accum == 0)
                {
                    for (i = 0; i < frame_size * this.channels; i++)
                        pcm[i] = 0;
                }
                /* For hybrid . SILK transitions, we let the CELT MDCT
                   do a fade-out by decoding a silence frame */
                if (this.prev_mode == OpusMode.MODE_HYBRID && !(redundancy != 0 && celt_to_silk != 0 && this.prev_redundancy != 0))
                {
                    celt_decoder.opus_custom_decoder_ctl(celt_dec, CeltControl.CELT_SET_START_BAND_REQUEST, 0);
                    celt_decoder.celt_decode_with_ec(celt_dec, silence.GetPointer(), 2, pcm, F2_5, null, celt_accum);
                }
            }

            if (mode != OpusMode.MODE_CELT_ONLY && celt_accum == 0)
            {
                for (i = 0; i < frame_size * this.channels; i++)
                    pcm[i] = Inlines.SAT16(Inlines.ADD32(pcm[i], pcm_silk[i]));
            }

            {
                BoxedValue<CeltMode> celt_mode = new BoxedValue<CeltMode>();
                celt_decoder.opus_custom_decoder_ctl(celt_dec, CeltControl.CELT_GET_MODE_REQUEST, celt_mode);
                window = celt_mode.Val.window;
            }

            /* 5 ms redundant frame for SILK.CELT */
            if (redundancy != 0 && celt_to_silk == 0)
            {
                celt_decoder.opus_custom_decoder_ctl(celt_dec, OpusControl.OPUS_RESET_STATE);
                celt_decoder.opus_custom_decoder_ctl(celt_dec, CeltControl.CELT_SET_START_BAND_REQUEST, 0);

                celt_decoder.celt_decode_with_ec(celt_dec, data.Point(len), redundancy_bytes, redundant_audio, F5, null, 0);
                BoxedValue<uint> boxed_range = new BoxedValue<uint>(redundant_rng);
                celt_decoder.opus_custom_decoder_ctl(celt_dec, OpusControl.OPUS_GET_FINAL_RANGE_REQUEST, boxed_range);
                redundant_rng = boxed_range.Val;
                CodecHelpers.smooth_fade(pcm.Point(this.channels * (frame_size - F2_5)), redundant_audio.Point(this.channels * F2_5),
                           pcm.Point(this.channels * (frame_size - F2_5)), F2_5, this.channels, window, this.Fs);
            }
            if (redundancy != 0 && celt_to_silk != 0)
            {
                for (c = 0; c < this.channels; c++)
                {
                    for (i = 0; i < F2_5; i++)
                        pcm[this.channels * i + c] = redundant_audio[this.channels * i + c];
                }
                CodecHelpers.smooth_fade(redundant_audio.Point(this.channels * F2_5), pcm.Point(this.channels * F2_5),
                            pcm.Point(this.channels * F2_5), F2_5, this.channels, window, this.Fs);
            }
            if (transition != 0)
            {
                if (audiosize >= F5)
                {
                    for (i = 0; i < this.channels * F2_5; i++)
                        pcm[i] = pcm_transition[i];
                    CodecHelpers.smooth_fade(pcm_transition.Point(this.channels * F2_5), pcm.Point(this.channels * F2_5),
                                pcm.Point(this.channels * F2_5), F2_5,
                                this.channels, window, this.Fs);
                }
                else {
                    /* Not enough time to do a clean transition, but we do it anyway
                       This will not preserve amplitude perfectly and may introduce
                       a bit of temporal aliasing, but it shouldn't be too bad and
                       that's pretty much the best we can do. In any case, generating this
                       transition it pretty silly in the first place */
                    CodecHelpers.smooth_fade(pcm_transition, pcm,
                                pcm, F2_5,
                                this.channels, window, this.Fs);
                }
            }

            if (this.decode_gain != 0)
            {
                int gain;
                gain = Inlines.celt_exp2(Inlines.MULT16_16_P15(Inlines.QCONST16(6.48814081e-4f, 25), this.decode_gain));
                for (i = 0; i < frame_size * this.channels; i++)
                {
                    int x;
                    x = Inlines.MULT16_32_P16(pcm[i], gain);
                    pcm[i] = (short)Inlines.SATURATE(x, 32767);
                }
            }

            if (len <= 1)
                this.rangeFinal = 0;
            else
                this.rangeFinal = dec.rng ^ redundant_rng;

            this.prev_mode = mode;
            this.prev_redundancy = (redundancy != 0 && celt_to_silk == 0) ? 1 : 0;

            return celt_ret < 0 ? celt_ret : audiosize;
        }

        internal int opus_decode_native(Pointer<byte> data,
          int len, Pointer<short> pcm, int frame_size, int decode_fec,
          int self_delimited, BoxedValue<int> packet_offset, int soft_clip)
        {
            int i, nb_samples;
            int count, offset;
            byte toc;
            int packet_frame_size, packet_bandwidth, packet_stream_channels;
            OpusMode packet_mode;
            /* 48 x 2.5 ms = 120 ms */
            // fixme: make sure these values can fit in an int16
            short[] size = new short[48];
            if (decode_fec < 0 || decode_fec > 1)
                return OpusError.OPUS_BAD_ARG;
            /* For FEC/PLC, frame_size has to be to have a multiple of 2.5 ms */
            if ((decode_fec != 0 || len == 0 || data == null) && frame_size % (this.Fs / 400) != 0)
                return OpusError.OPUS_BAD_ARG;
            if (len == 0 || data == null)
            {
                int pcm_count = 0;
                do
                {
                    int ret;
                    ret = opus_decode_frame(null, 0, pcm.Point(pcm_count * this.channels), frame_size - pcm_count, 0);
                    if (ret < 0)
                        return ret;
                    pcm_count += ret;
                } while (pcm_count < frame_size);
                Inlines.OpusAssert(pcm_count == frame_size);
                this.last_packet_duration = pcm_count;
                return pcm_count;
            }
            else if (len < 0)
                return OpusError.OPUS_BAD_ARG;

            packet_mode = OpusPacket.opus_packet_get_mode(data);
            packet_bandwidth = OpusPacket.opus_packet_get_bandwidth(data);
            packet_frame_size = OpusPacket.opus_packet_get_samples_per_frame(data, this.Fs);
            packet_stream_channels = OpusPacket.opus_packet_get_nb_channels(data);

            BoxedValue<byte> boxed_toc = new BoxedValue<byte>();
            BoxedValue<int> boxed_offset = new BoxedValue<int>();
            count = OpusPacket.opus_packet_parse_impl(data, len, self_delimited, boxed_toc, null,
                                           size.GetPointer(), boxed_offset, packet_offset);
            toc = boxed_toc.Val;
            offset = boxed_offset.Val;

            if (count < 0)
                return count;

            data = data.Point(offset);

            if (decode_fec != 0)
            {
                int duration_copy;
                int ret;
                /* If no FEC can be present, run the PLC (recursive call) */
                if (frame_size < packet_frame_size || packet_mode == OpusMode.MODE_CELT_ONLY || this.mode == OpusMode.MODE_CELT_ONLY)
                    return opus_decode_native(null, 0, pcm, frame_size, 0, 0, null, soft_clip);
                /* Otherwise, run the PLC on everything except the size for which we might have FEC */
                duration_copy = this.last_packet_duration;
                if (frame_size - packet_frame_size != 0)
                {
                    ret = opus_decode_native(null, 0, pcm, frame_size - packet_frame_size, 0, 0, null, soft_clip);
                    if (ret < 0)
                    {
                        this.last_packet_duration = duration_copy;
                        return ret;
                    }
                    Inlines.OpusAssert(ret == frame_size - packet_frame_size);
                }
                /* Complete with FEC */
                this.mode = packet_mode;
                this.bandwidth = packet_bandwidth;
                this.frame_size = packet_frame_size;
                this.stream_channels = packet_stream_channels;
                ret = opus_decode_frame(data, size[0], pcm.Point(this.channels * (frame_size - packet_frame_size)),
                      packet_frame_size, 1);
                if (ret < 0)
                    return ret;
                else {
                    this.last_packet_duration = frame_size;
                    return frame_size;
                }
            }

            if (count * packet_frame_size > frame_size)
                return OpusError.OPUS_BUFFER_TOO_SMALL;

            /* Update the state as the last step to avoid updating it on an invalid packet */
            this.mode = packet_mode;
            this.bandwidth = packet_bandwidth;
            this.frame_size = packet_frame_size;
            this.stream_channels = packet_stream_channels;

            nb_samples = 0;
            for (i = 0; i < count; i++)
            {
                int ret;
                ret = opus_decode_frame(data, size[i], pcm.Point(nb_samples * this.channels), frame_size - nb_samples, 0);
                if (ret < 0)
                    return ret;
                Inlines.OpusAssert(ret == packet_frame_size);
                data = data.Point(size[i]);
                nb_samples += ret;
            }
            this.last_packet_duration = nb_samples;

            return nb_samples;
        }

        /** Decode an Opus packet.
  * @param [in] st <tt>OpusDecoder*</tt>: Decoder state
  * @param [in] data <tt>char*</tt>: Input payload. Use a NULL pointer to indicate packet loss
  * @param [in] len <tt>opus_int32</tt>: Number of bytes in payload*
  * @param [out] pcm <tt>opus_int16*</tt>: Output signal (interleaved if 2 channels). length
  *  is frame_size*channels*sizeof(opus_int16)
  * @param [in] frame_size Number of samples per channel of available space in \a pcm.
  *  If this is less than the maximum packet duration (120ms; 5760 for 48kHz), this function will
  *  not be capable of decoding some packets. In the case of PLC (data==NULL) or FEC (decode_fec=1),
  *  then frame_size needs to be exactly the duration of audio that is missing, otherwise the
  *  decoder will not be in the optimal state to decode the next incoming packet. For the PLC and
  *  FEC cases, frame_size <b>must</b> be a multiple of 2.5 ms.
  * @param [in] decode_fec <tt>int</tt>: Flag (0 or 1) to request that any in-band forward error correction data be
  *  decoded. If no such data is available, the frame is decoded as if it were lost.
  * @returns Number of decoded samples or @ref opus_errorcodes
  */
        public int Decode(Pointer<byte> data,
             int len, Pointer<short> pcm, int frame_size, int decode_fec)
        {
            if (frame_size <= 0)
                return OpusError.OPUS_BAD_ARG;
            return opus_decode_native(data, len, pcm, frame_size, decode_fec, 0, null, 0);
        }

        public int Decode(Pointer<byte> data,
            int len, Pointer<float> pcm, int frame_size, int decode_fec)
        {
            Pointer<short> output;
            int ret, i;
            int nb_samples;

            if (frame_size <= 0)
            {
                return OpusError.OPUS_BAD_ARG;
            }
            if (data != null && len > 0 && decode_fec == 0)
            {
                nb_samples = OpusPacket.opus_decoder_get_nb_samples(this, data, len);
                if (nb_samples > 0)
                    frame_size = Inlines.IMIN(frame_size, nb_samples);
                else
                    return OpusError.OPUS_INVALID_PACKET;
            }
            output = Pointer.Malloc<short>(frame_size * this.channels);

            ret = opus_decode_native(data, len, output, frame_size, decode_fec, 0, null, 0);

            if (ret > 0)
            {
                for (i = 0; i < ret * this.channels; i++)
                    pcm[i] = (1.0f / 32768.0f) * (output[i]);
            }

            return ret;
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
            DecodeAPI.silk_InitDecoder(SilkDecoder);
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
