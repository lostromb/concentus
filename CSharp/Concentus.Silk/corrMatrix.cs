using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    /**********************************************************************
 * Correlation Matrix Computations for LS estimate.
 **********************************************************************/
    public static class corrMatrix
    {
        /* Calculates correlation vector X'*t */
        public static void silk_corrVector_FIX(
            Pointer<short> x,                                     /* I    x vector [L + order - 1] used to form data matrix X                         */
            Pointer<short> t,                                     /* I    Target vector [L]                                                           */
            int L,                                      /* I    Length of vectors                                                           */
            int order,                                  /* I    Max lag for correlation                                                     */
            Pointer<int> Xt,                                    /* O    Pointer to X'*t correlation vector [order]                                  */
            int rshifts,                                /* I    Right shifts of correlations                                                */
            int arch                                    /* I    Run-time architecture                                                       */
        )
        {
            int lag, i;
            Pointer<short> ptr1, ptr2;
            int inner_prod;

            ptr1 = x.Point(order - 1); /* Points to first sample of column 0 of X: X[:,0] */
            ptr2 = t;
            /* Calculate X'*t */
            if (rshifts > 0)
            {
                /* Right shifting used */
                for (lag = 0; lag < order; lag++)
                {
                    inner_prod = 0;
                    for (i = 0; i < L; i++)
                    {
                        inner_prod += Inlines.silk_RSHIFT32(Inlines.silk_SMULBB(ptr1[i], ptr2[i]), rshifts);
                    }
                    Xt[lag] = inner_prod; /* X[:,lag]'*t */
                    ptr1 = ptr1.Point(-1); /* Go to next column of X */
                }
            }
            else {
                Debug.Assert(rshifts == 0);
                for (lag = 0; lag < order; lag++)
                {
                    Xt[lag] = Inlines.silk_inner_prod_aligned(ptr1, ptr2, L, arch); /* X[:,lag]'*t */
                    ptr1 = ptr1.Point(-1); /* Go to next column of X */
                }
            }
        }

        /* Calculates correlation matrix X'*X */
        public static void silk_corrMatrix_FIX(
            Pointer<short> x,                                     /* I    x vector [L + order - 1] used to form data matrix X                         */
            int L,                                      /* I    Length of vectors                                                           */
            int order,                                  /* I    Max lag for correlation                                                     */
            int head_room,                              /* I    Desired headroom                                                            */
            Pointer<int> XX,                                    /* O    Pointer to X'*X correlation matrix [ order x order ]                        */
            BoxedValue<int> rshifts,                               /* I/O  Right shifts of correlations                                                */
            int arch                                    /* I    Run-time architecture                                                       */
        )
        {
            int i, j, lag, head_room_rshifts;
            int energy, rshifts_local;
            Pointer<short> ptr1, ptr2;

            BoxedValue<int> boxed_energy = new BoxedValue<int>();
            BoxedValue<int> boxed_rshift = new BoxedValue<int>();
            /* Calculate energy to find shift used to fit in 32 bits */
            SumSqrShift.silk_sum_sqr_shift(boxed_energy, boxed_rshift, x, L + order - 1);
            energy = boxed_energy.Val;
            rshifts_local = boxed_rshift.Val;
            /* Add shifts to get the desired head room */
            head_room_rshifts = Inlines.silk_max(head_room - Inlines.silk_CLZ32(energy), 0);

            energy = Inlines.silk_RSHIFT32(energy, head_room_rshifts);
            rshifts_local += head_room_rshifts;

            /* Calculate energy of first column (0) of X: X[:,0]'*X[:,0] */
            /* Remove contribution of first order - 1 samples */
            for (i = 0; i < order - 1; i++)
            {
                energy -= Inlines.silk_RSHIFT32(Inlines.silk_SMULBB(x[i], x[i]), rshifts_local);
            }
            if (rshifts_local < rshifts.Val)
            {
                /* Adjust energy */
                energy = Inlines.silk_RSHIFT32(energy, rshifts.Val - rshifts_local);
                rshifts_local = rshifts.Val;
            }

            /* Calculate energy of remaining columns of X: X[:,j]'*X[:,j] */
            /* Fill out the diagonal of the correlation matrix */
            Inlines.matrix_adr(XX, 0, 0, order)[0] = energy;
            ptr1 = x.Point(order - 1); /* First sample of column 0 of X */
            for (j = 1; j < order; j++)
            {
                energy = Inlines.silk_SUB32(energy, Inlines.silk_RSHIFT32(Inlines.silk_SMULBB(ptr1[L - j], ptr1[L - j]), rshifts_local));
                energy = Inlines.silk_ADD32(energy, Inlines.silk_RSHIFT32(Inlines.silk_SMULBB(ptr1[-j], ptr1[-j]), rshifts_local));
                Inlines.matrix_adr(XX, j, j, order)[0] = energy;
            }

            ptr2 = x.Point(order - 2); /* First sample of column 1 of X */
                                  /* Calculate the remaining elements of the correlation matrix */
            if (rshifts_local > 0)
            {
                /* Right shifting used */
                for (lag = 1; lag < order; lag++)
                {
                    /* Inner product of column 0 and column lag: X[:,0]'*X[:,lag] */
                    energy = 0;
                    for (i = 0; i < L; i++)
                    {
                        energy += Inlines.silk_RSHIFT32(Inlines.silk_SMULBB(ptr1[i], ptr2[i]), rshifts_local);
                    }
                    /* Calculate remaining off diagonal: X[:,j]'*X[:,j + lag] */
                    Inlines.matrix_adr(XX, lag, 0, order)[0] = energy;
                    Inlines.matrix_adr(XX, 0, lag, order)[0] = energy;
                    for (j = 1; j < (order - lag); j++)
                    {
                        energy = Inlines.silk_SUB32(energy, Inlines.silk_RSHIFT32(Inlines.silk_SMULBB(ptr1[L - j], ptr2[L - j]), rshifts_local));
                        energy = Inlines.silk_ADD32(energy, Inlines.silk_RSHIFT32(Inlines.silk_SMULBB(ptr1[-j], ptr2[-j]), rshifts_local));
                        Inlines.matrix_adr(XX, lag + j, j, order)[0] = energy;
                        Inlines.matrix_adr(XX, j, lag + j, order)[0] = energy;
                    }
                    ptr2 = ptr2.Point(-1); /* Update pointer to first sample of next column (lag) in X */
                }
            }
            else {
                for (lag = 1; lag < order; lag++)
                {
                    /* Inner product of column 0 and column lag: X[:,0]'*X[:,lag] */
                    energy = Inlines.silk_inner_prod_aligned(ptr1, ptr2, L, arch);
                    Inlines.matrix_adr(XX, lag, 0, order)[0] = energy;
                    Inlines.matrix_adr(XX, 0, lag, order)[0] = energy;
                    /* Calculate remaining off diagonal: X[:,j]'*X[:,j + lag] */
                    for (j = 1; j < (order - lag); j++)
                    {
                        energy = Inlines.silk_SUB32(energy, Inlines.silk_SMULBB(ptr1[L - j], ptr2[L - j]));
                        energy = Inlines.silk_SMLABB(energy, ptr1[-j], ptr2[-j]);
                        Inlines.matrix_adr(XX, lag + j, j, order)[0] = energy;
                        Inlines.matrix_adr(XX, j, lag + j, order)[0] = energy;
                    }
                    ptr2 = ptr2.Point(-1);/* Update pointer to first sample of next column (lag) in X */
                }
            }
            rshifts.Val = rshifts_local;
        }

    }
}
