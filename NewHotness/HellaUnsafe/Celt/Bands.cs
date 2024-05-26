/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2009 Xiph.Org Foundation
   Copyright (c) 2008-2009 Gregory Maxwell
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

using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Celt.Modes;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Celt.QuantBands;
using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Celt
{
    internal static class Bands
    {
        internal const int SPREAD_NONE = 0;
        internal const int SPREAD_LIGHT = 1;
        internal const int SPREAD_NORMAL = 2;
        internal const int SPREAD_AGGRESSIVE = 3;

        internal static unsafe int hysteresis_decision(float val, in float* thresholds, in float* hysteresis, int N, int prev)
        {
            int i;
            for (i = 0; i < N; i++)
            {
                if (val < thresholds[i])
                    break;
            }
            if (i > prev && val < thresholds[prev] + hysteresis[prev])
                i = prev;
            if (i < prev && val > thresholds[prev - 1] - hysteresis[prev - 1])
                i = prev;
            return i;
        }

        internal static uint celt_lcg_rand(uint seed)
        {
            return 1664525 * seed + 1013904223;
        }

        /* This is a cos() approximation designed to be bit-exact on any platform. Bit exactness
         with this approximation is important because it has an impact on the bit allocation */
        internal static int bitexact_cos(short x)
        {
            int tmp;
            int x2;
            tmp = (4096 + ((int)(x) * (x))) >> 13;
            ASSERT(tmp <= 32767);
            x2 = tmp;
            x2 = (32767 - x2) + FRAC_MUL16(x2, (-7651 + FRAC_MUL16(x2, (8277 + FRAC_MUL16(-626, x2)))));
            ASSERT(x2 <= 32766);
            return 1 + x2;
        }

        internal static int bitexact_log2tan(int isin, int icos)
        {
            int lc;
            int ls;
            lc = EC_ILOG((uint)icos);
            ls = EC_ILOG((uint)isin);
            icos <<= 15 - lc;
            isin <<= 15 - ls;
            return (ls - lc) * (1 << 11)
                  + FRAC_MUL16(isin, FRAC_MUL16(isin, -2597) + 7932)
                  - FRAC_MUL16(icos, FRAC_MUL16(icos, -2597) + 7932);
        }

        internal static unsafe void compute_band_energies(in CELTMode m, in float* X, float* bandE, int end, int C, int LM)
        {
            int i, c, N;
            short[] eBands = m.eBands;
            N = m.shortMdctSize << LM;
            c = 0; do
            {
                for (i = 0; i < end; i++)
                {
                    float sum;
                    sum = 1e-27f + celt_inner_prod(&X[c * N + (eBands[i] << LM)], &X[c * N + (eBands[i] << LM)], (eBands[i + 1] - eBands[i]) << LM);
                    bandE[i + c * m.nbEBands] = celt_sqrt(sum);
                    /*printf ("%f ", bandE[i+c*m->nbEBands]);*/
                }
            } while (++c < C);
            /*printf ("\n");*/
        }

        internal static unsafe void normalise_bands(in CELTMode m, in float* freq, float* X, in float* bandE, int end, int C, int M)
        {
            int i, c, N;
            short[] eBands = m.eBands;
            N = M * m.shortMdctSize;
            c = 0; do
            {
                for (i = 0; i < end; i++)
                {
                    int j;
                    float g = 1.0f / (1e-27f + bandE[i + c * m.nbEBands]);
                    for (j = M * eBands[i]; j < M * eBands[i + 1]; j++)
                        X[j + c * N] = freq[j + c * N] * g;
                }
            } while (++c < C);
        }

        /* De-normalise the energy to produce the synthesis from the unit-energy bands */
        internal static unsafe void denormalise_bands(in CELTMode m, in float* X,
            float* freq, in float* bandLogE, int start,
            int end, int M, int downsample, int silence)
        {
            int i, N;
            int bound;
            float* f;
            float* x;
            short[] eBands = m.eBands;
            N = M * m.shortMdctSize;
            bound = M * eBands[end];
            if (downsample != 1)
                bound = IMIN(bound, N / downsample);
            if (silence != 0)
            {
                bound = 0;
                start = end = 0;
            }
            f = freq;
            x = X + M * eBands[start];
            for (i = 0; i < M * eBands[start]; i++)
                *f++ = 0;
            for (i = start; i < end; i++)
            {
                int j, band_end;
                float g;
                float lg;
                int shift = 0;
                j = M * eBands[i];
                band_end = M * eBands[i + 1];
                lg = SATURATE16(ADD32(bandLogE[i], SHL32((float)eMeans[i], 6)));
                g = celt_exp2(MIN32(32.0f, lg));
                /* Be careful of the fixed-point "else" just above when changing this code */
                do
                {
                    *f++ = SHR32(MULT16_16(*x++, g), shift);
                } while (++j < band_end);
            }
            ASSERT(start <= end);
            OPUS_CLEAR(&freq[bound], N - bound);
        }


    }
}
