package opus

func combine_pulsesLen(output, input []int, input_ptr int, len int) {
	var k int
	for k = 0; k < len; k++ {
		output[k] = input[input_ptr+(2*k)] + input[input_ptr+(2*k)+1]
	}
}
func combine_pulses(output, input []int, len int) {
	var k int
	for k = 0; k < len; k++ {
		output[k] = input[2*k] + input[2*k+1]
	}
}

func encode_split(
	psRangeEnc *EntropyCoder,
	p_child1 int,
	p int,
	shell_table []int16,
) {
	if p > 0 {
		psRangeEnc.enc_icdf_offset(p_child1, shell_table, int(SilkTables.Silk_shell_code_table_offsets[p]), 8)
	}
}

func decode_split(
	p_child1 []int16,
	child1_ptr int,
	p_child2 []int16,
	p_child2_ptr int,
	psRangeDec *EntropyCoder,
	p int,
	shell_table []int16,
) {
	if p > 0 {
		p_child1[child1_ptr] = int16(psRangeDec.dec_icdf_offset(shell_table, int(SilkTables.Silk_shell_code_table_offsets[p]), 8))
		p_child2[p_child2_ptr] = int16(p) - p_child1[child1_ptr]
	} else {
		p_child1[child1_ptr] = 0
		p_child2[p_child2_ptr] = 0
	}
}

func silk_shell_encoder(psRangeEnc *EntropyCoder, pulses0 []int, pulses0_ptr int) {
	pulses1 := make([]int, 8)
	pulses2 := make([]int, 4)
	pulses3 := make([]int, 2)
	pulses4 := make([]int, 1)

	/* this function operates on one shell code frame of 16 pulses */
	OpusAssert(SilkConstants.SHELL_CODEC_FRAME_LENGTH == 16)

	/* tree representation per pulse-subframe */
	combine_pulsesLen(pulses1, pulses0, pulses0_ptr, 8)
	combine_pulses(pulses2, pulses1, 4)
	combine_pulses(pulses3, pulses2, 2)
	combine_pulses(pulses4, pulses3, 1)

	encode_split(psRangeEnc, pulses3[0], pulses4[0], silk_shell_code_table3)

	encode_split(psRangeEnc, pulses2[0], pulses3[0], silk_shell_code_table2)

	encode_split(psRangeEnc, pulses1[0], pulses2[0], silk_shell_code_table1)
	encode_split(psRangeEnc, pulses0[pulses0_ptr], pulses1[0], silk_shell_code_table0)
	encode_split(psRangeEnc, pulses0[pulses0_ptr+2], pulses1[1], silk_shell_code_table0)

	encode_split(psRangeEnc, pulses1[2], pulses2[1], silk_shell_code_table1)
	encode_split(psRangeEnc, pulses0[pulses0_ptr+4], pulses1[2], silk_shell_code_table0)
	encode_split(psRangeEnc, pulses0[pulses0_ptr+6], pulses1[3], silk_shell_code_table0)

	encode_split(psRangeEnc, pulses2[2], pulses3[1], silk_shell_code_table2)

	encode_split(psRangeEnc, pulses1[4], pulses2[2], silk_shell_code_table1)
	encode_split(psRangeEnc, pulses0[pulses0_ptr+8], pulses1[4], silk_shell_code_table0)
	encode_split(psRangeEnc, pulses0[pulses0_ptr+10], pulses1[5], silk_shell_code_table0)

	encode_split(psRangeEnc, pulses1[6], pulses2[3], silk_shell_code_table1)
	encode_split(psRangeEnc, pulses0[pulses0_ptr+12], pulses1[6], silk_shell_code_table0)
	encode_split(psRangeEnc, pulses0[pulses0_ptr+14], pulses1[7], silk_shell_code_table0)
}

func silk_shell_decoder(
	pulses0 []int16,
	pulses0_ptr int,
	psRangeDec *EntropyCoder,
	pulses4 int,
) {
	pulses1 := make([]int16, 8)
	pulses2 := make([]int16, 4)
	pulses3 := make([]int16, 2)

	OpusAssert(SilkConstants.SHELL_CODEC_FRAME_LENGTH == 16)

	decode_split(pulses3, 0, pulses3, 1, psRangeDec, pulses4, SilkTables.Silk_shell_code_table3)
	decode_split(pulses2, 0, pulses2, 1, psRangeDec, int(pulses3[0]), SilkTables.Silk_shell_code_table2)
	decode_split(pulses1, 0, pulses1, 1, psRangeDec, int(pulses2[0]), SilkTables.Silk_shell_code_table1)
	decode_split(pulses0, pulses0_ptr, pulses0, pulses0_ptr+1, psRangeDec, int(pulses1[0]), SilkTables.Silk_shell_code_table0)
	decode_split(pulses0, pulses0_ptr+2, pulses0, pulses0_ptr+3, psRangeDec, int(pulses1[1]), SilkTables.Silk_shell_code_table0)
	decode_split(pulses1, 2, pulses1, 3, psRangeDec, int(pulses2[1]), SilkTables.Silk_shell_code_table1)
	decode_split(pulses0, pulses0_ptr+4, pulses0, pulses0_ptr+5, psRangeDec, int(pulses1[2]), SilkTables.Silk_shell_code_table0)
	decode_split(pulses0, pulses0_ptr+6, pulses0, pulses0_ptr+7, psRangeDec, int(pulses1[3]), SilkTables.Silk_shell_code_table0)
	decode_split(pulses2, 2, pulses2, 3, psRangeDec, int(pulses3[1]), SilkTables.Silk_shell_code_table2)
	decode_split(pulses1, 4, pulses1, 5, psRangeDec, int(pulses2[2]), SilkTables.Silk_shell_code_table1)
	decode_split(pulses0, pulses0_ptr+8, pulses0, pulses0_ptr+9, psRangeDec, int(pulses1[4]), SilkTables.Silk_shell_code_table0)
	decode_split(pulses0, pulses0_ptr+10, pulses0, pulses0_ptr+11, psRangeDec, int(pulses1[5]), SilkTables.Silk_shell_code_table0)
	decode_split(pulses1, 6, pulses1, 7, psRangeDec, int(pulses2[3]), SilkTables.Silk_shell_code_table1)
	decode_split(pulses0, pulses0_ptr+12, pulses0, pulses0_ptr+13, psRangeDec, int(pulses1[6]), SilkTables.Silk_shell_code_table0)
	decode_split(pulses0, pulses0_ptr+14, pulses0, pulses0_ptr+15, psRangeDec, int(pulses1[7]), SilkTables.Silk_shell_code_table0)
}
