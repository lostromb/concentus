using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class warped_autocorrelation
    {
        private const int QC = 10;
        private const int QS = 14;

        /* Autocorrelations for a warped frequency axis */
        public static void silk_warped_autocorrelation_FIX(
                  Pointer<int> corr,                                  /* O    Result [order + 1]                                                          */
                  BoxedValue<int> scale,                                 /* O    Scaling of the correlation vector                                           */
                    Pointer<short> input,                                 /* I    Input data to correlate                                                     */
                    int warping_Q16,                            /* I    Warping coefficient                                                         */
                    int length,                                 /* I    Length of input                                                             */
                    int order                                   /* I    Correlation order (even)                                                    */
                )
        {
            int n, i, lsh;
            int tmp1_QS, tmp2_QS;
            int[] state_QS = new int[SilkConstants.MAX_SHAPE_LPC_ORDER + 1];// = { 0 };
            long[] corr_QC = new long[SilkConstants.MAX_SHAPE_LPC_ORDER + 1];// = { 0 };

            /* Order must be even */
            Debug.Assert((order & 1) == 0);
            Debug.Assert(2 * QS - QC >= 0);

            /* Loop over samples */
            for (n = 0; n < length; n++)
            {
                tmp1_QS = Inlines.silk_LSHIFT32((int)input[n], QS);
                /* Loop over allpass sections */
                for (i = 0; i < order; i += 2)
                {
                    /* Output of allpass section */
                    tmp2_QS = Inlines.silk_SMLAWB(state_QS[i], state_QS[i + 1] - tmp1_QS, warping_Q16);
                    state_QS[i] = tmp1_QS;
                    corr_QC[i] += Inlines.silk_RSHIFT64(Inlines.silk_SMULL(tmp1_QS, state_QS[0]), 2 * QS - QC);
                    /* Output of allpass section */
                    tmp1_QS = Inlines.silk_SMLAWB(state_QS[i + 1], state_QS[i + 2] - tmp2_QS, warping_Q16);
                    state_QS[i + 1] = tmp2_QS;
                    corr_QC[i + 1] += Inlines.silk_RSHIFT64(Inlines.silk_SMULL(tmp2_QS, state_QS[0]), 2 * QS - QC);
                }
                state_QS[order] = tmp1_QS;
                corr_QC[order] += Inlines.silk_RSHIFT64(Inlines.silk_SMULL(tmp1_QS, state_QS[0]), 2 * QS - QC);
            }

            lsh = Inlines.silk_CLZ64(corr_QC[0]) - 35;
            lsh = Inlines.silk_LIMIT(lsh, -12 - QC, 30 - QC);
            scale.Val = -(QC + lsh);
            Debug.Assert(scale.Val >= -30 && scale.Val <= 12);
            if (lsh >= 0)
            {
                for (i = 0; i < order + 1; i++)
                {
                    corr[i] = (int)Inlines.CHOP32(Inlines.silk_LSHIFT64(corr_QC[i], lsh));
                }
            }
            else {
                for (i = 0; i < order + 1; i++)
                {
                    corr[i] = (int)Inlines.CHOP32(Inlines.silk_RSHIFT64(corr_QC[i], -lsh));
                }
            }
            Debug.Assert(corr_QC[0] >= 0); /* If breaking, decrease QC*/
        }
    }
}
