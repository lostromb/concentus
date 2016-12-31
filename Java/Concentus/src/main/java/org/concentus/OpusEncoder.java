/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2011 Xiph.Org Foundation
   Originally written by Jean-Marc Valin, Gregory Maxwell, Koen Vos,
   Timothy B. Terriberry, and the Opus open-source contributors
   Ported to Java by Logan Stromberg

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
package org.concentus;

/// <summary>
/// The Opus encoder structure
/// </summary>
public class OpusEncoder {

    final EncControlState silk_mode = new EncControlState();
    OpusApplication application;
    int channels;
    int delay_compensation;
    int force_channels;
    OpusSignal signal_type;
    OpusBandwidth user_bandwidth;
    OpusBandwidth max_bandwidth;
    OpusMode user_forced_mode;
    int voice_ratio;
    int Fs;
    int use_vbr;
    int vbr_constraint;
    OpusFramesize variable_duration;
    int bitrate_bps;
    int user_bitrate_bps;
    int lsb_depth;
    int encoder_buffer;
    int lfe;
    final TonalityAnalysisState analysis = new TonalityAnalysisState();

    // partial reset happens below this line
    int stream_channels;
    short hybrid_stereo_width_Q14;
    int variable_HP_smth2_Q15;
    int prev_HB_gain;
    final int[] hp_mem = new int[4];
    OpusMode mode;
    OpusMode prev_mode;
    int prev_channels;
    int prev_framesize;
    OpusBandwidth bandwidth;
    int silk_bw_switch;
    /* Sampling rate (at the API level) */
    int first;
    int[] energy_masking;
    final StereoWidthState width_mem = new StereoWidthState();
    final short[] delay_buffer = new short[OpusConstants.MAX_ENCODER_BUFFER * 2];
    OpusBandwidth detected_bandwidth;
    int rangeFinal;

    // [Porting Note] There were originally "cabooses" that were tacked onto the end
    // of the struct without being explicitly included (since they have a variable size).
    // Here they are just included as an intrinsic variable.
    final SilkEncoder SilkEncoder = new SilkEncoder();
    final CeltEncoder Celt_Encoder = new CeltEncoder();

    OpusEncoder() {
    } // used internally

    void reset() {
        silk_mode.Reset();
        application = OpusApplication.OPUS_APPLICATION_UNIMPLEMENTED;
        channels = 0;
        delay_compensation = 0;
        force_channels = 0;
        signal_type = OpusSignal.OPUS_SIGNAL_UNKNOWN;
        user_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_UNKNOWN;
        max_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_UNKNOWN;
        user_forced_mode = OpusMode.MODE_UNKNOWN;
        voice_ratio = 0;
        Fs = 0;
        use_vbr = 0;
        vbr_constraint = 0;
        variable_duration = OpusFramesize.OPUS_FRAMESIZE_UNKNOWN;
        bitrate_bps = 0;
        user_bitrate_bps = 0;
        lsb_depth = 0;
        encoder_buffer = 0;
        lfe = 0;
        analysis.Reset();
        PartialReset();
    }

    /// <summary>
    /// OPUS_ENCODER_RESET_START
    /// </summary>
    void PartialReset() {
        stream_channels = 0;
        hybrid_stereo_width_Q14 = 0;
        variable_HP_smth2_Q15 = 0;
        prev_HB_gain = 0;
        Arrays.MemSet(hp_mem, 0, 4);
        mode = OpusMode.MODE_UNKNOWN;
        prev_mode = OpusMode.MODE_UNKNOWN;
        prev_channels = 0;
        prev_framesize = 0;
        bandwidth = OpusBandwidth.OPUS_BANDWIDTH_UNKNOWN;
        silk_bw_switch = 0;
        first = 0;
        energy_masking = null;
        width_mem.Reset();
        Arrays.MemSet(delay_buffer, (short) 0, OpusConstants.MAX_ENCODER_BUFFER * 2);
        detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_UNKNOWN;
        rangeFinal = 0;
        //SilkEncoder.Reset();
        //CeltEncoder.Reset();
    }

    public void resetState() {
        EncControlState dummy = new EncControlState();
        analysis.Reset();
        PartialReset();

        Celt_Encoder.ResetState();
        EncodeAPI.silk_InitEncoder(SilkEncoder, dummy);
        stream_channels = channels;
        hybrid_stereo_width_Q14 = 1 << 14;
        prev_HB_gain = CeltConstants.Q15ONE;
        first = 1;
        mode = OpusMode.MODE_HYBRID;
        bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;
        variable_HP_smth2_Q15 = Inlines.silk_LSHIFT(Inlines.silk_lin2log(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ), 8);
    }

    /**
     * Allocates and initializes an encoder state. Note that regardless of the
     * sampling rate and number channels selected, the Opus encoder can switch
     * to a lower audio bandwidth or number of channels if the bitrate selected
     * is too low. This also means that it is safe to always use 48 kHz stereo
     * input and let the encoder optimize the encoding. The decoder will not be
     * constrained later on by the mode that you select here for the encoder.
     *
     * @param Fs Sampling rate of input signal (Hz). This must be one of 8000,
     * 12000, 16000, 24000, or 48000.
     * @param channels Number of channels (1 or 2) in input signal
     * @param application There are three coding modes:
     *
     * OPUS_APPLICATION_VOIP gives best quality at a given bitrate for voice
     * signals. It enhances the input signal by high-pass filtering and
     * emphasizing formants and harmonics.Optionally it includes in-band forward
     * error correction to protect against packet loss. Use this mode for
     * typical VoIP applications. Because of the enhancement, even at high
     * bitrates the output may sound different from the input.
     *
     * OPUS_APPLICATION_AUDIO gives best quality at a given bitrate for most
     * non-voice signals like music. Use this mode for music and mixed
     * (music/voice) content, broadcast, and applications requiring less than 15
     * ms of coding delay.
     *
     * OPUS_APPLICATION_RESTRICTED_LOWDELAY configures low-delay mode that
     * disables the speech-optimized mode in exchange for slightly reduced
     * delay. This mode can only be set on an newly initialized or freshly reset
     * encoder because it changes the codec delay.
     * @throws OpusException
     */
    public OpusEncoder(int Fs, int channels, OpusApplication application) throws OpusException {
        int ret;
        if ((Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000)) {
            throw new IllegalArgumentException("Sample rate is invalid (must be 8/12/16/24/48 Khz)");
        }
        if (channels != 1 && channels != 2) {
            throw new IllegalArgumentException("Number of channels must be 1 or 2");
        }

        ret = this.opus_init_encoder(Fs, channels, application);
        if (ret != OpusError.OPUS_OK) {
            if (ret == OpusError.OPUS_BAD_ARG) {
                throw new IllegalArgumentException("OPUS_BAD_ARG when creating encoder");
            }
            throw new OpusException("Error while initializing encoder", ret);
        }
    }

    int opus_init_encoder(int Fs, int channels, OpusApplication application) {
        SilkEncoder silk_enc;
        CeltEncoder celt_enc;
        int err;
        int ret;

        if ((Fs != 48000 && Fs != 24000 && Fs != 16000 && Fs != 12000 && Fs != 8000) || (channels != 1 && channels != 2)
                || application == OpusApplication.OPUS_APPLICATION_UNIMPLEMENTED) {
            return OpusError.OPUS_BAD_ARG;
        }

        this.reset();
        /* Create SILK encoder */
        silk_enc = this.SilkEncoder;
        celt_enc = this.Celt_Encoder;

        this.stream_channels = this.channels = channels;

        this.Fs = Fs;

        ret = EncodeAPI.silk_InitEncoder(silk_enc, this.silk_mode);
        if (ret != 0) {
            return OpusError.OPUS_INTERNAL_ERROR;
        }

        /* default SILK parameters */
        this.silk_mode.nChannelsAPI = channels;
        this.silk_mode.nChannelsInternal = channels;
        this.silk_mode.API_sampleRate = this.Fs;
        this.silk_mode.maxInternalSampleRate = 16000;
        this.silk_mode.minInternalSampleRate = 8000;
        this.silk_mode.desiredInternalSampleRate = 16000;
        this.silk_mode.payloadSize_ms = 20;
        this.silk_mode.bitRate = 25000;
        this.silk_mode.packetLossPercentage = 0;
        this.silk_mode.complexity = 9;
        this.silk_mode.useInBandFEC = 0;
        this.silk_mode.useDTX = 0;
        this.silk_mode.useCBR = 0;
        this.silk_mode.reducedDependency = 0;

        /* Create CELT encoder */
 /* Initialize CELT encoder */
        err = celt_enc.celt_encoder_init(Fs, channels);
        if (err != OpusError.OPUS_OK) {
            return OpusError.OPUS_INTERNAL_ERROR;
        }

        celt_enc.SetSignalling(0);
        celt_enc.SetComplexity(this.silk_mode.complexity);

        this.use_vbr = 1;
        /* Makes constrained VBR the default (safer for real-time use) */
        this.vbr_constraint = 1;
        this.user_bitrate_bps = OpusConstants.OPUS_AUTO;
        this.bitrate_bps = 3000 + Fs * channels;
        this.application = application;
        this.signal_type = OpusSignal.OPUS_SIGNAL_AUTO;
        this.user_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_AUTO;
        this.max_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;
        this.force_channels = OpusConstants.OPUS_AUTO;
        this.user_forced_mode = OpusMode.MODE_AUTO;
        this.voice_ratio = -1;
        this.encoder_buffer = this.Fs / 100;
        this.lsb_depth = 24;
        this.variable_duration = OpusFramesize.OPUS_FRAMESIZE_ARG;

        /* Delay compensation of 4 ms (2.5 ms for SILK's extra look-ahead
           + 1.5 ms for SILK resamplers and stereo prediction) */
        this.delay_compensation = this.Fs / 250;

        this.hybrid_stereo_width_Q14 = 1 << 14;
        this.prev_HB_gain = CeltConstants.Q15ONE;
        this.variable_HP_smth2_Q15 = Inlines.silk_LSHIFT(Inlines.silk_lin2log(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ), 8);
        this.first = 1;
        this.mode = OpusMode.MODE_HYBRID;
        this.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;

        Analysis.tonality_analysis_init(this.analysis);

        return OpusError.OPUS_OK;
    }

    int user_bitrate_to_bitrate(int frame_size, int max_data_bytes) {
        if (frame_size == 0) {
            frame_size = this.Fs / 400;
        }
        if (this.user_bitrate_bps == OpusConstants.OPUS_AUTO) {
            return 60 * this.Fs / frame_size + this.Fs * this.channels;
        } else if (this.user_bitrate_bps == OpusConstants.OPUS_BITRATE_MAX) {
            return max_data_bytes * 8 * this.Fs / frame_size;
        } else {
            return this.user_bitrate_bps;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T">The storage type of analysis_pcm, either short or float</typeparam>
    /// <param name="this"></param>
    /// <param name="pcm"></param>
    /// <param name="frame_size"></param>
    /// <param name="data"></param>
    /// <param name="out_data_bytes"></param>
    /// <param name="lsb_depth"></param>
    /// <param name="analysis_pcm"></param>
    /// <param name="analysis_size"></param>
    /// <param name="c1"></param>
    /// <param name="c2"></param>
    /// <param name="analysis_channels"></param>
    /// <param name="downmix"></param>
    /// <param name="float_api"></param>
    /// <returns></returns>
    int opus_encode_native(short[] pcm, int pcm_ptr, int frame_size,
            byte[] data, int data_ptr, int out_data_bytes, int lsb_depth,
            short[] analysis_pcm, int analysis_pcm_ptr, int analysis_size, int c1, int c2,
            int analysis_channels, int float_api) {
        SilkEncoder silk_enc;
        CeltEncoder celt_enc;
        int i;
        int ret = 0;
        int nBytes;
        EntropyCoder enc = new EntropyCoder(); // porting note: stack var
        int bytes_target;
        int prefill = 0;
        int start_band = 0;
        int redundancy = 0;
        int redundancy_bytes = 0;
        /* Number of bytes to use for redundancy frame */
        int celt_to_silk = 0;
        short[] pcm_buf;
        int nb_compr_bytes;
        int to_celt = 0;
        int redundant_rng = 0;
        int cutoff_Hz, hp_freq_smth1;
        int voice_est;
        /* Probability of voice in Q7 */
        int equiv_rate;
        int delay_compensation;
        int frame_rate;
        int max_rate;
        /* Max bitrate we're allowed to use */
        OpusBandwidth curr_bandwidth;
        int HB_gain;
        int max_data_bytes;
        /* Max number of bytes we're allowed to use */
        int total_buffer;
        int stereo_width;
        CeltMode celt_mode;
        AnalysisInfo analysis_info = new AnalysisInfo(); // porting note: stack var
        int analysis_read_pos_bak = -1;
        int analysis_read_subframe_bak = -1;
        short[] tmp_prefill;

        max_data_bytes = Inlines.IMIN(1276, out_data_bytes);

        this.rangeFinal = 0;
        if ((this.variable_duration == OpusFramesize.OPUS_FRAMESIZE_UNKNOWN && 400 * frame_size != this.Fs && 200 * frame_size != this.Fs && 100 * frame_size != this.Fs
                && 50 * frame_size != this.Fs && 25 * frame_size != this.Fs && 50 * frame_size != 3 * this.Fs)
                || (400 * frame_size < this.Fs)
                || max_data_bytes <= 0) {
            return OpusError.OPUS_BAD_ARG;
        }

        silk_enc = this.SilkEncoder;
        celt_enc = this.Celt_Encoder;
        if (this.application == OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY) {
            delay_compensation = 0;
        } else {
            delay_compensation = this.delay_compensation;
        }

        lsb_depth = Inlines.IMIN(lsb_depth, this.lsb_depth);
        celt_mode = celt_enc.GetMode();
        this.voice_ratio = -1;

        if (this.analysis.enabled) {
            analysis_info.valid = 0;
            if (this.silk_mode.complexity >= 7 && this.Fs == 48000) {
                analysis_read_pos_bak = this.analysis.read_pos;
                analysis_read_subframe_bak = this.analysis.read_subframe;
                Analysis.run_analysis(this.analysis,
                        celt_mode,
                        analysis_pcm != null ? analysis_pcm : null,
                        analysis_pcm_ptr,
                        analysis_size,
                        frame_size,
                        c1,
                        c2,
                        analysis_channels,
                        this.Fs,
                        lsb_depth,
                        analysis_info);
            }

            this.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_UNKNOWN;
            if (analysis_info.valid != 0) {
                int analysis_bandwidth;
                if (this.signal_type == OpusSignal.OPUS_SIGNAL_AUTO) {
                    this.voice_ratio = (int) Math.floor(.5f + 100 * (1 - analysis_info.music_prob));
                }

                analysis_bandwidth = analysis_info.bandwidth;
                if (analysis_bandwidth <= 12) {
                    this.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
                } else if (analysis_bandwidth <= 14) {
                    this.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND;
                } else if (analysis_bandwidth <= 16) {
                    this.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
                } else if (analysis_bandwidth <= 18) {
                    this.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
                } else {
                    this.detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;
                }
            }
        }

        if (this.channels == 2 && this.force_channels != 1) {
            stereo_width = CodecHelpers.compute_stereo_width(pcm, pcm_ptr, frame_size, this.Fs, this.width_mem);
        } else {
            stereo_width = 0;
        }
        total_buffer = delay_compensation;
        this.bitrate_bps = user_bitrate_to_bitrate(frame_size, max_data_bytes);

        frame_rate = this.Fs / frame_size;
        if (this.use_vbr == 0) {
            int cbrBytes;
            /* Multiply by 3 to make sure the division is exact. */
            int frame_rate3 = 3 * this.Fs / frame_size;
            /* We need to make sure that "int" values always fit in 16 bits. */
            cbrBytes = Inlines.IMIN((3 * this.bitrate_bps / 8 + frame_rate3 / 2) / frame_rate3, max_data_bytes);
            this.bitrate_bps = cbrBytes * frame_rate3 * 8 / 3;
            max_data_bytes = cbrBytes;
        }
        if (max_data_bytes < 3 || this.bitrate_bps < 3 * frame_rate * 8
                || (frame_rate < 50 && (max_data_bytes * frame_rate < 300 || this.bitrate_bps < 2400))) {
            /*If the space is too low to do something useful, emit 'PLC' frames.*/
            OpusMode tocmode = this.mode;
            OpusBandwidth bw = this.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_UNKNOWN ? OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND : this.bandwidth;
            if (tocmode == OpusMode.MODE_UNKNOWN) {
                tocmode = OpusMode.MODE_SILK_ONLY;
            }
            if (frame_rate > 100) {
                tocmode = OpusMode.MODE_CELT_ONLY;
            }
            if (frame_rate < 50) {
                tocmode = OpusMode.MODE_SILK_ONLY;
            }
            if (tocmode == OpusMode.MODE_SILK_ONLY && OpusBandwidthHelpers.GetOrdinal(bw) > OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)) {
                bw = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
            } else if (tocmode == OpusMode.MODE_CELT_ONLY && OpusBandwidthHelpers.GetOrdinal(bw) == OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)) {
                bw = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
            } else if (tocmode == OpusMode.MODE_HYBRID && OpusBandwidthHelpers.GetOrdinal(bw) <= OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND)) {
                bw = OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
            }
            data[data_ptr] = CodecHelpers.gen_toc(tocmode, frame_rate, bw, this.stream_channels);
            ret = 1;
            if (this.use_vbr == 0) {
                ret = OpusRepacketizer.padPacket(data, data_ptr, ret, max_data_bytes);
                if (ret == OpusError.OPUS_OK) {
                    ret = max_data_bytes;
                }
            }
            return ret;
        }
        max_rate = frame_rate * max_data_bytes * 8;

        /* Equivalent 20-ms rate for mode/channel/bandwidth decisions */
        equiv_rate = this.bitrate_bps - (40 * this.channels + 20) * (this.Fs / frame_size - 50);

        if (this.signal_type == OpusSignal.OPUS_SIGNAL_VOICE) {
            voice_est = 127;
        } else if (this.signal_type == OpusSignal.OPUS_SIGNAL_MUSIC) {
            voice_est = 0;
        } else if (this.voice_ratio >= 0) {
            voice_est = this.voice_ratio * 327 >> 8;
            /* For AUDIO, never be more than 90% confident of having speech */
            if (this.application == OpusApplication.OPUS_APPLICATION_AUDIO) {
                voice_est = Inlines.IMIN(voice_est, 115);
            }
        } else if (this.application == OpusApplication.OPUS_APPLICATION_VOIP) {
            voice_est = 115;
        } else {
            voice_est = 48;
        }

        if (this.force_channels != OpusConstants.OPUS_AUTO && this.channels == 2) {
            this.stream_channels = this.force_channels;
        } else /* Rate-dependent mono-stereo decision */ if (this.channels == 2) {
            int stereo_threshold;
            stereo_threshold = OpusTables.stereo_music_threshold + ((voice_est * voice_est * (OpusTables.stereo_voice_threshold - OpusTables.stereo_music_threshold)) >> 14);
            if (this.stream_channels == 2) {
                stereo_threshold -= 1000;
            } else {
                stereo_threshold += 1000;
            }
            this.stream_channels = (equiv_rate > stereo_threshold) ? 2 : 1;
        } else {
            this.stream_channels = this.channels;
        }
        equiv_rate = this.bitrate_bps - (40 * this.stream_channels + 20) * (this.Fs / frame_size - 50);

        /* Mode selection depending on application and signal type */
        if (this.application == OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY) {
            this.mode = OpusMode.MODE_CELT_ONLY;
        } else if (this.user_forced_mode == OpusMode.MODE_AUTO) {
            int mode_voice, mode_music;
            int threshold;

            /* Interpolate based on stereo width */
            mode_voice = Inlines.MULT16_32_Q15(CeltConstants.Q15ONE - stereo_width, OpusTables.mode_thresholds[0][0])
                    + Inlines.MULT16_32_Q15(stereo_width, OpusTables.mode_thresholds[1][0]);
            mode_music = Inlines.MULT16_32_Q15(CeltConstants.Q15ONE - stereo_width, OpusTables.mode_thresholds[1][1])
                    + Inlines.MULT16_32_Q15(stereo_width, OpusTables.mode_thresholds[1][1]);
            /* Interpolate based on speech/music probability */
            threshold = mode_music + ((voice_est * voice_est * (mode_voice - mode_music)) >> 14);
            /* Bias towards SILK for VoIP because of some useful features */
            if (this.application == OpusApplication.OPUS_APPLICATION_VOIP) {
                threshold += 8000;
            }

            /*printf("%f %d\n", stereo_width/(float)1.0f, threshold);*/
 /* Hysteresis */
            if (this.prev_mode == OpusMode.MODE_CELT_ONLY) {
                threshold -= 4000;
            } else if (this.prev_mode != OpusMode.MODE_AUTO && this.prev_mode != OpusMode.MODE_UNKNOWN) {
                threshold += 4000;
            }

            this.mode = (equiv_rate >= threshold) ? OpusMode.MODE_CELT_ONLY : OpusMode.MODE_SILK_ONLY;

            /* When FEC is enabled and there's enough packet loss, use SILK */
            if (this.silk_mode.useInBandFEC != 0 && this.silk_mode.packetLossPercentage > (128 - voice_est) >> 4) {
                this.mode = OpusMode.MODE_SILK_ONLY;
            }
            /* When encoding voice and DTX is enabled, set the encoder to SILK mode (at least for now) */
            if (this.silk_mode.useDTX != 0 && voice_est > 100) {
                this.mode = OpusMode.MODE_SILK_ONLY;
            }
        } else {
            this.mode = this.user_forced_mode;
        }

        /* Override the chosen mode to make sure we meet the requested frame size */
        if (this.mode != OpusMode.MODE_CELT_ONLY && frame_size < this.Fs / 100) {
            this.mode = OpusMode.MODE_CELT_ONLY;
        }
        if (this.lfe != 0) {
            this.mode = OpusMode.MODE_CELT_ONLY;
        }
        /* If max_data_bytes represents less than 8 kb/s, switch to CELT-only mode */
        if (max_data_bytes < (frame_rate > 50 ? 12000 : 8000) * frame_size / (this.Fs * 8)) {
            this.mode = OpusMode.MODE_CELT_ONLY;
        }

        if (this.stream_channels == 1 && this.prev_channels == 2 && this.silk_mode.toMono == 0
                && this.mode != OpusMode.MODE_CELT_ONLY && this.prev_mode != OpusMode.MODE_CELT_ONLY) {
            /* Delay stereo.mono transition by two frames so that SILK can do a smooth downmix */
            this.silk_mode.toMono = 1;
            this.stream_channels = 2;
        } else {
            this.silk_mode.toMono = 0;
        }

        if ((this.prev_mode != OpusMode.MODE_AUTO && this.prev_mode != OpusMode.MODE_UNKNOWN)
                && ((this.mode != OpusMode.MODE_CELT_ONLY && this.prev_mode == OpusMode.MODE_CELT_ONLY)
                || (this.mode == OpusMode.MODE_CELT_ONLY && this.prev_mode != OpusMode.MODE_CELT_ONLY))) {
            redundancy = 1;
            celt_to_silk = (this.mode != OpusMode.MODE_CELT_ONLY) ? 1 : 0;
            if (celt_to_silk == 0) {
                /* Switch to SILK/hybrid if frame size is 10 ms or more*/
                if (frame_size >= this.Fs / 100) {
                    this.mode = this.prev_mode;
                    to_celt = 1;
                } else {
                    redundancy = 0;
                }
            }
        }
        /* For the first frame at a new SILK bandwidth */
        if (this.silk_bw_switch != 0) {
            redundancy = 1;
            celt_to_silk = 1;
            this.silk_bw_switch = 0;
            prefill = 1;
        }

        if (redundancy != 0) {
            /* Fair share of the max size allowed */
            redundancy_bytes = Inlines.IMIN(257, max_data_bytes * (this.Fs / 200) / (frame_size + this.Fs / 200));
            /* For VBR, target the actual bitrate (subject to the limit above) */
            if (this.use_vbr != 0) {
                redundancy_bytes = Inlines.IMIN(redundancy_bytes, this.bitrate_bps / 1600);
            }
        }

        if (this.mode != OpusMode.MODE_CELT_ONLY && this.prev_mode == OpusMode.MODE_CELT_ONLY) {
            EncControlState dummy = new EncControlState();
            EncodeAPI.silk_InitEncoder(silk_enc, dummy);
            prefill = 1;
        }

        /* Automatic (rate-dependent) bandwidth selection */
        if (this.mode == OpusMode.MODE_CELT_ONLY || this.first != 0 || this.silk_mode.allowBandwidthSwitch != 0) {
            int[] voice_bandwidth_thresholds;
            int[] music_bandwidth_thresholds;
            int[] bandwidth_thresholds = new int[8];
            OpusBandwidth bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;
            int equiv_rate2;

            equiv_rate2 = equiv_rate;
            if (this.mode != OpusMode.MODE_CELT_ONLY) {
                /* Adjust the threshold +/- 10% depending on complexity */
                equiv_rate2 = equiv_rate2 * (45 + this.silk_mode.complexity) / 50;
                /* CBR is less efficient by ~1 kb/s */
                if (this.use_vbr == 0) {
                    equiv_rate2 -= 1000;
                }
            }
            if (this.channels == 2 && this.force_channels != 1) {
                voice_bandwidth_thresholds = OpusTables.stereo_voice_bandwidth_thresholds;
                music_bandwidth_thresholds = OpusTables.stereo_music_bandwidth_thresholds;
            } else {
                voice_bandwidth_thresholds = OpusTables.mono_voice_bandwidth_thresholds;
                music_bandwidth_thresholds = OpusTables.mono_music_bandwidth_thresholds;
            }
            /* Interpolate bandwidth thresholds depending on voice estimation */
            for (i = 0; i < 8; i++) {
                bandwidth_thresholds[i] = music_bandwidth_thresholds[i]
                        + ((voice_est * voice_est * (voice_bandwidth_thresholds[i] - music_bandwidth_thresholds[i])) >> 14);
            }
            do {
                int threshold, hysteresis;
                threshold = bandwidth_thresholds[2 * (OpusBandwidthHelpers.GetOrdinal(bandwidth) - OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND))];
                hysteresis = bandwidth_thresholds[2 * (OpusBandwidthHelpers.GetOrdinal(bandwidth) - OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)) + 1];
                if (this.first == 0) {
                    if (OpusBandwidthHelpers.GetOrdinal(this.bandwidth) >= OpusBandwidthHelpers.GetOrdinal(bandwidth)) {
                        threshold -= hysteresis;
                    } else {
                        threshold += hysteresis;
                    }
                }
                if (equiv_rate2 >= threshold) {
                    break;
                }

                bandwidth = OpusBandwidthHelpers.SUBTRACT(bandwidth, 1);
            } while (OpusBandwidthHelpers.GetOrdinal(bandwidth) > OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND));
            this.bandwidth = bandwidth;
            /* Prevents any transition to SWB/FB until the SILK layer has fully
               switched to WB mode and turned the variable LP filter off */
            if (this.first == 0 && this.mode != OpusMode.MODE_CELT_ONLY
                    && this.silk_mode.inWBmodeWithoutVariableLP == 0
                    && OpusBandwidthHelpers.GetOrdinal(this.bandwidth) > OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)) {
                this.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
            }
        }

        if (OpusBandwidthHelpers.GetOrdinal(this.bandwidth) > OpusBandwidthHelpers.GetOrdinal(this.max_bandwidth)) {
            this.bandwidth = this.max_bandwidth;
        }

        if (this.user_bandwidth != OpusBandwidth.OPUS_BANDWIDTH_AUTO) {
            this.bandwidth = this.user_bandwidth;
        }

        /* This prevents us from using hybrid at unsafe CBR/max rates */
        if (this.mode != OpusMode.MODE_CELT_ONLY && max_rate < 15000) {
            this.bandwidth = OpusBandwidthHelpers.MIN(this.bandwidth, OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND);
        }

        /* Prevents Opus from wasting bits on frequencies that are above
           the Nyquist rate of the input signal */
        if (this.Fs <= 24000 && OpusBandwidthHelpers.GetOrdinal(this.bandwidth) > OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND)) {
            this.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
        }
        if (this.Fs <= 16000 && OpusBandwidthHelpers.GetOrdinal(this.bandwidth) > OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)) {
            this.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
        }
        if (this.Fs <= 12000 && OpusBandwidthHelpers.GetOrdinal(this.bandwidth) > OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)) {
            this.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND;
        }
        if (this.Fs <= 8000 && OpusBandwidthHelpers.GetOrdinal(this.bandwidth) > OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND)) {
            this.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
        }
        /* Use detected bandwidth to reduce the encoded bandwidth. */
        if (this.detected_bandwidth != OpusBandwidth.OPUS_BANDWIDTH_UNKNOWN && this.user_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_AUTO) {
            OpusBandwidth min_detected_bandwidth;
            /* Makes bandwidth detection more conservative just in case the detector
               gets it wrong when we could have coded a high bandwidth transparently.
               When operating in SILK/hybrid mode, we don't go below wideband to avoid
               more complicated switches that require redundancy. */
            if (equiv_rate <= 18000 * this.stream_channels && this.mode == OpusMode.MODE_CELT_ONLY) {
                min_detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
            } else if (equiv_rate <= 24000 * this.stream_channels && this.mode == OpusMode.MODE_CELT_ONLY) {
                min_detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND;
            } else if (equiv_rate <= 30000 * this.stream_channels) {
                min_detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
            } else if (equiv_rate <= 44000 * this.stream_channels) {
                min_detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
            } else {
                min_detected_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;
            }

            this.detected_bandwidth = OpusBandwidthHelpers.MAX(this.detected_bandwidth, min_detected_bandwidth);
            this.bandwidth = OpusBandwidthHelpers.MIN(this.bandwidth, this.detected_bandwidth);
        }
        celt_enc.SetLSBDepth(lsb_depth);

        /* CELT mode doesn't support mediumband, use wideband instead */
        if (this.mode == OpusMode.MODE_CELT_ONLY && this.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND) {
            this.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
        }
        if (this.lfe != 0) {
            this.bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
        }

        /* Can't support higher than wideband for >20 ms frames */
        if (frame_size > this.Fs / 50 && (this.mode == OpusMode.MODE_CELT_ONLY || OpusBandwidthHelpers.GetOrdinal(this.bandwidth) > OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND))) {
            byte[] tmp_data;
            int nb_frames;
            OpusBandwidth bak_bandwidth;
            int bak_channels, bak_to_mono;
            OpusMode bak_mode;
            OpusRepacketizer rp;
            int bytes_per_frame;
            int repacketize_len;

            if (this.analysis.enabled && analysis_read_pos_bak != -1) {
                this.analysis.read_pos = analysis_read_pos_bak;
                this.analysis.read_subframe = analysis_read_subframe_bak;
            }

            nb_frames = frame_size > this.Fs / 25 ? 3 : 2;
            bytes_per_frame = Inlines.IMIN(1276, (out_data_bytes - 3) / nb_frames);

            tmp_data = new byte[nb_frames * bytes_per_frame];

            rp = new OpusRepacketizer();

            bak_mode = this.user_forced_mode;
            bak_bandwidth = this.user_bandwidth;
            bak_channels = this.force_channels;

            this.user_forced_mode = this.mode;
            this.user_bandwidth = this.bandwidth;
            this.force_channels = this.stream_channels;
            bak_to_mono = this.silk_mode.toMono;

            if (bak_to_mono != 0) {
                this.force_channels = 1;
            } else {
                this.prev_channels = this.stream_channels;
            }
            for (i = 0; i < nb_frames; i++) {
                int tmp_len;
                this.silk_mode.toMono = 0;
                /* When switching from SILK/Hybrid to CELT, only ask for a switch at the last frame */
                if (to_celt != 0 && i == nb_frames - 1) {
                    this.user_forced_mode = OpusMode.MODE_CELT_ONLY;
                }
                tmp_len = opus_encode_native(pcm, pcm_ptr + (i * (this.channels * this.Fs / 50)), this.Fs / 50,
                        tmp_data, i * bytes_per_frame, bytes_per_frame, lsb_depth,
                        null, 0, 0, c1, c2, analysis_channels, float_api);
                if (tmp_len < 0) {

                    return OpusError.OPUS_INTERNAL_ERROR;
                }
                ret = rp.addPacket(tmp_data, i * bytes_per_frame, tmp_len);
                if (ret < 0) {

                    return OpusError.OPUS_INTERNAL_ERROR;
                }
            }
            if (this.use_vbr != 0) {
                repacketize_len = out_data_bytes;
            } else {
                repacketize_len = Inlines.IMIN(3 * this.bitrate_bps / (3 * 8 * 50 / nb_frames), out_data_bytes);
            }
            ret = rp.opus_repacketizer_out_range_impl(0, nb_frames, data, data_ptr, repacketize_len, 0, (this.use_vbr == 0) ? 1 : 0);
            if (ret < 0) {
                return OpusError.OPUS_INTERNAL_ERROR;
            }
            this.user_forced_mode = bak_mode;
            this.user_bandwidth = bak_bandwidth;
            this.force_channels = bak_channels;
            this.silk_mode.toMono = bak_to_mono;

            return ret;
        }
        curr_bandwidth = this.bandwidth;

        /* Chooses the appropriate mode for speech
           *NEVER* switch to/from CELT-only mode here as this will invalidate some assumptions */
        if (this.mode == OpusMode.MODE_SILK_ONLY && OpusBandwidthHelpers.GetOrdinal(curr_bandwidth) > OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)) {
            this.mode = OpusMode.MODE_HYBRID;
        }
        if (this.mode == OpusMode.MODE_HYBRID && OpusBandwidthHelpers.GetOrdinal(curr_bandwidth) <= OpusBandwidthHelpers.GetOrdinal(OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)) {
            this.mode = OpusMode.MODE_SILK_ONLY;
        }

        /* printf("%d %d %d %d\n", st.bitrate_bps, st.stream_channels, st.mode, curr_bandwidth); */
        bytes_target = Inlines.IMIN(max_data_bytes - redundancy_bytes, this.bitrate_bps * frame_size / (this.Fs * 8)) - 1;

        data_ptr += 1;

        enc.enc_init(data, data_ptr, (max_data_bytes - 1));

        pcm_buf = new short[(total_buffer + frame_size) * this.channels];
        System.arraycopy(this.delay_buffer, ((this.encoder_buffer - total_buffer) * this.channels), pcm_buf, 0, total_buffer * this.channels);

        if (this.mode == OpusMode.MODE_CELT_ONLY) {
            hp_freq_smth1 = Inlines.silk_LSHIFT(Inlines.silk_lin2log(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ), 8);
        } else {
            hp_freq_smth1 = silk_enc.state_Fxx[0].variable_HP_smth1_Q15;
        }

        this.variable_HP_smth2_Q15 = Inlines.silk_SMLAWB(this.variable_HP_smth2_Q15,
                hp_freq_smth1 - this.variable_HP_smth2_Q15, ((int) ((TuningParameters.VARIABLE_HP_SMTH_COEF2) * ((long) 1 << (16)) + 0.5))/*Inlines.SILK_CONST(TuningParameters.VARIABLE_HP_SMTH_COEF2, 16)*/);

        /* convert from log scale to Hertz */
        cutoff_Hz = Inlines.silk_log2lin(Inlines.silk_RSHIFT(this.variable_HP_smth2_Q15, 8));

        if (this.application == OpusApplication.OPUS_APPLICATION_VOIP) {
            CodecHelpers.hp_cutoff(pcm, pcm_ptr, cutoff_Hz, pcm_buf, (total_buffer * this.channels), this.hp_mem, frame_size, this.channels, this.Fs);
        } else {
            CodecHelpers.dc_reject(pcm, pcm_ptr, 3, pcm_buf, total_buffer * this.channels, this.hp_mem, frame_size, this.channels, this.Fs);
        }

        /* SILK processing */
        HB_gain = CeltConstants.Q15ONE;
        if (this.mode != OpusMode.MODE_CELT_ONLY) {
            int total_bitRate, celt_rate;
            short[] pcm_silk = new short[this.channels * frame_size];

            /* Distribute bits between SILK and CELT */
            total_bitRate = 8 * bytes_target * frame_rate;
            if (this.mode == OpusMode.MODE_HYBRID) {
                int HB_gain_ref;
                /* Base rate for SILK */
                this.silk_mode.bitRate = this.stream_channels * (5000 + ((this.Fs == 100 * frame_size) ? 1000 : 0));
                if (curr_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND) {
                    /* SILK gets 2/3 of the remaining bits */
                    this.silk_mode.bitRate += (total_bitRate - this.silk_mode.bitRate) * 2 / 3;
                } else {
                    /* FULLBAND */
 /* SILK gets 3/5 of the remaining bits */
                    this.silk_mode.bitRate += (total_bitRate - this.silk_mode.bitRate) * 3 / 5;
                }
                /* Don't let SILK use more than 80% */
                if (this.silk_mode.bitRate > total_bitRate * 4 / 5) {
                    this.silk_mode.bitRate = total_bitRate * 4 / 5;
                }
                if (this.energy_masking == null) {
                    /* Increasingly attenuate high band when it gets allocated fewer bits */
                    celt_rate = total_bitRate - this.silk_mode.bitRate;
                    HB_gain_ref = (curr_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND) ? 3000 : 3600;
                    HB_gain = Inlines.SHL32(celt_rate, 9) / Inlines.SHR32(celt_rate + this.stream_channels * HB_gain_ref, 6);
                    HB_gain = HB_gain < CeltConstants.Q15ONE * 6 / 7 ? HB_gain + CeltConstants.Q15ONE / 7 : CeltConstants.Q15ONE;
                }
            } else {
                /* SILK gets all bits */
                this.silk_mode.bitRate = total_bitRate;
            }

            /* Surround masking for SILK */
            if (this.energy_masking != null && this.use_vbr != 0 && this.lfe == 0) {
                int mask_sum = 0;
                int masking_depth;
                int rate_offset;
                int c;
                int end = 17;
                short srate = 16000;
                if (this.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND) {
                    end = 13;
                    srate = 8000;
                } else if (this.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND) {
                    end = 15;
                    srate = 12000;
                }
                for (c = 0; c < this.channels; c++) {
                    for (i = 0; i < end; i++) {
                        int mask;
                        mask = Inlines.MAX16(Inlines.MIN16(this.energy_masking[21 * c + i],
                                ((short) (0.5 + (.5f) * ((1) << (10))))/*Inlines.QCONST16(.5f, 10)*/), -((short) (0.5 + (2.0f) * ((1) << (10))))/*Inlines.QCONST16(2.0f, 10)*/);
                        if (mask > 0) {
                            mask = Inlines.HALF16(mask);
                        }
                        mask_sum += mask;
                    }
                }
                /* Conservative rate reduction, we cut the masking in half */
                masking_depth = mask_sum / end * this.channels;
                masking_depth += ((short) (0.5 + (.2f) * ((1) << (10))))/*Inlines.QCONST16(.2f, 10)*/;
                rate_offset = Inlines.PSHR32(Inlines.MULT16_16(srate, masking_depth), 10);
                rate_offset = Inlines.MAX32(rate_offset, -2 * this.silk_mode.bitRate / 3);
                /* Split the rate change between the SILK and CELT part for hybrid. */
                if (this.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND || this.bandwidth == OpusBandwidth.OPUS_BANDWIDTH_FULLBAND) {
                    this.silk_mode.bitRate += 3 * rate_offset / 5;
                } else {
                    this.silk_mode.bitRate += rate_offset;
                }
                bytes_target += rate_offset * frame_size / (8 * this.Fs);
            }

            this.silk_mode.payloadSize_ms = 1000 * frame_size / this.Fs;
            this.silk_mode.nChannelsAPI = this.channels;
            this.silk_mode.nChannelsInternal = this.stream_channels;
            if (curr_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND) {
                this.silk_mode.desiredInternalSampleRate = 8000;
            } else if (curr_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND) {
                this.silk_mode.desiredInternalSampleRate = 12000;
            } else {
                Inlines.OpusAssert(this.mode == OpusMode.MODE_HYBRID || curr_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND);
                this.silk_mode.desiredInternalSampleRate = 16000;
            }
            if (this.mode == OpusMode.MODE_HYBRID) {
                /* Don't allow bandwidth reduction at lowest bitrates in hybrid mode */
                this.silk_mode.minInternalSampleRate = 16000;
            } else {
                this.silk_mode.minInternalSampleRate = 8000;
            }

            if (this.mode == OpusMode.MODE_SILK_ONLY) {
                int effective_max_rate = max_rate;
                this.silk_mode.maxInternalSampleRate = 16000;
                if (frame_rate > 50) {
                    effective_max_rate = effective_max_rate * 2 / 3;
                }
                if (effective_max_rate < 13000) {
                    this.silk_mode.maxInternalSampleRate = 12000;
                    this.silk_mode.desiredInternalSampleRate = Inlines.IMIN(12000, this.silk_mode.desiredInternalSampleRate);
                }
                if (effective_max_rate < 9600) {
                    this.silk_mode.maxInternalSampleRate = 8000;
                    this.silk_mode.desiredInternalSampleRate = Inlines.IMIN(8000, this.silk_mode.desiredInternalSampleRate);
                }
            } else {
                this.silk_mode.maxInternalSampleRate = 16000;
            }

            this.silk_mode.useCBR = this.use_vbr == 0 ? 1 : 0;

            /* Call SILK encoder for the low band */
            nBytes = Inlines.IMIN(1275, max_data_bytes - 1 - redundancy_bytes);

            this.silk_mode.maxBits = nBytes * 8;
            /* Only allow up to 90% of the bits for hybrid mode*/
            if (this.mode == OpusMode.MODE_HYBRID) {
                this.silk_mode.maxBits = this.silk_mode.maxBits * 9 / 10;
            }
            if (this.silk_mode.useCBR != 0) {
                this.silk_mode.maxBits = (this.silk_mode.bitRate * frame_size / (this.Fs * 8)) * 8;
                /* Reduce the initial target to make it easier to reach the CBR rate */
                this.silk_mode.bitRate = Inlines.IMAX(1, this.silk_mode.bitRate - 2000);
            }

            if (prefill != 0) {
                BoxedValueInt zero = new BoxedValueInt(0);
                int prefill_offset;

                /* Use a smooth onset for the SILK prefill to avoid the encoder trying to encode
                   a discontinuity. The exact location is what we need to avoid leaving any "gap"
                   in the audio when mixing with the redundant CELT frame. Here we can afford to
                   overwrite st.delay_buffer because the only thing that uses it before it gets
                   rewritten is tmp_prefill[] and even then only the part after the ramp really
                   gets used (rather than sent to the encoder and discarded) */
                prefill_offset = this.channels * (this.encoder_buffer - this.delay_compensation - this.Fs / 400);
                CodecHelpers.gain_fade(this.delay_buffer, prefill_offset,
                        0, CeltConstants.Q15ONE, celt_mode.overlap, this.Fs / 400, this.channels, celt_mode.window, this.Fs);
                Arrays.MemSet(this.delay_buffer, (short) 0, prefill_offset);
                System.arraycopy(this.delay_buffer, 0, pcm_silk, 0, this.encoder_buffer * this.channels);

                EncodeAPI.silk_Encode(silk_enc, this.silk_mode, pcm_silk, this.encoder_buffer, null, zero, 1);
            }

            System.arraycopy(pcm_buf, total_buffer * this.channels, pcm_silk, 0, frame_size * this.channels);

            BoxedValueInt boxed_silkBytes = new BoxedValueInt(nBytes);
            ret = EncodeAPI.silk_Encode(silk_enc, this.silk_mode, pcm_silk, frame_size, enc, boxed_silkBytes, 0);
            nBytes = boxed_silkBytes.Val;

            if (ret != 0) {
                /*fprintf (stderr, "SILK encode error: %d\n", ret);*/
 /* Handle error */

                return OpusError.OPUS_INTERNAL_ERROR;
            }
            if (nBytes == 0) {
                this.rangeFinal = 0;
                data[data_ptr - 1] = CodecHelpers.gen_toc(this.mode, this.Fs / frame_size, curr_bandwidth, this.stream_channels);

                return 1;
            }
            /* Extract SILK public bandwidth for signaling in first byte */
            if (this.mode == OpusMode.MODE_SILK_ONLY) {
                if (this.silk_mode.internalSampleRate == 8000) {
                    curr_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND;
                } else if (this.silk_mode.internalSampleRate == 12000) {
                    curr_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND;
                } else if (this.silk_mode.internalSampleRate == 16000) {
                    curr_bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
                }
            } else {
                Inlines.OpusAssert(this.silk_mode.internalSampleRate == 16000);
            }

            this.silk_mode.opusCanSwitch = this.silk_mode.switchReady;
            if (this.silk_mode.opusCanSwitch != 0) {
                redundancy = 1;
                celt_to_silk = 0;
                this.silk_bw_switch = 1;
            }
        }

        /* CELT processing */
        {
            int endband = 21;

            switch (curr_bandwidth) {
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
            }
            celt_enc.SetEndBand(endband);
            celt_enc.SetChannels(this.stream_channels);
        }
        celt_enc.SetBitrate(OpusConstants.OPUS_BITRATE_MAX);
        if (this.mode != OpusMode.MODE_SILK_ONLY) {
            int celt_pred = 2;
            celt_enc.SetVBR(false);
            /* We may still decide to disable prediction later */
            if (this.silk_mode.reducedDependency != 0) {
                celt_pred = 0;
            }
            celt_enc.SetPrediction(celt_pred);

            if (this.mode == OpusMode.MODE_HYBRID) {
                int len;

                len = (enc.tell() + 7) >> 3;
                if (redundancy != 0) {
                    len += this.mode == OpusMode.MODE_HYBRID ? 3 : 1;
                }
                if (this.use_vbr != 0) {
                    nb_compr_bytes = len + bytes_target - (this.silk_mode.bitRate * frame_size) / (8 * this.Fs);
                } else {
                    /* check if SILK used up too much */
                    nb_compr_bytes = len > bytes_target ? len : bytes_target;
                }
            } else if (this.use_vbr != 0) {
                int bonus = 0;
                if (this.analysis.enabled && this.variable_duration == OpusFramesize.OPUS_FRAMESIZE_VARIABLE && frame_size != this.Fs / 50) {
                    bonus = (60 * this.stream_channels + 40) * (this.Fs / frame_size - 50);
                    if (analysis_info.valid != 0) {
                        bonus = (int) (bonus * (1.0f + .5f * analysis_info.tonality));
                    }
                }
                celt_enc.SetVBR(true);
                celt_enc.SetVBRConstraint(this.vbr_constraint != 0);
                celt_enc.SetBitrate(this.bitrate_bps + bonus);
                nb_compr_bytes = max_data_bytes - 1 - redundancy_bytes;
            } else {
                nb_compr_bytes = bytes_target;
            }

        } else {
            nb_compr_bytes = 0;
        }

        tmp_prefill = new short[this.channels * this.Fs / 400];
        if (this.mode != OpusMode.MODE_SILK_ONLY && this.mode != this.prev_mode && (this.prev_mode != OpusMode.MODE_AUTO && this.prev_mode != OpusMode.MODE_UNKNOWN)) {
            System.arraycopy(this.delay_buffer, ((this.encoder_buffer - total_buffer - this.Fs / 400) * this.channels), tmp_prefill, 0, this.channels * this.Fs / 400);
        }

        if (this.channels * (this.encoder_buffer - (frame_size + total_buffer)) > 0) {
            Arrays.MemMove(this.delay_buffer, this.channels * frame_size, 0, this.channels * (this.encoder_buffer - frame_size - total_buffer));
            System.arraycopy(pcm_buf, 0, this.delay_buffer, (this.channels * (this.encoder_buffer - frame_size - total_buffer)), (frame_size + total_buffer) * this.channels);
        } else {
            System.arraycopy(pcm_buf, (frame_size + total_buffer - this.encoder_buffer) * this.channels, this.delay_buffer, 0, this.encoder_buffer * this.channels);
        }

        /* gain_fade() and stereo_fade() need to be after the buffer copying
           because we don't want any of this to affect the SILK part */
        if (this.prev_HB_gain < CeltConstants.Q15ONE || HB_gain < CeltConstants.Q15ONE) {
            CodecHelpers.gain_fade(pcm_buf, 0,
                    this.prev_HB_gain, HB_gain, celt_mode.overlap, frame_size, this.channels, celt_mode.window, this.Fs);
        }

        this.prev_HB_gain = HB_gain;
        if (this.mode != OpusMode.MODE_HYBRID || this.stream_channels == 1) {
            this.silk_mode.stereoWidth_Q14 = Inlines.IMIN((1 << 14), 2 * Inlines.IMAX(0, equiv_rate - 30000));
        }
        if (this.energy_masking == null && this.channels == 2) {
            /* Apply stereo width reduction (at low bitrates) */
            if (this.hybrid_stereo_width_Q14 < (1 << 14) || this.silk_mode.stereoWidth_Q14 < (1 << 14)) {
                int g1, g2;
                g1 = this.hybrid_stereo_width_Q14;
                g2 = (this.silk_mode.stereoWidth_Q14);
                g1 = g1 == 16384 ? CeltConstants.Q15ONE : Inlines.SHL16(g1, 1);
                g2 = g2 == 16384 ? CeltConstants.Q15ONE : Inlines.SHL16(g2, 1);
                CodecHelpers.stereo_fade(pcm_buf, g1, g2, celt_mode.overlap,
                        frame_size, this.channels, celt_mode.window, this.Fs);
                this.hybrid_stereo_width_Q14 = (short) (this.silk_mode.stereoWidth_Q14);
            }
        }

        if (this.mode != OpusMode.MODE_CELT_ONLY && enc.tell() + 17 + 20 * ((this.mode == OpusMode.MODE_HYBRID) ? 1 : 0) <= 8 * (max_data_bytes - 1)) {
            /* For SILK mode, the redundancy is inferred from the length */
            if (this.mode == OpusMode.MODE_HYBRID && (redundancy != 0 || enc.tell() + 37 <= 8 * nb_compr_bytes)) {
                enc.enc_bit_logp(redundancy, 12);
            }
            if (redundancy != 0) {
                int max_redundancy;
                enc.enc_bit_logp(celt_to_silk, 1);
                if (this.mode == OpusMode.MODE_HYBRID) {
                    max_redundancy = (max_data_bytes - 1) - nb_compr_bytes;
                } else {
                    max_redundancy = (max_data_bytes - 1) - ((enc.tell() + 7) >> 3);
                }
                /* Target the same bit-rate for redundancy as for the rest,
                   up to a max of 257 bytes */
                redundancy_bytes = Inlines.IMIN(max_redundancy, this.bitrate_bps / 1600);
                redundancy_bytes = Inlines.IMIN(257, Inlines.IMAX(2, redundancy_bytes));
                if (this.mode == OpusMode.MODE_HYBRID) {
                    enc.enc_uint(redundancy_bytes - 2, 256);
                }
            }
        } else {
            redundancy = 0;
        }

        if (redundancy == 0) {
            this.silk_bw_switch = 0;
            redundancy_bytes = 0;
        }
        if (this.mode != OpusMode.MODE_CELT_ONLY) {
            start_band = 17;
        }

        if (this.mode == OpusMode.MODE_SILK_ONLY) {
            ret = (enc.tell() + 7) >> 3;
            enc.enc_done();
            nb_compr_bytes = ret;
        } else {
            nb_compr_bytes = Inlines.IMIN((max_data_bytes - 1) - redundancy_bytes, nb_compr_bytes);
            enc.enc_shrink(nb_compr_bytes);
        }

        if (this.analysis.enabled && redundancy != 0 || this.mode != OpusMode.MODE_SILK_ONLY) {
            analysis_info.enabled = this.analysis.enabled;
            celt_enc.SetAnalysis(analysis_info);
        }
        /* 5 ms redundant frame for CELT->SILK */
        if (redundancy != 0 && celt_to_silk != 0) {
            int err;
            celt_enc.SetStartBand(0);
            celt_enc.SetVBR(false);
            err = celt_enc.celt_encode_with_ec(pcm_buf, 0, this.Fs / 200, data, data_ptr + nb_compr_bytes, redundancy_bytes, null);
            if (err < 0) {
                return OpusError.OPUS_INTERNAL_ERROR;
            }
            redundant_rng = celt_enc.GetFinalRange();
            celt_enc.ResetState();
        }

        celt_enc.SetStartBand(start_band);

        if (this.mode != OpusMode.MODE_SILK_ONLY) {
            if (this.mode != this.prev_mode && (this.prev_mode != OpusMode.MODE_AUTO && this.prev_mode != OpusMode.MODE_UNKNOWN)) {
                byte[] dummy = new byte[2];
                celt_enc.ResetState();

                /* Prefilling */
                celt_enc.celt_encode_with_ec(tmp_prefill, 0, this.Fs / 400, dummy, 0, 2, null);
                celt_enc.SetPrediction(0);
            }
            /* If false, we already busted the budget and we'll end up with a "PLC packet" */
            if (enc.tell() <= 8 * nb_compr_bytes) {
                ret = celt_enc.celt_encode_with_ec(pcm_buf, 0, frame_size, null, 0, nb_compr_bytes, enc);
                if (ret < 0) {
                    return OpusError.OPUS_INTERNAL_ERROR;
                }
            }
        }

        /* 5 ms redundant frame for SILK->CELT */
        if (redundancy != 0 && celt_to_silk == 0) {
            int err;
            byte[] dummy = new byte[2];
            int N2, N4;
            N2 = this.Fs / 200;
            N4 = this.Fs / 400;

            celt_enc.ResetState();
            celt_enc.SetStartBand(0);
            celt_enc.SetPrediction(0);

            /* NOTE: We could speed this up slightly (at the expense of code size) by just adding a function that prefills the buffer */
            celt_enc.celt_encode_with_ec(pcm_buf, (this.channels * (frame_size - N2 - N4)), N4, dummy, 0, 2, null);

            err = celt_enc.celt_encode_with_ec(pcm_buf, (this.channels * (frame_size - N2)), N2, data, data_ptr + nb_compr_bytes, redundancy_bytes, null);
            if (err < 0) {
                return OpusError.OPUS_INTERNAL_ERROR;
            }
            redundant_rng = celt_enc.GetFinalRange();
        }

        /* Signalling the mode in the first byte */
        data_ptr -= 1;
        data[data_ptr] = CodecHelpers.gen_toc(this.mode, this.Fs / frame_size, curr_bandwidth, this.stream_channels);

        this.rangeFinal = ((int) enc.rng) ^ redundant_rng;

        if (to_celt != 0) {
            this.prev_mode = OpusMode.MODE_CELT_ONLY;
        } else {
            this.prev_mode = this.mode;
        }
        this.prev_channels = this.stream_channels;
        this.prev_framesize = frame_size;

        this.first = 0;

        /* In the unlikely case that the SILK encoder busted its target, tell
           the decoder to call the PLC */
        if (enc.tell() > (max_data_bytes - 1) * 8) {
            if (max_data_bytes < 2) {
                return OpusError.OPUS_BUFFER_TOO_SMALL;
            }
            data[data_ptr + 1] = 0;
            ret = 1;
            this.rangeFinal = 0;
        } else if (this.mode == OpusMode.MODE_SILK_ONLY && redundancy == 0) {
            /*When in LPC only mode it's perfectly
              reasonable to strip off trailing zero bytes as
              the required range decoder behavior is to
              fill these in. This can't be done when the MDCT
              modes are used because the decoder needs to know
              the actual length for allocation purposes.*/
            while (ret > 2 && data[data_ptr + ret] == 0) {
                ret--;
            }
        }
        /* Count ToC and redundancy */
        ret += 1 + redundancy_bytes;
        if (this.use_vbr == 0) {
            if (OpusRepacketizer.padPacket(data, data_ptr, ret, max_data_bytes) != OpusError.OPUS_OK) {
                return OpusError.OPUS_INTERNAL_ERROR;
            }
            ret = max_data_bytes;
        }

        return ret;
    }

    /**
     * Encodes an Opus frame, putting the output into a specified data buffer
     * @param in_pcm 16-bit input signal (Interleaved if stereo), in a short array. Length should be at least frame_size * channels
     * @param pcm_offset Offset to use when reading the in_pcm buffer
     * @param frame_size The number of samples _per channel_ in the inpus signal. The frame size must be a valid Opus framesize for the given sample rate.
     * For example, at 48Khz the permitted values are 120, 240, 480, 960, 1920, and 2880. Passing in a duration of less than 10ms
     * (480 samples at 48Khz) will prevent the encoder from using FEC, DTX, or hybrid modes.
     * @param out_data Destination buffer for the output payload. This must contain at least max_data_bytes
     * @param out_data_offset The offset to use when writing to the output data buffer
     * @param max_data_bytes The maximum amount of space allocated for the output payload. This may be used to impose
     * an upper limit on the instant bitrate, but should not be used as the only bitrate control (use setBitrate for that)
     * @return The length of the encoded packet, in bytes
     * @throws OpusException 
     */
    public int encode(short[] in_pcm, int pcm_offset, int frame_size,
            byte[] out_data, int out_data_offset, int max_data_bytes) throws OpusException {
        // Check that the caller is telling the truth about its input buffers
        if (out_data_offset + max_data_bytes > out_data.length) {
            throw new IllegalArgumentException("Output buffer is too small: Stated size is " + max_data_bytes + " bytes, actual size is " + (out_data.length - out_data_offset) + " bytes");
        }

        int delay_compensation;
        if (this.application == OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY) {
            delay_compensation = 0;
        } else {
            delay_compensation = this.delay_compensation;
        }

        int internal_frame_size = CodecHelpers.compute_frame_size(in_pcm, pcm_offset, frame_size,
                this.variable_duration, this.channels, this.Fs, this.bitrate_bps,
                delay_compensation, this.analysis.subframe_mem, this.analysis.enabled);

        // Check that input pcm length is >= frame_size
        if (pcm_offset + internal_frame_size > in_pcm.length) {
            throw new IllegalArgumentException("Not enough samples provided in input signal: Expected " + internal_frame_size + " samples, found " + (in_pcm.length - pcm_offset));
        }

        try {
            int ret = opus_encode_native(in_pcm, pcm_offset, internal_frame_size, out_data, out_data_offset, max_data_bytes, 16,
                    in_pcm, pcm_offset, frame_size, 0, -2, this.channels, 0);

            if (ret < 0) {
                // An error happened; report it
                if (ret == OpusError.OPUS_BAD_ARG) {
                    throw new IllegalArgumentException("OPUS_BAD_ARG while encoding");
                }
                throw new OpusException("An error occurred during encoding", ret);
            }

            return ret;
        } catch (ArithmeticException e) {
            throw new OpusException("Internal error during encoding: " + e.getMessage());
        }
    }

    /**
     * Encodes an Opus frame, putting the output into a specified data buffer
     * @param in_pcm 16-bit input signal (Interleaved if stereo), in a little endian byte array. Length should be at least frame_size * channels * 2
     * @param pcm_offset Offset to use when reading the in_pcm buffer
     * @param frame_size The number of samples _per channel_ in the inpus signal. The frame size must be a valid Opus framesize for the given sample rate.
     * For example, at 48Khz the permitted values are 120, 240, 480, 960, 1920, and 2880. Passing in a duration of less than 10ms
     * (480 samples at 48Khz) will prevent the encoder from using FEC, DTX, or hybrid modes.
     * @param out_data Destination buffer for the output payload. This must contain at least max_data_bytes
     * @param out_data_offset The offset to use when writing to the output data buffer
     * @param max_data_bytes The maximum amount of space allocated for the output payload. This may be used to impose
     * an upper limit on the instant bitrate, but should not be used as the only bitrate control (use setBitrate for that)
     * @return The length of the encoded packet, in bytes
     * @throws OpusException 
     */
    public int encode(byte[] in_pcm, int pcm_offset, int frame_size,
            byte[] out_data, int out_data_offset, int max_data_bytes) throws OpusException {
    	//Convert byte array to short array
    	short[] spcm = new short[(in_pcm.length - pcm_offset) / 2];
		for (int c = pcm_offset; c < spcm.length; c++) {
			short x = (short) ((in_pcm[(c * 2)]) & 0xff);
			short y = (short) ((in_pcm[(c * 2) + 1]) << 8);
			spcm[c] = (short) (x | y);
		}
		return encode(spcm, 0, frame_size, out_data, out_data_offset, max_data_bytes);
    }

    /// <summary>
    /// Gets or sets the application (or signal type) of the input signal. This hints
    /// to the encoder what type of details we want to preserve in the encoding.
    /// This cannot be changed after the encoder has started
    /// </summary>
    public OpusApplication getApplication() {
        return application;
    }

    public void setApplication(OpusApplication value) {
        if (first == 0 && application != value) {
            throw new IllegalArgumentException("Application cannot be changed after encoding has started");
        }

        application = value;
    }

    /// <summary>
    /// Gets or sets the bitrate for encoder, in bits per second. Valid bitrates are be between 6K (6144) and 510K (522240)
    /// </summary>
    public int getBitrate() {
        return user_bitrate_to_bitrate(prev_framesize, 1276);
    }

    public void setBitrate(int value) {
        if (value != OpusConstants.OPUS_AUTO && value != OpusConstants.OPUS_BITRATE_MAX) {
            if (value <= 0) {
                throw new IllegalArgumentException("Bitrate must be positive");
            } else if (value <= 500) {
                value = 500;
            } else if (value > 300000 * channels) {
                value = 300000 * channels;
            }
        }

        user_bitrate_bps = value;
    }

    /// <summary>
    /// Gets or sets the maximum number of channels to be encoded. This can be used to force a downmix from stereo to mono if stereo
    /// separation is not important
    /// </summary>
    public int getForceChannels() {
        return force_channels;
    }

    public void setForceChannels(int value) {
        if ((value < 1 || value > channels) && value != OpusConstants.OPUS_AUTO) {
            throw new IllegalArgumentException("Force channels must be <= num. of channels");
        }

        force_channels = value;
    }

    /// <summary>
    /// Gets or sets the maximum bandwidth to be used by the encoder.
    /// </summary>
    public OpusBandwidth getMaxBandwidth() {
        return max_bandwidth;
    }

    public void setMaxBandwidth(OpusBandwidth value) {
        max_bandwidth = value;
        if (max_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND) {
            silk_mode.maxInternalSampleRate = 8000;
        } else if (max_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND) {
            silk_mode.maxInternalSampleRate = 12000;
        } else {
            silk_mode.maxInternalSampleRate = 16000;
        }
    }

    /// <summary>
    /// Gets or sets the "preferred" encoded bandwidth
    /// </summary>
    public OpusBandwidth getBandwidth() {
        return bandwidth;
    }

    public void setBandwidth(OpusBandwidth value) {
        user_bandwidth = value;
        if (user_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND) {
            silk_mode.maxInternalSampleRate = 8000;
        } else if (user_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND) {
            silk_mode.maxInternalSampleRate = 12000;
        } else {
            silk_mode.maxInternalSampleRate = 16000;
        }
    }

    /// <summary>
    /// Gets or sets a flag to enable Discontinuous Transmission mode. This mode is only available in the SILK encoder
    /// (Bitrate &lt; 40Kbit/s and/or ForceMode == SILK). When enabled, the encoder detects silence and background noise
    /// and reduces the number of output packets, with up to 600ms in between separate packet transmissions.
    /// </summary>
    public boolean getUseDTX() {
        return silk_mode.useDTX != 0;
    }

    public void setUseDTX(boolean value) {
        silk_mode.useDTX = value ? 1 : 0;
    }

    /// <summary>
    /// Gets or sets the encoder complexity, between 0 and 10
    /// </summary>
    public int getComplexity() {
        return silk_mode.complexity;
    }

    public void setComplexity(int value) {
        if (value < 0 || value > 10) {
            throw new IllegalArgumentException("Complexity must be between 0 and 10");
        }
        silk_mode.complexity = value;
        Celt_Encoder.SetComplexity(value);
    }

    /// <summary>
    /// Gets or sets a flag to enable Forward Error Correction. This mode is only available in the SILK encoder
    /// (Bitrate &lt; 40Kbit/s and/or ForceMode == SILK). When enabled, lost packets can be partially recovered
    /// by decoding data stored in the following packet.
    /// </summary>
    public boolean getUseInbandFEC() {
        return silk_mode.useInBandFEC != 0;
    }

    public void setUseInbandFEC(boolean value) {
        silk_mode.useInBandFEC = value ? 1 : 0;
    }

    /// <summary>
    /// Gets or sets the expected amount of packet loss in the transmission medium, from 0 to 100.
    /// Only applies if UseInbandFEC is also enabled, and the encoder is in SILK mode.
    /// </summary>
    public int getPacketLossPercent() {
        return silk_mode.packetLossPercentage;
    }

    public void setPacketLossPercent(int value) {
        if (value < 0 || value > 100) {
            throw new IllegalArgumentException("Packet loss must be between 0 and 100");
        }
        silk_mode.packetLossPercentage = value;
        Celt_Encoder.SetPacketLossPercent(value);
    }

    /// <summary>
    /// Gets or sets a flag to enable Variable Bitrate encoding. This is recommended as it generally improves audio quality
    /// with little impact on average bitrate
    /// </summary>
    public boolean getUseVBR() {
        return use_vbr != 0;
    }

    public void setUseVBR(boolean value) {
        use_vbr = value ? 1 : 0;
        silk_mode.useCBR = value ? 0 : 1;
    }

    /// <summary>
    /// Gets or sets a flag to enable constrained VBR. This only applies when the encoder is in CELT mode (i.e. high bitrates)
    /// </summary>
    public boolean getUseConstrainedVBR() {
        return vbr_constraint != 0;
    }

    public void setUseConstrainedVBR(boolean value) {
        vbr_constraint = value ? 1 : 0;
    }

    /// <summary>
    /// Gets or sets a hint to the encoder for what type of audio is being processed, voice or music 
    /// </summary>
    public OpusSignal getSignalType() {
        return signal_type;
    }

    public void setSignalType(OpusSignal value) {
        signal_type = value;
    }

    /// <summary>
    /// Gets the number of samples of audio that are being stored in a buffer and are therefore contributing to latency.
    /// </summary>
    public int getLookahead() {
        int returnVal = Fs / 400;
        if (application != OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY) {
            returnVal += delay_compensation;
        }

        return returnVal;
    }

    /// <summary>
    /// Gets the encoder's input sample rate.
    /// </summary>
    public int getSampleRate() {
        return Fs;
    }

    public int getFinalRange() {
        return rangeFinal;
    }

    /// <summary>
    /// Gets or sets the bit resolution of the input audio signal. Though the encoder always uses 16-bit internally, this can help
    /// it make better decisions about bandwidth and cutoff values
    /// </summary>
    public int getLSBDepth() {
        return lsb_depth;
    }

    public void setLSBDepth(int value) {
        if (value < 8 || value > 24) {
            throw new IllegalArgumentException("LSB depth must be between 8 and 24");
        }

        lsb_depth = value;
    }

    /// <summary>
    /// Gets or sets a fixed length for each encoded frame. Typically, the encoder just chooses a frame duration based on the input length
    /// and the current mode. This can be used to enforce an exact length if it is required by your application (e.g. monotonous transmission)
    /// </summary>
    public OpusFramesize getExpertFrameDuration() {
        return variable_duration;
    }

    public void setExpertFrameDuration(OpusFramesize value) {
        variable_duration = value;
        Celt_Encoder.SetExpertFrameDuration(value);
    }

    /// <summary>
    /// Gets or sets a user-forced mode for the encoder. There are three modes, SILK, HYBRID, and CELT. Silk can only encode below 40Kbit/s and is best suited
    /// for speech. Silk also has modes such as FEC which may be desirable. Celt sounds better at higher bandwidth and is comparable to AAC.
    /// Hybrid is used to create a smooth transition between the two modes. Note that this value may not always be honored due to other factors such
    /// as frame size and bitrate.
    /// </summary>
    public OpusMode getForceMode() {
        return user_forced_mode;
    }

    public void setForceMode(OpusMode value) {
        user_forced_mode = value;
    }

    /// <summary>
    /// Gets or sets a value indicating that this stream is a low-frequency channel. This is used when encoding 5.1 surround audio.
    /// </summary>
    public boolean getIsLFE() {
        return lfe != 0;
    }

    public void setIsLFE(boolean value) {
        lfe = value ? 1 : 0;
        Celt_Encoder.SetLFE(value ? 1 : 0);
    }

    /// <summary>
    /// Gets or sets a flag to enable prediction, which does something.
    /// </summary>
    public boolean getPredictionDisabled() {
        return silk_mode.reducedDependency != 0;
    }

    public void setPredictionDisabled(boolean value) {
        silk_mode.reducedDependency = value ? 1 : 0;
    }

    /// <summary>
    /// Gets or sets a value indicating whether neural net analysis functions should be enabled, increasing encode quality
    /// at the expense of speed.
    /// </summary>
    public boolean getEnableAnalysis() {
        return analysis.enabled;
    }

    public void setEnableAnalysis(boolean value) {
        analysis.enabled = value;
    }

    void SetEnergyMask(int[] value) {
        energy_masking = value;
        Celt_Encoder.SetEnergyMask(value);
    }

    CeltMode GetCeltMode() {
        return Celt_Encoder.GetMode();
    }
}
