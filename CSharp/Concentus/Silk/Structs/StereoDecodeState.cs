using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    public class StereoDecodeState
    {
        public readonly Pointer<short> pred_prev_Q13 = Pointer.Malloc<short>(2);
        public readonly Pointer<short> sMid = Pointer.Malloc<short>(2);
        public readonly Pointer<short> sSide = Pointer.Malloc<short>(2);

        public void Reset()
        {
            pred_prev_Q13.MemSet(0, 2);
            sMid.MemSet(0, 2);
            sSide.MemSet(0, 2);
        }
    }
}
