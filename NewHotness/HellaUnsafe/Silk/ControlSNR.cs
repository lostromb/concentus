using System;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Inlines;
using static HellaUnsafe.Silk.Errors;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.Tables;

namespace HellaUnsafe.Silk
{
    internal static unsafe class ControlSNR
    {
        /* These tables hold SNR values divided by 21 (so they fit in 8 bits)
           for different target bitrates spaced at 400 bps interval. The first
           10 values are omitted (0-4 kb/s) because they're all zeros.
           These tables were obtained by running different SNRs through the
           encoder and measuring the active bitrate. */
        internal static readonly byte[] silk_TargetRate_NB_21 = new byte[] {
                                                      0, 15, 39, 52, 61, 68,
             74, 79, 84, 88, 92, 95, 99,102,105,108,111,114,117,119,122,124,
            126,129,131,133,135,137,139,142,143,145,147,149,151,153,155,157,
            158,160,162,163,165,167,168,170,171,173,174,176,177,179,180,182,
            183,185,186,187,189,190,192,193,194,196,197,199,200,201,203,204,
            205,207,208,209,211,212,213,215,216,217,219,220,221,223,224,225,
            227,228,230,231,232,234,235,236,238,239,241,242,243,245,246,248,
            249,250,252,253,255
        };

        internal static readonly byte[] silk_TargetRate_MB_21 = new byte[] {
                                                      0,  0, 28, 43, 52, 59,
             65, 70, 74, 78, 81, 85, 87, 90, 93, 95, 98,100,102,105,107,109,
            111,113,115,116,118,120,122,123,125,127,128,130,131,133,134,136,
            137,138,140,141,143,144,145,147,148,149,151,152,153,154,156,157,
            158,159,160,162,163,164,165,166,167,168,169,171,172,173,174,175,
            176,177,178,179,180,181,182,183,184,185,186,187,188,188,189,190,
            191,192,193,194,195,196,197,198,199,200,201,202,203,203,204,205,
            206,207,208,209,210,211,212,213,214,214,215,216,217,218,219,220,
            221,222,223,224,224,225,226,227,228,229,230,231,232,233,234,235,
            236,236,237,238,239,240,241,242,243,244,245,246,247,248,249,250,
            251,252,253,254,255
        };

        internal static readonly byte[] silk_TargetRate_WB_21 = new byte[] {
                                                      0,  0,  0,  8, 29, 41,
             49, 56, 62, 66, 70, 74, 77, 80, 83, 86, 88, 91, 93, 95, 97, 99,
            101,103,105,107,108,110,112,113,115,116,118,119,121,122,123,125,
            126,127,129,130,131,132,134,135,136,137,138,140,141,142,143,144,
            145,146,147,148,149,150,151,152,153,154,156,157,158,159,159,160,
            161,162,163,164,165,166,167,168,169,170,171,171,172,173,174,175,
            176,177,177,178,179,180,181,181,182,183,184,185,185,186,187,188,
            189,189,190,191,192,192,193,194,195,195,196,197,198,198,199,200,
            200,201,202,203,203,204,205,206,206,207,208,209,209,210,211,211,
            212,213,214,214,215,216,216,217,218,219,219,220,221,221,222,223,
            224,224,225,226,226,227,228,229,229,230,231,232,232,233,234,234,
            235,236,237,237,238,239,240,240,241,242,243,243,244,245,246,246,
            247,248,249,249,250,251,252,253,255
        };

        /* Control SNR of redidual quantizer */
        internal static unsafe int silk_control_SNR(
            silk_encoder_state          *psEncC,                        /* I/O  Pointer to Silk encoder state               */
            int                  TargetRate_bps                  /* I    Target max bitrate (bps)                    */
        )
        {
            int id;
            int bound;
            byte[] snr_table;

            psEncC->TargetRate_bps = TargetRate_bps;
            if( psEncC->nb_subfr == 2 ) {
                TargetRate_bps -= 2000 + psEncC->fs_kHz/16;
            }
            if( psEncC->fs_kHz == 8 ) {
                bound = silk_TargetRate_NB_21.Length;
                snr_table = silk_TargetRate_NB_21;
            } else if( psEncC->fs_kHz == 12 ) {
                bound = silk_TargetRate_MB_21.Length;
                snr_table = silk_TargetRate_MB_21;
            } else {
                bound = silk_TargetRate_WB_21.Length;
                snr_table = silk_TargetRate_WB_21;
            }
            id = (TargetRate_bps+200)/400;
            id = silk_min(id - 10, bound-1);
            if( id <= 0 ) {
                psEncC->SNR_dB_Q7 = 0;
            } else {
                psEncC->SNR_dB_Q7 = snr_table[id]*21;
            }
            return SILK_NO_ERROR;
        }
    }
}
