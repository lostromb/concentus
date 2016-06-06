using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    public static class control_SNR
    {
        /* Control SNR of redidual quantizer */
        public static int silk_control_SNR(
            silk_encoder_state psEncC,                        /* I/O  Pointer to Silk encoder state               */
            int TargetRate_bps                  /* I    Target max bitrate (bps)                    */
        )
        {
            int k, ret = SilkError.SILK_NO_ERROR;
            int frac_Q6;
            Pointer<int> rateTable;

            /* Set bitrate/coding quality */
            TargetRate_bps = Inlines.silk_LIMIT(TargetRate_bps, SilkConstants.MIN_TARGET_RATE_BPS, SilkConstants.MAX_TARGET_RATE_BPS);
            if (TargetRate_bps != psEncC.TargetRate_bps)
            {
                psEncC.TargetRate_bps = TargetRate_bps;

                /* If new TargetRate_bps, translate to SNR_dB value */
                if (psEncC.fs_kHz == 8)
                {
                    rateTable = Tables.silk_TargetRate_table_NB.GetPointer();
                }
                else if (psEncC.fs_kHz == 12)
                {
                    rateTable = Tables.silk_TargetRate_table_MB.GetPointer();
                }
                else {
                    rateTable = Tables.silk_TargetRate_table_WB.GetPointer();
                }

                /* Reduce bitrate for 10 ms modes in these calculations */
                if (psEncC.nb_subfr == 2)
                {
                    TargetRate_bps -= TuningParameters.REDUCE_BITRATE_10_MS_BPS;
                }

                /* Find bitrate interval in table and interpolate */
                for (k = 1; k < SilkConstants.TARGET_RATE_TAB_SZ; k++)
                {
                    if (TargetRate_bps <= rateTable[k])
                    {
                        frac_Q6 = Inlines.silk_DIV32(Inlines.silk_LSHIFT(TargetRate_bps - rateTable[k - 1], 6),
                                                         rateTable[k] - rateTable[k - 1]);
                        psEncC.SNR_dB_Q7 = Inlines.silk_LSHIFT(Tables.silk_SNR_table_Q1[k - 1], 6) + Inlines.silk_MUL(frac_Q6, Tables.silk_SNR_table_Q1[k] - Tables.silk_SNR_table_Q1[k - 1]);
                        break;
                    }
                }
            }

            return ret;
        }
    }
}
