using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Silk
{
    public class DecodeParameters
    {
        /* Decode parameters from payload */
        internal static void silk_decode_parameters(
            SilkChannelDecoder psDec,                         /* I/O  State                                       */
            SilkDecoderControl psDecCtrl,                     /* I/O  Decoder control                             */
            int condCoding                      /* I    The type of conditional coding to use       */
        )
        {
            int i, k, Ix;
            Pointer<short> pNLSF_Q15 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);
            Pointer<short> pNLSF0_Q15 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);
            Pointer<sbyte> cbk_ptr_Q7;

            /* Dequant Gains */
            BoxedValue<sbyte> boxedLastGainIndex = new BoxedValue<sbyte>(psDec.LastGainIndex);
            GainQuantization.silk_gains_dequant(psDecCtrl.Gains_Q16, psDec.indices.GainsIndices,
                boxedLastGainIndex, condCoding == SilkConstants.CODE_CONDITIONALLY ? 1 : 0, psDec.nb_subfr);
            psDec.LastGainIndex = boxedLastGainIndex.Val;

            /****************/
            /* Decode NLSFs */
            /****************/
            NLSF.silk_NLSF_decode(pNLSF_Q15, psDec.indices.NLSFIndices, psDec.psNLSF_CB);

            /* Convert NLSF parameters to AR prediction filter coefficients */
            NLSF.silk_NLSF2A(psDecCtrl.PredCoef_Q12[1], pNLSF_Q15, psDec.LPC_order);

            /* If just reset, e.g., because internal Fs changed, do not allow interpolation */
            /* improves the case of packet loss in the first frame after a switch           */
            if (psDec.first_frame_after_reset == 1)
            {
                psDec.indices.NLSFInterpCoef_Q2 = 4;
            }

            if (psDec.indices.NLSFInterpCoef_Q2 < 4)
            {
                /* Calculation of the interpolated NLSF0 vector from the interpolation factor, */
                /* the previous NLSF1, and the current NLSF1                                   */
                for (i = 0; i < psDec.LPC_order; i++)
                {
                    pNLSF0_Q15[i] = Inlines.CHOP16(psDec.prevNLSF_Q15[i] + Inlines.silk_RSHIFT(Inlines.silk_MUL(psDec.indices.NLSFInterpCoef_Q2,
                        pNLSF_Q15[i] - psDec.prevNLSF_Q15[i]), 2));
                }

                /* Convert NLSF parameters to AR prediction filter coefficients */
                NLSF.silk_NLSF2A(psDecCtrl.PredCoef_Q12[0], pNLSF0_Q15, psDec.LPC_order);
            }
            else
            {
                /* Copy LPC coefficients for first half from second half */
                psDecCtrl.PredCoef_Q12[1].MemCopyTo(psDecCtrl.PredCoef_Q12[0], psDec.LPC_order);
            }

            pNLSF_Q15.MemCopyTo(psDec.prevNLSF_Q15, psDec.LPC_order);

            /* After a packet loss do BWE of LPC coefs */
            if (psDec.lossCnt != 0)
            {
                BWExpander.silk_bwexpander(psDecCtrl.PredCoef_Q12[0], psDec.LPC_order, SilkConstants.BWE_AFTER_LOSS_Q16);
                BWExpander.silk_bwexpander(psDecCtrl.PredCoef_Q12[1], psDec.LPC_order, SilkConstants.BWE_AFTER_LOSS_Q16);
            }

            if (psDec.indices.signalType == SilkConstants.TYPE_VOICED)
            {
                /*********************/
                /* Decode pitch lags */
                /*********************/

                /* Decode pitch values */
                DecodePitch.silk_decode_pitch(psDec.indices.lagIndex, psDec.indices.contourIndex, psDecCtrl.pitchL, psDec.fs_kHz, psDec.nb_subfr);

                /* Decode Codebook Index */
                cbk_ptr_Q7 = Tables.silk_LTP_vq_ptrs_Q7[psDec.indices.PERIndex]; /* set pointer to start of codebook */

                for (k = 0; k < psDec.nb_subfr; k++)
                {
                    Ix = psDec.indices.LTPIndex[k];
                    for (i = 0; i < SilkConstants.LTP_ORDER; i++)
                    {
                        psDecCtrl.LTPCoef_Q14[k * SilkConstants.LTP_ORDER + i] = Inlines.CHOP16(Inlines.silk_LSHIFT(cbk_ptr_Q7[Ix * SilkConstants.LTP_ORDER + i], 7));
                    }
                }

                /**********************/
                /* Decode LTP scaling */
                /**********************/
                Ix = psDec.indices.LTP_scaleIndex;
                psDecCtrl.LTP_scale_Q14 = Tables.silk_LTPScales_table_Q14[Ix];
            }
            else
            {
                psDecCtrl.pitchL.MemSet(0, psDec.nb_subfr);
                psDecCtrl.LTPCoef_Q14.MemSet(0, SilkConstants.LTP_ORDER * psDec.nb_subfr);
                psDec.indices.PERIndex = 0;
                psDecCtrl.LTP_scale_Q14 = 0;
            }
        }
    }
}
