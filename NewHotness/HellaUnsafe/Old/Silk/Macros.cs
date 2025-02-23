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

using static HellaUnsafe.Old.Celt.EntCode;
using static HellaUnsafe.Old.Silk.SigProcFIX;

namespace HellaUnsafe.Old.Silk
{
    internal static class Macros
    {

        /* (a32 * (opus_int32)((opus_int16)(b32))) >> 16 output have to be 32bit int */
        //#define silk_SMULWB(a32, b32)            ((opus_int32)(((a32) * (opus_int64)((opus_int16)(b32))) >> 16))

        /* a32 + (b32 * (opus_int32)((opus_int16)(c32))) >> 16 output have to be 32bit int */
        //#define silk_SMLAWB(a32, b32, c32)       ((opus_int32)((a32) + (((b32) * (opus_int64)((opus_int16)(c32))) >> 16)))

        /* (a32 * (b32 >> 16)) >> 16 */
        //#define silk_SMULWT(a32, b32)            ((opus_int32)(((a32) * (opus_int64)((b32) >> 16)) >> 16))
        internal static int silk_SMULWT(int a32, int b32)
        {
            return (a32 >> 16) * (b32 >> 16) + ((a32 & 0x0000FFFF) * (b32 >> 16) >> 16);
        }

        /* a32 + (b32 * (c32 >> 16)) >> 16 */
        //#define silk_SMLAWT(a32, b32, c32)       ((opus_int32)((a32) + (((b32) * ((opus_int64)(c32) >> 16)) >> 16)))
        internal static int silk_SMLAWT(int a32, int b32, int c32)
        {
            return a32 + (b32 >> 16) * (c32 >> 16) + ((b32 & 0x0000FFFF) * (c32 >> 16) >> 16);
        }

        /* (opus_int32)((opus_int16)(a3))) * (opus_int32)((opus_int16)(b32)) output have to be 32bit int */
        //#define silk_SMULBB(a32, b32)            ((opus_int32)((opus_int16)(a32)) * (opus_int32)((opus_int16)(b32)))

        /* a32 + (opus_int32)((opus_int16)(b32)) * (opus_int32)((opus_int16)(c32)) output have to be 32bit int */
        //#define silk_SMLABB(a32, b32, c32)       ((a32) + ((opus_int32)((opus_int16)(b32))) * (opus_int32)((opus_int16)(c32)))

        /* (opus_int32)((opus_int16)(a32)) * (b32 >> 16) */
        //#define silk_SMULBT(a32, b32)            ((opus_int32)((opus_int16)(a32)) * ((b32) >> 16))
        internal static int silk_SMULBT(int a32, int b32)
        {
            return (short)a32 * (b32 >> 16);
        }

        /* a32 + (opus_int32)((opus_int16)(b32)) * (c32 >> 16) */
        //#define silk_SMLABT(a32, b32, c32)       ((a32) + ((opus_int32)((opus_int16)(b32))) * ((c32) >> 16))
        internal static int silk_SMLABT(int a32, int b32, int c32)
        {
            return a32 + (short)b32 * (c32 >> 16);
        }

        /* a64 + (b32 * c32) */
        //#define silk_SMLAL(a64, b32, c32)        (silk_ADD64((a64), ((opus_int64)(b32) * (opus_int64)(c32))))
        internal static long silk_SMLAL(long a64, int b32, int c32)
        {
            return silk_ADD64(a64, b32 * (long)c32);
        }

        /* (a32 * b32) >> 16 */
        //#define silk_SMULWW(a32, b32)            ((opus_int32)(((opus_int64)(a32) * (b32)) >> 16))

        /* a32 + ((b32 * c32) >> 16) */
        //#define silk_SMLAWW(a32, b32, c32)       ((opus_int32)((a32) + (((opus_int64)(b32) * (c32)) >> 16)))

        /* add/subtract with output saturated */
        //#define silk_ADD_SAT32(a, b)             ((((opus_uint32)(a) + (opus_uint32)(b)) & 0x80000000) == 0 ?                              \
        //        ((((a) & (b)) & 0x80000000) != 0 ? silk_int32_MIN : (a)+(b)) :   \
        //                                        ((((a) | (b)) & 0x80000000) == 0 ? silk_int32_MAX : (a)+(b)) )

        //#define silk_SUB_SAT32(a, b)             ((((opus_uint32)(a)-(opus_uint32)(b)) & 0x80000000) == 0 ?                                        \
        //                                        (((a) & ((b)^0x80000000) & 0x80000000) ? silk_int32_MIN : (a)-(b)) :    \
        //                                        ((((a)^0x80000000) & (b)  & 0x80000000) ? silk_int32_MAX : (a)-(b)) )

        internal static int silk_CLZ16(short in16)
        {
            return 32 - EC_ILOG((uint)(in16 << 16 | 0x8000));
        }

        internal static int silk_CLZ32(int in32)
        {
            return in32 == 0 ? 32 : 32 - EC_ILOG(unchecked((uint)in32));
        }

        /* Row based */
        internal static unsafe ref float matrix_ptr(in float* Matrix_base_adr, int row, int column, int N)
        {
            return ref (*(Matrix_base_adr + (row * N + column)));
        }

        internal static unsafe float* matrix_adr(in float* Matrix_base_adr, int row, int column, int N)
        {
            return Matrix_base_adr + (row * N + column);
        }

        /* Column based */
        internal static unsafe ref float matrix_c_ptr(in float* Matrix_base_adr, int row, int column, int M)
        {
            return ref (*(Matrix_base_adr + (row + M * column)));
        }
    }
}
