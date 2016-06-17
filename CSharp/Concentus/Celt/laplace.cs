using Concentus.Celt.Enums;
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
    internal static class Laplace
    {
        /* The minimum probability of an energy delta (out of 32768). */
        private const int LAPLACE_LOG_MINP = 0;
        private const uint LAPLACE_MINP = (1 << LAPLACE_LOG_MINP);
        /* The minimum number of guaranteed representable energy deltas (in one
            direction). */
        private const int LAPLACE_NMIN = 16;

        /* When called, decay is positive and at most 11456. */
        internal static uint ec_laplace_get_freq1(uint fs0, int decay)
        {
            uint ft;
            ft = 32768 - LAPLACE_MINP * (2 * LAPLACE_NMIN) - fs0;
            return Inlines.CHOP32U((ft * (int)(16384 - decay)) >> 15);
        }

        internal static void ec_laplace_encode(EntropyCoder enc, BoxedValue<int> value, uint fs, int decay)
        {
            uint fl;
            int val = value.Val;
            fl = 0;
            if (val != 0)
            {
                int s;
                int i;
                s = 0 - (val < 0 ? 1 : 0);
                val = (val + s) ^ s;
                fl = fs;
                fs = ec_laplace_get_freq1(fs, decay);

                /* Search the decaying part of the PDF.*/
                for (i = 1; fs > 0 && i < val; i++)
                {
                    fs *= 2;
                    fl += fs + 2 * LAPLACE_MINP;
                    fs = Inlines.CHOP32U((fs * (int)decay) >> 15);
                }

                /* Everything beyond that has probability LAPLACE_MINP. */
                if (fs == 0)
                {
                    int di;
                    int ndi_max;
                    ndi_max = (int)(32768 - fl + LAPLACE_MINP - 1) >> LAPLACE_LOG_MINP;
                    ndi_max = (ndi_max - s) >> 1;
                    di = Inlines.IMIN(val - i, ndi_max - 1);
                    fl += (uint)(2 * di + 1 + s) * LAPLACE_MINP;
                    fs = Inlines.IMIN(LAPLACE_MINP, 32768 - fl);
                    value.Val = (i + di + s) ^ s;
                }
                else
                {
                    fs += LAPLACE_MINP;
                    fl += Inlines.CHOP32U(fs & ~s);
                }
                Inlines.OpusAssert(fl + fs <= 32768);
                Inlines.OpusAssert(fs > 0);
            }

            enc.ec_encode_bin(fl, fl + fs, 15);
        }

        internal static int ec_laplace_decode(EntropyCoder dec, uint fs, int decay)
        {
            int val = 0;
            uint fl;
            uint fm;
            fm = dec.ec_decode_bin(15);
            fl = 0;

            if (fm >= fs)
            {
                val++;
                fl = fs;
                fs = ec_laplace_get_freq1(fs, decay) + LAPLACE_MINP;
                /* Search the decaying part of the PDF.*/
                while (fs > LAPLACE_MINP && fm >= fl + 2 * fs)
                {
                    fs *= 2;
                    fl += fs;
                    fs = Inlines.CHOP32U(((fs - 2 * LAPLACE_MINP) * (int)decay) >> 15);
                    fs += LAPLACE_MINP;
                    val++;
                }
                /* Everything beyond that has probability LAPLACE_MINP. */
                if (fs <= LAPLACE_MINP)
                {
                    int di;
                    di = (int)(fm - fl) >> (LAPLACE_LOG_MINP + 1);
                    val += di;
                    fl += Inlines.CHOP32U(2 * di * LAPLACE_MINP);
                }
                if (fm < fl + fs)
                    val = -val;
                else
                    fl += fs;
            }

            Inlines.OpusAssert(fl < 32768);
            Inlines.OpusAssert(fs > 0);
            Inlines.OpusAssert(fl <= fm);
            Inlines.OpusAssert(fm < Inlines.IMIN(fl + fs, 32768));

            dec.ec_dec_update(fl, Inlines.IMIN(fl + fs, 32768), 32768);
            return val;
        }

    }
}
