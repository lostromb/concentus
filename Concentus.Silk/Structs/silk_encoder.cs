using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Encoder Super Struct
    /// </summary>
    public class silk_encoder
    {
        public readonly Pointer<silk_encoder_state_fix> state_Fxx = Pointer.Malloc<silk_encoder_state_fix>(SilkConstants.ENCODER_NUM_CHANNELS);
        public readonly stereo_enc_state sStereo = new stereo_enc_state();
        public int nBitsUsedLBRR = 0;
        public int nBitsExceeded = 0;
        public int nChannelsAPI = 0;
        public int nChannelsInternal = 0;
        public int nPrevChannelsInternal = 0;
        public int timeSinceSwitchAllowed_ms = 0;
        public int allowBandwidthSwitch = 0;
        public int prev_decode_only_middle = 0;

        public silk_encoder()
        {
            for (int c = 0; c < SilkConstants.ENCODER_NUM_CHANNELS; c++)
            {
                state_Fxx[c] = new silk_encoder_state_fix();
            }
        }

        public void Reset()
        {
            for (int c = 0; c < SilkConstants.ENCODER_NUM_CHANNELS; c++)
            {
                state_Fxx[c].Reset();
            }

            sStereo.Reset();
            nBitsUsedLBRR = 0;
            nBitsExceeded = 0;
            nChannelsAPI = 0;
            nChannelsInternal = 0;
            nPrevChannelsInternal = 0;
            timeSinceSwitchAllowed_ms = 0;
            allowBandwidthSwitch = 0;
            prev_decode_only_middle = 0;
        }

        /// <summary>
        /// Initialize Silk Encoder state
        /// </summary>
        /// <param name="psEnc">I/O  Pointer to Silk FIX encoder state</param>
        /// <param name="arch">I    Run-time architecture</param>
        /// <returns></returns>
        public static int silk_init_encoder(silk_encoder_state_fix psEnc, int arch)
        {
            int ret = 0;

            // Clear the entire encoder state
            psEnc.Reset();

            psEnc.sCmn.arch = arch;

            psEnc.sCmn.variable_HP_smth1_Q15 = Inlines.silk_LSHIFT(Inlines.silk_lin2log(Inlines.SILK_FIX_CONST(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ, 16)) - (16 << 7), 8);
            psEnc.sCmn.variable_HP_smth2_Q15 = psEnc.sCmn.variable_HP_smth1_Q15;

            // Used to deactivate LSF interpolation, pitch prediction
            psEnc.sCmn.first_frame_after_reset = 1;

            // Initialize Silk VAD
            ret += VAD.silk_VAD_Init(psEnc.sCmn.sVAD);

            return ret;
        }
    }
}