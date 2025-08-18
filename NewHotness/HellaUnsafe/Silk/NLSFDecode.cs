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

using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.NLSFUnpack;
using static HellaUnsafe.Silk.NLSFStabilize;

namespace HellaUnsafe.Silk
{
    internal static unsafe class NLSFDecode
    {
        /* Predictive dequantizer for NLSF residuals */
        internal static unsafe void silk_NLSF_residual_dequant(          /* O    Returns RD value in Q30                     */
                  short* x_Q10,                        /* O    Output [ order ]                            */
            in sbyte* indices,                      /* I    Quantization indices [ order ]              */
            in byte* pred_coef_Q8,                 /* I    Backward predictor coefs [ order ]          */
            in int quant_step_size_Q16,            /* I    Quantization step size                      */
            in short order                           /* I    Number of input values                      */
        )
        {
            int i, pred_Q10;
            short out_Q10;

            out_Q10 = 0;
            for (i = order - 1; i >= 0; i--)
            {
                pred_Q10 = silk_RSHIFT(silk_SMULBB(out_Q10, (short)pred_coef_Q8[i]), 8);
                out_Q10 = silk_LSHIFT16(indices[i], 10);
                if (out_Q10 > 0)
                {
                    out_Q10 = silk_SUB16(out_Q10, (short)SILK_FIX_CONST(NLSF_QUANT_LEVEL_ADJ, 10));
                }
                else if (out_Q10 < 0)
                {
                    out_Q10 = silk_ADD16(out_Q10, (short)SILK_FIX_CONST(NLSF_QUANT_LEVEL_ADJ, 10));
                }
                out_Q10 = (short)silk_SMLAWB(pred_Q10, (int)out_Q10, quant_step_size_Q16);
                x_Q10[i] = (short)out_Q10;
            }
        }


        /***********************/
        /* NLSF vector decoder */
        /***********************/
        internal static unsafe void silk_NLSF_decode(
                  short* pNLSF_Q15,                     /* O    Quantized NLSF vector [ LPC_ORDER ]         */
                  sbyte* NLSFIndices,                   /* I    Codebook path vector [ LPC_ORDER + 1 ]      */
            in silk_NLSF_CB_struct* psNLSF_CB                      /* I    Codebook object                             */
        )
        {
            int i;
            byte* pred_Q8 = stackalloc byte[MAX_LPC_ORDER];
            short* ec_ix = stackalloc short[MAX_LPC_ORDER];
            short* res_Q10 = stackalloc short[MAX_LPC_ORDER];
            int NLSF_Q15_tmp;
            byte* pCB_element;
            short* pCB_Wght_Q9;

            /* Unpack entropy table indices and predictor for current CB1 index */
            silk_NLSF_unpack(ec_ix, pred_Q8, psNLSF_CB, NLSFIndices[0]);

            /* Predictive residual dequantizer */
            silk_NLSF_residual_dequant(res_Q10, &NLSFIndices[1], pred_Q8, psNLSF_CB->quantStepSize_Q16, psNLSF_CB->order);

            /* Apply inverse square-rooted weights to first stage and add to output */
            pCB_element = &psNLSF_CB->CB1_NLSF_Q8[NLSFIndices[0] * psNLSF_CB->order];
            pCB_Wght_Q9 = &psNLSF_CB->CB1_Wght_Q9[NLSFIndices[0] * psNLSF_CB->order];
            for (i = 0; i < psNLSF_CB->order; i++)
            {
                NLSF_Q15_tmp = silk_ADD_LSHIFT32(silk_DIV32_16(silk_LSHIFT((int)res_Q10[i], 14), pCB_Wght_Q9[i]), (short)pCB_element[i], 7);
                pNLSF_Q15[i] = (short)silk_LIMIT(NLSF_Q15_tmp, 0, 32767);
            }

            /* NLSF stabilization */
            silk_NLSF_stabilize(pNLSF_Q15, psNLSF_CB->deltaMin_Q15, psNLSF_CB->order);
        }
    }
}
