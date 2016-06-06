using Concentus.Common.CPlusPlus;
using Concentus.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Silk
{
    public static class encode_pulses
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pulses_comb">(O)</param>
        /// <param name="pulses_in">(I)</param>
        /// <param name="max_pulses"> I    max value for sum of pulses</param>
        /// <param name="len">I    number of output values</param>
        /// <returns>return ok</returns>
        public static int combine_and_check(
            Pointer<int> pulses_comb,
            Pointer<int> pulses_in,
            int max_pulses,
            int len)
        {
            int k, sum;

            for (k = 0; k < len; k++)
            {
                sum = pulses_in[2 * k] + pulses_in[2 * k + 1];

                if (sum > max_pulses)
                {
                    return 1;
                }

                pulses_comb[k] = sum;
            }

            return 0;
        }

        /// <summary>
        /// Encode quantization indices of excitation
        /// </summary>
        /// <param name="psRangeEnc">I/O  compressor data structure</param>
        /// <param name="signalType">I    Signal type</param>
        /// <param name="quantOffsetType">I    quantOffsetType</param>
        /// <param name="pulses">I    quantization indices</param>
        /// <param name="frame_length">I    Frame length</param>
        public static void silk_encode_pulses(
            ec_ctx psRangeEnc,
            int signalType,
            int quantOffsetType,
            Pointer<sbyte> pulses,
            int frame_length)
        {
            int i, k, j, iter, bit, nLS, scale_down, RateLevelIndex = 0;
            int abs_q, minSumBits_Q5, sumBits_Q5;
            Pointer<int> abs_pulses;
            Pointer<int> sum_pulses;
            Pointer<int> nRshifts;
            Pointer<int> pulses_comb = Pointer.Malloc<int>(8);
            Pointer<int> abs_pulses_ptr;
            Pointer<sbyte> pulses_ptr;
            Pointer<byte> cdf_ptr;
            Pointer<byte> nBits_ptr;

            pulses_comb.MemSet(0, 8);

            /****************************/
            /* Prepare for shell coding */
            /****************************/
            /* Calculate number of shell blocks */
            Inlines.OpusAssert(1 << SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH == SilkConstants.SHELL_CODEC_FRAME_LENGTH);
            iter = Inlines.silk_RSHIFT(frame_length, SilkConstants.LOG2_SHELL_CODEC_FRAME_LENGTH);
            if (iter * SilkConstants.SHELL_CODEC_FRAME_LENGTH < frame_length)
            {
                Inlines.OpusAssert(frame_length == 12 * 10); /* Make sure only happens for 10 ms @ 12 kHz */
                iter++;
                pulses.Point(frame_length).MemSet(0, SilkConstants.SHELL_CODEC_FRAME_LENGTH);
            }

            /* Take the absolute value of the pulses */
            abs_pulses = Pointer.Malloc<int>(iter * SilkConstants.SHELL_CODEC_FRAME_LENGTH);
            Inlines.OpusAssert((SilkConstants.SHELL_CODEC_FRAME_LENGTH & 3) == 0);
            
            // unrolled loop
            for (i = 0; i < iter * SilkConstants.SHELL_CODEC_FRAME_LENGTH; i += 4)
            {
                abs_pulses[i + 0] = (int)Inlines.silk_abs(pulses[i + 0]);
                abs_pulses[i + 1] = (int)Inlines.silk_abs(pulses[i + 1]);
                abs_pulses[i + 2] = (int)Inlines.silk_abs(pulses[i + 2]);
                abs_pulses[i + 3] = (int)Inlines.silk_abs(pulses[i + 3]);
            }

            /* Calc sum pulses per shell code frame */
            sum_pulses = Pointer.Malloc<int>(iter);
            nRshifts = Pointer.Malloc<int>(iter);
            abs_pulses_ptr = abs_pulses;
            for (i = 0; i < iter; i++)
            {
                nRshifts[i] = 0;

                while (true)
                {
                    /* 1+1 . 2 */
                    scale_down = combine_and_check(pulses_comb, abs_pulses_ptr, Tables.silk_max_pulses_table[0], 8);
                    /* 2+2 . 4 */
                    scale_down += combine_and_check(pulses_comb, pulses_comb, Tables.silk_max_pulses_table[1], 4);
                    /* 4+4 . 8 */
                    scale_down += combine_and_check(pulses_comb, pulses_comb, Tables.silk_max_pulses_table[2], 2);
                    /* 8+8 . 16 */
                    scale_down += combine_and_check(sum_pulses.Point(i), pulses_comb, Tables.silk_max_pulses_table[3], 1);
                    
                    if (scale_down != 0)
                    {
                        /* We need to downscale the quantization signal */
                        nRshifts[i]++;
                        for (k = 0; k < SilkConstants.SHELL_CODEC_FRAME_LENGTH; k++)
                        {
                            abs_pulses_ptr[k] = Inlines.silk_RSHIFT(abs_pulses_ptr[k], 1);
                        }
                    }
                    else
                    {
                        /* Jump out of while(1) loop and go to next shell coding frame */
                        break;
                    }
                }

                abs_pulses_ptr = abs_pulses_ptr.Point(SilkConstants.SHELL_CODEC_FRAME_LENGTH);
            }

            /**************/
            /* Rate level */
            /**************/
            /* find rate level that leads to fewest bits for coding of pulses per block info */
            minSumBits_Q5 = int.MaxValue;
            for (k = 0; k < SilkConstants.N_RATE_LEVELS - 1; k++)
            {
                nBits_ptr = Tables.silk_pulses_per_block_BITS_Q5[k].GetPointer();
                sumBits_Q5 = Tables.silk_rate_levels_BITS_Q5[signalType >> 1][k];
                for (i = 0; i < iter; i++)
                {
                    if (nRshifts[i] > 0)
                    {
                        sumBits_Q5 += nBits_ptr[SilkConstants.SILK_MAX_PULSES + 1];
                    }
                    else {
                        sumBits_Q5 += nBits_ptr[sum_pulses[i]];
                    }
                }
                if (sumBits_Q5 < minSumBits_Q5)
                {
                    minSumBits_Q5 = sumBits_Q5;
                    RateLevelIndex = k;
                }
            }

            EntropyCoder.ec_enc_icdf(psRangeEnc, RateLevelIndex, Tables.silk_rate_levels_iCDF[signalType >> 1].GetPointer(), 8);

            /***************************************************/
            /* Sum-Weighted-Pulses Encoding                    */
            /***************************************************/
            cdf_ptr = new Pointer<byte>(Tables.silk_pulses_per_block_iCDF[RateLevelIndex]);
            for (i = 0; i < iter; i++)
            {
                if (nRshifts[i] == 0)
                {
                    EntropyCoder.ec_enc_icdf(psRangeEnc, sum_pulses[i], cdf_ptr, 8);
                }
                else
                {
                    EntropyCoder.ec_enc_icdf(psRangeEnc, SilkConstants.SILK_MAX_PULSES + 1, cdf_ptr, 8);
                    for (k = 0; k < nRshifts[i] - 1; k++)
                    {
                        EntropyCoder.ec_enc_icdf(psRangeEnc, SilkConstants.SILK_MAX_PULSES + 1, Tables.silk_pulses_per_block_iCDF[SilkConstants.N_RATE_LEVELS - 1].GetPointer(), 8);
                    }

                    EntropyCoder.ec_enc_icdf(psRangeEnc, sum_pulses[i], Tables.silk_pulses_per_block_iCDF[SilkConstants.N_RATE_LEVELS - 1].GetPointer(), 8);
                }
            }

            /******************/
            /* Shell Encoding */
            /******************/
            for (i = 0; i < iter; i++)
            {
                if (sum_pulses[i] > 0)
                {
                    ShellCoder.silk_shell_encoder(psRangeEnc, abs_pulses.Point(i * SilkConstants.SHELL_CODEC_FRAME_LENGTH));
                }
            }

            /****************/
            /* LSB Encoding */
            /****************/
            for (i = 0; i < iter; i++)
            {
                if (nRshifts[i] > 0)
                {
                    pulses_ptr = pulses.Point(i * SilkConstants.SHELL_CODEC_FRAME_LENGTH);
                    nLS = nRshifts[i] - 1;
                    for (k = 0; k < SilkConstants.SHELL_CODEC_FRAME_LENGTH; k++)
                    {
                        abs_q = (sbyte)Inlines.silk_abs(pulses_ptr[k]);
                        for (j = nLS; j > 0; j--)
                        {
                            bit = Inlines.silk_RSHIFT(abs_q, j) & 1;
                            EntropyCoder.ec_enc_icdf(psRangeEnc, bit, Tables.silk_lsb_iCDF.GetPointer(), 8);
                        }
                        bit = abs_q & 1;
                        EntropyCoder.ec_enc_icdf(psRangeEnc, bit, Tables.silk_lsb_iCDF.GetPointer(), 8);
                    }
                }
            }

            /****************/
            /* Encode signs */
            /****************/
            code_signs.silk_encode_signs(psRangeEnc, pulses, frame_length, signalType, quantOffsetType, sum_pulses);
        }
    }
}
