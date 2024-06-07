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
using static HellaUnsafe.Celt.EntDec;
using static HellaUnsafe.Celt.EntEnc;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Tables;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk
{
    /* shell coder; pulse-subframe length is hardcoded */
    internal static class ShellCoder
    {
        internal static unsafe void combine_pulses(
            int         * output,   /* O    combined pulses vector [len] */
            in int*    input,    /* I    input vector       [2 * len] */
            in int   len     /* I    number of OUTPUT samples     */
        )
        {
            int k;
            for( k = 0; k < len; k++ ) {
                output[ k ] = input[ 2 * k ] + input[ 2 * k + 1 ];
            }
        }

        internal static unsafe void encode_split(
            ref ec_ctx          psRangeEnc,    /* I/O  compressor data structure                   */
            in byte* ecbuf ,
            in int              p_child1,       /* I    pulse amplitude of first child subframe     */
            in int              p,              /* I    pulse amplitude of current subframe         */
            in byte*            shell_table    /* I    table of shell cdfs */
        )
        {
            if( p > 0 ) {
                ec_enc_icdf(ref psRangeEnc, ecbuf, p_child1, shell_table + silk_shell_code_table_offsets[ p ], 8 );
            }
        }

        internal static unsafe void decode_split(
            short                  *p_child1,      /* O    pulse amplitude of first child subframe     */
            short* p_child2,      /* O    pulse amplitude of second child subframe    */
            ref ec_ctx                      psRangeDec,    /* I/O  Compressor data structure                   */
            in byte* ecbuf ,
            in int              p,              /* I    pulse amplitude of current subframe         */
            in byte            * shell_table    /* I    table of shell cdfs */    
        )
        {
            if( p > 0 ) {
                p_child1[ 0 ] = (short)ec_dec_icdf(ref psRangeDec, ecbuf, shell_table + silk_shell_code_table_offsets[ p ], 8 );
                p_child2[ 0 ] = (short)(p - p_child1[ 0 ]);
            } else {
                p_child1[ 0 ] = 0;
                p_child2[ 0 ] = 0;
            }
        }

        /* Shell encoder, operates on one shell code frame of 16 pulses */
        internal static unsafe void silk_shell_encoder(
            ref ec_ctx                      psRangeEnc,                    /* I/O  compressor data structure                   */
            in byte* ecbuf,
            in int              *pulses0                        /* I    data: nonnegative pulse amplitudes          */
        )
        {
            Span<int> pulses1_array = stackalloc int[8];
            Span<int> pulses2_array = stackalloc int[4];
            Span<int> pulses3_array = stackalloc int[2];
            Span<int> pulses4_array = stackalloc int[1];

            fixed (int* pulses1 = pulses1_array)
            fixed (int* pulses2 = pulses2_array)
            fixed (int* pulses3 = pulses3_array)
            fixed (int* pulses4 = pulses4_array)
            fixed (byte* shelltable0 = silk_shell_code_table0)
            fixed (byte* shelltable1 = silk_shell_code_table1)
            fixed (byte* shelltable2 = silk_shell_code_table2)
            fixed (byte* shelltable3 = silk_shell_code_table3)
            {
                /* this function operates on one shell code frame of 16 pulses */
                silk_assert(SHELL_CODEC_FRAME_LENGTH == 16);

                /* tree representation per pulse-subframe */
                combine_pulses(pulses1, pulses0, 8);
                combine_pulses(pulses2, pulses1, 4);
                combine_pulses(pulses3, pulses2, 2);
                combine_pulses(pulses4, pulses3, 1);

                encode_split(ref psRangeEnc, ecbuf, pulses3[0], pulses4[0], shelltable3);

                encode_split(ref psRangeEnc, ecbuf, pulses2[0], pulses3[0], shelltable2);

                encode_split(ref psRangeEnc, ecbuf, pulses1[0], pulses2[0], shelltable1);
                encode_split(ref psRangeEnc, ecbuf, pulses0[0], pulses1[0], shelltable0);
                encode_split(ref psRangeEnc, ecbuf, pulses0[2], pulses1[1], shelltable0);

                encode_split(ref psRangeEnc, ecbuf, pulses1[2], pulses2[1], shelltable1);
                encode_split(ref psRangeEnc, ecbuf, pulses0[4], pulses1[2], shelltable0);
                encode_split(ref psRangeEnc, ecbuf, pulses0[6], pulses1[3], shelltable0);

                encode_split(ref psRangeEnc, ecbuf, pulses2[2], pulses3[1], shelltable2);

                encode_split(ref psRangeEnc, ecbuf, pulses1[4], pulses2[2], shelltable1);
                encode_split(ref psRangeEnc, ecbuf, pulses0[8], pulses1[4], shelltable0);
                encode_split(ref psRangeEnc, ecbuf, pulses0[10], pulses1[5], shelltable0);

                encode_split(ref psRangeEnc, ecbuf, pulses1[6], pulses2[3], shelltable1);
                encode_split(ref psRangeEnc, ecbuf, pulses0[12], pulses1[6], shelltable0);
                encode_split(ref psRangeEnc, ecbuf, pulses0[14], pulses1[7], shelltable0);
            }
        }


        /* Shell decoder, operates on one shell code frame of 16 pulses */
        internal static unsafe void silk_shell_decoder(
            short* pulses0,                       /* O    data: nonnegative pulse amplitudes          */
            ref ec_ctx psRangeDec,                    /* I/O  Compressor data structure                   */
            in byte* ecbuf,
            int pulses4                         /* I    number of pulses per pulse-subframe         */
        )
        {
            Span<short> pulses1_array = stackalloc short[8];
            Span<short> pulses2_array = stackalloc short[4];
            Span<short> pulses3_array = stackalloc short[2];

            fixed (short* pulses1 = pulses1_array)
            fixed (short* pulses2 = pulses2_array)
            fixed (short* pulses3 = pulses3_array)
            fixed (byte* shelltable0 = silk_shell_code_table0)
            fixed (byte* shelltable1 = silk_shell_code_table1)
            fixed (byte* shelltable2 = silk_shell_code_table2)
            fixed (byte* shelltable3 = silk_shell_code_table3)
            {
                /* this function operates on one shell code frame of 16 pulses */
                silk_assert(SHELL_CODEC_FRAME_LENGTH == 16);

                decode_split(&pulses3[0], &pulses3[1], ref psRangeDec, ecbuf, pulses4, shelltable3);

                decode_split(&pulses2[0], &pulses2[1], ref psRangeDec, ecbuf, pulses3[0], shelltable2);

                decode_split(&pulses1[0], &pulses1[1], ref psRangeDec, ecbuf, pulses2[0], shelltable1);
                decode_split(&pulses0[0], &pulses0[1], ref psRangeDec, ecbuf, pulses1[0], shelltable0);
                decode_split(&pulses0[2], &pulses0[3], ref psRangeDec, ecbuf, pulses1[1], shelltable0);

                decode_split(&pulses1[2], &pulses1[3], ref psRangeDec, ecbuf, pulses2[1], shelltable1);
                decode_split(&pulses0[4], &pulses0[5], ref psRangeDec, ecbuf, pulses1[2], shelltable0);
                decode_split(&pulses0[6], &pulses0[7], ref psRangeDec, ecbuf, pulses1[3], shelltable0);

                decode_split(&pulses2[2], &pulses2[3], ref psRangeDec, ecbuf, pulses3[1], shelltable2);

                decode_split(&pulses1[4], &pulses1[5], ref psRangeDec, ecbuf, pulses2[2], shelltable1);
                decode_split(&pulses0[8], &pulses0[9], ref psRangeDec, ecbuf, pulses1[4], shelltable0);
                decode_split(&pulses0[10], &pulses0[11], ref psRangeDec, ecbuf, pulses1[5], shelltable0);

                decode_split(&pulses1[6], &pulses1[7], ref psRangeDec, ecbuf, pulses2[3], shelltable1);
                decode_split(&pulses0[12], &pulses0[13], ref psRangeDec, ecbuf, pulses1[6], shelltable0);
                decode_split(&pulses0[14], &pulses0[15], ref psRangeDec, ecbuf, pulses1[7], shelltable0);
            }
        }
    }
}
