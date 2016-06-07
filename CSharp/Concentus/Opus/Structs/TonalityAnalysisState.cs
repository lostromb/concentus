using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Opus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Structs
{
    public class TonalityAnalysisState
    {
        public int arch;
        public /*readonly*/ Pointer<float> angle = Pointer.Malloc<float>(240);
        public /*readonly*/ Pointer<float> d_angle = Pointer.Malloc<float>(240);
        public /*readonly*/ Pointer<float> d2_angle = Pointer.Malloc<float>(240);
        public /*readonly*/ Pointer<int> inmem = Pointer.Malloc<int>(OpusConstants.ANALYSIS_BUF_SIZE);
        public int mem_fill;                      /* number of usable samples in the buffer */
        public /*readonly*/ Pointer<float> prev_band_tonality = Pointer.Malloc<float>(OpusConstants.NB_TBANDS);
        public float prev_tonality;
        public /*readonly*/ Pointer<Pointer<float>> E = Arrays.InitTwoDimensionalArrayPointer<float>(OpusConstants.NB_FRAMES, OpusConstants.NB_TBANDS);
        public /*readonly*/ Pointer<float> lowE = Pointer.Malloc<float>(OpusConstants.NB_TBANDS);
        public /*readonly*/ Pointer<float> highE = Pointer.Malloc<float>(OpusConstants.NB_TBANDS);
        public /*readonly*/ Pointer<float> meanE = Pointer.Malloc<float>(OpusConstants.NB_TOT_BANDS);
        public /*readonly*/ Pointer<float> mem = Pointer.Malloc<float>(32);
        public /*readonly*/ Pointer<float> cmean = Pointer.Malloc<float>(8);
        public /*readonly*/ Pointer<float> std = Pointer.Malloc<float>(9);
        public float music_prob;
        public float Etracker;
        public float lowECount;
        public int E_count;
        public int last_music;
        public int last_transition;
        public int count;
        public /*readonly*/ Pointer<float> subframe_mem = Pointer.Malloc<float>(3);
        public int analysis_offset;
        /** Probability of having speech for time i to DETECT_SIZE-1 (and music before).
            pspeech[0] is the probability that all frames in the window are speech. */
        public /*readonly*/ Pointer<float> pspeech = Pointer.Malloc<float>(OpusConstants.DETECT_SIZE);
        /** Probability of having music for time i to DETECT_SIZE-1 (and speech before).
            pmusic[0] is the probability that all frames in the window are music. */
        public /*readonly*/ Pointer<float> pmusic = Pointer.Malloc<float>(OpusConstants.DETECT_SIZE);
        public float speech_confidence;
        public float music_confidence;
        public int speech_confidence_count;
        public int music_confidence_count;
        public int write_pos;
        public int read_pos;
        public int read_subframe;
        public /*readonly*/ AnalysisInfo[] info = new AnalysisInfo[OpusConstants.DETECT_SIZE];

        public TonalityAnalysisState()
        {
            for (int c = 0; c < OpusConstants.DETECT_SIZE; c++)
            {
                info[c] = new AnalysisInfo();
            }
        }

        public void Reset()
        {
            arch = 0;
            angle.MemSet(0, 240);
            d_angle.MemSet(0, 240);
            d2_angle.MemSet(0, 240);
            inmem.MemSet(0, OpusConstants.ANALYSIS_BUF_SIZE);
            mem_fill = 0;
            prev_band_tonality.MemSet(0, OpusConstants.NB_TBANDS);
            prev_tonality = 0;
            for (int c = 0; c < OpusConstants.NB_FRAMES; c++)
            {
                E[c].MemSet(0, OpusConstants.NB_TBANDS);
            }
            lowE.MemSet(0, OpusConstants.NB_TBANDS);
            highE.MemSet(0, OpusConstants.NB_TBANDS);
            meanE.MemSet(0, OpusConstants.NB_TOT_BANDS);
            mem.MemSet(0, 32);
            cmean.MemSet(0, 8);
            std.MemSet(0, 9);
            music_prob = 0;
            Etracker = 0;
            lowECount = 0;
            E_count = 0;
            last_music = 0;
            last_transition = 0;
            count = 0;
            subframe_mem.MemSet(0, 3);
            analysis_offset = 0;
            pspeech.MemSet(0, OpusConstants.DETECT_SIZE);
            pmusic.MemSet(0, OpusConstants.DETECT_SIZE);
            speech_confidence = 0;
            music_confidence = 0;
            speech_confidence_count = 0;
            music_confidence_count = 0;
            write_pos = 0;
            read_pos = 0;
            read_subframe = 0;
            for (int c = 0; c < OpusConstants.DETECT_SIZE; c++)
            {
                info[c].Reset();
            }
        }
    }
}
