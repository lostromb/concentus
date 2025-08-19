using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Celt
{
    // celt.h
    // Could probably merge with celt.c file for less (?) confusion
    internal static unsafe class CeltH
    {
        internal const int LEAK_BANDS = 19;
        internal const int COMBFILTER_MAXPERIOD = 1024;
        internal const int COMBFILTER_MINPERIOD = 15;

        internal unsafe struct AnalysisInfo
        {
            internal int valid;
            internal float tonality;
            internal float tonality_slope;
            internal float noisiness;
            internal float activity;
            internal float music_prob;
            internal float music_prob_min;
            internal float music_prob_max;
            internal int bandwidth;
            internal float activity_probability;
            internal float max_pitch_ratio;
            /* Store as Q6 char to save space. */
            internal fixed byte leak_boost[LEAK_BANDS];
        }

        internal unsafe struct SILKInfo
        {
            internal int signalType;
            internal int offset;
        }

        internal const int CELT_SET_PREDICTION_REQUEST = 10002;
        /** Controls the use of interframe prediction.
            0=Independent frames
            1=Short term interframe prediction allowed
            2=Long term prediction allowed
         */
        //internal const int CELT_SET_PREDICTION(x) CELT_SET_PREDICTION_REQUEST, __opus_check_int(x)

        internal const int CELT_SET_INPUT_CLIPPING_REQUEST = 10004;
        //internal const int CELT_SET_INPUT_CLIPPING(x) CELT_SET_INPUT_CLIPPING_REQUEST, __opus_check_int(x)

        internal const int CELT_GET_AND_CLEAR_ERROR_REQUEST = 10007;
        //internal const int CELT_GET_AND_CLEAR_ERROR(x) CELT_GET_AND_CLEAR_ERROR_REQUEST, __opus_check_int_ptr(x)

        internal const int CELT_SET_CHANNELS_REQUEST = 10008;
        //internal const int CELT_SET_CHANNELS(x) CELT_SET_CHANNELS_REQUEST, __opus_check_int(x)


        /* Internal */
        internal const int CELT_SET_START_BAND_REQUEST = 10010;
        //internal const int CELT_SET_START_BAND(x) CELT_SET_START_BAND_REQUEST, __opus_check_int(x)

        internal const int CELT_SET_END_BAND_REQUEST = 10012;
        //internal const int CELT_SET_END_BAND(x) CELT_SET_END_BAND_REQUEST, __opus_check_int(x)

        internal const int CELT_GET_MODE_REQUEST = 10015;
        /** Get the CELTMode used by an encoder or decoder */
        //internal const int CELT_GET_MODE(x) CELT_GET_MODE_REQUEST, __celt_check_mode_ptr_ptr(x)

        internal const int CELT_SET_SIGNALLING_REQUEST = 10016;
        //internal const int CELT_SET_SIGNALLING(x) CELT_SET_SIGNALLING_REQUEST, __opus_check_int(x)

        internal const int CELT_SET_TONALITY_REQUEST = 10018;
        //internal const int CELT_SET_TONALITY(x) CELT_SET_TONALITY_REQUEST, __opus_check_int(x)
        internal const int CELT_SET_TONALITY_SLOPE_REQUEST = 10020;
        //internal const int CELT_SET_TONALITY_SLOPE(x) CELT_SET_TONALITY_SLOPE_REQUEST, __opus_check_int(x)

        internal const int CELT_SET_ANALYSIS_REQUEST = 10022;
        //internal const int CELT_SET_ANALYSIS(x) CELT_SET_ANALYSIS_REQUEST, __celt_check_analysis_ptr(x)

        internal const int OPUS_SET_LFE_REQUEST = 10024;
        //internal const int OPUS_SET_LFE(x) OPUS_SET_LFE_REQUEST, __opus_check_int(x)

        internal const int OPUS_SET_ENERGY_MASK_REQUEST = 10026;
        //internal const int OPUS_SET_ENERGY_MASK(x) OPUS_SET_ENERGY_MASK_REQUEST, __opus_check_val16_ptr(x)

        internal const int CELT_SET_SILK_INFO_REQUEST = 10028;
        //internal const int CELT_SET_SILK_INFO(x) CELT_SET_SILK_INFO_REQUEST, __celt_check_silkinfo_ptr(x)

        internal static readonly byte* trim_icdf = AllocateGlobalArray<byte>(11, new byte[] {
             126, 124, 119, 109, 87, 41, 19, 9, 4, 2, 0 });

        /* Probs: NONE: 21.875%, LIGHT: 6.25%, NORMAL: 65.625%, AGGRESSIVE: 6.25% */
        internal static readonly byte* spread_icdf = AllocateGlobalArray<byte>(4, new byte[] {
            25, 23, 2, 0 });

        internal static readonly byte* tapset_icdf = AllocateGlobalArray<byte>(3, new byte[] {
            2, 1, 0 });
    }
}
