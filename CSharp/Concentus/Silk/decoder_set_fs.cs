using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    public static class decoder_set_fs
    {
        /* Set decoder sampling rate */
        public static int silk_decoder_set_fs(
            silk_decoder_state psDec,                         /* I/O  Decoder state pointer                       */
            int fs_kHz,                         /* I    Sampling frequency (kHz)                    */
            int fs_API_Hz                       /* I    API Sampling frequency (Hz)                 */
        )
        {
            int frame_length, ret = 0;

            Inlines.OpusAssert(fs_kHz == 8 || fs_kHz == 12 || fs_kHz == 16);
            Inlines.OpusAssert(psDec.nb_subfr == SilkConstants.MAX_NB_SUBFR || psDec.nb_subfr == SilkConstants.MAX_NB_SUBFR / 2);

            /* New (sub)frame length */
            psDec.subfr_length = Inlines.silk_SMULBB(SilkConstants.SUB_FRAME_LENGTH_MS, fs_kHz);
            frame_length = Inlines.silk_SMULBB(psDec.nb_subfr, psDec.subfr_length);

            /* Initialize resampler when switching internal or external sampling frequency */
            if (psDec.fs_kHz != fs_kHz || psDec.fs_API_hz != fs_API_Hz)
            {
                /* Initialize the resampler for dec_API.c preparing resampling from fs_kHz to API_fs_Hz */
                ret += Resampler.silk_resampler_init(psDec.resampler_state, Inlines.silk_SMULBB(fs_kHz, 1000), fs_API_Hz, 0);

                psDec.fs_API_hz = fs_API_Hz;
            }

            if (psDec.fs_kHz != fs_kHz || frame_length != psDec.frame_length)
            {
                if (fs_kHz == 8)
                {
                    if (psDec.nb_subfr == SilkConstants.MAX_NB_SUBFR)
                    {
                        psDec.pitch_contour_iCDF = Tables.silk_pitch_contour_NB_iCDF.GetPointer();
                    }
                    else {
                        psDec.pitch_contour_iCDF = Tables.silk_pitch_contour_10_ms_NB_iCDF.GetPointer();
                    }
                }
                else {
                    if (psDec.nb_subfr == SilkConstants.MAX_NB_SUBFR)
                    {
                        psDec.pitch_contour_iCDF = Tables.silk_pitch_contour_iCDF.GetPointer();
                    }
                    else {
                        psDec.pitch_contour_iCDF = Tables.silk_pitch_contour_10_ms_iCDF.GetPointer();
                    }
                }
                if (psDec.fs_kHz != fs_kHz)
                {
                    psDec.ltp_mem_length = Inlines.silk_SMULBB(SilkConstants.LTP_MEM_LENGTH_MS, fs_kHz);
                    if (fs_kHz == 8 || fs_kHz == 12)
                    {
                        psDec.LPC_order = SilkConstants.MIN_LPC_ORDER;
                        psDec.psNLSF_CB = Tables.silk_NLSF_CB_NB_MB;
                    }
                    else {
                        psDec.LPC_order = SilkConstants.MAX_LPC_ORDER;
                        psDec.psNLSF_CB = Tables.silk_NLSF_CB_WB;
                    }
                    if (fs_kHz == 16)
                    {
                        psDec.pitch_lag_low_bits_iCDF = Tables.silk_uniform8_iCDF.GetPointer();
                    }
                    else if (fs_kHz == 12)
                    {
                        psDec.pitch_lag_low_bits_iCDF = Tables.silk_uniform6_iCDF.GetPointer();
                    }
                    else if (fs_kHz == 8)
                    {
                        psDec.pitch_lag_low_bits_iCDF = Tables.silk_uniform4_iCDF.GetPointer();
                    }
                    else {
                        /* unsupported sampling rate */
                        Inlines.OpusAssert(false);
                    }
                    psDec.first_frame_after_reset = 1;
                    psDec.lagPrev = 100;
                    psDec.LastGainIndex = 10;
                    psDec.prevSignalType = SilkConstants.TYPE_NO_VOICE_ACTIVITY;
                    psDec.outBuf.MemSet(0, SilkConstants.MAX_FRAME_LENGTH + 2 * SilkConstants.MAX_SUB_FRAME_LENGTH);
                    psDec.sLPC_Q14_buf.MemSet(0, SilkConstants.MAX_LPC_ORDER);
                }

                psDec.fs_kHz = fs_kHz;
                psDec.frame_length = frame_length;
            }

            /* Check that settings are valid */
            Inlines.OpusAssert(psDec.frame_length > 0 && psDec.frame_length <= SilkConstants.MAX_FRAME_LENGTH);

            return ret;
        }
    }
}
