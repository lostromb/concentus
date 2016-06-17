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
    public class SilkLPState
    {
        /// <summary>
        /// Low pass filter state
        /// </summary>
        public readonly Pointer<int> In_LP_State = Pointer.Malloc<int>(2);

        /// <summary>
        /// Counter which is mapped to a cut-off frequency
        /// </summary>
        public int transition_frame_no = 0;

        /// <summary>
        /// Operating mode, <0: switch down, >0: switch up; 0: do nothing
        /// </summary>
        public int mode = 0;

        public void Reset()
        {
            In_LP_State.MemSet(0, 2);
            transition_frame_no = 0;
            mode = 0;
        }
    }
}
