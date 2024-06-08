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

using HellaUnsafe.Common;
using System.Runtime.CompilerServices;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.TuningParameters;
using static HellaUnsafe.Silk.ResamplerStructs;
using static HellaUnsafe.Celt.KissFFT;

namespace HellaUnsafe.Silk.Float
{
    /********************************/
    /* Noise shaping analysis state */
    /********************************/
    internal unsafe struct silk_shape_state_FLP
    {
        internal sbyte LastGainIndex;
        internal float HarmShapeGain_smth;
        internal float Tilt_smth;
    }

    /********************************/
    /* Encoder state FLP            */
    /********************************/
    internal unsafe struct silk_encoder_state_FLP
    {
        internal silk_encoder_state sCmn;                               /* Common struct, shared with fixed-point code */
        internal silk_shape_state_FLP sShape;                             /* Noise shaping state */

        /* Buffer for find pitch and noise shape analysis */
        internal fixed float x_buf[2 * MAX_FRAME_LENGTH + LA_SHAPE_MAX];/* Buffer for find pitch and noise shape analysis */
        internal float LTPCorr;                            /* Normalized correlation from pitch lag estimator */
    }

    /************************/
    /* Encoder control FLP  */
    /************************/
    internal unsafe struct silk_encoder_control_FLP
    {
        /* Prediction and coding parameters */
        internal fixed float Gains[MAX_NB_SUBFR];
        // Porting note: 2D array
        internal fixed float PredCoef_2D[2 * MAX_LPC_ORDER];     /* holds interpolated and final coefficients */
        internal fixed float LTPCoef[LTP_ORDER * MAX_NB_SUBFR];
        internal float LTP_scale;
        internal fixed int pitchL[MAX_NB_SUBFR];

        /* Noise shaping parameters */
        internal fixed float AR[MAX_NB_SUBFR * MAX_SHAPE_LPC_ORDER];
        internal fixed float LF_MA_shp[MAX_NB_SUBFR];
        internal fixed float LF_AR_shp[MAX_NB_SUBFR];
        internal fixed float Tilt[MAX_NB_SUBFR];
        internal fixed float HarmShapeGain[MAX_NB_SUBFR];
        internal float Lambda;
        internal float input_quality;
        internal float coding_quality;

        /* Measures */
        internal float predGain;
        internal float LTPredCodGain;
        internal fixed float ResNrg[MAX_NB_SUBFR];             /* Residual energy per subframe */

        /* Parameters for CBR mode */
        internal fixed int GainsUnq_Q16[MAX_NB_SUBFR];
        internal sbyte lastGainIndexPrev;
    }

    /************************/
    /* Encoder Super Struct */
    /************************/
    internal unsafe struct silk_encoder
    {
        [InlineArray(ENCODER_NUM_CHANNELS)]
        internal unsafe struct encoder_state_array
        {
            internal silk_encoder_state_FLP element;
        }

        internal encoder_state_array _state_Fxx_storage;
        internal silk_encoder_state_FLP* state_Fxx => (silk_encoder_state_FLP*)Unsafe.AsPointer(ref _state_Fxx_storage);
        internal stereo_enc_state sStereo;
        internal int nBitsUsedLBRR;
        internal int nBitsExceeded;
        internal int nChannelsAPI;
        internal int nChannelsInternal;
        internal int nPrevChannelsInternal;
        internal int timeSinceSwitchAllowed_ms;
        internal int allowBandwidthSwitch;
        internal int prev_decode_only_middle;
    }
}
