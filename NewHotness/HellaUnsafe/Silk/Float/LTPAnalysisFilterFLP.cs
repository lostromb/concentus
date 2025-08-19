using static HellaUnsafe.Silk.Define;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class LTPAnalysisFilterFLP
    {
        internal static unsafe void silk_LTP_analysis_filter_FLP(
            float                      *LTP_res,                           /* O    LTP res MAX_NB_SUBFR*(pre_lgth+subfr_lngth) */
            in float                *x,                                 /* I    Input signal, with preceding samples        */
            in float*                B/*[ LTP_ORDER * MAX_NB_SUBFR ]*/,      /* I    LTP coefficients for each subframe          */
            in int*                  pitchL/*[   MAX_NB_SUBFR ]*/,           /* I    Pitch lags                                  */
            in float*                invGains/*[ MAX_NB_SUBFR ]*/,           /* I    Inverse quantization gains                  */
            in int                  subfr_length,                       /* I    Length of each subframe                     */
            in int                  nb_subfr,                           /* I    number of subframes                         */
            in int                  pre_length                          /* I    Preceding samples for each subframe         */
        )
        {
            float *x_ptr, x_lag_ptr;
            float*   Btmp = stackalloc float[ LTP_ORDER ];
            float   *LTP_res_ptr;
            float   inv_gain;
            int     k, i, j;

            x_ptr = x;
            LTP_res_ptr = LTP_res;
            for( k = 0; k < nb_subfr; k++ ) {
                x_lag_ptr = x_ptr - pitchL[ k ];
                inv_gain = invGains[ k ];
                for( i = 0; i < LTP_ORDER; i++ ) {
                    Btmp[ i ] = B[ k * LTP_ORDER + i ];
                }

                /* LTP analysis FIR filter */
                for( i = 0; i < subfr_length + pre_length; i++ ) {
                    LTP_res_ptr[ i ] = x_ptr[ i ];
                    /* Subtract long-term prediction */
                    for( j = 0; j < LTP_ORDER; j++ ) {
                        LTP_res_ptr[ i ] -= Btmp[ j ] * x_lag_ptr[ LTP_ORDER / 2 - j ];
                    }
                    LTP_res_ptr[ i ] *= inv_gain;
                    x_lag_ptr++;
                }

                /* Update pointers */
                LTP_res_ptr += subfr_length + pre_length;
                x_ptr       += subfr_length;
            }
        }
    }
}
