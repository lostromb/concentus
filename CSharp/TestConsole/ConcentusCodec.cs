using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestConsole
{
    using Concentus;
    using Concentus.Common.CPlusPlus;
    using Concentus.Structs;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;

    public class ConcentusCodec
    {
        private int _quality = 16;

        public ConcentusCodec(int bitrate)
        {
            _quality = bitrate;
        }

        /// <summary>
        /// The bitrate to use for encoding. 24 is the default
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

        public string GetFormatCode()
        {
            return "opus";
        }

        public string GetDescription()
        {
            return "Opus audio codec 1.1.2 (via Concentus)";
        }

        public bool Initialize()
        {
            return true;
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

        public class OpusCompressionStream : IAudioCompressionStream
        {
            private const int OPUS_APPLICATION_VOIP = 2048;
            private const int OPUS_APPLICATION_MUSIC = 2049;
            private const int OUTPUT_FRAME_SIZE_MS = 60;

            private BasicBufferShort _incomingSamples;

            private OpusEncoder _hEncoder = null;

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
                BoxedValue<int> error = new BoxedValue<int>();
                _hEncoder = opus_encoder.opus_encoder_create(_sampleRate, 1, OPUS_APPLICATION_MUSIC, error);
                if (error.Val != 0)
                {
                    return false;
                }

                // Set the encoder bitrate and complexity
                _hEncoder.SetBitrate(_qualityKbps * 1024);
                _hEncoder.SetComplexity(10);
                    
                return true;
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

                byte[] outputBuffer = new byte[_incomingSamples.Available() * 3];
                int outCursor = 0;
                
                while (outCursor < outputBuffer.Length - 4000 && _incomingSamples.Available() >= frameSize)
                {
                    short[] nextFrameData = _incomingSamples.Read(frameSize);
                    int thisPacketSize = opus_encoder.opus_encode(_hEncoder, nextFrameData.GetPointer(), frameSize, outputBuffer.GetPointer(outCursor + 2), 4000);
                    byte[] packetSize = BitConverter.GetBytes(thisPacketSize);
                    outputBuffer[outCursor++] = packetSize[0];
                    outputBuffer[outCursor++] = packetSize[1];
                    outCursor += thisPacketSize;
                }

                byte[] finalOutput = new byte[outCursor];
                Array.Copy(outputBuffer, 0, finalOutput, 0, outCursor);
                return finalOutput;
            }

            public byte[] Close()
            {
                byte[] trailer = Compress(null);
                return trailer;
            }

            public string GetEncodeParams()
            {
                return string.Format("samplerate={0} q=10 framesize={1}", _sampleRate, OUTPUT_FRAME_SIZE_MS);
            }
        }

        public class OpusDecompressionStream : IAudioDecompressionStream
        {
            private OpusDecoder _hDecoder;

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
                BoxedValue<int> error = new BoxedValue<int>();
                _hDecoder = opus_decoder.opus_decoder_create(_outputSampleRate, 1, error);
                if (error.Val != 0)
                {
                    return false;
                }

                return true;
            }

            public AudioChunk Decompress(byte[] input)
            {
                int frameSize = GetFrameSize();

                if (input != null)
                {
                    _incomingBytes.Write(input);
                }
                
                short[] outputBuffer = new short[frameSize];
                int outCursor = 0;

                if (_nextPacketSize <= 0 && _incomingBytes.Available() >= 2)
                {
                    byte[] packetSize = _incomingBytes.Read(2);
                    _nextPacketSize = BitConverter.ToInt16(packetSize, 0);
                }
                
                while (_nextPacketSize > 0 && _incomingBytes.Available() >= _nextPacketSize)
                {
                    byte[] nextPacketData = _incomingBytes.Read(_nextPacketSize);
                    int thisFrameSize = opus_decoder.opus_decode(_hDecoder, nextPacketData.GetPointer(), _nextPacketSize, outputBuffer.GetPointer(outCursor), frameSize, 0);
                    outCursor += thisFrameSize;

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

                short[] finalOutput = new short[outCursor];
                Array.Copy(outputBuffer, finalOutput, finalOutput.Length);
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
                return trailer;
            }
        }
    }
}
