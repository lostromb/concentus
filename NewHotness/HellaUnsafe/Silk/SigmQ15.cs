using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk
{
    /* Approximate sigmoid function */
    internal static unsafe class SigmQ15
    {
        /* fprintf(1, '%d, ', round(1024 * ([1 ./ (1 + exp(-(1:5))), 1] - 1 ./ (1 + exp(-(0:5)))))); */
        private static readonly int[] sigm_LUT_slope_Q10/*[ 6 ]*/ = {
            237, 153, 73, 30, 12, 7
        };
        /* fprintf(1, '%d, ', round(32767 * 1 ./ (1 + exp(-(0:5))))); */
        private static readonly int[] sigm_LUT_pos_Q15/*[ 6 ]*/ = {
            16384, 23955, 28861, 31213, 32178, 32548
        };
        /* fprintf(1, '%d, ', round(32767 * 1 ./ (1 + exp((0:5))))); */
        private static readonly int[] sigm_LUT_neg_Q15/*[ 6 ]*/ = {
            16384, 8812, 3906, 1554, 589, 219
        };

        internal static unsafe int silk_sigm_Q15(
            int                    in_Q5               /* I                                                                */
        )
        {
            int ind;

            if( in_Q5 < 0 ) {
                /* Negative input */
                in_Q5 = -in_Q5;
                if( in_Q5 >= 6 * 32 ) {
                    return 0;        /* Clip */
                } else {
                    /* Linear interpolation of look up table */
                    ind = silk_RSHIFT( in_Q5, 5 );
                    return( sigm_LUT_neg_Q15[ ind ] - silk_SMULBB( sigm_LUT_slope_Q10[ ind ], in_Q5 & 0x1F ) );
                }
            } else {
                /* Positive input */
                if( in_Q5 >= 6 * 32 ) {
                    return 32767;        /* clip */
                } else {
                    /* Linear interpolation of look up table */
                    ind = silk_RSHIFT( in_Q5, 5 );
                    return( sigm_LUT_pos_Q15[ ind ] + silk_SMULBB( sigm_LUT_slope_Q10[ ind ], in_Q5 & 0x1F ) );
                }
            }
        }
    }
}
