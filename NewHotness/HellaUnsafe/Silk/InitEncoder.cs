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
using static HellaUnsafe.Silk.Control;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.EncodeIndices;
using static HellaUnsafe.Silk.EncodePulses;
using static HellaUnsafe.Silk.Errors;
using static HellaUnsafe.Silk.Float.StructsFLP;
using static HellaUnsafe.Silk.Resampler;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.StereoDecodePred;
using static HellaUnsafe.Silk.StereoLRToMS;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.Tables;
using static HellaUnsafe.Silk.TuningParameters;
using static HellaUnsafe.Silk.Inlines;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.VAD;

namespace HellaUnsafe.Silk
{
    internal static unsafe class InitEncoder
    {
        /*********************************/
        /* Initialize Silk Encoder state */
        /*********************************/
        internal static unsafe int silk_init_encoder(
            silk_encoder_state_FLP          *psEnc                                 /* I/O  Pointer to Silk FIX encoder state                                           */
        )
        {
            int ret = 0;

            /* Clear the entire encoder state */
            //silk_memset( psEnc, 0, sizeof(silk_encoder_state_FLP) );
            *psEnc = new silk_encoder_state_FLP();

            psEnc->sCmn.variable_HP_smth1_Q15 = silk_LSHIFT( silk_lin2log( SILK_FIX_CONST( VARIABLE_HP_MIN_CUTOFF_HZ, 16 ) ) - ( 16 << 7 ), 8 );
            psEnc->sCmn.variable_HP_smth2_Q15 = psEnc->sCmn.variable_HP_smth1_Q15;

            /* Used to deactivate LSF interpolation, pitch prediction */
            psEnc->sCmn.first_frame_after_reset = 1;

            /* Initialize Silk VAD */
            ret += silk_VAD_Init( &psEnc->sCmn.sVAD );

            return  ret;
        }
    }
}
