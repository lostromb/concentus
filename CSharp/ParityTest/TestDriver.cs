using Concentus;
using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ParityTest
{
    public class TestDriver
    {
        private const string OPUS_TARGET_DLL = "opus32-fix.dll";

        private const int DECODER_CHANNELS = 2;
        private const int DECODER_FS = 48000;

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

        private static OpusEncoder CreateConcentusEncoder(TestParameters parameters, BoxedValue<int> concentusError)
        {
            OpusEncoder concentusEncoder = OpusEncoder.Create(parameters.SampleRate, parameters.Channels, parameters.Application, concentusError);
            if (concentusError.Val != 0)
            {
                return null;
            }

            concentusEncoder.SetBitrate(parameters.Bitrate * 1024);
            concentusEncoder.SetComplexity(parameters.Complexity);
            concentusEncoder.SetUseDTX(parameters.UseDTX);
            if (parameters.PacketLossPercent > 0)
            {
                concentusEncoder.SetPacketLossPercent(parameters.PacketLossPercent);
                concentusEncoder.SetUseInbandFEC(true);
            }
            if (parameters.ForceMode != OpusMode.MODE_AUTO)
            {
                concentusEncoder.SetForceMode(parameters.ForceMode);
            }
            concentusEncoder.SetVBR(parameters.UseVBR);
            concentusEncoder.SetVBRConstraint(parameters.ConstrainedVBR);
            return concentusEncoder;
        }

        public static TestResults RunTest(TestParameters parameters, short[] inputFile)
        {
            TestResults returnVal = new TestResults();
            // Create Opus encoder
            IntPtr opusEncoder = IntPtr.Zero;
            IntPtr opusError;
            opusEncoder = opus_encoder_create(parameters.SampleRate, parameters.Channels, (int)parameters.Application, out opusError);
            if ((int)opusError != 0)
            {
                returnVal.Message = "There was an error initializing the Opus encoder";
                returnVal.Passed = false;
                return returnVal;
            }

            opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_BITRATE_REQUEST, parameters.Bitrate * 1024);
            opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_COMPLEXITY_REQUEST, parameters.Complexity);
            opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_DTX_REQUEST, parameters.UseDTX ? 1 : 0);
            if (parameters.PacketLossPercent > 0)
            {
                opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_PACKET_LOSS_PERC_REQUEST, parameters.PacketLossPercent);
                opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_INBAND_FEC_REQUEST, 1);
            }
            if (parameters.ForceMode != OpusMode.MODE_AUTO)
            {
                opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_FORCE_MODE_REQUEST, (int)parameters.ForceMode);
            }
            opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_VBR_REQUEST, parameters.UseVBR ? 1 : 0);
            opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_VBR_CONSTRAINT_REQUEST, parameters.ConstrainedVBR ? 1 : 0);

            // Create Opus decoder
            IntPtr opusDecoder = IntPtr.Zero;
            opusDecoder = opus_decoder_create(DECODER_FS, DECODER_CHANNELS, out opusError);
            if ((int)opusError != 0)
            {
                returnVal.Message = "There was an error initializing the Opus decoder";
                returnVal.Passed = false;
                return returnVal;
            }

            // Create Concentus encoder
            BoxedValue<int> concentusError = new BoxedValue<int>();
            OpusEncoder concentusEncoder = CreateConcentusEncoder(parameters, concentusError);
            if (concentusError.Val != 0)
            {
                returnVal.Message = "There was an error initializing the Concentus encoder";
                returnVal.Passed = false;
                return returnVal;
            }

            OpusEncoder concentusEncoderWithoutFEC = null;
            if (parameters.PacketLossPercent > 0)
            {
                concentusEncoderWithoutFEC = CreateConcentusEncoder(parameters, concentusError);
                if (concentusError.Val != 0)
                {
                    returnVal.Message = "There was an error initializing the Concentus encoder";
                    returnVal.Passed = false;
                    return returnVal;
                }
                concentusEncoderWithoutFEC.SetUseInbandFEC(false);
            }

            // Create Concentus decoder
            OpusDecoder concentusDecoder = OpusDecoder.Create(DECODER_FS, DECODER_CHANNELS, concentusError);
            if (concentusError.Val != 0)
            {
                returnVal.Message = "There was an error initializing the Concentus decoder";
                returnVal.Passed = false;
                return returnVal;
            }

            // Number of paired samples (the audio length)
            int frameSize = (int)(parameters.FrameSize * parameters.SampleRate / 1000);
            // Number of actual samples in the array (the array length)
            int frameSizeStereo = frameSize * parameters.Channels;
            int decodedFrameSize = (int)(parameters.FrameSize * DECODER_FS / 1000);
            int decodedFrameSizeStereo = decodedFrameSize * DECODER_CHANNELS;

            returnVal.FrameLength = frameSize;

            int inputPointer = 0;
            byte[] outputBuffer = new byte[10000];
            short[] inputPacket = new short[frameSizeStereo];
            short[] opusDecoded = new short[decodedFrameSizeStereo];
            short[] concentusDecoded = new short[decodedFrameSizeStereo];
            int frameCount = 0;
            Stopwatch concentusTimer = new Stopwatch();
            Stopwatch opusTimer = new Stopwatch();
            Random packetLoss = new Random();
            Queue<string> PacketTransmissionPattern = new Queue<string>();
            for (int c = 0; c < 5; c++) PacketTransmissionPattern.Enqueue("|");

            byte[] concentusEncoded = null;
            int concentusPacketSize = 0;

            try
            {
                while (inputPointer + frameSizeStereo < inputFile.Length)
                {
                    returnVal.FrameCount = frameCount;
                    Array.Copy(inputFile, inputPointer, inputPacket, 0, frameSizeStereo);
                    inputPointer += frameSizeStereo;

                    concentusTimer.Start();
                    // Encode with Concentus
                    concentusPacketSize = concentusEncoder.Encode(inputPacket, 0, frameSize, outputBuffer, 0, 10000);
                    concentusTimer.Stop();
                    if (concentusPacketSize <= 0)
                    {
                        returnVal.Message = "Invalid packet produced (" + concentusPacketSize + ") (frame " + frameCount + ")";
                        returnVal.Passed = false;
                        returnVal.FailureFrame = inputPacket;
                        return returnVal;
                    }
                    concentusEncoded = new byte[concentusPacketSize];
                    Array.Copy(outputBuffer, concentusEncoded, concentusPacketSize);

                    // Encode with Opus
                    byte[] opusEncoded;
                    unsafe
                    {
                        fixed (byte* benc = outputBuffer)
                        {
                            byte[] nextFrameBytes = ShortsToBytes(inputPacket);
                            IntPtr encodedPtr = new IntPtr((void*)(benc));
                            opusTimer.Start();
                            int opusPacketSize = opus_encode(opusEncoder, nextFrameBytes, frameSize, encodedPtr, 10000);
                            opusTimer.Stop();
                            if (opusPacketSize != concentusPacketSize)
                            {
                                returnVal.Message = "Output packet sizes do not match (frame " + frameCount + ")";
                                returnVal.Passed = false;
                                returnVal.FailureFrame = inputPacket;
                                return returnVal;
                            }
                            opusEncoded = new byte[opusPacketSize];
                            Array.Copy(outputBuffer, opusEncoded, opusPacketSize);
                        }
                    }

                    // Check for encoder parity
                    for (int c = 0; c < concentusPacketSize; c++)
                    {
                        if (opusEncoded[c] != concentusEncoded[c])
                        {
                            returnVal.Message = "Encoded packets do not match (frame " + frameCount + ")";
                            returnVal.Passed = false;
                            returnVal.FailureFrame = inputPacket;
                            return returnVal;
                        }
                    }

                    if (concentusEncoderWithoutFEC != null)
                    {
                        // Encode again without FEC and verify that there is a difference
                        int packetSizeWithoutFEC = concentusEncoderWithoutFEC.Encode(inputPacket, 0, frameSize, outputBuffer, 0, 10000);
                        bool areEqual = concentusPacketSize == packetSizeWithoutFEC;
                        if (areEqual)
                        {
                            for (int c = 0; c < concentusPacketSize; c++)
                            {
                                areEqual = areEqual && outputBuffer[c] == concentusEncoded[c];
                            }
                        }
                        if (areEqual && frameCount > 0)
                        {
                            returnVal.Message = "Enabling FEC did not change the output packet (frame " + frameCount + ")";
                            returnVal.Passed = false;
                            returnVal.FailureFrame = inputPacket;
                            return returnVal;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                returnVal.Message = "ENCODER: " + e.Message + " (frame " + frameCount + ")";
                returnVal.Passed = false;
                returnVal.FailureFrame = inputPacket;
                return returnVal;
            }

            try
            {
                // Should we simulate dropping the packet?
                PacketTransmissionPattern.Dequeue();
                bool droppedPacket = false;
                if (packetLoss.Next(0, 100) < parameters.PacketLossPercent)
                {
                    droppedPacket = true;
                    PacketTransmissionPattern.Enqueue("X");
                }
                PacketTransmissionPattern.Enqueue("O");

                if (!droppedPacket)
                {
                    // Decode with Concentus
                    int concentusOutputFrameSize = concentusDecoder.Decode(concentusEncoded, 0, concentusPacketSize, concentusDecoded, 0, decodedFrameSize, false);

                    // Decode with Opus
                    unsafe
                    {
                        fixed (short* bdec = opusDecoded)
                        {
                            IntPtr decodedPtr = new IntPtr((void*)(bdec));
                            int opusOutputFrameSize = opus_decode(opusDecoder, concentusEncoded, concentusPacketSize, decodedPtr, decodedFrameSize, 0);
                        }
                    }
                }
                else
                {
                    // Decode with Concentus FEC
                    int concentusOutputFrameSize = concentusDecoder.Decode(null, 0, 0, concentusDecoded, 0, decodedFrameSize, true);

                    // Decode with Opus FEC
                    unsafe
                    {
                        fixed (short* bdec = opusDecoded)
                        {
                            IntPtr decodedPtr = new IntPtr((void*)(bdec));
                            int opusOutputFrameSize = opus_decode(opusDecoder, null, 0, decodedPtr, decodedFrameSize, 1);
                        }
                    }
                }

                // Check for decoder parity
                for (int c = 0; c < decodedFrameSizeStereo; c++)
                {
                    if (opusDecoded[c] != concentusDecoded[c])
                    {
                        returnVal.Message = "Decoded frames do not match (frame " + frameCount + ")";
                        if (parameters.PacketLossPercent > 0)
                        {
                            StringBuilder packetLossPattern = new StringBuilder();
                            foreach (string x in PacketTransmissionPattern)
                                packetLossPattern.Append(x);
                            returnVal.Message += " (Packet loss " + packetLossPattern.ToString() + ")";
                        }
                        returnVal.Passed = false;
                        returnVal.FailureFrame = inputPacket;
                        return returnVal;
                    }
                }
                frameCount++;
            }
            catch (Exception e)
            {
                returnVal.Message = "DECODER: " + e.Message + " (frame " + frameCount + ")";
                returnVal.Passed = false;
                returnVal.FailureFrame = inputPacket;
                return returnVal;
            }

            returnVal.Passed = true;
            returnVal.ConcentusTimeMs = concentusTimer.ElapsedMilliseconds;
            returnVal.OpusTimeMs = opusTimer.ElapsedMilliseconds;
            returnVal.Message = "Ok!";

            return returnVal;
        }

        /// <summary>
        /// Converts interleaved byte samples (such as what you get from a capture device)
        /// into linear short samples (that are much easier to work with)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static short[] BytesToShorts(byte[] input)
        {
            return BytesToShorts(input, 0, input.Length);
        }

        /// <summary>
        /// Converts interleaved byte samples (such as what you get from a capture device)
        /// into linear short samples (that are much easier to work with)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static short[] BytesToShorts(byte[] input, int offset, int length)
        {
            short[] processedValues = new short[length / 2];
            for (int c = 0; c < processedValues.Length; c++)
            {
                processedValues[c] = (short)(((int)input[(c * 2) + offset]) << 0);
                processedValues[c] += (short)(((int)input[(c * 2) + 1 + offset]) << 8);
            }

            return processedValues;
        }

        /// <summary>
        /// Converts linear short samples into interleaved byte samples, for writing to a file, waveout device, etc.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] ShortsToBytes(short[] input)
        {
            return ShortsToBytes(input, 0, input.Length);
        }

        /// <summary>
        /// Converts linear short samples into interleaved byte samples, for writing to a file, waveout device, etc.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] ShortsToBytes(short[] input, int offset, int length)
        {
            byte[] processedValues = new byte[length * 2];
            for (int c = 0; c < length; c++)
            {
                processedValues[c * 2] = (byte)(input[c + offset] & 0xFF);
                processedValues[c * 2 + 1] = (byte)((input[c + offset] >> 8) & 0xFF);
            }

            return processedValues;
        }
    }
}
