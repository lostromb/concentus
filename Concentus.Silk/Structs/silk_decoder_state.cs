using Concentus.Common;
using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Decoder state
    /// </summary>
    public class silk_decoder_state
    {
        public int prev_gain_Q16 = 0;
        public /*readonly*/ Pointer<int> exc_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_FRAME_LENGTH);
        public /*readonly*/ Pointer<int> sLPC_Q14_buf = Pointer.Malloc<int>(SilkConstants.MAX_LPC_ORDER);
        public /*readonly*/ Pointer<short> outBuf = Pointer.Malloc<short>(SilkConstants.MAX_FRAME_LENGTH + 2 * SilkConstants.MAX_SUB_FRAME_LENGTH);  /* Buffer for output signal                     */
        public int lagPrev = 0;                            /* Previous Lag                                                     */
        public sbyte LastGainIndex = 0;                      /* Previous gain index                                              */
        public int fs_kHz = 0;                             /* Sampling frequency in kHz                                        */
        public int fs_API_hz = 0;                          /* API sample frequency (Hz)                                        */
        public int nb_subfr = 0;                           /* Number of 5 ms subframes in a frame                              */
        public int frame_length = 0;                       /* Frame length (samples)                                           */
        public int subfr_length = 0;                       /* Subframe length (samples)                                        */
        public int ltp_mem_length = 0;                     /* Length of LTP memory                                             */
        public int LPC_order = 0;                          /* LPC order                                                        */
        public /*readonly*/ Pointer<short> prevNLSF_Q15 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);      /* Used to interpolate LSFs                                         */
        public int first_frame_after_reset = 0;            /* Flag for deactivating NLSF interpolation                         */
        public Pointer<byte> pitch_lag_low_bits_iCDF;           /* Pointer to iCDF table for low bits of pitch lag index            */
        public Pointer<byte> pitch_contour_iCDF;                /* Pointer to iCDF table for pitch contour index                    */

        /* For buffering payload in case of more frames per packet */
        public int nFramesDecoded = 0;
        public int nFramesPerPacket = 0;

        /* Specifically for entropy coding */
        public int ec_prevSignalType = 0;
        public short ec_prevLagIndex = 0;

        public /*readonly*/ Pointer<int> VAD_flags = Pointer.Malloc<int>(SilkConstants.MAX_FRAMES_PER_PACKET);
        public int LBRR_flag = 0;
        public /*readonly*/ Pointer<int> LBRR_flags = Pointer.Malloc<int>(SilkConstants.MAX_FRAMES_PER_PACKET);

        public /*readonly*/ silk_resampler_state_struct resampler_state = new silk_resampler_state_struct();

        public silk_NLSF_CB_struct psNLSF_CB = null;                         /* Pointer to NLSF codebook                                         */

        /* Quantization indices */
        public /*readonly*/ SideInfoIndices indices = new SideInfoIndices();

        /* CNG state */
        public /*readonly*/ silk_CNG_struct sCNG = new silk_CNG_struct();

        /* Stuff used for PLC */
        public int lossCnt = 0;
        public int prevSignalType = 0;

        public /*readonly*/ silk_PLC_struct sPLC = new silk_PLC_struct();
        
        public void Reset()
        {
            prev_gain_Q16 = 0;
            exc_Q14.MemSet(0, SilkConstants.MAX_FRAME_LENGTH);
            sLPC_Q14_buf.MemSet(0, SilkConstants.MAX_LPC_ORDER);
            outBuf.MemSet(0, SilkConstants.MAX_FRAME_LENGTH + 2 * SilkConstants.MAX_SUB_FRAME_LENGTH);
            lagPrev = 0;
            LastGainIndex = 0;
            fs_kHz = 0;
            fs_API_hz = 0;
            nb_subfr = 0;
            frame_length = 0;
            subfr_length = 0;
            ltp_mem_length = 0;
            LPC_order = 0;
            prevNLSF_Q15.MemSet(0, SilkConstants.MAX_LPC_ORDER);
            first_frame_after_reset = 0;
            pitch_lag_low_bits_iCDF = null;
            pitch_contour_iCDF = null;
            nFramesDecoded = 0;
            nFramesPerPacket = 0;
            ec_prevSignalType = 0;
            ec_prevLagIndex = 0;
            VAD_flags.MemSet(0, SilkConstants.MAX_FRAMES_PER_PACKET);
            LBRR_flag = 0;
            LBRR_flags.MemSet(0, SilkConstants.MAX_FRAMES_PER_PACKET);
            resampler_state.Reset();
            psNLSF_CB = null;
            indices.Reset();
            sCNG.Reset();
            lossCnt = 0;
            prevSignalType = 0;
            sPLC.Reset();
        }

        /// <summary>
        /// Init Decoder State
        /// </summary>
        /// <param name="psDec">I/O  Decoder state pointer</param>
        /// <returns></returns>
        public static int silk_init_decoder(silk_decoder_state psDec)
        {
            /* Clear the entire encoder state, except anything copied */
            psDec.Reset();

            /* Used to deactivate LSF interpolation */
            psDec.first_frame_after_reset = 1;
            psDec.prev_gain_Q16 = 65536;

            /* Reset CNG state */
            silk_CNG_Reset(psDec);

            /* Reset PLC state */
            silk_PLC_Reset(psDec);

            return (0);
        }

        /// <summary>
        /// Resets CNG state
        /// </summary>
        /// <param name="psDec">I/O  Decoder state</param>
        public static void silk_CNG_Reset(silk_decoder_state psDec)
        {
            int i, NLSF_step_Q15, NLSF_acc_Q15;

            NLSF_step_Q15 = Inlines.silk_DIV32_16(short.MaxValue, psDec.LPC_order + 1);
            NLSF_acc_Q15 = 0;
            for (i = 0; i < psDec.LPC_order; i++)
            {
                NLSF_acc_Q15 += NLSF_step_Q15;
                psDec.sCNG.CNG_smth_NLSF_Q15[i] = Inlines.CHOP16(NLSF_acc_Q15);
            }
            psDec.sCNG.CNG_smth_Gain_Q16 = 0;
            psDec.sCNG.rand_seed = 3176576;
        }

        /// <summary>
        /// Resets PLC state
        /// </summary>
        /// <param name="psDec">I/O Decoder state</param>
        public static void silk_PLC_Reset(silk_decoder_state psDec)
        {
            psDec.sPLC.pitchL_Q8 = Inlines.silk_LSHIFT(psDec.frame_length, 8 - 1);
            psDec.sPLC.prevGain_Q16[0] = Inlines.SILK_FIX_CONST(1, 16);
            psDec.sPLC.prevGain_Q16[1] = Inlines.SILK_FIX_CONST(1, 16);
            psDec.sPLC.subfr_length = 20;
            psDec.sPLC.nb_subfr = 2;
        }
    }
}
