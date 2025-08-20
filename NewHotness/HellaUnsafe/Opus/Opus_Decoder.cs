
using System;
using System.Diagnostics;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.CeltDecoderH;
using static HellaUnsafe.Celt.CeltH;
using static HellaUnsafe.Celt.CELTModeH;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Opus.OpusPrivate;
using static HellaUnsafe.Opus.Opus;
using static HellaUnsafe.Silk.Control;
using static HellaUnsafe.Silk.DecAPI;
using static HellaUnsafe.Silk.Float.FloatCast;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Opus
{
    public static unsafe class Opus_Decoder
    {
        public unsafe struct OpusDecoder
        {
            internal int celt_dec_offset;
            internal int silk_dec_offset;
            internal int channels;
            internal int Fs;          /** Sampling rate (at the API level) */
            internal silk_DecControlStruct DecControl;
            internal int decode_gain;
            internal int complexity;

            /// <summary>
            /// The number of bytes from the start of the decoder struct to clear from on reset
            /// </summary>
            internal static int OPUS_DECODER_RESET_START =>
                //(void*)Unsafe.AsPointer(ref rng) - (void*)Unsafe.AsPointer(ref mode); // this doesn't work
                //Unsafe.ByteOffset(ref mode, ref rng); // neither does this
                sizeof(silk_DecControlStruct) + (6 * sizeof(int)); // whatever, just hardcode the lengths

            /* Everything beyond this point gets cleared on a reset */

            internal int stream_channels;

            internal int bandwidth;
            internal int mode;
            internal int prev_mode;
            internal int frame_size;
            internal int prev_redundancy;
            internal int last_packet_duration;
            internal fixed float softclip_mem[2];
            internal uint rangeFinal;
        }

        [Conditional("DEBUG")]
        internal static unsafe void validate_opus_decoder(OpusDecoder* st)
        {
            celt_assert(st->channels == 1 || st->channels == 2);
            celt_assert(st->Fs == 48000 || st->Fs == 24000 || st->Fs == 16000 || st->Fs == 12000 || st->Fs == 8000);
            celt_assert(st->DecControl.API_sampleRate == st->Fs);
            celt_assert(st->DecControl.internalSampleRate == 0 || st->DecControl.internalSampleRate == 16000 || st->DecControl.internalSampleRate == 12000 || st->DecControl.internalSampleRate == 8000);
            celt_assert(st->DecControl.nChannelsAPI == st->channels);
            celt_assert(st->DecControl.nChannelsInternal == 0 || st->DecControl.nChannelsInternal == 1 || st->DecControl.nChannelsInternal == 2);
            celt_assert(st->DecControl.payloadSize_ms == 0 || st->DecControl.payloadSize_ms == 10 || st->DecControl.payloadSize_ms == 20 || st->DecControl.payloadSize_ms == 40 || st->DecControl.payloadSize_ms == 60);
            celt_assert(st->stream_channels == 1 || st->stream_channels == 2);
        }

        internal static unsafe int opus_decoder_get_size(int channels)
        {
            int silkDecSizeBytes, celtDecSizeBytes;
            int ret;
            if (channels < 1 || channels > 2)
                return 0;
            ret = silk_Get_Decoder_Size(&silkDecSizeBytes);
            if (ret != 0)
                return 0;
            silkDecSizeBytes = align(silkDecSizeBytes);
            celtDecSizeBytes = celt_decoder_get_size(channels);
            return align(sizeof(OpusDecoder)) + silkDecSizeBytes + celtDecSizeBytes;
        }

        internal static unsafe int opus_decoder_init(OpusDecoder* st, int Fs, int channels)
        {
            void* silk_dec;
            OpusCustomDecoder* celt_dec;
            int ret, silkDecSizeBytes;

            if ((Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000)
             || (channels != 1 && channels != 2))
                return OPUS_BAD_ARG;

            OPUS_CLEAR((byte*)st, opus_decoder_get_size(channels));
            /* Initialize SILK decoder */
            ret = silk_Get_Decoder_Size(&silkDecSizeBytes);
            if (ret != 0)
                return OPUS_INTERNAL_ERROR;

            silkDecSizeBytes = align(silkDecSizeBytes);
            st->silk_dec_offset = align(sizeof(OpusDecoder));
            st->celt_dec_offset = st->silk_dec_offset + silkDecSizeBytes;
            silk_dec = (byte*)st + st->silk_dec_offset;
            celt_dec = (OpusCustomDecoder*)((byte*)st + st->celt_dec_offset);
            st->stream_channels = st->channels = channels;
            st->complexity = 0;

            st->Fs = Fs;
            st->DecControl.API_sampleRate = st->Fs;
            st->DecControl.nChannelsAPI = st->channels;

            /* Reset decoder */
            ret = silk_InitDecoder(silk_dec);
            if (ret != 0)
                return OPUS_INTERNAL_ERROR;

            /* Initialize CELT decoder */
            ret = celt_decoder_init(celt_dec, Fs, channels);
            if (ret != OPUS_OK)
                return OPUS_INTERNAL_ERROR;

            opus_custom_decoder_ctl(celt_dec, CELT_SET_SIGNALLING_REQUEST, 0);

            st->prev_mode = 0;
            st->frame_size = Fs / 400;
            return OPUS_OK;
        }

        public static unsafe OpusDecoder* opus_decoder_create(int Fs, int channels, int* error)
        {
            int ret;
            OpusDecoder* st;
            if ((Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000)
             || (channels != 1 && channels != 2))
            {
                if (error != null)
                    *error = OPUS_BAD_ARG;
                return null;
            }
            st = (OpusDecoder*)opus_alloc(opus_decoder_get_size(channels));
            if (st == null)
            {
                if (error != null)
                    *error = OPUS_ALLOC_FAIL;
                return null;
            }
            ret = opus_decoder_init(st, Fs, channels);
            if (error != null)
                *error = ret;
            if (ret != OPUS_OK)
            {
                opus_free(st);
                st = null;
            }
            return st;
        }

        internal static unsafe void smooth_fade(in float* in1, in float* in2,
              float* output, int overlap, int channels,
              in float* window, int Fs)
        {
            int i, c;
            int inc = 48000 / Fs;
            for (c = 0; c < channels; c++)
            {
                for (i = 0; i < overlap; i++)
                {
                    float w = MULT16_16_Q15(window[i * inc], window[i * inc]);
                    output[i * channels + c] = SHR32(MAC16_16(MULT16_16(w, in2[i * channels + c]),
                                              Q15ONE - w, in1[i * channels + c]), 15);
                }
            }
        }

        internal static unsafe int opus_packet_get_mode(in byte* data)
        {
            int mode;
            if ((data[0] & 0x80) != 0)
            {
                mode = MODE_CELT_ONLY;
            }
            else if ((data[0] & 0x60) == 0x60)
            {
                mode = MODE_HYBRID;
            }
            else
            {
                mode = MODE_SILK_ONLY;
            }
            return mode;
        }

        internal static unsafe int opus_decode_frame(OpusDecoder* st, byte* data,
              int len, float* pcm, int frame_size, int decode_fec)
        {
            void* silk_dec;
            OpusCustomDecoder* celt_dec;
            int i, silk_ret = 0, celt_ret = 0;
            ec_ctx dec = default;
            int silk_frame_size;
            int pcm_silk_size;
            int pcm_transition_silk_size;
            int pcm_transition_celt_size;
            float* pcm_transition = null;
            int redundant_audio_size;

            int audiosize;
            int mode;
            int bandwidth;
            int transition = 0;
            int start_band;
            int redundancy = 0;
            int redundancy_bytes = 0;
            int celt_to_silk = 0;
            int c;
            int F2_5, F5, F10, F20;
            float* window;
            uint redundant_rng = 0;
            int celt_accum;

            silk_dec = (byte*)st + st->silk_dec_offset;
            celt_dec = (OpusCustomDecoder*)((byte*)st + st->celt_dec_offset);
            F20 = st->Fs / 50;
            F10 = F20 >> 1;
            F5 = F10 >> 1;
            F2_5 = F5 >> 1;
            if (frame_size < F2_5)
            {
                return OPUS_BUFFER_TOO_SMALL;
            }
            /* Limit frame_size to avoid excessive stack allocations. */
            frame_size = IMIN(frame_size, st->Fs / 25 * 3);
            /* Payloads of 1 (2 including ToC) or 0 trigger the PLC/DTX */
            if (len <= 1)
            {
                data = null;
                /* In that case, don't conceal more than what the ToC says */
                frame_size = IMIN(frame_size, st->frame_size);
            }
            if (data != null)
            {
                audiosize = st->frame_size;
                mode = st->mode;
                bandwidth = st->bandwidth;
                ec_dec_init(&dec, (byte*)data, (uint)len);
            }
            else
            {
                audiosize = frame_size;
                /* Run PLC using last used mode (CELT if we ended with CELT redundancy) */
                mode = st->prev_redundancy != 0 ? MODE_CELT_ONLY : st->prev_mode;
                bandwidth = 0;

                if (mode == 0)
                {
                    /* If we haven't got any packet yet, all we can do is return zeros */
                    for (i = 0; i < audiosize * st->channels; i++)
                        pcm[i] = 0;
                    return audiosize;
                }

                /* Avoids trying to run the PLC on sizes other than 2.5 (CELT), 5 (CELT),
                   10, or 20 (e.g. 12.5 or 30 ms). */
                if (audiosize > F20)
                {
                    do
                    {
                        int ret = opus_decode_frame(st, null, 0, pcm, IMIN(audiosize, F20), 0);
                        if (ret < 0)
                        {
                            return ret;
                        }
                        pcm += ret * st->channels;
                        audiosize -= ret;
                    } while (audiosize > 0);
                    return frame_size;
                }
                else if (audiosize < F20)
                {
                    if (audiosize > F10)
                        audiosize = F10;
                    else if (mode != MODE_SILK_ONLY && audiosize > F5 && audiosize < F10)
                        audiosize = F5;
                }
            }

            /* In fixed-point, we can tell CELT to do the accumulation on top of the
               SILK PCM buffer. This saves some stack space. */
            celt_accum = 0;

            pcm_transition_silk_size = 0;
            pcm_transition_celt_size = 0;
            if (data != null && st->prev_mode > 0 && (
                (mode == MODE_CELT_ONLY && st->prev_mode != MODE_CELT_ONLY && st->prev_redundancy == 0)
             || (mode != MODE_CELT_ONLY && st->prev_mode == MODE_CELT_ONLY))
               )
            {
                transition = 1;
                /* Decide where to allocate the stack memory for pcm_transition */
                if (mode == MODE_CELT_ONLY)
                    pcm_transition_celt_size = F5 * st->channels;
                else
                    pcm_transition_silk_size = F5 * st->channels;
            }

            float[] pcm_transition_celt_data = pcm_transition_celt_size > 0 ? new float[pcm_transition_celt_size] : Array.Empty<float>();
            fixed (float* pcm_transition_celt = pcm_transition_celt_data)
            {
                if (transition != 0 && mode == MODE_CELT_ONLY)
                {
                    pcm_transition = pcm_transition_celt;
                    opus_decode_frame(st, null, 0, pcm_transition, IMIN(F5, audiosize), 0);
                }
                if (audiosize > frame_size)
                {
                    /*fprintf(stderr, "PCM buffer too small: %d vs %d (mode = %d)\n", audiosize, frame_size, mode);*/
                    return OPUS_BAD_ARG;
                }
                else
                {
                    frame_size = audiosize;
                }

                /* Don't allocate any memory when in CELT-only mode */
                pcm_silk_size = (mode != MODE_CELT_ONLY && celt_accum == 0) ? IMAX(F10, frame_size) * st->channels : 0;

                short[] pcm_silk_data = pcm_silk_size > 0 ? new short[pcm_silk_size] : Array.Empty<short>();
                fixed (short* pcm_silk = pcm_silk_data)
                {
                    /* SILK processing */
                    if (mode != MODE_CELT_ONLY)
                    {
                        int lost_flag, decoded_samples;
                        short* pcm_ptr;
                        pcm_ptr = pcm_silk;

                        if (st->prev_mode == MODE_CELT_ONLY)
                            silk_ResetDecoder(silk_dec);

                        /* The SILK PLC cannot produce frames of less than 10 ms */
                        st->DecControl.payloadSize_ms = IMAX(10, 1000 * audiosize / st->Fs);

                        if (data != null)
                        {
                            st->DecControl.nChannelsInternal = st->stream_channels;
                            if (mode == MODE_SILK_ONLY)
                            {
                                if (bandwidth == OPUS_BANDWIDTH_NARROWBAND)
                                {
                                    st->DecControl.internalSampleRate = 8000;
                                }
                                else if (bandwidth == OPUS_BANDWIDTH_MEDIUMBAND)
                                {
                                    st->DecControl.internalSampleRate = 12000;
                                }
                                else if (bandwidth == OPUS_BANDWIDTH_WIDEBAND)
                                {
                                    st->DecControl.internalSampleRate = 16000;
                                }
                                else
                                {
                                    st->DecControl.internalSampleRate = 16000;
                                    celt_assert(false);
                                }
                            }
                            else
                            {
                                /* Hybrid mode */
                                st->DecControl.internalSampleRate = 16000;
                            }
                        }
                        st->DecControl.enable_deep_plc = BOOL2INT(st->complexity >= 5);

                        lost_flag = data == null ? 1 : 2 * BOOL2INT(decode_fec != 0);
                        decoded_samples = 0;
                        do
                        {
                            /* Call SILK decoder */
                            int first_frame = BOOL2INT(decoded_samples == 0);
                            silk_ret = silk_Decode(silk_dec, &st->DecControl,
                                                    lost_flag, first_frame, &dec, pcm_ptr, &silk_frame_size);
                            if (silk_ret != 0)
                            {
                                if (lost_flag != 0)
                                {
                                    /* PLC failure should not be fatal */
                                    silk_frame_size = frame_size;
                                    for (i = 0; i < frame_size * st->channels; i++)
                                        pcm_ptr[i] = 0;
                                }
                                else
                                {
                                    return OPUS_INTERNAL_ERROR;
                                }
                            }
                            pcm_ptr += silk_frame_size * st->channels;
                            decoded_samples += silk_frame_size;
                        } while (decoded_samples < frame_size);
                    }

                    start_band = 0;
                    if (decode_fec == 0 && mode != MODE_CELT_ONLY && data != null
                     && ec_tell(&dec) + 17 + 20 * BOOL2INT(mode == MODE_HYBRID) <= 8 * len)
                    {
                        /* Check if we have a redundant 0-8 kHz band */
                        if (mode == MODE_HYBRID)
                            redundancy = ec_dec_bit_logp(&dec, 12);
                        else
                            redundancy = 1;
                        if (redundancy != 0)
                        {
                            celt_to_silk = ec_dec_bit_logp(&dec, 1);
                            /* redundancy_bytes will be at least two, in the non-hybrid
                               case due to the ec_tell() check above */
                            redundancy_bytes = mode == MODE_HYBRID ?
                                  (int)ec_dec_uint(&dec, 256) + 2 :
                                  len - ((ec_tell(&dec) + 7) >> 3);
                            len -= redundancy_bytes;
                            /* This is a sanity check. It should never happen for a valid
                               packet, so the exact behaviour is not normative. */
                            if (len * 8 < ec_tell(&dec))
                            {
                                len = 0;
                                redundancy_bytes = 0;
                                redundancy = 0;
                            }
                            /* Shrink decoder because of raw bits */
                            dec.storage -= (uint)redundancy_bytes;
                        }
                    }
                    if (mode != MODE_CELT_ONLY)
                        start_band = 17;

                    if (redundancy != 0)
                    {
                        transition = 0;
                        pcm_transition_silk_size = 0;
                    }

                    float[] pcm_transition_silk_data = pcm_transition_silk_size > 0 ? new float[pcm_transition_silk_size] : Array.Empty<float>();
                    fixed (float* pcm_transition_silk = pcm_transition_silk_data)
                    {

                        if (transition != 0 && mode != MODE_CELT_ONLY)
                        {
                            pcm_transition = pcm_transition_silk;
                            opus_decode_frame(st, null, 0, pcm_transition, IMIN(F5, audiosize), 0);
                        }


                        if (bandwidth != 0)
                        {
                            int endband = 21;

                            switch (bandwidth)
                            {
                                case OPUS_BANDWIDTH_NARROWBAND:
                                    endband = 13;
                                    break;
                                case OPUS_BANDWIDTH_MEDIUMBAND:
                                case OPUS_BANDWIDTH_WIDEBAND:
                                    endband = 17;
                                    break;
                                case OPUS_BANDWIDTH_SUPERWIDEBAND:
                                    endband = 19;
                                    break;
                                case OPUS_BANDWIDTH_FULLBAND:
                                    endband = 21;
                                    break;
                                default:
                                    celt_assert(false);
                                    break;
                            }
                            MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, CELT_SET_END_BAND_REQUEST, endband));
                        }
                        MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, CELT_SET_CHANNELS_REQUEST, st->stream_channels));

                        /* Only allocation memory for redundancy if/when needed */
                        redundant_audio_size = redundancy != 0 ? F5 * st->channels : 0;

                        float[] redundant_audio_data = redundant_audio_size > 0 ? new float[redundant_audio_size] : Array.Empty<float>();
                        fixed (float* redundant_audio = redundant_audio_data)
                        {

                            /* 5 ms redundant frame for CELT->SILK*/
                            if (redundancy != 0 && celt_to_silk != 0)
                            {
                                /* If the previous frame did not use CELT (the first redundancy frame in
                                   a transition from SILK may have been lost) then the CELT decoder is
                                   stale at this point and the redundancy audio is not useful, however
                                   the final range is still needed (for testing), so the redundancy is
                                   always decoded but the decoded audio may not be used */
                                MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, CELT_SET_START_BAND_REQUEST, 0));
                                celt_decode_with_ec(celt_dec, data + len, redundancy_bytes,
                                                    redundant_audio, F5, null, 0);
                                MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, OPUS_GET_FINAL_RANGE_REQUEST, &redundant_rng));
                            }

                            /* MUST be after PLC */
                            MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, CELT_SET_START_BAND_REQUEST, start_band));

                            if (mode != MODE_SILK_ONLY)
                            {
                                int celt_frame_size = IMIN(F20, frame_size);
                                /* Make sure to discard any previous CELT state */
                                if (mode != st->prev_mode && st->prev_mode > 0 && st->prev_redundancy == 0)
                                    MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, OPUS_RESET_STATE));
                                /* Decode CELT */
                                celt_ret = celt_decode_with_ec_dred(celt_dec, decode_fec != 0 ? null : data,
                                                               len, pcm, celt_frame_size, &dec, celt_accum
                                                               );
                            }
                            else
                            {
                                byte* silence = stackalloc byte[2];
                                new Span<byte>(silence, 2).Fill(0xFF);
                                if (celt_accum == 0)
                                {
                                    for (i = 0; i < frame_size * st->channels; i++)
                                        pcm[i] = 0;
                                }
                                /* For hybrid -> SILK transitions, we let the CELT MDCT
                                   do a fade-out by decoding a silence frame */
                                if (st->prev_mode == MODE_HYBRID && !(redundancy != 0 && celt_to_silk != 0 && st->prev_redundancy != 0))
                                {
                                    MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, CELT_SET_START_BAND_REQUEST, 0));
                                    celt_decode_with_ec(celt_dec, silence, 2, pcm, F2_5, null, celt_accum);
                                }
                            }

                            if (mode != MODE_CELT_ONLY && celt_accum == 0)
                            {
                                for (i = 0; i < frame_size * st->channels; i++)
                                    pcm[i] = pcm[i] + (float)((1.0f / 32768.0f) * pcm_silk[i]);
                            }

                            {
                                OpusCustomMode* celt_mode = null;
                                MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, CELT_GET_MODE_REQUEST, &celt_mode));
                                window = celt_mode->window;
                            }

                            /* 5 ms redundant frame for SILK->CELT */
                            if (redundancy != 0 && celt_to_silk == 0)
                            {
                                MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, OPUS_RESET_STATE));
                                MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, CELT_SET_START_BAND_REQUEST, 0));

                                celt_decode_with_ec(celt_dec, data + len, redundancy_bytes, redundant_audio, F5, null, 0);
                                MUST_SUCCEED(opus_custom_decoder_ctl(celt_dec, OPUS_GET_FINAL_RANGE_REQUEST, &redundant_rng));
                                smooth_fade(pcm + st->channels * (frame_size - F2_5), redundant_audio + st->channels * F2_5,
                                            pcm + st->channels * (frame_size - F2_5), F2_5, st->channels, window, st->Fs);
                            }
                            /* 5ms redundant frame for CELT->SILK; ignore if the previous frame did not
                               use CELT (the first redundancy frame in a transition from SILK may have
                               been lost) */
                            if (redundancy != 0 && celt_to_silk != 0 && (st->prev_mode != MODE_SILK_ONLY || st->prev_redundancy != 0))
                            {
                                for (c = 0; c < st->channels; c++)
                                {
                                    for (i = 0; i < F2_5; i++)
                                        pcm[st->channels * i + c] = redundant_audio[st->channels * i + c];
                                }
                                smooth_fade(redundant_audio + st->channels * F2_5, pcm + st->channels * F2_5,
                                            pcm + st->channels * F2_5, F2_5, st->channels, window, st->Fs);
                            }
                            if (transition != 0)
                            {
                                if (audiosize >= F5)
                                {
                                    for (i = 0; i < st->channels * F2_5; i++)
                                        pcm[i] = pcm_transition[i];
                                    smooth_fade(pcm_transition + st->channels * F2_5, pcm + st->channels * F2_5,
                                                pcm + st->channels * F2_5, F2_5,
                                                st->channels, window, st->Fs);
                                }
                                else
                                {
                                    /* Not enough time to do a clean transition, but we do it anyway
                                       This will not preserve amplitude perfectly and may introduce
                                       a bit of temporal aliasing, but it shouldn't be too bad and
                                       that's pretty much the best we can do. In any case, generating this
                                       transition it pretty silly in the first place */
                                    smooth_fade(pcm_transition, pcm,
                                                pcm, F2_5,
                                                st->channels, window, st->Fs);
                                }
                            }

                            if (st->decode_gain != 0)
                            {
                                float gain;
                                gain = celt_exp2(MULT16_16_P15(QCONST16(6.48814081e-4f, 25), st->decode_gain));
                                for (i = 0; i < frame_size * st->channels; i++)
                                {
                                    float x;
                                    x = MULT16_32_P16(pcm[i], gain);
                                    pcm[i] = SATURATE(x, 32767);
                                }
                            }

                            if (len <= 1)
                                st->rangeFinal = 0;
                            else
                                st->rangeFinal = dec.rng ^ redundant_rng;

                            st->prev_mode = mode;
                            st->prev_redundancy = BOOL2INT(redundancy != 0 && celt_to_silk == 0);

                            if (celt_ret >= 0)
                            {
                                //if (OPUS_CHECK_ARRAY(pcm, audiosize*st->channels))
                                //   OPUS_PRINT_INT(audiosize);
                            }

                            return celt_ret < 0 ? celt_ret : audiosize;
                        }
                    }
                }
            }
        }

        internal static unsafe int opus_decode_native(OpusDecoder* st, byte* data,
              int len, float* pcm, int frame_size, int decode_fec,
              int self_delimited, int* packet_offset, int soft_clip)
        {
            int i, nb_samples;
            int count, offset;
            byte toc;
            int packet_frame_size, packet_bandwidth, packet_mode, packet_stream_channels;
            /* 48 x 2.5 ms = 120 ms */
            short* size = stackalloc short[48];
            validate_opus_decoder(st);
            if (decode_fec < 0 || decode_fec > 1)
                return OPUS_BAD_ARG;
            /* For FEC/PLC, frame_size has to be to have a multiple of 2.5 ms */
            if ((decode_fec != 0 || len == 0 || data == null) && frame_size % (st->Fs / 400) != 0)
                return OPUS_BAD_ARG;
            if (len == 0 || data == null)
            {
                int pcm_count = 0;
                do
                {
                    int ret;
                    ret = opus_decode_frame(st, null, 0, pcm + pcm_count * st->channels, frame_size - pcm_count, 0);
                    if (ret < 0)
                        return ret;
                    pcm_count += ret;
                } while (pcm_count < frame_size);
                celt_assert(pcm_count == frame_size);
                //if (OPUS_CHECK_ARRAY(pcm, pcm_count * st->channels))
                //    OPUS_PRINT_INT(pcm_count);
                st->last_packet_duration = pcm_count;
                return pcm_count;
            }
            else if (len < 0)
                return OPUS_BAD_ARG;

            packet_mode = opus_packet_get_mode(data);
            packet_bandwidth = opus_packet_get_bandwidth(data);
            packet_frame_size = opus_packet_get_samples_per_frame(data, st->Fs);
            packet_stream_channels = opus_packet_get_nb_channels(data);

            count = opus_packet_parse_impl(data, len, self_delimited, &toc, null,
                                           size, &offset, packet_offset, null, null);
            if (count < 0)
                return count;

            data += offset;

            if (decode_fec != 0)
            {
                int duration_copy;
                int ret;
                /* If no FEC can be present, run the PLC (recursive call) */
                if (frame_size < packet_frame_size || packet_mode == MODE_CELT_ONLY || st->mode == MODE_CELT_ONLY)
                    return opus_decode_native(st, null, 0, pcm, frame_size, 0, 0, null, soft_clip);
                /* Otherwise, run the PLC on everything except the size for which we might have FEC */
                duration_copy = st->last_packet_duration;
                if (frame_size - packet_frame_size != 0)
                {
                    ret = opus_decode_native(st, null, 0, pcm, frame_size - packet_frame_size, 0, 0, null, soft_clip);
                    if (ret < 0)
                    {
                        st->last_packet_duration = duration_copy;
                        return ret;
                    }
                    celt_assert(ret == frame_size - packet_frame_size);
                }
                /* Complete with FEC */
                st->mode = packet_mode;
                st->bandwidth = packet_bandwidth;
                st->frame_size = packet_frame_size;
                st->stream_channels = packet_stream_channels;
                ret = opus_decode_frame(st, data, size[0], pcm + st->channels * (frame_size - packet_frame_size),
                      packet_frame_size, 1);
                if (ret < 0)
                    return ret;
                else
                {
                    //if (OPUS_CHECK_ARRAY(pcm, frame_size * st->channels))
                    //    OPUS_PRINT_INT(frame_size);
                    st->last_packet_duration = frame_size;
                    return frame_size;
                }
            }

            if (count * packet_frame_size > frame_size)
                return OPUS_BUFFER_TOO_SMALL;

            /* Update the state as the last step to avoid updating it on an invalid packet */
            st->mode = packet_mode;
            st->bandwidth = packet_bandwidth;
            st->frame_size = packet_frame_size;
            st->stream_channels = packet_stream_channels;

            nb_samples = 0;
            for (i = 0; i < count; i++)
            {
                int ret;
                ret = opus_decode_frame(st, data, size[i], pcm + nb_samples * st->channels, frame_size - nb_samples, 0);
                if (ret < 0)
                    return ret;
                celt_assert(ret == packet_frame_size);
                data += size[i];
                nb_samples += ret;
            }
            st->last_packet_duration = nb_samples;
            //if (OPUS_CHECK_ARRAY(pcm, nb_samples * st->channels))
            //    OPUS_PRINT_INT(nb_samples);
            if (soft_clip != 0)
                opus_pcm_soft_clip(pcm, nb_samples, st->channels, st->softclip_mem);
            else
                st->softclip_mem[0] = st->softclip_mem[1] = 0;
            return nb_samples;
        }

        public static unsafe int opus_decode(OpusDecoder* st, in byte* data,
              int len, short* pcm, int frame_size, int decode_fec)
        {
            int ret, i;
            int nb_samples;

            if (frame_size <= 0)
            {
                return OPUS_BAD_ARG;
            }

            if (data != null && len > 0 && decode_fec == 0)
            {
                nb_samples = opus_decoder_get_nb_samples(st, data, len);
                if (nb_samples > 0)
                    frame_size = IMIN(frame_size, nb_samples);
                else
                    return OPUS_INVALID_PACKET;
            }
            celt_assert(st->channels == 1 || st->channels == 2);
            float[] output_data = new float[frame_size * st->channels];
            fixed (float* output = output_data)
            {
                ret = opus_decode_native(st, data, len, output, frame_size, decode_fec, 0, null, 1);
                if (ret > 0)
                {
                    for (i = 0; i < ret * st->channels; i++)
                        pcm[i] = FLOAT2INT16(output[i]);
                }

                return ret;
            }
        }

        public static unsafe int opus_decode_float(OpusDecoder* st, in byte* data,
              int len, float* pcm, int frame_size, int decode_fec)
        {
            if (frame_size <= 0)
                return OPUS_BAD_ARG;
            return opus_decode_native(st, data, len, pcm, frame_size, decode_fec, 0, null, 0);
        }

        /// <summary>
        /// Int parameter (most setters)
        /// </summary>
        public static unsafe int opus_decoder_ctl(OpusDecoder* st, int request, int value)
        {
            int ret = OPUS_OK;
            void* silk_dec;
            OpusCustomDecoder* celt_dec;

            silk_dec = (byte*)st + st->silk_dec_offset;
            celt_dec = (OpusCustomDecoder*)((byte*)st + st->celt_dec_offset);

            switch (request)
            {
                case OPUS_SET_COMPLEXITY_REQUEST:
                    {
                        if (value < 0 || value > 10)
                        {
                            goto bad_arg;
                        }
                        st->complexity = value;
                        opus_custom_decoder_ctl(celt_dec, OPUS_SET_COMPLEXITY_REQUEST, value);
                    }
                    break;
                case OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST:
                    {
                        if (value < 0 || value > 1)
                        {
                            goto bad_arg;
                        }
                        ret = opus_custom_decoder_ctl(celt_dec, OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST, value);
                    }
                    break;
                case OPUS_SET_GAIN_REQUEST:
                    {
                        if (value < -32768 || value > 32767)
                        {
                            goto bad_arg;
                        }
                        st->decode_gain = value;
                    }
                    break;
                default:
                    /*fprintf(stderr, "unknown opus_decoder_ctl() request: %d", request);*/
                    ret = OPUS_UNIMPLEMENTED;
                    break;
            }

            return ret;
        bad_arg:
            return OPUS_BAD_ARG;
        }

        /// <summary>
        /// Int* parameter (most getters)
        /// </summary>
        public static unsafe int opus_decoder_ctl(OpusDecoder* st, int request, int* value)
        {
            int ret = OPUS_OK;
            void* silk_dec;
            OpusCustomDecoder* celt_dec;

            silk_dec = (byte*)st + st->silk_dec_offset;
            celt_dec = (OpusCustomDecoder*)((byte*)st + st->celt_dec_offset);

            switch (request)
            {
                case OPUS_GET_BANDWIDTH_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->bandwidth;
                    }
                    break;
                case OPUS_GET_COMPLEXITY_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->complexity;
                    }
                    break;
                case OPUS_GET_SAMPLE_RATE_REQUEST:
                    {
                        if (value != null)
                        {
                            goto bad_arg;
                        }
                        *value = st->Fs;
                    }
                    break;
                case OPUS_GET_PITCH_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        if (st->prev_mode == MODE_CELT_ONLY)
                            ret = opus_custom_decoder_ctl(celt_dec, OPUS_GET_PITCH_REQUEST, value);
                        else
                            *value = st->DecControl.prevPitchLag;
                    }
                    break;
                case OPUS_GET_GAIN_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->decode_gain;
                    }
                    break;
                case OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        ret = opus_custom_decoder_ctl(celt_dec, OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST, value);
                    }
                    break;
                case OPUS_GET_LAST_PACKET_DURATION_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->last_packet_duration;
                    }
                    break;
                default:
                    /*fprintf(stderr, "unknown opus_decoder_ctl() request: %d", request);*/
                    ret = OPUS_UNIMPLEMENTED;
                    break;
            }

            return ret;
        bad_arg:
            return OPUS_BAD_ARG;
        }

        /// <summary>
        /// Override for OPUS_GET_FINAL_RANGE_REQUEST
        /// </summary>
        public static unsafe int opus_decoder_ctl(OpusDecoder* st, int request, uint* value)
        {
            int ret = OPUS_OK;
            void* silk_dec;
            OpusCustomDecoder* celt_dec;

            silk_dec = (byte*)st + st->silk_dec_offset;
            celt_dec = (OpusCustomDecoder*)((byte*)st + st->celt_dec_offset);

            switch (request)
            {
                case OPUS_GET_FINAL_RANGE_REQUEST:
                    {
                        if (value == null)
                        {
                            goto bad_arg;
                        }
                        *value = st->rangeFinal;
                    }
                    break;
                default:
                    /*fprintf(stderr, "unknown opus_decoder_ctl() request: %d", request);*/
                    ret = OPUS_UNIMPLEMENTED;
                    break;
            }

            return ret;
        bad_arg:
            return OPUS_BAD_ARG;
        }

        /// <summary>
        /// Specific handler for OPUS_RESET_STATE
        /// </summary>
        public static unsafe int opus_decoder_ctl(OpusDecoder* st, int request)
        {
            int ret = OPUS_OK;
            void* silk_dec;
            OpusCustomDecoder* celt_dec;

            silk_dec = (byte*)st + st->silk_dec_offset;
            celt_dec = (OpusCustomDecoder*)((byte*)st + st->celt_dec_offset);

            switch (request)
            {
                case OPUS_RESET_STATE:
                    {
                        OPUS_CLEAR(
                            ((byte*)st) + OpusDecoder.OPUS_DECODER_RESET_START,
                            sizeof(OpusDecoder) -
                            OpusDecoder.OPUS_DECODER_RESET_START);

                        opus_custom_decoder_ctl(celt_dec, OPUS_RESET_STATE);
                        silk_ResetDecoder(silk_dec);
                        st->stream_channels = st->channels;
                        st->frame_size = st->Fs / 400;
                    }
                    break;
                default:
                    /*fprintf(stderr, "unknown opus_decoder_ctl() request: %d", request);*/
                    ret = OPUS_UNIMPLEMENTED;
                    break;
            }

            return ret;
        }

        public static unsafe void opus_decoder_destroy(OpusDecoder* st)
        {
            opus_free(st);
        }


        internal static unsafe int opus_packet_get_bandwidth(in byte* data)
        {
            int bandwidth;
            if ((data[0] & 0x80) != 0)
            {
                bandwidth = OPUS_BANDWIDTH_MEDIUMBAND + ((data[0] >> 5) & 0x3);
                if (bandwidth == OPUS_BANDWIDTH_MEDIUMBAND)
                    bandwidth = OPUS_BANDWIDTH_NARROWBAND;
            }
            else if ((data[0] & 0x60) == 0x60)
            {
                bandwidth = (data[0] & 0x10) != 0 ? OPUS_BANDWIDTH_FULLBAND :
                                             OPUS_BANDWIDTH_SUPERWIDEBAND;
            }
            else
            {
                bandwidth = OPUS_BANDWIDTH_NARROWBAND + ((data[0] >> 5) & 0x3);
            }
            return bandwidth;
        }

        internal static unsafe int opus_packet_get_nb_channels(in byte* data)
        {
            return (data[0] & 0x4) != 0 ? 2 : 1;
        }

        internal static unsafe int opus_packet_get_nb_frames(in byte* packet, int len)
        {
            int count;
            if (len < 1)
                return OPUS_BAD_ARG;
            count = packet[0] & 0x3;
            if (count == 0)
                return 1;
            else if (count != 3)
                return 2;
            else if (len < 2)
                return OPUS_INVALID_PACKET;
            else
                return packet[1] & 0x3F;
        }

        internal static unsafe int opus_packet_get_nb_samples(in byte* packet, int len,
              int Fs)
        {
            int samples;
            int count = opus_packet_get_nb_frames(packet, len);

            if (count < 0)
                return count;

            samples = count * opus_packet_get_samples_per_frame(packet, Fs);
            /* Can't have more than 120 ms */
            if (samples * 25 > Fs * 3)
                return OPUS_INVALID_PACKET;
            else
                return samples;
        }

        internal static unsafe int opus_packet_has_lbrr(in byte* packet, int len)
        {
            int ret;
            byte** frames = stackalloc byte*[48];
            short* size = stackalloc short[48];
            int packet_mode, packet_frame_size, packet_stream_channels;
            int nb_frames = 1;
            int lbrr;

            packet_mode = opus_packet_get_mode(packet);
            if (packet_mode == MODE_CELT_ONLY)
                return 0;
            packet_frame_size = opus_packet_get_samples_per_frame(packet, 48000);
            if (packet_frame_size > 960)
                nb_frames = packet_frame_size / 960;
            packet_stream_channels = opus_packet_get_nb_channels(packet);
            ret = opus_packet_parse(packet, len, null, frames, size, null);
            if (ret <= 0)
                return ret;
            lbrr = (frames[0][0] >> (7 - nb_frames)) & 0x1;
            if (packet_stream_channels == 2)
                lbrr = BOOL2INT((lbrr != 0) || (((frames[0][0] >> (6 - 2 * nb_frames)) & 0x1) != 0));
            return lbrr;
        }

        internal static unsafe int opus_decoder_get_nb_samples(in OpusDecoder* dec,
              in byte* packet, int len)
        {
            return opus_packet_get_nb_samples(packet, len, dec->Fs);
        }
    }
}
