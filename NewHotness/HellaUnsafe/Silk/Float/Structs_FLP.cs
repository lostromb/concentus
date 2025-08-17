using HellaUnsafe.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Structs;

namespace HellaUnsafe.Silk.Float
{
    internal unsafe static class Structs_FLP
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
            private fixed float _PredCoef[2 * MAX_LPC_ORDER];     /* holds interpolated and final coefficients */

            public Native2DArray<float> PredCoef => new Native2DArray<float>(2, MAX_LPC_ORDER, (float*)Unsafe.AsPointer(ref _PredCoef[0]));

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

        [System.Runtime.CompilerServices.InlineArray(ENCODER_NUM_CHANNELS)]
        public struct silk_encoder_state_FLPArray
        {
            private silk_encoder_state_FLP _element;
        }

        /************************/
        /* Encoder Super Struct */
        /************************/
        internal unsafe struct silk_encoder
        {
            internal silk_encoder_state_FLPArray state_Fxx;
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
}
