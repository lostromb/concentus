using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConcentusDemo
{
    
    using Concentus.Common.CPlusPlus;
    using Concentus.Structs;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;

    public class OpusCodec
    {
        private int _quality = 32;
        private const string OPUS_TARGET_DLL = "opus64-fix.dll";

        public OpusCodec(int bitrate)
        {
            _quality = bitrate;
        }

        /// <summary>
        /// The bitrate to use for encoding.
        /// </summary>
        public int QualityKbps
        {
            get
            {
                return _quality;
            }
            set
            {
                _quality = value;
            }
        }

        public byte[] Compress(AudioChunk audio)
        {
            string dummy;
            return Compress(audio, out dummy);
        }

        public AudioChunk Decompress(byte[] input)
        {
            return Decompress(input, string.Empty);
        }

        public byte[] Compress(AudioChunk audio, out string encodeParams)
        {
            byte[] returnVal = AudioUtils.CompressAudioUsingStream(audio, this.CreateCompressionStream(audio.SampleRate), out encodeParams);
            return returnVal;
        }

        public AudioChunk Decompress(byte[] input, string encodeParams)
        {
            AudioChunk returnVal = AudioUtils.DecompressAudioUsingStream(input, this.CreateDecompressionStream(encodeParams));
            return returnVal;
        }

        public IAudioCompressionStream CreateCompressionStream(int inputSampleRate, string traceId = null)
        {
            OpusCompressionStream returnVal = new OpusCompressionStream(inputSampleRate, _quality);
            if (!returnVal.Initialize())
            {
                return null;
            }

            return returnVal;
        }

        public IAudioDecompressionStream CreateDecompressionStream(string encodeParams, string traceId = null)
        {
            OpusDecompressionStream returnVal = new OpusDecompressionStream(encodeParams);
            if (!returnVal.Initialize())
            {
                return null;
            }

            return returnVal;
        }

        public class OpusCompressionStream : IAudioCompressionStream, IDisposable
        {
            [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr opus_encoder_create(int Fs, int channels, int application, out IntPtr error);

            [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
            private static extern void opus_encoder_destroy(IntPtr encoder);

            [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
            private static extern int opus_encode(IntPtr st, byte[] pcm, int frame_size, IntPtr data, int max_data_bytes);

            [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
            private static extern int opus_encoder_ctl(IntPtr st, int request, int value);

            private const int OPUS_SET_BITRATE_REQUEST = 4002;
            private const int OPUS_SET_COMPLEXITY_REQUEST = 4010;
            private const int OPUS_APPLICATION_VOIP = 2048;
            private const int OPUS_APPLICATION_MUSIC = 2049;
            private const int OUTPUT_FRAME_SIZE_MS = 60;

            private BasicBufferShort _incomingSamples;

            /// <summary>
            /// The native pointer to the encoder state object
            /// </summary>
            private IntPtr _hEncoder = IntPtr.Zero;

            private int _sampleRate = 48000;
            private int _qualityKbps;

            public OpusCompressionStream(int sampleRate, int qualityKbps)
            {
                _sampleRate = FindSampleRateFloor(sampleRate);
                _qualityKbps = qualityKbps;

                // Buffer for 4 seconds of input
                _incomingSamples = new BasicBufferShort(_sampleRate * 4);
            }

            private int FindSampleRateFloor(int desiredSampleRate)
            {
                if (desiredSampleRate >= 48000)
                {
                    return 48000;
                }
                if (desiredSampleRate >= 24000)
                {
                    return 24000;
                }
                if (desiredSampleRate >= 16000)
                {
                    return 16000;
                }
                if (desiredSampleRate >= 12000)
                {
                    return 12000;
                }

                return 8000;
            }

            public bool Initialize()
            {
                try
                {
                    IntPtr error;
                    _hEncoder = opus_encoder_create(_sampleRate, 1, OPUS_APPLICATION_MUSIC, out error);
                    if ((int)error != 0)
                    {
                        return false;
                    }

                    // Set the encoder bitrate and set the complexity to 10
                    opus_encoder_ctl(_hEncoder, OPUS_SET_BITRATE_REQUEST, _qualityKbps * 1024);
                    opus_encoder_ctl(_hEncoder, OPUS_SET_COMPLEXITY_REQUEST, 10);

                    return true;
                }
                catch (BadImageFormatException e)
                {
                    return false;
                }
                catch (DllNotFoundException)
                {
                    return false;
                }
            }

            private int GetFrameSize()
            {
                // 60ms window is used for all packets
                return _sampleRate * OUTPUT_FRAME_SIZE_MS / 1000;
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

                byte[] outputBuffer = new byte[_incomingSamples.Capacity() * 3];
                int outCursor = 0;

                unsafe
                {
                    fixed (byte* benc = outputBuffer)
                    {
                        while (outCursor < outputBuffer.Length - 4000 && _incomingSamples.Available() >= frameSize)
                        {
                            short[] nextFrameData = _incomingSamples.Read(frameSize);
                            byte[] nextFrameBytes = AudioMath.ShortsToBytes(nextFrameData);
                            IntPtr encodedPtr = new IntPtr((void*)(benc + outCursor + 2));
                            int thisPacketSize = opus_encode(_hEncoder, nextFrameBytes, frameSize, encodedPtr, 4000);
                            byte[] packetSize = BitConverter.GetBytes(thisPacketSize);
                            outputBuffer[outCursor++] = packetSize[0];
                            outputBuffer[outCursor++] = packetSize[1];
                            outCursor += thisPacketSize;
                        }
                    }
                }

                byte[] finalOutput = new byte[outCursor];
                Array.Copy(outputBuffer, 0, finalOutput, 0, outCursor);
                return finalOutput;
            }

            public byte[] Close()
            {
                byte[] trailer = Compress(null);
                Dispose();
                return trailer;
            }

            public string GetEncodeParams()
            {
                return string.Format("samplerate={0} q=10 framesize={1}", _sampleRate, OUTPUT_FRAME_SIZE_MS);
            }

            public void Dispose()
            {
                if (_hEncoder != IntPtr.Zero)
                {
                    opus_encoder_destroy(_hEncoder);
                    _hEncoder = IntPtr.Zero;
                }
            }
        }

        public class OpusDecompressionStream : IAudioDecompressionStream, IDisposable
        {
            [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr opus_decoder_create(int Fs, int channels, out IntPtr error);

            [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
            private static extern void opus_decoder_destroy(IntPtr decoder);

            [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
            private static extern int opus_decode(IntPtr st, byte[] data, int len, IntPtr pcm, int frame_size, int decode_fec);

            /// <summary>
            /// The native pointer to the decoder state object
            /// </summary>
            private IntPtr _hDecoder = IntPtr.Zero;

            private BasicBufferByte _incomingBytes;

            private int _nextPacketSize = 0;
            private float _outputFrameLengthMs;
            private int _outputSampleRate;

            public OpusDecompressionStream(string encodeParams)
            {
                _incomingBytes = new BasicBufferByte(1000000);

                Match sampleRateParse = Regex.Match(encodeParams, "sampleRate=([0-9]+)");
                if (sampleRateParse.Success)
                {
                    _outputSampleRate = int.Parse(sampleRateParse.Groups[1].Value);
                }
                else
                {
                    _outputSampleRate = 48000;
                }

                Match frameSizeParse = Regex.Match(encodeParams, "framesize=([0-9]+)");
                if (frameSizeParse.Success)
                {
                    _outputFrameLengthMs = float.Parse(frameSizeParse.Groups[1].Value);
                }
                else
                {
                    _outputFrameLengthMs = 60;
                }
            }

            public bool Initialize()
            {
                try
                {
                    IntPtr error;
                    _hDecoder = opus_decoder_create(_outputSampleRate, 1, out error);
                    if ((int)error != 0)
                    {
                        return false;
                    }

                    return true;
                }
                catch (BadImageFormatException e)
                {
                    return false;
                }
                catch (DllNotFoundException)
                {
                    return false;
                }
            }

            public AudioChunk Decompress(byte[] input)
            {
                int frameSize = GetFrameSize();

                if (input != null)
                {
                    _incomingBytes.Write(input);
                }

                // Assume the compression ratio will never be above 40:1
                byte[] outputBuffer = new byte[_incomingBytes.Capacity() * 40];
                int outCursor = 0;

                if (_nextPacketSize <= 0 && _incomingBytes.Available() >= 2)
                {
                    byte[] packetSize = _incomingBytes.Read(2);
                    _nextPacketSize = BitConverter.ToInt16(packetSize, 0);
                }

                unsafe
                {
                    fixed (byte* bdec = outputBuffer)
                    {
                        while (_nextPacketSize > 0 && _incomingBytes.Available() >= _nextPacketSize)
                        {
                            byte[] nextPacketData = _incomingBytes.Read(_nextPacketSize);
                            IntPtr decodedPtr = new IntPtr((void*)(bdec + outCursor));
                            int thisFrameSize = opus_decode(_hDecoder, nextPacketData, _nextPacketSize, decodedPtr, frameSize, 0);
                            outCursor += thisFrameSize * 2;

                            if (_incomingBytes.Available() >= 2)
                            {
                                byte[] packetSize = _incomingBytes.Read(2);
                                _nextPacketSize = BitConverter.ToInt16(packetSize, 0);
                            }
                            else
                            {
                                _nextPacketSize = 0;
                            }
                        }
                    }
                }

                short[] finalOutput = AudioMath.BytesToShorts(outputBuffer, 0, outCursor);
                return new AudioChunk(finalOutput, _outputSampleRate);
            }

            private int GetFrameSize()
            {
                // 60ms window is the default used for all packets
                return (int)(_outputSampleRate * _outputFrameLengthMs / 1000);
            }

            public AudioChunk Close()
            {
                AudioChunk trailer = Decompress(null);
                Dispose();
                return trailer;
            }

            public void Dispose()
            {
                if (_hDecoder != IntPtr.Zero)
                {
                    opus_decoder_destroy(_hDecoder);
                    _hDecoder = IntPtr.Zero;
                }
            }
        }
    }
}
