using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    internal static class LinearAlgebra
    {
        /* Solves Ax = b, assuming A is symmetric */
        internal static void silk_solve_LDL(
            Pointer<int> A,                                     /* I    Pointer to symetric square matrix A                                         */
            int M,                                      /* I    Size of matrix                                                              */
            Pointer<int> b,                                     /* I    Pointer to b vector                                                         */
            Pointer<int> x_Q16                                  /* O    Pointer to x solution vector                                                */
            )
        {
            Pointer<int> L_Q16;
            Pointer<int> Y = Pointer.Malloc<int>(SilkConstants.MAX_MATRIX_SIZE);

            // [Porting note] This is an interleaved array. Formerly it was an array of data structures laid out thus:
            //private struct inv_D_t
            //{
            //    int Q36_part;
            //    int Q48_part;
            //}
            Pointer<int> inv_D = Pointer.Malloc<int>(SilkConstants.MAX_MATRIX_SIZE * 2);


            Inlines.OpusAssert(M <= SilkConstants.MAX_MATRIX_SIZE);
            L_Q16 = Pointer.Malloc<int>(M * M);

            /***************************************************
            Factorize A by LDL such that A = L*D*L',
            where L is lower triangular with ones on diagonal
            ****************************************************/
            silk_LDL_factorize(A, M, L_Q16, inv_D);

            /****************************************************
            * substitute D*L'*x = Y. ie:
            L*D*L'*x = b => L*Y = b <=> Y = inv(L)*b
            ******************************************************/
            silk_LS_SolveFirst(L_Q16, M, b, Y);

            /****************************************************
            D*L'*x = Y <=> L'*x = inv(D)*Y, because D is
            diagonal just multiply with 1/d_i
            ****************************************************/
            silk_LS_divide_Q16(Y, inv_D, M);

            /****************************************************
            x = inv(L') * inv(D) * Y
            *****************************************************/
            silk_LS_SolveLast(L_Q16, M, Y, x_Q16);

        }

        /* Factorize square matrix A into LDL form */
        private static void silk_LDL_factorize(
            Pointer<int> A,         /* I/O Pointer to Symetric Square Matrix                            */
            int M,          /* I   Size of Matrix                                               */
            Pointer<int> L_Q16,     /* I/O Pointer to Square Upper triangular Matrix                    */
            Pointer<int> inv_D      /* I/O Pointer to vector holding inverted diagonal elements of D    */
        )
        {
            int i, j, k, status, loop_count;
            Pointer<int> ptr1, ptr2;
            int diag_min_value, tmp_32, err;
            Pointer<int> v_Q0 = Pointer.Malloc<int>(SilkConstants.MAX_MATRIX_SIZE);
            Pointer<int> D_Q0 = Pointer.Malloc<int>(SilkConstants.MAX_MATRIX_SIZE);
            int one_div_diag_Q36, one_div_diag_Q40, one_div_diag_Q48;

            Inlines.OpusAssert(M <= SilkConstants.MAX_MATRIX_SIZE);

            status = 1;
            diag_min_value = Inlines.silk_max_32(Inlines.silk_SMMUL(Inlines.silk_ADD_SAT32(A[0], A[Inlines.silk_SMULBB(M, M) - 1]), Inlines.SILK_CONST(TuningParameters.FIND_LTP_COND_FAC, 31)), 1 << 9);
            for (loop_count = 0; loop_count < M && status == 1; loop_count++)
            {
                status = 0;
                for (j = 0; j < M; j++)
                {
                    ptr1 = Inlines.matrix_adr(L_Q16, j, 0, M);
                    tmp_32 = 0;
                    for (i = 0; i < j; i++)
                    {
                        v_Q0[i] = Inlines.silk_SMULWW(D_Q0[i], ptr1[i]); /* Q0 */
                        tmp_32 = Inlines.silk_SMLAWW(tmp_32, v_Q0[i], ptr1[i]); /* Q0 */
                    }
                    tmp_32 = Inlines.silk_SUB32(Inlines.matrix_ptr(A, j, j, M), tmp_32);

                    if (tmp_32 < diag_min_value)
                    {
                        tmp_32 = Inlines.silk_SUB32(Inlines.silk_SMULBB(loop_count + 1, diag_min_value), tmp_32);
                        /* Matrix not positive semi-definite, or ill conditioned */
                        for (i = 0; i < M; i++)
                        {
                            Inlines.matrix_adr(A, i, i, M)[0] = Inlines.silk_ADD32(Inlines.matrix_ptr(A, i, i, M), tmp_32);
                        }
                        status = 1;
                        break;
                    }
                    D_Q0[j] = tmp_32;                         /* always < max(Correlation) */

                    /* two-step division */
                    one_div_diag_Q36 = Inlines.silk_INVERSE32_varQ(tmp_32, 36);                    /* Q36 */
                    one_div_diag_Q40 = Inlines.silk_LSHIFT(one_div_diag_Q36, 4);                   /* Q40 */
                    err = Inlines.silk_SUB32((int)1 << 24, Inlines.silk_SMULWW(tmp_32, one_div_diag_Q40));     /* Q24 */
                    one_div_diag_Q48 = Inlines.silk_SMULWW(err, one_div_diag_Q40);                 /* Q48 */

                    /* Save 1/Ds */
                    inv_D[(j * 2) + 0] = one_div_diag_Q36;
                    inv_D[(j * 2) + 1] = one_div_diag_Q48;

                    Inlines.matrix_adr(L_Q16, j, j, M)[0] = 65536; /* 1.0 in Q16 */
                    ptr1 = Inlines.matrix_adr(A, j, 0, M);
                    ptr2 = Inlines.matrix_adr(L_Q16, j + 1, 0, M);
                    for (i = j + 1; i < M; i++)
                    {
                        tmp_32 = 0;
                        for (k = 0; k < j; k++)
                        {
                            tmp_32 = Inlines.silk_SMLAWW(tmp_32, v_Q0[k], ptr2[k]); /* Q0 */
                        }
                        tmp_32 = Inlines.silk_SUB32(ptr1[i], tmp_32); /* always < max(Correlation) */

                        /* tmp_32 / D_Q0[j] : Divide to Q16 */
                        Inlines.matrix_adr(L_Q16, i, j, M)[0] = Inlines.silk_ADD32(Inlines.silk_SMMUL(tmp_32, one_div_diag_Q48),
                            Inlines.silk_RSHIFT(Inlines.silk_SMULWW(tmp_32, one_div_diag_Q36), 4));

                        /* go to next column */
                        ptr2 = ptr2.Point(M);
                    }
                }
            }

            Inlines.OpusAssert(status == 0);
        }

        private static void silk_LS_divide_Q16(
            Pointer<int> T,        /* I/O  Numenator vector                                            */
            Pointer<int> inv_D,     /* I    1 / D vector                                                */
            int M           /* I    dimension                                                   */
        )
        {
            int i;
            int tmp_32;
            int one_div_diag_Q36, one_div_diag_Q48;

            for (i = 0; i < M; i++)
            {
                one_div_diag_Q36 = inv_D[(i * 2) + 0];
                one_div_diag_Q48 = inv_D[(i * 2) + 1];

                tmp_32 = T[i];
                T[i] = Inlines.silk_ADD32(Inlines.silk_SMMUL(tmp_32, one_div_diag_Q48), Inlines.silk_RSHIFT(Inlines.silk_SMULWW(tmp_32, one_div_diag_Q36), 4));
            }
        }

        /* Solve Lx = b, when L is lower triangular and has ones on the diagonal */
        private static void silk_LS_SolveFirst(
            Pointer<int> L_Q16,     /* I    Pointer to Lower Triangular Matrix                          */
            int M,          /* I    Dim of Matrix equation                                      */
            Pointer<int> b,         /* I    b Vector                                                    */
            Pointer<int> x_Q16      /* O    x Vector                                                    */
            )
        {
            int i, j;
            Pointer<int> ptr32;
            int tmp_32;

            for (i = 0; i < M; i++)
            {
                ptr32 = Inlines.matrix_adr(L_Q16, i, 0, M);
                tmp_32 = 0;
                for (j = 0; j < i; j++)
                {
                    tmp_32 = Inlines.silk_SMLAWW(tmp_32, ptr32[j], x_Q16[j]);
                }
                x_Q16[i] = Inlines.silk_SUB32(b[i], tmp_32);
            }
        }

        /* Solve L^t*x = b, where L is lower triangular with ones on the diagonal */
        private static void silk_LS_SolveLast(
        Pointer<int> L_Q16,     /* I    Pointer to Lower Triangular Matrix                          */
        int M,          /* I    Dim of Matrix equation                                      */
        Pointer<int> b,         /* I    b Vector                                                    */
            Pointer<int> x_Q16      /* O    x Vector                                                    */
        )
        {
            int i, j;
            Pointer<int> ptr32;
            int tmp_32;

            for (i = M - 1; i >= 0; i--)
            {
                ptr32 = Inlines.matrix_adr(L_Q16, 0, i, M);
                tmp_32 = 0;
                for (j = M - 1; j > i; j--)
                {
                    tmp_32 = Inlines.silk_SMLAWW(tmp_32, ptr32[Inlines.silk_SMULBB(j, M)], x_Q16[j]);
                }
                x_Q16[i] = Inlines.silk_SUB32(b[i], tmp_32);
            }
        }
    }
}
