using Concentus.Common;
using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Silk
{
    /// <summary>
    /// shell coder; pulse-subframe length is hardcoded
    /// </summary>
    internal static class ShellCoder
    {
        /// <summary>
        /// </summary>
        /// <param name="output">O    combined pulses vector [len]</param>
        /// <param name="input">I    input vector       [2 * len]</param>
        /// <param name="len">I    number of OUTPUT samples</param>
        internal static void combine_pulses(
            Pointer<int> output,
            Pointer<int> input,
            int len)
        {
            int k;
            for (k = 0; k < len; k++)
            {
                output[k] = input[2 * k] + input[2 * k + 1];
            }
        }

        internal static void encode_split(
            ec_ctx psRangeEnc,    /* I/O  compressor data structure                   */
            int p_child1,       /* I    pulse amplitude of first child subframe     */
            int p,              /* I    pulse amplitude of current subframe         */
            Pointer<byte> shell_table    /* I    table of shell cdfs                         */
        )
        {
            if (p > 0)
            {
                EntropyCoder.ec_enc_icdf(psRangeEnc, p_child1, shell_table.Point(Tables.silk_shell_code_table_offsets[p]), 8);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p_child1">O    pulse amplitude of first child subframe</param>
        /// <param name="p_child2">O    pulse amplitude of second child subframe</param>
        /// <param name="psRangeDec">I/O  Compressor data structure</param>
        /// <param name="p">I    pulse amplitude of current subframe</param>
        /// <param name="shell_table">I    table of shell cdfs</param>
        internal static void decode_split(
            Pointer<short> p_child1,
            Pointer<short> p_child2,
            ec_ctx psRangeDec,
            int p,
            Pointer<byte> shell_table)
        {
            if (p > 0)
            {
                p_child1[0] = Inlines.CHOP16(EntropyCoder.ec_dec_icdf(psRangeDec, shell_table.Point(Tables.silk_shell_code_table_offsets[p]), 8));
                p_child2[0] = Inlines.CHOP16(p - p_child1[0]);
            }
            else
            {
                p_child1[0] = 0;
                p_child2[0] = 0;
            }
        }

        /// <summary>
        /// Shell encoder, operates on one shell code frame of 16 pulses
        /// </summary>
        /// <param name="psRangeEnc">I/O  compressor data structure</param>
        /// <param name="pulses0">I    data: nonnegative pulse amplitudes</param>
        internal static void silk_shell_encoder(ec_ctx psRangeEnc, Pointer<int> pulses0)
        {
            Pointer<int> pulses1 = Pointer.Malloc<int>(8);
            Pointer<int> pulses2 = Pointer.Malloc<int>(4);
            Pointer<int> pulses3 = Pointer.Malloc<int>(2);
            Pointer<int> pulses4 = Pointer.Malloc<int>(1);

            /* this function operates on one shell code frame of 16 pulses */
            Inlines.OpusAssert(SilkConstants.SHELL_CODEC_FRAME_LENGTH == 16);

            /* tree representation per pulse-subframe */
            combine_pulses(pulses1, pulses0, 8);
            combine_pulses(pulses2, pulses1, 4);
            combine_pulses(pulses3, pulses2, 2);
            combine_pulses(pulses4, pulses3, 1);

            encode_split(psRangeEnc, pulses3[0], pulses4[0], Tables.silk_shell_code_table3.GetPointer());

            encode_split(psRangeEnc, pulses2[0], pulses3[0], Tables.silk_shell_code_table2.GetPointer());

            encode_split(psRangeEnc, pulses1[0], pulses2[0], Tables.silk_shell_code_table1.GetPointer());
            encode_split(psRangeEnc, pulses0[0], pulses1[0], Tables.silk_shell_code_table0.GetPointer());
            encode_split(psRangeEnc, pulses0[2], pulses1[1], Tables.silk_shell_code_table0.GetPointer());

            encode_split(psRangeEnc, pulses1[2], pulses2[1], Tables.silk_shell_code_table1.GetPointer());
            encode_split(psRangeEnc, pulses0[4], pulses1[2], Tables.silk_shell_code_table0.GetPointer());
            encode_split(psRangeEnc, pulses0[6], pulses1[3], Tables.silk_shell_code_table0.GetPointer());

            encode_split(psRangeEnc, pulses2[2], pulses3[1], Tables.silk_shell_code_table2.GetPointer());

            encode_split(psRangeEnc, pulses1[4], pulses2[2], Tables.silk_shell_code_table1.GetPointer());
            encode_split(psRangeEnc, pulses0[8], pulses1[4], Tables.silk_shell_code_table0.GetPointer());
            encode_split(psRangeEnc, pulses0[10], pulses1[5], Tables.silk_shell_code_table0.GetPointer());

            encode_split(psRangeEnc, pulses1[6], pulses2[3], Tables.silk_shell_code_table1.GetPointer());
            encode_split(psRangeEnc, pulses0[12], pulses1[6], Tables.silk_shell_code_table0.GetPointer());
            encode_split(psRangeEnc, pulses0[14], pulses1[7], Tables.silk_shell_code_table0.GetPointer());
        }


        /* Shell decoder, operates on one shell code frame of 16 pulses */
        internal static void silk_shell_decoder(
            Pointer<short> pulses0,                       /* O    data: nonnegative pulse amplitudes          */
            ec_ctx psRangeDec,                    /* I/O  Compressor data structure                   */
            int pulses4                         /* I    number of pulses per pulse-subframe         */
        )
        {
            Pointer<short> pulses1 = Pointer.Malloc<short>(8);
            Pointer<short> pulses2 = Pointer.Malloc<short>(4);
            Pointer<short> pulses3 = Pointer.Malloc<short>(2);

            /* this function operates on one shell code frame of 16 pulses */
            Inlines.OpusAssert(SilkConstants.SHELL_CODEC_FRAME_LENGTH == 16);

            decode_split(pulses3.Point(0), pulses3.Point(1), psRangeDec, pulses4, Tables.silk_shell_code_table3.GetPointer());

            decode_split(pulses2.Point(0), pulses2.Point(1), psRangeDec, pulses3[0], Tables.silk_shell_code_table2.GetPointer());

            decode_split(pulses1.Point(0), pulses1.Point(1), psRangeDec, pulses2[0], Tables.silk_shell_code_table1.GetPointer());
            decode_split(pulses0.Point(0), pulses0.Point(1), psRangeDec, pulses1[0], Tables.silk_shell_code_table0.GetPointer());
            decode_split(pulses0.Point(2), pulses0.Point(3), psRangeDec, pulses1[1], Tables.silk_shell_code_table0.GetPointer());

            decode_split(pulses1.Point(2), pulses1.Point(3), psRangeDec, pulses2[1], Tables.silk_shell_code_table1.GetPointer());
            decode_split(pulses0.Point(4), pulses0.Point(5), psRangeDec, pulses1[2], Tables.silk_shell_code_table0.GetPointer());
            decode_split(pulses0.Point(6), pulses0.Point(7), psRangeDec, pulses1[3], Tables.silk_shell_code_table0.GetPointer());

            decode_split(pulses2.Point(2), pulses2.Point(3), psRangeDec, pulses3[1], Tables.silk_shell_code_table2.GetPointer());

            decode_split(pulses1.Point(4), pulses1.Point(5), psRangeDec, pulses2[2], Tables.silk_shell_code_table1.GetPointer());
            decode_split(pulses0.Point(8), pulses0.Point(9), psRangeDec, pulses1[4], Tables.silk_shell_code_table0.GetPointer());
            decode_split(pulses0.Point(10), pulses0.Point(11), psRangeDec, pulses1[5], Tables.silk_shell_code_table0.GetPointer());

            decode_split(pulses1.Point(6), pulses1.Point(7), psRangeDec, pulses2[3], Tables.silk_shell_code_table1.GetPointer());
            decode_split(pulses0.Point(12), pulses0.Point(13), psRangeDec, pulses1[6], Tables.silk_shell_code_table0.GetPointer());
            decode_split(pulses0.Point(14), pulses0.Point(15), psRangeDec, pulses1[7], Tables.silk_shell_code_table0.GetPointer());
        }
    }
}
