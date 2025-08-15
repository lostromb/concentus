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
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.Tables;

namespace HellaUnsafe.Silk
{
    internal static unsafe class CodeSigns
    {
        /*#define silk_enc_map(a)                ((a) > 0 ? 1 : 0)*/
        /*#define silk_dec_map(a)                ((a) > 0 ? 1 : -1)*/
        /* shifting avoids if-statement */
        private static int silk_enc_map(int a)
        {
            return (silk_RSHIFT((a), 15) + 1);
        }

        private static int silk_dec_map(int a)
        {
            return (silk_LSHIFT((a), 1) - 1);
        }

        /* Encodes signs of excitation */
        internal static unsafe void silk_encode_signs(
            ec_ctx* psRangeEnc,                        /* I/O  Compressor data structure                   */
            in sbyte* pulses,                           /* I    pulse signal                                */
            int length,                             /* I    length of input                             */
            in int signalType,                         /* I    Signal type                                 */
            in int quantOffsetType,                    /* I    Quantization offset type                    */
            in int* sum_pulses/*[ MAX_NB_SHELL_BLOCKS ]*/   /* I    Sum of absolute pulses per block            */
        )
        {
            int i, j, p;
            byte* icdf = stackalloc byte[2];
            sbyte* q_ptr;
            byte* icdf_ptr;

            icdf[1] = 0;
            q_ptr = pulses;
            i = silk_SMULBB(7, silk_ADD_LSHIFT(quantOffsetType, signalType, 1));
            icdf_ptr = &silk_sign_iCDF[i];
            length = silk_RSHIFT(length + SHELL_CODEC_FRAME_LENGTH / 2, LOG2_SHELL_CODEC_FRAME_LENGTH);
            for (i = 0; i < length; i++)
            {
                p = sum_pulses[i];
                if (p > 0)
                {
                    icdf[0] = icdf_ptr[silk_min(p & 0x1F, 6)];
                    for (j = 0; j < SHELL_CODEC_FRAME_LENGTH; j++)
                    {
                        if (q_ptr[j] != 0)
                        {
                            ec_enc_icdf(psRangeEnc, silk_enc_map(q_ptr[j]), icdf, 8);
                        }
                    }
                }
                q_ptr += SHELL_CODEC_FRAME_LENGTH;
            }
        }

        /* Decodes signs of excitation */
        internal static unsafe void silk_decode_signs(
            ec_ctx* psRangeDec,                        /* I/O  Compressor data structure                   */
            short* pulses,                           /* I/O  pulse signal                                */
            int length,                             /* I    length of input                             */
            in int signalType,                         /* I    Signal type                                 */
            in int quantOffsetType,                    /* I    Quantization offset type                    */
            in int* sum_pulses/*[ MAX_NB_SHELL_BLOCKS ]*/   /* I    Sum of absolute pulses per block            */
        )
        {
            int i, j, p;
            byte* icdf = stackalloc byte[2];
            short* q_ptr;
            byte* icdf_ptr;

            icdf[1] = 0;
            q_ptr = pulses;
            i = silk_SMULBB(7, silk_ADD_LSHIFT(quantOffsetType, signalType, 1));
            icdf_ptr = &silk_sign_iCDF[i];
            length = silk_RSHIFT(length + SHELL_CODEC_FRAME_LENGTH / 2, LOG2_SHELL_CODEC_FRAME_LENGTH);
            for (i = 0; i < length; i++)
            {
                p = sum_pulses[i];
                if (p > 0)
                {
                    icdf[0] = icdf_ptr[silk_min(p & 0x1F, 6)];
                    for (j = 0; j < SHELL_CODEC_FRAME_LENGTH; j++)
                    {
                        if (q_ptr[j] > 0)
                        {
                            /* attach sign */
                            /* implementation with shift, subtraction, multiplication */
                            q_ptr[j] *= (short)silk_dec_map(ec_dec_icdf(psRangeDec, icdf, 8));
                        }
                    }
                }
                q_ptr += SHELL_CODEC_FRAME_LENGTH;
            }
        }
    }
}
