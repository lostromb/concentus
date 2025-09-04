package opus

func silk_enc_map(a int) int {
	return silk_RSHIFT(a, 15) + 1
}

func silk_dec_map(a int) int {
	return silk_LSHIFT(a, 1) - 1
}

func silk_encode_signs(
	psRangeEnc *EntropyCoder,
	pulses []int8,
	length int,
	signalType int,
	quantOffsetType int,
	sum_pulses []int) {

	var i, j, p int
	icdf := make([]int16, 2)
	var q_ptr int
	icdf[1] = 0
	q_ptr = 0
	i = silk_SMULBB(7, silk_ADD_LSHIFT(quantOffsetType, signalType, 1))
	icdf_ptr := i
	length = silk_RSHIFT(length+(SHELL_CODEC_FRAME_LENGTH/2), LOG2_SHELL_CODEC_FRAME_LENGTH)
	for i = 0; i < length; i++ {
		p = sum_pulses[i]
		if p > 0 {
			icdf[0] = silk_sign_iCDF[icdf_ptr+silk_min(p&0x1F, 6)]
			for j = q_ptr; j < q_ptr+SHELL_CODEC_FRAME_LENGTH; j++ {
				if pulses[j] != 0 {
					psRangeEnc.enc_icdf(silk_enc_map(int(pulses[j])), icdf, 8)
				}
			}
		}
		q_ptr += SHELL_CODEC_FRAME_LENGTH
	}
}

func silk_decode_signs(
	psRangeDec *EntropyCoder,
	pulses []int16,
	length int,
	signalType int,
	quantOffsetType int,
	sum_pulses []int) {

	var i, j, p int
	var icdf = make([]int16, 2)
	var q_ptr int
	var icdf_table = SilkTables.Silk_sign_iCDF
	var icdf_ptr int
	icdf[1] = 0
	q_ptr = 0
	i = silk_SMULBB(7, silk_ADD_LSHIFT(quantOffsetType, signalType, 1))
	icdf_ptr = i
	length = silk_RSHIFT(length+SilkConstants.SHELL_CODEC_FRAME_LENGTH/2, SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH)

	for i = 0; i < length; i++ {
		p = sum_pulses[i]

		if p > 0 {
			icdf[0] = icdf_table[icdf_ptr+silk_min(p&0x1F, 6)]
			for j = 0; j < SilkConstants.SHELL_CODEC_FRAME_LENGTH; j++ {
				if pulses[q_ptr+j] > 0 {
					/* attach sign */
					pulses[q_ptr+j] *= int16(silk_dec_map(psRangeDec.dec_icdf(icdf, 8)))
				}
			}
		}

		q_ptr += SilkConstants.SHELL_CODEC_FRAME_LENGTH
	}
}
