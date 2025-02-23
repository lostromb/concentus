/* Copyright (c) 2010-2011 Xiph.Org Foundation, Skype Limited
   Written by Jean-Marc Valin and Koen Vos */
/*
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

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

using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Old.Silk.Control;
using static HellaUnsafe.Old.Opus.Analysis;

namespace HellaUnsafe.Old.Opus
{
    internal static unsafe class opus_encoder
    {
        internal const int MAX_ENCODER_BUFFER = 480;
        internal const float PSEUDO_SNR_THRESHOLD = 316.23f;    /* 10^(25/10) */

        internal struct StereoWidthState
        {
            float XX, XY, YY;
            float smoothed_width;
            float max_follower;
        }

        internal unsafe struct OpusEncoder
        {
            internal int celt_enc_offset;
            internal int silk_enc_offset;
            internal silk_EncControlStruct silk_mode;
            internal int application;
            internal int channels;
            internal int delay_compensation;
            internal int force_channels;
            internal int signal_type;
            internal int user_bandwidth;
            internal int max_bandwidth;
            internal int user_forced_mode;
            internal int voice_ratio;
            internal int Fs;
            internal int use_vbr;
            internal int vbr_constraint;
            internal int variable_duration;
            internal int bitrate_bps;
            internal int user_bitrate_bps;
            internal int lsb_depth;
            internal int encoder_buffer;
            internal int lfe;
            internal int arch;
            internal int use_dtx;                 /* general DTX for both SILK and CELT */
            internal int fec_config;
            internal TonalityAnalysisState analysis;
            //#define OPUS_ENCODER_RESET_START stream_channels
            internal int stream_channels;
            internal short hybrid_stereo_width_Q14;
            internal int variable_HP_smth2_Q15;
            internal float prev_HB_gain;
            internal fixed float hp_mem[4];
            internal int mode;
            internal int prev_mode;
            internal int prev_channels;
            internal int prev_framesize;
            internal int bandwidth;
            /* Bandwidth determined automatically from the rate (before any other adjustment) */
            internal int auto_bandwidth;
            internal int silk_bw_switch;
            /* Sampling rate (at the API level) */
            internal int first;
            internal float* energy_masking;
            internal StereoWidthState width_mem;
            internal fixed float delay_buffer[MAX_ENCODER_BUFFER * 2];
            internal int detected_bandwidth;
            internal int nb_no_activity_ms_Q1;
            internal float peak_signal_energy;
            internal int nonfinal_frame; /* current frame is not the final in a packet */
            internal uint rangeFinal;
        };

        /* Transition tables for the voice and music. First column is the
           middle (memoriless) threshold. The second column is the hysteresis
           (difference with the middle) */
        internal static readonly int* mono_voice_bandwidth_thresholds = AllocateGlobalArray(new int[]
        {
            9000,  700, /* NB<->MB */
             9000,  700, /* MB<->WB */
            13500, 1000, /* WB<->SWB */
            14000, 2000, /* SWB<->FB */
        });

        internal static readonly int* mono_music_bandwidth_thresholds = AllocateGlobalArray(new int[]
        {
            9000,  700, /* NB<->MB */
             9000,  700, /* MB<->WB */
            11000, 1000, /* WB<->SWB */
            12000, 2000, /* SWB<->FB */
        });

        internal static readonly int* stereo_voice_bandwidth_thresholds = AllocateGlobalArray(new int[]
        {
            9000,  700, /* NB<->MB */
             9000,  700, /* MB<->WB */
            13500, 1000, /* WB<->SWB */
            14000, 2000, /* SWB<->FB */
        });

        internal static readonly int* stereo_music_bandwidth_thresholds = AllocateGlobalArray(new int[]
        {
            9000,  700, /* NB<->MB */
             9000,  700, /* MB<->WB */
            11000, 1000, /* WB<->SWB */
            12000, 2000, /* SWB<->FB */
        });

        /* Threshold bit-rates for switching between mono and stereo */
        internal const int stereo_voice_threshold = 19000;
        internal const int stereo_music_threshold = 17000;

        internal static readonly int* mode_thresholds_2D/*[2][2]*/ = AllocateGlobalArray(new int[]{
              /* voice */ /* music */
                64000,      10000, /* mono */
                44000,      10000, /* stereo */
        });

        internal static readonly int* fec_thresholds = AllocateGlobalArray(new int[]
        {
            12000, 1000, /* NB */
            14000, 1000, /* MB */
            16000, 1000, /* WB */
            20000, 1000, /* SWB */
            22000, 1000, /* FB */
        });


    }
}
