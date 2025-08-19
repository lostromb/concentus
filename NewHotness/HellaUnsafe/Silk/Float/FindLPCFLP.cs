using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Float.BurgModifiedFLP;
using static HellaUnsafe.Silk.Float.EnergyFLP;
using static HellaUnsafe.Silk.Float.LPCAnalysisFilterFLP;
using static HellaUnsafe.Silk.Float.WrappersFLP;
using static HellaUnsafe.Silk.Interpolate;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class FindLPCFLP
    {
        /* LPC analysis */
        internal static unsafe void silk_find_LPC_FLP(
            silk_encoder_state              *psEncC,                            /* I/O  Encoder state                               */
            short*                      NLSF_Q15,                         /* O    NLSFs                                       */
            in float*                x,                                /* I    Input signal                                */
            in float                minInvGain                         /* I    Inverse of max prediction gain              */
        )
        {
            int    k, subfr_length;
            float*  a = stackalloc float[ MAX_LPC_ORDER ];

            /* Used only for NLSF interpolation */
            float  res_nrg, res_nrg_2nd, res_nrg_interp;
            short* NLSF0_Q15 = stackalloc short[ MAX_LPC_ORDER ];
            float* a_tmp = stackalloc float[ MAX_LPC_ORDER ];
            float* LPC_res = stackalloc float[ MAX_FRAME_LENGTH + MAX_NB_SUBFR * MAX_LPC_ORDER ];

            subfr_length = psEncC->subfr_length + psEncC->predictLPCOrder;

            /* Default: No interpolation */
            psEncC->indices.NLSFInterpCoef_Q2 = 4;

            /* Burg AR analysis for the full frame */
            res_nrg = silk_burg_modified_FLP( a, x, minInvGain, subfr_length, psEncC->nb_subfr, psEncC->predictLPCOrder );

            if( psEncC->useInterpolatedNLSFs != 0 && psEncC->first_frame_after_reset == 0 && psEncC->nb_subfr == MAX_NB_SUBFR ) {
                /* Optimal solution for last 10 ms; subtract residual energy here, as that's easier than        */
                /* adding it to the residual energy of the first 10 ms in each iteration of the search below    */
                res_nrg -= silk_burg_modified_FLP( a_tmp, x + ( MAX_NB_SUBFR / 2 ) * subfr_length, minInvGain, subfr_length, MAX_NB_SUBFR / 2, psEncC->predictLPCOrder );

                /* Convert to NLSFs */
                silk_A2NLSF_FLP( NLSF_Q15, a_tmp, psEncC->predictLPCOrder );

                /* Search over interpolation indices to find the one with lowest residual energy */
                res_nrg_2nd = float_MAX;
                for( k = 3; k >= 0; k-- ) {
                    /* Interpolate NLSFs for first half */
                    silk_interpolate( NLSF0_Q15, psEncC->prev_NLSFq_Q15, NLSF_Q15, k, psEncC->predictLPCOrder );

                    /* Convert to LPC for residual energy evaluation */
                    silk_NLSF2A_FLP( a_tmp, NLSF0_Q15, psEncC->predictLPCOrder );

                    /* Calculate residual energy with LSF interpolation */
                    silk_LPC_analysis_filter_FLP( LPC_res, a_tmp, x, 2 * subfr_length, psEncC->predictLPCOrder );
                    res_nrg_interp = (float)(
                        silk_energy_FLP( LPC_res + psEncC->predictLPCOrder,                subfr_length - psEncC->predictLPCOrder ) +
                        silk_energy_FLP( LPC_res + psEncC->predictLPCOrder + subfr_length, subfr_length - psEncC->predictLPCOrder ) );

                    /* Determine whether current interpolated NLSFs are best so far */
                    if( res_nrg_interp < res_nrg ) {
                        /* Interpolation has lower residual energy */
                        res_nrg = res_nrg_interp;
                        psEncC->indices.NLSFInterpCoef_Q2 = (sbyte)k;
                    } else if( res_nrg_interp > res_nrg_2nd ) {
                        /* No reason to continue iterating - residual energies will continue to climb */
                        break;
                    }
                    res_nrg_2nd = res_nrg_interp;
                }
            }

            if( psEncC->indices.NLSFInterpCoef_Q2 == 4 ) {
                /* NLSF interpolation is currently inactive, calculate NLSFs from full frame AR coefficients */
                silk_A2NLSF_FLP( NLSF_Q15, a, psEncC->predictLPCOrder );
            }

            celt_assert( psEncC->indices.NLSFInterpCoef_Q2 == 4 ||
                ( psEncC->useInterpolatedNLSFs != 0 && psEncC->first_frame_after_reset == 0 && psEncC->nb_subfr == MAX_NB_SUBFR ) );
        }
    }
}
