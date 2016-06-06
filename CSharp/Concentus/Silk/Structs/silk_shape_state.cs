using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Noise shaping analysis state
    /// </summary>
    public class silk_shape_state
    {
        public sbyte LastGainIndex = 0;
        public int HarmBoost_smth_Q16 = 0;
        public int HarmShapeGain_smth_Q16 = 0;
        public int Tilt_smth_Q16 = 0;

        public void Reset()
        {
            LastGainIndex = 0;
            HarmBoost_smth_Q16 = 0;
            HarmShapeGain_smth_Q16 = 0;
            Tilt_smth_Q16 = 0;
        }
    }
}
