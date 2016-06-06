using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System.Diagnostics;

namespace Concentus.Silk
{
    public static class find_LPC
    {
        /* Finds LPC vector from correlations, and converts to NLSF */
        public static void silk_find_LPC_FIX(
            silk_encoder_state psEncC,                                /* I/O  Encoder state                                                               */
            Pointer<short> NLSF_Q15,                             /* O    NLSFs                                                                       */
            Pointer<short> x,                                    /* I    Input signal                                                                */
            int minInvGain_Q30                          /* I    Inverse of max prediction gain                                              */
        )
        {
            int k, subfr_length;
            Pointer<int> a_Q16 = Pointer.Malloc<int>(SilkConstants.MAX_LPC_ORDER);
            int isInterpLower, shift;
            int res_nrg0, res_nrg1;
            int rshift0, rshift1;
            BoxedValue<int> scratch_box1 = new BoxedValue<int>();
            BoxedValue<int> scratch_box2 = new BoxedValue<int>();

            /* Used only for LSF interpolation */
            Pointer<int> a_tmp_Q16 = Pointer.Malloc<int>(SilkConstants.MAX_LPC_ORDER);
            int res_nrg_interp, res_nrg, res_tmp_nrg;
            int res_nrg_interp_Q, res_nrg_Q, res_tmp_nrg_Q;
            Pointer<short> a_tmp_Q12 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);
            Pointer<short> NLSF0_Q15 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);
            
            subfr_length = psEncC.subfr_length + psEncC.predictLPCOrder;

            /* Default: no interpolation */
            psEncC.indices.NLSFInterpCoef_Q2 = 4;

            /* Burg AR analysis for the full frame */
            burg_modified.silk_burg_modified_c(scratch_box1, scratch_box2, a_Q16, x, minInvGain_Q30, subfr_length, psEncC.nb_subfr, psEncC.predictLPCOrder, psEncC.arch);
            res_nrg = scratch_box1.Val;
            res_nrg_Q = scratch_box2.Val;

            if (psEncC.useInterpolatedNLSFs != 0 && psEncC.first_frame_after_reset == 0 && psEncC.nb_subfr == SilkConstants.MAX_NB_SUBFR)
            {
                Pointer<short> LPC_res;

                /* Optimal solution for last 10 ms */
                burg_modified.silk_burg_modified_c(scratch_box1, scratch_box2, a_tmp_Q16, x.Point(2 * subfr_length), minInvGain_Q30, subfr_length, 2, psEncC.predictLPCOrder, psEncC.arch);
                res_tmp_nrg = scratch_box1.Val;
                res_tmp_nrg_Q = scratch_box2.Val;

                /* subtract residual energy here, as that's easier than adding it to the    */
                /* residual energy of the first 10 ms in each iteration of the search below */
                shift = res_tmp_nrg_Q - res_nrg_Q;
                if (shift >= 0)
                {
                    if (shift < 32)
                    {
                        res_nrg = res_nrg - Inlines.silk_RSHIFT(res_tmp_nrg, shift);
                    }
                }
                else {
                    Debug.Assert(shift > -32);
                    res_nrg = Inlines.silk_RSHIFT(res_nrg, -shift) - res_tmp_nrg;
                    res_nrg_Q = res_tmp_nrg_Q;
                }

                /* Convert to NLSFs */
                NLSF.silk_A2NLSF(NLSF_Q15, a_tmp_Q16, psEncC.predictLPCOrder);

               LPC_res = Pointer.Malloc<short>(2 * subfr_length);

                /* Search over interpolation indices to find the one with lowest residual energy */
                for (k = 3; k >= 0; k--)
                {
                    /* Interpolate NLSFs for first half */
                    Inlines.silk_interpolate(NLSF0_Q15, psEncC.prev_NLSFq_Q15, NLSF_Q15, k, psEncC.predictLPCOrder);

                    /* Convert to LPC for residual energy evaluation */
                    NLSF.silk_NLSF2A(a_tmp_Q12, NLSF0_Q15, psEncC.predictLPCOrder);

                    /* Calculate residual energy with NLSF interpolation */
                    Filters.silk_LPC_analysis_filter(LPC_res, x, a_tmp_Q12, 2 * subfr_length, psEncC.predictLPCOrder, psEncC.arch);
                    
                    SumSqrShift.silk_sum_sqr_shift(scratch_box1, scratch_box2, LPC_res.Point(psEncC.predictLPCOrder), subfr_length - psEncC.predictLPCOrder);
                    res_nrg0 = scratch_box1.Val;
                    rshift0 = scratch_box2.Val;
                    
                    SumSqrShift.silk_sum_sqr_shift(scratch_box1, scratch_box2, LPC_res.Point(psEncC.predictLPCOrder + subfr_length), subfr_length - psEncC.predictLPCOrder);
                    res_nrg1 = scratch_box1.Val;
                    rshift1 = scratch_box2.Val;

                    /* Add subframe energies from first half frame */
                    shift = rshift0 - rshift1;
                    if (shift >= 0)
                    {
                        res_nrg1 = Inlines.silk_RSHIFT(res_nrg1, shift);
                        res_nrg_interp_Q = -rshift0;
                    }
                    else {
                        res_nrg0 = Inlines.silk_RSHIFT(res_nrg0, -shift);
                        res_nrg_interp_Q = -rshift1;
                    }
                    res_nrg_interp = Inlines.silk_ADD32(res_nrg0, res_nrg1);

                    /* Compare with first half energy without NLSF interpolation, or best interpolated value so far */
                    shift = res_nrg_interp_Q - res_nrg_Q;
                    if (shift >= 0)
                    {
                        if (Inlines.silk_RSHIFT(res_nrg_interp, shift) < res_nrg)
                        {
                            isInterpLower = (true ? 1 : 0);
                        }
                        else {
                            isInterpLower = (false ? 1 : 0);
                        }
                    }
                    else {
                        if (-shift < 32)
                        {
                            if (res_nrg_interp < Inlines.silk_RSHIFT(res_nrg, -shift))
                            {
                                isInterpLower = (true ? 1 : 0);
                            }
                            else {
                                isInterpLower = (false ? 1 : 0);
                            }
                        }
                        else {
                            isInterpLower = (false ? 1 : 0);
                        }
                    }

                    /* Determine whether current interpolated NLSFs are best so far */
                    if (isInterpLower == (true ? 1 : 0))
                    {
                        /* Interpolation has lower residual energy */
                        res_nrg = res_nrg_interp;
                        res_nrg_Q = res_nrg_interp_Q;
                        psEncC.indices.NLSFInterpCoef_Q2 = (sbyte)k;
                    }
                }
            }

            if (psEncC.indices.NLSFInterpCoef_Q2 == 4)
            {
                /* NLSF interpolation is currently inactive, calculate NLSFs from full frame AR coefficients */
                NLSF.silk_A2NLSF(NLSF_Q15, a_Q16, psEncC.predictLPCOrder);
            }

            Debug.Assert(psEncC.indices.NLSFInterpCoef_Q2 == 4 || (psEncC.useInterpolatedNLSFs != 0 && psEncC.first_frame_after_reset == 0 && psEncC.nb_subfr == SilkConstants.MAX_NB_SUBFR));

        }
    }
}
