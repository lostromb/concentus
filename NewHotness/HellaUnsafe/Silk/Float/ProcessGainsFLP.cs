using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Float.SigProcFLP;
using static HellaUnsafe.Silk.Float.StructsFLP;
using static HellaUnsafe.Silk.GainQuant;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.TuningParameters;
using static HellaUnsafe.Silk.Tables;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class ProcessGainsFLP
    {
        /* Processing of gains */
        internal static unsafe void silk_process_gains_FLP(
            silk_encoder_state_FLP          *psEnc,                             /* I/O  Encoder state FLP                           */
            silk_encoder_control_FLP        *psEncCtrl,                         /* I/O  Encoder control FLP                         */
            int                        condCoding                          /* I    The type of conditional coding to use       */
        )
        {
            silk_shape_state_FLP *psShapeSt = &psEnc->sShape;
            int     k;
            int*   pGains_Q16 = stackalloc int[ MAX_NB_SUBFR ];
            float   s, InvMaxSqrVal, gain, quant_offset;

            /* Gain reduction when LTP coding gain is high */
            if( psEnc->sCmn.indices.signalType == TYPE_VOICED ) {
                s = 1.0f - 0.5f * silk_sigmoid( 0.25f * ( psEncCtrl->LTPredCodGain - 12.0f ) );
                for( k = 0; k < psEnc->sCmn.nb_subfr; k++ ) {
                    psEncCtrl->Gains[ k ] *= s;
                }
            }

            /* Limit the quantized signal */
            InvMaxSqrVal = ( float )( pow( 2.0f, 0.33f * ( 21.0f - psEnc->sCmn.SNR_dB_Q7 * ( 1 / 128.0f ) ) ) / psEnc->sCmn.subfr_length );

            for( k = 0; k < psEnc->sCmn.nb_subfr; k++ ) {
                /* Soft limit on ratio residual energy and squared gains */
                gain = psEncCtrl->Gains[ k ];
                gain = ( float )sqrt( gain * gain + psEncCtrl->ResNrg[ k ] * InvMaxSqrVal );
                psEncCtrl->Gains[ k ] = silk_min_float( gain, 32767.0f );
            }

            /* Prepare gains for noise shaping quantization */
            for( k = 0; k < psEnc->sCmn.nb_subfr; k++ ) {
                pGains_Q16[ k ] = (int)( psEncCtrl->Gains[ k ] * 65536.0f );
            }

            /* Save unquantized gains and gain Index */
            silk_memcpy( psEncCtrl->GainsUnq_Q16, pGains_Q16, psEnc->sCmn.nb_subfr * sizeof( int ) );
            psEncCtrl->lastGainIndexPrev = psShapeSt->LastGainIndex;

            /* Quantize gains */
            silk_gains_quant( psEnc->sCmn.indices.GainsIndices, pGains_Q16,
                    &psShapeSt->LastGainIndex, BOOL2INT(condCoding == CODE_CONDITIONALLY), psEnc->sCmn.nb_subfr );

            /* Overwrite unquantized gains with quantized gains and convert back to Q0 from Q16 */
            for( k = 0; k < psEnc->sCmn.nb_subfr; k++ ) {
                psEncCtrl->Gains[ k ] = pGains_Q16[ k ] / 65536.0f;
            }

            /* Set quantizer offset for voiced signals. Larger offset when LTP coding gain is low or tilt is high (ie low-pass) */
            if( psEnc->sCmn.indices.signalType == TYPE_VOICED ) {
                if( psEncCtrl->LTPredCodGain + psEnc->sCmn.input_tilt_Q15 * ( 1.0f / 32768.0f ) > 1.0f ) {
                    psEnc->sCmn.indices.quantOffsetType = 0;
                } else {
                    psEnc->sCmn.indices.quantOffsetType = 1;
                }
            }

            /* Quantizer boundary adjustment */
            quant_offset = silk_Quantization_Offsets_Q10[ psEnc->sCmn.indices.signalType >> 1 ][ psEnc->sCmn.indices.quantOffsetType ] / 1024.0f;
            psEncCtrl->Lambda = LAMBDA_OFFSET
                              + LAMBDA_DELAYED_DECISIONS * psEnc->sCmn.nStatesDelayedDecision
                              + LAMBDA_SPEECH_ACT        * psEnc->sCmn.speech_activity_Q8 * ( 1.0f /  256.0f )
                              + LAMBDA_INPUT_QUALITY     * psEncCtrl->input_quality
                              + LAMBDA_CODING_QUALITY    * psEncCtrl->coding_quality
                              + LAMBDA_QUANT_OFFSET      * quant_offset;

            silk_assert( psEncCtrl->Lambda > 0.0f );
            silk_assert( psEncCtrl->Lambda < 2.0f );
        }
    }
}
