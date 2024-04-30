﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Concentus.Native
{
    internal static class NativeOpus
    {
        private const string LIBRARY_NAME = "opus";

        internal static bool Initialize(TextWriter logger)
        {
            NativeLibraryStatus status = NativePlatformUtils.PrepareNativeLibrary(LIBRARY_NAME, logger);
            if (status == NativeLibraryStatus.Available)
            {
                // Test to see if the library is loaded properly
                return opus_get_version_string() != IntPtr.Zero;
            }

            return false;
        }

        internal const int OPUS_SET_APPLICATION_REQUEST = 4000;
        internal const int OPUS_GET_APPLICATION_REQUEST = 4001;
        internal const int OPUS_SET_BITRATE_REQUEST = 4002;
        internal const int OPUS_GET_BITRATE_REQUEST = 4003;
        internal const int OPUS_SET_MAX_BANDWIDTH_REQUEST = 4004;
        internal const int OPUS_GET_MAX_BANDWIDTH_REQUEST = 4005;
        internal const int OPUS_SET_VBR_REQUEST = 4006;
        internal const int OPUS_GET_VBR_REQUEST = 4007;
        internal const int OPUS_SET_BANDWIDTH_REQUEST = 4008;
        internal const int OPUS_GET_BANDWIDTH_REQUEST = 4009;
        internal const int OPUS_SET_COMPLEXITY_REQUEST = 4010;
        internal const int OPUS_GET_COMPLEXITY_REQUEST = 4011;
        internal const int OPUS_SET_INBAND_FEC_REQUEST = 4012;
        internal const int OPUS_GET_INBAND_FEC_REQUEST = 4013;
        internal const int OPUS_SET_PACKET_LOSS_PERC_REQUEST = 4014;
        internal const int OPUS_GET_PACKET_LOSS_PERC_REQUEST = 4015;
        internal const int OPUS_SET_DTX_REQUEST = 4016;
        internal const int OPUS_GET_DTX_REQUEST = 4017;
        internal const int OPUS_SET_VBR_CONSTRAINT_REQUEST = 4020;
        internal const int OPUS_GET_VBR_CONSTRAINT_REQUEST = 4021;
        internal const int OPUS_SET_FORCE_CHANNELS_REQUEST = 4022;
        internal const int OPUS_GET_FORCE_CHANNELS_REQUEST = 4023;
        internal const int OPUS_SET_SIGNAL_REQUEST = 4024;
        internal const int OPUS_GET_SIGNAL_REQUEST = 4025;
        internal const int OPUS_GET_LOOKAHEAD_REQUEST = 4027;
        internal const int OPUS_GET_SAMPLE_RATE_REQUEST = 4029;
        internal const int OPUS_GET_FINAL_RANGE_REQUEST = 4031;
        internal const int OPUS_GET_PITCH_REQUEST = 4033;
        internal const int OPUS_SET_GAIN_REQUEST = 4034;
        internal const int OPUS_GET_GAIN_REQUEST = 4045; /* Should have been 4035 */
        internal const int OPUS_SET_LSB_DEPTH_REQUEST = 4036;
        internal const int OPUS_GET_LSB_DEPTH_REQUEST = 4037;
        internal const int OPUS_GET_LAST_PACKET_DURATION_REQUEST = 4039;
        internal const int OPUS_SET_EXPERT_FRAME_DURATION_REQUEST = 4040;
        internal const int OPUS_GET_EXPERT_FRAME_DURATION_REQUEST = 4041;
        internal const int OPUS_SET_PREDICTION_DISABLED_REQUEST = 4042;
        internal const int OPUS_GET_PREDICTION_DISABLED_REQUEST = 4043;
        internal const int OPUS_SET_FORCE_MODE_REQUEST = 11002;

        internal const int OPUS_RESET_STATE = 4028;

#if !NETSTANDARD1_1 // Use strongly-typed SafeHandles if we support them
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeOpusDecoder opus_decoder_create(int Fs, int channels, out int error);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe NativeOpusMultistreamDecoder opus_multistream_decoder_create(int Fs, int channels, int streams, int coupled_streams, byte* mapping, out int error);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_decode(NativeOpusDecoder st, byte* data, int len, short* pcm, int frame_size, int decode_fec);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_decode_float(NativeOpusDecoder st, byte* data, int len, float* pcm, int frame_size, int decode_fec);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_multistream_decode(NativeOpusMultistreamDecoder st, byte* data, int len, short* pcm, int frame_size, int decode_fec);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_multistream_decode_float(NativeOpusMultistreamDecoder st, byte* data, int len, float* pcm, int frame_size, int decode_fec);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeOpusEncoder opus_encoder_create(int Fs, int channels, int application, out int error);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe NativeOpusMultistreamEncoder opus_multistream_surround_encoder_create(int Fs, int channels, int mapping_family, out int streams, out int coupled_streams, byte* mapping, int application, out int error);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_encode(NativeOpusEncoder st, short* pcm, int frame_size, byte* data, int max_data_bytes);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_encode_float(NativeOpusEncoder st, float* pcm, int frame_size, byte* data, int max_data_bytes);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_multistream_encode(NativeOpusMultistreamEncoder st, short* pcm, int frame_size, byte* data, int max_data_bytes);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_multistream_encode_float(NativeOpusMultistreamEncoder st, float* pcm, int frame_size, byte* data, int max_data_bytes);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_decoder_ctl(NativeOpusDecoder st, int request, int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_decoder_ctl(NativeOpusDecoder st, int request, out int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encoder_ctl(NativeOpusEncoder st, int request, int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encoder_ctl(NativeOpusEncoder st, int request, out int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_multistream_decoder_ctl(NativeOpusMultistreamDecoder st, int request, int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_multistream_decoder_ctl(NativeOpusMultistreamDecoder st, int request, out int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_multistream_encoder_ctl(NativeOpusMultistreamEncoder st, int request, int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_multistream_encoder_ctl(NativeOpusMultistreamEncoder st, int request, out int value);

#else // If we are running in netstandard (presumably on some older platform like Mono / Net45, use IntPtr handles for compatibility

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opus_decoder_create(int Fs, int channels, out int error);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe IntPtr opus_multistream_decoder_create(int Fs, int channels, int streams, int coupled_streams, byte* mapping, out int error);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_decode(IntPtr st, byte* data, int len, short* pcm, int frame_size, int decode_fec);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_decode_float(IntPtr st, byte* data, int len, float* pcm, int frame_size, int decode_fec);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_multistream_decode(IntPtr st, byte* data, int len, short* pcm, int frame_size, int decode_fec);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_multistream_decode_float(IntPtr st, byte* data, int len, float* pcm, int frame_size, int decode_fec);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opus_encoder_create(int Fs, int channels, int application, out int error);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe IntPtr opus_multistream_surround_encoder_create(int Fs, int channels, int mapping_family, out int streams, out int coupled_streams, byte* mapping, int application, out int error);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_encode(IntPtr st, short* pcm, int frame_size, byte* data, int max_data_bytes);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_encode_float(IntPtr st, float* pcm, int frame_size, byte* data, int max_data_bytes);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_multistream_encode(IntPtr st, short* pcm, int frame_size, byte* data, int max_data_bytes);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int opus_multistream_encode_float(IntPtr st, float* pcm, int frame_size, byte* data, int max_data_bytes);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_decoder_ctl(IntPtr st, int request, int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_decoder_ctl(IntPtr st, int request, out int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encoder_ctl(IntPtr st, int request, int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encoder_ctl(IntPtr st, int request, out int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_multistream_decoder_ctl(IntPtr st, int request, int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_multistream_decoder_ctl(IntPtr st, int request, out int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_multistream_encoder_ctl(IntPtr st, int request, int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_multistream_encoder_ctl(IntPtr st, int request, out int value);
#endif

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opus_get_version_string();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opus_encoder_destroy(IntPtr encoder);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opus_multistream_encoder_destroy(IntPtr encoder);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opus_decoder_destroy(IntPtr decoder);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opus_multistream_decoder_destroy(IntPtr decoder);
    }
}
