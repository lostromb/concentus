using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class LPCAnalysisFilterFLP
            {
        /************************************************/
        /* LPC analysis filter                          */
        /* NB! State is kept internally and the         */
        /* filter always starts with zero state         */
        /* first Order output samples are set to zero   */
        /************************************************/

        /* 16th order LPC analysis filter, does not write first 16 samples */
        internal static unsafe  void silk_LPC_analysis_filter16_FLP(
                  float* r_LPC,            /* O    LPC residual signal                     */
            in float*                 PredCoef,         /* I    LPC coefficients                        */
            in float* s,                /* I    Input signal                            */
            in int                   length              /* I    Length of input signal                  */
        )
        {
            int   ix;
            float LPC_pred;
            float* s_ptr;

            for( ix = 16; ix < length; ix++ ) {
                s_ptr = &s[ix - 1];

                /* short-term prediction */
                LPC_pred = s_ptr[  0 ]  * PredCoef[ 0 ]  +
                           s_ptr[ -1 ]  * PredCoef[ 1 ]  +
                           s_ptr[ -2 ]  * PredCoef[ 2 ]  +
                           s_ptr[ -3 ]  * PredCoef[ 3 ]  +
                           s_ptr[ -4 ]  * PredCoef[ 4 ]  +
                           s_ptr[ -5 ]  * PredCoef[ 5 ]  +
                           s_ptr[ -6 ]  * PredCoef[ 6 ]  +
                           s_ptr[ -7 ]  * PredCoef[ 7 ]  +
                           s_ptr[ -8 ]  * PredCoef[ 8 ]  +
                           s_ptr[ -9 ]  * PredCoef[ 9 ]  +
                           s_ptr[ -10 ] * PredCoef[ 10 ] +
                           s_ptr[ -11 ] * PredCoef[ 11 ] +
                           s_ptr[ -12 ] * PredCoef[ 12 ] +
                           s_ptr[ -13 ] * PredCoef[ 13 ] +
                           s_ptr[ -14 ] * PredCoef[ 14 ] +
                           s_ptr[ -15 ] * PredCoef[ 15 ];

                /* prediction error */
                r_LPC[ix] = s_ptr[ 1 ] - LPC_pred;
            }
        }

        /* 12th order LPC analysis filter, does not write first 12 samples */
        internal static unsafe void silk_LPC_analysis_filter12_FLP(
                  float*                 r_LPC,            /* O    LPC residual signal                     */
            in float*                 PredCoef,         /* I    LPC coefficients                        */
            in float*                 s,                /* I    Input signal                            */
            in int                   length              /* I    Length of input signal                  */
        )
        {
            int   ix;
            float LPC_pred;
            float *s_ptr;

            for( ix = 12; ix < length; ix++ ) {
                s_ptr = &s[ix - 1];

                /* short-term prediction */
                LPC_pred = s_ptr[  0 ]  * PredCoef[ 0 ]  +
                           s_ptr[ -1 ]  * PredCoef[ 1 ]  +
                           s_ptr[ -2 ]  * PredCoef[ 2 ]  +
                           s_ptr[ -3 ]  * PredCoef[ 3 ]  +
                           s_ptr[ -4 ]  * PredCoef[ 4 ]  +
                           s_ptr[ -5 ]  * PredCoef[ 5 ]  +
                           s_ptr[ -6 ]  * PredCoef[ 6 ]  +
                           s_ptr[ -7 ]  * PredCoef[ 7 ]  +
                           s_ptr[ -8 ]  * PredCoef[ 8 ]  +
                           s_ptr[ -9 ]  * PredCoef[ 9 ]  +
                           s_ptr[ -10 ] * PredCoef[ 10 ] +
                           s_ptr[ -11 ] * PredCoef[ 11 ];

                /* prediction error */
                r_LPC[ix] = s_ptr[ 1 ] - LPC_pred;
            }
        }

        /* 10th order LPC analysis filter, does not write first 10 samples */
        internal static unsafe void silk_LPC_analysis_filter10_FLP(
                  float*                 r_LPC,            /* O    LPC residual signal                     */
            in float*                 PredCoef,         /* I    LPC coefficients                        */
            in float*                 s,                /* I    Input signal                            */
            in int                   length              /* I    Length of input signal                  */
        )
        {
            int   ix;
            float LPC_pred;
            float *s_ptr;

            for( ix = 10; ix < length; ix++ ) {
                s_ptr = &s[ix - 1];

                /* short-term prediction */
                LPC_pred = s_ptr[  0 ] * PredCoef[ 0 ]  +
                           s_ptr[ -1 ] * PredCoef[ 1 ]  +
                           s_ptr[ -2 ] * PredCoef[ 2 ]  +
                           s_ptr[ -3 ] * PredCoef[ 3 ]  +
                           s_ptr[ -4 ] * PredCoef[ 4 ]  +
                           s_ptr[ -5 ] * PredCoef[ 5 ]  +
                           s_ptr[ -6 ] * PredCoef[ 6 ]  +
                           s_ptr[ -7 ] * PredCoef[ 7 ]  +
                           s_ptr[ -8 ] * PredCoef[ 8 ]  +
                           s_ptr[ -9 ] * PredCoef[ 9 ];

                /* prediction error */
                r_LPC[ix] = s_ptr[ 1 ] - LPC_pred;
            }
        }

        /* 8th order LPC analysis filter, does not write first 8 samples */
        internal static unsafe void silk_LPC_analysis_filter8_FLP(
                  float*                 r_LPC,            /* O    LPC residual signal                     */
            in float*                 PredCoef,         /* I    LPC coefficients                        */
            in float*                 s,                /* I    Input signal                            */
            in int                   length              /* I    Length of input signal                  */
        )
        {
            int   ix;
            float LPC_pred;
            float *s_ptr;

            for( ix = 8; ix < length; ix++ ) {
                s_ptr = &s[ix - 1];

                /* short-term prediction */
                LPC_pred = s_ptr[  0 ] * PredCoef[ 0 ]  +
                           s_ptr[ -1 ] * PredCoef[ 1 ]  +
                           s_ptr[ -2 ] * PredCoef[ 2 ]  +
                           s_ptr[ -3 ] * PredCoef[ 3 ]  +
                           s_ptr[ -4 ] * PredCoef[ 4 ]  +
                           s_ptr[ -5 ] * PredCoef[ 5 ]  +
                           s_ptr[ -6 ] * PredCoef[ 6 ]  +
                           s_ptr[ -7 ] * PredCoef[ 7 ];

                /* prediction error */
                r_LPC[ix] = s_ptr[ 1 ] - LPC_pred;
            }
        }

        /* 6th order LPC analysis filter, does not write first 6 samples */
        internal static unsafe void silk_LPC_analysis_filter6_FLP(
                  float*                 r_LPC,            /* O    LPC residual signal                     */
            in float*                 PredCoef,         /* I    LPC coefficients                        */
            in float*                 s,                /* I    Input signal                            */
            in int                   length              /* I    Length of input signal                  */
        )
        {
            int   ix;
            float LPC_pred;
            float *s_ptr;

            for( ix = 6; ix < length; ix++ ) {
                s_ptr = &s[ix - 1];

                /* short-term prediction */
                LPC_pred = s_ptr[  0 ] * PredCoef[ 0 ]  +
                           s_ptr[ -1 ] * PredCoef[ 1 ]  +
                           s_ptr[ -2 ] * PredCoef[ 2 ]  +
                           s_ptr[ -3 ] * PredCoef[ 3 ]  +
                           s_ptr[ -4 ] * PredCoef[ 4 ]  +
                           s_ptr[ -5 ] * PredCoef[ 5 ];

                /* prediction error */
                r_LPC[ix] = s_ptr[ 1 ] - LPC_pred;
            }
        }

        /************************************************/
        /* LPC analysis filter                          */
        /* NB! State is kept internally and the         */
        /* filter always starts with zero state         */
        /* first Order output samples are set to zero   */
        /************************************************/
        internal static unsafe void silk_LPC_analysis_filter_FLP(
            float*                      r_LPC,                            /* O    LPC residual signal                         */
            in float*                PredCoef,                         /* I    LPC coefficients                            */
            in float*                s,                                /* I    Input signal                                */
            in int                  length,                             /* I    Length of input signal                      */
            in int                  Order                               /* I    LPC order                                   */
        )
        {
            celt_assert( Order <= length );

            switch( Order ) {
                case 6:
                    silk_LPC_analysis_filter6_FLP(  r_LPC, PredCoef, s, length );
                break;

                case 8:
                    silk_LPC_analysis_filter8_FLP(  r_LPC, PredCoef, s, length );
                break;

                case 10:
                    silk_LPC_analysis_filter10_FLP( r_LPC, PredCoef, s, length );
                break;

                case 12:
                    silk_LPC_analysis_filter12_FLP( r_LPC, PredCoef, s, length );
                break;

                case 16:
                    silk_LPC_analysis_filter16_FLP( r_LPC, PredCoef, s, length );
                break;

                default:
                    celt_assert( false );
                break;
            }

            /* Set first Order output samples to zero */
            silk_memset( r_LPC, 0, Order * sizeof( float ) );
        }
    }
}
