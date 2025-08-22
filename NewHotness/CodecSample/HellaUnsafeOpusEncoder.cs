
using static HellaUnsafe.Opus.Opus_Encoder;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Opus.OpusPrivate;

namespace HellaUnsafe
{
    internal unsafe class HellaUnsafeOpusEncoder : Concentus.IOpusEncoder
    {
        private readonly OpusEncoder* _handle = null;
        private int _disposed = 0;

        internal HellaUnsafeOpusEncoder(OpusEncoder* handle)
        {
            _handle = handle;
        }

        ~HellaUnsafeOpusEncoder()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            if (_handle != null)
            {
                opus_encoder_destroy(_handle);
            }
        }

        public static HellaUnsafeOpusEncoder Create(int sampleRate, int channelCount, Concentus.Enums.OpusApplication application)
        {
            int error = 0;
            OpusEncoder* handle = opus_encoder_create(sampleRate, channelCount, (int)application, &error);
            if (error != Concentus.Enums.OpusError.OPUS_OK)
            {
                if (handle != null)
                {
                    opus_encoder_destroy(handle);
                }

                throw new Exception($"Failed to create opus encoder: error {error}");
            }

            HellaUnsafeOpusEncoder returnVal = new HellaUnsafeOpusEncoder(handle);
            returnVal._sampleRate = sampleRate;
            returnVal._numChannels = channelCount;
            return returnVal;
        }
        private OpusEncoder* NativeHandle => _handle;

        private int _sampleRate;
        private int _numChannels;

        /// <inheritdoc/>
        public unsafe int Encode(ReadOnlySpan<short> in_pcm, int frame_size, Span<byte> out_data, int max_data_bytes)
        {
            fixed (short* inPtr = in_pcm)
            fixed (byte* outPtr = out_data)
            {
                return opus_encode(NativeHandle, inPtr, frame_size, outPtr, max_data_bytes);
            }
        }

        /// <inheritdoc/>
        public unsafe int Encode(ReadOnlySpan<float> in_pcm, int frame_size, Span<byte> out_data, int max_data_bytes)
        {
            fixed (float* inPtr = in_pcm)
            fixed (byte* outPtr = out_data)
            {
                return opus_encode_float(NativeHandle, inPtr, frame_size, outPtr, max_data_bytes);
            }
        }

        /// <inheritdoc/>
        public void ResetState()
        {
            opus_encoder_ctl(NativeHandle, OPUS_RESET_STATE, 0);
        }

        /// <inheritdoc/>
        public string GetVersionString()
        {
            return "OPUS-HELLA-UNSAFE";
        }

        /// <inheritdoc/>
        public int SampleRate => _sampleRate;

        /// <inheritdoc/>
        public int NumChannels => _numChannels;

        /// <inheritdoc/>
        public int Complexity
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_COMPLEXITY_REQUEST, out returnVal);
                return returnVal;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_COMPLEXITY_REQUEST, value);
            }
        }

        /// <inheritdoc/>
        public bool UseDTX
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_DTX_REQUEST, out returnVal);
                return returnVal != 0;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_DTX_REQUEST, value ? 1 : 0);
            }
        }

        /// <inheritdoc/>
        public int Bitrate
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_BITRATE_REQUEST, out returnVal);
                return returnVal;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_BITRATE_REQUEST, value);
            }
        }

        /// <inheritdoc/>
        public Concentus.Enums.OpusMode ForceMode
        {
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_FORCE_MODE_REQUEST, (int)value);
            }
        }

        /// <inheritdoc/>
        public bool UseVBR
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_VBR_REQUEST, out returnVal);
                return returnVal != 0;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_VBR_REQUEST, value ? 1 : 0);
            }
        }

        /// <inheritdoc/>
        public Concentus.Enums.OpusApplication Application
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_APPLICATION_REQUEST, out returnVal);
                return (Concentus.Enums.OpusApplication)returnVal;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_APPLICATION_REQUEST, (int)value);
            }
        }

        /// <inheritdoc/>
        public int ForceChannels
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_FORCE_CHANNELS_REQUEST, out returnVal);
                return returnVal;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_FORCE_CHANNELS_REQUEST, (int)value);
            }
        }

        /// <inheritdoc/>
        public Concentus.Enums.OpusBandwidth MaxBandwidth
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_MAX_BANDWIDTH_REQUEST, out returnVal);
                return (Concentus.Enums.OpusBandwidth)returnVal;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_MAX_BANDWIDTH_REQUEST, (int)value);
            }
        }

        /// <inheritdoc/>
        public Concentus.Enums.OpusBandwidth Bandwidth
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_BANDWIDTH_REQUEST, out returnVal);
                return (Concentus.Enums.OpusBandwidth)returnVal;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_BANDWIDTH_REQUEST, (int)value);
            }
        }

        /// <inheritdoc/>
        public bool UseInbandFEC
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_INBAND_FEC_REQUEST, out returnVal);
                return returnVal != 0;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_INBAND_FEC_REQUEST, value ? 1 : 0);
            }
        }

        /// <inheritdoc/>
        public int PacketLossPercent
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_PACKET_LOSS_PERC_REQUEST, out returnVal);
                return returnVal;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_PACKET_LOSS_PERC_REQUEST, value);
            }
        }

        /// <inheritdoc/>
        public bool UseConstrainedVBR
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_VBR_CONSTRAINT_REQUEST, out returnVal);
                return returnVal != 0;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_VBR_CONSTRAINT_REQUEST, value ? 1 : 0);
            }
        }

        /// <inheritdoc/>
        public Concentus.Enums.OpusSignal SignalType
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_SIGNAL_REQUEST, out returnVal);
                return (Concentus.Enums.OpusSignal)returnVal;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_SIGNAL_REQUEST, (int)value);
            }
        }

        /// <inheritdoc/>
        public int Lookahead
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_LOOKAHEAD_REQUEST, out returnVal);
                return returnVal;
            }
        }

        /// <inheritdoc/>
        public uint FinalRange
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_FINAL_RANGE_REQUEST, out returnVal);
                return (uint)returnVal;
            }
        }

        /// <inheritdoc/>
        public int LSBDepth
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_LSB_DEPTH_REQUEST, out returnVal);
                return returnVal;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_LSB_DEPTH_REQUEST, value);
            }
        }

        /// <inheritdoc/>
        public Concentus.Enums.OpusFramesize ExpertFrameDuration
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_EXPERT_FRAME_DURATION_REQUEST, out returnVal);
                return (Concentus.Enums.OpusFramesize)returnVal;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_EXPERT_FRAME_DURATION_REQUEST, (int)value);
            }
        }

        /// <inheritdoc/>
        public bool PredictionDisabled
        {
            get
            {
                int returnVal;
                opus_encoder_ctl(NativeHandle, OPUS_GET_PREDICTION_DISABLED_REQUEST, out returnVal);
                return returnVal != 0;
            }
            set
            {
                opus_encoder_ctl(NativeHandle, OPUS_SET_PREDICTION_DISABLED_REQUEST, value ? 1 : 0);
            }
        }
    }
}