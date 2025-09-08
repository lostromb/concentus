package silk

import "math"

func silk_LTP_scale_ctrl(psEnc *SilkChannelEncoder, psEncCtrl *SilkEncoderControl, condCoding int) {
	if condCoding == CODE_INDEPENDENTLY {
		round_loss := psEnc.PacketLoss_perc + psEnc.NFramesPerPacket
		psEnc.indices.LTP_scaleIndex = int8(inlines.Silk_LIMIT(inlines.Silk_SMULWB(inlines.Silk_SMULBB(round_loss, psEncCtrl.LTPredCodGain_Q7), int(math.Trunc((0.1)*(1<<(9))+0.5))), 0, 2))

	} else {
		psEnc.indices.LTP_scaleIndex = 0
	}
	psEncCtrl.LTP_scale_Q14 = int(silk_LTPScales_table_Q14[psEnc.indices.LTP_scaleIndex])
}
