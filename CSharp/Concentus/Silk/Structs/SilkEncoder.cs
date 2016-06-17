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
    public class SilkEncoder
    {
        public readonly Pointer<SilkChannelEncoder> state_Fxx = Pointer.Malloc<SilkChannelEncoder>(SilkConstants.ENCODER_NUM_CHANNELS);
        public readonly StereoEncodeState sStereo = new StereoEncodeState();
        public int nBitsUsedLBRR = 0;
        public int nBitsExceeded = 0;
        public int nChannelsAPI = 0;
        public int nChannelsInternal = 0;
        public int nPrevChannelsInternal = 0;
        public int timeSinceSwitchAllowed_ms = 0;
        public int allowBandwidthSwitch = 0;
        public int prev_decode_only_middle = 0;

        public SilkEncoder()
        {
            for (int c = 0; c < SilkConstants.ENCODER_NUM_CHANNELS; c++)
            {
                state_Fxx[c] = new SilkChannelEncoder();
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
        internal static int silk_init_encoder(SilkChannelEncoder psEnc)
        {
            int ret = 0;

            // Clear the entire encoder state
            psEnc.Reset();

            psEnc.variable_HP_smth1_Q15 = Inlines.silk_LSHIFT(Inlines.silk_lin2log(Inlines.SILK_CONST(TuningParameters.VARIABLE_HP_MIN_CUTOFF_HZ, 16)) - (16 << 7), 8);
            psEnc.variable_HP_smth2_Q15 = psEnc.variable_HP_smth1_Q15;

            // Used to deactivate LSF interpolation, pitch prediction
            psEnc.first_frame_after_reset = 1;

            // Initialize Silk VAD
            ret += VoiceActivityDetection.silk_VAD_Init(psEnc.sVAD);

            return ret;
        }
    }
}