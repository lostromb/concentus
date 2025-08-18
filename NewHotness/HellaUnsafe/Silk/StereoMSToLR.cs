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

using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;

namespace HellaUnsafe.Silk
{
    internal static unsafe class StereoMSToLR
    {
        /* Convert adaptive Mid/Side representation to Left/Right stereo signal */
        internal static unsafe void silk_stereo_MS_to_LR(
            stereo_dec_state            *state,                         /* I/O  State                                       */
            short*                  x1,                           /* I/O  Left input signal, becomes mid signal       */
            short*                  x2,                           /* I/O  Right input signal, becomes side signal     */
            in int*            pred_Q13,                     /* I    Predictors                                  */
            int                    fs_kHz,                         /* I    Samples rate (kHz)                          */
            int                    frame_length                    /* I    Number of samples                           */
        )
        {
            int   n, denom_Q16, delta0_Q13, delta1_Q13;
            int sum, diff, pred0_Q13, pred1_Q13;

            /* Buffering */
            silk_memcpy( x1, state->sMid,  2 * sizeof( short ) );
            silk_memcpy( x2, state->sSide, 2 * sizeof( short ) );
            silk_memcpy( state->sMid,  &x1[ frame_length ], 2 * sizeof( short ) );
            silk_memcpy( state->sSide, &x2[ frame_length ], 2 * sizeof( short ) );

            /* Interpolate predictors and add prediction to side channel */
            pred0_Q13  = state->pred_prev_Q13[ 0 ];
            pred1_Q13  = state->pred_prev_Q13[ 1 ];
            denom_Q16  = silk_DIV32_16( (int)1 << 16, STEREO_INTERP_LEN_MS * fs_kHz );
            delta0_Q13 = silk_RSHIFT_ROUND( silk_SMULBB( pred_Q13[ 0 ] - state->pred_prev_Q13[ 0 ], denom_Q16 ), 16 );
            delta1_Q13 = silk_RSHIFT_ROUND( silk_SMULBB( pred_Q13[ 1 ] - state->pred_prev_Q13[ 1 ], denom_Q16 ), 16 );
            for( n = 0; n < STEREO_INTERP_LEN_MS * fs_kHz; n++ ) {
                pred0_Q13 += delta0_Q13;
                pred1_Q13 += delta1_Q13;
                sum = silk_LSHIFT( silk_ADD_LSHIFT32( x1[ n ] + (int)x1[ n + 2 ], x1[ n + 1 ], 1 ), 9 );       /* Q11 */
                sum = silk_SMLAWB( silk_LSHIFT( (int)x2[ n + 1 ], 8 ), sum, pred0_Q13 );         /* Q8  */
                sum = silk_SMLAWB( sum, silk_LSHIFT( (int)x1[ n + 1 ], 11 ), pred1_Q13 );        /* Q8  */
                x2[ n + 1 ] = (short)silk_SAT16( silk_RSHIFT_ROUND( sum, 8 ) );
            }
            pred0_Q13 = pred_Q13[ 0 ];
            pred1_Q13 = pred_Q13[ 1 ];
            for( n = STEREO_INTERP_LEN_MS * fs_kHz; n < frame_length; n++ ) {
                sum = silk_LSHIFT( silk_ADD_LSHIFT32( x1[ n ] + (int)x1[ n + 2 ], x1[ n + 1 ], 1 ), 9 );       /* Q11 */
                sum = silk_SMLAWB( silk_LSHIFT( (int)x2[ n + 1 ], 8 ), sum, pred0_Q13 );         /* Q8  */
                sum = silk_SMLAWB( sum, silk_LSHIFT( (int)x1[ n + 1 ], 11 ), pred1_Q13 );        /* Q8  */
                x2[ n + 1 ] = (short)silk_SAT16( silk_RSHIFT_ROUND( sum, 8 ) );
            }
            state->pred_prev_Q13[ 0 ] = (short)pred_Q13[ 0 ];
            state->pred_prev_Q13[ 1 ] = (short)pred_Q13[ 1 ];

            /* Convert to left/right signals */
            for( n = 0; n < frame_length; n++ ) {
                sum  = x1[ n + 1 ] + (int)x2[ n + 1 ];
                diff = x1[ n + 1 ] - (int)x2[ n + 1 ];
                x1[ n + 1 ] = (short)silk_SAT16( sum );
                x2[ n + 1 ] = (short)silk_SAT16( diff );
            }
        }
    }
}
