using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt
{
    internal static class CeltControl
    {
        public const int CELT_SET_PREDICTION_REQUEST = 10002;
        /** Controls the use of interframe prediction.
            0=Independent frames
            1=Short term interframe prediction allowed
            2=Long term prediction allowed
         */

        public const int CELT_SET_INPUT_CLIPPING_REQUEST = 10004;

        public const int CELT_GET_AND_CLEAR_ERROR_REQUEST = 10007;

        public const int CELT_SET_CHANNELS_REQUEST = 10008;


        /* Internal */
        public const int CELT_SET_START_BAND_REQUEST = 10010;

        public const int CELT_SET_END_BAND_REQUEST = 10012;

        public const int CELT_GET_MODE_REQUEST = 10015;
        /** Get the CELTMode used by an encoder or decoder */

        public const int CELT_SET_SIGNALLING_REQUEST = 10016;

        public const int CELT_SET_TONALITY_REQUEST = 10018;
        public const int CELT_SET_TONALITY_SLOPE_REQUEST = 10020;

        public const int CELT_SET_ANALYSIS_REQUEST = 10022;

        public const int OPUS_SET_LFE_REQUEST = 10024;

        public const int OPUS_SET_ENERGY_MASK_REQUEST = 10026;
    }
}
