using Concentus.Celt.Enums;
using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Celt
{
    public static class celt
    {
        public static int resampling_factor(int rate)
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
                    Debug.Assert(false);
                    ret = 0;
                    break;
            }
            return ret;
        }

        public static void comb_filter_const_c(Pointer<int> y, Pointer<int> x, int T, int N,
              int g10, int g11, int g12)
        {
            int x0, x1, x2, x3, x4;
            int i;
            x4 = x[-T - 2];
            x3 = x[-T - 1];
            x2 = x[-T];
            x1 = x[-T + 1];
            for (i = 0; i < N; i++)
            {
                x0 = x[i - T + 2];
                y[i] = x[i]
                        + Inlines.MULT16_32_Q15(g10, x2)
                        + Inlines.MULT16_32_Q15(g11, Inlines.ADD32(x1, x3))
                        + Inlines.MULT16_32_Q15(g12, Inlines.ADD32(x0, x4));
                x4 = x3;
                x3 = x2;
                x2 = x1;
                x1 = x0;
            }

        }

        public static void comb_filter(Pointer<int> y, Pointer<int> x, int T0, int T1, int N,
              int g0, int g1, int tapset0, int tapset1,
            Pointer<int> window, int overlap, int arch)
        {
            int i;
            /* printf ("%d %d %f %f\n", T0, T1, g0, g1); */
            int g00, g01, g02, g10, g11, g12;
            int x0, x1, x2, x3, x4;
            short[][] gains = {
                new short[]{ Inlines.QCONST16(0.3066406250f, 15), Inlines.QCONST16(0.2170410156f, 15), Inlines.QCONST16(0.1296386719f, 15)},
                new short[]{ Inlines.QCONST16(0.4638671875f, 15), Inlines.QCONST16(0.2680664062f, 15), Inlines.QCONST16(0.0f, 15)},
                new short[]{ Inlines.QCONST16(0.7998046875f, 15), Inlines.QCONST16(0.1000976562f, 15), Inlines.QCONST16(0.0f, 15)}
            };

            if (g0 == 0 && g1 == 0)
            {
                /* OPT: Happens to work without the OPUS_MOVE(), but only because the current encoder already copies x to y */
                if (x != y)
                {
                    x.MemMoveTo(y, N);
                }

                return;
            }
            g00 = Inlines.MULT16_16_P15(g0, gains[tapset0][0]);
            g01 = Inlines.MULT16_16_P15(g0, gains[tapset0][1]);
            g02 = Inlines.MULT16_16_P15(g0, gains[tapset0][2]);
            g10 = Inlines.MULT16_16_P15(g1, gains[tapset1][0]);
            g11 = Inlines.MULT16_16_P15(g1, gains[tapset1][1]);
            g12 = Inlines.MULT16_16_P15(g1, gains[tapset1][2]);
            x1 = x[-T1 + 1];
            x2 = x[-T1];
            x3 = x[-T1 - 1];
            x4 = x[-T1 - 2];
            /* If the filter didn't change, we don't need the overlap */
            if (g0 == g1 && T0 == T1 && tapset0 == tapset1)
                overlap = 0;
            for (i = 0; i < overlap; i++)
            {
                int f;
                x0 = x[i - T1 + 2];
                f = Inlines.MULT16_16_Q15(window[i], window[i]);
                y[i] = x[i]
                    + Inlines.MULT16_32_Q15(Inlines.MULT16_16_Q15(Inlines.CHOP16(CeltConstants.Q15ONE - f), g00), x[i - T0])
                    + Inlines.MULT16_32_Q15(Inlines.MULT16_16_Q15(Inlines.CHOP16(CeltConstants.Q15ONE - f), g01), Inlines.ADD32(x[i - T0 + 1], x[i - T0 - 1]))
                    + Inlines.MULT16_32_Q15(Inlines.MULT16_16_Q15(Inlines.CHOP16(CeltConstants.Q15ONE - f), g02), Inlines.ADD32(x[i - T0 + 2], x[i - T0 - 2]))
                    + Inlines.MULT16_32_Q15(Inlines.MULT16_16_Q15(f, g10), x2)
                    + Inlines.MULT16_32_Q15(Inlines.MULT16_16_Q15(f, g11), Inlines.ADD32(x1, x3))
                    + Inlines.MULT16_32_Q15(Inlines.MULT16_16_Q15(f, g12), Inlines.ADD32(x0, x4));
                x4 = x3;
                x3 = x2;
                x2 = x1;
                x1 = x0;

            }
            if (g1 == 0)
            {
                /* OPT: Happens to work without the OPUS_MOVE(), but only because the current encoder already copies x to y */
                if (x != y)
                {
                    //OPUS_MOVE(y + overlap, x + overlap, N - overlap);
                    x.Point(overlap).MemMoveTo(y.Point(overlap), N - overlap);
                }
                return;
            }

            /* Compute the part with the constant filter. */
            comb_filter_const_c(y.Point(i), x.Point(i), T1, N - i, g10, g11, g12);
        }

        private static readonly sbyte[][] tf_select_table = {
              new sbyte[]{0, -1, 0, -1,    0,-1, 0,-1},
              new sbyte[]{0, -1, 0, -2,    1, 0, 1,-1},
              new sbyte[]{0, -2, 0, -3,    2, 0, 1,-1},
              new sbyte[]{0, -2, 0, -3,    3, 0, 1,-1},
        };


        public static void init_caps(CELTMode m, Pointer<int> cap, int LM, int C)
        {
            int i;
            for (i = 0; i < m.nbEBands; i++)
            {
                int N;
                N = (m.eBands[i + 1] - m.eBands[i]) << LM;
                cap[i] = (m.cache.caps[m.nbEBands * (2 * LM + C - 1) + i] + 64) * C * N >> 2;
            }
        }

        public static string opus_strerror(int error)
        {
            string[] error_strings = {
              "success",
              "invalid argument",
              "buffer too small",
              "internal error",
              "corrupted stream",
              "request not implemented",
              "invalid state",
              "memory allocation failed"
           };
            if (error > 0 || error < -7)
                return "unknown error";
            else
                return error_strings[-error];
        }

        public static string opus_get_version_string()
        {
            return "concentus 1.0-fixed"
#if FUZZING
          + "-fuzzing"
#endif
          ;
        }
    }
}
