using HellaUnsafe.Common;
using static HellaUnsafe.Silk.A2NLSF;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Float.FloatCast;
using static HellaUnsafe.Silk.Float.StructsFLP;
using static HellaUnsafe.Silk.NLSF2A;
using static HellaUnsafe.Silk.NSQ;
using static HellaUnsafe.Silk.NSQDelDec;
using static HellaUnsafe.Silk.ProcessNLSFs;
using static HellaUnsafe.Silk.QuantLTPGains;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.Tables;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class WrappersFLP
    {
        /* Convert AR filter coefficients to NLSF parameters */
        internal static unsafe void silk_A2NLSF_FLP(
            short* NLSF_Q15,                          /* O    NLSF vector      [ LPC_order ]              */
            in float* pAR,                               /* I    LPC coefficients [ LPC_order ]              */
            in int LPC_order                           /* I    LPC order                                   */
        )
        {
            int i;
            int* a_fix_Q16 = stackalloc int[MAX_LPC_ORDER];

            for (i = 0; i < LPC_order; i++)
            {
                a_fix_Q16[i] = float2int(pAR[i] * 65536.0f);
            }

            silk_A2NLSF(NLSF_Q15, a_fix_Q16, LPC_order);
        }

        /* Convert LSF parameters to AR prediction filter coefficients */
        internal static unsafe void silk_NLSF2A_FLP(
            float* pAR,                               /* O    LPC coefficients [ LPC_order ]              */
            in short* NLSF_Q15,                          /* I    NLSF vector      [ LPC_order ]              */
            in int LPC_order                          /* I    LPC order                                   */
        )
        {
            int i;
            short* a_fix_Q12 = stackalloc short[MAX_LPC_ORDER];

            silk_NLSF2A(a_fix_Q12, NLSF_Q15, LPC_order);

            for (i = 0; i < LPC_order; i++)
            {
                pAR[i] = (float)a_fix_Q12[i] * (1.0f / 4096.0f);
            }
        }

        /******************************************/
        /* Floating-point NLSF processing wrapper */
        /******************************************/
        internal static unsafe void silk_process_NLSFs_FLP(
            silk_encoder_state* psEncC,                            /* I/O  Encoder state                               */
            Native2DArray<float> PredCoef/*[2][MAX_LPC_ORDER]*/,     /* O    Prediction coefficients                     */
            short* NLSF_Q15/*[MAX_LPC_ORDER]*/,     /* I/O  Normalized LSFs (quant out) (0 - (2^15-1))  */
            in short* prev_NLSF_Q15/*[MAX_LPC_ORDER]*/      /* I    Previous Normalized LSFs (0 - (2^15-1))     */
        )
        {
            int i, j;
            short* PredCoef_Q12_data = stackalloc short[2 * MAX_LPC_ORDER];
            Native2DArray<short> PredCoef_Q12 = new Native2DArray<short>(2, MAX_LPC_ORDER, PredCoef_Q12_data);

            silk_process_NLSFs(psEncC, PredCoef_Q12, NLSF_Q15, prev_NLSF_Q15);

            for (j = 0; j < 2; j++)
            {
                for (i = 0; i < psEncC->predictLPCOrder; i++)
                {
                    PredCoef[j][i] = (float)PredCoef_Q12[j][i] * (1.0f / 4096.0f);
                }
            }
        }

        /****************************************/
        /* Floating-point Silk NSQ wrapper      */
        /****************************************/
        internal static unsafe void silk_NSQ_wrapper_FLP(
            silk_encoder_state_FLP* psEnc,                             /* I/O  Encoder state FLP                           */
            silk_encoder_control_FLP* psEncCtrl,                         /* I/O  Encoder control FLP                         */
            SideInfoIndices* psIndices,                         /* I/O  Quantization indices                        */
            silk_nsq_state* psNSQ,                             /* I/O  Noise Shaping Quantzation state             */
            sbyte* pulses,                           /* O    Quantized pulse signal                      */
            in float* x                                 /* I    Prefiltered input signal                    */
        )
        {
            int i, j;
            short* x16 = stackalloc short[MAX_FRAME_LENGTH];
            int* Gains_Q16 = stackalloc int[MAX_NB_SUBFR];
            /*silk_DWORD_ALIGN*/ short* PredCoef_Q12_data = stackalloc short[2 * MAX_LPC_ORDER] ;
            Native2DArray<short> PredCoef_Q12 = new Native2DArray<short>(2, MAX_LPC_ORDER, PredCoef_Q12_data);
            short* LTPCoef_Q14 = stackalloc short[LTP_ORDER * MAX_NB_SUBFR];
            int LTP_scale_Q14;

            /* Noise shaping parameters */
            short* AR_Q13 = stackalloc short[MAX_NB_SUBFR * MAX_SHAPE_LPC_ORDER];
            int* LF_shp_Q14 = stackalloc int[MAX_NB_SUBFR];         /* Packs two int16 coefficients per int32 value             */
            int Lambda_Q10;
            int* Tilt_Q14 = stackalloc int[MAX_NB_SUBFR];
            int* HarmShapeGain_Q14 = stackalloc int[MAX_NB_SUBFR];

            /* Convert control struct to fix control struct */
            /* Noise shape parameters */
            for (i = 0; i < psEnc->sCmn.nb_subfr; i++)
            {
                for (j = 0; j < psEnc->sCmn.shapingLPCOrder; j++)
                {
                    AR_Q13[i * MAX_SHAPE_LPC_ORDER + j] = (short)float2int(psEncCtrl->AR[i * MAX_SHAPE_LPC_ORDER + j] * 8192.0f);
                }
            }

            for (i = 0; i < psEnc->sCmn.nb_subfr; i++)
            {
                LF_shp_Q14[i] = silk_LSHIFT32(float2int(psEncCtrl->LF_AR_shp[i] * 16384.0f), 16) |
                                      (ushort)float2int(psEncCtrl->LF_MA_shp[i] * 16384.0f);
                Tilt_Q14[i] = (int)float2int(psEncCtrl->Tilt[i] * 16384.0f);
                HarmShapeGain_Q14[i] = (int)float2int(psEncCtrl->HarmShapeGain[i] * 16384.0f);
            }
            Lambda_Q10 = (int)float2int(psEncCtrl->Lambda * 1024.0f);

            /* prediction and coding parameters */
            for (i = 0; i < psEnc->sCmn.nb_subfr * LTP_ORDER; i++)
            {
                LTPCoef_Q14[i] = (short)float2int(psEncCtrl->LTPCoef[i] * 16384.0f);
            }

            for (j = 0; j < 2; j++)
            {
                for (i = 0; i < psEnc->sCmn.predictLPCOrder; i++)
                {
                    PredCoef_Q12[j][i] = (short)float2int(psEncCtrl->PredCoef[j][i] * 4096.0f);
                }
            }

            for (i = 0; i < psEnc->sCmn.nb_subfr; i++)
            {
                Gains_Q16[i] = float2int(psEncCtrl->Gains[i] * 65536.0f);
                silk_assert(Gains_Q16[i] > 0);
            }

            if (psIndices->signalType == TYPE_VOICED)
            {
                LTP_scale_Q14 = silk_LTPScales_table_Q14[psIndices->LTP_scaleIndex];
            }
            else
            {
                LTP_scale_Q14 = 0;
            }

            /* Convert input to fix */
            for (i = 0; i < psEnc->sCmn.frame_length; i++)
            {
                x16[i] = (short)float2int(x[i]);
            }

            /* Call NSQ */
            if (psEnc->sCmn.nStatesDelayedDecision > 1 || psEnc->sCmn.warping_Q16 > 0)
            {
                silk_NSQ_del_dec(&psEnc->sCmn, psNSQ, psIndices, x16, pulses, PredCoef_Q12[0], LTPCoef_Q14,
                    AR_Q13, HarmShapeGain_Q14, Tilt_Q14, LF_shp_Q14, Gains_Q16, psEncCtrl->pitchL, Lambda_Q10, LTP_scale_Q14);
            }
            else
            {
                silk_NSQ(&psEnc->sCmn, psNSQ, psIndices, x16, pulses, PredCoef_Q12[0], LTPCoef_Q14,
                    AR_Q13, HarmShapeGain_Q14, Tilt_Q14, LF_shp_Q14, Gains_Q16, psEncCtrl->pitchL, Lambda_Q10, LTP_scale_Q14);
            }
        }

        /***********************************************/
        /* Floating-point Silk LTP quantiation wrapper */
        /***********************************************/
        internal static unsafe void silk_quant_LTP_gains_FLP(
            float* B/*[MAX_NB_SUBFR * LTP_ORDER]*/,      /* O    Quantized LTP gains                            */
            sbyte* cbk_index/*[MAX_NB_SUBFR]*/,          /* O    Codebook index                              */
            sbyte* periodicity_index,                 /* O    Periodicity index                           */
            int* sum_log_gain_Q7,                   /* I/O  Cumulative max prediction gain  */
            float* pred_gain_dB,                        /* O    LTP prediction gain                            */
            in float* XX/*[MAX_NB_SUBFR * LTP_ORDER * LTP_ORDER]*/, /* I    Correlation matrix                    */
            in float* xX/*[MAX_NB_SUBFR * LTP_ORDER]*/,        /* I    Correlation vector                            */
            in int subfr_len,                            /* I    Number of samples per subframe                */
            in int nb_subfr                           /* I    Number of subframes                            */
        )
        {
            int i, pred_gain_dB_Q7;
            short* B_Q14 = stackalloc short[MAX_NB_SUBFR * LTP_ORDER];
            int* XX_Q17 = stackalloc int[MAX_NB_SUBFR * LTP_ORDER * LTP_ORDER];
            int* xX_Q17 = stackalloc int[MAX_NB_SUBFR * LTP_ORDER];

            i = 0;
            do
            {
                XX_Q17[i] = (int)float2int(XX[i] * 131072.0f);
            } while (++i < nb_subfr * LTP_ORDER * LTP_ORDER);
            i = 0;
            do
            {
                xX_Q17[i] = (int)float2int(xX[i] * 131072.0f);
            } while (++i < nb_subfr * LTP_ORDER);

            silk_quant_LTP_gains(B_Q14, cbk_index, periodicity_index, sum_log_gain_Q7, &pred_gain_dB_Q7, XX_Q17, xX_Q17, subfr_len, nb_subfr);

            for (i = 0; i < nb_subfr * LTP_ORDER; i++)
            {
                B[i] = (float)B_Q14[i] * (1.0f / 16384.0f);
            }

            *pred_gain_dB = (float)pred_gain_dB_Q7 * (1.0f / 128.0f);
        }
    }
}
