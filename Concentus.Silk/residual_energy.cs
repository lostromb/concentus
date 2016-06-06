using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class residual_energy
    {
        /* Calculates residual energies of input subframes where all subframes have LPC_order   */
        /* of preceding samples                                                                 */
        public static void silk_residual_energy_FIX(
            Pointer<int> nrgs,                   /* O    Residual energy per subframe  [MAX_NB_SUBFR]                                              */
            Pointer<int> nrgsQ,                  /* O    Q value per subframe   [MAX_NB_SUBFR]                                                     */
            Pointer<short> x,                                    /* I    Input signal                                                                */
            Pointer<short> a_Q12,            /* I    AR coefs for each frame half [2][MAX_LPC_ORDER] (linearized)                                               */
            Pointer<int> gains,                  /* I    Quantization gains [SilkConstants.MAX_NB_SUBFR]                                                         */
            int subfr_length,                           /* I    Subframe length                                                             */
            int nb_subfr,                               /* I    Number of subframes                                                         */
            int LPC_order,                              /* I    LPC order                                                                   */
            int arch                                    /* I    Run-time architecture                                                       */
            )
        {
            int offset, i, j, lz1, lz2;
            BoxedValue<int> rshift = new BoxedValue<int>();
            Pointer<short> LPC_res_ptr;
            Pointer<short> LPC_res;
            Pointer<short> x_ptr;
            int tmp32;


            x_ptr = x;
            offset = LPC_order + subfr_length;

            /* Filter input to create the LPC residual for each frame half, and measure subframe energies */
            LPC_res = Pointer.Malloc<short>((SilkConstants.MAX_NB_SUBFR >> 1) * offset);
            Debug.Assert((nb_subfr >> 1) * (SilkConstants.MAX_NB_SUBFR >> 1) == nb_subfr);
            for (i = 0; i < nb_subfr >> 1; i++)
            {
                /* Calculate half frame LPC residual signal including preceding samples */
                Filters.silk_LPC_analysis_filter(LPC_res, x_ptr, a_Q12.Point(i * SilkConstants.MAX_LPC_ORDER), (SilkConstants.MAX_NB_SUBFR >> 1) * offset, LPC_order, arch);

                /* Point to first subframe of the just calculated LPC residual signal */
                LPC_res_ptr = LPC_res.Point(LPC_order);
                for (j = 0; j < (SilkConstants.MAX_NB_SUBFR >> 1); j++)
                {
                    /* Measure subframe energy */
                    BoxedValue<int> boxed_energy = new BoxedValue<int>();
                    SumSqrShift.silk_sum_sqr_shift(boxed_energy, rshift, LPC_res_ptr, subfr_length);
                    nrgs[i * (SilkConstants.MAX_NB_SUBFR >> 1) + j] = boxed_energy.Val;

                    /* Set Q values for the measured energy */
                    nrgsQ[i * (SilkConstants.MAX_NB_SUBFR >> 1) + j] = 0 - rshift.Val;

                    /* Move to next subframe */
                    LPC_res_ptr = LPC_res_ptr.Point(offset);
                }
                /* Move to next frame half */
                x_ptr = x_ptr.Point((SilkConstants.MAX_NB_SUBFR >> 1) * offset);
            }

            /* Apply the squared subframe gains */
            for (i = 0; i < nb_subfr; i++)
            {
                /* Fully upscale gains and energies */
                lz1 = Inlines.silk_CLZ32(nrgs[i]) - 1;
                lz2 = Inlines.silk_CLZ32(gains[i]) - 1;

                tmp32 = Inlines.silk_LSHIFT32(gains[i], lz2);

                /* Find squared gains */
                tmp32 = Inlines.silk_SMMUL(tmp32, tmp32); /* Q( 2 * lz2 - 32 )*/

                /* Scale energies */
                nrgs[i] = Inlines.silk_SMMUL(tmp32, Inlines.silk_LSHIFT32(nrgs[i], lz1)); /* Q( nrgsQ[ i ] + lz1 + 2 * lz2 - 32 - 32 )*/
                nrgsQ[i] += lz1 + 2 * lz2 - 32 - 32;
            }

        }

        /* Residual energy: nrg = wxx - 2 * wXx * c + c' * wXX * c */
        public static int silk_residual_energy16_covar_FIX(
            Pointer<short> c,                                     /* I    Prediction vector                                                           */
            Pointer<int> wXX,                                   /* I    Correlation matrix                                                          */
            Pointer<int> wXx,                                   /* I    Correlation vector                                                          */
            int wxx,                                    /* I    Signal energy                                                               */
            int D,                                      /* I    Dimension                                                                   */
            int cQ                                      /* I    Q value for c vector 0 - 15                                                 */
        )
        {
            int i, j, lshifts, Qxtra;
            int c_max, w_max, tmp, tmp2, nrg;
            Pointer<int> cn = Pointer.Malloc<int>(SilkConstants.MAX_MATRIX_SIZE);
            Pointer<int> pRow;

            /* Safety checks */
            Debug.Assert(D >= 0);
            Debug.Assert(D <= 16);
            Debug.Assert(cQ > 0);
            Debug.Assert(cQ < 16);

            lshifts = 16 - cQ;
            Qxtra = lshifts;

            c_max = 0;
            for (i = 0; i < D; i++)
            {
                c_max = Inlines.silk_max_32(c_max, Inlines.silk_abs((int)c[i]));
            }
            Qxtra = Inlines.silk_min_int(Qxtra, Inlines.silk_CLZ32(c_max) - 17);

            w_max = Inlines.silk_max_32(wXX[0], wXX[D * D - 1]);
            Qxtra = Inlines.silk_min_int(Qxtra, Inlines.silk_CLZ32(Inlines.silk_MUL(D, Inlines.silk_RSHIFT(Inlines.silk_SMULWB(w_max, c_max), 4))) - 5);
            Qxtra = Inlines.silk_max_int(Qxtra, 0);
            for (i = 0; i < D; i++)
            {
                cn[i] = Inlines.silk_LSHIFT((int)c[i], Qxtra);
                Debug.Assert(Inlines.silk_abs(cn[i]) <= (short.MaxValue + 1)); /* Check that Inlines.silk_SMLAWB can be used */
            }
            lshifts -= Qxtra;

            /* Compute wxx - 2 * wXx * c */
            tmp = 0;
            for (i = 0; i < D; i++)
            {
                tmp = Inlines.silk_SMLAWB(tmp, wXx[i], cn[i]);
            }
            nrg = Inlines.silk_RSHIFT(wxx, 1 + lshifts) - tmp;                         /* Q: -lshifts - 1 */

            /* Add c' * wXX * c, assuming wXX is symmetric */
            tmp2 = 0;
            for (i = 0; i < D; i++)
            {
                tmp = 0;
                pRow = wXX.Point(i * D);
                for (j = i + 1; j < D; j++)
                {
                    tmp = Inlines.silk_SMLAWB(tmp, pRow[j], cn[j]);
                }
                tmp = Inlines.silk_SMLAWB(tmp, Inlines.silk_RSHIFT(pRow[i], 1), cn[i]);
                tmp2 = Inlines.silk_SMLAWB(tmp2, tmp, cn[i]);
            }
            nrg = Inlines.silk_ADD_LSHIFT32(nrg, tmp2, lshifts);                       /* Q: -lshifts - 1 */

            /* Keep one bit free always, because we add them for LSF interpolation */
            if (nrg < 1)
            {
                nrg = 1;
            }
            else if (nrg > Inlines.silk_RSHIFT(int.MaxValue, lshifts + 2))
            {
                nrg = int.MaxValue >> 1;
            }
            else {
                nrg = Inlines.silk_LSHIFT(nrg, lshifts + 1);                           /* Q0 */
            }
            return nrg;

        }
    }
}
