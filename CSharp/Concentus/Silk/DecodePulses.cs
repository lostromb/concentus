using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Silk
{
    internal static class DecodePulses
    {
        /*********************************************/
        /* Decode quantization indices of excitation */
        /*********************************************/
        internal static void silk_decode_pulses(
            EntropyCoder psRangeDec,                    /* I/O  Compressor data structure                   */
            Pointer<short> pulses,                       /* O    Excitation signal                           */
            int signalType,                     /* I    Sigtype                                     */
            int quantOffsetType,                /* I    quantOffsetType                             */
            int frame_length                    /* I    Frame length                                */
            )
        {
            int i, j, k, iter, abs_q, nLS, RateLevelIndex;
            Pointer<int> sum_pulses = Pointer.Malloc<int>(SilkConstants.MAX_NB_SHELL_BLOCKS);
            Pointer<int> nLshifts = Pointer.Malloc<int>(SilkConstants.MAX_NB_SHELL_BLOCKS);
            Pointer<short> pulses_ptr;
            Pointer<byte> cdf_ptr;

            /*********************/
            /* Decode rate level */
            /*********************/
            RateLevelIndex = psRangeDec.ec_dec_icdf(Tables.silk_rate_levels_iCDF[signalType >> 1].GetPointer(), 8);

            /* Calculate number of shell blocks */
            Inlines.OpusAssert(1 << SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH == SilkConstants.SHELL_CODEC_FRAME_LENGTH);
            iter = Inlines.silk_RSHIFT(frame_length, SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH);
            if (iter * SilkConstants.SHELL_CODEC_FRAME_LENGTH < frame_length)
            {
                Inlines.OpusAssert(frame_length == 12 * 10); /* Make sure only happens for 10 ms @ 12 kHz */
                iter++;
            }

            /***************************************************/
            /* Sum-Weighted-Pulses Decoding                    */
            /***************************************************/
            cdf_ptr = Tables.silk_pulses_per_block_iCDF[RateLevelIndex].GetPointer();
            for (i = 0; i < iter; i++)
            {
                nLshifts[i] = 0;
                sum_pulses[i] = psRangeDec.ec_dec_icdf(cdf_ptr, 8);

                /* LSB indication */
                while (sum_pulses[i] == SilkConstants.SILK_MAX_PULSES + 1)
                {
                    nLshifts[i]++;
                    /* When we've already got 10 LSBs, we shift the table to not allow (SILK_MAX_PULSES + 1) */
                    sum_pulses[i] = psRangeDec.ec_dec_icdf(
                          Tables.silk_pulses_per_block_iCDF[SilkConstants.N_RATE_LEVELS - 1].GetPointer(nLshifts[i] == 10 ? 1 : 0), 8);
                }
            }

            /***************************************************/
            /* Shell decoding                                  */
            /***************************************************/
            for (i = 0; i < iter; i++)
            {
                if (sum_pulses[i] > 0)
                {
                    ShellCoder.silk_shell_decoder(pulses.Point(Inlines.silk_SMULBB(i, SilkConstants.SHELL_CODEC_FRAME_LENGTH)), psRangeDec, sum_pulses[i]);
                }
                else
                {
                    pulses.Point(Inlines.silk_SMULBB(i, SilkConstants.SHELL_CODEC_FRAME_LENGTH)).MemSet(0, SilkConstants.SHELL_CODEC_FRAME_LENGTH);
                }
            }

            /***************************************************/
            /* LSB Decoding                                    */
            /***************************************************/
            for (i = 0; i < iter; i++)
            {
                if (nLshifts[i] > 0)
                {
                    nLS = nLshifts[i];
                    pulses_ptr = pulses.Point(Inlines.silk_SMULBB(i, SilkConstants.SHELL_CODEC_FRAME_LENGTH));
                    for (k = 0; k < SilkConstants.SHELL_CODEC_FRAME_LENGTH; k++)
                    {
                        abs_q = pulses_ptr[k];
                        for (j = 0; j < nLS; j++)
                        {
                            abs_q = Inlines.silk_LSHIFT(abs_q, 1);
                            abs_q += psRangeDec.ec_dec_icdf(Tables.silk_lsb_iCDF.GetPointer(), 8);
                        }
                        pulses_ptr[k] = Inlines.CHOP16(abs_q);
                    }
                    /* Mark the number of pulses non-zero for sign decoding. */
                    sum_pulses[i] |= nLS << 5;
                }
            }

            /****************************************/
            /* Decode and add signs to pulse signal */
            /****************************************/
            CodeSigns.silk_decode_signs(psRangeDec, pulses, frame_length, signalType, quantOffsetType, sum_pulses);
        }
    }
}
