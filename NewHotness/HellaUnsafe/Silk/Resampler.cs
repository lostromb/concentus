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
using static HellaUnsafe.Silk.ResamplerRom;
using static HellaUnsafe.Silk.ResamplerStructs;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.Tables;

namespace HellaUnsafe.Silk
{
    /*
     * Matrix of resampling methods used:
     *                                 Fs_out (kHz)
     *                        8      12     16     24     48
     *
     *               8        C      UF     U      UF     UF
     *              12        AF     C      UF     U      UF
     * Fs_in (kHz)  16        D      AF     C      UF     UF
     *              24        AF     D      AF     C      U
     *              48        AF     AF     AF     D      C
     *
     * C   -> Copy (no resampling)
     * D   -> Allpass-based 2x downsampling
     * U   -> Allpass-based 2x upsampling
     * UF  -> Allpass-based 2x upsampling followed by FIR interpolation
     * AF  -> AR2 filter followed by FIR interpolation
     */
    internal static unsafe class Resampler
    {
        private const int ORDER_FIR = 4;

        /* Tables with delay compensation values to equalize total delay for different modes */
        private static readonly sbyte[][] delay_matrix_enc = new sbyte[5][]
        {
            /* in  \ out  8  12  16 */
            /*  8 */  new sbyte[3]  {  6,  0,  3 },
            /* 12 */  new sbyte[3]  {  0,  7,  3 },
            /* 16 */  new sbyte[3]  {  0,  1, 10 },
            /* 24 */  new sbyte[3]  {  0,  2,  6 },
            /* 48 */  new sbyte[3]  { 18, 10, 12 }
        };

        private static readonly sbyte[][] delay_matrix_dec = new sbyte[3][]
        {
            /* in  \ out  8  12  16  24  48 */
            /*  8 */  new sbyte[5] {  4,  0,  2,  0,  0 },
            /* 12 */  new sbyte[5] {  0,  9,  4,  7,  4 },
            /* 16 */  new sbyte[5] {  0,  3, 12,  7,  7 }
        };

        /* Simple way to make [8000, 12000, 16000, 24000, 48000] to [0, 1, 2, 3, 4] */
        private static int rateID(int R)
        {
            return ((((R >> 12) - BOOL2INT(R > 16000)) >> BOOL2INT((R) > 24000)) - 1);
        }

        private const int USE_silk_resampler_copy = 0;
        private const int USE_silk_resampler_private_up2_HQ_wrapper = 1;
        private const int USE_silk_resampler_private_IIR_FIR = 2;
        private const int USE_silk_resampler_private_down_FIR = 3;

        /* Initialize/reset the resampler state for a given pair of input/output sampling rates */
        internal static unsafe int silk_resampler_init(
            silk_resampler_state_struct* S,                 /* I/O  Resampler state                                             */
            int                  Fs_Hz_in,           /* I    Input sampling rate (Hz)                                    */
            int                  Fs_Hz_out,          /* I    Output sampling rate (Hz)                                   */
            int                    forEnc              /* I    If 1: encoder; if 0: decoder                                */
        )
        {
            int up2x;

            /* Clear state */
            //silk_memset(S, 0, sizeof(silk_resampler_state_struct));
            *S = new silk_resampler_state_struct();

            /* Input checking */
            if( forEnc != 0 ) {
                if( ( Fs_Hz_in  != 8000 && Fs_Hz_in  != 12000 && Fs_Hz_in  != 16000 && Fs_Hz_in  != 24000 && Fs_Hz_in  != 48000 ) ||
                    ( Fs_Hz_out != 8000 && Fs_Hz_out != 12000 && Fs_Hz_out != 16000 ) ) {
                    celt_assert( false );
                    return -1;
                }
                S->inputDelay = delay_matrix_enc[ rateID( Fs_Hz_in ) ][ rateID( Fs_Hz_out ) ];
            } else {
                if( ( Fs_Hz_in  != 8000 && Fs_Hz_in  != 12000 && Fs_Hz_in  != 16000 ) ||
                    ( Fs_Hz_out != 8000 && Fs_Hz_out != 12000 && Fs_Hz_out != 16000 && Fs_Hz_out != 24000 && Fs_Hz_out != 48000 ) ) {
                    celt_assert(false);
                    return -1;
                }
                S->inputDelay = delay_matrix_dec[ rateID( Fs_Hz_in ) ][ rateID( Fs_Hz_out ) ];
            }

            S->Fs_in_kHz  = silk_DIV32_16( Fs_Hz_in,  1000 );
            S->Fs_out_kHz = silk_DIV32_16( Fs_Hz_out, 1000 );

            /* Number of samples processed per batch */
            S->batchSize = S->Fs_in_kHz * RESAMPLER_MAX_BATCH_SIZE_MS;

            /* Find resampler with the right sampling ratio */
            up2x = 0;
            if( Fs_Hz_out > Fs_Hz_in ) {
                /* Upsample */
                if( Fs_Hz_out == silk_MUL( Fs_Hz_in, 2 ) ) {                            /* Fs_out : Fs_in = 2 : 1 */
                    /* Special case: directly use 2x upsampler */
                    S->resampler_function = USE_silk_resampler_private_up2_HQ_wrapper;
                } else {
                    /* Default resampler */
                    S->resampler_function = USE_silk_resampler_private_IIR_FIR;
                    up2x = 1;
                }
            } else if ( Fs_Hz_out < Fs_Hz_in ) {
                /* Downsample */
                 S->resampler_function = USE_silk_resampler_private_down_FIR;
                if( silk_MUL( Fs_Hz_out, 4 ) == silk_MUL( Fs_Hz_in, 3 ) ) {             /* Fs_out : Fs_in = 3 : 4 */
                    S->FIR_Fracs = 3;
                    S->FIR_Order = RESAMPLER_DOWN_ORDER_FIR0;
                    S->Coefs = silk_Resampler_3_4_COEFS;
                } else if( silk_MUL( Fs_Hz_out, 3 ) == silk_MUL( Fs_Hz_in, 2 ) ) {      /* Fs_out : Fs_in = 2 : 3 */
                    S->FIR_Fracs = 2;
                    S->FIR_Order = RESAMPLER_DOWN_ORDER_FIR0;
                    S->Coefs = silk_Resampler_2_3_COEFS;
                } else if( silk_MUL( Fs_Hz_out, 2 ) == Fs_Hz_in ) {                     /* Fs_out : Fs_in = 1 : 2 */
                    S->FIR_Fracs = 1;
                    S->FIR_Order = RESAMPLER_DOWN_ORDER_FIR1;
                    S->Coefs = silk_Resampler_1_2_COEFS;
                } else if( silk_MUL( Fs_Hz_out, 3 ) == Fs_Hz_in ) {                     /* Fs_out : Fs_in = 1 : 3 */
                    S->FIR_Fracs = 1;
                    S->FIR_Order = RESAMPLER_DOWN_ORDER_FIR2;
                    S->Coefs = silk_Resampler_1_3_COEFS;
                } else if( silk_MUL( Fs_Hz_out, 4 ) == Fs_Hz_in ) {                     /* Fs_out : Fs_in = 1 : 4 */
                    S->FIR_Fracs = 1;
                    S->FIR_Order = RESAMPLER_DOWN_ORDER_FIR2;
                    S->Coefs = silk_Resampler_1_4_COEFS;
                } else if( silk_MUL( Fs_Hz_out, 6 ) == Fs_Hz_in ) {                     /* Fs_out : Fs_in = 1 : 6 */
                    S->FIR_Fracs = 1;
                    S->FIR_Order = RESAMPLER_DOWN_ORDER_FIR2;
                    S->Coefs = silk_Resampler_1_6_COEFS;
                } else {
                    /* None available */
                    celt_assert( false );
                    return -1;
                }
            } else {
                /* Input and output sampling rates are equal: copy */
                S->resampler_function = USE_silk_resampler_copy;
            }

            /* Ratio of input/output samples */
            S->invRatio_Q16 = silk_LSHIFT32( silk_DIV32( silk_LSHIFT32( Fs_Hz_in, 14 + up2x ), Fs_Hz_out ), 2 );
            /* Make sure the ratio is rounded up */
            while( silk_SMULWW( S->invRatio_Q16, Fs_Hz_out ) < silk_LSHIFT32( Fs_Hz_in, up2x ) ) {
                S->invRatio_Q16++;
            }

            return 0;
        }

        /* Resampler: convert from one sampling rate to another */
        /* Input and output sampling rate are at most 48000 Hz  */
        internal static unsafe int silk_resampler(
            silk_resampler_state_struct *S,                 /* I/O  Resampler state                                             */
            short*                  output,              /* O    Output signal                                               */
            in short*            input,               /* I    Input signal                                                */
            int                  inLen               /* I    Number of input samples                                     */
        )
        {
            int nSamples;

            /* Need at least 1 ms of input data */
            celt_assert( inLen >= S->Fs_in_kHz );
            /* Delay can't exceed the 1 ms of buffering */
            celt_assert( S->inputDelay <= S->Fs_in_kHz );

            nSamples = S->Fs_in_kHz - S->inputDelay;

            /* Copy to delay buffer */
            silk_memcpy( &S->delayBuf[ S->inputDelay ], input, nSamples * sizeof( short ) );

            switch( S->resampler_function ) {
                case USE_silk_resampler_private_up2_HQ_wrapper:
                    silk_resampler_private_up2_HQ_wrapper( S, output, S->delayBuf, S->Fs_in_kHz );
                    silk_resampler_private_up2_HQ_wrapper( S, &output[ S->Fs_out_kHz ], &input[ nSamples ], inLen - S->Fs_in_kHz );
                    break;
                case USE_silk_resampler_private_IIR_FIR:
                    silk_resampler_private_IIR_FIR( S, output, S->delayBuf, S->Fs_in_kHz );
                    silk_resampler_private_IIR_FIR( S, &output[ S->Fs_out_kHz ], &input[ nSamples ], inLen - S->Fs_in_kHz );
                    break;
                case USE_silk_resampler_private_down_FIR:
                    silk_resampler_private_down_FIR( S, output, S->delayBuf, S->Fs_in_kHz );
                    silk_resampler_private_down_FIR( S, &output[ S->Fs_out_kHz ], &input[ nSamples ], inLen - S->Fs_in_kHz );
                    break;
                default:
                    silk_memcpy(output, S->delayBuf, S->Fs_in_kHz * sizeof( short ) );
                    silk_memcpy( &output[ S->Fs_out_kHz ], &input[ nSamples ], ( inLen - S->Fs_in_kHz ) * sizeof( short ) );
                    break;
            }

            /* Copy to delay buffer */
            silk_memcpy( S->delayBuf, &input[ inLen - S->inputDelay ], S->inputDelay * sizeof( short ) );

            return 0;
        }

        /* Downsample by a factor 2 */
        internal static unsafe void silk_resampler_down2(
            int                  *S,                 /* I/O  State vector [ 2 ]                                          */
            short                  *output,               /* O    Output signal [ floor(len/2) ]                              */
            in short            *input,                /* I    Input signal [ len ]                                        */
            int                  inLen               /* I    Number of input samples                                     */
        )
        {
            int k, len2 = silk_RSHIFT32( inLen, 1 );
            int in32, out32, Y, X;

            celt_assert( silk_resampler_down2_0 > 0 );
            celt_assert( silk_resampler_down2_1 < 0 );

            /* Internal variables and state are in Q10 format */
            for( k = 0; k < len2; k++ ) {
                /* Convert to Q10 */
                in32 = silk_LSHIFT( (int)input[ 2 * k ], 10 );

                /* All-pass section for even input sample */
                Y      = silk_SUB32( in32, S[ 0 ] );
                X      = silk_SMLAWB( Y, Y, silk_resampler_down2_1 );
                out32  = silk_ADD32( S[ 0 ], X );
                S[ 0 ] = silk_ADD32( in32, X );

                /* Convert to Q10 */
                in32 = silk_LSHIFT( (int)input[ 2 * k + 1 ], 10 );

                /* All-pass section for odd input sample, and add to output of previous section */
                Y      = silk_SUB32( in32, S[ 1 ] );
                X      = silk_SMULWB( Y, silk_resampler_down2_0 );
                out32  = silk_ADD32( out32, S[ 1 ] );
                out32  = silk_ADD32( out32, X );
                S[ 1 ] = silk_ADD32( in32, X );

                /* Add, convert back to int16 and store to output */
                output[ k ] = (short)silk_SAT16( silk_RSHIFT_ROUND( out32, 11 ) );
            }
        }

        /* Downsample by a factor 2/3, low quality */
        internal static unsafe void silk_resampler_down2_3(
            int                  *S,                 /* I/O  State vector [ 6 ]                                          */
            short                  *output,               /* O    Output signal [ floor(2*inLen/3) ]                          */
            short            * input,                /* I    Input signal [ inLen ]                                      */
            int                  inLen               /* I    Number of input samples                                     */
        )
        {
            int nSamplesIn, counter, res_Q6;
            int[] buf_data;
            int* buf_ptr;
            buf_data = new int[RESAMPLER_MAX_BATCH_SIZE_IN + ORDER_FIR];
            fixed (int* buf = buf_data)
            {
                /* Copy buffered samples to start of buffer */
                silk_memcpy(buf, S, ORDER_FIR * sizeof(int));

                /* Iterate over blocks of frameSizeIn input samples */
                while (true) {
                    nSamplesIn = silk_min(inLen, RESAMPLER_MAX_BATCH_SIZE_IN);

                    /* Second-order AR filter (output in Q8) */
                    silk_resampler_private_AR2(&S[ORDER_FIR], &buf[ORDER_FIR], input,
                        silk_Resampler_2_3_COEFS_LQ, nSamplesIn);

                    /* Interpolate filtered signal */
                    buf_ptr = buf;
                    counter = nSamplesIn;
                    while (counter > 2) {
                        /* Inner product */
                        res_Q6 = silk_SMULWB(buf_ptr[0], silk_Resampler_2_3_COEFS_LQ[2]);
                        res_Q6 = silk_SMLAWB(res_Q6, buf_ptr[1], silk_Resampler_2_3_COEFS_LQ[3]);
                        res_Q6 = silk_SMLAWB(res_Q6, buf_ptr[2], silk_Resampler_2_3_COEFS_LQ[5]);
                        res_Q6 = silk_SMLAWB(res_Q6, buf_ptr[3], silk_Resampler_2_3_COEFS_LQ[4]);

                        /* Scale down, saturate and store in output array */
                        *output++ = (short)silk_SAT16(silk_RSHIFT_ROUND(res_Q6, 6));

                        res_Q6 = silk_SMULWB(buf_ptr[1], silk_Resampler_2_3_COEFS_LQ[4]);
                        res_Q6 = silk_SMLAWB(res_Q6, buf_ptr[2], silk_Resampler_2_3_COEFS_LQ[5]);
                        res_Q6 = silk_SMLAWB(res_Q6, buf_ptr[3], silk_Resampler_2_3_COEFS_LQ[3]);
                        res_Q6 = silk_SMLAWB(res_Q6, buf_ptr[4], silk_Resampler_2_3_COEFS_LQ[2]);

                        /* Scale down, saturate and store in output array */
                        *output++ = (short)silk_SAT16(silk_RSHIFT_ROUND(res_Q6, 6));

                        buf_ptr += 3;
                        counter -= 3;
                    }

                    input += nSamplesIn;
                    inLen -= nSamplesIn;

                    if (inLen > 0) {
                        /* More iterations to do; copy last part of filtered signal to beginning of buffer */
                        silk_memcpy(buf, &buf[nSamplesIn], ORDER_FIR * sizeof(int));
                    } else {
                        break;
                    }
                }

                /* Copy last part of filtered signal to the state for the next call */
                silk_memcpy(S, &buf[nSamplesIn], ORDER_FIR * sizeof(int));
            }
        }

        /* Second order AR filter with single delay elements */
        internal static unsafe void silk_resampler_private_AR2(
            int*                      S,            /* I/O  State vector [ 2 ]          */
            int*                      out_Q8,       /* O    Output signal               */
            in short*                input,           /* I    Input signal                */
            in short*                A_Q14,        /* I    AR coefficients, Q14        */
            int                      len             /* I    Signal length               */
        )
        {
            int    k;
            int    out32;

            for( k = 0; k < len; k++ ) {
                out32       = silk_ADD_LSHIFT32( S[ 0 ], (int)input[ k ], 8 );
                out_Q8[ k ] = out32;
                out32       = silk_LSHIFT( out32, 2 );
                S[ 0 ]      = silk_SMLAWB( S[ 1 ], out32, A_Q14[ 0 ] );
                S[ 1 ]      = silk_SMULWB( out32, A_Q14[ 1 ] );
            }
        }

        internal static unsafe short* silk_resampler_private_down_FIR_INTERPOL(
            short          * output,
            int          *buf,
            in short    *FIR_Coefs,
            int            FIR_Order,
            int            FIR_Fracs,
            int          max_index_Q16,
            int          index_increment_Q16
        )
        {
            int index_Q16, res_Q6;
            int* buf_ptr;
            int interpol_ind;
            short* interpol_ptr;

            switch( FIR_Order ) {
                case RESAMPLER_DOWN_ORDER_FIR0:
                    for( index_Q16 = 0; index_Q16 < max_index_Q16; index_Q16 += index_increment_Q16 ) {
                        /* Integer part gives pointer to buffered input */
                        buf_ptr = buf + silk_RSHIFT( index_Q16, 16 );

                        /* Fractional part gives interpolation coefficients */
                        interpol_ind = silk_SMULWB( index_Q16 & 0xFFFF, FIR_Fracs );

                        /* Inner product */
                        interpol_ptr = &FIR_Coefs[ RESAMPLER_DOWN_ORDER_FIR0 / 2 * interpol_ind ];
                        res_Q6 = silk_SMULWB(         buf_ptr[ 0 ], interpol_ptr[ 0 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 1 ], interpol_ptr[ 1 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 2 ], interpol_ptr[ 2 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 3 ], interpol_ptr[ 3 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 4 ], interpol_ptr[ 4 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 5 ], interpol_ptr[ 5 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 6 ], interpol_ptr[ 6 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 7 ], interpol_ptr[ 7 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 8 ], interpol_ptr[ 8 ] );
                        interpol_ptr = &FIR_Coefs[ RESAMPLER_DOWN_ORDER_FIR0 / 2 * ( FIR_Fracs - 1 - interpol_ind ) ];
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 17 ], interpol_ptr[ 0 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 16 ], interpol_ptr[ 1 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 15 ], interpol_ptr[ 2 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 14 ], interpol_ptr[ 3 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 13 ], interpol_ptr[ 4 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 12 ], interpol_ptr[ 5 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 11 ], interpol_ptr[ 6 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[ 10 ], interpol_ptr[ 7 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, buf_ptr[  9 ], interpol_ptr[ 8 ] );

                        /* Scale down, saturate and store in output array */
                        *output++ = (short)silk_SAT16( silk_RSHIFT_ROUND( res_Q6, 6 ) );
                    }
                    break;
                case RESAMPLER_DOWN_ORDER_FIR1:
                    for( index_Q16 = 0; index_Q16 < max_index_Q16; index_Q16 += index_increment_Q16 ) {
                        /* Integer part gives pointer to buffered input */
                        buf_ptr = buf + silk_RSHIFT( index_Q16, 16 );

                        /* Inner product */
                        res_Q6 = silk_SMULWB(         silk_ADD32( buf_ptr[  0 ], buf_ptr[ 23 ] ), FIR_Coefs[  0 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  1 ], buf_ptr[ 22 ] ), FIR_Coefs[  1 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  2 ], buf_ptr[ 21 ] ), FIR_Coefs[  2 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  3 ], buf_ptr[ 20 ] ), FIR_Coefs[  3 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  4 ], buf_ptr[ 19 ] ), FIR_Coefs[  4 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  5 ], buf_ptr[ 18 ] ), FIR_Coefs[  5 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  6 ], buf_ptr[ 17 ] ), FIR_Coefs[  6 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  7 ], buf_ptr[ 16 ] ), FIR_Coefs[  7 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  8 ], buf_ptr[ 15 ] ), FIR_Coefs[  8 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  9 ], buf_ptr[ 14 ] ), FIR_Coefs[  9 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[ 10 ], buf_ptr[ 13 ] ), FIR_Coefs[ 10 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[ 11 ], buf_ptr[ 12 ] ), FIR_Coefs[ 11 ] );

                        /* Scale down, saturate and store in output array */
                        *output++ = (short)silk_SAT16( silk_RSHIFT_ROUND( res_Q6, 6 ) );
                    }
                    break;
                case RESAMPLER_DOWN_ORDER_FIR2:
                    for( index_Q16 = 0; index_Q16 < max_index_Q16; index_Q16 += index_increment_Q16 ) {
                        /* Integer part gives pointer to buffered input */
                        buf_ptr = buf + silk_RSHIFT( index_Q16, 16 );

                        /* Inner product */
                        res_Q6 = silk_SMULWB(         silk_ADD32( buf_ptr[  0 ], buf_ptr[ 35 ] ), FIR_Coefs[  0 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  1 ], buf_ptr[ 34 ] ), FIR_Coefs[  1 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  2 ], buf_ptr[ 33 ] ), FIR_Coefs[  2 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  3 ], buf_ptr[ 32 ] ), FIR_Coefs[  3 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  4 ], buf_ptr[ 31 ] ), FIR_Coefs[  4 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  5 ], buf_ptr[ 30 ] ), FIR_Coefs[  5 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  6 ], buf_ptr[ 29 ] ), FIR_Coefs[  6 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  7 ], buf_ptr[ 28 ] ), FIR_Coefs[  7 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  8 ], buf_ptr[ 27 ] ), FIR_Coefs[  8 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[  9 ], buf_ptr[ 26 ] ), FIR_Coefs[  9 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[ 10 ], buf_ptr[ 25 ] ), FIR_Coefs[ 10 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[ 11 ], buf_ptr[ 24 ] ), FIR_Coefs[ 11 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[ 12 ], buf_ptr[ 23 ] ), FIR_Coefs[ 12 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[ 13 ], buf_ptr[ 22 ] ), FIR_Coefs[ 13 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[ 14 ], buf_ptr[ 21 ] ), FIR_Coefs[ 14 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[ 15 ], buf_ptr[ 20 ] ), FIR_Coefs[ 15 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[ 16 ], buf_ptr[ 19 ] ), FIR_Coefs[ 16 ] );
                        res_Q6 = silk_SMLAWB( res_Q6, silk_ADD32( buf_ptr[ 17 ], buf_ptr[ 18 ] ), FIR_Coefs[ 17 ] );

                        /* Scale down, saturate and store in output array */
                        *output++ = (short)silk_SAT16( silk_RSHIFT_ROUND( res_Q6, 6 ) );
                    }
                    break;
                default:
                    celt_assert( false );
                    break;
            }
            return output;
        }

        /* Resample with a 2nd order AR filter followed by FIR interpolation */
        internal static unsafe void silk_resampler_private_down_FIR(
            void                            *SS,            /* I/O  Resampler state             */
            short*                      output,          /* O    Output signal               */
            short*                input,           /* I    Input signal                */
            int                      inLen           /* I    Number of input samples     */
        )
        {
            silk_resampler_state_struct *S = (silk_resampler_state_struct *)SS;
            int nSamplesIn;
            int max_index_Q16, index_increment_Q16;
            int[] buf_data;
            short* FIR_Coefs;

            buf_data = new int[S->batchSize + S->FIR_Order];
            fixed (int* buf = buf_data)
            {
                /* Copy buffered samples to start of buffer */
                silk_memcpy(buf, S->sFIR_i32, S->FIR_Order * sizeof(int));

                FIR_Coefs = &S->Coefs[2];

                /* Iterate over blocks of frameSizeIn input samples */
                index_increment_Q16 = S->invRatio_Q16;
                while (true)
                {
                    nSamplesIn = silk_min(inLen, S->batchSize);

                    /* Second-order AR filter (output in Q8) */
                    silk_resampler_private_AR2(S->sIIR, &buf[S->FIR_Order], input, S->Coefs, nSamplesIn);

                    max_index_Q16 = silk_LSHIFT32(nSamplesIn, 16);

                    /* Interpolate filtered signal */
                    output = silk_resampler_private_down_FIR_INTERPOL(output, buf, FIR_Coefs, S->FIR_Order,
                        S->FIR_Fracs, max_index_Q16, index_increment_Q16);

                    input += nSamplesIn;
                    inLen -= nSamplesIn;

                    if (inLen > 1)
                    {
                        /* More iterations to do; copy last part of filtered signal to beginning of buffer */
                        silk_memcpy(buf, &buf[nSamplesIn], S->FIR_Order * sizeof(int));
                    }
                    else
                    {
                        break;
                    }
                }

                /* Copy last part of filtered signal to the state for the next call */
                silk_memcpy(S->sFIR_i32, &buf[nSamplesIn], S->FIR_Order * sizeof(int));
            }
        }

        internal static unsafe short* silk_resampler_private_IIR_FIR_INTERPOL(
            short  * output,
            short  *buf,
            int  max_index_Q16,
            int  index_increment_Q16
        )
        {
            int index_Q16, res_Q15;
            short *buf_ptr;
            int table_index;

            /* Interpolate upsampled signal and store in output array */
            for( index_Q16 = 0; index_Q16 < max_index_Q16; index_Q16 += index_increment_Q16 ) {
                table_index = silk_SMULWB( index_Q16 & 0xFFFF, 12 );
                buf_ptr = &buf[ index_Q16 >> 16 ];

                res_Q15 = silk_SMULBB(          buf_ptr[ 0 ], silk_resampler_frac_FIR_12[      table_index ][ 0 ] );
                res_Q15 = silk_SMLABB( res_Q15, buf_ptr[ 1 ], silk_resampler_frac_FIR_12[      table_index ][ 1 ] );
                res_Q15 = silk_SMLABB( res_Q15, buf_ptr[ 2 ], silk_resampler_frac_FIR_12[      table_index ][ 2 ] );
                res_Q15 = silk_SMLABB( res_Q15, buf_ptr[ 3 ], silk_resampler_frac_FIR_12[      table_index ][ 3 ] );
                res_Q15 = silk_SMLABB( res_Q15, buf_ptr[ 4 ], silk_resampler_frac_FIR_12[ 11 - table_index ][ 3 ] );
                res_Q15 = silk_SMLABB( res_Q15, buf_ptr[ 5 ], silk_resampler_frac_FIR_12[ 11 - table_index ][ 2 ] );
                res_Q15 = silk_SMLABB( res_Q15, buf_ptr[ 6 ], silk_resampler_frac_FIR_12[ 11 - table_index ][ 1 ] );
                res_Q15 = silk_SMLABB( res_Q15, buf_ptr[ 7 ], silk_resampler_frac_FIR_12[ 11 - table_index ][ 0 ] );
                *output++ = (short)silk_SAT16( silk_RSHIFT_ROUND( res_Q15, 15 ) );
            }
            return output;
        }
        /* Upsample using a combination of allpass-based 2x upsampling and FIR interpolation */
        internal static unsafe void silk_resampler_private_IIR_FIR(
            void                            *SS,            /* I/O  Resampler state             */
            short*                      output,          /* O    Output signal               */
            short*                input,           /* I    Input signal                */
            int                      inLen           /* I    Number of input samples     */
        )
        {
            silk_resampler_state_struct *S = (silk_resampler_state_struct *)SS;
            int nSamplesIn;
            int max_index_Q16, index_increment_Q16;
            short[] buf_data;
            buf_data = new short[2 * S->batchSize + RESAMPLER_ORDER_FIR_12];
            fixed (short* buf = buf_data)
            {
                /* Copy buffered samples to start of buffer */
                silk_memcpy(buf, S->sFIR_i16, RESAMPLER_ORDER_FIR_12 * sizeof(short));

                /* Iterate over blocks of frameSizeIn input samples */
                index_increment_Q16 = S->invRatio_Q16;
                while (true)
                {
                    nSamplesIn = silk_min(inLen, S->batchSize);

                    /* Upsample 2x */
                    silk_resampler_private_up2_HQ(S->sIIR, &buf[RESAMPLER_ORDER_FIR_12], input, nSamplesIn);

                    max_index_Q16 = silk_LSHIFT32(nSamplesIn, 16 + 1);         /* + 1 because 2x upsampling */
                    output = silk_resampler_private_IIR_FIR_INTERPOL(output, buf, max_index_Q16, index_increment_Q16);
                    input += nSamplesIn;
                    inLen -= nSamplesIn;

                    if (inLen > 0)
                    {
                        /* More iterations to do; copy last part of filtered signal to beginning of buffer */
                        silk_memcpy(buf, &buf[nSamplesIn << 1], RESAMPLER_ORDER_FIR_12 * sizeof(short));
                    }
                    else
                    {
                        break;
                    }
                }

                /* Copy last part of filtered signal to the state for the next call */
                silk_memcpy(S->sFIR_i16, &buf[nSamplesIn << 1], RESAMPLER_ORDER_FIR_12 * sizeof(short));
            }
        }

        /* Upsample by a factor 2, high quality */
        /* Uses 2nd order allpass filters for the 2x upsampling, followed by a      */
        /* notch filter just above Nyquist.                                         */
        internal static unsafe void silk_resampler_private_up2_HQ(
            int                      *S,             /* I/O  Resampler state [ 6 ]       */
            short                      * output,           /* O    Output signal [ 2 * len ]   */
            in short                * input,            /* I    Input signal [ len ]        */
            int                      len             /* I    Number of input samples     */
        )
        {
            int k;
            int in32, out32_1, out32_2, Y, X;

            silk_assert( silk_resampler_up2_hq_0[ 0 ] > 0 );
            silk_assert( silk_resampler_up2_hq_0[ 1 ] > 0 );
            silk_assert( silk_resampler_up2_hq_0[ 2 ] < 0 );
            silk_assert( silk_resampler_up2_hq_1[ 0 ] > 0 );
            silk_assert( silk_resampler_up2_hq_1[ 1 ] > 0 );
            silk_assert( silk_resampler_up2_hq_1[ 2 ] < 0 );

            /* Internal variables and state are in Q10 format */
            for( k = 0; k < len; k++ ) {
                /* Convert to Q10 */
                in32 = silk_LSHIFT( (int)input[ k ], 10 );

                /* First all-pass section for even output sample */
                Y       = silk_SUB32( in32, S[ 0 ] );
                X       = silk_SMULWB( Y, silk_resampler_up2_hq_0[ 0 ] );
                out32_1 = silk_ADD32( S[ 0 ], X );
                S[ 0 ]  = silk_ADD32( in32, X );

                /* Second all-pass section for even output sample */
                Y       = silk_SUB32( out32_1, S[ 1 ] );
                X       = silk_SMULWB( Y, silk_resampler_up2_hq_0[ 1 ] );
                out32_2 = silk_ADD32( S[ 1 ], X );
                S[ 1 ]  = silk_ADD32( out32_1, X );

                /* Third all-pass section for even output sample */
                Y       = silk_SUB32( out32_2, S[ 2 ] );
                X       = silk_SMLAWB( Y, Y, silk_resampler_up2_hq_0[ 2 ] );
                out32_1 = silk_ADD32( S[ 2 ], X );
                S[ 2 ]  = silk_ADD32( out32_2, X );

                /* Apply gain in Q15, convert back to int16 and store to output */
                output[ 2 * k ] = (short)silk_SAT16( silk_RSHIFT_ROUND( out32_1, 10 ) );

                /* First all-pass section for odd output sample */
                Y       = silk_SUB32( in32, S[ 3 ] );
                X       = silk_SMULWB( Y, silk_resampler_up2_hq_1[ 0 ] );
                out32_1 = silk_ADD32( S[ 3 ], X );
                S[ 3 ]  = silk_ADD32( in32, X );

                /* Second all-pass section for odd output sample */
                Y       = silk_SUB32( out32_1, S[ 4 ] );
                X       = silk_SMULWB( Y, silk_resampler_up2_hq_1[ 1 ] );
                out32_2 = silk_ADD32( S[ 4 ], X );
                S[ 4 ]  = silk_ADD32( out32_1, X );

                /* Third all-pass section for odd output sample */
                Y       = silk_SUB32( out32_2, S[ 5 ] );
                X       = silk_SMLAWB( Y, Y, silk_resampler_up2_hq_1[ 2 ] );
                out32_1 = silk_ADD32( S[ 5 ], X );
                S[ 5 ]  = silk_ADD32( out32_2, X );

                /* Apply gain in Q15, convert back to int16 and store to output */
                output[ 2 * k + 1 ] = (short)silk_SAT16( silk_RSHIFT_ROUND( out32_1, 10 ) );
            }
        }

        internal static unsafe void silk_resampler_private_up2_HQ_wrapper(
            void                            *SS,            /* I/O  Resampler state (unused)    */
            short                      * output,           /* O    Output signal [ 2 * len ]   */
            in short                * input,            /* I    Input signal [ len ]        */
            int                      len             /* I    Number of input samples     */
        )
        {
            silk_resampler_state_struct *S = (silk_resampler_state_struct *)SS;
            silk_resampler_private_up2_HQ( S->sIIR, output, input, len );
        }
    }
}
