using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /************************/
    /* Encoder control FIX  */
    /************************/
    public class SilkEncoderControl
    {
        /* Prediction and coding parameters */
        public readonly Pointer<int> Gains_Q16 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        // [porting note] originally a 2D array of [2][MAX_LPC_ORDER], now linearized
        public readonly Pointer<short> PredCoef_Q12 = Pointer.Malloc<short>(2 * SilkConstants.MAX_LPC_ORDER);     /* holds interpolated and final coefficients */
        public readonly Pointer<short> LTPCoef_Q14 = Pointer.Malloc<short>(SilkConstants.LTP_ORDER * SilkConstants.MAX_NB_SUBFR);
        public int LTP_scale_Q14 = 0;
        public readonly Pointer<int> pitchL = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);

        /* Noise shaping parameters */
        public readonly Pointer<short> AR1_Q13 = Pointer.Malloc<short>(SilkConstants.MAX_NB_SUBFR * SilkConstants.MAX_SHAPE_LPC_ORDER);
        public readonly Pointer<short> AR2_Q13 = Pointer.Malloc<short>(SilkConstants.MAX_NB_SUBFR * SilkConstants.MAX_SHAPE_LPC_ORDER);
        public readonly Pointer<int> LF_shp_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR); /* Packs two int16 coefficients per int32 value         */
        public readonly Pointer<int> GainsPre_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        public readonly Pointer<int> HarmBoost_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        public readonly Pointer<int> Tilt_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        public readonly Pointer<int> HarmShapeGain_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        public int Lambda_Q10 = 0;
        public int input_quality_Q14 = 0;
        public int coding_quality_Q14 = 0;

        /* Measures */
        public int sparseness_Q8 = 0;
        public int predGain_Q16 = 0;
        public int LTPredCodGain_Q7 = 0;

        /* Residual energy per subframe */
        public readonly Pointer<int> ResNrg = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);

        /* Q domain for the residual energy > 0                 */
        public readonly Pointer<int> ResNrgQ = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);

        /* Parameters for CBR mode */
        public readonly Pointer<int> GainsUnq_Q16 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        public sbyte lastGainIndexPrev = 0;

        public void Reset()
        {
            Gains_Q16.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            PredCoef_Q12.MemSet(0, 2 * SilkConstants.MAX_LPC_ORDER);
            LTPCoef_Q14.MemSet(0, SilkConstants.LTP_ORDER * SilkConstants.MAX_NB_SUBFR);
            LTP_scale_Q14 = 0;
            pitchL.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            AR1_Q13.MemSet(0, SilkConstants.MAX_NB_SUBFR * SilkConstants.MAX_SHAPE_LPC_ORDER);
            AR2_Q13.MemSet(0, SilkConstants.MAX_NB_SUBFR * SilkConstants.MAX_SHAPE_LPC_ORDER);
            LF_shp_Q14.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            GainsPre_Q14.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            HarmBoost_Q14.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            Tilt_Q14.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            HarmShapeGain_Q14.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            Lambda_Q10 = 0;
            input_quality_Q14 = 0;
            coding_quality_Q14 = 0;
            sparseness_Q8 = 0;
            predGain_Q16 = 0;
            LTPredCodGain_Q7 = 0;
            ResNrg.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            ResNrgQ.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            GainsUnq_Q16.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            lastGainIndexPrev = 0;
        }
    }
}
