using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    internal class AnalysisInfo
    {
        internal int valid = 0;
        internal float tonality = 0;
        internal float tonality_slope = 0;
        internal float noisiness = 0;
        internal float activity = 0;
        internal float music_prob = 0;
        internal int bandwidth = 0;

        internal AnalysisInfo()
        {
        }

        internal void Assign(AnalysisInfo other)
        {
            this.valid = other.valid;
            this.tonality = other.tonality;
            this.tonality_slope = other.tonality_slope;
            this.noisiness = other.noisiness;
            this.activity = other.activity;
            this.music_prob = other.music_prob;
            this.bandwidth = other.bandwidth;
        }

        internal void Reset()
        {
            valid = 0;
            tonality = 0;
            tonality_slope = 0;
            noisiness = 0;
            activity = 0;
            music_prob = 0;
            bandwidth = 0;
        }
    }
}
