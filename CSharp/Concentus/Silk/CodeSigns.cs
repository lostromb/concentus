using Concentus.Common.CPlusPlus;
using Concentus.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

namespace Concentus.Silk
{
    internal static class CodeSigns
    {
        private static int silk_enc_map(int a)
        {
            return (Inlines.silk_RSHIFT((a), 15) + 1);
        }

        private static int silk_dec_map(int a)
        {
            return (Inlines.silk_LSHIFT((a), 1) - 1);
        }
        
        /// <summary>
        /// Encodes signs of excitation
        /// </summary>
        /// <param name="psRangeEnc">I/O  Compressor data structure</param>
        /// <param name="pulses">I    pulse signal</param>
        /// <param name="length">I    length of input</param>
        /// <param name="signalType">I    Signal type</param>
        /// <param name="quantOffsetType">I    Quantization offset type</param>
        /// <param name="sum_pulses">I    Sum of absolute pulses per block [MAX_NB_SHELL_BLOCKS]</param>
        internal static void silk_encode_signs(
            ec_ctx psRangeEnc,
            Pointer<sbyte> pulses,
            int length,
            int signalType,
            int quantOffsetType,
            Pointer<int> sum_pulses)
        {
            int i, j, p;
            Pointer<byte> icdf = Pointer.Malloc<byte>(2);
            Pointer<sbyte> q_ptr;
            Pointer<byte> icdf_ptr;

            icdf[1] = 0;
            q_ptr = pulses;
            i = Inlines.silk_SMULBB(7, Inlines.silk_ADD_LSHIFT(quantOffsetType, signalType, 1));
            icdf_ptr = Tables.silk_sign_iCDF.GetPointer(i);
            length = Inlines.silk_RSHIFT(length + (SilkConstants.SHELL_CODEC_FRAME_LENGTH / 2), SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH);
            for (i = 0; i < length; i++)
            {
                p = sum_pulses[i];
                if (p > 0)
                {
                    icdf[0] = icdf_ptr[Inlines.silk_min(p & 0x1F, 6)];
                    for (j = 0; j < SilkConstants.SHELL_CODEC_FRAME_LENGTH; j++)
                    {
                        if (q_ptr[j] != 0)
                        {
                            EntropyCoder.ec_enc_icdf(psRangeEnc, silk_enc_map(q_ptr[j]), icdf, 8);
                        }
                    }
                }

                q_ptr = q_ptr.Point(SilkConstants.SHELL_CODEC_FRAME_LENGTH);
            }
        }

        /// <summary>
        /// Decodes signs of excitation
        /// </summary>
        /// <param name="psRangeDec">I/O  Compressor data structure</param>
        /// <param name="pulses">I/O  pulse signal</param>
        /// <param name="length">I    length of input</param>
        /// <param name="signalType">I    Signal type</param>
        /// <param name="quantOffsetType">I    Quantization offset type</param>
        /// <param name="sum_pulses">I    Sum of absolute pulses per block [MAX_NB_SHELL_BLOCKS]</param>
        internal static void silk_decode_signs(
            ec_ctx psRangeDec,
            Pointer<short> pulses,
            int length,
            int signalType,
            int quantOffsetType,
            Pointer<int> sum_pulses)
        {
            int i, j, p;
            Pointer<byte> icdf = Pointer.Malloc<byte>(2);
            Pointer<short> q_ptr;
            Pointer<byte> icdf_ptr;

            icdf[1] = 0;
            q_ptr = pulses;
            i = Inlines.silk_SMULBB(7, Inlines.silk_ADD_LSHIFT(quantOffsetType, signalType, 1));
            icdf_ptr = new Pointer<byte>(Tables.silk_sign_iCDF, i);
            length = Inlines.silk_RSHIFT(length + SilkConstants.SHELL_CODEC_FRAME_LENGTH / 2, SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH);

            for (i = 0; i < length; i++)
            {
                p = sum_pulses[i];

                if (p > 0)
                {
                    icdf[0] = icdf_ptr[Inlines.silk_min(p & 0x1F, 6)];
                    for (j = 0; j < SilkConstants.SHELL_CODEC_FRAME_LENGTH; j++)
                    {
                        if (q_ptr[j] > 0)
                        {
                            /* attach sign */
                            q_ptr[j] *= Inlines.CHOP16(silk_dec_map(EntropyCoder.ec_dec_icdf(psRangeDec, icdf, 8)));
                        }
                    }
                }

                q_ptr = q_ptr.Point(SilkConstants.SHELL_CODEC_FRAME_LENGTH);
            }
        }
    }
}
