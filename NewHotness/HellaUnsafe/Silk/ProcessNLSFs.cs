using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.NLSF2A;
using static HellaUnsafe.Silk.NLSFEncode;
using static HellaUnsafe.Silk.NLSFVQWeightsLaroia;
using static HellaUnsafe.Silk.Interpolate;
using static HellaUnsafe.Silk.Macros;
using HellaUnsafe.Common;

namespace HellaUnsafe.Silk
{
    internal static unsafe class ProcessNLSFs
    {
        /* Limit, stabilize, convert and quantize NLSFs */
        internal static unsafe void silk_process_NLSFs(
            silk_encoder_state          *psEncC,                            /* I/O  Encoder state                               */
            Native2DArray<short>                  PredCoef_Q12/*[ 2 ][ MAX_LPC_ORDER ]*/, /* O    Prediction coefficients                     */
            short*                  pNLSF_Q15/*[         MAX_LPC_ORDER ]*/, /* I/O  Normalized LSFs (quant out) (0 - (2^15-1))  */
            in short*            prev_NLSFq_Q15/*[    MAX_LPC_ORDER ]*/  /* I    Previous Normalized LSFs (0 - (2^15-1))     */
        )
        {
            int     i, doInterpolate;
            int     NLSF_mu_Q20;
            short   i_sqr_Q15;
            short*   pNLSF0_temp_Q15 = stackalloc short[ MAX_LPC_ORDER ];
            short* pNLSFW_QW = stackalloc short[ MAX_LPC_ORDER ];
            short* pNLSFW0_temp_QW = stackalloc short[ MAX_LPC_ORDER ];

            silk_assert( psEncC->speech_activity_Q8 >=   0 );
            silk_assert( psEncC->speech_activity_Q8 <= SILK_FIX_CONST( 1.0, 8 ) );
            celt_assert( psEncC->useInterpolatedNLSFs == 1 || psEncC->indices.NLSFInterpCoef_Q2 == ( 1 << 2 ) );

            /***********************/
            /* Calculate mu values */
            /***********************/
            /* NLSF_mu  = 0.003 - 0.0015 * psEnc->speech_activity; */
            NLSF_mu_Q20 = silk_SMLAWB( SILK_FIX_CONST( 0.003, 20 ), SILK_FIX_CONST( -0.001, 28 ), psEncC->speech_activity_Q8 );
            if( psEncC->nb_subfr == 2 ) {
                /* Multiply by 1.5 for 10 ms packets */
                NLSF_mu_Q20 = silk_ADD_RSHIFT( NLSF_mu_Q20, NLSF_mu_Q20, 1 );
            }

            celt_assert( NLSF_mu_Q20 >  0 );
            silk_assert( NLSF_mu_Q20 <= SILK_FIX_CONST( 0.005, 20 ) );

            /* Calculate NLSF weights */
            silk_NLSF_VQ_weights_laroia( pNLSFW_QW, pNLSF_Q15, psEncC->predictLPCOrder );

            /* Update NLSF weights for interpolated NLSFs */
            doInterpolate = BOOL2INT(( psEncC->useInterpolatedNLSFs == 1 ) && ( psEncC->indices.NLSFInterpCoef_Q2 < 4 ));
            if( doInterpolate != 0) {
                /* Calculate the interpolated NLSF vector for the first half */
                silk_interpolate( pNLSF0_temp_Q15, prev_NLSFq_Q15, pNLSF_Q15,
                    psEncC->indices.NLSFInterpCoef_Q2, psEncC->predictLPCOrder );

                /* Calculate first half NLSF weights for the interpolated NLSFs */
                silk_NLSF_VQ_weights_laroia( pNLSFW0_temp_QW, pNLSF0_temp_Q15, psEncC->predictLPCOrder );

                /* Update NLSF weights with contribution from first half */
                i_sqr_Q15 = (short)silk_LSHIFT( silk_SMULBB( psEncC->indices.NLSFInterpCoef_Q2, psEncC->indices.NLSFInterpCoef_Q2 ), 11 );
                for( i = 0; i < psEncC->predictLPCOrder; i++ ) {
                    pNLSFW_QW[ i ] = silk_ADD16( (short)silk_RSHIFT( pNLSFW_QW[ i ], 1 ), (short)silk_RSHIFT(
                          silk_SMULBB( pNLSFW0_temp_QW[ i ], i_sqr_Q15 ), 16) );
                    silk_assert( pNLSFW_QW[ i ] >= 1 );
                }
            }

            silk_NLSF_encode( psEncC->indices.NLSFIndices, pNLSF_Q15, psEncC->psNLSF_CB, pNLSFW_QW,
                NLSF_mu_Q20, psEncC->NLSF_MSVQ_Survivors, psEncC->indices.signalType );

            /* Convert quantized NLSFs back to LPC coefficients */
            silk_NLSF2A( PredCoef_Q12[ 1 ], pNLSF_Q15, psEncC->predictLPCOrder );

            if( doInterpolate != 0) {
                /* Calculate the interpolated, quantized LSF vector for the first half */
                silk_interpolate( pNLSF0_temp_Q15, prev_NLSFq_Q15, pNLSF_Q15,
                    psEncC->indices.NLSFInterpCoef_Q2, psEncC->predictLPCOrder );

                /* Convert back to LPC coefficients */
                silk_NLSF2A( PredCoef_Q12[ 0 ], pNLSF0_temp_Q15, psEncC->predictLPCOrder );

            } else {
                /* Copy LPC coefficients for first half from second half */
                celt_assert( psEncC->predictLPCOrder <= MAX_LPC_ORDER );
                silk_memcpy( PredCoef_Q12[ 0 ], PredCoef_Q12[ 1 ], psEncC->predictLPCOrder * sizeof( short ) );
            }
        }
    }
}
