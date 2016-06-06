using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Structs
{
    public class StereoWidthState
    {
        public int XX;
        public int XY;
        public int YY;
        public int smoothed_width;
        public int max_follower;

        public void Reset()
        {
            XX = 0;
            XY = 0;
            YY = 0;
            smoothed_width = 0;
            max_follower = 0;
        }
    }
}
