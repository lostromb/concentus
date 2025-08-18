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
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Inlines;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.Tables;

namespace HellaUnsafe.Silk
{
    internal static unsafe class GainQuant
    {
        private const int OFFSET = ((MIN_QGAIN_DB * 128) / 6 + 16 * 128);
        private const int SCALE_Q16 = ((65536 * (N_LEVELS_QGAIN - 1)) / (((MAX_QGAIN_DB - MIN_QGAIN_DB) * 128) / 6));
        private const int INV_SCALE_Q16 = ((65536 * (((MAX_QGAIN_DB - MIN_QGAIN_DB) * 128) / 6)) / (N_LEVELS_QGAIN - 1));

        /* Gain scalar quantization with hysteresis, uniform on log scale */
        internal static unsafe void silk_gains_quant(
            sbyte*                   ind/*[ MAX_NB_SUBFR ]*/,            /* O    gain indices                                */
            int*                  gain_Q16/*[ MAX_NB_SUBFR ]*/,       /* I/O  gains (quantized out)                       */
            sbyte                   *prev_ind,                      /* I/O  last index in previous frame                */
            in int              conditional,                    /* I    first gain is delta coded if 1              */
            in int              nb_subfr                        /* I    number of subframes                         */
        )
        {
            int k, double_step_size_threshold;

            for( k = 0; k < nb_subfr; k++ ) {
                /* Convert to log scale, scale, floor() */
                ind[ k ] = (sbyte)silk_SMULWB( SCALE_Q16, silk_lin2log( gain_Q16[ k ] ) - OFFSET );

                /* Round towards previous quantized gain (hysteresis) */
                if( ind[ k ] < *prev_ind ) {
                    ind[ k ]++;
                }
                ind[ k ] = (sbyte)silk_LIMIT_int( ind[ k ], 0, N_LEVELS_QGAIN - 1 );

                /* Compute delta indices and limit */
                if( k == 0 && conditional == 0 ) {
                    /* Full index */
                    ind[ k ] = (sbyte)silk_LIMIT_int( ind[ k ], *prev_ind + MIN_DELTA_GAIN_QUANT, N_LEVELS_QGAIN - 1 );
                    *prev_ind = ind[ k ];
                } else {
                    /* Delta index */
                    ind[ k ] = (sbyte)(ind[ k ] - *prev_ind);

                    /* Double the quantization step size for large gain increases, so that the max gain level can be reached */
                    double_step_size_threshold = 2 * MAX_DELTA_GAIN_QUANT - N_LEVELS_QGAIN + *prev_ind;
                    if( ind[ k ] > double_step_size_threshold ) {
                        ind[ k ] = (sbyte)(double_step_size_threshold + silk_RSHIFT( ind[ k ] - double_step_size_threshold + 1, 1 ));
                    }

                    ind[ k ] = (sbyte)silk_LIMIT_int( ind[ k ], MIN_DELTA_GAIN_QUANT, MAX_DELTA_GAIN_QUANT );

                    /* Accumulate deltas */
                    if( ind[ k ] > double_step_size_threshold ) {
                        *prev_ind += (sbyte)(silk_LSHIFT( ind[ k ], 1 ) - double_step_size_threshold);
                        *prev_ind = (sbyte)silk_min_int( *prev_ind, N_LEVELS_QGAIN - 1 );
                    } else {
                        *prev_ind += ind[ k ];
                    }

                    /* Shift to make non-negative */
                    ind[ k ] -= MIN_DELTA_GAIN_QUANT;
                }

                /* Scale and convert to linear scale */
                gain_Q16[ k ] = silk_log2lin( silk_min_32( silk_SMULWB( INV_SCALE_Q16, *prev_ind ) + OFFSET, 3967 ) ); /* 3967 = 31 in Q7 */
            }
        }

        /* Gains scalar dequantization, uniform on log scale */
        internal static unsafe void silk_gains_dequant(
            int*                  gain_Q16/*[ MAX_NB_SUBFR ]*/,       /* O    quantized gains                             */
            in sbyte*             ind/*[ MAX_NB_SUBFR ]*/,            /* I    gain indices                                */
            sbyte                   *prev_ind,                      /* I/O  last index in previous frame                */
            in int              conditional,                    /* I    first gain is delta coded if 1              */
            in int              nb_subfr                        /* I    number of subframes                          */
        )
        {
            int   k, ind_tmp, double_step_size_threshold;

            for( k = 0; k < nb_subfr; k++ ) {
                if( k == 0 && conditional == 0 ) {
                    /* Gain index is not allowed to go down more than 16 steps (~21.8 dB) */
                    *prev_ind = (sbyte)silk_max_int( ind[ k ], *prev_ind - 16 );
                } else {
                    /* Delta index */
                    ind_tmp = ind[ k ] + MIN_DELTA_GAIN_QUANT;

                    /* Accumulate deltas */
                    double_step_size_threshold = 2 * MAX_DELTA_GAIN_QUANT - N_LEVELS_QGAIN + *prev_ind;
                    if( ind_tmp > double_step_size_threshold ) {
                        *prev_ind += (sbyte)(silk_LSHIFT( ind_tmp, 1 ) - double_step_size_threshold);
                    } else {
                        *prev_ind += (sbyte)ind_tmp;
                    }
                }
                *prev_ind = (sbyte)silk_LIMIT_int( *prev_ind, 0, N_LEVELS_QGAIN - 1 );

                /* Scale and convert to linear scale */
                gain_Q16[ k ] = silk_log2lin( silk_min_32( silk_SMULWB( INV_SCALE_Q16, *prev_ind ) + OFFSET, 3967 ) ); /* 3967 = 31 in Q7 */
            }
        }

        /* Compute unique identifier of gain indices vector */
        internal static unsafe int silk_gains_ID(                                       /* O    returns unique identifier of gains          */
            in sbyte*             ind/*[ MAX_NB_SUBFR ]*/,            /* I    gain indices                                */
            in int              nb_subfr                        /* I    number of subframes                         */
        )
        {
            int   k;
            int gainsID;

            gainsID = 0;
            for( k = 0; k < nb_subfr; k++ ) {
                gainsID = silk_ADD_LSHIFT32( ind[ k ], gainsID, 8 );
            }

            return gainsID;
        }
    }
}
