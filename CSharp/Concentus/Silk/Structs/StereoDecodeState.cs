using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    internal class StereoDecodeState
    {
        internal readonly Pointer<short> pred_prev_Q13 = Pointer.Malloc<short>(2);
        internal readonly Pointer<short> sMid = Pointer.Malloc<short>(2);
        internal readonly Pointer<short> sSide = Pointer.Malloc<short>(2);

        internal void Reset()
        {
            pred_prev_Q13.MemSet(0, 2);
            sMid.MemSet(0, 2);
            sSide.MemSet(0, 2);
        }
    }
}
