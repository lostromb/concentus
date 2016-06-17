using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Structs
{
    internal class StereoWidthState
    {
        internal int XX;
        internal int XY;
        internal int YY;
        internal int smoothed_width;
        internal int max_follower;

        internal void Reset()
        {
            XX = 0;
            XY = 0;
            YY = 0;
            smoothed_width = 0;
            max_follower = 0;
        }
    }
}
