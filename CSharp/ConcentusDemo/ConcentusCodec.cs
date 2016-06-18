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

        private BasicBufferShort _incomingSamples = new BasicBufferShort(48000);

        private OpusEncoder _encoder;
        private OpusDecoder _decoder;
        private CodecStatistics _statistics = new CodecStatistics();
        private Stopwatch _encodeTimer = new Stopwatch();

        public ConcentusCodec()
        {
            BoxedValue<int> error = new BoxedValue<int>();
            _encoder = OpusEncoder.Create(48000, 1, OpusApplication.OPUS_APPLICATION_AUDIO, error);
            if (error.Val != 0)
            {
                //return false;
            }

            // Set the encoder bitrate and complexity
            _encoder.SetBitrate(_bitrate * 1024);
            _encoder.SetComplexity(_complexity);

            _decoder = OpusDecoder.Create(48000, 1, error);
            if (error.Val != 0)
            {
                //return false;
            }
        }

        public void SetBitrate(int bitrate)
        {
            _bitrate = bitrate;
            _encoder.SetBitrate(_bitrate * 1024);
        }

        public void SetComplexity(int complexity)
        {
            _complexity = complexity;
            _encoder.SetComplexity(_complexity);
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

            byte[] outputBuffer = new byte[frameSize * 2];
            int outCursor = 0;
            
            if (_incomingSamples.Available() >= frameSize)
            {
                _encodeTimer.Reset();
                _encodeTimer.Start();
                short[] nextFrameData = _incomingSamples.Read(frameSize);
                int thisPacketSize = _encoder.Encode(nextFrameData, 0, frameSize, outputBuffer, outCursor, 4000);
                outCursor += thisPacketSize;
                _encodeTimer.Stop();
            }

            if (outCursor > 0)
            {
                _statistics.EncodeSpeed = (double)_frameSize / 48 * 100000 / (double)_encodeTimer.ElapsedTicks;
            } 

            byte[] finalOutput = new byte[outCursor];
            Array.Copy(outputBuffer, 0, finalOutput, 0, outCursor);
            return finalOutput;
        }
        
        public AudioChunk Decompress(byte[] inputPacket)
        {
            int frameSize = GetFrameSize();
            
            short[] outputBuffer = new short[frameSize];
            
            int thisFrameSize = _decoder.Decode(inputPacket, 0, inputPacket.Length, outputBuffer, 0, frameSize, false);
            
            short[] finalOutput = new short[frameSize];
            Array.Copy(outputBuffer, finalOutput, finalOutput.Length);

            // Update statistics
            _statistics.Bitrate = inputPacket.Length * 8 * 48000 / 1024 / frameSize;
            OpusMode curMode = OpusPacket.opus_packet_get_mode(inputPacket.GetPointer());
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

            return new AudioChunk(finalOutput, 48000);
        }
    }
}
