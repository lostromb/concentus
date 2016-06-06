using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class apply_sine_window
    {
        /* Apply sine window to signal vector.                                      */
        /* Window types:                                                            */
        /*    1 . sine window from 0 to pi/2                                       */
        /*    2 . sine window from pi/2 to pi                                      */
        /* Every other sample is linearly interpolated, for speed.                  */
        /* Window length must be between 16 and 120 (incl) and a multiple of 4.     */

        /* Matlab code for table:
           for k=16:9*4:16+2*9*4, fprintf(' %7.d,', -round(65536*pi ./ (k:4:k+8*4))); fprintf('\n'); end
        */
        private static readonly short[] freq_table_Q16 = {
   12111,    9804,    8235,    7100,    6239,    5565,    5022,    4575,    4202,
    3885,    3612,    3375,    3167,    2984,    2820,    2674,    2542,    2422,
    2313,    2214,    2123,    2038,    1961,    1889,    1822,    1760,    1702,
};

        public static void silk_apply_sine_window(
            Pointer<short> px_win,           /* O    Pointer to windowed signal                                  */
    Pointer<short> px,               /* I    Pointer to input signal                                     */
    int win_type,           /* I    Selects a window type                                       */
    int length              /* I    Window length, multiple of 4                                */
)
        {
            int k, f_Q16, c_Q16;
            int S0_Q16, S1_Q16;

            Inlines.OpusAssert(win_type == 1 || win_type == 2);

            /* Length must be in a range from 16 to 120 and a multiple of 4 */
            Inlines.OpusAssert(length >= 16 && length <= 120);
            Inlines.OpusAssert((length & 3) == 0);

            /* Frequency */
            k = (length >> 2) - 4;
            Inlines.OpusAssert(k >= 0 && k <= 26);
            f_Q16 = (int)freq_table_Q16[k];

            /* Factor used for cosine approximation */
            c_Q16 = Inlines.silk_SMULWB((int)f_Q16, -f_Q16);
            Inlines.OpusAssert(c_Q16 >= -32768);

            /* initialize state */
            if (win_type == 1)
            {
                /* start from 0 */
                S0_Q16 = 0;
                /* approximation of sin(f) */
                S1_Q16 = f_Q16 + Inlines.silk_RSHIFT(length, 3);
            }
            else {
                /* start from 1 */
                S0_Q16 = ((int)1 << 16);
                /* approximation of cos(f) */
                S1_Q16 = ((int)1 << 16) + Inlines.silk_RSHIFT(c_Q16, 1) + Inlines.silk_RSHIFT(length, 4);
            }

            /* Uses the recursive equation:   sin(n*f) = 2 * cos(f) * sin((n-1)*f) - sin((n-2)*f)    */
            /* 4 samples at a time */
            for (k = 0; k < length; k += 4)
            {
                px_win[k] = (short)Inlines.silk_SMULWB(Inlines.silk_RSHIFT(S0_Q16 + S1_Q16, 1), px[k]);
                px_win[k + 1] = (short)Inlines.silk_SMULWB(S1_Q16, px[k + 1]);
                S0_Q16 = Inlines.silk_SMULWB(S1_Q16, c_Q16) + Inlines.silk_LSHIFT(S1_Q16, 1) - S0_Q16 + 1;
                S0_Q16 = Inlines.silk_min(S0_Q16, ((int)1 << 16));

                px_win[k + 2] = (short)Inlines.silk_SMULWB(Inlines.silk_RSHIFT(S0_Q16 + S1_Q16, 1), px[k + 2]);
                px_win[k + 3] = (short)Inlines.silk_SMULWB(S0_Q16, px[k + 3]);
                S1_Q16 = Inlines.silk_SMULWB(S0_Q16, c_Q16) + Inlines.silk_LSHIFT(S0_Q16, 1) - S1_Q16;
                S1_Q16 = Inlines.silk_min(S1_Q16, ((int)1 << 16));
            }
        }
    }
}