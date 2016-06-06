using Concentus.Common.CPlusPlus;
using Concentus.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    public static class quant_LTP_gains
    {
        public static void silk_quant_LTP_gains(
            Pointer<short> B_Q14,          /* I/O  (un)quantized LTP gains [MAX_NB_SUBFR * LTP_ORDER]        */
            Pointer<sbyte> cbk_index,                  /* O    Codebook Index [MAX_NB_SUBFR]                 */
            BoxedValue<sbyte> periodicity_index,                         /* O    Periodicity Index               */
            BoxedValue<int> sum_log_gain_Q7,                           /* I/O  Cumulative max prediction gain  */
            Pointer<int> W_Q18,  /* I    Error Weights in Q18 [MAX_NB_SUBFR * LTP_ORDER * LTP_ORDER]           */
            int mu_Q9,                                      /* I    Mu value (R/D tradeoff)         */
            int lowComplexity,                              /* I    Flag for low complexity         */
            int nb_subfr,                                   /* I    number of subframes             */
            int arch                                        /* I    Run-time architecture           */
            )
        {
            int j, k, cbk_size;
            sbyte[] temp_idx = new sbyte[SilkConstants.MAX_NB_SUBFR];
            Pointer<byte> cl_ptr_Q5;
            Pointer<sbyte> cbk_ptr_Q7;
            Pointer<byte> cbk_gain_ptr_Q7;
            Pointer<short> b_Q14_ptr;
            Pointer<int> W_Q18_ptr;
            int rate_dist_Q14_subfr, rate_dist_Q14, min_rate_dist_Q14;
            int sum_log_gain_tmp_Q7, best_sum_log_gain_Q7, max_gain_Q7, gain_Q7;

            /***************************************************/
            /* iterate over different codebooks with different */
            /* rates/distortions, and choose best */
            /***************************************************/
            min_rate_dist_Q14 = int.MaxValue;
            best_sum_log_gain_Q7 = 0;
            for (k = 0; k < 3; k++)
            {
                /* Safety margin for pitch gain control, to take into account factors
                   such as state rescaling/rewhitening. */
                int gain_safety = Inlines.SILK_FIX_CONST(0.4f, 7);

                cl_ptr_Q5 = Tables.silk_LTP_gain_BITS_Q5_ptrs[k].GetPointer();
                cbk_ptr_Q7 = Tables.silk_LTP_vq_ptrs_Q7[k];
                cbk_gain_ptr_Q7 = Tables.silk_LTP_vq_gain_ptrs_Q7[k];
                cbk_size = Tables.silk_LTP_vq_sizes[k];

                /* Set up pointer to first subframe */
                W_Q18_ptr = W_Q18;
                b_Q14_ptr = B_Q14;

                rate_dist_Q14 = 0;
                sum_log_gain_tmp_Q7 = sum_log_gain_Q7.Val;
                for (j = 0; j < nb_subfr; j++)
                {
                    max_gain_Q7 = Inlines.silk_log2lin((Inlines.SILK_FIX_CONST(TuningParameters.MAX_SUM_LOG_GAIN_DB / 6.0f, 7) - sum_log_gain_tmp_Q7)
                                                + Inlines.SILK_FIX_CONST(7, 7)) - gain_safety;


                    BoxedValue<sbyte> temp_idx_box = new BoxedValue<sbyte>(temp_idx[j]);
                    BoxedValue<int> rate_dist_Q14_subfr_box = new BoxedValue<int>();
                    BoxedValue<int> gain_Q7_box = new BoxedValue<int>();
                    VQ_WMat_EC.silk_VQ_WMat_EC_c(
                        temp_idx_box,         /* O    index of best codebook vector                           */
                        rate_dist_Q14_subfr_box,   /* O    best weighted quantization error + mu * rate            */
                        gain_Q7_box,               /* O    sum of absolute LTP coefficients                        */
                        b_Q14_ptr,              /* I    input vector to be quantized                            */
                        W_Q18_ptr,              /* I    weighting matrix                                        */
                        cbk_ptr_Q7,             /* I    codebook                                                */
                        cbk_gain_ptr_Q7,        /* I    codebook effective gains                                */
                        cl_ptr_Q5,              /* I    code length for each codebook vector                    */
                        mu_Q9,                  /* I    tradeoff between weighted error and rate                */
                        max_gain_Q7,            /* I    maximum sum of absolute LTP coefficients                */
                        cbk_size               /* I    number of vectors in codebook                           */
                    );
                    rate_dist_Q14_subfr = rate_dist_Q14_subfr_box.Val;
                    gain_Q7 = gain_Q7_box.Val;

                    rate_dist_Q14 = Inlines.silk_ADD_POS_SAT32(rate_dist_Q14, rate_dist_Q14_subfr);
                    sum_log_gain_tmp_Q7 = Inlines.silk_max(0, sum_log_gain_tmp_Q7
                                            + Inlines.silk_lin2log(gain_safety + gain_Q7) - Inlines.SILK_FIX_CONST(7, 7));

                    b_Q14_ptr = b_Q14_ptr.Point(SilkConstants.LTP_ORDER);
                    W_Q18_ptr = W_Q18_ptr.Point(SilkConstants.LTP_ORDER * SilkConstants.LTP_ORDER);
                }

                /* Avoid never finding a codebook */
                rate_dist_Q14 = Inlines.silk_min(int.MaxValue - 1, rate_dist_Q14);

                if (rate_dist_Q14 < min_rate_dist_Q14)
                {
                    min_rate_dist_Q14 = rate_dist_Q14;
                    periodicity_index.Val = (sbyte)k;
                    cbk_index.MemCopyFrom(temp_idx, 0, nb_subfr);
                    best_sum_log_gain_Q7 = sum_log_gain_tmp_Q7;
                }

                /* Break early in low-complexity mode if rate distortion is below threshold */
                if (lowComplexity != 0 && (rate_dist_Q14 < Tables.silk_LTP_gain_middle_avg_RD_Q14))
                {
                    break;
                }
            }

            cbk_ptr_Q7 = Tables.silk_LTP_vq_ptrs_Q7[periodicity_index.Val];
            for (j = 0; j < nb_subfr; j++)
            {
                for (k = 0; k < SilkConstants.LTP_ORDER; k++)
                {
                    B_Q14[j * SilkConstants.LTP_ORDER + k] = Inlines.CHOP16(Inlines.silk_LSHIFT(cbk_ptr_Q7[cbk_index[j] * SilkConstants.LTP_ORDER + k], 7));
                }
            }

            sum_log_gain_Q7.Val = best_sum_log_gain_Q7;
        }
    }
}
