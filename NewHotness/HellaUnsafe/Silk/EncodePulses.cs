/***********************************************************************
Copyright (c) 2006-2011, Skype Limited. All rights reserved.
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
- Redistributions of source code must retain the above copyright notice,
this list of conditions and the following disclaimer.
- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.
- Neither the name of Internet Society, IETF or IETF Trust, nor the
names of specific contributors, may be used to endorse or promote
products derived from this software without specific prior written
permission.
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
***********************************************************************/

using System;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.CodeSigns;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.ShellCoder;
using static HellaUnsafe.Silk.Tables;

namespace HellaUnsafe.Silk
{
    internal static unsafe class EncodePulses
    {
        /*********************************************/
        /* Encode quantization indices of excitation */
        /*********************************************/

        internal static unsafe int combine_and_check(    /* return ok                           */
            int         *pulses_comb,           /* O                                   */
            in int* pulses_in,             /* I                                   */
            int max_pulses,             /* I    max value for sum of pulses    */
            int len                     /* I    number of output values        */
        )
        {
            int k, sum;

            for( k = 0; k < len; k++ ) {
                sum = pulses_in[ 2 * k ] + pulses_in[ 2 * k + 1 ];
                if( sum > max_pulses ) {
                    return 1;
                }
                pulses_comb[ k ] = sum;
            }

            return 0;
        }

        /* Encode quantization indices of excitation */
        internal static unsafe void silk_encode_pulses(
            ec_ctx                      *psRangeEnc,                    /* I/O  compressor data structure                   */
            in int              signalType,                     /* I    Signal type                                 */
            in int quantOffsetType,                /* I    quantOffsetType                             */
            sbyte*                   pulses,                       /* I    quantization indices                        */
            in int frame_length                    /* I    Frame length                                */
        )
        {
            int i, k, j, iter, bit, nLS, scale_down, RateLevelIndex = 0;
            int abs_q, minSumBits_Q5, sumBits_Q5;
            int* pulses_comb = stackalloc int[ 8 ];
            int* abs_pulses_ptr;
            sbyte *pulses_ptr;
            byte *cdf_ptr;
            byte *nBits_ptr;

            silk_memset( pulses_comb, 0, 8 * sizeof(int) ); /* Fixing Valgrind reported problem*/

            /****************************/
            /* Prepare for shell coding */
            /****************************/
            /* Calculate number of shell blocks */
            silk_assert( 1 << LOG2_SHELL_CODEC_FRAME_LENGTH == SHELL_CODEC_FRAME_LENGTH );
            iter = silk_RSHIFT( frame_length, LOG2_SHELL_CODEC_FRAME_LENGTH );
            if( iter * SHELL_CODEC_FRAME_LENGTH < frame_length ) {
                celt_assert( frame_length == 12 * 10 ); /* Make sure only happens for 10 ms @ 12 kHz */
                iter++;
                silk_memset( &pulses[ frame_length ], 0, SHELL_CODEC_FRAME_LENGTH * sizeof(sbyte));
            }

            /* Take the absolute value of the pulses */
            int[] abs_pulses_data = new int[iter * SHELL_CODEC_FRAME_LENGTH];
            fixed (int* abs_pulses = abs_pulses_data)
            {
                silk_assert((SHELL_CODEC_FRAME_LENGTH & 3) == 0);
                for (i = 0; i < iter * SHELL_CODEC_FRAME_LENGTH; i += 4)
                {
                    abs_pulses[i + 0] = (int)silk_abs(pulses[i + 0]);
                    abs_pulses[i + 1] = (int)silk_abs(pulses[i + 1]);
                    abs_pulses[i + 2] = (int)silk_abs(pulses[i + 2]);
                    abs_pulses[i + 3] = (int)silk_abs(pulses[i + 3]);
                }

                /* Calc sum pulses per shell code frame */
                int[] sum_pulses_data = new int[iter];
                int[] nRshifts_data = new int[iter];
                fixed (int* sum_pulses = sum_pulses_data)
                fixed (int* nRshifts = nRshifts_data)
                {
                    abs_pulses_ptr = abs_pulses;
                    for (i = 0; i < iter; i++)
                    {
                        nRshifts[i] = 0;

                        while (true)
                        {
                            /* 1+1 -> 2 */
                            scale_down = combine_and_check(pulses_comb, abs_pulses_ptr, silk_max_pulses_table[0], 8);
                            /* 2+2 -> 4 */
                            scale_down += combine_and_check(pulses_comb, pulses_comb, silk_max_pulses_table[1], 4);
                            /* 4+4 -> 8 */
                            scale_down += combine_and_check(pulses_comb, pulses_comb, silk_max_pulses_table[2], 2);
                            /* 8+8 -> 16 */
                            scale_down += combine_and_check(&sum_pulses[i], pulses_comb, silk_max_pulses_table[3], 1);

                            if (scale_down != 0)
                            {
                                /* We need to downscale the quantization signal */
                                nRshifts[i]++;
                                for (k = 0; k < SHELL_CODEC_FRAME_LENGTH; k++)
                                {
                                    abs_pulses_ptr[k] = silk_RSHIFT(abs_pulses_ptr[k], 1);
                                }
                            }
                            else
                            {
                                /* Jump out of while(1) loop and go to next shell coding frame */
                                break;
                            }
                        }
                        abs_pulses_ptr += SHELL_CODEC_FRAME_LENGTH;
                    }

                    /**************/
                    /* Rate level */
                    /**************/
                    /* find rate level that leads to fewest bits for coding of pulses per block info */
                    minSumBits_Q5 = silk_int32_MAX;
                    for (k = 0; k < N_RATE_LEVELS - 1; k++)
                    {
                        nBits_ptr = silk_pulses_per_block_BITS_Q5[k];
                        sumBits_Q5 = silk_rate_levels_BITS_Q5[signalType >> 1][k];
                        for (i = 0; i < iter; i++)
                        {
                            if (nRshifts[i] > 0)
                            {
                                sumBits_Q5 += nBits_ptr[SILK_MAX_PULSES + 1];
                            }
                            else
                            {
                                sumBits_Q5 += nBits_ptr[sum_pulses[i]];
                            }
                        }
                        if (sumBits_Q5 < minSumBits_Q5)
                        {
                            minSumBits_Q5 = sumBits_Q5;
                            RateLevelIndex = k;
                        }
                    }
                    ec_enc_icdf(psRangeEnc, RateLevelIndex, silk_rate_levels_iCDF[signalType >> 1], 8);

                    /***************************************************/
                    /* Sum-Weighted-Pulses Encoding                    */
                    /***************************************************/
                    cdf_ptr = silk_pulses_per_block_iCDF[RateLevelIndex];
                    for (i = 0; i < iter; i++)
                    {
                        if (nRshifts[i] == 0)
                        {
                            ec_enc_icdf(psRangeEnc, sum_pulses[i], cdf_ptr, 8);
                        }
                        else
                        {
                            ec_enc_icdf(psRangeEnc, SILK_MAX_PULSES + 1, cdf_ptr, 8);
                            for (k = 0; k < nRshifts[i] - 1; k++)
                            {
                                ec_enc_icdf(psRangeEnc, SILK_MAX_PULSES + 1, silk_pulses_per_block_iCDF[N_RATE_LEVELS - 1], 8);
                            }
                            ec_enc_icdf(psRangeEnc, sum_pulses[i], silk_pulses_per_block_iCDF[N_RATE_LEVELS - 1], 8);
                        }
                    }

                    /******************/
                    /* Shell Encoding */
                    /******************/
                    for (i = 0; i < iter; i++)
                    {
                        if (sum_pulses[i] > 0)
                        {
                            silk_shell_encoder(psRangeEnc, &abs_pulses[i * SHELL_CODEC_FRAME_LENGTH]);
                        }
                    }

                    /****************/
                    /* LSB Encoding */
                    /****************/
                    for (i = 0; i < iter; i++)
                    {
                        if (nRshifts[i] > 0)
                        {
                            pulses_ptr = &pulses[i * SHELL_CODEC_FRAME_LENGTH];
                            nLS = nRshifts[i] - 1;
                            for (k = 0; k < SHELL_CODEC_FRAME_LENGTH; k++)
                            {
                                abs_q = (sbyte)silk_abs(pulses_ptr[k]);
                                for (j = nLS; j > 0; j--)
                                {
                                    bit = silk_RSHIFT(abs_q, j) & 1;
                                    ec_enc_icdf(psRangeEnc, bit, silk_lsb_iCDF, 8);
                                }
                                bit = abs_q & 1;
                                ec_enc_icdf(psRangeEnc, bit, silk_lsb_iCDF, 8);
                            }
                        }
                    }

                    /****************/
                    /* Encode signs */
                    /****************/
                    silk_encode_signs(psRangeEnc, pulses, frame_length, signalType, quantOffsetType, sum_pulses);
                }
            }
        }
    }
}
