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
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.TuningParameters;
using static HellaUnsafe.Silk.Float.BurgModified;
using static HellaUnsafe.Silk.Float.Energy;
using static HellaUnsafe.Silk.Float.InnerProduct;
using static HellaUnsafe.Silk.Float.SigProcFLP;

namespace HellaUnsafe.Silk.Float
{
    internal static class FindLPC
    {
        /* LPC analysis */
        internal static unsafe void silk_find_LPC_FLP(
            silk_encoder_state* psEncC,                            /* I/O  Encoder state                               */
            short* NLSF_Q15,                         /* O    NLSFs                                       */
            in float* x,                                /* I    Input signal                                */
            in float minInvGain                         /* I    Inverse of max prediction gain              */
        )
        {
            int k, subfr_length;
            Span<float> a_array = stackalloc float[MAX_LPC_ORDER];

            /* Used only for NLSF interpolation */
            float res_nrg, res_nrg_2nd, res_nrg_interp;
            Span<short> NLSF0_Q15_array = stackalloc short[MAX_LPC_ORDER];
            Span<float> a_tmp_array = stackalloc float[MAX_LPC_ORDER];
            Span<float> LPC_res_array = stackalloc float[MAX_FRAME_LENGTH + MAX_NB_SUBFR * MAX_LPC_ORDER];
            fixed (float* a = a_array)
            fixed (short* NLSF0_Q15 = NLSF0_Q15_array)
            fixed (float* a_tmp = a_tmp_array)
            fixed (float* LPC_res = LPC_res_array)
            {
                subfr_length = psEncC->subfr_length + psEncC->predictLPCOrder;

                /* Default: No interpolation */
                psEncC->indices.NLSFInterpCoef_Q2 = 4;

                /* Burg AR analysis for the full frame */
                res_nrg = silk_burg_modified_FLP(a, x, minInvGain, subfr_length, psEncC->nb_subfr, psEncC->predictLPCOrder);

                if (psEncC->useInterpolatedNLSFs != 0 && psEncC->first_frame_after_reset == 0 && psEncC->nb_subfr == MAX_NB_SUBFR)
                {
                    /* Optimal solution for last 10 ms; subtract residual energy here, as that's easier than        */
                    /* adding it to the residual energy of the first 10 ms in each iteration of the search below    */
                    res_nrg -= silk_burg_modified_FLP(a_tmp, x + (MAX_NB_SUBFR / 2) * subfr_length, minInvGain, subfr_length, MAX_NB_SUBFR / 2, psEncC.predictLPCOrder, arch);

                    /* Convert to NLSFs */
                    silk_A2NLSF_FLP(NLSF_Q15, a_tmp, psEncC->predictLPCOrder);

                    /* Search over interpolation indices to find the one with lowest residual energy */
                    res_nrg_2nd = silk_float_MAX;
                    for (k = 3; k >= 0; k--)
                    {
                        /* Interpolate NLSFs for first half */
                        silk_interpolate(NLSF0_Q15, psEncC->prev_NLSFq_Q15, NLSF_Q15, k, psEncC->predictLPCOrder);

                        /* Convert to LPC for residual energy evaluation */
                        silk_NLSF2A_FLP(a_tmp, NLSF0_Q15, psEncC->predictLPCOrder);

                        /* Calculate residual energy with LSF interpolation */
                        silk_LPC_analysis_filter_FLP(LPC_res, a_tmp, x, 2 * subfr_length, psEncC->predictLPCOrder);
                        res_nrg_interp = (float)(
                            silk_energy_FLP(LPC_res + psEncC->predictLPCOrder, subfr_length - psEncC->predictLPCOrder) +
                                silk_energy_FLP(LPC_res + psEncC->predictLPCOrder + subfr_length, subfr_length - psEncC->predictLPCOrder));

                        /* Determine whether current interpolated NLSFs are best so far */
                        if (res_nrg_interp < res_nrg)
                        {
                            /* Interpolation has lower residual energy */
                            res_nrg = res_nrg_interp;
                            psEncC->indices.NLSFInterpCoef_Q2 = (sbyte)k;
                        }
                        else if (res_nrg_interp > res_nrg_2nd)
                        {
                            /* No reason to continue iterating - residual energies will continue to climb */
                            break;
                        }
                        res_nrg_2nd = res_nrg_interp;
                    }
                }

                if (psEncC->indices.NLSFInterpCoef_Q2 == 4)
                {
                    /* NLSF interpolation is currently inactive, calculate NLSFs from full frame AR coefficients */
                    silk_A2NLSF_FLP(NLSF_Q15, a, psEncC->predictLPCOrder);
                }

                ASSERT(psEncC->indices.NLSFInterpCoef_Q2 == 4 ||
                    (psEncC->useInterpolatedNLSFs != 0 && psEncC->first_frame_after_reset == 0 && psEncC->nb_subfr == MAX_NB_SUBFR));
            }
        }
    }
}
