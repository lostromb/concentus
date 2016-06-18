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

    public class ConcentusCodec : IOpusCodec
    {
        private int _bitrate = 64;
        private int _complexity = 5;
        private double _frameSize = 60;

        private BasicBufferShort _incomingSamples;

        private OpusEncoder _encoder;
        private OpusDecoder _decoder;

        public ConcentusCodec()
        {
            _incomingSamples = new BasicBufferShort(48000);

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
                
            while (outCursor < outputBuffer.Length - 4000 && _incomingSamples.Available() >= frameSize)
            {
                short[] nextFrameData = _incomingSamples.Read(frameSize);
                int thisPacketSize = _encoder.Encode(nextFrameData, 0, frameSize, outputBuffer, outCursor, 4000);
                outCursor += thisPacketSize;
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
            return new AudioChunk(finalOutput, 48000);
        }
    }
}
