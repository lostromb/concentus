/* Copyright (c) 2011 Xiph.Org Foundation
   Written by Jean-Marc Valin */
/*
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE FOUNDATION OR
   CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System.Runtime.CompilerServices;
using static HellaUnsafe.Old.Celt.Celt;
using static HellaUnsafe.Old.Opus.MLP;

namespace HellaUnsafe.Old.Opus
{
    internal static class Analysis
    {
        internal const int NB_FRAMES = 8;
        internal const int NB_TBANDS = 18;
        internal const int ANALYSIS_BUF_SIZE = 720; /* 30 ms at 24 kHz */

        /* At that point we can stop counting frames because it no longer matters. */
        internal const int ANALYSIS_COUNT_MAX = 10000;
        internal const int DETECT_SIZE = 100;

        internal unsafe struct TonalityAnalysisState
        {
            internal int application;
            internal int Fs;
            //#define TONALITY_ANALYSIS_RESET_START angle
            internal fixed float angle[240];
            internal fixed float d_angle[240];
            internal fixed float d2_angle[240];
            internal fixed float inmem[ANALYSIS_BUF_SIZE];
            internal int mem_fill;                      /* number of usable samples in the buffer */
            internal fixed float prev_band_tonality[NB_TBANDS];
            internal float prev_tonality;
            internal int prev_bandwidth;
            internal fixed float E_2D[NB_FRAMES * NB_TBANDS]; // Porting note: 2D array
            internal fixed float logE_2D[NB_FRAMES * NB_TBANDS]; // Porting note: 2D array
            internal fixed float lowE[NB_TBANDS];
            internal fixed float highE[NB_TBANDS];
            internal fixed float meanE[NB_TBANDS + 1];
            internal fixed float mem[32];
            internal fixed float cmean[8];
            internal fixed float std[9];
            internal float Etracker;
            internal float lowECount;
            internal int E_count;
            internal int count;
            internal int analysis_offset;
            internal int write_pos;
            internal int read_pos;
            internal int read_subframe;
            internal float hp_ener_accum;
            internal int initialized;
            internal fixed float rnn_state[MAX_NEURONS];
            internal fixed float downmix_state[3];

            [InlineArray(DETECT_SIZE)]
            internal unsafe struct analysis_state_array
            {
                internal AnalysisInfo element;
            }


            internal analysis_state_array _info_storage;
            internal AnalysisInfo* info => (AnalysisInfo*)Unsafe.AsPointer(ref _info_storage);
        }
    }
}
