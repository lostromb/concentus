using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    public class AnalysisInfo
    {
        public int valid = 0;
        public float tonality = 0;
        public float tonality_slope = 0;
        public float noisiness = 0;
        public float activity = 0;
        public float music_prob = 0;
        public int bandwidth = 0;

        public AnalysisInfo()
        {
        }

        public void Assign(AnalysisInfo other)
        {
            this.valid = other.valid;
            this.tonality = other.tonality;
            this.tonality_slope = other.tonality_slope;
            this.noisiness = other.noisiness;
            this.activity = other.activity;
            this.music_prob = other.music_prob;
            this.bandwidth = other.bandwidth;
        }

        public void Reset()
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
