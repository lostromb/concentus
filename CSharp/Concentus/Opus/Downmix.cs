using Concentus.Celt;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus
{
    internal static class Downmix
    {
        /// <summary>
        /// fixme: refactor this out
        /// </summary>
        /// <typeparam name="T">The type of signal being handled (either short or float)</typeparam>
        /// <param name="_x"></param>
        /// <param name="sub"></param>
        /// <param name="subframe"></param>
        /// <param name="offset"></param>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <param name="C"></param>
        public delegate void downmix_func<T>(T[] _x, int x_ptr, int[] sub, int subframe, int offset, int c1, int c2, int C);

        internal static void downmix_float(float[] x, int x_ptr, int[] sub, int subframe, int offset, int c1, int c2, int C)
        {
            int scale;
            int j;
            int c1x = c1 + x_ptr;
            for (j = 0; j < subframe; j++)
                sub[j] = Inlines.FLOAT2INT16(x[(j + offset) * C + c1x]);
            if (c2 > -1)
            {
                int c2x = c2 + x_ptr;
                for (j = 0; j < subframe; j++)
                    sub[j] += Inlines.FLOAT2INT16(x[(j + offset) * C + c2x]);
            }
            else if (c2 == -2)
            {
                int c;
                int cx;
                for (c = 1; c < C; c++)
                {
                    cx = c + x_ptr;
                    for (j = 0; j < subframe; j++)
                        sub[j] += Inlines.FLOAT2INT16(x[(j + offset) * C + cx]);
                }
            }
            scale = (1 << CeltConstants.SIG_SHIFT);
            if (C == -2)
                scale /= C;
            else
                scale /= 2;
            for (j = 0; j < subframe; j++)
                sub[j] *= scale;
        }

        internal static void downmix_int(short[] x, int x_ptr, int[] sub, int subframe, int offset, int c1, int c2, int C)
        {
            int scale;
            int j;
            for (j = 0; j < subframe; j++)
                sub[j] = x[(j + offset) * C + c1];
            if (c2 > -1)
            {
                for (j = 0; j < subframe; j++)
                    sub[j] += x[(j + offset) * C + c2];
            }
            else if (c2 == -2)
            {
                int c;
                for (c = 1; c < C; c++)
                {
                    for (j = 0; j < subframe; j++)
                        sub[j] += x[(j + offset) * C + c];
                }
            }
            scale = (1 << CeltConstants.SIG_SHIFT);
            if (C == -2)
                scale /= C;
            else
                scale /= 2;
            for (j = 0; j < subframe; j++)
                sub[j] *= scale;
        }

    }
}
