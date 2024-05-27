/* Copyright (c) 2007 CSIRO
   Copyright (c) 2007-2009 Xiph.Org Foundation
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
using static HellaUnsafe.Celt.EntDec;
using static HellaUnsafe.Celt.EntEnc;

namespace HellaUnsafe.Celt
{
    internal static class Laplace
    {
        /* The minimum probability of an energy delta (out of 32768). */
        internal const int LAPLACE_LOG_MINP = 0;

        internal const int LAPLACE_MINP = (1 << LAPLACE_LOG_MINP);

        /* The minimum number of guaranteed representable energy deltas (in one
    direction). */
        internal const int LAPLACE_NMIN = 16;

        internal static uint ec_laplace_get_freq1(uint fs0, int decay)
        {
            uint ft;
            ft = 32768 - LAPLACE_MINP * (2 * LAPLACE_NMIN) - fs0;
            return (uint)(ft * (int)(16384 - decay) >> 15);
        }

        internal static unsafe void ec_laplace_encode(ref ec_ctx enc, in byte* ecbuf, int* value, uint fs, int decay)
        {
            uint fl;
            int val = *value;
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
                    fs = (uint)((fs * (int)decay) >> 15);
                }
                /* Everything beyond that has probability LAPLACE_MINP. */
                if (fs == 0)
                {
                    int di;
                    int ndi_max;
                    ndi_max = (int)(32768 - fl + LAPLACE_MINP - 1) >> LAPLACE_LOG_MINP;
                    ndi_max = (ndi_max - s) >> 1;
                    di = IMIN(val - i, ndi_max - 1);
                    fl += (uint)(2 * di + 1 + s) * LAPLACE_MINP;
                    fs = IMIN(LAPLACE_MINP, 32768 - fl);
                    *value = (i + di + s) ^ s;
                }
                else
                {
                    fs += LAPLACE_MINP;
                    fl += (uint)(fs & ~s);
                }
                ASSERT(fl + fs <= 32768);
                ASSERT(fs > 0);
            }

            ec_encode_bin(ref enc, ecbuf, fl, fl + fs, 15);
        }

        internal static unsafe int ec_laplace_decode(ref ec_ctx dec, in byte* ecbuf, uint fs, int decay)
        {
            int val = 0;
            uint fl;
            uint fm;
            fm = ec_decode_bin(ref dec, 15);
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
                    fs = (uint)(((fs - 2 * LAPLACE_MINP) * (int)decay) >> 15);
                    fs += LAPLACE_MINP;
                    val++;
                }
                /* Everything beyond that has probability LAPLACE_MINP. */
                if (fs <= LAPLACE_MINP)
                {
                    int di;
                    di = (int)(fm - fl) >> (LAPLACE_LOG_MINP + 1);
                    val += di;
                    fl += (uint)(2 * di * LAPLACE_MINP);
                }
                if (fm < fl + fs)
                    val = -val;
                else
                    fl += fs;
            }

            ASSERT(fl < 32768);
            ASSERT(fs > 0);
            ASSERT(fl <= fm);
            ASSERT(fm < IMIN(fl + fs, 32768));
            ec_dec_update(ref dec, ecbuf, fl, IMIN(fl + fs, 32768), 32768);
            return val;
        }

        //internal static unsafe void ec_laplace_encode_p0(ref ec_ctx enc, int value, ushort p0, ushort decay)
        //{
        //    int s;
        //    Span<ushort> sign_icdf = stackalloc ushort[3];
        //    sign_icdf[0] = 32768 - p0;
        //    sign_icdf[1] = sign_icdf[0] / 2;
        //    sign_icdf[2] = 0;
        //    s = value == 0 ? 0 : (value > 0 ? 1 : 2);
        //    ec_enc_icdf16(enc, s, sign_icdf, 15);
        //    value = Abs(value);
        //    if (value != 0)
        //    {
        //        int i;
        //        Span<ushort> icdf = stackalloc ushort[8];
        //        icdf[0] = IMAX(7, decay);
        //        for (i = 1; i < 7; i++)
        //        {
        //            icdf[i] = IMAX(7 - i, (icdf[i - 1] * (int)decay) >> 15);
        //        }
        //        icdf[7] = 0;
        //        value--;
        //        do
        //        {
        //            ec_enc_icdf16(enc, IMIN(value, 7), icdf, 15);
        //            value -= 7;
        //        } while (value >= 0);
        //    }
        //}

        //internal static unsafe int ec_laplace_decode_p0(ref ec_ctx dec, ushort p0, ushort decay)
        //{
        //    int s;
        //    int value;
        //    Span<ushort> sign_icdf = stackalloc ushort[3];
        //    sign_icdf[0] = 32768 - p0;
        //    sign_icdf[1] = sign_icdf[0] / 2;
        //    sign_icdf[2] = 0;
        //    s = ec_dec_icdf16(dec, sign_icdf, 15);
        //    if (s == 2) s = -1;
        //    if (s != 0)
        //    {
        //        int i;
        //        int v;
        //        Span<ushort> icdf = stackalloc ushort[8];
        //        icdf[0] = IMAX(7, decay);
        //        for (i = 1; i < 7; i++)
        //        {
        //            icdf[i] = IMAX(7 - i, (icdf[i - 1] * (opus_int32)decay) >> 15);
        //        }
        //        icdf[7] = 0;
        //        value = 1;
        //        do
        //        {
        //            v = ec_dec_icdf16(dec, icdf, 15);
        //            value += v;
        //        } while (v == 7);
        //        return s * value;
        //    }
        //    else return 0;
        //}
    }
}
