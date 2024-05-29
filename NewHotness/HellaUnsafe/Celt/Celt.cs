/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2010 Xiph.Org Foundation
   Copyright (c) 2008 Gregory Maxwell
   Written by Jean-Marc Valin and Gregory Maxwell */
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
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.Modes;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Celt
{
    internal static class Celt
    {
        internal const int SIG_SHIFT = 0; // Unneeded
        internal const int SIG_SAT = 300000000; // Unneeded
        internal const int COMBFILTER_MAXPERIOD = 1024;
        internal const int COMBFILTER_MINPERIOD = 15;

        internal const int LEAK_BANDS = 19;

        internal const int CELT_SET_PREDICTION_REQUEST = 10002;
        internal const int CELT_SET_INPUT_CLIPPING_REQUEST = 10004;
        internal const int CELT_GET_AND_CLEAR_ERROR_REQUEST = 10007;
        internal const int CELT_SET_CHANNELS_REQUEST = 10008;
        internal const int CELT_SET_START_BAND_REQUEST = 10010;
        internal const int CELT_SET_END_BAND_REQUEST = 10012;
        internal const int CELT_GET_MODE_REQUEST = 10015;
        internal const int CELT_SET_SIGNALLING_REQUEST = 10016;
        internal const int CELT_SET_TONALITY_REQUEST = 10018;
        internal const int CELT_SET_TONALITY_SLOPE_REQUEST = 10020;
        internal const int CELT_SET_ANALYSIS_REQUEST = 10022;
        internal const int OPUS_SET_LFE_REQUEST = 10024;
        internal const int OPUS_SET_ENERGY_MASK_REQUEST = 10026;
        internal const int CELT_SET_SILK_INFO_REQUEST = 10028;

        internal static readonly byte[] trim_icdf = { 126, 124, 119, 109, 87, 41, 19, 9, 4, 2, 0 };
        /* Probs: NONE: 21.875%, LIGHT: 6.25%, NORMAL: 65.625%, AGGRESSIVE: 6.25% */
        internal static readonly byte[] spread_icdf = { 25, 23, 2, 0 };

        internal static readonly byte[] tapset_icdf = { 2, 1, 0 };

        internal struct AnalysisInfo
        {
            internal int valid;
            internal float tonality;
            internal float tonality_slope;
            internal float noisiness;
            internal float activity;
            internal float music_prob;
            internal float music_prob_min;
            internal float music_prob_max;
            internal int bandwidth;
            internal float activity_probability;
            internal float max_pitch_ratio;
            /* Store as Q6 char to save space. */
            internal byte[] leak_boost; //[LEAK_BANDS];

            public void Assign(ref AnalysisInfo other)
            {
                this.valid = other.valid;
                this.tonality = other.tonality;
                this.tonality_slope = other.tonality_slope;
                this.noisiness = other.noisiness;
                this.activity = other.activity;
                this.music_prob = other.music_prob;
                this.music_prob_min = other.music_prob_min;
                this.music_prob_max = other.music_prob_max;
                this.bandwidth = other.bandwidth;
                this.activity_probability = other.activity_probability;
                this.max_pitch_ratio = other.max_pitch_ratio;
                ASSERT(this.leak_boost != null);
                ASSERT(other.leak_boost != null);
                ASSERT(this.leak_boost.Length == LEAK_BANDS);
                ASSERT(other.leak_boost.Length == LEAK_BANDS);
                other.leak_boost.AsSpan(0, LEAK_BANDS).CopyTo(this.leak_boost);
            }
        }

        internal struct SILKInfo
        {
            internal int signalType;
            internal int offset;

            public void Assign(ref SILKInfo other)
            {
                this.signalType = other.signalType;
                this.offset = other.offset;
            }
        }

        internal static int resampling_factor(int rate)
        {
            int ret;
            switch (rate)
            {
                case 48000:
                    ret = 1;
                    break;
                case 24000:
                    ret = 2;
                    break;
                case 16000:
                    ret = 3;
                    break;
                case 12000:
                    ret = 4;
                    break;
                case 8000:
                    ret = 6;
                    break;
                default:
                    ASSERT(false, "Invalid resampling factor");
                    ret = 0;
                    break;
            }
            return ret;
        }

        /* This version should be faster on ARM */
        internal static unsafe void comb_filter_const_c_alt(float* y, float* x, int T, int N,
              float g10, float g11, float g12)
        {
            float x0, x1, x2, x3, x4;
            int i;
            x4 = SHL32(x[-T - 2], 1);
            x3 = SHL32(x[-T - 1], 1);
            x2 = SHL32(x[-T], 1);
            x1 = SHL32(x[-T + 1], 1);
            for (i = 0; i < N - 4; i += 5)
            {
                float t;
                x0 = SHL32(x[i - T + 2], 1);
                t = MAC16_32_Q16(x[i], g10, x2);
                t = MAC16_32_Q16(t, g11, ADD32(x1, x3));
                t = MAC16_32_Q16(t, g12, ADD32(x0, x4));
                t = SATURATE(t, SIG_SAT);
                y[i] = t;
                x4 = SHL32(x[i - T + 3], 1);
                t = MAC16_32_Q16(x[i + 1], g10, x1);
                t = MAC16_32_Q16(t, g11, ADD32(x0, x2));
                t = MAC16_32_Q16(t, g12, ADD32(x4, x3));
                t = SATURATE(t, SIG_SAT);
                y[i + 1] = t;
                x3 = SHL32(x[i - T + 4], 1);
                t = MAC16_32_Q16(x[i + 2], g10, x0);
                t = MAC16_32_Q16(t, g11, ADD32(x4, x1));
                t = MAC16_32_Q16(t, g12, ADD32(x3, x2));
                t = SATURATE(t, SIG_SAT);
                y[i + 2] = t;
                x2 = SHL32(x[i - T + 5], 1);
                t = MAC16_32_Q16(x[i + 3], g10, x4);
                t = MAC16_32_Q16(t, g11, ADD32(x3, x0));
                t = MAC16_32_Q16(t, g12, ADD32(x2, x1));
                t = SATURATE(t, SIG_SAT);
                y[i + 3] = t;
                x1 = SHL32(x[i - T + 6], 1);
                t = MAC16_32_Q16(x[i + 4], g10, x3);
                t = MAC16_32_Q16(t, g11, ADD32(x2, x4));
                t = MAC16_32_Q16(t, g12, ADD32(x1, x0));
                t = SATURATE(t, SIG_SAT);
                y[i + 4] = t;
            }
        }

        internal static unsafe void comb_filter_const_c(float* y, float* x, int T, int N,
             float g10, float g11, float g12)
        {
            float x0, x1, x2, x3, x4;
            int i;
            x4 = x[-T - 2];
            x3 = x[-T - 1];
            x2 = x[-T];
            x1 = x[-T + 1];
            for (i = 0; i < N; i++)
            {
                x0 = x[i - T + 2];
                y[i] = x[i]
                         + MULT16_32_Q15(g10, x2)
                         + MULT16_32_Q15(g11, ADD32(x1, x3))
                         + MULT16_32_Q15(g12, ADD32(x0, x4));
                y[i] = SATURATE(y[i], SIG_SAT);
                x4 = x3;
                x3 = x2;
                x2 = x1;
                x1 = x0;
            }
        }

        internal static readonly float[][] gains =
        {
             new float[] {QCONST16(0.3066406250f, 15), QCONST16(0.2170410156f, 15), QCONST16(0.1296386719f, 15)},
             new float[] {QCONST16(0.4638671875f, 15), QCONST16(0.2680664062f, 15), QCONST16(0.0f, 15)},
             new float[] {QCONST16(0.7998046875f, 15), QCONST16(0.1000976562f, 15), QCONST16(0.0f, 15)}
        };

        internal static unsafe void comb_filter(float* y, float* x, int T0, int T1, int N,
              float g0, float g1, int tapset0, int tapset1,
              in float* window, int overlap)
        {
            int i;
            /* printf ("%d %d %f %f\n", T0, T1, g0, g1); */
            float g00, g01, g02, g10, g11, g12;
            float x0, x1, x2, x3, x4;

            if (g0 == 0 && g1 == 0)
            {
                /* OPT: Happens to work without the OPUS_MOVE(), but only because the current encoder already copies x to y */
                if (x != y)
                    OPUS_MOVE(y, x, N);
                return;
            }
            /* When the gain is zero, T0 and/or T1 is set to zero. We need
               to have then be at least 2 to avoid processing garbage data. */
            T0 = IMAX(T0, COMBFILTER_MINPERIOD);
            T1 = IMAX(T1, COMBFILTER_MINPERIOD);
            g00 = MULT16_16_P15(g0, gains[tapset0][0]);
            g01 = MULT16_16_P15(g0, gains[tapset0][1]);
            g02 = MULT16_16_P15(g0, gains[tapset0][2]);
            g10 = MULT16_16_P15(g1, gains[tapset1][0]);
            g11 = MULT16_16_P15(g1, gains[tapset1][1]);
            g12 = MULT16_16_P15(g1, gains[tapset1][2]);
            x1 = x[-T1 + 1];
            x2 = x[-T1];
            x3 = x[-T1 - 1];
            x4 = x[-T1 - 2];
            /* If the filter didn't change, we don't need the overlap */
            if (g0 == g1 && T0 == T1 && tapset0 == tapset1)
                overlap = 0;
            for (i = 0; i < overlap; i++)
            {
                float f;
                x0 = x[i - T1 + 2];
                f = MULT16_16_Q15(window[i], window[i]);
                y[i] = x[i]
                         + MULT16_32_Q15(MULT16_16_Q15((Q15ONE - f), g00), x[i - T0])
                         + MULT16_32_Q15(MULT16_16_Q15((Q15ONE - f), g01), ADD32(x[i - T0 + 1], x[i - T0 - 1]))
                         + MULT16_32_Q15(MULT16_16_Q15((Q15ONE - f), g02), ADD32(x[i - T0 + 2], x[i - T0 - 2]))
                         + MULT16_32_Q15(MULT16_16_Q15(f, g10), x2)
                         + MULT16_32_Q15(MULT16_16_Q15(f, g11), ADD32(x1, x3))
                         + MULT16_32_Q15(MULT16_16_Q15(f, g12), ADD32(x0, x4));
                y[i] = SATURATE(y[i], SIG_SAT);
                x4 = x3;
                x3 = x2;
                x2 = x1;
                x1 = x0;

            }
            if (g1 == 0)
            {
                /* OPT: Happens to work without the OPUS_MOVE(), but only because the current encoder already copies x to y */
                if (x != y)
                    OPUS_MOVE(y + overlap, x + overlap, N - overlap);
                return;
            }

            /* Compute the part with the constant filter. */
            comb_filter_const(y + i, x + i, T1, N - i, g10, g11, g12);
        }

        /* TF change table. Positive values mean better frequency resolution (longer
           effective window), whereas negative values mean better time resolution
           (shorter effective window). The second index is computed as:
           4*isTransient + 2*tf_select + per_band_flag */
        internal static readonly sbyte[][] tf_select_table = {
            /*isTransient=0     isTransient=1 */
              new sbyte[] {0, -1, 0, -1,    0,-1, 0,-1}, /* 2.5 ms */
              new sbyte[] {0, -1, 0, -2,    1, 0, 1,-1}, /* 5 ms */
              new sbyte[] {0, -2, 0, -3,    2, 0, 1,-1}, /* 10 ms */
              new sbyte[] {0, -2, 0, -3,    3, 0, 1,-1}, /* 20 ms */
        };

        internal static unsafe void init_caps(in CeltCustomMode m, int* cap, int LM, int C)
        {
            int i;
            for (i = 0; i < m.nbEBands; i++)
            {
                int N;
                N = (m.eBands[i + 1] - m.eBands[i]) << LM;
                cap[i] = (m.cache.caps[m.nbEBands * (2 * LM + C - 1) + i] + 64) * C * N >> 2;
            }
        }

        internal static readonly string[] error_strings = {
          "success",
          "invalid argument",
          "buffer too small",
          "internal error",
          "corrupted stream",
          "request not implemented",
          "invalid state",
          "memory allocation failed"
        };

        internal static string opus_strerror(int error)
        {
            if (error > 0 || error < -7)
                return "unknown error";
            else
                return error_strings[-error];
        }

        internal static string opus_get_version_string()
        {
            return "Concentus 0.0.1-unsafe";
        }
    }
}
