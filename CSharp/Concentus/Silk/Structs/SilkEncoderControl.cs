/* Copyright (c) 2006-2011 Skype Limited. All Rights Reserved
   Ported to C# by Logan Stromberg

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

namespace Concentus.Silk.Structs
{
    using Concentus.Common;
    using Concentus.Common.CPlusPlus;
    using Concentus.Silk.Enums;

    /************************/
    /* Encoder control FIX  */
    /************************/
    internal class SilkEncoderControl
    {
        /* Prediction and coding parameters */
        internal readonly Pointer<int> Gains_Q16 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        // [porting note] originally a 2D array of [2][MAX_LPC_ORDER], now linearized
        internal readonly Pointer<short> PredCoef_Q12 = Pointer.Malloc<short>(2 * SilkConstants.MAX_LPC_ORDER);     /* holds interpolated and final coefficients */
        internal readonly Pointer<short> LTPCoef_Q14 = Pointer.Malloc<short>(SilkConstants.LTP_ORDER * SilkConstants.MAX_NB_SUBFR);
        internal int LTP_scale_Q14 = 0;
        internal readonly Pointer<int> pitchL = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);

        /* Noise shaping parameters */
        internal readonly Pointer<short> AR1_Q13 = Pointer.Malloc<short>(SilkConstants.MAX_NB_SUBFR * SilkConstants.MAX_SHAPE_LPC_ORDER);
        internal readonly Pointer<short> AR2_Q13 = Pointer.Malloc<short>(SilkConstants.MAX_NB_SUBFR * SilkConstants.MAX_SHAPE_LPC_ORDER);
        internal readonly Pointer<int> LF_shp_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR); /* Packs two int16 coefficients per int32 value         */
        internal readonly Pointer<int> GainsPre_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        internal readonly Pointer<int> HarmBoost_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        internal readonly Pointer<int> Tilt_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        internal readonly Pointer<int> HarmShapeGain_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        internal int Lambda_Q10 = 0;
        internal int input_quality_Q14 = 0;
        internal int coding_quality_Q14 = 0;

        /* Measures */
        internal int sparseness_Q8 = 0;
        internal int predGain_Q16 = 0;
        internal int LTPredCodGain_Q7 = 0;

        /* Residual energy per subframe */
        internal readonly Pointer<int> ResNrg = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);

        /* Q domain for the residual energy > 0                 */
        internal readonly Pointer<int> ResNrgQ = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);

        /* Parameters for CBR mode */
        internal readonly Pointer<int> GainsUnq_Q16 = Pointer.Malloc<int>(SilkConstants.MAX_NB_SUBFR);
        internal sbyte lastGainIndexPrev = 0;

        internal void Reset()
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
