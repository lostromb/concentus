package opus

type NSQ_del_dec_struct struct {
	sLPC_Q14  []int
	RandState [DECISION_DELAY]int
	Q_Q10     [DECISION_DELAY]int
	Xq_Q14    [DECISION_DELAY]int
	Pred_Q15  [DECISION_DELAY]int
	Shape_Q14 [DECISION_DELAY]int
	sAR2_Q14  []int
	LF_AR_Q14 int
	Seed      int
	SeedInit  int
	RD_Q10    int
}

func (s *NSQ_del_dec_struct) PartialCopyFrom(other *NSQ_del_dec_struct, q14Offset int) {
	copy(s.sLPC_Q14[q14Offset:], other.sLPC_Q14[q14Offset:])
	copy(s.RandState[:], other.RandState[:])
	copy(s.Q_Q10[:], other.Q_Q10[:])
	copy(s.Xq_Q14[:], other.Xq_Q14[:])
	copy(s.Pred_Q15[:], other.Pred_Q15[:])
	copy(s.Shape_Q14[:], other.Shape_Q14[:])
	copy(s.sAR2_Q14, other.sAR2_Q14)
	s.LF_AR_Q14 = other.LF_AR_Q14
	s.Seed = other.Seed
	s.SeedInit = other.SeedInit
	s.RD_Q10 = other.RD_Q10
}

func (s *NSQ_del_dec_struct) Assign(other *NSQ_del_dec_struct) {
	s.PartialCopyFrom(other, 0)
}

type NSQ_sample_struct struct {
	Q_Q10        int
	RD_Q10       int
	xq_Q14       int
	LF_AR_Q14    int
	sLTP_shp_Q14 int
	LPC_exc_Q14  int
}

func (s *NSQ_sample_struct) Assign(other *NSQ_sample_struct) {
	s.Q_Q10 = other.Q_Q10
	s.RD_Q10 = other.RD_Q10
	s.xq_Q14 = other.xq_Q14
	s.LF_AR_Q14 = other.LF_AR_Q14
	s.sLTP_shp_Q14 = other.sLTP_shp_Q14
	s.LPC_exc_Q14 = other.LPC_exc_Q14
}

type SilkNSQState struct {
	xq               [2 * MAX_FRAME_LENGTH]int16
	sLTP_shp_Q14     [2 * MAX_FRAME_LENGTH]int
	sLPC_Q14         [MAX_SUB_FRAME_LENGTH + 32]int
	sAR2_Q14         [MAX_SHAPE_LPC_ORDER]int
	sLF_AR_shp_Q14   int
	lagPrev          int
	sLTP_buf_idx     int
	sLTP_shp_buf_idx int
	rand_seed        int
	prev_gain_Q16    int
	rewhite_flag     int
}

func NewSilkNSQState() *SilkNSQState {
	return &SilkNSQState{}
}
func (s *SilkNSQState) Reset() {
	for i := range s.xq {
		s.xq[i] = 0
	}
	for i := range s.sLTP_shp_Q14 {
		s.sLTP_shp_Q14[i] = 0
	}
	for i := range s.sLPC_Q14 {
		s.sLPC_Q14[i] = 0
	}
	for i := range s.sAR2_Q14 {
		s.sAR2_Q14[i] = 0
	}
	s.sLF_AR_shp_Q14 = 0
	s.lagPrev = 0
	s.sLTP_buf_idx = 0
	s.sLTP_shp_buf_idx = 0
	s.rand_seed = 0
	s.prev_gain_Q16 = 0
	s.rewhite_flag = 0
}

func (s *SilkNSQState) Assign(other *SilkNSQState) {
	s.sLF_AR_shp_Q14 = other.sLF_AR_shp_Q14
	s.lagPrev = other.lagPrev
	s.sLTP_buf_idx = other.sLTP_buf_idx
	s.sLTP_shp_buf_idx = other.sLTP_shp_buf_idx
	s.rand_seed = other.rand_seed
	s.prev_gain_Q16 = other.prev_gain_Q16
	s.rewhite_flag = other.rewhite_flag
	copy(s.xq[:], other.xq[:])
	copy(s.sLTP_shp_Q14[:], other.sLTP_shp_Q14[:])
	copy(s.sLPC_Q14[:], other.sLPC_Q14[:])
	copy(s.sAR2_Q14[:], other.sAR2_Q14[:])
}

func (s *SilkNSQState) silk_NSQ(
	psEncC *SilkChannelEncoder,
	psIndices *SideInfoIndices,
	x_Q3 []int,
	pulses []int8,
	PredCoef_Q12 [][]int16,
	LTPCoef_Q14 []int16,
	AR2_Q13 []int16,
	HarmShapeGain_Q14 []int,
	Tilt_Q14 []int,
	LF_shp_Q14 []int,
	Gains_Q16 []int,
	pitchL []int,
	Lambda_Q10 int,
	LTP_scale_Q14 int,
) {
	var k, lag, start_idx, LSF_interpolation_flag int
	var A_Q12, B_Q14, AR_shp_Q13 int
	var pxq int
	var sLTP_Q15 []int
	var sLTP []int16
	var HarmShapeFIRPacked_Q14 int
	var offset_Q10 int
	var x_sc_Q10 []int
	pulses_ptr := 0
	x_Q3_ptr := 0

	s.rand_seed = int(psIndices.Seed)

	lag = s.lagPrev

	OpusAssert(s.prev_gain_Q16 != 0)

	offset_Q10 = int(silk_Quantization_Offsets_Q10[psIndices.signalType>>1][psIndices.quantOffsetType])

	if psIndices.NLSFInterpCoef_Q2 == 4 {
		LSF_interpolation_flag = 0
	} else {
		LSF_interpolation_flag = 1
	}

	sLTP_Q15 = make([]int, psEncC.ltp_mem_length+psEncC.frame_length)
	sLTP = make([]int16, psEncC.ltp_mem_length+psEncC.frame_length)
	x_sc_Q10 = make([]int, psEncC.subfr_length)
	s.sLTP_shp_buf_idx = psEncC.ltp_mem_length
	s.sLTP_buf_idx = psEncC.ltp_mem_length
	pxq = psEncC.ltp_mem_length
	for k = 0; k < psEncC.nb_subfr; k++ {
		A_Q12 = ((k >> 1) | (1 - LSF_interpolation_flag))
		B_Q14 = k * LTP_ORDER
		AR_shp_Q13 = k * MAX_SHAPE_LPC_ORDER

		OpusAssert(HarmShapeGain_Q14[k] >= 0)
		HarmShapeFIRPacked_Q14 = silk_RSHIFT(HarmShapeGain_Q14[k], 2)
		HarmShapeFIRPacked_Q14 |= silk_LSHIFT(int(silk_RSHIFT(HarmShapeGain_Q14[k], 1)), 16)

		s.rewhite_flag = 0
		if psIndices.signalType == TYPE_VOICED {
			lag = pitchL[k]

			if (k & (3 - silk_LSHIFT(LSF_interpolation_flag, 1))) == 0 {
				start_idx = psEncC.ltp_mem_length - lag - psEncC.predictLPCOrder - LTP_ORDER/2
				OpusAssert(start_idx > 0)

				silk_LPC_analysis_filter(sLTP, start_idx, s.xq[:], start_idx+k*psEncC.subfr_length, PredCoef_Q12[A_Q12], 0, psEncC.ltp_mem_length-start_idx, psEncC.predictLPCOrder)

				s.rewhite_flag = 1
				s.sLTP_buf_idx = psEncC.ltp_mem_length
			}
		}

		s.silk_nsq_scale_states(psEncC, x_Q3, x_Q3_ptr, x_sc_Q10, sLTP, sLTP_Q15, k, LTP_scale_Q14, Gains_Q16, pitchL, int(psIndices.signalType))

		s.silk_noise_shape_quantizer(
			int(psIndices.signalType),
			x_sc_Q10,
			pulses,
			pulses_ptr,
			s.xq[:],
			pxq,
			sLTP_Q15,
			PredCoef_Q12[A_Q12],
			LTPCoef_Q14,
			B_Q14,
			AR2_Q13,
			AR_shp_Q13,
			lag,
			HarmShapeFIRPacked_Q14,
			Tilt_Q14[k],
			LF_shp_Q14[k],
			Gains_Q16[k],
			Lambda_Q10,
			offset_Q10,
			psEncC.subfr_length,
			psEncC.shapingLPCOrder,
			psEncC.predictLPCOrder,
		)

		x_Q3_ptr += psEncC.subfr_length
		pulses_ptr += psEncC.subfr_length
		pxq += psEncC.subfr_length
	}

	s.lagPrev = pitchL[psEncC.nb_subfr-1]

	copy(s.xq[:psEncC.ltp_mem_length], s.xq[psEncC.frame_length:psEncC.frame_length+psEncC.ltp_mem_length])
	copy(s.sLTP_shp_Q14[:psEncC.ltp_mem_length], s.sLTP_shp_Q14[psEncC.frame_length:psEncC.frame_length+psEncC.ltp_mem_length])
}

func (s *SilkNSQState) silk_noise_shape_quantizer(
	signalType int,
	x_sc_Q10 []int,
	pulses []int8,
	pulses_ptr int,
	xq []int16,
	xq_ptr int,
	sLTP_Q15 []int,
	a_Q12 []int16,
	b_Q14 []int16,
	b_Q14_ptr int,
	AR_shp_Q13 []int16,
	AR_shp_Q13_ptr int,
	lag int,
	HarmShapeFIRPacked_Q14 int,
	Tilt_Q14 int,
	LF_shp_Q14 int,
	Gain_Q16 int,
	Lambda_Q10 int,
	offset_Q10 int,
	length int,
	shapingLPCOrder int,
	predictLPCOrder int,
) {
	var i, j int
	var LTP_pred_Q13, LPC_pred_Q10, n_AR_Q12, n_LTP_Q13 int
	var n_LF_Q12, r_Q10, rr_Q10, q1_Q0, q1_Q10, q2_Q10, rd1_Q20, rd2_Q20 int
	var exc_Q14, LPC_exc_Q14, xq_Q14, Gain_Q10 int
	var tmp1, tmp2, sLF_AR_shp_Q14 int
	var psLPC_Q14 int
	var shp_lag_ptr int
	var pred_lag_ptr int

	shp_lag_ptr = s.sLTP_shp_buf_idx - lag + SilkConstants.HARM_SHAPE_FIR_TAPS/2
	pred_lag_ptr = s.sLTP_buf_idx - lag + SilkConstants.LTP_ORDER/2
	Gain_Q10 = silk_RSHIFT(Gain_Q16, 6)

	/* Set up short term AR state */
	psLPC_Q14 = SilkConstants.NSQ_LPC_BUF_LENGTH - 1

	for i = 0; i < length; i++ {
		/* Generate dither */
		s.rand_seed = silk_RAND(s.rand_seed)

		/* Short-term prediction */
		OpusAssert(predictLPCOrder == 10 || predictLPCOrder == 16)
		/* Avoids introducing a bias because Inlines.silk_SMLAWB() always rounds to -inf */
		LPC_pred_Q10 = silk_RSHIFT(predictLPCOrder, 1)
		LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-0], int(a_Q12[0]))
		LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-1], int(a_Q12[1]))
		LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-2], int(a_Q12[2]))
		LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-3], int(a_Q12[3]))
		LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-4], int(a_Q12[4]))
		LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-5], int(a_Q12[5]))
		LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-6], int(a_Q12[6]))
		LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-7], int(a_Q12[7]))
		LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-8], int(a_Q12[8]))
		LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-9], int(a_Q12[9]))
		if predictLPCOrder == 16 {
			LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-10], int(a_Q12[10]))
			LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-11], int(a_Q12[11]))
			LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-12], int(a_Q12[12]))
			LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-13], int(a_Q12[13]))
			LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-14], int(a_Q12[14]))
			LPC_pred_Q10 = silk_SMLAWB(LPC_pred_Q10, s.sLPC_Q14[psLPC_Q14-15], int(a_Q12[15]))
		}

		/* Long-term prediction */
		if signalType == SilkConstants.TYPE_VOICED {
			/* Unrolled loop */
			/* Avoids introducing a bias because Inlines.silk_SMLAWB() always rounds to -inf */
			LTP_pred_Q13 = 2
			LTP_pred_Q13 = silk_SMLAWB(LTP_pred_Q13, sLTP_Q15[pred_lag_ptr], int(b_Q14[b_Q14_ptr]))
			LTP_pred_Q13 = silk_SMLAWB(LTP_pred_Q13, sLTP_Q15[pred_lag_ptr-1], int(b_Q14[b_Q14_ptr+1]))
			LTP_pred_Q13 = silk_SMLAWB(LTP_pred_Q13, sLTP_Q15[pred_lag_ptr-2], int(b_Q14[b_Q14_ptr+2]))
			LTP_pred_Q13 = silk_SMLAWB(LTP_pred_Q13, sLTP_Q15[pred_lag_ptr-3], int(b_Q14[b_Q14_ptr+3]))
			LTP_pred_Q13 = silk_SMLAWB(LTP_pred_Q13, sLTP_Q15[pred_lag_ptr-4], int(b_Q14[b_Q14_ptr+4]))
			pred_lag_ptr += 1
		} else {
			LTP_pred_Q13 = 0
		}

		/* Noise shape feedback */
		OpusAssert((shapingLPCOrder & 1) == 0)
		/* check that order is even */
		tmp2 = s.sLPC_Q14[psLPC_Q14]
		tmp1 = s.sAR2_Q14[0]
		s.sAR2_Q14[0] = tmp2
		n_AR_Q12 = silk_RSHIFT(shapingLPCOrder, 1)
		n_AR_Q12 = silk_SMLAWB(n_AR_Q12, tmp2, int(AR_shp_Q13[AR_shp_Q13_ptr]))
		for j = 2; j < shapingLPCOrder; j += 2 {
			tmp2 = s.sAR2_Q14[j-1]
			s.sAR2_Q14[j-1] = tmp1
			n_AR_Q12 = silk_SMLAWB(n_AR_Q12, tmp1, int(AR_shp_Q13[AR_shp_Q13_ptr+j-1]))
			tmp1 = s.sAR2_Q14[j+0]
			s.sAR2_Q14[j+0] = tmp2
			n_AR_Q12 = silk_SMLAWB(n_AR_Q12, tmp2, int(AR_shp_Q13[AR_shp_Q13_ptr+j]))
		}
		s.sAR2_Q14[shapingLPCOrder-1] = tmp1
		n_AR_Q12 = silk_SMLAWB(n_AR_Q12, tmp1, int(AR_shp_Q13[AR_shp_Q13_ptr+shapingLPCOrder-1]))

		n_AR_Q12 = silk_LSHIFT32(n_AR_Q12, 1)
		/* Q11 . Q12 */
		n_AR_Q12 = silk_SMLAWB(n_AR_Q12, s.sLF_AR_shp_Q14, Tilt_Q14)

		n_LF_Q12 = silk_SMULWB(s.sLTP_shp_Q14[s.sLTP_shp_buf_idx-1], LF_shp_Q14)
		n_LF_Q12 = silk_SMLAWT(n_LF_Q12, s.sLF_AR_shp_Q14, LF_shp_Q14)

		OpusAssert(lag > 0 || signalType != SilkConstants.TYPE_VOICED)

		/* Combine prediction and noise shaping signals */
		tmp1 = silk_SUB32(silk_LSHIFT32(LPC_pred_Q10, 2), n_AR_Q12)
		/* Q12 */
		tmp1 = silk_SUB32(tmp1, n_LF_Q12)
		/* Q12 */
		if lag > 0 {
			/* Symmetric, packed FIR coefficients */
			n_LTP_Q13 = silk_SMULWB(silk_ADD32(s.sLTP_shp_Q14[shp_lag_ptr], s.sLTP_shp_Q14[shp_lag_ptr-2]), HarmShapeFIRPacked_Q14)
			n_LTP_Q13 = silk_SMLAWT(n_LTP_Q13, s.sLTP_shp_Q14[shp_lag_ptr-1], HarmShapeFIRPacked_Q14)
			n_LTP_Q13 = silk_LSHIFT(n_LTP_Q13, 1)
			shp_lag_ptr += 1

			tmp2 = silk_SUB32(LTP_pred_Q13, n_LTP_Q13)
			/* Q13 */
			tmp1 = silk_ADD_LSHIFT32(tmp2, tmp1, 1)
			/* Q13 */
			tmp1 = silk_RSHIFT_ROUND(tmp1, 3)
			/* Q10 */
		} else {
			tmp1 = silk_RSHIFT_ROUND(tmp1, 2)
			/* Q10 */
		}

		r_Q10 = silk_SUB32(x_sc_Q10[i], tmp1)
		/* residual error Q10 */

		/* Flip sign depending on dither */
		if s.rand_seed < 0 {
			r_Q10 = -r_Q10
		}
		r_Q10 = silk_LIMIT_32(r_Q10, -(31 << 10), 30<<10)

		/* Find two quantization level candidates and measure their rate-distortion */
		q1_Q10 = silk_SUB32(r_Q10, offset_Q10)
		q1_Q0 = silk_RSHIFT(q1_Q10, 10)
		if q1_Q0 > 0 {
			q1_Q10 = silk_SUB32(silk_LSHIFT(q1_Q0, 10), SilkConstants.QUANT_LEVEL_ADJUST_Q10)
			q1_Q10 = silk_ADD32(q1_Q10, offset_Q10)
			q2_Q10 = silk_ADD32(q1_Q10, 1024)
			rd1_Q20 = silk_SMULBB(q1_Q10, Lambda_Q10)
			rd2_Q20 = silk_SMULBB(q2_Q10, Lambda_Q10)
		} else if q1_Q0 == 0 {
			q1_Q10 = offset_Q10
			q2_Q10 = silk_ADD32(q1_Q10, 1024-SilkConstants.QUANT_LEVEL_ADJUST_Q10)
			rd1_Q20 = silk_SMULBB(q1_Q10, Lambda_Q10)
			rd2_Q20 = silk_SMULBB(q2_Q10, Lambda_Q10)
		} else if q1_Q0 == -1 {
			q2_Q10 = offset_Q10
			q1_Q10 = silk_SUB32(q2_Q10, 1024-SilkConstants.QUANT_LEVEL_ADJUST_Q10)
			rd1_Q20 = silk_SMULBB(-q1_Q10, Lambda_Q10)
			rd2_Q20 = silk_SMULBB(q2_Q10, Lambda_Q10)
		} else {
			/* Q1_Q0 < -1 */
			q1_Q10 = silk_ADD32(silk_LSHIFT(q1_Q0, 10), SilkConstants.QUANT_LEVEL_ADJUST_Q10)
			q1_Q10 = silk_ADD32(q1_Q10, offset_Q10)
			q2_Q10 = silk_ADD32(q1_Q10, 1024)
			rd1_Q20 = silk_SMULBB(-q1_Q10, Lambda_Q10)
			rd2_Q20 = silk_SMULBB(-q2_Q10, Lambda_Q10)
		}
		rr_Q10 = silk_SUB32(r_Q10, q1_Q10)
		rd1_Q20 = silk_SMLABB(rd1_Q20, rr_Q10, rr_Q10)
		rr_Q10 = silk_SUB32(r_Q10, q2_Q10)
		rd2_Q20 = silk_SMLABB(rd2_Q20, rr_Q10, rr_Q10)

		if rd2_Q20 < rd1_Q20 {
			q1_Q10 = q2_Q10
		}

		pulses[pulses_ptr+i] = int8(silk_RSHIFT_ROUND(q1_Q10, 10))

		/* Excitation */
		exc_Q14 = silk_LSHIFT(q1_Q10, 4)
		if s.rand_seed < 0 {
			exc_Q14 = -exc_Q14
		}

		/* Add predictions */
		LPC_exc_Q14 = silk_ADD_LSHIFT32(exc_Q14, LTP_pred_Q13, 1)
		xq_Q14 = silk_ADD_LSHIFT32(LPC_exc_Q14, LPC_pred_Q10, 4)

		/* Scale XQ back to normal level before saving */
		xq[xq_ptr+i] = int16(silk_SAT16(silk_RSHIFT_ROUND(silk_SMULWW(xq_Q14, Gain_Q10), 8)))

		/* Update states */
		psLPC_Q14 += 1
		s.sLPC_Q14[psLPC_Q14] = xq_Q14
		sLF_AR_shp_Q14 = silk_SUB_LSHIFT32(xq_Q14, n_AR_Q12, 2)
		s.sLF_AR_shp_Q14 = sLF_AR_shp_Q14

		s.sLTP_shp_Q14[s.sLTP_shp_buf_idx] = silk_SUB_LSHIFT32(sLF_AR_shp_Q14, n_LF_Q12, 2)
		sLTP_Q15[s.sLTP_buf_idx] = silk_LSHIFT(LPC_exc_Q14, 1)
		s.sLTP_shp_buf_idx++
		s.sLTP_buf_idx++

		/* Make dither dependent on quantized signal */
		s.rand_seed = int(silk_ADD32_ovflw(int32(s.rand_seed), int32(pulses[pulses_ptr+i])))

	}

	/* Update LPC synth buffer */
	//System.arraycopy(s.sLPC_Q14, length, s.sLPC_Q14, 0, SilkConstants.NSQ_LPC_BUF_LENGTH)
	copy(s.sLPC_Q14[0:], s.sLPC_Q14[length:length+32])

}

func (s *SilkNSQState) silk_nsq_scale_states(
	psEncC *SilkChannelEncoder,
	x_Q3 []int,
	x_Q3_ptr int,
	x_sc_Q10 []int,
	sLTP []int16,
	sLTP_Q15 []int,
	subfr int,
	LTP_scale_Q14 int,
	Gains_Q16 []int,
	pitchL []int,
	signal_type int,
) {
	var i, lag int
	var gain_adj_Q16, inv_gain_Q31, inv_gain_Q23 int

	lag = pitchL[subfr]
	inv_gain_Q31 = silk_INVERSE32_varQ(silk_max(Gains_Q16[subfr], 1), 47)
	OpusAssert(inv_gain_Q31 != 0)

	/* Calculate gain adjustment factor */
	if Gains_Q16[subfr] != s.prev_gain_Q16 {
		gain_adj_Q16 = silk_DIV32_varQ(s.prev_gain_Q16, Gains_Q16[subfr], 16)
	} else {
		gain_adj_Q16 = int(int32(1) << 16)
	}

	/* Scale input */
	inv_gain_Q23 = silk_RSHIFT_ROUND(inv_gain_Q31, 8)
	for i = 0; i < psEncC.subfr_length; i++ {
		x_sc_Q10[i] = silk_SMULWW(x_Q3[x_Q3_ptr+i], inv_gain_Q23)
	}

	/* Save inverse gain */
	s.prev_gain_Q16 = Gains_Q16[subfr]

	/* After rewhitening the LTP state is un-scaled, so scale with inv_gain_Q16 */
	if s.rewhite_flag != 0 {
		if subfr == 0 {
			/* Do LTP downscaling */
			inv_gain_Q31 = silk_LSHIFT(silk_SMULWB(inv_gain_Q31, LTP_scale_Q14), 2)
		}
		for i = s.sLTP_buf_idx - lag - SilkConstants.LTP_ORDER/2; i < s.sLTP_buf_idx; i++ {
			OpusAssert(i < SilkConstants.MAX_FRAME_LENGTH)
			sLTP_Q15[i] = silk_SMULWB(inv_gain_Q31, int(sLTP[i]))
		}
	}

	/* Adjust for changing gain */
	if gain_adj_Q16 != int(int32(1)<<16) {
		/* Scale long-term shaping state */
		for i = s.sLTP_shp_buf_idx - psEncC.ltp_mem_length; i < s.sLTP_shp_buf_idx; i++ {
			s.sLTP_shp_Q14[i] = silk_SMULWW(gain_adj_Q16, s.sLTP_shp_Q14[i])
		}

		/* Scale long-term prediction state */
		if signal_type == SilkConstants.TYPE_VOICED && s.rewhite_flag == 0 {
			for i = s.sLTP_buf_idx - lag - SilkConstants.LTP_ORDER/2; i < s.sLTP_buf_idx; i++ {
				sLTP_Q15[i] = silk_SMULWW(gain_adj_Q16, sLTP_Q15[i])
			}
		}

		s.sLF_AR_shp_Q14 = silk_SMULWW(gain_adj_Q16, s.sLF_AR_shp_Q14)

		/* Scale short-term prediction and shaping states */
		for i = 0; i < SilkConstants.NSQ_LPC_BUF_LENGTH; i++ {
			s.sLPC_Q14[i] = silk_SMULWW(gain_adj_Q16, s.sLPC_Q14[i])
		}
		for i = 0; i < SilkConstants.MAX_SHAPE_LPC_ORDER; i++ {
			s.sAR2_Q14[i] = silk_SMULWW(gain_adj_Q16, s.sAR2_Q14[i])
		}
	}
}

func (s *SilkNSQState) silk_NSQ_del_dec(
	psEncC *SilkChannelEncoder,
	psIndices *SideInfoIndices,
	x_Q3 []int,
	pulses []int8,
	PredCoef_Q12 [][]int16,
	LTPCoef_Q14 []int16,
	AR2_Q13 []int16,
	HarmShapeGain_Q14 []int,
	Tilt_Q14 []int,
	LF_shp_Q14 []int,
	Gains_Q16 []int,
	pitchL []int,
	Lambda_Q10 int,
	LTP_scale_Q14 int,
) {
	var i, k, lag, start_idx, LSF_interpolation_flag, Winner_ind, subfr int
	var last_smple_idx, smpl_buf_idx, decisionDelay int
	var A_Q12 int
	pulses_ptr := 0
	pxq := 0
	var sLTP_Q15 []int
	var sLTP []int16
	var HarmShapeFIRPacked_Q14 int
	var offset_Q10 int
	var RDmin_Q10, Gain_Q10 int
	var x_sc_Q10 []int
	var delayedGain_Q10 []int
	x_Q3_ptr := 0
	var psDelDec []*NSQ_del_dec_struct
	var psDD *NSQ_del_dec_struct

	lag = s.lagPrev
	OpusAssert(s.prev_gain_Q16 != 0)

	psDelDec = make([]*NSQ_del_dec_struct, psEncC.nStatesDelayedDecision)
	for c := range psDelDec {
		psDelDec[c] = &NSQ_del_dec_struct{
			sAR2_Q14: make([]int, psEncC.shapingLPCOrder),
			sLPC_Q14: make([]int, MAX_SUB_FRAME_LENGTH+NSQ_LPC_BUF_LENGTH),
		}
	}

	for k = 0; k < psEncC.nStatesDelayedDecision; k++ {
		psDD = psDelDec[k]
		psDD.Seed = (k + int(psIndices.Seed)) & 3
		psDD.SeedInit = psDD.Seed
		psDD.RD_Q10 = 0
		psDD.LF_AR_Q14 = s.sLF_AR_shp_Q14
		psDD.Shape_Q14[0] = s.sLTP_shp_Q14[psEncC.ltp_mem_length-1]
		copy(psDD.sLPC_Q14, s.sLPC_Q14[:])
		copy(psDD.sAR2_Q14, s.sAR2_Q14[:])
	}

	offset_Q10 = int(silk_Quantization_Offsets_Q10[psIndices.signalType>>1][psIndices.quantOffsetType])
	smpl_buf_idx = 0
	decisionDelay = silk_min_int(DECISION_DELAY, psEncC.subfr_length)

	if psIndices.signalType == TYPE_VOICED {
		for k = 0; k < psEncC.nb_subfr; k++ {
			decisionDelay = silk_min_int(decisionDelay, pitchL[k]-LTP_ORDER/2-1)
		}
	} else if lag > 0 {
		decisionDelay = silk_min_int(decisionDelay, lag-LTP_ORDER/2-1)
	}

	if psIndices.NLSFInterpCoef_Q2 == 4 {
		LSF_interpolation_flag = 0
	} else {
		LSF_interpolation_flag = 1
	}

	sLTP_Q15 = make([]int, psEncC.ltp_mem_length+psEncC.frame_length)
	sLTP = make([]int16, psEncC.ltp_mem_length+psEncC.frame_length)
	x_sc_Q10 = make([]int, psEncC.subfr_length)
	delayedGain_Q10 = make([]int, DECISION_DELAY)
	pxq = psEncC.ltp_mem_length
	s.sLTP_shp_buf_idx = psEncC.ltp_mem_length
	s.sLTP_buf_idx = psEncC.ltp_mem_length
	subfr = 0
	for k = 0; k < psEncC.nb_subfr; k++ {
		A_Q12 = ((k >> 1) | (1 - LSF_interpolation_flag))

		OpusAssert(HarmShapeGain_Q14[k] >= 0)
		HarmShapeFIRPacked_Q14 = silk_RSHIFT(HarmShapeGain_Q14[k], 2)
		HarmShapeFIRPacked_Q14 |= silk_LSHIFT(int(silk_RSHIFT(HarmShapeGain_Q14[k], 1)), 16)

		s.rewhite_flag = 0
		if psIndices.signalType == TYPE_VOICED {
			lag = pitchL[k]

			if (k & (3 - silk_LSHIFT(LSF_interpolation_flag, 1))) == 0 {
				if k == 2 {
					RDmin_Q10 = psDelDec[0].RD_Q10
					Winner_ind = 0
					for i = 1; i < psEncC.nStatesDelayedDecision; i++ {
						if psDelDec[i].RD_Q10 < RDmin_Q10 {
							RDmin_Q10 = psDelDec[i].RD_Q10
							Winner_ind = i
						}
					}
					for i = 0; i < psEncC.nStatesDelayedDecision; i++ {
						if i != Winner_ind {
							psDelDec[i].RD_Q10 += int(2147483647 >> 4)
							OpusAssert(psDelDec[i].RD_Q10 >= 0)
						}
					}

					psDD = psDelDec[Winner_ind]
					last_smple_idx = smpl_buf_idx + decisionDelay
					for i = 0; i < decisionDelay; i++ {
						last_smple_idx = (last_smple_idx - 1) & DECISION_DELAY_MASK
						pulses[pulses_ptr+i-decisionDelay] = int8(silk_RSHIFT_ROUND(psDD.Q_Q10[last_smple_idx], 10))
						s.xq[pxq+i-decisionDelay] = int16(silk_SAT16(silk_RSHIFT_ROUND(silk_SMULWW(psDD.Xq_Q14[last_smple_idx], Gains_Q16[1]), 14)))
						s.sLTP_shp_Q14[s.sLTP_shp_buf_idx-decisionDelay+i] = psDD.Shape_Q14[last_smple_idx]
					}

					subfr = 0
				}

				start_idx = psEncC.ltp_mem_length - lag - psEncC.predictLPCOrder - LTP_ORDER/2
				OpusAssert(start_idx > 0)

				silk_LPC_analysis_filter(sLTP, start_idx, s.xq[:], start_idx+k*psEncC.subfr_length, PredCoef_Q12[A_Q12], 0, psEncC.ltp_mem_length-start_idx, psEncC.predictLPCOrder)

				s.sLTP_buf_idx = psEncC.ltp_mem_length
				s.rewhite_flag = 1
			}
		}

		s.silk_nsq_del_dec_scale_states(psEncC, psDelDec, x_Q3, x_Q3_ptr, x_sc_Q10, sLTP, sLTP_Q15, k, psEncC.nStatesDelayedDecision, LTP_scale_Q14, Gains_Q16, pitchL, int(psIndices.signalType), decisionDelay)

		smpl_buf_idx_boxed := &BoxedValueInt{Val: smpl_buf_idx}
		s.silk_noise_shape_quantizer_del_dec(psDelDec, int(psIndices.signalType), x_sc_Q10, pulses, pulses_ptr, s.xq[:], pxq, sLTP_Q15, delayedGain_Q10, PredCoef_Q12[A_Q12], LTPCoef_Q14, k*LTP_ORDER, AR2_Q13, k*MAX_SHAPE_LPC_ORDER, lag, HarmShapeFIRPacked_Q14, Tilt_Q14[k], LF_shp_Q14[k], Gains_Q16[k], Lambda_Q10, offset_Q10, psEncC.subfr_length, subfr, psEncC.shapingLPCOrder, psEncC.predictLPCOrder, psEncC.warping_Q16, psEncC.nStatesDelayedDecision, smpl_buf_idx_boxed, decisionDelay)
		smpl_buf_idx = smpl_buf_idx_boxed.Val

		x_Q3_ptr += psEncC.subfr_length
		pulses_ptr += psEncC.subfr_length
		pxq += psEncC.subfr_length
		subfr++
	}

	RDmin_Q10 = psDelDec[0].RD_Q10
	Winner_ind = 0
	for k = 1; k < psEncC.nStatesDelayedDecision; k++ {
		if psDelDec[k].RD_Q10 < RDmin_Q10 {
			RDmin_Q10 = psDelDec[k].RD_Q10
			Winner_ind = k
		}
	}

	psDD = psDelDec[Winner_ind]
	psIndices.Seed = int8(psDD.SeedInit)
	last_smple_idx = smpl_buf_idx + decisionDelay
	Gain_Q10 = silk_RSHIFT32(Gains_Q16[psEncC.nb_subfr-1], 6)
	for i = 0; i < decisionDelay; i++ {
		last_smple_idx = (last_smple_idx - 1) & DECISION_DELAY_MASK
		pulses[pulses_ptr+i-decisionDelay] = int8(silk_RSHIFT_ROUND(psDD.Q_Q10[last_smple_idx], 10))
		s.xq[pxq+i-decisionDelay] = int16(silk_SAT16(silk_RSHIFT_ROUND(silk_SMULWW(psDD.Xq_Q14[last_smple_idx], Gain_Q10), 8)))
		s.sLTP_shp_Q14[s.sLTP_shp_buf_idx-decisionDelay+i] = psDD.Shape_Q14[last_smple_idx]
	}
	copy(s.sLPC_Q14[:], psDD.sLPC_Q14[psEncC.subfr_length:])
	copy(s.sAR2_Q14[:], psDD.sAR2_Q14)

	s.sLF_AR_shp_Q14 = psDD.LF_AR_Q14
	s.lagPrev = pitchL[psEncC.nb_subfr-1]

	copy(s.xq[:psEncC.ltp_mem_length], s.xq[psEncC.frame_length:psEncC.frame_length+psEncC.ltp_mem_length])
	copy(s.sLTP_shp_Q14[:psEncC.ltp_mem_length], s.sLTP_shp_Q14[psEncC.frame_length:psEncC.frame_length+psEncC.ltp_mem_length])
}

func (s *SilkNSQState) silk_noise_shape_quantizer_del_dec(
	psDelDec []*NSQ_del_dec_struct,
	signalType int,
	x_Q10 []int,
	pulses []int8,
	pulses_ptr int,
	xq []int16,
	xq_ptr int,
	sLTP_Q15 []int,
	delayedGain_Q10 []int,
	a_Q12 []int16,
	b_Q14 []int16,
	b_Q14_ptr int,
	AR_shp_Q13 []int16,
	AR_shp_Q13_ptr int,
	lag int,
	HarmShapeFIRPacked_Q14 int,
	Tilt_Q14 int,
	LF_shp_Q14 int,
	Gain_Q16 int,
	Lambda_Q10 int,
	offset_Q10 int,
	length int,
	subfr int,
	shapingLPCOrder int,
	predictLPCOrder int,
	warping_Q16 int,
	nStatesDelayedDecision int,
	smpl_buf_idx *BoxedValueInt,
	decisionDelay int,
) {
	var i, j, k, Winner_ind, RDmin_ind, RDmax_ind, last_smple_idx int
	var Winner_rand_state int
	var LTP_pred_Q14, LPC_pred_Q14, n_AR_Q14, n_LTP_Q14 int
	var n_LF_Q14, r_Q10, rr_Q10, rd1_Q10, rd2_Q10, RDmin_Q10, RDmax_Q10 int
	var q1_Q0, q1_Q10, q2_Q10, exc_Q14, LPC_exc_Q14, xq_Q14, Gain_Q10 int
	var tmp1, tmp2, sLF_AR_shp_Q14 int
	var pred_lag_ptr, shp_lag_ptr, psLPC_Q14 int
	var sampleStates []*NSQ_sample_struct
	var psDD *NSQ_del_dec_struct
	var SS_left, SS_right int

	OpusAssert(nStatesDelayedDecision > 0)
	sampleStates = make([]*NSQ_sample_struct, 2*nStatesDelayedDecision)
	for c := range sampleStates {
		sampleStates[c] = &NSQ_sample_struct{}
	}

	shp_lag_ptr = s.sLTP_shp_buf_idx - lag + HARM_SHAPE_FIR_TAPS/2
	pred_lag_ptr = s.sLTP_buf_idx - lag + LTP_ORDER/2
	Gain_Q10 = silk_RSHIFT(Gain_Q16, 6)

	for i = 0; i < length; i++ {
		if signalType == TYPE_VOICED {
			LTP_pred_Q14 = 2
			LTP_pred_Q14 = silk_SMLAWB(LTP_pred_Q14, sLTP_Q15[pred_lag_ptr], int(b_Q14[b_Q14_ptr]))
			LTP_pred_Q14 = silk_SMLAWB(LTP_pred_Q14, sLTP_Q15[pred_lag_ptr-1], int(b_Q14[b_Q14_ptr+1]))
			LTP_pred_Q14 = silk_SMLAWB(LTP_pred_Q14, sLTP_Q15[pred_lag_ptr-2], int(b_Q14[b_Q14_ptr+2]))
			LTP_pred_Q14 = silk_SMLAWB(LTP_pred_Q14, sLTP_Q15[pred_lag_ptr-3], int(b_Q14[b_Q14_ptr+3]))
			LTP_pred_Q14 = silk_SMLAWB(LTP_pred_Q14, sLTP_Q15[pred_lag_ptr-4], int(b_Q14[b_Q14_ptr+4]))
			LTP_pred_Q14 = silk_LSHIFT(LTP_pred_Q14, 1)
			pred_lag_ptr++
		} else {
			LTP_pred_Q14 = 0
		}

		if lag > 0 {
			n_LTP_Q14 = silk_SMULWB(silk_ADD32(s.sLTP_shp_Q14[shp_lag_ptr], s.sLTP_shp_Q14[shp_lag_ptr-2]), HarmShapeFIRPacked_Q14)
			n_LTP_Q14 = silk_SMLAWT(n_LTP_Q14, s.sLTP_shp_Q14[shp_lag_ptr-1], HarmShapeFIRPacked_Q14)
			n_LTP_Q14 = silk_SUB_LSHIFT32(LTP_pred_Q14, n_LTP_Q14, 2)
			shp_lag_ptr++
		} else {
			n_LTP_Q14 = 0
		}

		for k = 0; k < nStatesDelayedDecision; k++ {
			psDD = psDelDec[k]
			psLPC_Q14 = NSQ_LPC_BUF_LENGTH - 1 + i
			LPC_pred_Q14 = silk_RSHIFT(predictLPCOrder, 1)
			LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14], int(a_Q12[0]))
			LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-1], int(a_Q12[1]))
			LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-2], int(a_Q12[2]))
			LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-3], int(a_Q12[3]))
			LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-4], int(a_Q12[4]))
			LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-5], int(a_Q12[5]))
			LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-6], int(a_Q12[6]))
			LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-7], int(a_Q12[7]))
			LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-8], int(a_Q12[8]))
			LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-9], int(a_Q12[9]))
			if predictLPCOrder == 16 {
				LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-10], int(a_Q12[10]))
				LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-11], int(a_Q12[11]))
				LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-12], int(a_Q12[12]))
				LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-13], int(a_Q12[13]))
				LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-14], int(a_Q12[14]))
				LPC_pred_Q14 = silk_SMLAWB(LPC_pred_Q14, psDD.sLPC_Q14[psLPC_Q14-15], int(a_Q12[15]))
			}
			LPC_pred_Q14 = silk_LSHIFT(LPC_pred_Q14, 4)

			psDD.Seed = silk_RAND(psDD.Seed)
			tmp2 = silk_SMLAWB(psDD.sLPC_Q14[psLPC_Q14], psDD.sAR2_Q14[0], warping_Q16)
			tmp1 = silk_SMLAWB(psDD.sAR2_Q14[0], psDD.sAR2_Q14[1]-tmp2, warping_Q16)
			psDD.sAR2_Q14[0] = tmp2
			n_AR_Q14 = silk_RSHIFT(shapingLPCOrder, 1)
			n_AR_Q14 = silk_SMLAWB(n_AR_Q14, tmp2, int(AR_shp_Q13[AR_shp_Q13_ptr]))
			for j = 2; j < shapingLPCOrder; j += 2 {
				tmp2 = silk_SMLAWB(psDD.sAR2_Q14[j-1], psDD.sAR2_Q14[j]-tmp1, warping_Q16)
				psDD.sAR2_Q14[j-1] = tmp1
				n_AR_Q14 = silk_SMLAWB(n_AR_Q14, tmp1, int(AR_shp_Q13[AR_shp_Q13_ptr+j-1]))
				tmp1 = silk_SMLAWB(psDD.sAR2_Q14[j], psDD.sAR2_Q14[j+1]-tmp2, warping_Q16)
				psDD.sAR2_Q14[j] = tmp2
				n_AR_Q14 = silk_SMLAWB(n_AR_Q14, tmp2, int(AR_shp_Q13[AR_shp_Q13_ptr+j]))
			}
			psDD.sAR2_Q14[shapingLPCOrder-1] = tmp1
			n_AR_Q14 = silk_SMLAWB(n_AR_Q14, tmp1, int(AR_shp_Q13[AR_shp_Q13_ptr+shapingLPCOrder-1]))

			n_AR_Q14 = silk_LSHIFT(n_AR_Q14, 1)
			n_AR_Q14 = silk_SMLAWB(n_AR_Q14, psDD.LF_AR_Q14, Tilt_Q14)
			n_AR_Q14 = silk_LSHIFT(n_AR_Q14, 2)

			n_LF_Q14 = silk_SMULWB(psDD.Shape_Q14[smpl_buf_idx.Val], LF_shp_Q14)
			n_LF_Q14 = silk_SMLAWT(n_LF_Q14, psDD.LF_AR_Q14, LF_shp_Q14)
			n_LF_Q14 = silk_LSHIFT(n_LF_Q14, 2)

			tmp1 = silk_ADD32(n_AR_Q14, n_LF_Q14)
			tmp2 = silk_ADD32(n_LTP_Q14, LPC_pred_Q14)
			tmp1 = silk_SUB32(tmp2, tmp1)
			tmp1 = silk_RSHIFT_ROUND(tmp1, 4)

			r_Q10 = silk_SUB32(x_Q10[i], tmp1)
			if psDD.Seed < 0 {
				r_Q10 = -r_Q10
			}
			r_Q10 = silk_LIMIT_32(r_Q10, -(31 << 10), 30<<10)

			q1_Q10 = silk_SUB32(r_Q10, offset_Q10)
			q1_Q0 = silk_RSHIFT(q1_Q10, 10)
			if q1_Q0 > 0 {
				q1_Q10 = silk_SUB32(silk_LSHIFT(q1_Q0, 10), QUANT_LEVEL_ADJUST_Q10)
				q1_Q10 = silk_ADD32(q1_Q10, offset_Q10)
				q2_Q10 = silk_ADD32(q1_Q10, 1024)
				rd1_Q10 = silk_SMULBB(q1_Q10, Lambda_Q10)
				rd2_Q10 = silk_SMULBB(q2_Q10, Lambda_Q10)
			} else if q1_Q0 == 0 {
				q1_Q10 = offset_Q10
				q2_Q10 = silk_ADD32(q1_Q10, 1024-QUANT_LEVEL_ADJUST_Q10)
				rd1_Q10 = silk_SMULBB(q1_Q10, Lambda_Q10)
				rd2_Q10 = silk_SMULBB(q2_Q10, Lambda_Q10)
			} else if q1_Q0 == -1 {
				q2_Q10 = offset_Q10
				q1_Q10 = silk_SUB32(q2_Q10, 1024-QUANT_LEVEL_ADJUST_Q10)
				rd1_Q10 = silk_SMULBB(-q1_Q10, Lambda_Q10)
				rd2_Q10 = silk_SMULBB(q2_Q10, Lambda_Q10)
			} else {
				q1_Q10 = silk_ADD32(silk_LSHIFT(q1_Q0, 10), QUANT_LEVEL_ADJUST_Q10)
				q1_Q10 = silk_ADD32(q1_Q10, offset_Q10)
				q2_Q10 = silk_ADD32(q1_Q10, 1024)
				rd1_Q10 = silk_SMULBB(-q1_Q10, Lambda_Q10)
				rd2_Q10 = silk_SMULBB(-q2_Q10, Lambda_Q10)
			}
			rr_Q10 = silk_SUB32(r_Q10, q1_Q10)
			rd1_Q10 = silk_RSHIFT(silk_SMLABB(rd1_Q10, rr_Q10, rr_Q10), 10)
			rr_Q10 = silk_SUB32(r_Q10, q2_Q10)
			rd2_Q10 = silk_RSHIFT(silk_SMLABB(rd2_Q10, rr_Q10, rr_Q10), 10)

			SS_left = k * 2
			SS_right = SS_left + 1
			if rd1_Q10 < rd2_Q10 {
				sampleStates[SS_left].RD_Q10 = silk_ADD32(psDD.RD_Q10, rd1_Q10)
				sampleStates[SS_right].RD_Q10 = silk_ADD32(psDD.RD_Q10, rd2_Q10)
				sampleStates[SS_left].Q_Q10 = q1_Q10
				sampleStates[SS_right].Q_Q10 = q2_Q10
			} else {
				sampleStates[SS_left].RD_Q10 = silk_ADD32(psDD.RD_Q10, rd2_Q10)
				sampleStates[SS_right].RD_Q10 = silk_ADD32(psDD.RD_Q10, rd1_Q10)
				sampleStates[SS_left].Q_Q10 = q2_Q10
				sampleStates[SS_right].Q_Q10 = q1_Q10
			}

			exc_Q14 = silk_LSHIFT32(sampleStates[SS_left].Q_Q10, 4)
			if psDD.Seed < 0 {
				exc_Q14 = -exc_Q14
			}
			LPC_exc_Q14 = silk_ADD32(exc_Q14, LTP_pred_Q14)
			xq_Q14 = silk_ADD32(LPC_exc_Q14, LPC_pred_Q14)
			sLF_AR_shp_Q14 = silk_SUB32(xq_Q14, n_AR_Q14)
			sampleStates[SS_left].sLTP_shp_Q14 = silk_SUB32(sLF_AR_shp_Q14, n_LF_Q14)
			sampleStates[SS_left].LF_AR_Q14 = sLF_AR_shp_Q14
			sampleStates[SS_left].LPC_exc_Q14 = LPC_exc_Q14
			sampleStates[SS_left].xq_Q14 = xq_Q14

			exc_Q14 = silk_LSHIFT32(sampleStates[SS_right].Q_Q10, 4)
			if psDD.Seed < 0 {
				exc_Q14 = -exc_Q14
			}
			LPC_exc_Q14 = silk_ADD32(exc_Q14, LTP_pred_Q14)
			xq_Q14 = silk_ADD32(LPC_exc_Q14, LPC_pred_Q14)
			sLF_AR_shp_Q14 = silk_SUB32(xq_Q14, n_AR_Q14)
			sampleStates[SS_right].sLTP_shp_Q14 = silk_SUB32(sLF_AR_shp_Q14, n_LF_Q14)
			sampleStates[SS_right].LF_AR_Q14 = sLF_AR_shp_Q14
			sampleStates[SS_right].LPC_exc_Q14 = LPC_exc_Q14
			sampleStates[SS_right].xq_Q14 = xq_Q14
		}

		smpl_buf_idx.Val = (smpl_buf_idx.Val - 1) & DECISION_DELAY_MASK
		last_smple_idx = (smpl_buf_idx.Val + decisionDelay) & DECISION_DELAY_MASK

		RDmin_Q10 = sampleStates[0].RD_Q10
		Winner_ind = 0
		for k = 1; k < nStatesDelayedDecision; k++ {
			if sampleStates[k*2].RD_Q10 < RDmin_Q10 {
				RDmin_Q10 = sampleStates[k*2].RD_Q10
				Winner_ind = k
			}
		}

		Winner_rand_state = psDelDec[Winner_ind].RandState[last_smple_idx]
		for k = 0; k < nStatesDelayedDecision; k++ {
			if psDelDec[k].RandState[last_smple_idx] != Winner_rand_state {
				k2 := k * 2
				sampleStates[k2].RD_Q10 = silk_ADD32(sampleStates[k2].RD_Q10, int(2147483647>>4))
				sampleStates[k2+1].RD_Q10 = silk_ADD32(sampleStates[k2+1].RD_Q10, int(2147483647>>4))
				OpusAssert(sampleStates[k2].RD_Q10 >= 0)
			}
		}

		RDmax_Q10 = sampleStates[0].RD_Q10
		RDmin_Q10 = sampleStates[1].RD_Q10
		RDmax_ind = 0
		RDmin_ind = 0
		for k = 1; k < nStatesDelayedDecision; k++ {
			k2 := k * 2
			if sampleStates[k2].RD_Q10 > RDmax_Q10 {
				RDmax_Q10 = sampleStates[k2].RD_Q10
				RDmax_ind = k
			}
			if sampleStates[k2+1].RD_Q10 < RDmin_Q10 {
				RDmin_Q10 = sampleStates[k2+1].RD_Q10
				RDmin_ind = k
			}
		}

		if RDmin_Q10 < RDmax_Q10 {
			psDelDec[RDmax_ind].PartialCopyFrom(psDelDec[RDmin_ind], i)
			sampleStates[RDmax_ind*2].Assign(sampleStates[RDmin_ind*2+1])
		}

		psDD = psDelDec[Winner_ind]
		if subfr > 0 || i >= decisionDelay {
			pulses[pulses_ptr+i-decisionDelay] = int8(silk_RSHIFT_ROUND(psDD.Q_Q10[last_smple_idx], 10))
			xq[xq_ptr+i-decisionDelay] = int16(silk_SAT16(silk_RSHIFT_ROUND(silk_SMULWW(psDD.Xq_Q14[last_smple_idx], delayedGain_Q10[last_smple_idx]), 8)))
			s.sLTP_shp_Q14[s.sLTP_shp_buf_idx-decisionDelay] = psDD.Shape_Q14[last_smple_idx]
			sLTP_Q15[s.sLTP_buf_idx-decisionDelay] = psDD.Pred_Q15[last_smple_idx]
		}
		s.sLTP_shp_buf_idx++
		s.sLTP_buf_idx++

		for k = 0; k < nStatesDelayedDecision; k++ {
			psDD = psDelDec[k]
			SS_left = k * 2
			psDD.LF_AR_Q14 = sampleStates[SS_left].LF_AR_Q14
			psDD.sLPC_Q14[NSQ_LPC_BUF_LENGTH+i] = sampleStates[SS_left].xq_Q14
			psDD.Xq_Q14[smpl_buf_idx.Val] = sampleStates[SS_left].xq_Q14
			psDD.Q_Q10[smpl_buf_idx.Val] = sampleStates[SS_left].Q_Q10
			psDD.Pred_Q15[smpl_buf_idx.Val] = silk_LSHIFT32(sampleStates[SS_left].LPC_exc_Q14, 1)
			psDD.Shape_Q14[smpl_buf_idx.Val] = sampleStates[SS_left].sLTP_shp_Q14
			psDD.Seed = int(silk_ADD32_ovflw(int32(psDD.Seed), int32(silk_RSHIFT_ROUND(sampleStates[SS_left].Q_Q10, 10))))

			psDD.RandState[smpl_buf_idx.Val] = psDD.Seed
			psDD.RD_Q10 = sampleStates[SS_left].RD_Q10
		}
		delayedGain_Q10[smpl_buf_idx.Val] = Gain_Q10
	}

	for k = 0; k < nStatesDelayedDecision; k++ {
		psDD := psDelDec[k]
		copy(psDD.sLPC_Q14, psDD.sLPC_Q14[length:])
	}
}

func (s *SilkNSQState) silk_nsq_del_dec_scale_states(
	psEncC *SilkChannelEncoder,
	psDelDec []*NSQ_del_dec_struct,
	x_Q3 []int,
	x_Q3_ptr int,
	x_sc_Q10 []int,
	sLTP []int16,
	sLTP_Q15 []int,
	subfr int,
	nStatesDelayedDecision int,
	LTP_scale_Q14 int,
	Gains_Q16 []int,
	pitchL []int,
	signal_type int,
	decisionDelay int,
) {
	var i, k, lag int
	var gain_adj_Q16, inv_gain_Q31, inv_gain_Q23 int
	var psDD *NSQ_del_dec_struct

	lag = pitchL[subfr]
	inv_gain_Q31 = silk_INVERSE32_varQ(silk_max(Gains_Q16[subfr], 1), 47)
	OpusAssert(inv_gain_Q31 != 0)

	if Gains_Q16[subfr] != s.prev_gain_Q16 {
		gain_adj_Q16 = silk_DIV32_varQ(s.prev_gain_Q16, Gains_Q16[subfr], 16)
	} else {
		gain_adj_Q16 = 1 << 16
	}

	inv_gain_Q23 = silk_RSHIFT_ROUND(inv_gain_Q31, 8)
	for i = 0; i < psEncC.subfr_length; i++ {
		x_sc_Q10[i] = silk_SMULWW(x_Q3[x_Q3_ptr+i], inv_gain_Q23)
	}

	s.prev_gain_Q16 = Gains_Q16[subfr]

	if s.rewhite_flag != 0 {
		if subfr == 0 {
			inv_gain_Q31 = silk_LSHIFT(silk_SMULWB(inv_gain_Q31, LTP_scale_Q14), 2)
		}
		for i = s.sLTP_buf_idx - lag - LTP_ORDER/2; i < s.sLTP_buf_idx; i++ {
			OpusAssert(i < MAX_FRAME_LENGTH)
			sLTP_Q15[i] = silk_SMULWB(inv_gain_Q31, int(sLTP[i]))
		}
	}

	if gain_adj_Q16 != 1<<16 {
		for i = s.sLTP_shp_buf_idx - psEncC.ltp_mem_length; i < s.sLTP_shp_buf_idx; i++ {
			s.sLTP_shp_Q14[i] = silk_SMULWW(gain_adj_Q16, s.sLTP_shp_Q14[i])
		}

		if signal_type == TYPE_VOICED && s.rewhite_flag == 0 {
			for i = s.sLTP_buf_idx - lag - LTP_ORDER/2; i < s.sLTP_buf_idx-decisionDelay; i++ {
				sLTP_Q15[i] = silk_SMULWW(gain_adj_Q16, sLTP_Q15[i])
			}
		}

		for k = 0; k < nStatesDelayedDecision; k++ {
			psDD = psDelDec[k]
			psDD.LF_AR_Q14 = silk_SMULWW(gain_adj_Q16, psDD.LF_AR_Q14)
			for i = 0; i < NSQ_LPC_BUF_LENGTH; i++ {
				psDD.sLPC_Q14[i] = silk_SMULWW(gain_adj_Q16, psDD.sLPC_Q14[i])
			}
			for i = 0; i < psEncC.shapingLPCOrder; i++ {
				psDD.sAR2_Q14[i] = silk_SMULWW(gain_adj_Q16, psDD.sAR2_Q14[i])
			}
			for i = 0; i < DECISION_DELAY; i++ {
				psDD.Pred_Q15[i] = silk_SMULWW(gain_adj_Q16, psDD.Pred_Q15[i])
				psDD.Shape_Q14[i] = silk_SMULWW(gain_adj_Q16, psDD.Shape_Q14[i])
			}
		}
	}
}
