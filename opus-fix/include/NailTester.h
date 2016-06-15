#ifndef NAILTEST_H
#define NAILTEST_H

#include <stdio.h>
#include "../silk/resampler_structs.h"
#include "../silk/structs.h"
#include "../silk/fixed/structs_FIX.h"
#include "../celt/modes.h"
#include "opus_custom.h"
#include "celt.h"

// a very hacky and leaky way of concatenating cstrings
char* concatenate(char* a, char* b)
{
	char* dest = (char*)malloc((strlen(a) + strlen(b) + 2) * sizeof(char));
	sprintf(dest, "%s%s", a, b);
	return dest;
}

void NailTestPrintFloatArray(const float* array, int length)
{
	printf("Helpers.ConvertBytesToFloatArray(new uint[] {\n");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		unsigned int testhex = *((unsigned int*)&array[c]);
		printf("0x%xU", testhex);
		if (c != (length - 1))
		{
			printf(", ");
		}
		if (++col > 12)
		{
			printf("\n");
			col = 0;
		}
	}

	printf("})");
}

void NailTestPrintIntArray(const int* array, int length)
{
	printf("new int[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		printf("%d", array[c]);
		if (c != (length - 1))
		{
			printf(",");
		}
		if (++col > 12)
		{
			printf("\n");
			col = 0;
		}
	}

	printf("}");
}

void NailTestPrintUintArray(const unsigned int* array, int length)
{
	printf("new uint[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		printf("%dU", array[c]);
		if (c != (length - 1))
		{
			printf(",");
		}
		if (++col > 12)
		{
			printf("\n");
			col = 0;
		}
	}

	printf("}");
}

void NailTestPrintShortArray(const short* array, int length)
{
	printf("new short[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		printf("%d", array[c]);
		if (c != (length - 1))
		{
			printf(",");
		}
		if (++col > 16)
		{
			printf("\n");
			col = 0;
		}
	}

	printf("}");
}

void NailTestPrintShortArrayAsInt(const short* array, int length)
{
	printf("new int[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		printf("%d", array[c]);
		if (c != (length - 1))
		{
			printf(",");
		}
		if (++col > 16)
		{
			printf("\n");
			col = 0;
		}
	}

	printf("}");
}

void NailTestPrintByteArray(const unsigned char* array, int length)
{
	printf("new byte[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		printf("%d", array[c]);
		if (c != (length - 1))
		{
			printf(",");
		}
		if (++col > 32)
		{
			printf("\n");
			col = 0;
		}
	}

	printf("}");
}

void NailTestPrintSbyteArray(const signed char* array, int length)
{
	printf("new sbyte[] { ");
	int col = 0;
	for (int c = 0; c < length; c++)
	{
		printf("%d", array[c]);
		if (c != (length - 1))
		{
			printf(",");
		}
		if (++col > 32)
		{
			printf("\n");
			col = 0;
		}
	}

	printf("}");
}

void NailTestPrintInt(char* varName, const int var)
{
	printf("%s = %d", varName, var);
}

void NailTestPrintUint(char* varName, const unsigned int var)
{
	printf("%s = 0x%xU", varName, var);
}

void NailTestPrintShort(char* varName, const short var)
{
	printf("%s = %d", varName, var);
}

void NailTestPrintSbyte(char* varName, const int var)
{
	printf("%s = %d", varName, var);
}

void NailTestPrintFloat(char* varName, const float var)
{
	printf("%s = BitConverter.ToSingle(BitConverter.GetBytes((uint)0x%xU), 0)", varName, *((unsigned int*)&var));
}


static int TestNumCounter = 0;

void NailTestPrintTestHeader(char* methodName)
{
	printf("[TestMethod]\npublic void Test_%s_%d()\n{\n", methodName, TestNumCounter++);
}

void NailTestPrintTestFooter()
{
	printf("}\n\n");
}

void NailTestPrintInputFloatArrayDeclaration(char* varName, const float* array, const int length)
{
	printf("Pointer<float> in_%s = Helpers.WrapWithArrayPointer<float>(\n", varName);
	NailTestPrintFloatArray(array, length);
	printf(");\n");
}

void NailTestPrintInputIntArrayDeclaration(char* varName, const int* array, const int length)
{
	printf("Pointer<int> in_%s = Helpers.WrapWithArrayPointer<int>(\n", varName);
	NailTestPrintIntArray(array, length);
	printf(");\n");
}

void NailTestPrintInputShortArrayDeclaration(char* varName, const short* array, const int length)
{
	printf("Pointer<short> in_%s = Helpers.WrapWithArrayPointer<short>(\n", varName);
	NailTestPrintShortArray(array, length);
	printf(");\n");
}

void NailTestPrintInputSbyteArrayDeclaration(char* varName, const signed char* array, const int length)
{
	printf("Pointer<sbyte> in_%s = Helpers.WrapWithArrayPointer<sbyte>(\n", varName);
	NailTestPrintSbyteArray(array, length);
	printf(");\n");
}

void NailTestPrintInputByteArrayDeclaration(char* varName, const unsigned char* array, const int length)
{
	printf("Pointer<byte> in_%s = Helpers.WrapWithArrayPointer<byte>(\n", varName);
	NailTestPrintByteArray(array, length);
	printf(");\n");
}

void NailTestPrintInputIntDeclaration(char* varName, const int var)
{
	printf("int in_");
	NailTestPrintInt(varName, var);
	printf(";\n");
}

void NailTestPrintInputUintDeclaration(char* varName, const unsigned int var)
{
	printf("uint in_");
	NailTestPrintUint(varName, var);
	printf(";\n");
}

void NailTestPrintInputSbyteDeclaration(char* varName, const signed char var)
{
	printf("sbyte in_");
	NailTestPrintSbyte(varName, var);
	printf(";\n");
}

void NailTestPrintInputFloatDeclaration(char* varName, const float var)
{
	printf("float in_");
	NailTestPrintFloat(varName, var);
	printf(";\n");
}

void NailTestPrintOutputFloatArrayDeclaration(char* varName, const float* array, const int length)
{
	printf("float[] expected_%s = \n", varName);
	NailTestPrintFloatArray(array, length);
	printf(";\n");
}

void NailTestPrintOutputIntArrayDeclaration(char* varName, const int* array, const int length)
{
	printf("int[] expected_%s = \n", varName);
	NailTestPrintIntArray(array, length);
	printf(";\n");
}

void NailTestPrintOutputSbyteArrayDeclaration(char* varName, const signed char* array, const int length)
{
	printf("sbyte[] expected_%s = \n", varName);
	NailTestPrintSbyteArray(array, length);
	printf(";\n");
}

void NailTestPrintOutputByteArrayDeclaration(char* varName, const unsigned char* array, const int length)
{
	printf("byte[] expected_%s = \n", varName);
	if (!array)
	{
		printf("new byte[0];\n");
		return;
	}
	NailTestPrintByteArray(array, length);
	printf(";\n");
}

void NailTestPrintOutputIntDeclaration(char* varName, int var)
{
	printf("int expected_");
	NailTestPrintInt(varName, var);
	printf(";\n");
}

void NailTestPrintOutputUintDeclaration(char* varName, unsigned int var)
{
	printf("uint expected_");
	NailTestPrintUint(varName, var);
	printf(";\n");
}

void NailTestPrintOutputShortArrayDeclaration(char* varName, const short* array, const int length)
{
	printf("short[] expected_%s = \n", varName);
	NailTestPrintShortArray(array, length);
	printf(";\n");
}

void NailTestPrintOutputShortDeclaration(char* varName, const short var)
{
	printf("short expected_");
	NailTestPrintShort(varName, var);
	printf(";\n");
}

void NailTestPrintOutputByteDeclaration(char* varName, const unsigned char var)
{
	printf("byte expected_");
	NailTestPrintInt(varName, var);
	printf(";\n");
}

void NailTestPrintOutputSbyteDeclaration(char* varName, const signed char var)
{
	printf("sbyte expected_");
	NailTestPrintInt(varName, var);
	printf(";\n");
}

void NailTestPrintOutputFloatDeclaration(char* varName, const float var)
{
	printf("float expected_");
	NailTestPrintFloat(varName, var);
	printf(";\n");
}

void NailTestPrintMemberVarInt(char* structName, char* varName, const int value)
{
	printf("%s.", structName);
	NailTestPrintInt(varName, value);
	printf(";\n");
}

void NailTestPrintMemberVarUint(char* structName, char* varName, const unsigned int value)
{
	printf("%s.", structName);
	NailTestPrintUint(varName, value);
	printf(";\n");
}

void NailTestPrintMemberVarFloat(char* structName, char* varName, const float value)
{
	printf("%s.", structName);
	NailTestPrintFloat(varName, value);
	printf(";\n");
}

void NailTestPrintMemberVarShort(char* structName, char* varName, const short value)
{
	printf("%s.", structName);
	NailTestPrintShort(varName, value);
	printf(";\n");
}

void NailTestPrintMemberVarSbyte(char* structName, char* varName, const signed char value)
{
	printf("%s.", structName);
	NailTestPrintSbyte(varName, value);
	printf(";\n");
}

void NailTestPrintMemberVarIntArray(char* structName, char* varName, const int* value, int length)
{
	if (!value)
	{
		printf("%s.%s = null;\n", structName, varName);
		return;
	}
	printf("%s.%s = new Pointer<int>(", structName, varName);
	NailTestPrintIntArray(value, length);
	printf(");\n");
}

void NailTestPrintMemberVarShortArray(char* structName, char* varName, const short* value, int length)
{
	if (!value)
	{
		printf("%s.%s = null;\n", structName, varName);
		return;
	}
	printf("%s.%s = new Pointer<short>(", structName, varName);
	NailTestPrintShortArray(value, length);
	printf(");\n");
}

void NailTestPrintMemberVarShortArrayAsInt(char* structName, char* varName, const short* value, int length)
{
	if (!value)
	{
		printf("%s.%s = null;\n", structName, varName);
		return;
	}
	printf("%s.%s = new Pointer<int>(", structName, varName);
	NailTestPrintShortArrayAsInt(value, length);
	printf(");\n");
}

void NailTestPrintMemberVarByteArray(char* structName, char* varName, const unsigned char* value, int length)
{
	if (!value)
	{
		printf("%s.%s = null;\n", structName, varName);
		return;
	}
	printf("%s.%s = new Pointer<byte>(", structName, varName);
	NailTestPrintByteArray(value, length);
	printf(");\n");
}

void NailTestPrintMemberVarSbyteArray(char* structName, char* varName, const signed char* value, int length)
{
	if (!value)
	{
		printf("%s.%s = null;\n", structName, varName);
		return;
	}
	printf("%s.%s = new Pointer<sbyte>(", structName, varName);
	NailTestPrintSbyteArray(value, length);
	printf(");\n");
}

void NailTestPrintMemberVarFloatArray(char* structName, char* varName, const float* value, int length)
{
	if (!value)
	{
		printf("%s.%s = null;\n", structName, varName);
		return;
	}
	printf("%s.%s = new Pointer<float>(", structName, varName);
	NailTestPrintFloatArray(value, length);
	printf(");\n");
}

// PRINT silk_nsq_state
void NailTesterPrint_silk_nsq_state(char* varName, const silk_nsq_state* nsq_state)
{
	printf("%s = new silk_nsq_state();\n", varName);
	printf("%s.xq = new Pointer<short>(", varName);
	NailTestPrintShortArray(nsq_state->xq, 640);
	printf(");\n");
	printf("%s.sLTP_shp_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(nsq_state->sLTP_shp_Q14, 640);
	printf(");\n");
	printf("%s.sLPC_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(nsq_state->sLPC_Q14, 112);
	printf(");\n");
	printf("%s.sAR2_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(nsq_state->sAR2_Q14, 16);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintInt("sLF_AR_shp_Q14", nsq_state->sLF_AR_shp_Q14);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("lagPrev", nsq_state->lagPrev);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("sLTP_buf_idx", nsq_state->sLTP_buf_idx);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("sLTP_shp_buf_idx", nsq_state->sLTP_shp_buf_idx);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("rand_seed", nsq_state->rand_seed);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("prev_gain_Q16", nsq_state->prev_gain_Q16);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("rewhite_flag", nsq_state->rewhite_flag);
	printf(";\n");
}

// PRINT silk_VAD_state
void NailTesterPrint_silk_VAD_state(char* varName, const silk_VAD_state* vad_state)
{
	printf("%s = new silk_VAD_state();\n", varName);
	printf("%s.AnaState = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->AnaState, 2);
	printf(");\n");
	printf("%s.AnaState1 = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->AnaState1, 2);
	printf(");\n");
	printf("%s.AnaState2 = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->AnaState2, 2);
	printf(");\n");
	printf("%s.XnrgSubfr = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->XnrgSubfr, 4);
	printf(");\n");
	printf("%s.NrgRatioSmth_Q8 = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->NrgRatioSmth_Q8, 4);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintShort("HPstate", vad_state->HPstate);
	printf(";\n");
	printf("%s.NL = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->NL, 4);
	printf(");\n");
	printf("%s.inv_NL = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->inv_NL, 4);
	printf(");\n");
	printf("%s.NoiseLevelBias = new Pointer<int>(", varName);
	NailTestPrintIntArray(vad_state->NoiseLevelBias, 4);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintInt("counter", vad_state->counter);
	printf(";\n");
}

// PRINT silk_LP_state
void NailTesterPrint_silk_LP_state(char* varName, const silk_LP_state* state)
{
	printf("%s = new silk_LP_state();\n", varName);
	printf("%s.In_LP_State = new Pointer<int>(", varName);
	NailTestPrintIntArray(state->In_LP_State, 2);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintInt("transition_frame_no", state->transition_frame_no);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("mode", state->mode);
	printf(";\n");
}

// PRINT silk_NLSF_CB_struct
void NailTesterPrint_silk_NLSF_CB_struct(char* varName, const silk_NLSF_CB_struct* state, int arraySize)
{
	printf("%s = new silk_NLSF_CB_struct();\n", varName);
	printf("%s.", varName);
	NailTestPrintShort("nVectors", state->nVectors);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintShort("order", state->order);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintShort("quantStepSize_Q16", state->quantStepSize_Q16);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintShort("invQuantStepSize_Q6", state->invQuantStepSize_Q6);
	printf(";\n");
	printf("%s.CB1_NLSF_Q8 = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->CB1_NLSF_Q8, 16); // FIXME sizes here are estimates
	printf(");\n");
	printf("%s.CB1_iCDF = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->CB1_iCDF, 16);
	printf(");\n");
	printf("%s.pred_Q8 = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->pred_Q8, 16);
	printf(");\n");
	printf("%s.ec_sel = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->ec_sel, 16);
	printf(");\n");
	printf("%s.ec_iCDF = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->ec_iCDF, 16);
	printf(");\n");
	printf("%s.ec_Rates_Q5 = new Pointer<byte>(", varName);
	NailTestPrintByteArray(state->ec_Rates_Q5, 16);
	printf(");\n");
	printf("%s.deltaMin_Q15 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->deltaMin_Q15, 16);
	printf(");\n");
}

// PRINT SideInfoIndices
void NailTesterPrint_SideInfoIndices(char* varName, const SideInfoIndices* indices)
{
	printf("%s = new SideInfoIndices();\n", varName);
	printf("%s.GainsIndices = new Pointer<sbyte>(", varName);
	NailTestPrintSbyteArray(indices->GainsIndices, 4);
	printf(");\n");
	printf("%s.LTPIndex = new Pointer<sbyte>(", varName);
	NailTestPrintSbyteArray(indices->LTPIndex, 4);
	printf(");\n");
	printf("%s.NLSFIndices = new Pointer<sbyte>(", varName);
	NailTestPrintSbyteArray(indices->NLSFIndices, 17);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintShort("lagIndex", indices->lagIndex);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintSbyte("contourIndex", indices->contourIndex);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintSbyte("signalType", indices->signalType);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintSbyte("quantOffsetType", indices->quantOffsetType);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintSbyte("NLSFInterpCoef_Q2", indices->NLSFInterpCoef_Q2);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintSbyte("PERIndex", indices->PERIndex);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintSbyte("LTP_scaleIndex", indices->LTP_scaleIndex);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintSbyte("Seed", indices->Seed);
	printf(";\n");
}

// PRINT stereo_enc_state
void NailTesterPrint_stereo_enc_state(char* varName, const stereo_enc_state* state)
{
	printf("%s = new stereo_enc_state();\n", varName);
	printf("%s.pred_prev_Q13 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->pred_prev_Q13, 2);
	printf(");\n");
	printf("%s.sMid = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->sMid, 2);
	printf(");\n");
	printf("%s.sSide = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->sSide, 2);
	printf(");\n");
	printf("%s.mid_side_amp_Q0 = new Pointer<int>(", varName);
	NailTestPrintIntArray(state->mid_side_amp_Q0, 4);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintShort("smth_width_Q14", state->smth_width_Q14);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintShort("width_prev_Q14", state->width_prev_Q14);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintShort("silent_side_len", state->silent_side_len);
	printf(";\n");
	
	printf("%s.predIx = Pointer.Malloc<Pointer<Pointer<sbyte>>>(SilkConstants.MAX_FRAMES_PER_PACKET);\n", varName);
	for (int x = 0; x < MAX_FRAMES_PER_PACKET; x++)
	{
		printf("%s.predIx[%d] = Pointer.Malloc<Pointer<sbyte>>(2);\n", varName, x);
		for (int y = 0; y < 2; y++)
		{
			printf("%s.predIx[%d][%d] = new Pointer<sbyte>(", varName, x, y);
			NailTestPrintSbyteArray(state->predIx[x][y], 3);
			printf(");\n");
		}
	}

	printf("%s.mid_only_flags = new Pointer<sbyte>(", varName);
	NailTestPrintSbyteArray(state->mid_only_flags, 3);
	printf(");\n");
}

// PRINT stereo_dec_state
void NailTesterPrint_stereo_dec_state(char* varName, const stereo_dec_state* state)
{
	printf("%s = new stereo_dec_state();\n", varName);
	printf("%s.pred_prev_Q13 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->pred_prev_Q13, 2);
	printf(");\n");
	printf("%s.sMid = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->sMid, 2);
	printf(");\n");
	printf("%s.sSide = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->sSide, 2);
	printf(");\n");
}

// PRINT silk_PLC_struct
void NailTesterPrint_silk_PLC_struct(char* varName, const silk_PLC_struct* state)
{
	printf("%s = new silk_PLC_struct();\n", varName);
	printf("%s.", varName);
	NailTestPrintInt("pitchL_Q8", state->pitchL_Q8);
	printf(";\n");
	printf("%s.LTPCoef_Q14 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->LTPCoef_Q14, 5);
	printf(");\n");
	printf("%s.prevLPC_Q12 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->prevLPC_Q12, 16);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintInt("last_frame_lost", state->last_frame_lost);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("rand_seed", state->rand_seed);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintShort("randScale_Q14", state->randScale_Q14);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("conc_energy", state->conc_energy);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("conc_energy_shift", state->conc_energy_shift);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintShort("prevLTP_scale_Q14", state->prevLTP_scale_Q14);
	printf(";\n");
	printf("%s.prevGain_Q16 = new Pointer<int>(", varName);
	NailTestPrintIntArray(state->prevGain_Q16, 2);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintInt("fs_kHz", state->fs_kHz);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("nb_subfr", state->nb_subfr);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("subfr_length", state->subfr_length);
	printf(";\n");
}

// PRINT silksilk_CNG_struct_PLC_struct
void NailTesterPrint_silk_CNG_struct(char* varName, const silk_CNG_struct* state)
{
	printf("%s = new silk_CNG_struct();\n", varName);
	printf("%s.CNG_exc_buf_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(state->CNG_exc_buf_Q14, 320);
	printf(");\n");
	printf("%s.CNG_smth_NLSF_Q15 = new Pointer<short>(", varName);
	NailTestPrintShortArray(state->CNG_smth_NLSF_Q15, 16);
	printf(");\n");
	printf("%s.CNG_synth_state = new Pointer<int>(", varName);
	NailTestPrintIntArray(state->CNG_synth_state, 16);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintInt("CNG_smth_Gain_Q16", state->CNG_smth_Gain_Q16);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("rand_seed", state->rand_seed);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("fs_kHz", state->fs_kHz);
	printf(";\n");
}

// PRINT ec_ctx
void NailTesterPrint_ec_ctx(char* varName, const ec_ctx* coder)
{
	if (!coder)
	{
		printf("%s = null;\n", varName);
		return;
	}

	printf("%s = new ec_ctx();\n", varName);
	NailTestPrintMemberVarByteArray(varName, "buf", coder->buf, coder->storage);
	printf("%s.", varName);
	NailTestPrintUint("storage", coder->storage);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintUint("end_offs", coder->end_offs);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintUint("end_window", coder->end_window);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("nend_bits", coder->nend_bits);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("nbits_total", coder->nbits_total);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintUint("offs", coder->offs);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintUint("rng", coder->rng);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintUint("val", coder->val);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintUint("ext", coder->ext);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("rem", coder->rem);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("error", coder->error);
	printf(";\n");
}

// PRINT silk_encoder_control
void NailTesterPrint_silk_encoder_control(char* varName, const silk_encoder_control_FIX* enc_control)
{
	printf("%s = new silk_encoder_control();\n", varName);
	printf("%s.Gains_Q16 = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->Gains_Q16, 4);
	printf(");\n");
	printf("%s.PredCoef_Q12 = new Pointer<short>(", varName);
	NailTestPrintShortArray(enc_control->PredCoef_Q12[0], 32);
	printf(");\n");
	printf("%s.LTPCoef_Q14 = new Pointer<short>(", varName);
	NailTestPrintShortArray(enc_control->LTPCoef_Q14, 20);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintInt("LTP_scale_Q14", enc_control->LTP_scale_Q14);
	printf(";\n");
	printf("%s.pitchL = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->pitchL, 4);
	printf(");\n");
	printf("%s.AR1_Q13 = new Pointer<short>(", varName);
	NailTestPrintShortArray(enc_control->AR1_Q13, 64);
	printf(");\n");
	printf("%s.AR2_Q13 = new Pointer<short>(", varName);
	NailTestPrintShortArray(enc_control->AR2_Q13, 64);
	printf(");\n");
	printf("%s.LF_shp_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->LF_shp_Q14, 4);
	printf(");\n");
	printf("%s.GainsPre_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->GainsPre_Q14, 4);
	printf(");\n");
	printf("%s.HarmBoost_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->HarmBoost_Q14, 4);
	printf(");\n");
	printf("%s.Tilt_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->Tilt_Q14, 4);
	printf(");\n");
	printf("%s.HarmShapeGain_Q14 = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->HarmShapeGain_Q14, 4);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintInt("Lambda_Q10", enc_control->Lambda_Q10);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("input_quality_Q14", enc_control->input_quality_Q14);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("coding_quality_Q14", enc_control->coding_quality_Q14);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("sparseness_Q8", enc_control->sparseness_Q8);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("predGain_Q16", enc_control->predGain_Q16);
	printf(";\n");
	printf("%s.", varName);
	NailTestPrintInt("LTPredCodGain_Q7", enc_control->LTPredCodGain_Q7);
	printf(";\n");
	printf("%s.ResNrg = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->ResNrg, 4);
	printf(");\n");
	printf("%s.ResNrgQ = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->ResNrgQ, 4);
	printf(");\n");
	printf("%s.GainsUnq_Q16 = new Pointer<int>(", varName);
	NailTestPrintIntArray(enc_control->GainsUnq_Q16, 4);
	printf(");\n");
	printf("%s.", varName);
	NailTestPrintSbyte("lastGainIndexPrev", enc_control->lastGainIndexPrev);
	printf(";\n");
}

// PRINT silk_resampler_state_struct
void NailTesterPrint_silk_resampler_state(char* varName, const silk_resampler_state_struct* state)
{
	printf("%s = new _silk_resampler_state_struct();\n", varName);
	NailTestPrintMemberVarIntArray(varName, "sIIR", state->sIIR, 6);
	NailTestPrintMemberVarIntArray(varName, "i32", state->sFIR.i32, 36);
	NailTestPrintMemberVarShortArray(varName, "i16", state->sFIR.i16, 36);
	NailTestPrintMemberVarInt(varName, "resampler_function", state->resampler_function);
	NailTestPrintMemberVarInt(varName, "batchSize", state->batchSize);
	NailTestPrintMemberVarInt(varName, "invRatio_Q16", state->invRatio_Q16);
	NailTestPrintMemberVarInt(varName, "FIR_Order", state->FIR_Order);
	NailTestPrintMemberVarInt(varName, "FIR_Fracs", state->FIR_Fracs);
	NailTestPrintMemberVarInt(varName, "Fs_in_kHz", state->Fs_in_kHz);
	NailTestPrintMemberVarInt(varName, "Fs_out_kHz", state->Fs_out_kHz);
	NailTestPrintMemberVarInt(varName, "inputDelay", state->inputDelay);
}

// PRINT silk_encoder_state
void NailTesterPrint_silk_encoder_state(char* varName, const silk_encoder_state* state)
{
	printf("%s = new silk_encoder_state();\n", varName);
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
	NailTestPrintMemberVarByteArray(varName, "pitch_lag_low_bits_iCDF", state->pitch_lag_low_bits_iCDF, 16);
	NailTestPrintMemberVarByteArray(varName, "pitch_contour_iCDF", state->pitch_contour_iCDF, 16);
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
	NailTesterPrint_silk_resampler_state(concatenate(varName, ".resampler_state"), &state->resampler_state);
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
void NailTesterPrint_silk_shape_state_FIX(char* varName, const silk_shape_state_FIX* state)
{
	printf("%s = new silk_shape_state();\n", varName);
	NailTestPrintMemberVarSbyte(varName, "LastGainIndex", state->LastGainIndex);
	NailTestPrintMemberVarInt(varName, "HarmBoost_smth_Q16", state->HarmBoost_smth_Q16);
	NailTestPrintMemberVarInt(varName, "HarmShapeGain_smth_Q16", state->HarmShapeGain_smth_Q16);
	NailTestPrintMemberVarInt(varName, "Tilt_smth_Q16", state->Tilt_smth_Q16);
}

// PRINT silk_shape_state_FLP
void NailTesterPrint_silk_prefilter_state_FIX(char* varName, const silk_prefilter_state_FIX* state)
{
	printf("%s = new silk_prefilter_state();\n", varName);
	NailTestPrintMemberVarShortArray(varName, "sLTP_shp", state->sLTP_shp, 512);
	NailTestPrintMemberVarIntArray(varName, "sAR_shp", state->sAR_shp, 17);
	NailTestPrintMemberVarInt(varName, "sLTP_shp_buf_idx", state->sLTP_shp_buf_idx);
	NailTestPrintMemberVarInt(varName, "sLF_AR_shp_Q12", state->sLF_AR_shp_Q12);
	NailTestPrintMemberVarInt(varName, "sLF_MA_shp_Q12", state->sLF_MA_shp_Q12);
	NailTestPrintMemberVarInt(varName, "sHarmHP_Q2", state->sHarmHP_Q2);
	NailTestPrintMemberVarInt(varName, "rand_seed", state->rand_seed);
	NailTestPrintMemberVarInt(varName, "lagPrev", state->lagPrev);
}

// PRINT silk_encoder_state_FLP
void NailTesterPrint_silk_encoder_state_FIX(char* varName, const silk_encoder_state_FIX* enc_state)
{
	printf("%s = new silk_encoder_state_fix();\n", varName);
	NailTesterPrint_silk_encoder_state(concatenate(varName, ".sCmn"), &enc_state->sCmn);
	NailTesterPrint_silk_shape_state_FIX(concatenate(varName, ".sShape"), &enc_state->sShape);
	NailTesterPrint_silk_prefilter_state_FIX(concatenate(varName, ".sPrefilt"), &enc_state->sPrefilt);
	NailTestPrintMemberVarShortArray(varName, "x_buf", enc_state->x_buf, 720);
	NailTestPrintMemberVarInt(varName, "LTPCorr_Q15", enc_state->LTPCorr_Q15);
}

//
//void NailTesterPrint_kiss_fft_state(char* varName, const kiss_fft_state* state)
//{
//	printf("%s = new kiss_fft_state();\n", varName);
//}
//
//void NailTesterPrint_mdct_lookup(char* varName, const mdct_lookup* info)
//{
//	printf("%s = new mdct_lookup();\n", varName);
//	NailTestPrintMemberVarInt(varName, "n", info->n);
//	NailTestPrintMemberVarInt(varName, "maxshift", info->maxshift);
//	NailTesterPrint_kiss_fft_state(concatenate(varName, ".kfft[0]"), info->kfft[0]);
//	NailTesterPrint_kiss_fft_state(concatenate(varName, ".kfft[1]"), info->kfft[1]);
//	NailTesterPrint_kiss_fft_state(concatenate(varName, ".kfft[2]"), info->kfft[2]);
//	NailTesterPrint_kiss_fft_state(concatenate(varName, ".kfft[3]"), info->kfft[3]);
//	NailTestPrintMemberVarShortArray(varName, "trig", info->trig, 1800);
//}
//
//void NailTestPrint_PulseCache(char* varName, const PulseCache* info)
//{
//	printf("%s = new PulseCache();\n", varName);
//	NailTestPrintMemberVarInt(varName, "size", info->size);
//	NailTestPrintMemberVarShortArray(varName, "index", info->index, 105);
//	NailTestPrintMemberVarByteArray(varName, "bits", info->bits, 392);
//	NailTestPrintMemberVarByteArray(varName, "caps", info->caps, 168);
//}
//
//void NailTesterPrint_OpusCustomMode(char* varName, const OpusCustomMode* mode)
//{
//	printf("%s = new CELTMode();\n", varName);
//	NailTestPrintMemberVarInt(varName, "Fs", mode->Fs);
//	NailTestPrintMemberVarInt(varName, "overlap", mode->overlap);
//	NailTestPrintMemberVarInt(varName, "nbEBands", mode->nbEBands);
//	NailTestPrintMemberVarInt(varName, "effEBands", mode->effEBands);
//	NailTestPrintMemberVarShortArray(varName, "preemph", mode->preemph, 4);
//	NailTestPrintMemberVarShortArray(varName, "eBands", mode->eBands, 22);
//	NailTestPrintMemberVarInt(varName, "maxLM", mode->maxLM);
//	NailTestPrintMemberVarInt(varName, "nbShortMdcts", mode->nbShortMdcts);
//	NailTestPrintMemberVarInt(varName, "shortMdctSize", mode->shortMdctSize);
//	NailTestPrintMemberVarInt(varName, "nbAllocVectors", mode->nbAllocVectors);
//	NailTestPrintMemberVarByteArray(varName, "allocVectors", mode->allocVectors, mode->nbAllocVectors);
//	NailTestPrintMemberVarShortArray(varName, "logN", mode->logN, 21);
//	NailTestPrintMemberVarShortArray(varName, "window", mode->window, 120);
//	NailTesterPrint_mdct_lookup(concatenate(varName, ".mdct"), mode->mdct);
//	NailTesterPrint_PulseCache(concatenate(varName, ".cache"), mode->cache);
//}

void NailTestPrint_AnalysisInfo(char* varName, const AnalysisInfo* info)
{
	if (!info)
	{
		printf("%s = null;\n", varName);
		return;
	}

	printf("%s = new AnalysisInfo();\n", varName);
	NailTestPrintMemberVarInt(varName, "valid", info->valid);
	NailTestPrintMemberVarFloat(varName, "tonality", info->tonality);
	NailTestPrintMemberVarFloat(varName, "tonality_slope", info->tonality_slope);
	NailTestPrintMemberVarFloat(varName, "noisiness", info->noisiness);
	NailTestPrintMemberVarFloat(varName, "activity", info->activity);
	NailTestPrintMemberVarFloat(varName, "music_prob", info->music_prob);
	NailTestPrintMemberVarInt(varName, "bandwidth", info->bandwidth);
}



#endif