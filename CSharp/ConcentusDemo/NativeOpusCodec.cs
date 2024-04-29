using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConcentusDemo
{
    using Concentus;
    using Concentus.Common.CPlusPlus;
    using Concentus.Enums;
    using Concentus.Structs;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;

    public class NativeOpusCodec : IOpusCodec
    {
        private const string OPUS_TARGET_DLL = "opus32-float-avx.dll";

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr opus_encoder_create(int Fs, int channels, int application, out IntPtr error);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void opus_encoder_destroy(IntPtr encoder);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int opus_encode(IntPtr st, byte[] pcm, int frame_size, IntPtr data, int max_data_bytes);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int opus_encoder_ctl(IntPtr st, int request, int value);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr opus_decoder_create(int Fs, int channels, out IntPtr error);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void opus_decoder_destroy(IntPtr decoder);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int opus_decode(IntPtr st, byte[] data, int len, IntPtr pcm, int frame_size, int decode_fec);

        private const int OPUS_SET_APPLICATION_REQUEST = 4000;
        private const int OPUS_GET_APPLICATION_REQUEST = 4001;
        private const int OPUS_SET_BITRATE_REQUEST = 4002;
        private const int OPUS_GET_BITRATE_REQUEST = 4003;
        private const int OPUS_SET_MAX_BANDWIDTH_REQUEST = 4004;
        private const int OPUS_GET_MAX_BANDWIDTH_REQUEST = 4005;
        private const int OPUS_SET_VBR_REQUEST = 4006;
        private const int OPUS_GET_VBR_REQUEST = 4007;
        private const int OPUS_SET_BANDWIDTH_REQUEST = 4008;
        private const int OPUS_GET_BANDWIDTH_REQUEST = 4009;
        private const int OPUS_SET_COMPLEXITY_REQUEST = 4010;
        private const int OPUS_GET_COMPLEXITY_REQUEST = 4011;
        private const int OPUS_SET_INBAND_FEC_REQUEST = 4012;
        private const int OPUS_GET_INBAND_FEC_REQUEST = 4013;
        private const int OPUS_SET_PACKET_LOSS_PERC_REQUEST = 4014;
        private const int OPUS_GET_PACKET_LOSS_PERC_REQUEST = 4015;
        private const int OPUS_SET_DTX_REQUEST = 4016;
        private const int OPUS_GET_DTX_REQUEST = 4017;
        private const int OPUS_SET_VBR_CONSTRAINT_REQUEST = 4020;
        private const int OPUS_GET_VBR_CONSTRAINT_REQUEST = 4021;
        private const int OPUS_SET_FORCE_CHANNELS_REQUEST = 4022;
        private const int OPUS_GET_FORCE_CHANNELS_REQUEST = 4023;
        private const int OPUS_SET_SIGNAL_REQUEST = 4024;
        private const int OPUS_GET_SIGNAL_REQUEST = 4025;
        private const int OPUS_GET_LOOKAHEAD_REQUEST = 4027;
        /* private const int OPUS_RESET_STATE 4028 */
        private const int OPUS_GET_SAMPLE_RATE_REQUEST = 4029;
        private const int OPUS_GET_FINAL_RANGE_REQUEST = 4031;
        private const int OPUS_GET_PITCH_REQUEST = 4033;
        private const int OPUS_SET_GAIN_REQUEST = 4034;
        private const int OPUS_GET_GAIN_REQUEST = 4045;
        private const int OPUS_SET_LSB_DEPTH_REQUEST = 4036;
        private const int OPUS_GET_LSB_DEPTH_REQUEST = 4037;
        private const int OPUS_GET_LAST_PACKET_DURATION_REQUEST = 4039;
        private const int OPUS_SET_EXPERT_FRAME_DURATION_REQUEST = 4040;
        private const int OPUS_GET_EXPERT_FRAME_DURATION_REQUEST = 4041;
        private const int OPUS_SET_PREDICTION_DISABLED_REQUEST = 4042;
        private const int OPUS_GET_PREDICTION_DISABLED_REQUEST = 4043;
        private const int OPUS_RESET_STATE = 4028;
        private const int OPUS_SET_VOICE_RATIO_REQUEST = 11018;
        private const int OPUS_GET_VOICE_RATIO_REQUEST = 11019;
        private const int OPUS_SET_FORCE_MODE_REQUEST = 11002;

        private const int OPUS_MODE_SILK_ONLY = 1000;

        private const int OPUS_APPLICATION_VOIP = 2048;
        private const int OPUS_APPLICATION_AUDIO = 2049;

        private int _bitrate = 64;
        private int _complexity = 5;
        private double _frameSize = 20;
        private int _packetLoss = 0;
        private int _application = OPUS_APPLICATION_AUDIO;
        private bool _vbr = false;
        private bool _cvbr = false;

        private BasicBufferShort _incomingSamples = new BasicBufferShort(48000);

        private IntPtr _encoder = IntPtr.Zero;
        private IntPtr _decoder = IntPtr.Zero;
        private CodecStatistics _statistics = new CodecStatistics();
        private Stopwatch _timer = new Stopwatch();

        private byte[] scratchBuffer = new byte[10000];

        public NativeOpusCodec()
        {
            IntPtr error;
            _encoder = opus_encoder_create(48000, 1, OPUS_APPLICATION_AUDIO, out error);
            if ((int)error != 0)
            {
                throw new ApplicationException("Could not initialize Opus encoder");
            }
            
            SetBitrate(_bitrate);
            SetComplexity(_complexity);
            SetVBRMode(_vbr, _cvbr);

            _decoder = opus_decoder_create(48000, 1, out error);
            if ((int)error != 0)
            {
                throw new ApplicationException("Could not initialize Opus decoder");
            }
        }

        public void SetBitrate(int bitrate)
        {
            _bitrate = bitrate;
            opus_encoder_ctl(_encoder, OPUS_SET_BITRATE_REQUEST, _bitrate * 1024);
        }

        public void SetComplexity(int complexity)
        {
            _complexity = complexity;
            opus_encoder_ctl(_encoder, OPUS_SET_COMPLEXITY_REQUEST, _complexity);
        }

        public void SetFrameSize(double frameSize)
        {
            _frameSize = frameSize;
        }

        public void SetPacketLoss(int loss)
        {
            _packetLoss = loss;
            if (loss > 0)
            {
                opus_encoder_ctl(_encoder, OPUS_SET_PACKET_LOSS_PERC_REQUEST, _packetLoss);
                opus_encoder_ctl(_encoder, OPUS_SET_INBAND_FEC_REQUEST, 1);
            }
            else
            {
                opus_encoder_ctl(_encoder, OPUS_SET_PACKET_LOSS_PERC_REQUEST, 0);
                opus_encoder_ctl(_encoder, OPUS_SET_INBAND_FEC_REQUEST, 0);
            }
        }

        public void SetApplication(OpusApplication application)
        {
            _application = (int)application;
            opus_encoder_ctl(_encoder, OPUS_SET_APPLICATION_REQUEST, _application);
        }

        public void SetVBRMode(bool vbr, bool constrained)
        {
            _vbr = vbr;
            _cvbr = constrained;
            opus_encoder_ctl(_encoder, OPUS_SET_VBR_REQUEST, vbr ? 1 : 0);
            opus_encoder_ctl(_encoder, OPUS_SET_VBR_CONSTRAINT_REQUEST, constrained ? 1 : 0);
        }

        private int GetFrameSize()
        {
            return (int)(48000 * _frameSize / 1000);
        }

        public CodecStatistics GetStatistics()
        {
            return _statistics;
        }

        public byte[] Compress(AudioChunk input)
        {
            int frameSize = GetFrameSize();

            if (input != null)
            {
                short[] newData = input.Data;
                _incomingSamples.Write(newData);
            }
            else
            {
                // If input is null, assume we are at end of stream and pad the output with zeroes
                int paddingNeeded = _incomingSamples.Available() % frameSize;
                if (paddingNeeded > 0)
                {
                    _incomingSamples.Write(new short[paddingNeeded]);
                }
            }

            int outCursor = 0;

            if (_incomingSamples.Available() >= frameSize)
            {
                unsafe
                {
                    fixed (byte* benc = scratchBuffer)
                    {
                        short[] nextFrameData = _incomingSamples.Read(frameSize);
                        byte[] nextFrameBytes = AudioMath.ShortsToBytes(nextFrameData);
                        IntPtr encodedPtr = new IntPtr(benc);
                        _timer.Reset();
                        _timer.Start();
                        int thisPacketSize = opus_encode(_encoder, nextFrameBytes, frameSize, encodedPtr, scratchBuffer.Length);
                        outCursor += thisPacketSize;
                        _timer.Stop();
                    }
                }
            }

            if (outCursor > 0)
            {
                _statistics.EncodeSpeed = _frameSize / ((double)_timer.ElapsedTicks / Stopwatch.Frequency * 1000);
            }

            byte[] finalOutput = new byte[outCursor];
            Array.Copy(scratchBuffer, 0, finalOutput, 0, outCursor);
            return finalOutput;
        }

        public AudioChunk Decompress(byte[] inputPacket)
        {
            int frameSize = GetFrameSize();

            short[] outputBuffer = new short[frameSize];

            bool lostPacket = new Random().Next(0, 100) < _packetLoss;
            if (!lostPacket)
            {
                // normal decoding
                _timer.Reset();
                _timer.Start();
                unsafe
                {
                    fixed (short* bdec = outputBuffer)
                    {
                        IntPtr decodedPtr = new IntPtr((byte*)(bdec));
                        int thisFrameSize = opus_decode(_decoder, inputPacket, inputPacket.Length, decodedPtr, frameSize, 0);
                    }
                }
                _timer.Stop();
            }
            else
            {
                // packet loss path
                _timer.Reset();
                _timer.Start();
                unsafe
                {
                    fixed (short* bdec = outputBuffer)
                    {
                        IntPtr decodedPtr = new IntPtr((byte*)(bdec));
                        int thisFrameSize = opus_decode(_decoder, null, 0, decodedPtr, frameSize, 1);
                    }
                }
                _timer.Stop();
            }

            short[] finalOutput = new short[frameSize];
            Array.Copy(outputBuffer, finalOutput, finalOutput.Length);

            // Update statistics
            _statistics.Bitrate = inputPacket.Length * 8 * 48000 / 1024 / frameSize;
            OpusMode curMode = OpusPacketInfo.GetEncoderMode(inputPacket.AsSpan());
            if (curMode == OpusMode.MODE_CELT_ONLY)
            {
                _statistics.Mode = "CELT";
            }
            else if (curMode == OpusMode.MODE_HYBRID)
            {
                _statistics.Mode = "Hybrid";
            }
            else if (curMode == OpusMode.MODE_SILK_ONLY)
            {
                _statistics.Mode = "SILK";
            }
            else
            {
                _statistics.Mode = "Unknown";
            }
            _statistics.DecodeSpeed = _frameSize / ((double)_timer.ElapsedTicks / Stopwatch.Frequency * 1000);

            return new AudioChunk(finalOutput, 48000);
        }
    }
}