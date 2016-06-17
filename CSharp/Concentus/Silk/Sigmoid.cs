using Concentus.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    /// <summary>
    /// Approximate sigmoid function
    /// </summary>
    internal static class Sigmoid
    {
        private static readonly int[] sigm_LUT_slope_Q10 = {
            237, 153, 73, 30, 12, 7
        };

        private static readonly int[] sigm_LUT_pos_Q15 = {
            16384, 23955, 28861, 31213, 32178, 32548
        };

        private static readonly int[] sigm_LUT_neg_Q15 = {
            16384, 8812, 3906, 1554, 589, 219
        };

        internal static int silk_sigm_Q15(int in_Q5)
        {
            int ind;

            if (in_Q5 < 0)
            {
                /* Negative input */
                in_Q5 = -in_Q5;
                if (in_Q5 >= 6 * 32)
                {
                    return 0;        /* Clip */
                }
                else
                {
                    /* Linear interpolation of look up table */
                    ind = Inlines.silk_RSHIFT(in_Q5, 5);
                    return (sigm_LUT_neg_Q15[ind] - Inlines.silk_SMULBB(sigm_LUT_slope_Q10[ind], in_Q5 & 0x1F));
                }
            }
            else
            {
                /* Positive input */
                if (in_Q5 >= 6 * 32)
                {
                    return 32767;        /* clip */
                }
                else
                {
                    /* Linear interpolation of look up table */
                    ind = Inlines.silk_RSHIFT(in_Q5, 5);
                    return (sigm_LUT_pos_Q15[ind] + Inlines.silk_SMULBB(sigm_LUT_slope_Q10[ind], in_Q5 & 0x1F));
                }
            }
        }
    }
}
