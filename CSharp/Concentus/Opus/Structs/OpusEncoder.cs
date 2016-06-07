using Concentus.Celt;
using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Opus;
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
    public class OpusEncoder
    {
        // These are no longer necessary - they are only used for targeted memsets and copies
        // public int celt_enc_offset;
        // public int silk_enc_offset;
        public readonly silk_EncControlStruct silk_mode = new silk_EncControlStruct();
        public int application;
        public int channels;
        public int delay_compensation;
        public int force_channels;
        public int signal_type;
        public int user_bandwidth;
        public int max_bandwidth;
        public int user_forced_mode;
        public int voice_ratio;
        public int Fs;
        public int use_vbr;
        public int vbr_constraint;
        public int variable_duration;
        public int bitrate_bps;
        public int user_bitrate_bps;
        public int lsb_depth;
        public int encoder_buffer;
        public int lfe;
        public int arch;
        // public readonly TonalityAnalysisState analysis = new TonalityAnalysisState();

        // partial reset happens below this line
        public int stream_channels;
        public short hybrid_stereo_width_Q14;
        public int variable_HP_smth2_Q15;
        public int prev_HB_gain;
        public /*readonly*/ Pointer<int> hp_mem = Pointer.Malloc<int>(4);
        public int mode;
        public int prev_mode;
        public int prev_channels;
        public int prev_framesize;
        public int bandwidth;
        public int silk_bw_switch;
        /* Sampling rate (at the API level) */
        public int first;
        public Pointer<int> energy_masking;
        public readonly StereoWidthState width_mem = new StereoWidthState();
        public /*readonly*/ Pointer<int> delay_buffer = Pointer.Malloc<int>(OpusConstants.MAX_ENCODER_BUFFER * 2);
        // public int detected_bandwidth;
        public uint rangeFinal;

        // [Porting Note] There were originally "cabooses" that were tacked onto the end
        // of the struct without being explicitly included (since they have a variable size).
        // Here they are just included as an intrinsic variable.
        public readonly silk_encoder SilkEncoder = new silk_encoder();
        public readonly CELTEncoder CeltEncoder = new CELTEncoder();

        internal void Reset()
        {
            silk_mode.Reset();
            application = 0;
            channels = 0;
            delay_compensation = 0;
            force_channels = 0;
            signal_type = 0;
            user_bandwidth = 0;
            max_bandwidth = 0;
            user_forced_mode = 0;
            voice_ratio = 0;
            Fs = 0;
            use_vbr = 0;
            vbr_constraint = 0;
            variable_duration = 0;
            bitrate_bps = 0;
            user_bitrate_bps = 0;
            lsb_depth = 0;
            encoder_buffer = 0;
            lfe = 0;
            arch = 0;
            //analysis.Reset();
            PartialReset();
        }

        /// <summary>
        /// OPUS_ENCODER_RESET_START
        /// </summary>
        internal void PartialReset()
        {
            stream_channels = 0;
            hybrid_stereo_width_Q14 = 0;
            variable_HP_smth2_Q15 = 0;
            prev_HB_gain = 0;
            hp_mem.MemSet(0, 4);
            mode = 0;
            prev_mode = 0;
            prev_channels = 0;
            prev_framesize = 0;
            bandwidth = 0;
            silk_bw_switch = 0;
            first = 0;
            energy_masking = null;
            width_mem.Reset();
            delay_buffer.MemSet(0, OpusConstants.MAX_ENCODER_BUFFER * 2);
            //detected_bandwidth = 0;
            rangeFinal = 0;
            SilkEncoder.Reset();
            CeltEncoder.Reset();
        }

        public void ResetState()
        {
            silk_EncControlStruct dummy = new silk_EncControlStruct();
            //st.analysis.Reset();

            PartialReset();

            celt_encoder.opus_custom_encoder_ctl(CeltEncoder, OpusControl.OPUS_RESET_STATE);
            enc_API.silk_InitEncoder(SilkEncoder, arch, dummy);
            stream_channels = channels;
            hybrid_stereo_width_Q14 = 1 << 14;
            prev_HB_gain = CeltConstants.Q15ONE;
            first = 1;
            mode = OpusMode.MODE_HYBRID;
            bandwidth = OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;
            variable_HP_smth2_Q15 = Inlines.silk_LSHIFT(Inlines.silk_lin2log(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ), 8);
        }

        public void SetApplication(int value)
        {
            if ((value != OpusApplication.OPUS_APPLICATION_VOIP && value != OpusApplication.OPUS_APPLICATION_AUDIO
                    && value != OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY)
                || (first == 0 && application != value))
            {
                throw new ArgumentException("Unsupported application");
            }

            application = value;
        }

        public int GetApplication()
        {
            return application;
        }

        public void SetBitrate(int bitrate)
        {
            if (bitrate != OpusConstants.OPUS_AUTO && bitrate != OpusConstants.OPUS_BITRATE_MAX)
            {
                if (bitrate <= 0)
                    throw new ArgumentException("Bitrate must be positive");
                else if (bitrate <= 500)
                    bitrate = 500;
                else if (bitrate > (int)300000 * channels)
                    bitrate = (int)300000 * channels;
            }

            user_bitrate_bps = bitrate;
        }

        public int GetBitrate()
        {
            return opus_encoder.user_bitrate_to_bitrate(this, prev_framesize, 1276);
        }

        public void SetForceChannels(int value)
        {
            if ((value < 1 || value > channels) && value != OpusConstants.OPUS_AUTO)
            {
                throw new ArgumentException("Force channels must be <= num. of channels");
            }

            force_channels = value;
        }

        public int GetForceChannels()
        {
            return force_channels;
        }

        public void SetMaxBandwidth(int value)
        {
            if (value < OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND || value > OpusBandwidth.OPUS_BANDWIDTH_FULLBAND)
            {
                throw new ArgumentException("Max bandwidth must be within acceptable range");
            }
            max_bandwidth = value;
            if (max_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND)
            {
                silk_mode.maxInternalSampleRate = 8000;
            }
            else if (max_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
            {
                silk_mode.maxInternalSampleRate = 12000;
            }
            else {
                silk_mode.maxInternalSampleRate = 16000;
            }
        }

        public int GetMaxBandwidth()
        {
            return max_bandwidth;
        }

        public void SetBandwidth(int value)
        {
            if ((value < OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND || value > OpusBandwidth.OPUS_BANDWIDTH_FULLBAND) && value != OpusConstants.OPUS_AUTO)
            {
                throw new ArgumentException("Bandwidth must be within acceptable range");
            }
            user_bandwidth = value;
            if (user_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND)
            {
                silk_mode.maxInternalSampleRate = 8000;
            }
            else if (user_bandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
            {
                silk_mode.maxInternalSampleRate = 12000;
            }
            else {
                silk_mode.maxInternalSampleRate = 16000;
            }
        }

        public int GetBandwidth()
        {
            return bandwidth;
        }

        public void SetUseDTX(bool value)
        {
            silk_mode.useDTX = value ? 1 : 0;
        }

        public bool GetUseDTX()
        {
            return silk_mode.useDTX != 0;
        }

        public void SetComplexity(int value)
        {
            if (value < 0 || value > 10)
            {
                throw new ArgumentException("Complexity must be between 0 and 10");
            }
            silk_mode.complexity = value;
            celt_encoder.opus_custom_encoder_ctl(CeltEncoder, OpusControl.OPUS_SET_COMPLEXITY_REQUEST, (value));
        }

        public int GetComplexity()
        {
            return silk_mode.complexity;
        }

        public void SetUseInbandFEC(bool value)
        {
            silk_mode.useInBandFEC = value ? 1 : 0;
        }

        public bool GetUseInbandFEC()
        {
            return silk_mode.useInBandFEC != 0;
        }

        public void SetPacketLossPercent(int value)
        {
            if (value < 0 || value > 100)
            {
                throw new ArgumentException("Packet loss must be between 0 and 100");
            }
            silk_mode.packetLossPercentage = value;
            celt_encoder.opus_custom_encoder_ctl(CeltEncoder, OpusControl.OPUS_SET_PACKET_LOSS_PERC_REQUEST, value);
        }

        public int GetPacketLossPercent()
        {
            return silk_mode.packetLossPercentage;
        }

        public void SetVBR(bool value)
        {
            use_vbr = value ? 1 : 0;
            silk_mode.useCBR = value ? 0 : 1;
        }

        public bool GetVBR()
        {
            return use_vbr != 0;
        }

        public void SetVoiceRatio(int value)
        {
            if (value < -1 || value > 100)
            {
                throw new ArgumentException("Voice ratio must be between -1 and 100");
            }

            voice_ratio = value;
        }

        public int GetVoiceRatio()
        {
            return voice_ratio;
        }

        public void SetVBRConstraint(bool value)
        {
            vbr_constraint = value ? 1 : 0;
        }

        public bool GetVBRConstraint()
        {
            return vbr_constraint != 0;
        }

        public void SetSignalType(int value)
        {
            if (value != OpusConstants.OPUS_AUTO && value != OpusSignal.OPUS_SIGNAL_VOICE && value != OpusSignal.OPUS_SIGNAL_MUSIC)
            {
                throw new ArgumentException("Invalid signal type");
            }
            signal_type = value;
        }

        public int GetSignalType()
        {
            return signal_type;
        }

        public int GetLookahead()
        {
            int returnVal = Fs / 400;
            if (application != OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY)
                returnVal += delay_compensation;

            return returnVal;
        }

        public int GetSampleRate()
        {
            return Fs;
        }

        public uint GetFinalRange()
        {
            return rangeFinal;
        }

        public void SetLSBDepth(int value)
        {
            if (value < 8 || value > 24)
            {
                throw new ArgumentException("LSB depth must be between 8 and 24");
            }

            lsb_depth = value;
        }

        public int GetLSBDepth()
        {
            return lsb_depth;
        }

        public void SetExpertFrameDuration(int value)
        {
            if (value != OpusFramesize.OPUS_FRAMESIZE_ARG && value != OpusFramesize.OPUS_FRAMESIZE_2_5_MS &&
                            value != OpusFramesize.OPUS_FRAMESIZE_5_MS && value != OpusFramesize.OPUS_FRAMESIZE_10_MS &&
                            value != OpusFramesize.OPUS_FRAMESIZE_20_MS && value != OpusFramesize.OPUS_FRAMESIZE_40_MS &&
                            value != OpusFramesize.OPUS_FRAMESIZE_60_MS && value != OpusFramesize.OPUS_FRAMESIZE_VARIABLE)
            {
                throw new ArgumentException("Invalid frame size");
            }
            variable_duration = value;
            celt_encoder.opus_custom_encoder_ctl(CeltEncoder, OpusControl.OPUS_SET_EXPERT_FRAME_DURATION_REQUEST, (value));
        }

        public int GetExpertFrameDuration()
        {
            return variable_duration;
        }

        public void SetPredictionDisabled(bool value)
        {
            silk_mode.reducedDependency = value ? 1 : 0;
        }

        public bool GetPredictionDisabled()
        {
            return silk_mode.reducedDependency != 0;
        }

        public void SetForceMode(int value)
        {
            if ((value < OpusMode.MODE_SILK_ONLY || value > OpusMode.MODE_CELT_ONLY) && value != OpusConstants.OPUS_AUTO)
            {
                throw new ArgumentException("Unsupported mode (must be OpusMode.___ or OPUS_AUTO");
            }
            
            user_forced_mode = value;
        }

        public void SetLFE(int value)
        {
            lfe = value;
            celt_encoder.opus_custom_encoder_ctl(CeltEncoder, CeltControl.OPUS_SET_LFE_REQUEST, (value));
        }

        public void SetEnergyMask(Pointer<int> value)
        {
            energy_masking = value;
            celt_encoder.opus_custom_encoder_ctl(CeltEncoder, CeltControl.OPUS_SET_ENERGY_MASK_REQUEST, (value));
        }

        public CELTMode GetCeltMode()
        {
            BoxedValue<CELTMode> value = new BoxedValue<CELTMode>();
            celt_encoder.opus_custom_encoder_ctl(CeltEncoder, CeltControl.CELT_GET_MODE_REQUEST, (value));
            return value.Val;
        }
    }
}
