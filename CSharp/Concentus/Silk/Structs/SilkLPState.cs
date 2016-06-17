using Concentus.Common;
using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Variable cut-off low-pass filter state
    /// </summary>
    internal class SilkLPState
    {
        /// <summary>
        /// Low pass filter state
        /// </summary>
        internal readonly Pointer<int> In_LP_State = Pointer.Malloc<int>(2);

        /// <summary>
        /// Counter which is mapped to a cut-off frequency
        /// </summary>
        internal int transition_frame_no = 0;

        /// <summary>
        /// Operating mode, <0: switch down, >0: switch up; 0: do nothing
        /// </summary>
        internal int mode = 0;

        internal void Reset()
        {
            In_LP_State.MemSet(0, 2);
            transition_frame_no = 0;
            mode = 0;
        }

        /* Low-pass filter with variable cutoff frequency based on  */
        /* piece-wise linear interpolation between elliptic filters */
        /* Start by setting psEncC.mode <> 0;                      */
        /* Deactivate by setting psEncC.mode = 0;                  */
        internal void silk_LP_variable_cutoff(
            Pointer<short> frame,                         /* I/O  Low-pass filtered output signal             */
            int frame_length                    /* I    Frame length                                */
            )
        {
            Pointer<int> B_Q28 = Pointer.Malloc<int>(SilkConstants.TRANSITION_NB);
            Pointer<int> A_Q28 = Pointer.Malloc<int>(SilkConstants.TRANSITION_NA);
            int fac_Q16 = 0;
            int ind = 0;

            Inlines.OpusAssert(this.transition_frame_no >= 0 && this.transition_frame_no <= SilkConstants.TRANSITION_FRAMES);

            /* Run filter if needed */
            if (this.mode != 0)
            {
                /* Calculate index and interpolation factor for interpolation */
                fac_Q16 = Inlines.silk_LSHIFT(SilkConstants.TRANSITION_FRAMES - this.transition_frame_no, 16 - 6);

                ind = Inlines.silk_RSHIFT(fac_Q16, 16);
                fac_Q16 -= Inlines.silk_LSHIFT(ind, 16);

                Inlines.OpusAssert(ind >= 0);
                Inlines.OpusAssert(ind < SilkConstants.TRANSITION_INT_NUM);

                /* Interpolate filter coefficients */
                Filters.silk_LP_interpolate_filter_taps(B_Q28, A_Q28, ind, fac_Q16);

                /* Update transition frame number for next frame */
                this.transition_frame_no = Inlines.silk_LIMIT(this.transition_frame_no + this.mode, 0, SilkConstants.TRANSITION_FRAMES);

                /* ARMA low-pass filtering */
                Inlines.OpusAssert(SilkConstants.TRANSITION_NB == 3 && SilkConstants.TRANSITION_NA == 2);
                Filters.silk_biquad_alt(frame, B_Q28, A_Q28, this.In_LP_State, frame, frame_length, 1);
            }
        }
    }
}
