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
    public static class decode_core
    {
        /**********************************************************/
        /* Core decoder. Performs inverse NSQ operation LTP + LPC */
        /**********************************************************/
        public static void silk_decode_core(
                silk_decoder_state psDec,                         /* I/O  Decoder state                               */
                silk_decoder_control psDecCtrl,                     /* I    Decoder control                             */
                Pointer<short> xq,                           /* O    Decoded speech                              */
                Pointer<short> pulses     /* I    Pulse signal [MAX_FRAME_LENGTH]                               */
            )
        {
            int i, k, lag = 0, start_idx, sLTP_buf_idx, NLSF_interpolation_flag, signalType;
            Pointer<short> A_Q12;
            Pointer<short> B_Q14;
            Pointer<short> pxq;
            Pointer<short> A_Q12_tmp = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);
            Pointer<short> sLTP;
            Pointer<int> sLTP_Q15;
            int LTP_pred_Q13, LPC_pred_Q10, Gain_Q10, inv_gain_Q31, gain_adj_Q16, rand_seed, offset_Q10;
            Pointer<int> pred_lag_ptr;
            Pointer<int> pexc_Q14;
            Pointer<int> pres_Q14;
            Pointer<int> res_Q14;
            Pointer<int> sLPC_Q14;

            Inlines.OpusAssert(psDec.prev_gain_Q16 != 0);

            sLTP= Pointer.Malloc<short>(psDec.ltp_mem_length);
            sLTP_Q15 = Pointer.Malloc<int>(psDec.ltp_mem_length + psDec.frame_length);
            res_Q14 = Pointer.Malloc<int>(psDec.subfr_length);
            sLPC_Q14 = Pointer.Malloc<int>(psDec.subfr_length + SilkConstants.MAX_LPC_ORDER);

            offset_Q10 = Tables.silk_Quantization_Offsets_Q10[psDec.indices.signalType >> 1][psDec.indices.quantOffsetType];

            if (psDec.indices.NLSFInterpCoef_Q2 < 1 << 2)
            {
                NLSF_interpolation_flag = 1;
            }
            else {
                NLSF_interpolation_flag = 0;
            }

            /* Decode excitation */
            rand_seed = psDec.indices.Seed;
            for (i = 0; i < psDec.frame_length; i++)
            {
                rand_seed = Inlines.silk_RAND(rand_seed);
                psDec.exc_Q14[i] = Inlines.silk_LSHIFT((int)pulses[i], 14);
                if (psDec.exc_Q14[i] > 0)
                {
                    psDec.exc_Q14[i] -= SilkConstants.QUANT_LEVEL_ADJUST_Q10 << 4;
                }
                else
                if (psDec.exc_Q14[i] < 0)
                {
                    psDec.exc_Q14[i] += SilkConstants.QUANT_LEVEL_ADJUST_Q10 << 4;
                }
                psDec.exc_Q14[i] += offset_Q10 << 4;
                if (rand_seed < 0)
                {
                    psDec.exc_Q14[i] = -psDec.exc_Q14[i];
                }

                rand_seed = Inlines.silk_ADD32_ovflw(rand_seed, pulses[i]);
            }

            /* Copy LPC state */
            psDec.sLPC_Q14_buf.MemCopyTo(sLPC_Q14, SilkConstants.MAX_LPC_ORDER);

            pexc_Q14 = psDec.exc_Q14;
            pxq = xq;
            sLTP_buf_idx = psDec.ltp_mem_length;
            /* Loop over subframes */
            for (k = 0; k < psDec.nb_subfr; k++)
            {
                pres_Q14 = res_Q14;
                A_Q12 = psDecCtrl.PredCoef_Q12[k >> 1];

                /* Preload LPC coeficients to array on stack. Gives small performance gain. FIXME no it doesn't anymore */
                A_Q12.MemCopyTo(A_Q12_tmp, psDec.LPC_order);
                B_Q14 = psDecCtrl.LTPCoef_Q14.Point(k * SilkConstants.LTP_ORDER);
                signalType = psDec.indices.signalType;

                Gain_Q10 = Inlines.silk_RSHIFT(psDecCtrl.Gains_Q16[k], 6);
                inv_gain_Q31 = Inlines.silk_INVERSE32_varQ(psDecCtrl.Gains_Q16[k], 47);

                /* Calculate gain adjustment factor */
                if (psDecCtrl.Gains_Q16[k] != psDec.prev_gain_Q16)
                {
                    gain_adj_Q16 = Inlines.silk_DIV32_varQ(psDec.prev_gain_Q16, psDecCtrl.Gains_Q16[k], 16);

                    /* Scale short term state */
                    for (i = 0; i < SilkConstants.MAX_LPC_ORDER; i++)
                    {
                        sLPC_Q14[i] = Inlines.silk_SMULWW(gain_adj_Q16, sLPC_Q14[i]);
                    }
                }
                else {
                    gain_adj_Q16 = (int)1 << 16;
                }

                /* Save inv_gain */
                Inlines.OpusAssert(inv_gain_Q31 != 0);
                psDec.prev_gain_Q16 = psDecCtrl.Gains_Q16[k];

                /* Avoid abrupt transition from voiced PLC to unvoiced normal decoding */
                if (psDec.lossCnt != 0 && psDec.prevSignalType == SilkConstants.TYPE_VOICED &&
                    psDec.indices.signalType != SilkConstants.TYPE_VOICED && k < SilkConstants.MAX_NB_SUBFR / 2)
                {

                    B_Q14.MemSet(0, SilkConstants.LTP_ORDER);
                    B_Q14[SilkConstants.LTP_ORDER / 2] = Inlines.CHOP16(Inlines.SILK_FIX_CONST(0.25f, 14));

                    signalType = SilkConstants.TYPE_VOICED;
                    psDecCtrl.pitchL[k] = psDec.lagPrev;
                }

                if (signalType == SilkConstants.TYPE_VOICED)
                {
                    /* Voiced */
                    lag = psDecCtrl.pitchL[k];

                    /* Re-whitening */
                    if (k == 0 || (k == 2 && (NLSF_interpolation_flag != 0)))
                    {
                        /* Rewhiten with new A coefs */
                        start_idx = psDec.ltp_mem_length - lag - psDec.LPC_order - SilkConstants.LTP_ORDER / 2;
                        Inlines.OpusAssert(start_idx > 0);

                        if (k == 2)
                        {
                            xq.MemCopyTo(psDec.outBuf.Point(psDec.ltp_mem_length), 2 * psDec.subfr_length);
                        }

                        Filters.silk_LPC_analysis_filter(sLTP.Point(start_idx), psDec.outBuf.Point(start_idx + k * psDec.subfr_length),
                            A_Q12, psDec.ltp_mem_length - start_idx, psDec.LPC_order);

                        /* After rewhitening the LTP state is unscaled */
                        if (k == 0)
                        {
                            /* Do LTP downscaling to reduce inter-packet dependency */
                            inv_gain_Q31 = Inlines.silk_LSHIFT(Inlines.silk_SMULWB(inv_gain_Q31, psDecCtrl.LTP_scale_Q14), 2);
                        }
                        for (i = 0; i < lag + SilkConstants.LTP_ORDER / 2; i++)
                        {
                            sLTP_Q15[sLTP_buf_idx - i - 1] = Inlines.silk_SMULWB(inv_gain_Q31, sLTP[psDec.ltp_mem_length - i - 1]);
                        }
                    }
                    else {
                        /* Update LTP state when Gain changes */
                        if (gain_adj_Q16 != (int)1 << 16)
                        {
                            for (i = 0; i < lag + SilkConstants.LTP_ORDER / 2; i++)
                            {
                                sLTP_Q15[sLTP_buf_idx - i - 1] = Inlines.silk_SMULWW(gain_adj_Q16, sLTP_Q15[sLTP_buf_idx - i - 1]);
                            }
                        }
                    }
                }

                /* Long-term prediction */
                if (signalType == SilkConstants.TYPE_VOICED)
                {
                    /* Set up pointer */
                    pred_lag_ptr = sLTP_Q15.Point(sLTP_buf_idx - lag + SilkConstants.LTP_ORDER / 2);
                    for (i = 0; i < psDec.subfr_length; i++)
                    {
                        /* Unrolled loop */
                        /* Avoids introducing a bias because silk_SMLAWB() always rounds to -inf */
                        LTP_pred_Q13 = 2;
                        LTP_pred_Q13 = Inlines.silk_SMLAWB(LTP_pred_Q13, pred_lag_ptr[0], B_Q14[0]);
                        LTP_pred_Q13 = Inlines.silk_SMLAWB(LTP_pred_Q13, pred_lag_ptr[-1], B_Q14[1]);
                        LTP_pred_Q13 = Inlines.silk_SMLAWB(LTP_pred_Q13, pred_lag_ptr[-2], B_Q14[2]);
                        LTP_pred_Q13 = Inlines.silk_SMLAWB(LTP_pred_Q13, pred_lag_ptr[-3], B_Q14[3]);
                        LTP_pred_Q13 = Inlines.silk_SMLAWB(LTP_pred_Q13, pred_lag_ptr[-4], B_Q14[4]);
                        pred_lag_ptr = pred_lag_ptr.Point(1);

                        /* Generate LPC excitation */
                        pres_Q14[i] = Inlines.silk_ADD_LSHIFT32(pexc_Q14[i], LTP_pred_Q13, 1);

                        /* Update states */
                        sLTP_Q15[sLTP_buf_idx] = Inlines.silk_LSHIFT(pres_Q14[i], 1);
                        sLTP_buf_idx++;
                    }
                }
                else {
                    pres_Q14 = pexc_Q14;
                }

                for (i = 0; i < psDec.subfr_length; i++)
                {
                    /* Short-term prediction */
                    Inlines.OpusAssert(psDec.LPC_order == 10 || psDec.LPC_order == 16);
                    /* Avoids introducing a bias because silk_SMLAWB() always rounds to -inf */
                    LPC_pred_Q10 = Inlines.silk_RSHIFT(psDec.LPC_order, 1);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 1], A_Q12_tmp[0]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 2], A_Q12_tmp[1]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 3], A_Q12_tmp[2]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 4], A_Q12_tmp[3]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 5], A_Q12_tmp[4]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 6], A_Q12_tmp[5]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 7], A_Q12_tmp[6]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 8], A_Q12_tmp[7]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 9], A_Q12_tmp[8]);
                    LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 10], A_Q12_tmp[9]);
                    if (psDec.LPC_order == 16)
                    {
                        LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 11], A_Q12_tmp[10]);
                        LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 12], A_Q12_tmp[11]);
                        LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 13], A_Q12_tmp[12]);
                        LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 14], A_Q12_tmp[13]);
                        LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 15], A_Q12_tmp[14]);
                        LPC_pred_Q10 = Inlines.silk_SMLAWB(LPC_pred_Q10, sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i - 16], A_Q12_tmp[15]);
                    }

                    /* Add prediction to LPC excitation */
                    sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i] = Inlines.silk_ADD_LSHIFT32(pres_Q14[i], LPC_pred_Q10, 4);

                    /* Scale with gain */
                    pxq[i] = (short)Inlines.silk_SAT16(Inlines.silk_RSHIFT_ROUND(Inlines.silk_SMULWW(sLPC_Q14[SilkConstants.MAX_LPC_ORDER + i], Gain_Q10), 8));
                }

                /* DEBUG_STORE_DATA( dec.pcm, pxq, psDec.subfr_length * sizeof( short ) ) */

                /* Update LPC filter state */
                sLPC_Q14.Point(psDec.subfr_length).MemCopyTo(sLPC_Q14, SilkConstants.MAX_LPC_ORDER);
                pexc_Q14 = pexc_Q14.Point(psDec.subfr_length);
                pxq = pxq.Point(psDec.subfr_length);
            }

            /* Save LPC state */
            sLPC_Q14.MemCopyTo(psDec.sLPC_Q14_buf, SilkConstants.MAX_LPC_ORDER);
        }
    }
}
