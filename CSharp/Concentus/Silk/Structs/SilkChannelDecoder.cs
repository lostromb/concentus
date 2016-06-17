using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Silk.Enums;
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
    public class SilkChannelDecoder
    {
        public int prev_gain_Q16 = 0;
        public readonly Pointer<int> exc_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_FRAME_LENGTH);
        public readonly Pointer<int> sLPC_Q14_buf = Pointer.Malloc<int>(SilkConstants.MAX_LPC_ORDER);
        public readonly Pointer<short> outBuf = Pointer.Malloc<short>(SilkConstants.MAX_FRAME_LENGTH + 2 * SilkConstants.MAX_SUB_FRAME_LENGTH);  /* Buffer for output signal                     */
        public int lagPrev = 0;                            /* Previous Lag                                                     */
        public sbyte LastGainIndex = 0;                      /* Previous gain index                                              */
        public int fs_kHz = 0;                             /* Sampling frequency in kHz                                        */
        public int fs_API_hz = 0;                          /* API sample frequency (Hz)                                        */
        public int nb_subfr = 0;                           /* Number of 5 ms subframes in a frame                              */
        public int frame_length = 0;                       /* Frame length (samples)                                           */
        public int subfr_length = 0;                       /* Subframe length (samples)                                        */
        public int ltp_mem_length = 0;                     /* Length of LTP memory                                             */
        public int LPC_order = 0;                          /* LPC order                                                        */
        public readonly Pointer<short> prevNLSF_Q15 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);      /* Used to interpolate LSFs                                         */
        public int first_frame_after_reset = 0;            /* Flag for deactivating NLSF interpolation                         */
        public Pointer<byte> pitch_lag_low_bits_iCDF;           /* Pointer to iCDF table for low bits of pitch lag index            */
        public Pointer<byte> pitch_contour_iCDF;                /* Pointer to iCDF table for pitch contour index                    */

        /* For buffering payload in case of more frames per packet */
        public int nFramesDecoded = 0;
        public int nFramesPerPacket = 0;

        /* Specifically for entropy coding */
        public int ec_prevSignalType = 0;
        public short ec_prevLagIndex = 0;

        public readonly Pointer<int> VAD_flags = Pointer.Malloc<int>(SilkConstants.MAX_FRAMES_PER_PACKET);
        public int LBRR_flag = 0;
        public readonly Pointer<int> LBRR_flags = Pointer.Malloc<int>(SilkConstants.MAX_FRAMES_PER_PACKET);

        public readonly SilkResamplerState resampler_state = new SilkResamplerState();

        public NLSFCodebook psNLSF_CB = null;                         /* Pointer to NLSF codebook                                         */

        /* Quantization indices */
        public readonly SideInfoIndices indices = new SideInfoIndices();

        /* CNG state */
        public readonly CNGState sCNG = new CNGState();

        /* Stuff used for PLC */
        public int lossCnt = 0;
        public int prevSignalType = 0;

        public readonly PLCStruct sPLC = new PLCStruct();
        
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
        /// <param name="this">I/O  Decoder state pointer</param>
        /// <returns></returns>
        internal int silk_init_decoder()
        {
            /* Clear the entire encoder state, except anything copied */
            this.Reset();

            /* Used to deactivate LSF interpolation */
            this.first_frame_after_reset = 1;
            this.prev_gain_Q16 = 65536;

            /* Reset CNG state */
            silk_CNG_Reset();

            /* Reset PLC state */
            silk_PLC_Reset();

            return (0);
        }

        /// <summary>
        /// Resets CNG state
        /// </summary>
        /// <param name="this">I/O  Decoder state</param>
        private void silk_CNG_Reset()
        {
            int i, NLSF_step_Q15, NLSF_acc_Q15;

            NLSF_step_Q15 = Inlines.silk_DIV32_16(short.MaxValue, this.LPC_order + 1);
            NLSF_acc_Q15 = 0;
            for (i = 0; i < this.LPC_order; i++)
            {
                NLSF_acc_Q15 += NLSF_step_Q15;
                this.sCNG.CNG_smth_NLSF_Q15[i] = Inlines.CHOP16(NLSF_acc_Q15);
            }
            this.sCNG.CNG_smth_Gain_Q16 = 0;
            this.sCNG.rand_seed = 3176576;
        }

        /// <summary>
        /// Resets PLC state
        /// </summary>
        /// <param name="this">I/O Decoder state</param>
        private void silk_PLC_Reset()
        {
            this.sPLC.pitchL_Q8 = Inlines.silk_LSHIFT(this.frame_length, 8 - 1);
            this.sPLC.prevGain_Q16[0] = Inlines.SILK_CONST(1, 16);
            this.sPLC.prevGain_Q16[1] = Inlines.SILK_CONST(1, 16);
            this.sPLC.subfr_length = 20;
            this.sPLC.nb_subfr = 2;
        }

        /* Set decoder sampling rate */
        internal int silk_decoder_set_fs(
            int fs_kHz,                         /* I    Sampling frequency (kHz)                    */
            int fs_API_Hz                       /* I    API Sampling frequency (Hz)                 */
        )
        {
            int frame_length, ret = 0;

            Inlines.OpusAssert(fs_kHz == 8 || fs_kHz == 12 || fs_kHz == 16);
            Inlines.OpusAssert(this.nb_subfr == SilkConstants.MAX_NB_SUBFR || this.nb_subfr == SilkConstants.MAX_NB_SUBFR / 2);

            /* New (sub)frame length */
            this.subfr_length = Inlines.silk_SMULBB(SilkConstants.SUB_FRAME_LENGTH_MS, fs_kHz);
            frame_length = Inlines.silk_SMULBB(this.nb_subfr, this.subfr_length);

            /* Initialize resampler when switching internal or external sampling frequency */
            if (this.fs_kHz != fs_kHz || this.fs_API_hz != fs_API_Hz)
            {
                /* Initialize the resampler for dec_API.c preparing resampling from fs_kHz to API_fs_Hz */
                ret += Resampler.silk_resampler_init(this.resampler_state, Inlines.silk_SMULBB(fs_kHz, 1000), fs_API_Hz, 0);

                this.fs_API_hz = fs_API_Hz;
            }

            if (this.fs_kHz != fs_kHz || frame_length != this.frame_length)
            {
                if (fs_kHz == 8)
                {
                    if (this.nb_subfr == SilkConstants.MAX_NB_SUBFR)
                    {
                        this.pitch_contour_iCDF = Tables.silk_pitch_contour_NB_iCDF.GetPointer();
                    }
                    else {
                        this.pitch_contour_iCDF = Tables.silk_pitch_contour_10_ms_NB_iCDF.GetPointer();
                    }
                }
                else {
                    if (this.nb_subfr == SilkConstants.MAX_NB_SUBFR)
                    {
                        this.pitch_contour_iCDF = Tables.silk_pitch_contour_iCDF.GetPointer();
                    }
                    else {
                        this.pitch_contour_iCDF = Tables.silk_pitch_contour_10_ms_iCDF.GetPointer();
                    }
                }
                if (this.fs_kHz != fs_kHz)
                {
                    this.ltp_mem_length = Inlines.silk_SMULBB(SilkConstants.LTP_MEM_LENGTH_MS, fs_kHz);
                    if (fs_kHz == 8 || fs_kHz == 12)
                    {
                        this.LPC_order = SilkConstants.MIN_LPC_ORDER;
                        this.psNLSF_CB = Tables.silk_NLSF_CB_NB_MB;
                    }
                    else {
                        this.LPC_order = SilkConstants.MAX_LPC_ORDER;
                        this.psNLSF_CB = Tables.silk_NLSF_CB_WB;
                    }
                    if (fs_kHz == 16)
                    {
                        this.pitch_lag_low_bits_iCDF = Tables.silk_uniform8_iCDF.GetPointer();
                    }
                    else if (fs_kHz == 12)
                    {
                        this.pitch_lag_low_bits_iCDF = Tables.silk_uniform6_iCDF.GetPointer();
                    }
                    else if (fs_kHz == 8)
                    {
                        this.pitch_lag_low_bits_iCDF = Tables.silk_uniform4_iCDF.GetPointer();
                    }
                    else {
                        /* unsupported sampling rate */
                        Inlines.OpusAssert(false);
                    }
                    this.first_frame_after_reset = 1;
                    this.lagPrev = 100;
                    this.LastGainIndex = 10;
                    this.prevSignalType = SilkConstants.TYPE_NO_VOICE_ACTIVITY;
                    this.outBuf.MemSet(0, SilkConstants.MAX_FRAME_LENGTH + 2 * SilkConstants.MAX_SUB_FRAME_LENGTH);
                    this.sLPC_Q14_buf.MemSet(0, SilkConstants.MAX_LPC_ORDER);
                }

                this.fs_kHz = fs_kHz;
                this.frame_length = frame_length;
            }

            /* Check that settings are valid */
            Inlines.OpusAssert(this.frame_length > 0 && this.frame_length <= SilkConstants.MAX_FRAME_LENGTH);

            return ret;
        }

        /****************/
        /* Decode frame */
        /****************/
        internal int silk_decode_frame(
            EntropyCoder psRangeDec,                    /* I/O  Compressor data structure                   */
            Pointer<short> pOut,                         /* O    Pointer to output speech frame              */
            BoxedValue<int> pN,                            /* O    Pointer to size of output frame             */
            int lostFlag,                       /* I    0: no loss, 1 loss, 2 decode fec            */
            int condCoding                     /* I    The type of conditional coding to use       */
        )
        {
            // [porting note] this is a pointer to a heap struct, not a stack variable
            SilkDecoderControl thisCtrl = new SilkDecoderControl();
            int L, mv_len, ret = 0;

            L = this.frame_length;
            thisCtrl.LTP_scale_Q14 = 0;

            /* Safety checks */
            Inlines.OpusAssert(L > 0 && L <= SilkConstants.MAX_FRAME_LENGTH);

            if (lostFlag == DecoderAPIFlag.FLAG_DECODE_NORMAL ||
                (lostFlag == DecoderAPIFlag.FLAG_DECODE_LBRR && this.LBRR_flags[this.nFramesDecoded] == 1))
            {
                Pointer<short> pulses = Pointer.Malloc<short>((L + SilkConstants.SHELL_CODEC_FRAME_LENGTH - 1) & ~(SilkConstants.SHELL_CODEC_FRAME_LENGTH - 1));
                /*********************************************/
                /* Decode quantization indices of side info  */
                /*********************************************/
                DecodeIndices.silk_decode_indices(this, psRangeDec, this.nFramesDecoded, lostFlag, condCoding);

                /*********************************************/
                /* Decode quantization indices of excitation */
                /*********************************************/
                DecodePulses.silk_decode_pulses(psRangeDec, pulses, this.indices.signalType,
                        this.indices.quantOffsetType, this.frame_length);

                /********************************************/
                /* Decode parameters and pulse signal       */
                /********************************************/
                DecodeParameters.silk_decode_parameters(this, thisCtrl, condCoding);

                /********************************************************/
                /* Run inverse NSQ                                      */
                /********************************************************/
                DecodeCore.silk_decode_core(this, thisCtrl, pOut, pulses);

                /********************************************************/
                /* Update PLC state                                     */
                /********************************************************/
                PLC.silk_PLC(this, thisCtrl, pOut, 0);

                this.lossCnt = 0;
                this.prevSignalType = this.indices.signalType;
                Inlines.OpusAssert(this.prevSignalType >= 0 && this.prevSignalType <= 2);

                /* A frame has been decoded without errors */
                this.first_frame_after_reset = 0;
            }
            else
            {
                /* Handle packet loss by extrapolation */
                PLC.silk_PLC(this, thisCtrl, pOut, 1);
            }

            /*************************/
            /* Update output buffer. */
            /*************************/
            Inlines.OpusAssert(this.ltp_mem_length >= this.frame_length);
            mv_len = this.ltp_mem_length - this.frame_length;
            // FIXME CHECK THIS
            // silk_memmove(this.outBuf, &this.outBuf[this.frame_length], mv_len * sizeof(short));
            this.outBuf.Point(this.frame_length).MemMove(0 - this.frame_length, mv_len);
            pOut.MemCopyTo(this.outBuf.Point(mv_len), this.frame_length);

            /************************************************/
            /* Comfort noise generation / estimation        */
            /************************************************/
            CNG.silk_CNG(this, thisCtrl, pOut, L);

            /****************************************************************/
            /* Ensure smooth connection of extrapolated and good frames     */
            /****************************************************************/
            PLC.silk_PLC_glue_frames(this, pOut, L);

            /* Update some decoder state variables */
            this.lagPrev = thisCtrl.pitchL[this.nb_subfr - 1];

            /* Set output frame length */
            pN.Val = L;

            return ret;
        }
    }
}
