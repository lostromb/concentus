
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Opus.OpusPrivate;
using static HellaUnsafe.Silk.Control;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.DecAPI;

namespace HellaUnsafe.Opus
{
    internal static unsafe class Opus_Decoder
    {
        internal unsafe struct OpusDecoder {
           internal int          celt_dec_offset;
           internal int          silk_dec_offset;
           internal int          channels;
           internal int          Fs;          /** Sampling rate (at the API level) */
           internal silk_DecControlStruct DecControl;
           internal int          decode_gain;
           internal int          complexity;

           /* Everything beyond this point gets cleared on a reset */
           //#define OPUS_DECODER_RESET_START stream_channels
           internal int          stream_channels;

           internal int          bandwidth;
           internal int          mode;
           internal int          prev_mode;
           internal int          frame_size;
           internal int          prev_redundancy;
           internal int          last_packet_duration;
           internal fixed float  softclip_mem[2];
           internal uint         rangeFinal;
        }

        internal static unsafe void validate_opus_decoder(OpusDecoder* st)
        {
            celt_assert(st->channels == 1 || st->channels == 2);
            celt_assert(st->Fs == 48000 || st->Fs == 24000 || st->Fs == 16000 || st->Fs == 12000 || st->Fs == 8000);
            celt_assert(st->DecControl.API_sampleRate == st->Fs);
            celt_assert(st->DecControl.internalSampleRate == 0 || st->DecControl.internalSampleRate == 16000 || st->DecControl.internalSampleRate == 12000 || st->DecControl.internalSampleRate == 8000);
            celt_assert(st->DecControl.nChannelsAPI == st->channels);
            celt_assert(st->DecControl.nChannelsInternal == 0 || st->DecControl.nChannelsInternal == 1 || st->DecControl.nChannelsInternal == 2);
            celt_assert(st->DecControl.payloadSize_ms == 0 || st->DecControl.payloadSize_ms == 10 || st->DecControl.payloadSize_ms == 20 || st->DecControl.payloadSize_ms == 40 || st->DecControl.payloadSize_ms == 60);
            celt_assert(st->stream_channels == 1 || st->stream_channels == 2);
        }

        internal static unsafe int opus_decoder_get_size(int channels)
        {
            int silkDecSizeBytes, celtDecSizeBytes;
            int ret;
            if (channels < 1 || channels > 2)
                return 0;
            ret = silk_Get_Decoder_Size(&silkDecSizeBytes);
            if (ret != 0)
                return 0;
            silkDecSizeBytes = align(silkDecSizeBytes);
            celtDecSizeBytes = celt_decoder_get_size(channels);
            return align(sizeof(OpusDecoder)) + silkDecSizeBytes + celtDecSizeBytes;
        }
    }
}
