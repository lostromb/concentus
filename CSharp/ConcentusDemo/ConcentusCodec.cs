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
    public class ConcentusCodec : IOpusCodec
    {
        private int _bitrate = 64;
        private int _complexity = 5;
        private double _frameSize = 20;
        private int _packetLoss = 0;
        private bool _vbr = false;
        private bool _cvbr = false;
        private OpusApplication _application = OpusApplication.OPUS_APPLICATION_AUDIO;

        private BasicBufferShort _incomingSamples = new BasicBufferShort(48000);

        private IOpusEncoder _encoder;
        private IOpusDecoder _decoder;
        private CodecStatistics _statistics = new CodecStatistics();
        private Stopwatch _timer = new Stopwatch();

        private byte[] scratchBuffer = new byte[10000];

        public ConcentusCodec()
        {
            _encoder = new OpusEncoder(48000, 1, OpusApplication.OPUS_APPLICATION_AUDIO);

            SetBitrate(_bitrate);
            SetComplexity(_complexity);
            SetVBRMode(_vbr, _cvbr);
            //_encoder.EnableAnalysis = true;
            _decoder = new OpusDecoder(48000, 1);
        }

        public void SetBitrate(int bitrate)
        {
            _bitrate = bitrate;
            _encoder.Bitrate = (_bitrate * 1024);
        }

        public void SetComplexity(int complexity)
        {
            _complexity = complexity;
            _encoder.Complexity = (_complexity);
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
                _encoder.PacketLossPercent = _packetLoss;
                _encoder.UseInbandFEC = true;
            }
            else
            {
                _encoder.PacketLossPercent = 0;
                _encoder.UseInbandFEC = false;
            }
        }

        public void SetApplication(OpusApplication application)
        {
            _application = application;
            _encoder.Application = _application;
        }

        public void SetVBRMode(bool vbr, bool constrained)
        {
            _vbr = vbr;
            _cvbr = constrained;
            _encoder.UseVBR = vbr;
            _encoder.UseConstrainedVBR = constrained;
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
                _timer.Reset();
                _timer.Start();
                short[] nextFrameData = _incomingSamples.Read(frameSize);
                int thisPacketSize = _encoder.Encode(nextFrameData.AsSpan(), frameSize, scratchBuffer.AsSpan(outCursor), scratchBuffer.Length - outCursor);
                outCursor += thisPacketSize;
                _timer.Stop();
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
                // Normal decoding
                _timer.Reset();
                _timer.Start();
                int thisFrameSize = _decoder.Decode(inputPacket.AsSpan(), outputBuffer.AsSpan(), frameSize, false);
                _timer.Stop();
            }
            else
            {
                // packet loss path
                _timer.Reset();
                _timer.Start();
                int thisFrameSize = _decoder.Decode(ReadOnlySpan<byte>.Empty, outputBuffer.AsSpan(), frameSize, true);
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
            OpusBandwidth curBandwidth = OpusPacketInfo.GetBandwidth(inputPacket.AsSpan());
            if (curBandwidth == OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND)
            {
                _statistics.Bandwidth = 8000;
            }
            else if (curBandwidth == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
            {
                _statistics.Bandwidth = 12000;
            }
            else if (curBandwidth == OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND)
            {
                _statistics.Bandwidth = 16000;
            }
            else if (curBandwidth == OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND)
            {
                _statistics.Bandwidth = 24000;
            }
            else
            {
                _statistics.Bandwidth = 48000;
            }

            return new AudioChunk(finalOutput, 48000);
        }
    }
}
