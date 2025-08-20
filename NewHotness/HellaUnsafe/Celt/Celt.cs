using HellaUnsafe.Common;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.CeltH;
using static HellaUnsafe.Celt.CELTModeH;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Celt
{
    // celt.c
    internal static unsafe class Celt
    {
        internal static unsafe int resampling_factor(int rate)
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
                    celt_assert(false);
                    ret = 0;
                    break;
            }
            return ret;
        }

        internal static unsafe void comb_filter_const(float* y, float* x, int T, int N,
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
                y[i] = y[i];
                x4 = x3;
                x3 = x2;
                x2 = x1;
                x1 = x0;
            }
        }

        private static readonly Native2DArray<float> gains = new Native2DArray<float>(3, 3, new float[] {
                 QCONST16(0.3066406250f, 15), QCONST16(0.2170410156f, 15), QCONST16(0.1296386719f, 15),
                 QCONST16(0.4638671875f, 15), QCONST16(0.2680664062f, 15), QCONST16(0.0f, 15),
                 QCONST16(0.7998046875f, 15), QCONST16(0.1000976562f, 15), QCONST16(0.0f, 15)});

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
                y[i] = y[i];
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
        internal static readonly Native2DArray<sbyte> tf_select_table = new Native2DArray<sbyte>(4, 8, new sbyte[] {
            /*isTransient=0     isTransient=1 */
              0, -1, 0, -1,    0,-1, 0,-1, /* 2.5 ms */
              0, -1, 0, -2,    1, 0, 1,-1, /* 5 ms */
              0, -2, 0, -3,    2, 0, 1,-1, /* 10 ms */
              0, -2, 0, -3,    3, 0, 1,-1, /* 20 ms */
        });

        internal static unsafe void init_caps(in OpusCustomMode* m, int* cap, int LM, int C)
        {
            int i;
            for (i = 0; i < m->nbEBands; i++)
            {
                int N;
                N = (m->eBands[i + 1] - m->eBands[i]) << LM;
                cap[i] = (m->cache.caps[m->nbEBands * (2 * LM + C - 1) + i] + 64) * C * N >> 2;
            }
        }
    }
}
