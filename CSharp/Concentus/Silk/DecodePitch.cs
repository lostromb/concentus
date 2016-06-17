using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System;
using System.Diagnostics;

namespace Concentus.Silk
{
    internal static class DecodePitch
    {
        internal static void silk_decode_pitch(
                short lagIndex,           /* I                                                                */
                sbyte contourIndex,       /* O                                                                */
                Pointer<int> pitch_lags,       /* O    4 pitch values                                              */
                int Fs_kHz,             /* I    sampling frequency (kHz)                                    */
                int nb_subfr            /* I    number of sub frames                                        */
            )
        {
            int lag, k, min_lag, max_lag, cbk_size;
            Pointer<sbyte> Lag_CB_ptr;

            if (Fs_kHz == 8)
            {
                if (nb_subfr == SilkConstants.PE_MAX_NB_SUBFR)
                {
                    Lag_CB_ptr = Tables.silk_CB_lags_stage2.GetPointer(0);
                    cbk_size = SilkConstants.PE_NB_CBKS_STAGE2_EXT;
                }
                else
                {
                    Inlines.OpusAssert(nb_subfr == SilkConstants.PE_MAX_NB_SUBFR >> 1);
                    Lag_CB_ptr = Tables.silk_CB_lags_stage2_10_ms.GetPointer(0);
                    cbk_size = SilkConstants.PE_NB_CBKS_STAGE2_10MS;
                }
            }
            else
            {
                if (nb_subfr == SilkConstants.PE_MAX_NB_SUBFR)
                {
                    Lag_CB_ptr = Tables.silk_CB_lags_stage3.GetPointer(0);
                    cbk_size = SilkConstants.PE_NB_CBKS_STAGE3_MAX;
                }
                else
                {
                    Inlines.OpusAssert(nb_subfr == SilkConstants.PE_MAX_NB_SUBFR >> 1);
                    Lag_CB_ptr = Tables.silk_CB_lags_stage3_10_ms.GetPointer(0);
                    cbk_size = SilkConstants.PE_NB_CBKS_STAGE3_10MS;
                }
            }

            min_lag = Inlines.silk_SMULBB(SilkConstants.PE_MIN_LAG_MS, Fs_kHz);
            max_lag = Inlines.silk_SMULBB(SilkConstants.PE_MAX_LAG_MS, Fs_kHz);
            lag = min_lag + lagIndex;

            for (k = 0; k < nb_subfr; k++)
            {
                pitch_lags[k] = lag + Inlines.matrix_ptr(Lag_CB_ptr, k, contourIndex, cbk_size);
                pitch_lags[k] = Inlines.silk_LIMIT(pitch_lags[k], min_lag, max_lag);
            }
        }
    }
}
