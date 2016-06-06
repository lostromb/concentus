using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class LTP_analysis_filter
    {
        public static void silk_LTP_analysis_filter_FIX(
            Pointer<short> LTP_res,                               /* O    LTP residual signal of length SilkConstants.MAX_NB_SUBFR * ( pre_length + subfr_length )  */
            Pointer<short> x,                                     /* I    Pointer to input signal with at least max( pitchL ) preceding samples       */
            Pointer<short> LTPCoef_Q14,/* I    SilkConstants.LTP_ORDER LTP coefficients for each MAX_NB_SUBFR subframe  [SilkConstants.LTP_ORDER * SilkConstants.MAX_NB_SUBFR]                 */
            Pointer<int> pitchL,                 /* I    Pitch lag, one for each subframe [SilkConstants.MAX_NB_SUBFR]                                           */
            Pointer<int> invGains_Q16,           /* I    Inverse quantization gains, one for each subframe [SilkConstants.MAX_NB_SUBFR]                           */
            int subfr_length,                           /* I    Length of each subframe                                                     */
            int nb_subfr,                               /* I    Number of subframes                                                         */
            int pre_length                              /* I    Length of the preceding samples starting at &x[0] for each subframe         */
)
        {
            Pointer<short> x_ptr, x_lag_ptr;
            short[] Btmp_Q14 = new short[SilkConstants.LTP_ORDER];
            Pointer<short> LTP_res_ptr;
            int k, i;
            int LTP_est;

            x_ptr = x;
            LTP_res_ptr = LTP_res;
            for (k = 0; k < nb_subfr; k++)
            {

                x_lag_ptr = x_ptr.Point(0 - pitchL[k]);

                Btmp_Q14[0] = LTPCoef_Q14[k * SilkConstants.LTP_ORDER];
                Btmp_Q14[1] = LTPCoef_Q14[k * SilkConstants.LTP_ORDER + 1];
                Btmp_Q14[2] = LTPCoef_Q14[k * SilkConstants.LTP_ORDER + 2];
                Btmp_Q14[3] = LTPCoef_Q14[k * SilkConstants.LTP_ORDER + 3];
                Btmp_Q14[4] = LTPCoef_Q14[k * SilkConstants.LTP_ORDER + 4];

                /* LTP analysis FIR filter */
                for (i = 0; i < subfr_length + pre_length; i++)
                {
                    LTP_res_ptr[i] = x_ptr[i];

                    /* Long-term prediction */
                    LTP_est = Inlines.silk_SMULBB(x_lag_ptr[SilkConstants.LTP_ORDER / 2], Btmp_Q14[0]);
                    LTP_est = Inlines.silk_SMLABB_ovflw(LTP_est, x_lag_ptr[1], Btmp_Q14[1]);
                    LTP_est = Inlines.silk_SMLABB_ovflw(LTP_est, x_lag_ptr[0], Btmp_Q14[2]);
                    LTP_est = Inlines.silk_SMLABB_ovflw(LTP_est, x_lag_ptr[-1], Btmp_Q14[3]);
                    LTP_est = Inlines.silk_SMLABB_ovflw(LTP_est, x_lag_ptr[-2], Btmp_Q14[4]);

                    LTP_est = Inlines.silk_RSHIFT_ROUND(LTP_est, 14); /* round and . Q0*/

                    /* Subtract long-term prediction */
                    LTP_res_ptr[i] = (short)Inlines.silk_SAT16((int)x_ptr[i] - LTP_est);

                    /* Scale residual */
                    LTP_res_ptr[i] = Inlines.CHOP16(Inlines.silk_SMULWB(invGains_Q16[k], LTP_res_ptr[i]));

                    x_lag_ptr = x_lag_ptr.Point(1);
                }

                /* Update pointers */
                LTP_res_ptr = LTP_res_ptr.Point(subfr_length + pre_length);
                x_ptr = x_ptr.Point(subfr_length);
            }
        }
    }
}
