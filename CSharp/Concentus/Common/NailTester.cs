using Concentus.Common.CPlusPlus;
using Concentus.Silk;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common
{
    public static class NailTester
    {
        private static StringBuilder currentLine = new StringBuilder();

        public static void printf(string format, params object[] arguments)
        {
            printf(string.Format(format, arguments));
        }

        public static void printf(string message)
        {
            string[] parts = message.Split('\n');
            currentLine.Append(parts[0]);

            if (parts.Length > 1)
            {
                for (int part = 1; part < parts.Length; part++)
                {
                    Debug.WriteLine(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(parts[part]);
                }
            }
        }

        public static string concatenate(string a, string b)
        {
            return a + b;
        }

        public static void NailTestPrintFloatArray(Pointer<float> array, int length)
        {

            printf("Helpers.ConvertBytesToFloatArray(new uint[] {\n");
            int col = 0;
            try
            {
                for (int c = 0; c < length; c++)
                {
                    uint testhex = BitConverter.ToUInt32(BitConverter.GetBytes(array[c]), 0);

                    printf("0x{0:x}U", testhex);
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
            }
            catch (IndexOutOfRangeException e) { }
            
            printf("})");
        }

        public static void NailTestPrintIntArray(Pointer<int> array, int length)
        {
            printf("new int[] { ");
            int col = 0;
            try
            {
                for (int c = 0; c < length; c++)
                {
                    printf("{0}", array[c]);
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
            }
            catch (IndexOutOfRangeException e) { }

            printf("}");
        }

        public static void NailTestPrintUintArray(Pointer<uint> array, int length)
        {
            printf("new uint[] { ");
            int col = 0;
            try
            {
                for (int c = 0; c < length; c++)
                {
                    printf("{0}U", array[c]);
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
            }
            catch (IndexOutOfRangeException e) { }

            printf("}");
        }

        public static void NailTestPrintShortArray(Pointer<short> array, int length)
        {
            printf("new short[] { ");
            int col = 0;
            try
            {
                for (int c = 0; c < length; c++)
                {
                    printf("{0}", array[c]);
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
            }
            catch (IndexOutOfRangeException e) { }

            printf("}");
        }

        public static void NailTestPrintShortArrayAsInt(Pointer<short> array, int length)
        {
            printf("new int[] { ");
            int col = 0;
            try
            {
                for (int c = 0; c < length; c++)
                {
                    printf("{0}", array[c]);
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
            }
            catch (IndexOutOfRangeException e) { }

            printf("}");
        }

        public static void NailTestPrintByteArray(Pointer<byte> array, int length)
        {
            printf("new byte[] { ");
            int col = 0;
            try
            {
                for (int c = 0; c < length; c++)
                {
                    printf("{0}", array[c]);
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
            }
            catch (IndexOutOfRangeException e) { }

            printf("}");
        }

        public static void NailTestPrintSbyteArray(Pointer<sbyte> array, int length)
        {
            printf("new sbyte[] { ");
            int col = 0;
            try
            {
                for (int c = 0; c < length; c++)
                {
                    printf("{0}", array[c]);
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
            }
            catch (IndexOutOfRangeException e) { }

            printf("}");
        }

        public static void NailTestPrintInt(string varName, int var)
        {
            printf("{0} = {1}", varName, var);
        }

        public static void NailTestPrintUint(string varName, uint var)
        {
            printf("{0} = 0x{1:x}U", varName, var);
        }

        public static void NailTestPrintShort(string varName, short var)
        {
            printf("{0} = {1}", varName, var);
        }

        public static void NailTestPrintSbyte(string varName, int var)
        {
            printf("{0} = {1}", varName, var);
        }

        public static void NailTestPrintFloat(string varName, float var)
        {
            printf("{0} = BitConverter.ToSingle(BitConverter.GetBytes((uint)0x{1:x}U), 0)", varName, BitConverter.ToUInt32(BitConverter.GetBytes(var), 0));
        }

        private static int TestNumCounter = 0;

        public static void NailTestPrintTestHeader(string methodName)
        {
            printf("[TestMethod]\npublic public static void Test_{0}_{1}()\n{\n", methodName, TestNumCounter++);
        }

        public static void NailTestPrintTestFooter()
        {
            printf("}\n\n");
        }

        public static void NailTestPrintInputFloatArrayDeclaration(string varName, Pointer<float> array, int length)
        {
            printf("Pointer<float> in_{0} = Helpers.WrapWithArrayPointer<float>(\n", varName);
            NailTestPrintFloatArray(array, length);
            printf(");\n");
        }

        public static void NailTestPrintInputIntArrayDeclaration(string varName, Pointer<int> array, int length)
        {
            printf("Pointer<int> in_{0} = Helpers.WrapWithArrayPointer<int>(\n", varName);
            NailTestPrintIntArray(array, length);
            printf(");\n");
        }

        public static void NailTestPrintInputShortArrayDeclaration(string varName, Pointer<short> array, int length)
        {
            printf("Pointer<short> in_{0} = Helpers.WrapWithArrayPointer<short>(\n", varName);
            NailTestPrintShortArray(array, length);
            printf(");\n");
        }

        public static void NailTestPrintInputSbyteArrayDeclaration(string varName, Pointer<sbyte> array, int length)
        {
            printf("Pointer<sbyte> in_{0} = Helpers.WrapWithArrayPointer<sbyte>(\n", varName);
            NailTestPrintSbyteArray(array, length);
            printf(");\n");
        }

        public static void NailTestPrintInputByteArrayDeclaration(string varName, Pointer<byte> array, int length)
        {
            printf("Pointer<byte> in_{0} = Helpers.WrapWithArrayPointer<byte>(\n", varName);
            NailTestPrintByteArray(array, length);
            printf(");\n");
        }

        public static void NailTestPrintInputIntDeclaration(string varName, int var)
        {
            printf("int in_");
            NailTestPrintInt(varName, var);
            printf(";\n");
        }

        public static void NailTestPrintInputUintDeclaration(string varName, uint var)
        {
            printf("uint in_");
            NailTestPrintUint(varName, var);
            printf(";\n");
        }

        public static void NailTestPrintInputSbyteDeclaration(string varName, sbyte var)
        {
            printf("sbyte in_");
            NailTestPrintSbyte(varName, var);
            printf(";\n");
        }

        public static void NailTestPrintInputFloatDeclaration(string varName, float var)
        {
            printf("float in_");
            NailTestPrintFloat(varName, var);
            printf(";\n");
        }

        public static void NailTestPrintOutputFloatArrayDeclaration(string varName, Pointer<float> array, int length)
        {
            printf("float[] expected_{0} = \n", varName);
            NailTestPrintFloatArray(array, length);
            printf(";\n");
        }

        public static void NailTestPrintOutputIntArrayDeclaration(string varName, Pointer<int> array, int length)
        {
            printf("int[] expected_{0} = \n", varName);
            NailTestPrintIntArray(array, length);
            printf(";\n");
        }

        public static void NailTestPrintOutputSbyteArrayDeclaration(string varName, Pointer<sbyte> array, int length)
        {
            printf("sbyte[] expected_{0} = \n", varName);
            NailTestPrintSbyteArray(array, length);
            printf(";\n");
        }

        public static void NailTestPrintOutputByteArrayDeclaration(string varName, Pointer<byte> array, int length)
        {
            printf("byte[] expected_{0} = \n", varName);
            if (array == null)
            {
                printf("new byte[0];\n");
                return;
            }
            NailTestPrintByteArray(array, length);
            printf(";\n");
        }

        public static void NailTestPrintOutputIntDeclaration(string varName, int var)
        {
            printf("int expected_");
            NailTestPrintInt(varName, var);
            printf(";\n");
        }

        public static void NailTestPrintOutputUintDeclaration(string varName, uint var)
        {
            printf("uint expected_");
            NailTestPrintUint(varName, var);
            printf(";\n");
        }

        public static void NailTestPrintOutputShortArrayDeclaration(string varName, Pointer<short> array, int length)
        {
            printf("short[] expected_{0} = \n", varName);
            NailTestPrintShortArray(array, length);
            printf(";\n");
        }

        public static void NailTestPrintOutputShortDeclaration(string varName, short var)
        {
            printf("short expected_");
            NailTestPrintShort(varName, var);
            printf(";\n");
        }

        public static void NailTestPrintOutputByteDeclaration(string varName, byte var)
        {
            printf("byte expected_");
            NailTestPrintInt(varName, var);
            printf(";\n");
        }

        public static void NailTestPrintOutputSbyteDeclaration(string varName, sbyte var)
        {
            printf("sbyte expected_");
            NailTestPrintInt(varName, var);
            printf(";\n");
        }

        public static void NailTestPrintOutputFloatDeclaration(string varName, float var)
        {
            printf("float expected_");
            NailTestPrintFloat(varName, var);
            printf(";\n");
        }

        public static void NailTestPrintMemberVarInt(string structName, string varName, int value)
        {
            printf("{0}.", structName);
            NailTestPrintInt(varName, value);
            printf(";\n");
        }

        public static void NailTestPrintMemberVarUint(string structName, string varName, uint value)
        {
            printf("{0}.", structName);
            NailTestPrintUint(varName, value);
            printf(";\n");
        }

        public static void NailTestPrintMemberVarFloat(string structName, string varName, float value)
        {
            printf("{0}.", structName);
            NailTestPrintFloat(varName, value);
            printf(";\n");
        }

        public static void NailTestPrintMemberVarShort(string structName, string varName, short value)
        {
            printf("{0}.", structName);
            NailTestPrintShort(varName, value);
            printf(";\n");
        }

        public static void NailTestPrintMemberVarSbyte(string structName, string varName, sbyte value)
        {
            printf("{0}.", structName);
            NailTestPrintSbyte(varName, value);
            printf(";\n");
        }

        public static void NailTestPrintMemberVarIntArray(string structName, string varName, Pointer<int> value, int length)
        {
            if (value == null)
            {
                printf("{0}.{1} = null;\n", structName, varName);
                return;
            }
            printf("{0}.{1} = new Pointer<int>(", structName, varName);
            NailTestPrintIntArray(value, length);
            printf(");\n");
        }

        public static void NailTestPrintMemberVarShortArray(string structName, string varName, Pointer<short> value, int length)
        {
            if (value == null)
            {
                printf("{0}.{1} = null;\n", structName, varName);
                return;
            }
            printf("{0}.{1} = new Pointer<short>(", structName, varName);
            NailTestPrintShortArray(value, length);
            printf(");\n");
        }

        public static void NailTestPrintMemberVarShortArrayAsInt(string structName, string varName, Pointer<short> value, int length)
        {
            if (value == null)
            {
                printf("{0}.{1} = null;\n", structName, varName);
                return;
            }
            printf("{0}.{1} = new Pointer<int>(", structName, varName);
            NailTestPrintShortArrayAsInt(value, length);
            printf(");\n");
        }

        public static void NailTestPrintMemberVarByteArray(string structName, string varName, Pointer<byte> value, int length)
        {
            if (value == null)
            {
                printf("{0}.{1} = null;\n", structName, varName);
                return;
            }
            printf("{0}.{1} = new Pointer<byte>(", structName, varName);
            NailTestPrintByteArray(value, length);
            printf(");\n");
        }

        public static void NailTestPrintMemberVarSbyteArray(string structName, string varName, Pointer<sbyte> value, int length)
        {
            if (value == null)
            {
                printf("{0}.{1} = null;\n", structName, varName);
                return;
            }
            printf("{0}.{1} = new Pointer<sbyte>(", structName, varName);
            NailTestPrintSbyteArray(value, length);
            printf(");\n");
        }

        public static void NailTestPrintMemberVarFloatArray(string structName, string varName, Pointer<float> value, int length)
        {
            if (value == null)
            {
                printf("{0}.{1} = null;\n", structName, varName);
                return;
            }
            printf("{0}.{1} = new Pointer<float>(", structName, varName);
            NailTestPrintFloatArray(value, length);
            printf(");\n");
        }

        // PRINT silk_nsq_state
        public static void NailTesterPrint_silk_nsq_state(string varName, silk_nsq_state nsq_state)
        {

            printf("{0} = new silk_nsq_state();\n", varName);

            printf("{0}.xq = new Pointer<short>(", varName);

            NailTestPrintShortArray(nsq_state.xq, 640);

            printf(");\n");

            printf("{0}.sLTP_shp_Q14 = new Pointer<int>(", varName);

            NailTestPrintIntArray(nsq_state.sLTP_shp_Q14, 640);

            printf(");\n");

            printf("{0}.sLPC_Q14 = new Pointer<int>(", varName);

            NailTestPrintIntArray(nsq_state.sLPC_Q14, 112);

            printf(");\n");

            printf("{0}.sAR2_Q14 = new Pointer<int>(", varName);

            NailTestPrintIntArray(nsq_state.sAR2_Q14, 16);

            printf(");\n");

            printf("{0}.", varName);

            NailTestPrintInt("sLF_AR_shp_Q14", nsq_state.sLF_AR_shp_Q14);

            printf(";\n");

            printf("{0}.", varName);

            NailTestPrintInt("lagPrev", nsq_state.lagPrev);

            printf(";\n");

            printf("{0}.", varName);

            NailTestPrintInt("sLTP_buf_idx", nsq_state.sLTP_buf_idx);

            printf(";\n");

            printf("{0}.", varName);

            NailTestPrintInt("sLTP_shp_buf_idx", nsq_state.sLTP_shp_buf_idx);

            printf(";\n");

            printf("{0}.", varName);

            NailTestPrintInt("rand_seed", nsq_state.rand_seed);

            printf(";\n");

            printf("{0}.", varName);

            NailTestPrintInt("prev_gain_Q16", nsq_state.prev_gain_Q16);

            printf(";\n");

            printf("{0}.", varName);

            NailTestPrintInt("rewhite_flag", nsq_state.rewhite_flag);

            printf(";\n");
        }

        // PRINT silk_VAD_state
        public static void NailTesterPrint_silk_VAD_state(string varName, silk_VAD_state vad_state)
        {
            printf("{0} = new silk_VAD_state();\n", varName);
            printf("{0}.AnaState = new Pointer<int>(", varName);
            NailTestPrintIntArray(vad_state.AnaState, 2);
            printf(");\n");
            printf("{0}.AnaState1 = new Pointer<int>(", varName);
            NailTestPrintIntArray(vad_state.AnaState1, 2);
            printf(");\n");
            printf("{0}.AnaState2 = new Pointer<int>(", varName);
            NailTestPrintIntArray(vad_state.AnaState2, 2);
            printf(");\n");
            printf("{0}.XnrgSubfr = new Pointer<int>(", varName);
            NailTestPrintIntArray(vad_state.XnrgSubfr, 4);
            printf(");\n");
            printf("{0}.NrgRatioSmth_Q8 = new Pointer<int>(", varName);
            NailTestPrintIntArray(vad_state.NrgRatioSmth_Q8, 4);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintShort("HPstate", vad_state.HPstate);
            printf(";\n");
            printf("{0}.NL = new Pointer<int>(", varName);
            NailTestPrintIntArray(vad_state.NL, 4);
            printf(");\n");
            printf("{0}.inv_NL = new Pointer<int>(", varName);
            NailTestPrintIntArray(vad_state.inv_NL, 4);
            printf(");\n");
            printf("{0}.NoiseLevelBias = new Pointer<int>(", varName);
            NailTestPrintIntArray(vad_state.NoiseLevelBias, 4);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintInt("counter", vad_state.counter);
            printf(";\n");
        }

        // PRINT silk_LP_state
        public static void NailTesterPrint_silk_LP_state(string varName, silk_LP_state state)
        {
            printf("{0} = new silk_LP_state();\n", varName);
            printf("{0}.In_LP_State = new Pointer<int>(", varName);
            NailTestPrintIntArray(state.In_LP_State, 2);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintInt("transition_frame_no", state.transition_frame_no);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("mode", state.mode);
            printf(";\n");
        }

        // PRINT silk_NLSF_CB_struct
        public static void NailTesterPrint_silk_NLSF_CB_struct(string varName, silk_NLSF_CB_struct state, int arraySize)
        {
            printf("{0} = new silk_NLSF_CB_struct();\n", varName);
            printf("{0}.", varName);
            NailTestPrintShort("nVectors", state.nVectors);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintShort("order", state.order);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintShort("quantStepSize_Q16", state.quantStepSize_Q16);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintShort("invQuantStepSize_Q6", state.invQuantStepSize_Q6);
            printf(";\n");
            printf("{0}.CB1_NLSF_Q8 = new Pointer<byte>(", varName);
            NailTestPrintByteArray(state.CB1_NLSF_Q8, 16); // FIXME what are the sizes here?
            printf(");\n");
            printf("{0}.CB1_iCDF = new Pointer<byte>(", varName);
            NailTestPrintByteArray(state.CB1_iCDF, 16);
            printf(");\n");
            printf("{0}.pred_Q8 = new Pointer<byte>(", varName);
            NailTestPrintByteArray(state.pred_Q8, 16);
            printf(");\n");
            printf("{0}.ec_sel = new Pointer<byte>(", varName);
            NailTestPrintByteArray(state.ec_sel, 16);
            printf(");\n");
            printf("{0}.ec_iCDF = new Pointer<byte>(", varName);
            NailTestPrintByteArray(state.ec_iCDF, 16);
            printf(");\n");
            printf("{0}.ec_Rates_Q5 = new Pointer<byte>(", varName);
            NailTestPrintByteArray(state.ec_Rates_Q5, 16);
            printf(");\n");
            printf("{0}.deltaMin_Q15 = new Pointer<short>(", varName);
            NailTestPrintShortArray(state.deltaMin_Q15, 16);
            printf(");\n");
        }

        // PRINT SideInfoIndices
        public static void NailTesterPrint_SideInfoIndices(string varName, SideInfoIndices indices)
        {
            printf("{0} = new SideInfoIndices();\n", varName);
            printf("{0}.GainsIndices = new Pointer<sbyte>(", varName);
            NailTestPrintSbyteArray(indices.GainsIndices, 4);
            printf(");\n");
            printf("{0}.LTPIndex = new Pointer<sbyte>(", varName);
            NailTestPrintSbyteArray(indices.LTPIndex, 4);
            printf(");\n");
            printf("{0}.NLSFIndices = new Pointer<sbyte>(", varName);
            NailTestPrintSbyteArray(indices.NLSFIndices, 17);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintShort("lagIndex", indices.lagIndex);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintSbyte("contourIndex", indices.contourIndex);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintSbyte("signalType", indices.signalType);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintSbyte("quantOffsetType", indices.quantOffsetType);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintSbyte("NLSFInterpCoef_Q2", indices.NLSFInterpCoef_Q2);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintSbyte("PERIndex", indices.PERIndex);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintSbyte("LTP_scaleIndex", indices.LTP_scaleIndex);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintSbyte("Seed", indices.Seed);
            printf(";\n");
        }

        // PRINT stereo_enc_state
        public static void NailTesterPrint_stereo_enc_state(string varName, stereo_enc_state state)
        {
            printf("{0} = new stereo_enc_state();\n", varName);
            printf("{0}.pred_prev_Q13 = new Pointer<short>(", varName);
            NailTestPrintShortArray(state.pred_prev_Q13, 2);
            printf(");\n");
            printf("{0}.sMid = new Pointer<short>(", varName);
            NailTestPrintShortArray(state.sMid, 2);
            printf(");\n");
            printf("{0}.sSide = new Pointer<short>(", varName);
            NailTestPrintShortArray(state.sSide, 2);
            printf(");\n");
            printf("{0}.mid_side_amp_Q0 = new Pointer<int>(", varName);
            NailTestPrintIntArray(state.mid_side_amp_Q0, 4);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintShort("smth_width_Q14", state.smth_width_Q14);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintShort("width_prev_Q14", state.width_prev_Q14);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintShort("silent_side_len", state.silent_side_len);
            printf(";\n");

            printf("{0}.predIx = Pointer.Malloc<Pointer<Pointer<sbyte>>>(SilkConstants.MAX_FRAMES_PER_PACKET);\n", varName);
            for (int x = 0; x < SilkConstants.MAX_FRAMES_PER_PACKET; x++)
            {
                printf("{0}.predIx[%d] = Pointer.Malloc<Pointer<sbyte>>(2);\n", varName, x);
                for (int y = 0; y < 2; y++)
                {
                    printf("{0}.predIx[%d][%d] = new Pointer<sbyte>(", varName, x, y);
                    NailTestPrintSbyteArray(state.predIx[x][y], 3);
                    printf(");\n");
                }
            }

            printf("{0}.mid_only_flags = new Pointer<sbyte>(", varName);
            NailTestPrintSbyteArray(state.mid_only_flags, 3);
            printf(");\n");
        }

        // PRINT stereo_dec_state
        public static void NailTesterPrint_stereo_dec_state(string varName, stereo_dec_state state)
        {
            printf("{0} = new stereo_dec_state();\n", varName);
            printf("{0}.pred_prev_Q13 = new Pointer<short>(", varName);
            NailTestPrintShortArray(state.pred_prev_Q13, 2);
            printf(");\n");
            printf("{0}.sMid = new Pointer<short>(", varName);
            NailTestPrintShortArray(state.sMid, 2);
            printf(");\n");
            printf("{0}.sSide = new Pointer<short>(", varName);
            NailTestPrintShortArray(state.sSide, 2);
            printf(");\n");
        }

        // PRINT silk_PLC_struct
        public static void NailTesterPrint_silk_PLC_struct(string varName, silk_PLC_struct state)
        {
            printf("{0} = new silk_PLC_struct();\n", varName);
            printf("{0}.", varName);
            NailTestPrintInt("pitchL_Q8", state.pitchL_Q8);
            printf(";\n");
            printf("{0}.LTPCoef_Q14 = new Pointer<short>(", varName);
            NailTestPrintShortArray(state.LTPCoef_Q14, 5);
            printf(");\n");
            printf("{0}.prevLPC_Q12 = new Pointer<short>(", varName);
            NailTestPrintShortArray(state.prevLPC_Q12, 16);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintInt("last_frame_lost", state.last_frame_lost);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("rand_seed", state.rand_seed);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintShort("randScale_Q14", state.randScale_Q14);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("conc_energy", state.conc_energy);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("conc_energy_shift", state.conc_energy_shift);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintShort("prevLTP_scale_Q14", state.prevLTP_scale_Q14);
            printf(";\n");
            printf("{0}.prevGain_Q16 = new Pointer<int>(", varName);
            NailTestPrintIntArray(state.prevGain_Q16, 2);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintInt("fs_kHz", state.fs_kHz);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("nb_subfr", state.nb_subfr);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("subfr_length", state.subfr_length);
            printf(";\n");
        }

        // PRINT silksilk_CNG_struct_PLC_struct
        public static void NailTesterPrint_silk_CNG_struct(string varName, silk_CNG_struct state)
        {
            printf("{0} = new silk_CNG_struct();\n", varName);
            printf("{0}.CNG_exc_buf_Q14 = new Pointer<int>(", varName);
            NailTestPrintIntArray(state.CNG_exc_buf_Q14, 320);
            printf(");\n");
            printf("{0}.CNG_smth_NLSF_Q15 = new Pointer<short>(", varName);
            NailTestPrintShortArray(state.CNG_smth_NLSF_Q15, 16);
            printf(");\n");
            printf("{0}.CNG_synth_state = new Pointer<int>(", varName);
            NailTestPrintIntArray(state.CNG_synth_state, 16);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintInt("CNG_smth_Gain_Q16", state.CNG_smth_Gain_Q16);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("rand_seed", state.rand_seed);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("fs_kHz", state.fs_kHz);
            printf(";\n");
        }

        // PRINT ec_ctx
        public static void NailTesterPrint_ec_ctx(string varName, ec_ctx coder)
        {
            if (coder == null)
            {
                printf("{0} = null;\n", varName);
                return;
            }

            printf("{0} = new ec_ctx();\n", varName);
            NailTestPrintMemberVarByteArray(varName, "buf", coder.buf, (int)coder.storage);
            printf("{0}.", varName);
            NailTestPrintUint("storage", coder.storage);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintUint("end_offs", coder.end_offs);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintUint("end_window", coder.end_window);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("nend_bits", coder.nend_bits);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("nbits_total", coder.nbits_total);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintUint("offs", coder.offs);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintUint("rng", coder.rng);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintUint("val", coder.val);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintUint("ext", coder.ext);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("rem", coder.rem);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("error", coder.error);
            printf(";\n");
        }

        // PRINT silk_encoder_control
        public static void NailTesterPrint_silk_encoder_control(string varName, silk_encoder_control enc_control)
        {
            printf("{0} = new silk_encoder_control();\n", varName);
            printf("{0}.Gains_Q16 = new Pointer<int>(", varName);
            NailTestPrintIntArray(enc_control.Gains_Q16, 4);
            printf(");\n");
            printf("{0}.PredCoef_Q12 = new Pointer<short>(", varName);
            NailTestPrintShortArray(enc_control.PredCoef_Q12, 32);
            printf(");\n");
            printf("{0}.LTPCoef_Q14 = new Pointer<short>(", varName);
            NailTestPrintShortArray(enc_control.LTPCoef_Q14, 20);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintInt("LTP_scale_Q14", enc_control.LTP_scale_Q14);
            printf(";\n");
            printf("{0}.pitchL = new Pointer<int>(", varName);
            NailTestPrintIntArray(enc_control.pitchL, 4);
            printf(");\n");
            printf("{0}.AR1_Q13 = new Pointer<short>(", varName);
            NailTestPrintShortArray(enc_control.AR1_Q13, 64);
            printf(");\n");
            printf("{0}.AR2_Q13 = new Pointer<short>(", varName);
            NailTestPrintShortArray(enc_control.AR2_Q13, 64);
            printf(");\n");
            printf("{0}.LF_shp_Q14 = new Pointer<int>(", varName);
            NailTestPrintIntArray(enc_control.LF_shp_Q14, 4);
            printf(");\n");
            printf("{0}.GainsPre_Q14 = new Pointer<int>(", varName);
            NailTestPrintIntArray(enc_control.GainsPre_Q14, 4);
            printf(");\n");
            printf("{0}.HarmBoost_Q14 = new Pointer<int>(", varName);
            NailTestPrintIntArray(enc_control.HarmBoost_Q14, 4);
            printf(");\n");
            printf("{0}.Tilt_Q14 = new Pointer<int>(", varName);
            NailTestPrintIntArray(enc_control.Tilt_Q14, 4);
            printf(");\n");
            printf("{0}.HarmShapeGain_Q14 = new Pointer<int>(", varName);
            NailTestPrintIntArray(enc_control.HarmShapeGain_Q14, 4);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintInt("Lambda_Q10", enc_control.Lambda_Q10);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("input_quality_Q14", enc_control.input_quality_Q14);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("coding_quality_Q14", enc_control.coding_quality_Q14);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("sparseness_Q8", enc_control.sparseness_Q8);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("predGain_Q16", enc_control.predGain_Q16);
            printf(";\n");
            printf("{0}.", varName);
            NailTestPrintInt("LTPredCodGain_Q7", enc_control.LTPredCodGain_Q7);
            printf(";\n");
            printf("{0}.ResNrg = new Pointer<int>(", varName);
            NailTestPrintIntArray(enc_control.ResNrg, 4);
            printf(");\n");
            printf("{0}.ResNrgQ = new Pointer<int>(", varName);
            NailTestPrintIntArray(enc_control.ResNrgQ, 4);
            printf(");\n");
            printf("{0}.GainsUnq_Q16 = new Pointer<int>(", varName);
            NailTestPrintIntArray(enc_control.GainsUnq_Q16, 4);
            printf(");\n");
            printf("{0}.", varName);
            NailTestPrintSbyte("lastGainIndexPrev", enc_control.lastGainIndexPrev);
            printf(";\n");
        }

        // PRINT silk_encoder_state
        public static void NailTesterPrint_silk_encoder_state(string varName, silk_encoder_state state)
        {
            printf("{0} = new silk_encoder_state();\n", varName);
            NailTestPrintMemberVarIntArray(varName, "In_HP_State", state.In_HP_State, 2);
            NailTestPrintMemberVarInt(varName, "variable_HP_smth1_Q15", state.variable_HP_smth1_Q15);
            NailTestPrintMemberVarInt(varName, "variable_HP_smth2_Q15", state.variable_HP_smth2_Q15);
            NailTesterPrint_silk_LP_state(concatenate(varName, ".sLP"), state.sLP);
            NailTesterPrint_silk_VAD_state(concatenate(varName, ".sVAD"), state.sVAD);
            NailTesterPrint_silk_nsq_state(concatenate(varName, ".sNSQ"), state.sNSQ);
            NailTestPrintMemberVarShortArray(varName, "prev_NLSFq_Q15", state.prev_NLSFq_Q15, SilkConstants.MAX_LPC_ORDER);
            NailTestPrintMemberVarInt(varName, "speech_activity_Q8", state.speech_activity_Q8);
            NailTestPrintMemberVarInt(varName, "allow_bandwidth_switch", state.allow_bandwidth_switch);
            NailTestPrintMemberVarSbyte(varName, "LBRRprevLastGainIndex", state.LBRRprevLastGainIndex);
            NailTestPrintMemberVarSbyte(varName, "prevSignalType", state.prevSignalType);
            NailTestPrintMemberVarInt(varName, "prevLag", state.prevLag);
            NailTestPrintMemberVarInt(varName, "pitch_LPC_win_length", state.pitch_LPC_win_length);
            NailTestPrintMemberVarInt(varName, "max_pitch_lag", state.max_pitch_lag);
            NailTestPrintMemberVarInt(varName, "API_fs_Hz", state.API_fs_Hz);
            NailTestPrintMemberVarInt(varName, "prev_API_fs_Hz", state.prev_API_fs_Hz);
            NailTestPrintMemberVarInt(varName, "maxInternal_fs_Hz", state.maxInternal_fs_Hz);
            NailTestPrintMemberVarInt(varName, "minInternal_fs_Hz", state.minInternal_fs_Hz);
            NailTestPrintMemberVarInt(varName, "desiredInternal_fs_Hz", state.desiredInternal_fs_Hz);
            NailTestPrintMemberVarInt(varName, "fs_kHz", state.fs_kHz);
            NailTestPrintMemberVarInt(varName, "nb_subfr", state.nb_subfr);
            NailTestPrintMemberVarInt(varName, "frame_length", state.frame_length);
            NailTestPrintMemberVarInt(varName, "subfr_length", state.subfr_length);
            NailTestPrintMemberVarInt(varName, "ltp_mem_length", state.ltp_mem_length);
            NailTestPrintMemberVarInt(varName, "la_pitch", state.la_pitch);
            NailTestPrintMemberVarInt(varName, "la_shape", state.la_shape);
            NailTestPrintMemberVarInt(varName, "shapeWinLength", state.shapeWinLength);
            NailTestPrintMemberVarInt(varName, "TargetRate_bps", state.TargetRate_bps);
            NailTestPrintMemberVarInt(varName, "PacketSize_ms", state.PacketSize_ms);
            NailTestPrintMemberVarInt(varName, "PacketLoss_perc", state.PacketLoss_perc);
            NailTestPrintMemberVarInt(varName, "frameCounter", state.frameCounter);
            NailTestPrintMemberVarInt(varName, "Complexity", state.Complexity);
            NailTestPrintMemberVarInt(varName, "nStatesDelayedDecision", state.nStatesDelayedDecision);
            NailTestPrintMemberVarInt(varName, "useInterpolatedNLSFs", state.useInterpolatedNLSFs);
            NailTestPrintMemberVarInt(varName, "shapingLPCOrder", state.shapingLPCOrder);
            NailTestPrintMemberVarInt(varName, "predictLPCOrder", state.predictLPCOrder);
            NailTestPrintMemberVarInt(varName, "pitchEstimationComplexity", state.pitchEstimationComplexity);
            NailTestPrintMemberVarInt(varName, "pitchEstimationLPCOrder", state.pitchEstimationLPCOrder);
            NailTestPrintMemberVarInt(varName, "pitchEstimationThreshold_Q16", state.pitchEstimationThreshold_Q16);
            NailTestPrintMemberVarInt(varName, "LTPQuantLowComplexity", state.LTPQuantLowComplexity);
            NailTestPrintMemberVarInt(varName, "mu_LTP_Q9", state.mu_LTP_Q9);
            NailTestPrintMemberVarInt(varName, "sum_log_gain_Q7", state.sum_log_gain_Q7);
            NailTestPrintMemberVarInt(varName, "NLSF_MSVQ_Survivors", state.NLSF_MSVQ_Survivors);
            NailTestPrintMemberVarInt(varName, "first_frame_after_reset", state.first_frame_after_reset);
            NailTestPrintMemberVarInt(varName, "controlled_since_last_payload", state.controlled_since_last_payload);
            NailTestPrintMemberVarInt(varName, "warping_Q16", state.warping_Q16);
            NailTestPrintMemberVarInt(varName, "useCBR", state.useCBR);
            NailTestPrintMemberVarInt(varName, "prefillFlag", state.prefillFlag);
            NailTestPrintMemberVarByteArray(varName, "pitch_lag_low_bits_iCDF", state.pitch_lag_low_bits_iCDF, 16);
            NailTestPrintMemberVarByteArray(varName, "pitch_contour_iCDF", state.pitch_contour_iCDF, 16);
            NailTesterPrint_silk_NLSF_CB_struct(concatenate(varName, ".psNLSF_CB"), state.psNLSF_CB, 1024);
            NailTestPrintMemberVarIntArray(varName, "input_quality_bands_Q15", state.input_quality_bands_Q15, 4);
            NailTestPrintMemberVarInt(varName, "input_tilt_Q15", state.input_tilt_Q15);
            NailTestPrintMemberVarInt(varName, "SNR_dB_Q7", state.SNR_dB_Q7);
            NailTestPrintMemberVarSbyteArray(varName, "VAD_flags", state.VAD_flags, 3);
            NailTestPrintMemberVarInt(varName, "LBRR_flag", state.LBRR_flag);
            NailTestPrintMemberVarIntArray(varName, "LBRR_flags", state.LBRR_flags, 3);
            NailTesterPrint_SideInfoIndices(concatenate(varName, ".indices"), state.indices);
            NailTestPrintMemberVarSbyteArray(varName, "pulses", state.pulses, SilkConstants.MAX_FRAME_LENGTH);
            NailTestPrintMemberVarInt(varName, "arch", state.arch);
            NailTestPrintMemberVarShortArray(varName, "inputBuf", state.inputBuf, SilkConstants.MAX_FRAME_LENGTH + 2);
            NailTestPrintMemberVarInt(varName, "inputBufIx", state.inputBufIx);
            NailTestPrintMemberVarInt(varName, "nFramesPerPacket", state.nFramesPerPacket);
            NailTestPrintMemberVarInt(varName, "nFramesEncoded", state.nFramesEncoded);
            NailTestPrintMemberVarInt(varName, "nChannelsAPI", state.nChannelsAPI);
            NailTestPrintMemberVarInt(varName, "nChannelsInternal", state.nChannelsInternal);
            NailTestPrintMemberVarInt(varName, "channelNb", state.channelNb);
            NailTestPrintMemberVarInt(varName, "frames_since_onset", state.frames_since_onset);
            NailTestPrintMemberVarInt(varName, "ec_prevSignalType", state.ec_prevSignalType);
            NailTestPrintMemberVarShort(varName, "ec_prevLagIndex", state.ec_prevLagIndex);
            NailTesterPrint_silk_resampler_state(concatenate(varName, ".resampler_state"), state.resampler_state);
            NailTestPrintMemberVarInt(varName, "useDTX", state.useDTX);
            NailTestPrintMemberVarInt(varName, "inDTX", state.inDTX);
            NailTestPrintMemberVarInt(varName, "noSpeechCounter", state.noSpeechCounter);
            NailTestPrintMemberVarInt(varName, "useInBandFEC", state.useInBandFEC);
            NailTestPrintMemberVarInt(varName, "LBRR_enabled", state.LBRR_enabled);
            NailTestPrintMemberVarInt(varName, "LBRR_GainIncreases", state.LBRR_GainIncreases);
            NailTesterPrint_SideInfoIndices(concatenate(varName, ".indices_LBRR[0]"), state.indices_LBRR[0]);
            NailTesterPrint_SideInfoIndices(concatenate(varName, ".indices_LBRR[1]"), state.indices_LBRR[1]);
            NailTesterPrint_SideInfoIndices(concatenate(varName, ".indices_LBRR[2]"), state.indices_LBRR[2]);
            NailTestPrintMemberVarSbyteArray(varName, "pulses_LBRR[0]", state.pulses_LBRR[0], 320);
            NailTestPrintMemberVarSbyteArray(varName, "pulses_LBRR[1]", state.pulses_LBRR[1], 320);
            NailTestPrintMemberVarSbyteArray(varName, "pulses_LBRR[2]", state.pulses_LBRR[2], 320);
        }

        public static void NailTesterPrint_silk_resampler_state(string varName, silk_resampler_state_struct state)
        {
            printf("{0} = new _silk_resampler_state_struct();\n", varName);
            NailTestPrintMemberVarIntArray(varName, "sIIR", state.sIIR, 6);
            NailTestPrintMemberVarIntArray(varName, "i32", state.sFIR_i32, 36);
            NailTestPrintMemberVarShortArray(varName, "i16", state.sFIR_i16, 36);
            NailTestPrintMemberVarInt(varName, "resampler_function", state.resampler_function);
            NailTestPrintMemberVarInt(varName, "batchSize", state.batchSize);
            NailTestPrintMemberVarInt(varName, "invRatio_Q16", state.invRatio_Q16);
            NailTestPrintMemberVarInt(varName, "FIR_Order", state.FIR_Order);
            NailTestPrintMemberVarInt(varName, "FIR_Fracs", state.FIR_Fracs);
            NailTestPrintMemberVarInt(varName, "Fs_in_kHz", state.Fs_in_kHz);
            NailTestPrintMemberVarInt(varName, "Fs_out_kHz", state.Fs_out_kHz);
            NailTestPrintMemberVarInt(varName, "inputDelay", state.inputDelay);
        }

        // PRINT silk_shape_state_FLP
        public static void NailTesterPrint_silk_shape_state_FIX(string varName, silk_shape_state state)
        {
            printf("{0} = new silk_shape_state();\n", varName);
            NailTestPrintMemberVarSbyte(varName, "LastGainIndex", state.LastGainIndex);
            NailTestPrintMemberVarInt(varName, "HarmBoost_smth_Q16", state.HarmBoost_smth_Q16);
            NailTestPrintMemberVarInt(varName, "HarmShapeGain_smth_Q16", state.HarmShapeGain_smth_Q16);
            NailTestPrintMemberVarInt(varName, "Tilt_smth_Q16", state.Tilt_smth_Q16);
        }

        // PRINT silk_shape_state_FLP
        public static void NailTesterPrint_silk_prefilter_state_FIX(string varName, silk_prefilter_state state)
        {
            printf("{0} = new silk_prefilter_state();\n", varName);
            NailTestPrintMemberVarShortArray(varName, "sLTP_shp", state.sLTP_shp, 512);
            NailTestPrintMemberVarIntArray(varName, "sAR_shp", state.sAR_shp, 17);
            NailTestPrintMemberVarInt(varName, "sLTP_shp_buf_idx", state.sLTP_shp_buf_idx);
            NailTestPrintMemberVarInt(varName, "sLF_AR_shp_Q12", state.sLF_AR_shp_Q12);
            NailTestPrintMemberVarInt(varName, "sLF_MA_shp_Q12", state.sLF_MA_shp_Q12);
            NailTestPrintMemberVarInt(varName, "sHarmHP_Q2", state.sHarmHP_Q2);
            NailTestPrintMemberVarInt(varName, "rand_seed", state.rand_seed);
            NailTestPrintMemberVarInt(varName, "lagPrev", state.lagPrev);
        }

        // PRINT silk_encoder_state_FLP
        public static void NailTesterPrint_silk_encoder_state_FIX(string varName, silk_encoder_state_fix enc_state)
        {
            printf("{0} = new silk_encoder_state_fix();\n", varName);
            NailTesterPrint_silk_encoder_state(concatenate(varName, ".sCmn"), enc_state.sCmn);
            NailTesterPrint_silk_shape_state_FIX(concatenate(varName, ".sShape"), enc_state.sShape);
            NailTesterPrint_silk_prefilter_state_FIX(concatenate(varName, ".sPrefilt"), enc_state.sPrefilt);
            NailTestPrintMemberVarShortArray(varName, "x_buf", enc_state.x_buf, 720);
            NailTestPrintMemberVarInt(varName, "LTPCorr_Q15", enc_state.LTPCorr_Q15);
        }
    }
}
