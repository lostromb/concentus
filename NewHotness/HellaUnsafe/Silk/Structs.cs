///***********************************************************************
//Copyright (c) 2006-2011, Skype Limited. All rights reserved.
//Redistribution and use in source and binary forms, with or without
//modification, are permitted provided that the following conditions
//are met:
//- Redistributions of source code must retain the above copyright notice,
//this list of conditions and the following disclaimer.
//- Redistributions in binary form must reproduce the above copyright
//notice, this list of conditions and the following disclaimer in the
//documentation and/or other materials provided with the distribution.
//- Neither the name of Internet Society, IETF or IETF Trust, nor the
//names of specific contributors, may be used to endorse or promote
//products derived from this software without specific prior written
//permission.
//THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
//AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
//IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
//ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
//LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
//CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
//SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
//INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
//CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
//ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
//POSSIBILITY OF SUCH DAMAGE.
//***********************************************************************/

//using HellaUnsafe.Common;
//using static HellaUnsafe.Silk.Define;
//using static HellaUnsafe.Silk.Macros;
//using static HellaUnsafe.Silk.TuningParameters;

//namespace HellaUnsafe.Silk
//{
//    /************************************/
//    /* Noise shaping quantization state */
//    /************************************/
//    internal unsafe struct silk_nsq_state
//    {
//        internal fixed short xq[2 * MAX_FRAME_LENGTH]; /* Buffer for quantized output signal                             */
//        internal fixed int sLTP_shp_Q14[2 * MAX_FRAME_LENGTH];
//        internal fixed int sLPC_Q14[MAX_SUB_FRAME_LENGTH + NSQ_LPC_BUF_LENGTH];
//        internal fixed int sAR2_Q14[MAX_SHAPE_LPC_ORDER];
//        internal int sLF_AR_shp_Q14;
//        internal int sDiff_shp_Q14;
//        internal int lagPrev;
//        internal int sLTP_buf_idx;
//        internal int sLTP_shp_buf_idx;
//        internal int rand_seed;
//        internal int prev_gain_Q16;
//        internal int rewhite_flag;
//    }

//    /********************************/
//    /* VAD state                    */
//    /********************************/
//    internal unsafe struct silk_VAD_state
//    {
//        internal fixed int AnaState[2];                  /* Analysis filterbank state: 0-8 kHz                                   */
//        internal fixed int AnaState1[2];                 /* Analysis filterbank state: 0-4 kHz                                   */
//        internal fixed int AnaState2[2];                 /* Analysis filterbank state: 0-2 kHz                                   */
//        internal fixed int XnrgSubfr[VAD_N_BANDS];       /* Subframe energies                                                    */
//        internal fixed int NrgRatioSmth_Q8[VAD_N_BANDS]; /* Smoothed energy level in each band                                   */
//        internal short HPstate;                        /* State of differentiator in the lowest band                           */
//        internal fixed int NL[VAD_N_BANDS];              /* Noise energy level in each band                                      */
//        internal fixed int inv_NL[VAD_N_BANDS];          /* Inverse noise energy level in each band                              */
//        internal fixed int NoiseLevelBias[VAD_N_BANDS];  /* Noise level estimator bias/offset                                    */
//        internal int counter;                        /* Frame counter used in the initial phase                              */
//    }

//    /* Variable cut-off low-pass filter state */
//    internal unsafe struct silk_LP_state
//    {
//        internal fixed int In_LP_State[2];           /* Low pass filter state */
//        internal int transition_frame_no;        /* Counter which is mapped to a cut-off frequency */
//        internal int mode;                       /* Operating mode, <0: switch down, >0: switch up; 0: do nothing           */
//        internal int saved_fs_kHz;               /* If non-zero, holds the last sampling rate before a bandwidth switching reset. */
//    }

//    /* Structure containing NLSF codebook */
//    internal struct silk_NLSF_CB_struct
//    {
//        internal short nVectors;
//        internal short order;
//        internal short quantStepSize_Q16;
//        internal short invQuantStepSize_Q6;
//        internal byte[] CB1_NLSF_Q8;
//        internal short[] CB1_Wght_Q9;
//        internal byte[] CB1_iCDF;
//        internal byte[] pred_Q8;
//        internal byte[] ec_sel;
//        internal byte[] ec_iCDF;
//        internal byte[] ec_Rates_Q5;
//        internal short[] deltaMin_Q15;
//    }

//    internal unsafe struct stereo_enc_state
//    {
//        internal fixed short pred_prev_Q13[2];
//        internal fixed short sMid[2];
//        internal fixed short sSide[2];
//        internal fixed int mid_side_amp_Q0[4];
//        internal short smth_width_Q14;
//        internal short width_prev_Q14;
//        internal short silent_side_len;
//        internal fixed sbyte predIx[MAX_FRAMES_PER_PACKET][ 2 ][ 3 ];
//        internal fixed sbyte mid_only_flags[MAX_FRAMES_PER_PACKET];
//    }

//    internal unsafe struct stereo_dec_state
//    {
//        internal fixed short pred_prev_Q13[2];
//        internal fixed short sMid[2];
//        internal fixed short sSide[2];
//    }

//    internal unsafe struct SideInfoIndices
//    {
//        internal fixed sbyte GainsIndices[MAX_NB_SUBFR];
//        internal fixed sbyte LTPIndex[MAX_NB_SUBFR];
//        internal fixed sbyte NLSFIndices[MAX_LPC_ORDER + 1];
//        internal short lagIndex;
//        internal sbyte contourIndex;
//        internal sbyte signalType;
//        internal sbyte quantOffsetType;
//        internal sbyte NLSFInterpCoef_Q2;
//        internal sbyte PERIndex;
//        internal sbyte LTP_scaleIndex;
//        internal sbyte Seed;
//    }

//    internal unsafe struct silk_encoder_state
//    {
//        internal fixed int In_HP_State[2];                  /* High pass filter state                                           */
//        internal int variable_HP_smth1_Q15;             /* State of first smoother                                          */
//        internal int variable_HP_smth2_Q15;             /* State of second smoother                                         */
//        internal silk_LP_state sLP;                               /* Low pass filter state                                            */
//        internal silk_VAD_state sVAD;                              /* Voice activity detector state                                    */
//        internal silk_nsq_state sNSQ;                              /* Noise Shape Quantizer State                                      */
//        internal fixed short prev_NLSFq_Q15[MAX_LPC_ORDER];   /* Previously quantized NLSF vector                                 */
//        internal int speech_activity_Q8;                /* Speech activity                                                  */
//        internal int allow_bandwidth_switch;            /* Flag indicating that switching of internal bandwidth is allowed  */
//        internal sbyte LBRRprevLastGainIndex;
//        internal sbyte prevSignalType;
//        internal int prevLag;
//        internal int pitch_LPC_win_length;
//        internal int max_pitch_lag;                     /* Highest possible pitch lag (samples)                             */
//        internal int API_fs_Hz;                         /* API sampling frequency (Hz)                                      */
//        internal int prev_API_fs_Hz;                    /* Previous API sampling frequency (Hz)                             */
//        internal int maxInternal_fs_Hz;                 /* Maximum internal sampling frequency (Hz)                         */
//        internal int minInternal_fs_Hz;                 /* Minimum internal sampling frequency (Hz)                         */
//        internal int desiredInternal_fs_Hz;             /* Soft request for internal sampling frequency (Hz)                */
//        internal int fs_kHz;                            /* Internal sampling frequency (kHz)                                */
//        internal int nb_subfr;                          /* Number of 5 ms subframes in a frame                              */
//        internal int frame_length;                      /* Frame length (samples)                                           */
//        internal int subfr_length;                      /* Subframe length (samples)                                        */
//        internal int ltp_mem_length;                    /* Length of LTP memory                                             */
//        internal int la_pitch;                          /* Look-ahead for pitch analysis (samples)                          */
//        internal int la_shape;                          /* Look-ahead for noise shape analysis (samples)                    */
//        internal int shapeWinLength;                    /* Window length for noise shape analysis (samples)                 */
//        internal int TargetRate_bps;                    /* Target bitrate (bps)                                             */
//        internal int PacketSize_ms;                     /* Number of milliseconds to put in each packet                     */
//        internal int PacketLoss_perc;                   /* Packet loss rate measured by farend                              */
//        internal int frameCounter;
//        internal int Complexity;                        /* Complexity setting                                               */
//        internal int nStatesDelayedDecision;            /* Number of states in delayed decision quantization                */
//        internal int useInterpolatedNLSFs;              /* Flag for using NLSF interpolation                                */
//        internal int shapingLPCOrder;                   /* Filter order for noise shaping filters                           */
//        internal int predictLPCOrder;                   /* Filter order for prediction filters                              */
//        internal int pitchEstimationComplexity;         /* Complexity level for pitch estimator                             */
//        internal int pitchEstimationLPCOrder;           /* Whitening filter order for pitch estimator                       */
//        internal int pitchEstimationThreshold_Q16;      /* Threshold for pitch estimator                                    */
//        internal int sum_log_gain_Q7;                   /* Cumulative max prediction gain                                   */
//        internal int NLSF_MSVQ_Survivors;               /* Number of survivors in NLSF MSVQ                                 */
//        internal int first_frame_after_reset;           /* Flag for deactivating NLSF interpolation, pitch prediction       */
//        internal int controlled_since_last_payload;     /* Flag for ensuring codec_control only runs once per packet        */
//        internal int warping_Q16;                       /* Warping parameter for warped noise shaping                       */
//        internal int useCBR;                            /* Flag to enable constant bitrate                                  */
//        internal int prefillFlag;                       /* Flag to indicate that only buffers are prefilled, no coding      */
//        internal byte* pitch_lag_low_bits_iCDF;          /* Pointer to iCDF table for low bits of pitch lag index            */
//        internal byte* pitch_contour_iCDF;               /* Pointer to iCDF table for pitch contour index                    */
//        internal silk_NLSF_CB_struct* psNLSF_CB;                        /* Pointer to NLSF codebook                                         */
//        internal fixed int input_quality_bands_Q15[VAD_N_BANDS];
//        internal int input_tilt_Q15;
//        internal int SNR_dB_Q7;                         /* Quality setting                                                  */

//        internal fixed sbyte VAD_flags[MAX_FRAMES_PER_PACKET];
//        internal sbyte LBRR_flag;
//        internal fixed int LBRR_flags[MAX_FRAMES_PER_PACKET];

//        internal SideInfoIndices indices;
//        internal fixed sbyte pulses[MAX_FRAME_LENGTH];

//        /* Input/output buffering */
//        internal fixed short inputBuf[MAX_FRAME_LENGTH + 2];  /* Buffer containing input signal                                   */
//        internal int inputBufIx;
//        internal int nFramesPerPacket;
//        internal int nFramesEncoded;                    /* Number of frames analyzed in current packet                      */

//        internal int nChannelsAPI;
//        internal int nChannelsInternal;
//        internal int channelNb;

//        /* Parameters For LTP scaling Control */
//        internal int frames_since_onset;

//        /* Specifically for entropy coding */
//        internal int ec_prevSignalType;
//        internal short ec_prevLagIndex;

//        internal silk_resampler_state_struct resampler_state;

//        /* DTX */
//        internal int useDTX;                            /* Flag to enable DTX                                               */
//        internal int inDTX;                             /* Flag to signal DTX period                                        */
//        internal int noSpeechCounter;                   /* Counts concecutive nonactive frames, used by DTX                 */

//        /* Inband Low Bitrate Redundancy (LBRR) data */
//        internal int useInBandFEC;                      /* Saves the API setting for query                                  */
//        internal int LBRR_enabled;                      /* Depends on useInBandFRC, bitrate and packet loss rate            */
//        internal int LBRR_GainIncreases;                /* Gains increment for coding LBRR frames                           */
//        internal SideInfoIndices[] indices_LBRR[MAX_FRAMES_PER_PACKET];
//        internal sbyte pulses_LBRR[MAX_FRAMES_PER_PACKET][MAX_FRAME_LENGTH];
//    }

//    internal unsafe struct silk_PLC_struct
//    {
//        internal int pitchL_Q8;                          /* Pitch lag to use for voiced concealment                          */
//        internal fixed short LTPCoef_Q14[LTP_ORDER];           /* LTP coeficients to use for voiced concealment                    */
//        internal fixed short prevLPC_Q12[MAX_LPC_ORDER];
//        internal int last_frame_lost;                    /* Was previous frame lost                                          */
//        internal int rand_seed;                          /* Seed for unvoiced signal generation                              */
//        internal short randScale_Q14;                      /* Scaling of unvoiced random signal                                */
//        internal int conc_energy;
//        internal int conc_energy_shift;
//        internal short prevLTP_scale_Q14;
//        internal fixed int prevGain_Q16[2];
//        internal int fs_kHz;
//        internal int nb_subfr;
//        internal int subfr_length;
//        internal int enable_deep_plc;
//    }

//    internal unsafe struct silk_CNG_struct
//    {
//        internal fixed int CNG_exc_buf_Q14[MAX_FRAME_LENGTH];
//        internal fixed short CNG_smth_NLSF_Q15[MAX_LPC_ORDER];
//        internal fixed int CNG_synth_state[MAX_LPC_ORDER];
//        internal int CNG_smth_Gain_Q16;
//        internal int rand_seed;
//        internal int fs_kHz;
//    }

//    internal unsafe struct silk_decoder_state
//    {
//        internal int prev_gain_Q16;
//        internal fixed int exc_Q14[MAX_FRAME_LENGTH];
//        internal fixed int sLPC_Q14_buf[MAX_LPC_ORDER];
//        internal fixed short outBuf[MAX_FRAME_LENGTH + 2 * MAX_SUB_FRAME_LENGTH];  /* Buffer for output signal                     */
//        internal int lagPrev;                            /* Previous Lag                                                     */
//        internal sbyte LastGainIndex;                      /* Previous gain index                                              */
//        internal int fs_kHz;                             /* Sampling frequency in kHz                                        */
//        internal int fs_API_hz;                          /* API sample frequency (Hz)                                        */
//        internal int nb_subfr;                           /* Number of 5 ms subframes in a frame                              */
//        internal int frame_length;                       /* Frame length (samples)                                           */
//        internal int subfr_length;                       /* Subframe length (samples)                                        */
//        internal int ltp_mem_length;                     /* Length of LTP memory                                             */
//        internal int LPC_order;                          /* LPC order                                                        */
//        internal fixed short prevNLSF_Q15[MAX_LPC_ORDER];      /* Used to interpolate LSFs                                         */
//        internal int first_frame_after_reset;            /* Flag for deactivating NLSF interpolation                         */
//        internal byte[] pitch_lag_low_bits_iCDF;           /* Pointer to iCDF table for low bits of pitch lag index            */
//        internal byte[] pitch_contour_iCDF;                /* Pointer to iCDF table for pitch contour index                    */

//        /* For buffering payload in case of more frames per packet */
//        internal int nFramesDecoded;
//        internal int nFramesPerPacket;

//        /* Specifically for entropy coding */
//        internal int ec_prevSignalType;
//        internal short ec_prevLagIndex;

//        internal fixed int VAD_flags[MAX_FRAMES_PER_PACKET];
//        internal int LBRR_flag;
//        internal fixed int LBRR_flags[MAX_FRAMES_PER_PACKET];

//        internal silk_resampler_state_struct resampler_state;

//        internal StructRef<silk_NLSF_CB_struct> psNLSF_CB;                         /* Pointer to NLSF codebook                                         */

//        /* Quantization indices */
//        internal SideInfoIndices indices;

//        /* CNG state */
//        internal silk_CNG_struct sCNG;

//        /* Stuff used for PLC */
//        internal int lossCnt;
//        internal int prevSignalType;

//        internal silk_PLC_struct sPLC;
//    }

//    internal unsafe struct silk_decoder_control
//    {
//        /* Prediction and coding parameters */
//        internal fixed int pitchL[MAX_NB_SUBFR];
//        internal fixed int Gains_Q16[MAX_NB_SUBFR];
//        /* Holds interpolated and final coefficients, 4-byte aligned */
//        internal short PredCoef_Q12[2][MAX_LPC_ORDER];
//        internal fixed short LTPCoef_Q14[LTP_ORDER * MAX_NB_SUBFR];
//        internal int LTP_scale_Q14;
//    }
//}
