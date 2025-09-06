package silk

import "github.com/dosgo/concentus/go/comm"

func silk_enc_map(a int) int {
	return inlines.Silk_RSHIFT(a, 15) + 1
}

func silk_dec_map(a int) int {
	return inlines.Silk_LSHIFT(a, 1) - 1
}

func silk_encode_signs(
	psRangeEnc *comm.EntropyCoder,
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
	i = inlines.Silk_SMULBB(7, inlines.Silk_ADD_LSHIFT(quantOffsetType, signalType, 1))
	icdf_ptr := i
	length = inlines.Silk_RSHIFT(length+(SHELL_CODEC_FRAME_LENGTH/2), LOG2_SHELL_CODEC_FRAME_LENGTH)
	for i = 0; i < length; i++ {
		p = sum_pulses[i]
		if p > 0 {
			icdf[0] = silk_sign_iCDF[icdf_ptr+inlines.Silk_min(p&0x1F, 6)]
			for j = q_ptr; j < q_ptr+SHELL_CODEC_FRAME_LENGTH; j++ {
				if pulses[j] != 0 {
					psRangeEnc.Enc_icdf(silk_enc_map(int(pulses[j])), icdf, 8)
				}
			}
		}
		q_ptr += SHELL_CODEC_FRAME_LENGTH
	}
}

func silk_decode_signs(
	psRangeDec *comm.EntropyCoder,
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
	i = inlines.Silk_SMULBB(7, inlines.Silk_ADD_LSHIFT(quantOffsetType, signalType, 1))
	icdf_ptr = i
	length = inlines.Silk_RSHIFT(length+SilkConstants.SHELL_CODEC_FRAME_LENGTH/2, SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH)

	for i = 0; i < length; i++ {
		p = sum_pulses[i]

		if p > 0 {
			icdf[0] = icdf_table[icdf_ptr+inlines.Silk_min(p&0x1F, 6)]
			for j = 0; j < SilkConstants.SHELL_CODEC_FRAME_LENGTH; j++ {
				if pulses[q_ptr+j] > 0 {
					/* attach sign */
					pulses[q_ptr+j] *= int16(silk_dec_map(psRangeDec.Dec_icdf(icdf, 8)))
				}
			}
		}

		q_ptr += SilkConstants.SHELL_CODEC_FRAME_LENGTH
	}
}
