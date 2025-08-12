/***********************************************************************
Copyright (c) 2006-2011, Skype Limited. All rights reserved.
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
- Redistributions of source code must retain the above copyright notice,
this list of conditions and the following disclaimer.
- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.
- Neither the name of Internet Society, IETF or IETF Trust, nor the
names of specific contributors, may be used to endorse or promote
products derived from this software without specific prior written
permission.
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
***********************************************************************/

using System;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.Float.SigProcFLP;
using static HellaUnsafe.Silk.Float.Structs_FLP;

namespace HellaUnsafe.Silk
{
    internal static unsafe class BWExpander
    {
        /* Chirp (bandwidth expand) LP AR filter */
        internal static unsafe void silk_bwexpander(
            short* ar,                /* I/O  AR filter to be expanded (without leading 1)                */
            in int d,                  /* I    Length of ar                                                */
            int chirp_Q16           /* I    Chirp factor (typically in the range 0 to 1)                */
        )
        {
            int i;
            int chirp_minus_one_Q16 = chirp_Q16 - 65536;

            /* NB: Dont use silk_SMULWB, instead of silk_RSHIFT_ROUND( silk_MUL(), 16 ), below.  */
            /* Bias in silk_SMULWB can lead to unstable filters                                */
            for (i = 0; i < d - 1; i++)
            {
                ar[i] = (short)silk_RSHIFT_ROUND(silk_MUL(chirp_Q16, ar[i]), 16);
                chirp_Q16 += silk_RSHIFT_ROUND(silk_MUL(chirp_Q16, chirp_minus_one_Q16), 16);
            }
            ar[d - 1] = (short)silk_RSHIFT_ROUND(silk_MUL(chirp_Q16, ar[d - 1]), 16);
        }

        internal static unsafe void silk_bwexpander_32(
            int* ar,                /* I/O  AR filter to be expanded (without leading 1)                */
            in int d,                  /* I    Length of ar                                                */
            int chirp_Q16           /* I    Chirp factor in Q16                                         */
        )
        {
            int i;
            int chirp_minus_one_Q16 = chirp_Q16 - 65536;

            for (i = 0; i < d - 1; i++)
            {
                ar[i] = silk_SMULWW(chirp_Q16, ar[i]);
                chirp_Q16 += silk_RSHIFT_ROUND(silk_MUL(chirp_Q16, chirp_minus_one_Q16), 16);
            }
            ar[d - 1] = silk_SMULWW(chirp_Q16, ar[d - 1]);
        }
    }
}
