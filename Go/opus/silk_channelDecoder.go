package opus

type SilkChannelDecoder struct {
	prev_gain_Q16           int
	exc_Q14                 []int
	sLPC_Q14_buf            []int
	outBuf                  []int16
	lagPrev                 int
	LastGainIndex           int8
	fs_kHz                  int
	fs_API_hz               int
	nb_subfr                int
	frame_length            int
	subfr_length            int
	ltp_mem_length          int
	LPC_order               int
	prevNLSF_Q15            []int16
	first_frame_after_reset int
	pitch_lag_low_bits_iCDF []int16
	pitch_contour_iCDF      []int16
	nFramesDecoded          int
	nFramesPerPacket        int
	ec_prevSignalType       int
	ec_prevLagIndex         int16
	VAD_flags               [MAX_FRAMES_PER_PACKET]int
	LBRR_flag               int
	LBRR_flags              [MAX_FRAMES_PER_PACKET]int
	resampler_state         *SilkResamplerState
	psNLSF_CB               *NLSFCodebook
	indices                 *SideInfoIndices
	sCNG                    *CNGState
	lossCnt                 int
	prevSignalType          int
	sPLC                    *PLCStruct
}

func NewSilkChannelDecoder() *SilkChannelDecoder {
	obj := &SilkChannelDecoder{}

	/* Buffer for output signal                     */
	obj.exc_Q14 = make([]int, SilkConstants.MAX_FRAME_LENGTH)
	obj.sLPC_Q14_buf = make([]int, SilkConstants.MAX_LPC_ORDER)
	obj.outBuf = make([]int16, SilkConstants.MAX_FRAME_LENGTH+2*SilkConstants.MAX_SUB_FRAME_LENGTH)

	obj.prevNLSF_Q15 = make([]int16, SilkConstants.MAX_LPC_ORDER)

	obj.resampler_state = NewSilkResamplerState()

	obj.indices = NewSideInfoIndices()
	obj.sCNG = NewCNGState()
	obj.sPLC = NewPLCStruct()
	return obj
}
func (d *SilkChannelDecoder) Reset() {
	d.prev_gain_Q16 = 0
	d.exc_Q14 = make([]int, MAX_FRAME_LENGTH)
	d.sLPC_Q14_buf = make([]int, MAX_LPC_ORDER)
	d.outBuf = make([]int16, (MAX_FRAME_LENGTH + 2*MAX_SUB_FRAME_LENGTH))
	d.lagPrev = 0
	d.LastGainIndex = 0
	d.fs_kHz = 0
	d.fs_API_hz = 0
	d.nb_subfr = 0
	d.frame_length = 0
	d.subfr_length = 0
	d.ltp_mem_length = 0
	d.LPC_order = 0
	d.prevNLSF_Q15 = make([]int16, MAX_LPC_ORDER)
	d.first_frame_after_reset = 0
	d.pitch_lag_low_bits_iCDF = nil
	d.pitch_contour_iCDF = nil
	d.nFramesDecoded = 0
	d.nFramesPerPacket = 0
	d.ec_prevSignalType = 0
	d.ec_prevLagIndex = 0
	d.VAD_flags = [MAX_FRAMES_PER_PACKET]int{}
	d.LBRR_flag = 0
	d.LBRR_flags = [MAX_FRAMES_PER_PACKET]int{}
	d.resampler_state.Reset()
	d.psNLSF_CB = nil
	d.indices.Reset()
	d.sCNG.Reset()
	d.lossCnt = 0
	d.prevSignalType = 0
	d.sPLC.Reset()
}

func (d *SilkChannelDecoder) silk_init_decoder() int {
	d.Reset()
	d.first_frame_after_reset = 1
	d.prev_gain_Q16 = 65536
	d.silk_CNG_Reset()
	d.silk_PLC_Reset()
	return 0
}

func (d *SilkChannelDecoder) silk_CNG_Reset() {
	NLSF_step_Q15 := silk_DIV32_16(32767, d.LPC_order+1)
	NLSF_acc_Q15 := 0
	for i := 0; i < d.LPC_order; i++ {
		NLSF_acc_Q15 += NLSF_step_Q15
		d.sCNG.CNG_smth_NLSF_Q15[i] = int16(NLSF_acc_Q15)
	}
	d.sCNG.CNG_smth_Gain_Q16 = 0
	d.sCNG.rand_seed = 3176576
}

func (d *SilkChannelDecoder) silk_PLC_Reset() {
	d.sPLC.pitchL_Q8 = silk_LSHIFT(d.frame_length, 8-1)
	d.sPLC.prevGain_Q16[0] = 1 << 16
	d.sPLC.prevGain_Q16[1] = 1 << 16
	d.sPLC.subfr_length = 20
	d.sPLC.nb_subfr = 2
}

func (d *SilkChannelDecoder) silk_decoder_set_fs(fs_kHz, fs_API_Hz int) int {
	ret := 0
	OpusAssert(fs_kHz == 8 || fs_kHz == 12 || fs_kHz == 16)
	OpusAssert(d.nb_subfr == SilkConstants.MAX_NB_SUBFR || d.nb_subfr == SilkConstants.MAX_NB_SUBFR/2)

	subfr_length := silk_SMULBB(SilkConstants.SUB_FRAME_LENGTH_MS, fs_kHz)
	frame_length := silk_SMULBB(d.nb_subfr, subfr_length)

	if d.fs_kHz != fs_kHz || d.fs_API_hz != fs_API_Hz {
		ret += silk_resampler_init(d.resampler_state, silk_SMULBB(fs_kHz, 1000), fs_API_Hz, 0)
		d.fs_API_hz = fs_API_Hz
	}

	if d.fs_kHz != fs_kHz || frame_length != d.frame_length {
		if fs_kHz == 8 {
			if d.nb_subfr == MAX_NB_SUBFR {
				d.pitch_contour_iCDF = silk_pitch_contour_NB_iCDF
			} else {
				d.pitch_contour_iCDF = silk_pitch_contour_10_ms_NB_iCDF
			}
		} else if d.nb_subfr == MAX_NB_SUBFR {
			d.pitch_contour_iCDF = silk_pitch_contour_iCDF
		} else {
			d.pitch_contour_iCDF = silk_pitch_contour_10_ms_iCDF
		}
		if d.fs_kHz != fs_kHz {
			d.ltp_mem_length = silk_SMULBB(LTP_MEM_LENGTH_MS, fs_kHz)
			if fs_kHz == 8 || fs_kHz == 12 {
				d.LPC_order = MIN_LPC_ORDER
				d.psNLSF_CB = SilkTables.Silk_NLSF_CB_NB_MB
			} else {
				d.LPC_order = MAX_LPC_ORDER
				d.psNLSF_CB = SilkTables.Silk_NLSF_CB_WB
			}
			switch fs_kHz {
			case 16:
				d.pitch_lag_low_bits_iCDF = SilkTables.Silk_uniform8_iCDF
			case 12:
				d.pitch_lag_low_bits_iCDF = SilkTables.Silk_uniform6_iCDF
			case 8:
				d.pitch_lag_low_bits_iCDF = SilkTables.Silk_uniform4_iCDF
			default:
				OpusAssert(false)
			}
			d.first_frame_after_reset = 1
			d.lagPrev = 100
			d.LastGainIndex = 10
			d.prevSignalType = TYPE_NO_VOICE_ACTIVITY
			d.outBuf = make([]int16, (MAX_FRAME_LENGTH + 2*MAX_SUB_FRAME_LENGTH))
			d.sLPC_Q14_buf = make([]int, MAX_LPC_ORDER)
		}
		d.fs_kHz = fs_kHz
		d.frame_length = frame_length
		d.subfr_length = subfr_length
	}

	OpusAssert(d.frame_length > 0 && d.frame_length <= MAX_FRAME_LENGTH)
	return ret
}

func (d *SilkChannelDecoder) silk_decode_frame(psRangeDec *EntropyCoder, pOut []int16, pOut_ptr int, pN *BoxedValueInt, lostFlag, condCoding int) int {
	thisCtrl := NewSilkDecoderControl()
	var L, mv_len, ret int = 0, 0, 0

	L = d.frame_length
	thisCtrl.LTP_scale_Q14 = 0

	/* Safety checks */
	OpusAssert(L > 0 && L <= SilkConstants.MAX_FRAME_LENGTH)

	if lostFlag == FLAG_DECODE_NORMAL ||
		(lostFlag == FLAG_DECODE_LBRR && d.LBRR_flags[d.nFramesDecoded] == 1) {

		pulses := make([]int16, (L+SilkConstants.SHELL_CODEC_FRAME_LENGTH-1)&^(SilkConstants.SHELL_CODEC_FRAME_LENGTH-1))
		/**
		 * ******************************************
		 */
		/* Decode quantization indices of side info  */
		/**
		 * ******************************************
		 */
		silk_decode_indices(d, psRangeDec, d.nFramesDecoded, lostFlag, condCoding)

		/**
		 * ******************************************
		 */
		/* Decode quantization indices of excitation */
		/**
		 * ******************************************
		 */
		silk_decode_pulses(psRangeDec, pulses, int(d.indices.signalType),
			int(d.indices.quantOffsetType), d.frame_length)

		/**
		 * *****************************************
		 */
		/* Decode parameters and pulse signal       */
		/**
		 * *****************************************
		 */
		silk_decode_parameters(d, thisCtrl, condCoding)

		/**
		 * *****************************************************
		 */
		/* Run inverse NSQ                                      */
		/**
		 * *****************************************************
		 */
		silk_decode_core(d, thisCtrl, pOut, pOut_ptr, pulses)

		/**
		 * *****************************************************
		 */
		/* Update PLC state                                     */
		/**
		 * *****************************************************
		 */
		silk_PLC(d, thisCtrl, pOut, pOut_ptr, 0)

		d.lossCnt = 0
		d.prevSignalType = int(d.indices.signalType)
		OpusAssert(d.prevSignalType >= 0 && d.prevSignalType <= 2)

		/* A frame has been decoded without errors */
		d.first_frame_after_reset = 0
	} else {
		/* Handle packet loss by extrapolation */
		silk_PLC(d, thisCtrl, pOut, pOut_ptr, 1)
	}

	/**
	 * **********************
	 */
	/* Update output buffer. */
	/**
	 * **********************
	 */
	OpusAssert(d.ltp_mem_length >= d.frame_length)
	mv_len = d.ltp_mem_length - d.frame_length
	MemMove(d.outBuf, d.frame_length, 0, mv_len)
	//System.arraycopy(pOut, pOut_ptr, d.outBuf, mv_len, d.frame_length)
	copy(d.outBuf[mv_len:], pOut[pOut_ptr:pOut_ptr+d.frame_length])

	/**
	 * *********************************************
	 */
	/* Comfort noise generation / estimation        */
	/**
	 * *********************************************
	 */
	silk_CNG(d, thisCtrl, pOut, pOut_ptr, L)

	/**
	 * *************************************************************
	 */
	/* Ensure smooth connection of extrapolated and good frames     */
	/**
	 * *************************************************************
	 */
	silk_PLC_glue_frames(d, pOut, pOut_ptr, L)

	/* Update some decoder state variables */
	d.lagPrev = thisCtrl.pitchL[d.nb_subfr-1]

	/* Set output frame length */
	pN.Val = L

	return ret
}
