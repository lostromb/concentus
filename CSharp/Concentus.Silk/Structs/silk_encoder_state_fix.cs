using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Encoder state FIX
    /// FIXME: Just merge this with the regular encoder state
    /// </summary>
    public class silk_encoder_state_fix
    {
        /* Common struct, shared with fixed-point code */
        public /*readonly*/ silk_encoder_state sCmn = new silk_encoder_state();

        /* Noise shaping state */
        public /*readonly*/ silk_shape_state sShape = new silk_shape_state();

        /* Prefilter State */
        public /*readonly*/ silk_prefilter_state sPrefilt = new silk_prefilter_state();

        /* Buffer for find pitch and noise shape analysis */
        public /*readonly*/ Pointer<short> x_buf = Pointer.Malloc<short>(2 * SilkConstants.MAX_FRAME_LENGTH + SilkConstants.LA_SHAPE_MAX);

        /* Normalized correlation from pitch lag estimator */
        public int LTPCorr_Q15 = 0;

        public void Reset()
        {
            sCmn.Reset();
            sShape.Reset();
            sPrefilt.Reset();
            x_buf.MemSet(0, 2 * SilkConstants.MAX_FRAME_LENGTH + SilkConstants.LA_SHAPE_MAX);
            LTPCorr_Q15 = 0;
        }
    }
}
