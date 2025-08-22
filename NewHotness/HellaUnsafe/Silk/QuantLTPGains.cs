using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Inlines;
using static HellaUnsafe.Silk.VQ_WMat_EC;
using static HellaUnsafe.Silk.Tables;
using static HellaUnsafe.Silk.TuningParameters;
using static HellaUnsafe.Silk.Lin2Log;
using static HellaUnsafe.Silk.Log2Lin;

namespace HellaUnsafe.Silk
{
    internal static unsafe class QuantLTPGains
    {
        internal static unsafe void silk_quant_LTP_gains(
            short*                  B_Q14/*[ MAX_NB_SUBFR * LTP_ORDER ]*/,          /* O    Quantized LTP gains             */
            sbyte*                   cbk_index/*[ MAX_NB_SUBFR ]*/,                  /* O    Codebook Index                  */
            sbyte                   *periodicity_index,                         /* O    Periodicity Index               */
            int                  *sum_log_gain_Q7,                           /* I/O  Cumulative max prediction gain  */
            int                    *pred_gain_dB_Q7,                           /* O    LTP prediction gain             */
            in int*            XX_Q17/*[ MAX_NB_SUBFR*LTP_ORDER*LTP_ORDER ]*/, /* I    Correlation matrix in Q18       */
            in int*            xX_Q17/*[ MAX_NB_SUBFR*LTP_ORDER ]*/,           /* I    Correlation vector in Q18       */
            in int              subfr_len,                                  /* I    Number of samples per subframe  */
            in int              nb_subfr                                   /* I    Number of subframes             */
        )
        {
            int             j, k, cbk_size;
            sbyte*            temp_idx = stackalloc sbyte[ MAX_NB_SUBFR ];
            byte     *cl_ptr_Q5;
            sbyte      *cbk_ptr_Q7;
            byte     *cbk_gain_ptr_Q7;
            int     *XX_Q17_ptr, xX_Q17_ptr;
            int           res_nrg_Q15_subfr, res_nrg_Q15, rate_dist_Q7_subfr, rate_dist_Q7, min_rate_dist_Q7;
            int           sum_log_gain_tmp_Q7, best_sum_log_gain_Q7, max_gain_Q7;
            int             gain_Q7;

            /***************************************************/
            /* iterate over different codebooks with different */
            /* rates/distortions, and choose best */
            /***************************************************/
            min_rate_dist_Q7 = silk_int32_MAX;
            best_sum_log_gain_Q7 = 0;
            res_nrg_Q15 = 0;
            for ( k = 0; k < 3; k++ ) {
                /* Safety margin for pitch gain control, to take into account factors
                   such as state rescaling/rewhitening. */
                int gain_safety = /*SILK_FIX_CONST*/((int)( 0.4 * ((long)1 <<  7 ) + 0.5));

                cl_ptr_Q5  = silk_LTP_gain_BITS_Q5_ptrs[ k ];
                cbk_ptr_Q7 = silk_LTP_vq_ptrs_Q7[        k ];
                cbk_gain_ptr_Q7 = silk_LTP_vq_gain_ptrs_Q7[ k ];
                cbk_size   = silk_LTP_vq_sizes[          k ];

                /* Set up pointers to first subframe */
                XX_Q17_ptr = XX_Q17;
                xX_Q17_ptr = xX_Q17;

                res_nrg_Q15 = 0;
                rate_dist_Q7 = 0;
                sum_log_gain_tmp_Q7 = *sum_log_gain_Q7;
                for( j = 0; j < nb_subfr; j++ ) {
                    max_gain_Q7 = silk_log2lin( ( /*SILK_FIX_CONST*/((int)( MAX_SUM_LOG_GAIN_DB / 6.0 * ((long)1 <<  7 ) + 0.5)) - sum_log_gain_tmp_Q7 )
                                                + /*SILK_FIX_CONST*/((int)( 7 * ((long)1 <<  7 ) + 0.5)) ) - gain_safety;
                    silk_VQ_WMat_EC(
                        &temp_idx[ j ],         /* O    index of best codebook vector                           */
                        &res_nrg_Q15_subfr,     /* O    residual energy                                         */
                        &rate_dist_Q7_subfr,    /* O    best weighted quantization error + mu * rate            */
                        &gain_Q7,               /* O    sum of absolute LTP coefficients                        */
                        XX_Q17_ptr,             /* I    correlation matrix                                      */
                        xX_Q17_ptr,             /* I    correlation vector                                      */
                        cbk_ptr_Q7,             /* I    codebook                                                */
                        cbk_gain_ptr_Q7,        /* I    codebook effective gains                                */
                        cl_ptr_Q5,              /* I    code length for each codebook vector                    */
                        subfr_len,              /* I    number of samples per subframe                          */
                        max_gain_Q7,            /* I    maximum sum of absolute LTP coefficients                */
                        cbk_size               /* I    number of vectors in codebook                           */
                    );

                    res_nrg_Q15  = silk_ADD_POS_SAT32( res_nrg_Q15, res_nrg_Q15_subfr );
                    rate_dist_Q7 = silk_ADD_POS_SAT32( rate_dist_Q7, rate_dist_Q7_subfr );
                    sum_log_gain_tmp_Q7 = silk_max(0, sum_log_gain_tmp_Q7
                                        + silk_lin2log( gain_safety + gain_Q7 ) - /*SILK_FIX_CONST*/((int)( 7 * ((long)1 <<  7 ) + 0.5)));

                    XX_Q17_ptr += LTP_ORDER * LTP_ORDER;
                    xX_Q17_ptr += LTP_ORDER;
                }

                if( rate_dist_Q7 <= min_rate_dist_Q7 ) {
                    min_rate_dist_Q7 = rate_dist_Q7;
                    *periodicity_index = (sbyte)k;
                    silk_memcpy( cbk_index, temp_idx, nb_subfr * sizeof( sbyte ) );
                    best_sum_log_gain_Q7 = sum_log_gain_tmp_Q7;
                }
            }

            cbk_ptr_Q7 = silk_LTP_vq_ptrs_Q7[ *periodicity_index ];
            for( j = 0; j < nb_subfr; j++ ) {
                for( k = 0; k < LTP_ORDER; k++ ) {
                    B_Q14[ j * LTP_ORDER + k ] = (short)silk_LSHIFT( cbk_ptr_Q7[ cbk_index[ j ] * LTP_ORDER + k ], 7 );
                }
            }

            if( nb_subfr == 2 ) {
                res_nrg_Q15 = silk_RSHIFT32( res_nrg_Q15, 1 );
            } else {
                res_nrg_Q15 = silk_RSHIFT32( res_nrg_Q15, 2 );
            }

            *sum_log_gain_Q7 = best_sum_log_gain_Q7;
            *pred_gain_dB_Q7 = (int)silk_SMULBB( -3, silk_lin2log( res_nrg_Q15 ) - ( 15 << 7 ) );
        }
    }
}
