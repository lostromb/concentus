using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HellaUnsafe.Silk
{
    internal static unsafe class PitchEstDefines
    {
        /********************************************************/
        /* Definitions for pitch estimator                      */
        /********************************************************/

        internal const int PE_MAX_FS_KHZ = 16; /* Maximum sampling frequency used */

        internal const int PE_MAX_NB_SUBFR = 4;
        internal const int PE_SUBFR_LENGTH_MS = 5;   /* 5 ms */

        internal const int PE_LTP_MEM_LENGTH_MS = (4 * PE_SUBFR_LENGTH_MS);

        internal const int PE_MAX_FRAME_LENGTH_MS = (PE_LTP_MEM_LENGTH_MS + PE_MAX_NB_SUBFR * PE_SUBFR_LENGTH_MS);
        internal const int PE_MAX_FRAME_LENGTH = (PE_MAX_FRAME_LENGTH_MS * PE_MAX_FS_KHZ);
        internal const int PE_MAX_FRAME_LENGTH_ST_1 = (PE_MAX_FRAME_LENGTH >> 2);
        internal const int PE_MAX_FRAME_LENGTH_ST_2 = (PE_MAX_FRAME_LENGTH >> 1);

        internal const int PE_MAX_LAG_MS = 18;           /* 18 ms -> 56 Hz */
        internal const int PE_MIN_LAG_MS = 2;            /* 2 ms -> 500 Hz */
        internal const int PE_MAX_LAG = (PE_MAX_LAG_MS * PE_MAX_FS_KHZ);
        internal const int PE_MIN_LAG = (PE_MIN_LAG_MS * PE_MAX_FS_KHZ);

        internal const int PE_D_SRCH_LENGTH = 24;

        internal const int PE_NB_STAGE3_LAGS = 5;

        internal const int PE_NB_CBKS_STAGE2 = 3;
        internal const int PE_NB_CBKS_STAGE2_EXT = 11;
        internal const int PE_NB_CBKS_STAGE3_MAX = 34;
        internal const int PE_NB_CBKS_STAGE3_MID = 24;
        internal const int PE_NB_CBKS_STAGE3_MIN = 16;

        internal const int PE_NB_CBKS_STAGE3_10MS = 12;
        internal const int PE_NB_CBKS_STAGE2_10MS = 3;

        internal const float PE_SHORTLAG_BIAS = 0.2f;    /* for logarithmic weighting    */
        internal const float PE_PREVLAG_BIAS = 0.2f;    /* for logarithmic weighting    */
        internal const float PE_FLATCONTOUR_BIAS = 0.05f;

        internal const int SILK_PE_MIN_COMPLEX = 0;
        internal const int SILK_PE_MID_COMPLEX = 1;
        internal const int SILK_PE_MAX_COMPLEX = 2;
    }
}
