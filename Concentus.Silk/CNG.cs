using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    /// <summary>
    /// Comfort noise generation and estimation
    /// </summary>
    public static class CNG
    {
        /// <summary>
        /// Generates excitation for CNG LPC synthesis
        /// </summary>
        /// <param name="exc_Q10">O    CNG excitation signal Q10</param>
        /// <param name="exc_buf_Q14">I    Random samples buffer Q10</param>
        /// <param name="Gain_Q16">I    Gain to apply</param>
        /// <param name="length">I    Length</param>
        /// <param name="rand_seed">I/O  Seed to random index generator</param>
        public static void silk_CNG_exc(
            Pointer<int> exc_Q10,
            Pointer<int> exc_buf_Q14,
            int Gain_Q16,
            int length,
            ref int rand_seed)
        {
            int seed;
            int i, idx, exc_mask;

            exc_mask = SilkConstants.CNG_BUF_MASK_MAX;

            while (exc_mask > length)
            {
                exc_mask = Inlines.silk_RSHIFT(exc_mask, 1);
            }

            seed = rand_seed;
            for (i = 0; i < length; i++)
            {
                seed = Inlines.silk_RAND(seed);
                idx = (int)(Inlines.silk_RSHIFT(seed, 24) & exc_mask);
                Debug.Assert(idx >= 0);
                Debug.Assert(idx <= SilkConstants.CNG_BUF_MASK_MAX);
                exc_Q10[i] = (short)Inlines.silk_SAT16(Inlines.silk_SMULWW(exc_buf_Q14[idx], Gain_Q16 >> 4));
            }

            rand_seed = seed;
        }

        /// <summary>
        /// Resets CNG state
        /// </summary>
        /// <param name="psDec">I/O  Decoder state</param>
        public static void silk_CNG_Reset(silk_decoder_state psDec)
        {
            int i, NLSF_step_Q15, NLSF_acc_Q15;

            NLSF_step_Q15 = Inlines.silk_DIV32_16(short.MaxValue, Inlines.CHOP16(psDec.LPC_order + 1));
            NLSF_acc_Q15 = 0;
            for (i = 0; i < psDec.LPC_order; i++)
            {
                NLSF_acc_Q15 += NLSF_step_Q15;
                psDec.sCNG.CNG_smth_NLSF_Q15[i] = Inlines.CHOP16(NLSF_acc_Q15);
            }
            psDec.sCNG.CNG_smth_Gain_Q16 = 0;
            psDec.sCNG.rand_seed = 3176576;
        }

        /// <summary>
        /// Updates CNG estimate, and applies the CNG when packet was lost
        /// </summary>
        /// <param name="psDec">I/O  Decoder state</param>
        /// <param name="psDecCtrl">I/O  Decoder control</param>
        /// <param name="frame">I/O  Signal</param>
        /// <param name="length">I    Length of residual</param>
        public static void silk_CNG(
            silk_decoder_state psDec,
            silk_decoder_control psDecCtrl,
            Pointer<short> frame,
            int length)
        {
            int i, subfr;
            int sum_Q6, max_Gain_Q16, gain_Q16;
            Pointer<short> A_Q12 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);
            silk_CNG_struct psCNG = psDec.sCNG;

            if (psDec.fs_kHz != psCNG.fs_kHz)
            {
                /* Reset state */
                silk_CNG_Reset(psDec);

                psCNG.fs_kHz = psDec.fs_kHz;
            }

            if (psDec.lossCnt == 0 && psDec.prevSignalType == SilkConstants.TYPE_NO_VOICE_ACTIVITY)
            {
                /* Update CNG parameters */

                /* Smoothing of LSF's  */
                for (i = 0; i < psDec.LPC_order; i++)
                {
                    psCNG.CNG_smth_NLSF_Q15[i] += Inlines.CHOP16(Inlines.silk_SMULWB((int)psDec.prevNLSF_Q15[i] - (int)psCNG.CNG_smth_NLSF_Q15[i], SilkConstants.CNG_NLSF_SMTH_Q16));
                }

                /* Find the subframe with the highest gain */
                max_Gain_Q16 = 0;
                subfr = 0;
                for (i = 0; i < psDec.nb_subfr; i++)
                {
                    if (psDecCtrl.Gains_Q16[i] > max_Gain_Q16)
                    {
                        max_Gain_Q16 = psDecCtrl.Gains_Q16[i];
                        subfr = i;
                    }
                }

                /* Update CNG excitation buffer with excitation from this subframe */
                // FIXME this pointer can be cached for performance
                psCNG.CNG_exc_buf_Q14.MemMove(psDec.subfr_length, (psDec.nb_subfr - 1) * psDec.subfr_length);

                /* Smooth gains */
                for (i = 0; i < psDec.nb_subfr; i++)
                {
                    psCNG.CNG_smth_Gain_Q16 += Inlines.silk_SMULWB(psDecCtrl.Gains_Q16[i] - psCNG.CNG_smth_Gain_Q16, SilkConstants.CNG_GAIN_SMTH_Q16);
                }
            }

            /* Add CNG when packet is lost or during DTX */
            if (psDec.lossCnt != 0)
            {
                Pointer<int> CNG_sig_Q10 = Pointer.Malloc<int>(length + SilkConstants.MAX_LPC_ORDER);

                /* Generate CNG excitation */
                gain_Q16 = Inlines.silk_SMULWW(psDec.sPLC.randScale_Q14, psDec.sPLC.prevGain_Q16[1]);
                if (gain_Q16 >= (1 << 21) || psCNG.CNG_smth_Gain_Q16 > (1 << 23))
                {
                    gain_Q16 = Inlines.silk_SMULTT(gain_Q16, gain_Q16);
                    gain_Q16 = Inlines.silk_SUB_LSHIFT32(Inlines.silk_SMULTT(psCNG.CNG_smth_Gain_Q16, psCNG.CNG_smth_Gain_Q16), gain_Q16, 5);
                    gain_Q16 = Inlines.silk_LSHIFT32(Inlines.silk_SQRT_APPROX(gain_Q16), 16);
                }
                else
                {
                    gain_Q16 = Inlines.silk_SMULWW(gain_Q16, gain_Q16);
                    gain_Q16 = Inlines.silk_SUB_LSHIFT32(Inlines.silk_SMULWW(psCNG.CNG_smth_Gain_Q16, psCNG.CNG_smth_Gain_Q16), gain_Q16, 5);
                    gain_Q16 = Inlines.silk_LSHIFT32(Inlines.silk_SQRT_APPROX(gain_Q16), 8);
                }
                silk_CNG_exc(CNG_sig_Q10.Point(SilkConstants.MAX_LPC_ORDER), psCNG.CNG_exc_buf_Q14, gain_Q16, length, ref psCNG.rand_seed);

                /* Convert CNG NLSF to filter representation */
                NLSF.silk_NLSF2A(A_Q12, psCNG.CNG_smth_NLSF_Q15, psDec.LPC_order);

                /* Generate CNG signal, by synthesis filtering */
                psCNG.CNG_synth_state.MemCopyTo(CNG_sig_Q10, SilkConstants.MAX_LPC_ORDER);

                for (i = 0; i < length; i++)
                {
                    Debug.Assert(psDec.LPC_order == 10 || psDec.LPC_order == 16);
                    /* Avoids introducing a bias because silk_SMLAWB() always rounds to -inf */
                    sum_Q6 = Inlines.silk_RSHIFT(psDec.LPC_order, 1);
                    sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 1], A_Q12[0]);
                    sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 2], A_Q12[1]);
                    sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 3], A_Q12[2]);
                    sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 4], A_Q12[3]);
                    sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 5], A_Q12[4]);
                    sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 6], A_Q12[5]);
                    sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 7], A_Q12[6]);
                    sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 8], A_Q12[7]);
                    sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 9], A_Q12[8]);
                    sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 10], A_Q12[9]);

                    if (psDec.LPC_order == 16)
                    {
                        sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 11], A_Q12[10]);
                        sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 12], A_Q12[11]);
                        sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 13], A_Q12[12]);
                        sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 14], A_Q12[13]);
                        sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 15], A_Q12[14]);
                        sum_Q6 = Inlines.silk_SMLAWB(sum_Q6, CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i - 16], A_Q12[15]);
                    }

                    /* Update states */
                    CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i] = Inlines.silk_ADD_LSHIFT(CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i], sum_Q6, 4);

                    frame[i] = Inlines.silk_ADD_SAT16(frame[i], Inlines.CHOP16(Inlines.silk_RSHIFT_ROUND(CNG_sig_Q10[SilkConstants.MAX_LPC_ORDER + i], 10)));
                }

                CNG_sig_Q10.Point(length).MemCopyTo(psCNG.CNG_synth_state, SilkConstants.MAX_LPC_ORDER);
            }
            else
            {
                psCNG.CNG_synth_state.MemSet(0, psDec.LPC_order);
            }
        }
    }
}
