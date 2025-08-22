﻿/***********************************************************************
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

using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Float.StructsFLP;
using static HellaUnsafe.Silk.Inlines;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.TuningParameters;
using static HellaUnsafe.Silk.Lin2Log;
using static HellaUnsafe.Silk.Log2Lin;

namespace HellaUnsafe.Silk
{
    internal static unsafe class HPVariableCutoff
    {
        /* High-pass filter with cutoff frequency adaptation based on pitch lag statistics */
        internal static unsafe void silk_HP_variable_cutoff(
            silk_encoder_state_FLP*          state_Fxx                         /* I/O  Encoder states                              */
        )
        {
           int   quality_Q15;
           int pitch_freq_Hz_Q16, pitch_freq_log_Q7, delta_freq_Q7;
           silk_encoder_state *psEncC1 = &state_Fxx[ 0 ].sCmn;

           /* Adaptive cutoff frequency: estimate low end of pitch frequency range */
           if( psEncC1->prevSignalType == TYPE_VOICED ) {
              /* difference, in log domain */
              pitch_freq_Hz_Q16 = silk_DIV32_16( silk_LSHIFT( silk_MUL( psEncC1->fs_kHz, 1000 ), 16 ), psEncC1->prevLag );
              pitch_freq_log_Q7 = silk_lin2log( pitch_freq_Hz_Q16 ) - ( 16 << 7 );

              /* adjustment based on quality */
              quality_Q15 = psEncC1->input_quality_bands_Q15[ 0 ];
              pitch_freq_log_Q7 = silk_SMLAWB( pitch_freq_log_Q7, silk_SMULWB( silk_LSHIFT( -quality_Q15, 2 ), quality_Q15 ),
                    pitch_freq_log_Q7 - ( silk_lin2log( /*SILK_FIX_CONST*/((int)( VARIABLE_HP_MIN_CUTOFF_HZ * ((long)1 <<  16 ) + 0.5)) ) - ( 16 << 7 ) ) );

              /* delta_freq = pitch_freq_log - psEnc->variable_HP_smth1; */
              delta_freq_Q7 = pitch_freq_log_Q7 - silk_RSHIFT( psEncC1->variable_HP_smth1_Q15, 8 );
              if( delta_freq_Q7 < 0 ) {
                 /* less smoothing for decreasing pitch frequency, to track something close to the minimum */
                 delta_freq_Q7 = silk_MUL( delta_freq_Q7, 3 );
              }

              /* limit delta, to reduce impact of outliers in pitch estimation */
              delta_freq_Q7 = silk_LIMIT_32( delta_freq_Q7, -/*SILK_FIX_CONST*/((int)( VARIABLE_HP_MAX_DELTA_FREQ * ((long)1 <<  7 ) + 0.5)), /*SILK_FIX_CONST*/((int)( VARIABLE_HP_MAX_DELTA_FREQ * ((long)1 <<  7 ) + 0.5)) );

              /* update smoother */
              psEncC1->variable_HP_smth1_Q15 = silk_SMLAWB( psEncC1->variable_HP_smth1_Q15,
                    silk_SMULBB( psEncC1->speech_activity_Q8, delta_freq_Q7 ), /*SILK_FIX_CONST*/((int)( VARIABLE_HP_SMTH_COEF1 * ((long)1 <<  16 ) + 0.5)) );

              /* limit frequency range */
              psEncC1->variable_HP_smth1_Q15 = silk_LIMIT_32( psEncC1->variable_HP_smth1_Q15,
                    silk_LSHIFT( silk_lin2log( VARIABLE_HP_MIN_CUTOFF_HZ ), 8 ),
                    silk_LSHIFT( silk_lin2log( VARIABLE_HP_MAX_CUTOFF_HZ ), 8 ) );
           }
        }
    }
}
