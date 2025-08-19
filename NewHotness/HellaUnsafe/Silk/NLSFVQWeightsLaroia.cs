using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk
{
    /*
    R. Laroia, N. Phamdo and N. Farvardin, "Robust and Efficient Quantization of Speech LSP
    Parameters Using Structured Vector Quantization", Proc. IEEE Int. Conf. Acoust., Speech,
    Signal Processing, pp. 641-644, 1991.
    */
    internal static unsafe class NLSFVQWeightsLaroia
    {
        /* Laroia low complexity NLSF weights */
        internal static unsafe void silk_NLSF_VQ_weights_laroia(
            short                  *pNLSFW_Q_OUT,      /* O     Pointer to input vector weights [D]                        */
            in short            *pNLSF_Q15,         /* I     Pointer to input vector         [D]                        */
            in int              D                   /* I     Input vector dimension (even)                              */
        )
        {
            int   k;
            int tmp1_int, tmp2_int;

            celt_assert( D > 0 );
            celt_assert( ( D & 1 ) == 0 );

            /* First value */
            tmp1_int = silk_max_int( pNLSF_Q15[ 0 ], 1 );
            tmp1_int = silk_DIV32_16( (int)1 << ( 15 + NLSF_W_Q ), tmp1_int );
            tmp2_int = silk_max_int( pNLSF_Q15[ 1 ] - pNLSF_Q15[ 0 ], 1 );
            tmp2_int = silk_DIV32_16( (int)1 << ( 15 + NLSF_W_Q ), tmp2_int );
            pNLSFW_Q_OUT[ 0 ] = (short)silk_min_int( tmp1_int + tmp2_int, silk_int16_MAX );
            silk_assert( pNLSFW_Q_OUT[ 0 ] > 0 );

            /* Main loop */
            for( k = 1; k < D - 1; k += 2 ) {
                tmp1_int = silk_max_int( pNLSF_Q15[ k + 1 ] - pNLSF_Q15[ k ], 1 );
                tmp1_int = silk_DIV32_16( (int)1 << ( 15 + NLSF_W_Q ), tmp1_int );
                pNLSFW_Q_OUT[ k ] = (short)silk_min_int( tmp1_int + tmp2_int, silk_int16_MAX );
                silk_assert( pNLSFW_Q_OUT[ k ] > 0 );

                tmp2_int = silk_max_int( pNLSF_Q15[ k + 2 ] - pNLSF_Q15[ k + 1 ], 1 );
                tmp2_int = silk_DIV32_16( (int)1 << ( 15 + NLSF_W_Q ), tmp2_int );
                pNLSFW_Q_OUT[ k + 1 ] = (short)silk_min_int( tmp1_int + tmp2_int, silk_int16_MAX );
                silk_assert( pNLSFW_Q_OUT[ k + 1 ] > 0 );
            }

            /* Last value */
            tmp1_int = silk_max_int( ( 1 << 15 ) - pNLSF_Q15[ D - 1 ], 1 );
            tmp1_int = silk_DIV32_16( (int)1 << ( 15 + NLSF_W_Q ), tmp1_int );
            pNLSFW_Q_OUT[ D - 1 ] = (short)silk_min_int( tmp1_int + tmp2_int, silk_int16_MAX );
            silk_assert( pNLSFW_Q_OUT[ D - 1 ] > 0 );
        }
    }
}
