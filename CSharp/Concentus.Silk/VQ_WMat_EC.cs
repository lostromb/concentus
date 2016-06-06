using Concentus.Common.CPlusPlus;
using Concentus.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    public static class VQ_WMat_EC
    {
        /* Entropy constrained matrix-weighted VQ, hard-coded to 5-element vectors, for a single input data vector */
        public static void silk_VQ_WMat_EC_c(
            BoxedValue<sbyte> ind,                           /* O    index of best codebook vector               */
            BoxedValue<int> rate_dist_Q14,                 /* O    best weighted quant error + mu * rate       */
            BoxedValue<int> gain_Q7,                       /* O    sum of absolute LTP coefficients            */
            Pointer<short> in_Q14,                        /* I    input vector to be quantized                */
            Pointer<int> W_Q18,                         /* I    weighting matrix                            */
            Pointer<sbyte> cb_Q7,                         /* I    codebook                                    */
            Pointer<byte> cb_gain_Q7,                    /* I    codebook effective gain                     */
            Pointer<byte> cl_Q5,                         /* I    code length for each codebook vector        */
            int mu_Q9,                          /* I    tradeoff betw. weighted error and rate      */
            int max_gain_Q7,                    /* I    maximum sum of absolute LTP coefficients    */
            int L                               /* I    number of vectors in codebook               */
)
        {
            int k, gain_tmp_Q7;
            Pointer<sbyte> cb_row_Q7;
            Pointer<short> diff_Q14 = Pointer.Malloc<short>(5);
            int sum1_Q14, sum2_Q16;

            /* Loop over codebook */
            rate_dist_Q14.Val = int.MaxValue;
            cb_row_Q7 = cb_Q7;
            for (k = 0; k < L; k++)
            {
                gain_tmp_Q7 = cb_gain_Q7[k];

                diff_Q14[0] = Inlines.CHOP16(in_Q14[0] - Inlines.silk_LSHIFT(cb_row_Q7[0], 7));
                diff_Q14[1] = Inlines.CHOP16(in_Q14[1] - Inlines.silk_LSHIFT(cb_row_Q7[1], 7));
                diff_Q14[2] = Inlines.CHOP16(in_Q14[2] - Inlines.silk_LSHIFT(cb_row_Q7[2], 7));
                diff_Q14[3] = Inlines.CHOP16(in_Q14[3] - Inlines.silk_LSHIFT(cb_row_Q7[3], 7));
                diff_Q14[4] = Inlines.CHOP16(in_Q14[4] - Inlines.silk_LSHIFT(cb_row_Q7[4], 7));

                /* Weighted rate */
                sum1_Q14 = Inlines.silk_SMULBB(mu_Q9, cl_Q5[k]);

                /* Penalty for too large gain */
                sum1_Q14 = Inlines.silk_ADD_LSHIFT32(sum1_Q14, Inlines.silk_max(Inlines.silk_SUB32(gain_tmp_Q7, max_gain_Q7), 0), 10);

                Debug.Assert(sum1_Q14 >= 0);

                /* first row of W_Q18 */
                sum2_Q16 = Inlines.silk_SMULWB(W_Q18[1], diff_Q14[1]);
                sum2_Q16 = Inlines.silk_SMLAWB(sum2_Q16, W_Q18[2], diff_Q14[2]);
                sum2_Q16 = Inlines.silk_SMLAWB(sum2_Q16, W_Q18[3], diff_Q14[3]);
                sum2_Q16 = Inlines.silk_SMLAWB(sum2_Q16, W_Q18[4], diff_Q14[4]);
                sum2_Q16 = Inlines.silk_LSHIFT(sum2_Q16, 1);
                sum2_Q16 = Inlines.silk_SMLAWB(sum2_Q16, W_Q18[0], diff_Q14[0]);
                sum1_Q14 = Inlines.silk_SMLAWB(sum1_Q14, sum2_Q16, diff_Q14[0]);

                /* second row of W_Q18 */
                sum2_Q16 = Inlines.silk_SMULWB(W_Q18[7], diff_Q14[2]);
                sum2_Q16 = Inlines.silk_SMLAWB(sum2_Q16, W_Q18[8], diff_Q14[3]);
                sum2_Q16 = Inlines.silk_SMLAWB(sum2_Q16, W_Q18[9], diff_Q14[4]);
                sum2_Q16 = Inlines.silk_LSHIFT(sum2_Q16, 1);
                sum2_Q16 = Inlines.silk_SMLAWB(sum2_Q16, W_Q18[6], diff_Q14[1]);
                sum1_Q14 = Inlines.silk_SMLAWB(sum1_Q14, sum2_Q16, diff_Q14[1]);

                /* third row of W_Q18 */
                sum2_Q16 = Inlines.silk_SMULWB(W_Q18[13], diff_Q14[3]);
                sum2_Q16 = Inlines.silk_SMLAWB(sum2_Q16, W_Q18[14], diff_Q14[4]);
                sum2_Q16 = Inlines.silk_LSHIFT(sum2_Q16, 1);
                sum2_Q16 = Inlines.silk_SMLAWB(sum2_Q16, W_Q18[12], diff_Q14[2]);
                sum1_Q14 = Inlines.silk_SMLAWB(sum1_Q14, sum2_Q16, diff_Q14[2]);

                /* fourth row of W_Q18 */
                sum2_Q16 = Inlines.silk_SMULWB(W_Q18[19], diff_Q14[4]);
                sum2_Q16 = Inlines.silk_LSHIFT(sum2_Q16, 1);
                sum2_Q16 = Inlines.silk_SMLAWB(sum2_Q16, W_Q18[18], diff_Q14[3]);
                sum1_Q14 = Inlines.silk_SMLAWB(sum1_Q14, sum2_Q16, diff_Q14[3]);

                /* last row of W_Q18 */
                sum2_Q16 = Inlines.silk_SMULWB(W_Q18[24], diff_Q14[4]);
                sum1_Q14 = Inlines.silk_SMLAWB(sum1_Q14, sum2_Q16, diff_Q14[4]);

                Debug.Assert(sum1_Q14 >= 0);

                /* find best */
                if (sum1_Q14 < rate_dist_Q14.Val)
                {
                    rate_dist_Q14.Val = sum1_Q14;
                    ind.Val = (sbyte)k;
                    gain_Q7.Val = gain_tmp_Q7;
                }

                /* Go to next cbk vector */
                cb_row_Q7 = cb_row_Q7.Point(SilkConstants.LTP_ORDER);
            }
        }
    }
}
