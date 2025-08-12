using System;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.Float.SigProcFLP;
using static HellaUnsafe.Silk.Float.Structs_FLP;

namespace HellaUnsafe.Silk
{
    internal static unsafe class Inlines
    {
        /* count leading zeros of long */
        internal static /*OPUS_INLINE*/ int silk_CLZ64(long input)
        {
            int in_upper;

            in_upper = (int)silk_RSHIFT64(input, 32);
            if (in_upper == 0)
            {
                /* Search in the lower 32 bits */
                return 32 + silk_CLZ32((int)input);
            }
            else
            {
                /* Search in the upper 32 bits */
                return silk_CLZ32(in_upper);
            }
        }

        /* get number of leading zeros and fractional part (the bits right after the leading one */
        internal static /*OPUS_INLINE*/ void silk_CLZ_FRAC(
            int input,            /* I  input                               */
            int* lz,           /* O  number of leading zeros             */
            int* frac_Q7       /* O  the 7 bits right after the leading one */
        )
        {
            int lzeros = silk_CLZ32(input);

            *lz = lzeros;
            *frac_Q7 = silk_ROR32(input, 24 - lzeros) & 0x7f;
        }

        /* Approximation of square root                                          */
        /* Accuracy: < +/- 10%  for output values > 15                           */
        /*           < +/- 2.5% for output values > 120                          */
        internal static /*OPUS_INLINE*/ int silk_SQRT_APPROX(int x)
        {
            int y, lz, frac_Q7;

            if (x <= 0)
            {
                return 0;
            }

            silk_CLZ_FRAC(x, &lz, &frac_Q7);

            if ((lz & 1) != 0)
            {
                y = 32768;
            }
            else
            {
                y = 46214;        /* 46214 = sqrt(2) * 32768 */
            }

            /* get scaling right */
            y >>= silk_RSHIFT(lz, 1);

            /* increment using fractional part of input */
            y = silk_SMLAWB(y, y, silk_SMULBB(213, frac_Q7));

            return y;
        }

        /* Divide two int32 values and return result as int32 in a given Q-domain */
        internal static /*OPUS_INLINE*/ int silk_DIV32_varQ(   /* O    returns a good approximation of "(a32 << Qres) / b32" */
            in int a32,               /* I    numerator (Q0)                  */
            in int b32,               /* I    denominator (Q0)                */
            in int Qres               /* I    Q-domain of result (>= 0)       */
        )
        {
            int a_headrm, b_headrm, lshift;
            int b32_inv, a32_nrm, b32_nrm, result;

            silk_assert(b32 != 0);
            silk_assert(Qres >= 0);

            /* Compute number of bits head room and normalize inputs */
            a_headrm = silk_CLZ32(silk_abs(a32)) - 1;
            a32_nrm = silk_LSHIFT(a32, a_headrm);                                       /* Q: a_headrm                  */
            b_headrm = silk_CLZ32(silk_abs(b32)) - 1;
            b32_nrm = silk_LSHIFT(b32, b_headrm);                                       /* Q: b_headrm                  */

            /* Inverse of b32, with 14 bits of precision */
            b32_inv = silk_DIV32_16(silk_int32_MAX >> 2, silk_RSHIFT(b32_nrm, 16));   /* Q: 29 + 16 - b_headrm        */

            /* First approximation */
            result = silk_SMULWB(a32_nrm, b32_inv);                                     /* Q: 29 + a_headrm - b_headrm  */

            /* Compute residual by subtracting product of denominator and first approximation */
            /* It's OK to overflow because the final value of a32_nrm should always be small */
            a32_nrm = silk_SUB32_ovflw(a32_nrm, silk_LSHIFT_ovflw(silk_SMMUL(b32_nrm, result), 3));  /* Q: a_headrm   */

            /* Refinement */
            result = silk_SMLAWB(result, a32_nrm, b32_inv);                             /* Q: 29 + a_headrm - b_headrm  */

            /* Convert to Qres domain */
            lshift = 29 + a_headrm - b_headrm - Qres;
            if (lshift < 0)
            {
                return silk_LSHIFT_SAT32(result, -lshift);
            }
            else
            {
                if (lshift < 32)
                {
                    return silk_RSHIFT(result, lshift);
                }
                else
                {
                    /* Avoid undefined result */
                    return 0;
                }
            }
        }

        /* Invert int32 value and return result as int32 in a given Q-domain */
        internal static /*OPUS_INLINE*/ int silk_INVERSE32_varQ(   /* O    returns a good approximation of "(1 << Qres) / b32" */
            in int b32,                   /* I    denominator (Q0)                */
            in int Qres                   /* I    Q-domain of result (> 0)        */
        )
        {
            int b_headrm, lshift;
            int b32_inv, b32_nrm, err_Q32, result;

            silk_assert(b32 != 0);
            silk_assert(Qres > 0);

            /* Compute number of bits head room and normalize input */
            b_headrm = silk_CLZ32(silk_abs(b32)) - 1;
            b32_nrm = silk_LSHIFT(b32, b_headrm);                                       /* Q: b_headrm                */

            /* Inverse of b32, with 14 bits of precision */
            b32_inv = silk_DIV32_16(silk_int32_MAX >> 2, silk_RSHIFT(b32_nrm, 16));   /* Q: 29 + 16 - b_headrm    */

            /* First approximation */
            result = silk_LSHIFT(b32_inv, 16);                                          /* Q: 61 - b_headrm            */

            /* Compute residual by subtracting product of denominator and first approximation from one */
            err_Q32 = silk_LSHIFT(((int)1 << 29) - silk_SMULWB(b32_nrm, b32_inv), 3);        /* Q32                        */

            /* Refinement */
            result = silk_SMLAWW(result, err_Q32, b32_inv);                             /* Q: 61 - b_headrm            */

            /* Convert to Qres domain */
            lshift = 61 - b_headrm - Qres;
            if (lshift <= 0)
            {
                return silk_LSHIFT_SAT32(result, -lshift);
            }
            else
            {
                if (lshift < 32)
                {
                    return silk_RSHIFT(result, lshift);
                }
                else
                {
                    /* Avoid undefined result */
                    return 0;
                }
            }
        }
    }
}
