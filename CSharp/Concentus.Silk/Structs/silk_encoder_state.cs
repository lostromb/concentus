using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Encoder state
    /// </summary>
    public class silk_encoder_state
    {
        public /*readonly*/ Pointer<int> In_HP_State = Pointer.Malloc<int>(2);                  /* High pass filter state                                           */
        public int variable_HP_smth1_Q15 = 0;             /* State of first smoother                                          */
        public int variable_HP_smth2_Q15 = 0;             /* State of second smoother                                         */
        public /*readonly*/ silk_LP_state sLP = new silk_LP_state();                               /* Low pass filter state                                            */
        public /*readonly*/ silk_VAD_state sVAD = new silk_VAD_state();                              /* Voice activity detector state                                    */
        public /*readonly*/ silk_nsq_state sNSQ = new silk_nsq_state();                              /* Noise Shape Quantizer State                                      */
        public /*readonly*/ Pointer<short> prev_NLSFq_Q15 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);   /* Previously quantized NLSF vector                                 */
        public int speech_activity_Q8 = 0;                /* Speech activity                                                  */
        public int allow_bandwidth_switch = 0;            /* Flag indicating that switching of internal bandwidth is allowed  */
        public sbyte LBRRprevLastGainIndex = 0;
        public sbyte prevSignalType = 0;
        public int prevLag = 0;
        public int pitch_LPC_win_length = 0;
        public int max_pitch_lag = 0;                     /* Highest possible pitch lag (samples)                             */
        public int API_fs_Hz = 0;                         /* API sampling frequency (Hz)                                      */
        public int prev_API_fs_Hz = 0;                    /* Previous API sampling frequency (Hz)                             */
        public int maxInternal_fs_Hz = 0;                 /* Maximum internal sampling frequency (Hz)                         */
        public int minInternal_fs_Hz = 0;                 /* Minimum internal sampling frequency (Hz)                         */
        public int desiredInternal_fs_Hz = 0;             /* Soft request for internal sampling frequency (Hz)                */
        public int fs_kHz = 0;                            /* Internal sampling frequency (kHz)                                */
        public int nb_subfr = 0;                          /* Number of 5 ms subframes in a frame                              */
        public int frame_length = 0;                      /* Frame length (samples)                                           */
        public int subfr_length = 0;                      /* Subframe length (samples)                                        */
        public int ltp_mem_length = 0;                    /* Length of LTP memory                                             */
        public int la_pitch = 0;                          /* Look-ahead for pitch analysis (samples)                          */
        public int la_shape = 0;                          /* Look-ahead for noise shape analysis (samples)                    */
        public int shapeWinLength = 0;                    /* Window length for noise shape analysis (samples)                 */
        public int TargetRate_bps = 0;                    /* Target bitrate (bps)                                             */
        public int PacketSize_ms = 0;                     /* Number of milliseconds to put in each packet                     */
        public int PacketLoss_perc = 0;                   /* Packet loss rate measured by farend                              */
        public int frameCounter = 0;
        public int Complexity = 0;                        /* Complexity setting                                               */
        public int nStatesDelayedDecision = 0;            /* Number of states in delayed decision quantization                */
        public int useInterpolatedNLSFs = 0;              /* Flag for using NLSF interpolation                                */
        public int shapingLPCOrder = 0;                   /* Filter order for noise shaping filters                           */
        public int predictLPCOrder = 0;                   /* Filter order for prediction filters                              */
        public int pitchEstimationComplexity = 0;         /* Complexity level for pitch estimator                             */
        public int pitchEstimationLPCOrder = 0;           /* Whitening filter order for pitch estimator                       */
        public int pitchEstimationThreshold_Q16 = 0;      /* Threshold for pitch estimator                                    */
        public int LTPQuantLowComplexity = 0;             /* Flag for low complexity LTP quantization                         */
        public int mu_LTP_Q9 = 0;                         /* Rate-distortion tradeoff in LTP quantization                     */
        public int sum_log_gain_Q7 = 0;                   /* Cumulative max prediction gain                                   */
        public int NLSF_MSVQ_Survivors = 0;               /* Number of survivors in NLSF MSVQ                                 */
        public int first_frame_after_reset = 0;           /* Flag for deactivating NLSF interpolation, pitch prediction       */
        public int controlled_since_last_payload = 0;     /* Flag for ensuring codec_control only runs once per packet        */
        public int warping_Q16 = 0;                       /* Warping parameter for warped noise shaping                       */
        public int useCBR = 0;                            /* Flag to enable constant bitrate                                  */
        public int prefillFlag = 0;                       /* Flag to indicate that only buffers are prefilled, no coding      */
        public Pointer<byte> pitch_lag_low_bits_iCDF = null;          /* Pointer to iCDF table for low bits of pitch lag index            */
        public Pointer<byte> pitch_contour_iCDF = null;               /* Pointer to iCDF table for pitch contour index                    */
        public silk_NLSF_CB_struct psNLSF_CB = null;                        /* Pointer to NLSF codebook                                         */
        public /*readonly*/ Pointer<int> input_quality_bands_Q15 = Pointer.Malloc<int>(SilkConstants.VAD_N_BANDS);
        public int input_tilt_Q15 = 0;
        public int SNR_dB_Q7 = 0;                         /* Quality setting                                                  */

        public /*readonly*/ Pointer<sbyte> VAD_flags = Pointer.Malloc<sbyte>(SilkConstants.MAX_FRAMES_PER_PACKET);
        public sbyte LBRR_flag = 0;
        public /*readonly*/ Pointer<int> LBRR_flags = Pointer.Malloc<int>(SilkConstants.MAX_FRAMES_PER_PACKET);

        public /*readonly*/ SideInfoIndices indices = new SideInfoIndices();
        public /*readonly*/ Pointer<sbyte> pulses = Pointer.Malloc<sbyte>(SilkConstants.MAX_FRAME_LENGTH);

        public int arch = 0;

        /* Input/output buffering */
        public /*readonly*/ Pointer<short> inputBuf = Pointer.Malloc<short>(SilkConstants.MAX_FRAME_LENGTH + 2);  /* Buffer containing input signal                                   */
        public int inputBufIx = 0;
        public int nFramesPerPacket = 0;
        public int nFramesEncoded = 0;                    /* Number of frames analyzed in current packet                      */

        public int nChannelsAPI = 0;
        public int nChannelsInternal = 0;
        public int channelNb = 0;

        /* Parameters For LTP scaling Control */
        public int frames_since_onset = 0;

        /* Specifically for entropy coding */
        public int ec_prevSignalType = 0;
        public short ec_prevLagIndex = 0;

        public /*readonly*/ silk_resampler_state_struct resampler_state = new silk_resampler_state_struct();

        /* DTX */
        public int useDTX = 0;                            /* Flag to enable DTX                                               */
        public int inDTX = 0;                             /* Flag to signal DTX period                                        */
        public int noSpeechCounter = 0;                   /* Counts concecutive nonactive frames, used by DTX                 */

        /* Inband Low Bitrate Redundancy (LBRR) data */
        public int useInBandFEC = 0;                      /* Saves the API setting for query                                  */
        public int LBRR_enabled = 0;                      /* Depends on useInBandFRC, bitrate and packet loss rate            */
        public int LBRR_GainIncreases = 0;                /* Gains increment for coding LBRR frames                           */
        public /*readonly*/ Pointer<SideInfoIndices> indices_LBRR = Pointer.Malloc<SideInfoIndices>(SilkConstants.MAX_FRAMES_PER_PACKET);
        public /*readonly*/ Pointer<Pointer<sbyte>> pulses_LBRR = Arrays.InitTwoDimensionalArrayPointer<sbyte>(SilkConstants.MAX_FRAMES_PER_PACKET, SilkConstants.MAX_FRAME_LENGTH);

        public silk_encoder_state()
        {
            for (int c = 0; c < SilkConstants.MAX_FRAMES_PER_PACKET; c++)
            {
                indices_LBRR[c] = new SideInfoIndices();
            }
        }

        public void Reset()
        {
            In_HP_State.MemSet(0, 2);
            variable_HP_smth1_Q15 = 0;
            variable_HP_smth2_Q15 = 0;
            sLP.Reset();
            sVAD.Reset();
            sNSQ.Reset();
            prev_NLSFq_Q15.MemSet(0, SilkConstants.MAX_LPC_ORDER);
            speech_activity_Q8 = 0;
            allow_bandwidth_switch = 0;
            LBRRprevLastGainIndex = 0;
            prevSignalType = 0;
            prevLag = 0;
            pitch_LPC_win_length = 0;
            max_pitch_lag = 0;
            API_fs_Hz = 0;
            prev_API_fs_Hz = 0;
            maxInternal_fs_Hz = 0;
            minInternal_fs_Hz = 0;
            desiredInternal_fs_Hz = 0;
            fs_kHz = 0;
            nb_subfr = 0;
            frame_length = 0;
            subfr_length = 0;
            ltp_mem_length = 0;
            la_pitch = 0;
            la_shape = 0;
            shapeWinLength = 0;
            TargetRate_bps = 0;
            PacketSize_ms = 0;
            PacketLoss_perc = 0;
            frameCounter = 0;
            Complexity = 0;
            nStatesDelayedDecision = 0;
            useInterpolatedNLSFs = 0;
            shapingLPCOrder = 0;
            predictLPCOrder = 0;
            pitchEstimationComplexity = 0;
            pitchEstimationLPCOrder = 0;
            pitchEstimationThreshold_Q16 = 0;
            LTPQuantLowComplexity = 0;
            mu_LTP_Q9 = 0;
            sum_log_gain_Q7 = 0;
            NLSF_MSVQ_Survivors = 0;
            first_frame_after_reset = 0;
            controlled_since_last_payload = 0;
            warping_Q16 = 0;
            useCBR = 0;
            prefillFlag = 0;
            pitch_lag_low_bits_iCDF = null;
            pitch_contour_iCDF = null;
            psNLSF_CB = null;
            input_quality_bands_Q15.MemSet(0, SilkConstants.VAD_N_BANDS);
            input_tilt_Q15 = 0;
            SNR_dB_Q7 = 0;
            VAD_flags.MemSet(0, SilkConstants.MAX_FRAMES_PER_PACKET);
            LBRR_flag = 0;
            LBRR_flags.MemSet(0, SilkConstants.MAX_FRAMES_PER_PACKET);
            indices.Reset();
            pulses.MemSet(0, SilkConstants.MAX_FRAME_LENGTH);
            arch = 0;
            inputBuf.MemSet(0, SilkConstants.MAX_FRAME_LENGTH + 2);
            inputBufIx = 0;
            nFramesPerPacket = 0;
            nFramesEncoded = 0;
            nChannelsAPI = 0;
            nChannelsInternal = 0;
            channelNb = 0;
            frames_since_onset = 0;
            ec_prevSignalType = 0;
            ec_prevLagIndex = 0;
            resampler_state.Reset();
            useDTX = 0;
            inDTX = 0;
            noSpeechCounter = 0;
            useInBandFEC = 0;
            LBRR_enabled = 0;
            LBRR_GainIncreases = 0;
            for (int c = 0; c < SilkConstants.MAX_FRAMES_PER_PACKET; c++)
            {
                indices_LBRR[c].Reset();
                pulses_LBRR[c].MemSet(0, SilkConstants.MAX_FRAME_LENGTH);
            }
        }
    }
}
