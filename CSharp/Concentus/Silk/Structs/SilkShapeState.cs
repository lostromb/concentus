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
    internal class SilkShapeState
    {
        internal sbyte LastGainIndex = 0;
        internal int HarmBoost_smth_Q16 = 0;
        internal int HarmShapeGain_smth_Q16 = 0;
        internal int Tilt_smth_Q16 = 0;

        internal void Reset()
        {
            LastGainIndex = 0;
            HarmBoost_smth_Q16 = 0;
            HarmShapeGain_smth_Q16 = 0;
            Tilt_smth_Q16 = 0;
        }
    }
}
