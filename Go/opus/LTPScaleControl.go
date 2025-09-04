package opus

import "math"

func silk_LTP_scale_ctrl(psEnc *SilkChannelEncoder, psEncCtrl *SilkEncoderControl, condCoding int) {
	if condCoding == CODE_INDEPENDENTLY {
		round_loss := psEnc.PacketLoss_perc + psEnc.nFramesPerPacket
		psEnc.indices.LTP_scaleIndex = int8(silk_LIMIT(silk_SMULWB(silk_SMULBB(round_loss, psEncCtrl.LTPredCodGain_Q7), int(math.Trunc((0.1)*(1<<(9))+0.5))), 0, 2))

	} else {
		psEnc.indices.LTP_scaleIndex = 0
	}
	psEncCtrl.LTP_scale_Q14 = int(silk_LTPScales_table_Q14[psEnc.indices.LTP_scaleIndex])
}
