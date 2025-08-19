using System;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Float.FindLPCFLP;
using static HellaUnsafe.Silk.Float.FindLTPFLP;
using static HellaUnsafe.Silk.Float.LTPAnalysisFilterFLP;
using static HellaUnsafe.Silk.Float.LTPScaleCtrlFLP;
using static HellaUnsafe.Silk.Float.ResidualEnergyFLP;
using static HellaUnsafe.Silk.Float.ScaleCopyVectorFLP;
using static HellaUnsafe.Silk.Float.StructsFLP;
using static HellaUnsafe.Silk.Float.WrappersFLP;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class FindPredCoefsFLP
    {
        /* Find LPC and LTP coefficients */
        internal static unsafe void silk_find_pred_coefs_FLP(
            silk_encoder_state_FLP          *psEnc,                             /* I/O  Encoder state FLP                           */
            silk_encoder_control_FLP        *psEncCtrl,                         /* I/O  Encoder control FLP                         */
            in float*                res_pitch,                        /* I    Residual from pitch analysis                */
            in float*                x,                                /* I    Speech signal                               */
            int                        condCoding                          /* I    The type of conditional coding to use       */
        )
        {
            int         i;
            float* XXLTP = stackalloc float[ MAX_NB_SUBFR * LTP_ORDER * LTP_ORDER ];
            float* xXLTP = stackalloc float[ MAX_NB_SUBFR * LTP_ORDER ];
            float* invGains = stackalloc float[ MAX_NB_SUBFR ];
            short* NLSF_Q15 = stackalloc short[MAX_LPC_ORDER];
            /* Set to NLSF_Q15 to zero so we don't copy junk to the state. */
            new Span<short>(NLSF_Q15, MAX_LPC_ORDER).Fill(0);
            float *x_ptr;
            float* x_pre_ptr;
            float* LPC_in_pre = stackalloc float[ MAX_NB_SUBFR * MAX_LPC_ORDER + MAX_FRAME_LENGTH ];
            float       minInvGain;

            /* Weighting for weighted least squares */
            for( i = 0; i < psEnc->sCmn.nb_subfr; i++ ) {
                silk_assert( psEncCtrl->Gains[ i ] > 0.0f );
                invGains[ i ] = 1.0f / psEncCtrl->Gains[ i ];
            }

            if( psEnc->sCmn.indices.signalType == TYPE_VOICED ) {
                /**********/
                /* VOICED */
                /**********/
                celt_assert( psEnc->sCmn.ltp_mem_length - psEnc->sCmn.predictLPCOrder >= psEncCtrl->pitchL[ 0 ] + LTP_ORDER / 2 );

                /* LTP analysis */
                silk_find_LTP_FLP( XXLTP, xXLTP, res_pitch, psEncCtrl->pitchL, psEnc->sCmn.subfr_length, psEnc->sCmn.nb_subfr );

                /* Quantize LTP gain parameters */
                silk_quant_LTP_gains_FLP( psEncCtrl->LTPCoef, psEnc->sCmn.indices.LTPIndex, &psEnc->sCmn.indices.PERIndex,
                    &psEnc->sCmn.sum_log_gain_Q7, &psEncCtrl->LTPredCodGain, XXLTP, xXLTP, psEnc->sCmn.subfr_length, psEnc->sCmn.nb_subfr );

                /* Control LTP scaling */
                silk_LTP_scale_ctrl_FLP( psEnc, psEncCtrl, condCoding );

                /* Create LTP residual */
                silk_LTP_analysis_filter_FLP( LPC_in_pre, x - psEnc->sCmn.predictLPCOrder, psEncCtrl->LTPCoef,
                    psEncCtrl->pitchL, invGains, psEnc->sCmn.subfr_length, psEnc->sCmn.nb_subfr, psEnc->sCmn.predictLPCOrder );
            } else {
                /************/
                /* UNVOICED */
                /************/
                /* Create signal with prepended subframes, scaled by inverse gains */
                x_ptr     = x - psEnc->sCmn.predictLPCOrder;
                x_pre_ptr = LPC_in_pre;
                for( i = 0; i < psEnc->sCmn.nb_subfr; i++ ) {
                    silk_scale_copy_vector_FLP( x_pre_ptr, x_ptr, invGains[ i ],
                        psEnc->sCmn.subfr_length + psEnc->sCmn.predictLPCOrder );
                    x_pre_ptr += psEnc->sCmn.subfr_length + psEnc->sCmn.predictLPCOrder;
                    x_ptr     += psEnc->sCmn.subfr_length;
                }
                silk_memset( psEncCtrl->LTPCoef, 0, psEnc->sCmn.nb_subfr * LTP_ORDER * sizeof( float ) );
                psEncCtrl->LTPredCodGain = 0.0f;
                psEnc->sCmn.sum_log_gain_Q7 = 0;
            }

            /* Limit on total predictive coding gain */
            if( psEnc->sCmn.first_frame_after_reset != 0) {
                minInvGain = 1.0f / MAX_PREDICTION_POWER_GAIN_AFTER_RESET;
            } else {
                minInvGain = (float)pow( 2, psEncCtrl->LTPredCodGain / 3 ) /  MAX_PREDICTION_POWER_GAIN;
                minInvGain /= 0.25f + 0.75f * psEncCtrl->coding_quality;
            }

            /* LPC_in_pre contains the LTP-filtered input for voiced, and the unfiltered input for unvoiced */
            silk_find_LPC_FLP( &psEnc->sCmn, NLSF_Q15, LPC_in_pre, minInvGain );

            /* Quantize LSFs */
            silk_process_NLSFs_FLP( &psEnc->sCmn, psEncCtrl->PredCoef, NLSF_Q15, psEnc->sCmn.prev_NLSFq_Q15 );

            /* Calculate residual energy using quantized LPC coefficients */
            silk_residual_energy_FLP( psEncCtrl->ResNrg, LPC_in_pre, psEncCtrl->PredCoef, psEncCtrl->Gains,
                psEnc->sCmn.subfr_length, psEnc->sCmn.nb_subfr, psEnc->sCmn.predictLPCOrder );

            /* Copy to prediction struct for use in next frame for interpolation */
            silk_memcpy( psEnc->sCmn.prev_NLSFq_Q15, NLSF_Q15, MAX_LPC_ORDER * sizeof(short) /*sizeof( psEnc->sCmn.prev_NLSFq_Q15 )*/ );
        }
    }
}
