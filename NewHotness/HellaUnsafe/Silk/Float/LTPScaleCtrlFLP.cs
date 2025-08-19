using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Lin2Log;
using static HellaUnsafe.Silk.Log2Lin;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Float.StructsFLP;
using static HellaUnsafe.Silk.Tables;

namespace HellaUnsafe.Silk.Float
{
    internal static unsafe class LTPScaleCtrlFLP
    {
        internal static unsafe void silk_LTP_scale_ctrl_FLP(
            silk_encoder_state_FLP          *psEnc,                             /* I/O  Encoder state FLP                           */
            silk_encoder_control_FLP        *psEncCtrl,                         /* I/O  Encoder control FLP                         */
            int                        condCoding                          /* I    The type of conditional coding to use       */
        )
        {
            int   round_loss;

            if( condCoding == CODE_INDEPENDENTLY ) {
                /* Only scale if first frame in packet */
                round_loss = psEnc->sCmn.PacketLoss_perc * psEnc->sCmn.nFramesPerPacket;
                if ( psEnc->sCmn.LBRR_flag != 0) {
                    /* LBRR reduces the effective loss. In practice, it does not square the loss because
                       losses aren't independent, but that still seems to work best. We also never go below 2%. */
                    round_loss = 2 + silk_SMULBB( round_loss, round_loss) / 100;
                }
                psEnc->sCmn.indices.LTP_scaleIndex = (sbyte)BOOL2INT(silk_SMULBB((int)psEncCtrl->LTPredCodGain, round_loss ) > silk_log2lin( 2900 - psEnc->sCmn.SNR_dB_Q7 ));
                psEnc->sCmn.indices.LTP_scaleIndex = (sbyte)(psEnc->sCmn.indices.LTP_scaleIndex + BOOL2INT(silk_SMULBB((int)psEncCtrl->LTPredCodGain, round_loss ) > silk_log2lin( 3900 - psEnc->sCmn.SNR_dB_Q7 )));
            } else {
                /* Default is minimum scaling */
                psEnc->sCmn.indices.LTP_scaleIndex = 0;
            }

            psEncCtrl->LTP_scale = (float)silk_LTPScales_table_Q14[ psEnc->sCmn.indices.LTP_scaleIndex ] / 16384.0f;
        }
    }
}
