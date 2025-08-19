using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Float.CorrMatrixFLP;
using static HellaUnsafe.Silk.Float.EnergyFLP;
using static HellaUnsafe.Silk.Float.ScaleVectorFLP;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.TuningParameters;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class FindLTPFLP
    {
        internal static unsafe void silk_find_LTP_FLP(
            float*                      XX/*[ MAX_NB_SUBFR * LTP_ORDER * LTP_ORDER ]*/, /* O    Weight for LTP quantization         */
            float*                      xX/*[ MAX_NB_SUBFR * LTP_ORDER ]*/,     /* O    Weight for LTP quantization                 */
            float*                r_ptr,                            /* I    LPC residual                                */
            in int* lag/*[ MAX_NB_SUBFR ]*/,                /* I    LTP lags                                    */
            in int subfr_length,                       /* I    Subframe length                             */
            in int nb_subfr                           /* I    number of subframes                         */
        )
        {
            int   k;
            float *xX_ptr, XX_ptr;
            float *lag_ptr;
            float xx, temp;

            xX_ptr = xX;
            XX_ptr = XX;
            for( k = 0; k < nb_subfr; k++ ) {
                lag_ptr = r_ptr - ( lag[ k ] + LTP_ORDER / 2 );
                silk_corrMatrix_FLP( lag_ptr, subfr_length, LTP_ORDER, XX_ptr );
                silk_corrVector_FLP( lag_ptr, r_ptr, subfr_length, LTP_ORDER, xX_ptr );
                xx = ( float )silk_energy_FLP( r_ptr, subfr_length + LTP_ORDER );
                temp = 1.0f / silk_max( xx, LTP_CORR_INV_MAX * 0.5f * ( XX_ptr[ 0 ] + XX_ptr[ 24 ] ) + 1.0f );
                silk_scale_vector_FLP( XX_ptr, temp, LTP_ORDER * LTP_ORDER );
                silk_scale_vector_FLP( xX_ptr, temp, LTP_ORDER );

                r_ptr  += subfr_length;
                XX_ptr += LTP_ORDER * LTP_ORDER;
                xX_ptr += LTP_ORDER;
            }
        }
    }
}
