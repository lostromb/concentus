using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Opus;
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

        public void Reset()
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
        public void PartialReset()
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
    }
}
