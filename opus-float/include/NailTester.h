#ifndef NAILTEST_H
#define NAILTEST_H

#include <stdio.h>
#include "structs.h"
#include "structs_FLP.h"

// a very hacky and leaky way of concatenating cstrings
char* concatenate(char* a, char* b)
{
	char* dest = (char*)malloc((strlen(a) + strlen(b) + 2) * sizeof(char));
	sprintf(dest, "%s%s", a, b);
	return dest;
}

void NailTestPrintFloatArray(const float* array, int length)
{
	fprintf(stdout, "Helpers.ConvertBytesToFloatArray(new uint[] {\n");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		unsigned int testhex = *((unsigned int*)&array[c]);
		fprintf(stdout, "0x%xU", testhex);
		if (c != (length - 1))
		{
			fprintf(stdout, ", ");
		}
		if (++col > 12)
		{
			fprintf(stdout, "\n");
			col = 0;
		}
	}

	fprintf(stdout, "})");
}

void NailTestPrintIntArray(const int* array, int length)
{
	fprintf(stdout, "new int[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		fprintf(stdout, "%d", array[c]);
		if (c != (length - 1))
		{
			fprintf(stdout, ",");
		}
		if (++col > 12)
		{
			fprintf(stdout, "\n");
			col = 0;
		}
	}

	fprintf(stdout, "}");
}

void NailTestPrintUintArray(const unsigned int* array, int length)
{
	fprintf(stdout, "new uint[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		fprintf(stdout, "%dU", array[c]);
		if (c != (length - 1))
		{
			fprintf(stdout, ",");
		}
		if (++col > 12)
		{
			fprintf(stdout, "\n");
			col = 0;
		}
	}

	fprintf(stdout, "}");
}

void NailTestPrintShortArray(const short* array, int length)
{
	fprintf(stdout, "new short[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		fprintf(stdout, "%d", array[c]);
		if (c != (length - 1))
		{
			fprintf(stdout, ",");
		}
		if (++col > 16)
		{
			fprintf(stdout, "\n");
			col = 0;
		}
	}

	fprintf(stdout, "}");
}

void NailTestPrintByteArray(const unsigned char* array, int length)
{
	fprintf(stdout, "new byte[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		fprintf(stdout, "%d", array[c]);
		if (c != (length - 1))
		{
			fprintf(stdout, ",");
		}
		if (++col > 32)
		{
			fprintf(stdout, "\n");
			col = 0;
		}
	}

	fprintf(stdout, "}");
}

void NailTestPrintSbyteArray(const signed char* array, int length)
{
	fprintf(stdout, "new sbyte[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		fprintf(stdout, "%d", array[c]);
		if (c != (length - 1))
		{
			fprintf(stdout, ",");
		}
		if (++col > 32)
		{
			fprintf(stdout, "\n");
			col = 0;
		}
	}

	fprintf(stdout, "}");
}

void NailTestPrintInt(char* varName, const int var)
{
	fprintf(stdout, "%s = %d", varName, var);
}

void NailTestPrintUint(char* varName, const unsigned int var)
{
	fprintf(stdout, "%s = 0x%xU", varName, var);
}

void NailTestPrintShort(char* varName, const short var)
{
	fprintf(stdout, "%s = %d", varName, var);
}

void NailTestPrintSbyte(char* varName, const int var)
{
	fprintf(stdout, "%s = %d", varName, var);
}

void NailTestPrintFloat(char* varName, const float var)
{
	fprintf(stdout, "%s = BitConverter.ToSingle(BitConverter.GetBytes((uint)0x%xU), 0)", varName, *((unsigned int*)&var));
}


static int TestNumCounter = 0;

void NailTestPrintTestHeader(char* methodName)
{
	fprintf(stdout, "[TestMethod]\npublic void Test_%s_%d()\n{\n", methodName, TestNumCounter++);
}

void NailTestPrintTestFooter()
{
	fprintf(stdout, "}\n\n");
}

void NailTestPrintInputFloatArrayDeclaration(char* varName, const float* array, const int length)
{
	fprintf(stdout, "Pointer<float> in_%s = Helpers.WrapWithArrayPointer<float>(\n", varName);
	NailTestPrintFloatArray(array, length);
	fprintf(stdout, ");\n");
}

void NailTestPrintInputIntArrayDeclaration(char* varName, const int* array, const int length)
{
	fprintf(stdout, "Pointer<int> in_%s = Helpers.WrapWithArrayPointer<int>(\n", varName);
	NailTestPrintIntArray(array, length);
	fprintf(stdout, ");\n");
}

void NailTestPrintInputShortArrayDeclaration(char* varName, const short* array, const int length)
{
	fprintf(stdout, "Pointer<short> in_%s = Helpers.WrapWithArrayPointer<short>(\n", varName);
	NailTestPrintShortArray(array, length);
	fprintf(stdout, ");\n");
}

void NailTestPrintInputSbyteArrayDeclaration(char* varName, const signed char* array, const int length)
{
	fprintf(stdout, "Pointer<sbyte> in_%s = Helpers.WrapWithArrayPointer<sbyte>(\n", varName);
	NailTestPrintSbyteArray(array, length);
	fprintf(stdout, ");\n");
}

void NailTestPrintInputByteArrayDeclaration(char* varName, const unsigned char* array, const int length)
{
	fprintf(stdout, "Pointer<byte> in_%s = Helpers.WrapWithArrayPointer<byte>(\n", varName);
	NailTestPrintByteArray(array, length);
	fprintf(stdout, ");\n");
}

void NailTestPrintInputIntDeclaration(char* varName, const int var)
{
	fprintf(stdout, "int in_");
	NailTestPrintInt(varName, var);
	fprintf(stdout, ";\n");
}

void NailTestPrintInputUintDeclaration(char* varName, const unsigned int var)
{
	fprintf(stdout, "uint in_");
	NailTestPrintUint(varName, var);
	fprintf(stdout, ";\n");
}

void NailTestPrintInputFloatDeclaration(char* varName, const float var)
{
	fprintf(stdout, "float in_");
	NailTestPrintFloat(varName, var);
	fprintf(stdout, ";\n");
}

void NailTestPrintOutputFloatArrayDeclaration(char* varName, const float* array, const int length)
{
	fprintf(stdout, "float[] expected_%s = \n", varName);
	NailTestPrintFloatArray(array, length);
	fprintf(stdout, ";\n");
}

void NailTestPrintOutputIntArrayDeclaration(char* varName, const int* array, const int length)
{
	fprintf(stdout, "int[] expected_%s = \n", varName);
	NailTestPrintIntArray(array, length);
	fprintf(stdout, ";\n");
}

void NailTestPrintOutputSbyteArrayDeclaration(char* varName, const signed char* array, const int length)
{
	fprintf(stdout, "sbyte[] expected_%s = \n", varName);
	NailTestPrintSbyteArray(array, length);
	fprintf(stdout, ";\n");
}

void NailTestPrintOutputIntDeclaration(char* varName, int var)
{
	fprintf(stdout, "int expected_");
	NailTestPrintInt(varName, var);
	fprintf(stdout, ";\n");
}

void NailTestPrintOutputUintDeclaration(char* varName, unsigned int var)
{
	fprintf(stdout, "uint expected_");
	NailTestPrintUint(varName, var);
	fprintf(stdout, ";\n");
}

void NailTestPrintOutputShortArrayDeclaration(char* varName, const short* array, const int length)
{
	fprintf(stdout, "short[] expected_%s = \n", varName);
	NailTestPrintShortArray(array, length);
	fprintf(stdout, ";\n");
}

void NailTestPrintOutputShortDeclaration(char* varName, const short var)
{
	fprintf(stdout, "short expected_");
	NailTestPrintShort(varName, var);
	fprintf(stdout, ";\n");
}

void NailTestPrintOutputByteDeclaration(char* varName, const unsigned char var)
{
	fprintf(stdout, "byte expected_");
	NailTestPrintInt(varName, var);
	fprintf(stdout, ";\n");
}

void NailTestPrintOutputSbyteDeclaration(char* varName, const signed char var)
{
	fprintf(stdout, "sbyte expected_");
	NailTestPrintInt(varName, var);
	fprintf(stdout, ";\n");
}

void NailTestPrintOutputFloatDeclaration(char* varName, const float var)
{
	fprintf(stdout, "float expected_");
	NailTestPrintFloat(varName, var);
	fprintf(stdout, ";\n");
}

// PRINT silk_nsq_state
void NailTesterPrint_silk_nsq_state(char* varName, const silk_nsq_state* nsq_state)
{
	fprintf(stdout, "%s = new silk_nsq_state();\n", varName);
	fprintf(stdout, "%s.xq = new Pointer<short>(", varName);
	NailTestPrintShortArray(nsq_state->xq, 640);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.sLTP_shp_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(nsq_state->sLTP_shp_Q14, 640);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.sLPC_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(nsq_state->sLPC_Q14, 112);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.sAR2_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(nsq_state->sAR2_Q14, 16);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("sLF_AR_shp_Q14", nsq_state->sLF_AR_shp_Q14);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("lagPrev", nsq_state->lagPrev);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("sLTP_buf_idx", nsq_state->sLTP_buf_idx);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("sLTP_shp_buf_idx", nsq_state->sLTP_shp_buf_idx);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("rand_seed", nsq_state->rand_seed);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("prev_gain_Q16", nsq_state->prev_gain_Q16);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("rewhite_flag", nsq_state->rewhite_flag);
	fprintf(stdout, ";\n");
}

// PRINT silk_VAD_state
void NailTesterPrint_silk_VAD_state(char* varName, const silk_VAD_state* vad_state)
{
	fprintf(stdout, "%s = new silk_VAD_state();\n", varName);
	fprintf(stdout, "%s.AnaState = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->AnaState, 2);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.AnaState1 = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->AnaState1, 2);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.AnaState2 = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->AnaState2, 2);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.XnrgSubfr = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->XnrgSubfr, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.NrgRatioSmth_Q8 = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->NrgRatioSmth_Q8, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("HPstate", vad_state->HPstate);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.NL = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->NL, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.inv_NL = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->inv_NL, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.NoiseLevelBias = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->NoiseLevelBias, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("counter", vad_state->counter);
	fprintf(stdout, ";\n");
}

// PRINT silk_LP_state
void NailTesterPrint_silk_LP_state(char* varName, const silk_LP_state* state)
{
	fprintf(stdout, "%s = new silk_LP_state();\n", varName);
	fprintf(stdout, "%s.In_LP_State = new Pointer<int>(", varName);
	NailTestPrintIntArray(state->In_LP_State, 2);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("transition_frame_no", state->transition_frame_no);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("mode", state->mode);
	fprintf(stdout, ";\n");
}

// PRINT silk_NLSF_CB_struct
void NailTesterPrint_silk_NLSF_CB_struct(char* varName, const silk_NLSF_CB_struct* state, int arraySize)
{
	fprintf(stdout, "%s = new silk_NLSF_CB_struct();\n", varName);
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("nVectors", state->nVectors);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("order", state->order);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("quantStepSize_Q16", state->quantStepSize_Q16);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("invQuantStepSize_Q6", state->invQuantStepSize_Q6);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.CB1_NLSF_Q8 = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->CB1_NLSF_Q8, arraySize); // FIXME what are the sizes here?
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.CB1_iCDF = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->CB1_iCDF, arraySize);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.pred_Q8 = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->pred_Q8, arraySize);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.ec_sel = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->ec_sel, arraySize);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.ec_iCDF = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->ec_iCDF, arraySize);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.ec_Rates_Q5 = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->ec_Rates_Q5, arraySize);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.deltaMin_Q15 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->deltaMin_Q15, arraySize);
	fprintf(stdout, ");\n");
}

// PRINT SideInfoIndices
void NailTesterPrint_SideInfoIndices(char* varName, const SideInfoIndices* indices)
{
	fprintf(stdout, "%s = new SideInfoIndices();\n", varName);
	fprintf(stdout, "%s.GainsIndices = new Pointer<sbyte>(", varName);
	NailTestPrintSbyteArray(indices->GainsIndices, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.LTPIndex = new Pointer<sbyte>(", varName);
	NailTestPrintSbyteArray(indices->LTPIndex, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.NLSFIndices = new Pointer<sbyte>(", varName);
	NailTestPrintSbyteArray(indices->NLSFIndices, 17);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("lagIndex", indices->lagIndex);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintSbyte("contourIndex", indices->contourIndex);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintSbyte("signalType", indices->signalType);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintSbyte("quantOffsetType", indices->quantOffsetType);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintSbyte("NLSFInterpCoef_Q2", indices->NLSFInterpCoef_Q2);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintSbyte("PERIndex", indices->PERIndex);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintSbyte("LTP_scaleIndex", indices->LTP_scaleIndex);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintSbyte("Seed", indices->Seed);
	fprintf(stdout, ";\n");
}

// PRINT stereo_enc_state
void NailTesterPrint_stereo_enc_state(char* varName, const stereo_enc_state* state)
{
	fprintf(stdout, "%s = new stereo_enc_state();\n", varName);
	fprintf(stdout, "%s.pred_prev_Q13 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->pred_prev_Q13, 2);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.sMid = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->sMid, 2);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.sSide = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->sSide, 2);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.mid_side_amp_Q0 = new Pointer<int>(", varName);
	NailTestPrintIntArray(state->mid_side_amp_Q0, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("smth_width_Q14", state->smth_width_Q14);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("width_prev_Q14", state->width_prev_Q14);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("silent_side_len", state->silent_side_len);
	fprintf(stdout, ";\n");
	
	fprintf(stdout, "%s.predIx = Pointer.Malloc<Pointer<Pointer<sbyte>>>(SilkConstants.MAX_FRAMES_PER_PACKET);\n", varName);
	for (int x = 0; x < MAX_FRAMES_PER_PACKET; x++)
	{
		fprintf(stdout, "%s.predIx[%d] = Pointer.Malloc<Pointer<sbyte>>(2);\n", varName, x);
		for (int y = 0; y < 2; y++)
		{
			fprintf(stdout, "%s.predIx[%d][%d] = new Pointer<sbyte>(", varName, x, y);
			NailTestPrintSbyteArray(state->predIx[x][y], 3);
			fprintf(stdout, ");\n");
		}
	}

	fprintf(stdout, "%s.mid_only_flags = new Pointer<sbyte>(", varName);
	NailTestPrintSbyteArray(state->mid_only_flags, 3);
	fprintf(stdout, ");\n");
}

// PRINT stereo_dec_state
void NailTesterPrint_stereo_dec_state(char* varName, const stereo_dec_state* state)
{
	fprintf(stdout, "%s = new stereo_dec_state();\n", varName);
	fprintf(stdout, "%s.pred_prev_Q13 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->pred_prev_Q13, 2);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.sMid = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->sMid, 2);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.sSide = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->sSide, 2);
	fprintf(stdout, ");\n");
}

// PRINT silk_PLC_struct
void NailTesterPrint_silk_PLC_struct(char* varName, const silk_PLC_struct* state)
{
	fprintf(stdout, "%s = new silk_PLC_struct();\n", varName);
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("pitchL_Q8", state->pitchL_Q8);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.LTPCoef_Q14 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->LTPCoef_Q14, 5);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.prevLPC_Q12 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->prevLPC_Q12, 16);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("last_frame_lost", state->last_frame_lost);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("rand_seed", state->rand_seed);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("randScale_Q14", state->randScale_Q14);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("conc_energy", state->conc_energy);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("conc_energy_shift", state->conc_energy_shift);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintShort("prevLTP_scale_Q14", state->prevLTP_scale_Q14);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.prevGain_Q16 = new Pointer<int>(", varName);
	NailTestPrintIntArray(state->prevGain_Q16, 2);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("fs_kHz", state->fs_kHz);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("nb_subfr", state->nb_subfr);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("subfr_length", state->subfr_length);
	fprintf(stdout, ";\n");
}

// PRINT silksilk_CNG_struct_PLC_struct
void NailTesterPrint_silk_CNG_struct(char* varName, const silk_CNG_struct* state)
{
	fprintf(stdout, "%s = new silk_CNG_struct();\n", varName);
	fprintf(stdout, "%s.CNG_exc_buf_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(state->CNG_exc_buf_Q14, 320);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.CNG_smth_NLSF_Q15 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->CNG_smth_NLSF_Q15, 16);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.CNG_synth_state = new Pointer<int>(", varName);
	NailTestPrintIntArray(state->CNG_synth_state, 16);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("CNG_smth_Gain_Q16", state->CNG_smth_Gain_Q16);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("rand_seed", state->rand_seed);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("fs_kHz", state->fs_kHz);
	fprintf(stdout, ";\n");
}

// PRINT ec_ctx
void NailTesterPrint_ec_ctx(char* varName, const ec_ctx* coder)
{
	fprintf(stdout, "%s = new ec_ctx();\n", varName);
	fprintf(stdout, "%s.buf = new Pointer<byte>(", varName);
	NailTestPrintByteArray(coder->buf, coder->storage);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintUint("storage", coder->storage);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintUint("end_offs", coder->end_offs);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintUint("end_window", coder->end_window);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("nend_bits", coder->nend_bits);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("nbits_total", coder->nbits_total);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintUint("offs", coder->offs);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintUint("rng", coder->rng);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintUint("val", coder->val);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintUint("ext", coder->ext);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("rem", coder->rem);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintInt("error", coder->error);
	fprintf(stdout, ";\n");
}

// PRINT silk_encoder_control
void NailTesterPrint_silk_encoder_control(char* varName, const silk_encoder_control_FLP* enc_control)
{
	fprintf(stdout, "%s = new silk_encoder_control();\n", varName);
	fprintf(stdout, "%s.Gains = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->Gains, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.PredCoef = Pointer.Malloc<Pointer<float>>(2);\n", varName);
	fprintf(stdout, "%s.PredCoef[0] = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->PredCoef[0], 16);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.PredCoef[1] = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->PredCoef[1], 16);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.LTPCoef = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->LTPCoef, 20);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintFloat("LTP_scale", enc_control->LTP_scale);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.pitchL = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->pitchL, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.AR1 = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->AR1, 64);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.AR2 = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->AR2, 64);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.LF_MA_shp = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->LF_MA_shp, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.LF_AR_shp = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->LF_AR_shp, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.GainsPre = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->GainsPre, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.HarmBoost = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->HarmBoost, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.Tilt = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->Tilt, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.HarmShapeGain = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->HarmShapeGain, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintFloat("Lambda", enc_control->Lambda);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintFloat("input_quality", enc_control->input_quality);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintFloat("coding_quality", enc_control->coding_quality);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintFloat("sparseness", enc_control->sparseness);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintFloat("predGain", enc_control->predGain);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintFloat("LTPredCodGain", enc_control->LTPredCodGain);
	fprintf(stdout, ";\n");
	fprintf(stdout, "%s.ResNrg = new Pointer<float>(", varName);
	NailTestPrintFloatArray(enc_control->ResNrg, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.GainsUnq_Q16 = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->GainsUnq_Q16, 4);
	fprintf(stdout, ");\n");
	fprintf(stdout, "%s.", varName);
	NailTestPrintSbyte("lastGainIndexPrev", enc_control->lastGainIndexPrev);
	fprintf(stdout, ";\n");
}

void NailTestPrintMemberVarInt(char* structName, char* varName, const int value)
{
	fprintf(stdout, "%s.", structName);
	NailTestPrintInt(varName, value);
	fprintf(stdout, ";\n");
}

void NailTestPrintMemberVarFloat(char* structName, char* varName, const float value)
{
	fprintf(stdout, "%s.", structName);
	NailTestPrintFloat(varName, value);
	fprintf(stdout, ";\n");
}

void NailTestPrintMemberVarShort(char* structName, char* varName, const short value)
{
	fprintf(stdout, "%s.", structName);
	NailTestPrintShort(varName, value);
	fprintf(stdout, ";\n");
}

void NailTestPrintMemberVarSbyte(char* structName, char* varName, const signed char value)
{
	fprintf(stdout, "%s.", structName);
	NailTestPrintSbyte(varName, value);
	fprintf(stdout, ";\n");
}

void NailTestPrintMemberVarIntArray(char* structName, char* varName, const int* value, int length)
{
	fprintf(stdout, "%s.%s = new Pointer<int>(", structName, varName);
	NailTestPrintIntArray(value, length);
	fprintf(stdout, ");\n");
}

void NailTestPrintMemberVarShortArray(char* structName, char* varName, const short* value, int length)
{
	fprintf(stdout, "%s.%s = new Pointer<short>(", structName, varName);
	NailTestPrintShortArray(value, length);
	fprintf(stdout, ");\n");
}

void NailTestPrintMemberVarByteArray(char* structName, char* varName, const unsigned char* value, int length)
{
	fprintf(stdout, "%s.%s = new Pointer<byte>(", structName, varName);
	NailTestPrintByteArray(value, length);
	fprintf(stdout, ");\n");
}

void NailTestPrintMemberVarSbyteArray(char* structName, char* varName, const signed char* value, int length)
{
	fprintf(stdout, "%s.%s = new Pointer<sbyte>(", structName, varName);
	NailTestPrintSbyteArray(value, length);
	fprintf(stdout, ");\n");
}

void NailTestPrintMemberVarFloatArray(char* structName, char* varName, const float* value, int length)
{
	fprintf(stdout, "%s.%s = new Pointer<float>(", structName, varName);
	NailTestPrintFloatArray(value, length);
	fprintf(stdout, ");\n");
}

// PRINT silk_encoder_state
void NailTesterPrint_silk_encoder_state(char* varName, const silk_encoder_state* state)
{
	fprintf(stdout, "%s = new silk_encoder_state();\n", varName);
	NailTestPrintMemberVarIntArray(varName, "In_HP_State", state->In_HP_State, 2);
	NailTestPrintMemberVarInt(varName, "variable_HP_smth1_Q15", state->variable_HP_smth1_Q15);
	NailTestPrintMemberVarInt(varName, "variable_HP_smth2_Q15", state->variable_HP_smth2_Q15);
	NailTesterPrint_silk_LP_state(concatenate(varName, ".sLP"), &state->sLP);
	NailTesterPrint_silk_VAD_state(concatenate(varName, ".sVAD"), &state->sVAD);
	NailTesterPrint_silk_nsq_state(concatenate(varName, ".sNSQ"), &state->sNSQ);
	NailTestPrintMemberVarShortArray(varName, "prev_NLSFq_Q15", state->prev_NLSFq_Q15, MAX_LPC_ORDER);
	NailTestPrintMemberVarInt(varName, "speech_activity_Q8", state->speech_activity_Q8);
	NailTestPrintMemberVarInt(varName, "allow_bandwidth_switch", state->allow_bandwidth_switch);
	NailTestPrintMemberVarSbyte(varName, "LBRRprevLastGainIndex", state->LBRRprevLastGainIndex);
	NailTestPrintMemberVarSbyte(varName, "prevSignalType", state->prevSignalType);
	NailTestPrintMemberVarInt(varName, "prevLag", state->prevLag);
	NailTestPrintMemberVarInt(varName, "pitch_LPC_win_length", state->pitch_LPC_win_length);
	NailTestPrintMemberVarInt(varName, "max_pitch_lag", state->max_pitch_lag);
	NailTestPrintMemberVarInt(varName, "API_fs_Hz", state->API_fs_Hz);
	NailTestPrintMemberVarInt(varName, "prev_API_fs_Hz", state->prev_API_fs_Hz);
	NailTestPrintMemberVarInt(varName, "maxInternal_fs_Hz", state->maxInternal_fs_Hz);
	NailTestPrintMemberVarInt(varName, "minInternal_fs_Hz", state->minInternal_fs_Hz);
	NailTestPrintMemberVarInt(varName, "desiredInternal_fs_Hz", state->desiredInternal_fs_Hz);
	NailTestPrintMemberVarInt(varName, "fs_kHz", state->fs_kHz);
	NailTestPrintMemberVarInt(varName, "nb_subfr", state->nb_subfr);
	NailTestPrintMemberVarInt(varName, "frame_length", state->frame_length);
	NailTestPrintMemberVarInt(varName, "subfr_length", state->subfr_length);
	NailTestPrintMemberVarInt(varName, "ltp_mem_length", state->ltp_mem_length);
	NailTestPrintMemberVarInt(varName, "la_pitch", state->la_pitch);
	NailTestPrintMemberVarInt(varName, "la_shape", state->la_shape);
	NailTestPrintMemberVarInt(varName, "shapeWinLength", state->shapeWinLength);
	NailTestPrintMemberVarInt(varName, "TargetRate_bps", state->TargetRate_bps);
	NailTestPrintMemberVarInt(varName, "PacketSize_ms", state->PacketSize_ms);
	NailTestPrintMemberVarInt(varName, "PacketLoss_perc", state->PacketLoss_perc);
	NailTestPrintMemberVarInt(varName, "frameCounter", state->frameCounter);
	NailTestPrintMemberVarInt(varName, "Complexity", state->Complexity);
	NailTestPrintMemberVarInt(varName, "nStatesDelayedDecision", state->nStatesDelayedDecision);
	NailTestPrintMemberVarInt(varName, "useInterpolatedNLSFs", state->useInterpolatedNLSFs);
	NailTestPrintMemberVarInt(varName, "shapingLPCOrder", state->shapingLPCOrder);
	NailTestPrintMemberVarInt(varName, "predictLPCOrder", state->predictLPCOrder);
	NailTestPrintMemberVarInt(varName, "pitchEstimationComplexity", state->pitchEstimationComplexity);
	NailTestPrintMemberVarInt(varName, "pitchEstimationLPCOrder", state->pitchEstimationLPCOrder);
	NailTestPrintMemberVarInt(varName, "pitchEstimationThreshold_Q16", state->pitchEstimationThreshold_Q16);
	NailTestPrintMemberVarInt(varName, "LTPQuantLowComplexity", state->LTPQuantLowComplexity);
	NailTestPrintMemberVarInt(varName, "mu_LTP_Q9", state->mu_LTP_Q9);
	NailTestPrintMemberVarInt(varName, "sum_log_gain_Q7", state->sum_log_gain_Q7);
	NailTestPrintMemberVarInt(varName, "NLSF_MSVQ_Survivors", state->NLSF_MSVQ_Survivors);
	NailTestPrintMemberVarInt(varName, "first_frame_after_reset", state->first_frame_after_reset);
	NailTestPrintMemberVarInt(varName, "controlled_since_last_payload", state->controlled_since_last_payload);
	NailTestPrintMemberVarInt(varName, "warping_Q16", state->warping_Q16);
	NailTestPrintMemberVarInt(varName, "useCBR", state->useCBR);
	NailTestPrintMemberVarInt(varName, "prefillFlag", state->prefillFlag);
	NailTestPrintMemberVarByteArray(varName, "pitch_lag_low_bits_iCDF", state->pitch_lag_low_bits_iCDF, 1024);
	NailTestPrintMemberVarByteArray(varName, "pitch_contour_iCDF", state->pitch_contour_iCDF, 1024);
	NailTesterPrint_silk_NLSF_CB_struct(concatenate(varName, ".psNLSF_CB"), state->psNLSF_CB, 1024);
	NailTestPrintMemberVarIntArray(varName, "input_quality_bands_Q15", state->input_quality_bands_Q15, 4);
	NailTestPrintMemberVarInt(varName, "input_tilt_Q15", state->input_tilt_Q15);
	NailTestPrintMemberVarInt(varName, "SNR_dB_Q7", state->SNR_dB_Q7);
	NailTestPrintMemberVarSbyteArray(varName, "VAD_flags", state->VAD_flags, 3);
	NailTestPrintMemberVarInt(varName, "LBRR_flag", state->LBRR_flag);
	NailTestPrintMemberVarIntArray(varName, "LBRR_flags", state->LBRR_flags, 3);
	NailTesterPrint_SideInfoIndices(concatenate(varName, ".indices"), &state->indices);
	NailTestPrintMemberVarSbyteArray(varName, "pulses", state->pulses, MAX_FRAME_LENGTH);
	NailTestPrintMemberVarInt(varName, "arch", state->arch);
	NailTestPrintMemberVarShortArray(varName, "inputBuf", state->inputBuf, MAX_FRAME_LENGTH + 2);
	NailTestPrintMemberVarInt(varName, "inputBufIx", state->inputBufIx);
	NailTestPrintMemberVarInt(varName, "nFramesPerPacket", state->nFramesPerPacket);
	NailTestPrintMemberVarInt(varName, "nFramesEncoded", state->nFramesEncoded);
	NailTestPrintMemberVarInt(varName, "nChannelsAPI", state->nChannelsAPI);
	NailTestPrintMemberVarInt(varName, "nChannelsInternal", state->nChannelsInternal);
	NailTestPrintMemberVarInt(varName, "channelNb", state->channelNb);
	NailTestPrintMemberVarInt(varName, "frames_since_onset", state->frames_since_onset);
	NailTestPrintMemberVarInt(varName, "ec_prevSignalType", state->ec_prevSignalType);
	NailTestPrintMemberVarShort(varName, "ec_prevLagIndex", state->ec_prevLagIndex);
	//NailTestPrintMemberVarInt(varName, "resampler_state", state->resampler_state);
	NailTestPrintMemberVarInt(varName, "useDTX", state->useDTX);
	NailTestPrintMemberVarInt(varName, "inDTX", state->inDTX);
	NailTestPrintMemberVarInt(varName, "noSpeechCounter", state->noSpeechCounter);
	NailTestPrintMemberVarInt(varName, "useInBandFEC", state->useInBandFEC);
	NailTestPrintMemberVarInt(varName, "LBRR_enabled", state->LBRR_enabled);
	NailTestPrintMemberVarInt(varName, "LBRR_GainIncreases", state->LBRR_GainIncreases);
	NailTesterPrint_SideInfoIndices(concatenate(varName, ".indices_LBRR[0]"), &state->indices_LBRR[0]);
	NailTesterPrint_SideInfoIndices(concatenate(varName, ".indices_LBRR[1]"), &state->indices_LBRR[1]);
	NailTesterPrint_SideInfoIndices(concatenate(varName, ".indices_LBRR[2]"), &state->indices_LBRR[2]);
	NailTestPrintMemberVarSbyteArray(varName, "pulses_LBRR[0]", state->pulses_LBRR[0], 320);
	NailTestPrintMemberVarSbyteArray(varName, "pulses_LBRR[1]", state->pulses_LBRR[1], 320);
	NailTestPrintMemberVarSbyteArray(varName, "pulses_LBRR[2]", state->pulses_LBRR[2], 320);
}

// PRINT silk_shape_state_FLP
void NailTesterPrint_silk_shape_state_FLP(char* varName, const silk_shape_state_FLP* state)
{
	fprintf(stdout, "%s = new silk_shape_state();\n", varName);
	NailTestPrintMemberVarSbyte(varName, "LastGainIndex", state->LastGainIndex);
	NailTestPrintMemberVarFloat(varName, "HarmBoost_smth", state->HarmBoost_smth);
	NailTestPrintMemberVarFloat(varName, "HarmShapeGain_smth", state->HarmShapeGain_smth);
	NailTestPrintMemberVarFloat(varName, "Tilt_smth", state->Tilt_smth);
}

// PRINT silk_shape_state_FLP
void NailTesterPrint_silk_prefilter_state_FLP(char* varName, const silk_prefilter_state_FLP* state)
{
	fprintf(stdout, "%s = new silk_prefilter_state();\n", varName);
	NailTestPrintMemberVarFloatArray(varName, "sLTP_shp", state->sLTP_shp, 512);
	NailTestPrintMemberVarFloatArray(varName, "sAR_shp", state->sAR_shp, 17);
	NailTestPrintMemberVarInt(varName, "sLTP_shp_buf_idx", state->sLTP_shp_buf_idx);
	NailTestPrintMemberVarFloat(varName, "sLF_AR_shp", state->sLF_AR_shp);
	NailTestPrintMemberVarFloat(varName, "sLF_MA_shp", state->sLF_MA_shp);
	NailTestPrintMemberVarFloat(varName, "sHarmHP", state->sHarmHP);
	NailTestPrintMemberVarInt(varName, "rand_seed", state->rand_seed);
	NailTestPrintMemberVarInt(varName, "lagPrev", state->lagPrev);
}

// PRINT silk_encoder_state_FLP
void NailTesterPrint_silk_encoder_state_FLP(char* varName, const silk_encoder_state_FLP* enc_state)
{
	fprintf(stdout, "%s = new silk_encoder_state_flp();\n", varName);
	NailTesterPrint_silk_encoder_state(concatenate(varName, ".sCmn"), &enc_state->sCmn);
	NailTesterPrint_silk_shape_state_FLP(concatenate(varName, ".sShape"), &enc_state->sShape);
	NailTesterPrint_silk_prefilter_state_FLP(concatenate(varName, ".sPrefilt"), &enc_state->sPrefilt);
	NailTestPrintMemberVarFloatArray(varName, "x_buf", enc_state->x_buf, 720);
	NailTestPrintMemberVarFloat(varName, "LTPCorr", enc_state->LTPCorr);
}

#endif