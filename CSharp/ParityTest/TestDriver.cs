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
        private const bool ACTUALLY_COMPARE = true;
        
        private const int BUFFER_OFFSET = 30;

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

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe IntPtr opus_multistream_surround_encoder_create(int Fs, int channels, int mapping_family, out int streams, out int coupled_streams, byte* mapping, int application, out IntPtr error);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int opus_multistream_encode(IntPtr st, short* pcm, int frame_size, byte* data, int max_data_bytes);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void opus_multistream_encoder_destroy(IntPtr encoder);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int opus_multistream_encoder_ctl(IntPtr st, int request, int value);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int opus_multistream_encoder_ctl(IntPtr st, int request, out int value);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe IntPtr opus_multistream_decoder_create(int Fs, int channels, int streams, int coupled_streams, byte* mapping, out IntPtr error);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void opus_multistream_decoder_destroy(IntPtr decoder);

        [DllImport(OPUS_TARGET_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int opus_multistream_decode(IntPtr st, byte* data, int len, short* pcm, int frame_size, int decode_fec);

        private static OpusEncoder CreateConcentusEncoder(TestParameters parameters)
        {
            OpusEncoder concentusEncoder = new OpusEncoder(parameters.SampleRate, parameters.Channels, parameters.Application);

            if (parameters.Bitrate > 0)
            {
                concentusEncoder.Bitrate = (parameters.Bitrate * 1024);
            }
            concentusEncoder.Complexity = (parameters.Complexity);
            concentusEncoder.UseDTX = (parameters.UseDTX);
            if (parameters.PacketLossPercent > 0)
            {
                concentusEncoder.PacketLossPercent = (parameters.PacketLossPercent);
                concentusEncoder.UseInbandFEC = (true);
            }
            if (parameters.ForceMode != OpusMode.MODE_AUTO)
            {
                concentusEncoder.ForceMode = (parameters.ForceMode);
            }
            concentusEncoder.UseVBR = (parameters.UseVBR);
            concentusEncoder.UseConstrainedVBR = (parameters.ConstrainedVBR);
            concentusEncoder.EnableAnalysis = false;
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

            if (parameters.Bitrate > 0)
            {
                opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_BITRATE_REQUEST, parameters.Bitrate * 1024);
            }
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
            opusDecoder = opus_decoder_create(parameters.DecoderSampleRate, parameters.DecoderChannels, out opusError);
            if ((int)opusError != 0)
            {
                returnVal.Message = "There was an error initializing the Opus decoder";
                returnVal.Passed = false;
                return returnVal;
            }

            // Create Concentus encoder
            OpusEncoder concentusEncoder = null;
            OpusEncoder concentusEncoderWithoutFEC = null;
            try
            {
                concentusEncoder = CreateConcentusEncoder(parameters);

                if (parameters.PacketLossPercent > 0)
                {
                    concentusEncoderWithoutFEC = CreateConcentusEncoder(parameters);
                    concentusEncoderWithoutFEC.UseInbandFEC = (false);
                }
            }
            catch (OpusException e)
            {
                returnVal.Message = "There was an error initializing the Concentus encoder: " + e.Message;
                returnVal.Passed = false;
                return returnVal;
            }

            // Create Concentus decoder
            OpusDecoder concentusDecoder;
            try
            {
                concentusDecoder = new OpusDecoder(parameters.DecoderSampleRate, parameters.DecoderChannels);
            }
            catch (OpusException e)
            {
                returnVal.Message = "There was an error initializing the Concentus decoder: " + e.Message;
                returnVal.Passed = false;
                return returnVal;
            }

            // Number of paired samples (the audio length)
            int frameSize = (int)(parameters.FrameSize * parameters.SampleRate / 1000);
            // Number of actual samples in the array (the array length)
            int frameSizeStereo = frameSize * parameters.Channels;
            int decodedFrameSize = (int)(parameters.FrameSize * parameters.DecoderSampleRate / 1000);
            int decodedFrameSizeStereo = decodedFrameSize * parameters.DecoderChannels;

            returnVal.FrameLength = frameSize;

            int inputPointer = 0;
            byte[] outputBuffer = new byte[10000];
            short[] inputPacket = new short[frameSizeStereo];
            short[] opusDecoded = new short[decodedFrameSizeStereo];
            short[] concentusDecoded = new short[decodedFrameSizeStereo];
            int frameCount = 0;
            Stopwatch concentusTimer = new Stopwatch();
            Stopwatch opusTimer = new Stopwatch();
            Random random = new Random();
            Queue<string> PacketTransmissionPattern = new Queue<string>();
            for (int c = 0; c < 5; c++) PacketTransmissionPattern.Enqueue("|");

            byte[] concentusEncoded = null;
            int concentusPacketSize = 0;

            try
            {
                try
                {
                    while (inputPointer + frameSizeStereo < inputFile.Length)
                    {
                        returnVal.FrameCount = frameCount;
                        Array.Copy(inputFile, inputPointer, inputPacket, 0, frameSizeStereo);
                        inputPointer += frameSizeStereo;

                        // Should we randomly switch modes?
                        if (parameters.ForceMode != OpusMode.MODE_AUTO && random.NextDouble() < 0.2)
                        {
                            if (random.NextDouble() < 0.5)
                            {
                                concentusEncoder.ForceMode = (OpusMode.MODE_AUTO);
                                if (concentusEncoderWithoutFEC != null) concentusEncoderWithoutFEC.ForceMode = (OpusMode.MODE_AUTO);
                                opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_FORCE_MODE_REQUEST, OpusConstants.OPUS_AUTO);
                            }
                            else
                            {
                                concentusEncoder.ForceMode = (parameters.ForceMode);
                                if (concentusEncoderWithoutFEC != null) concentusEncoderWithoutFEC.ForceMode = (parameters.ForceMode);
                                opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_FORCE_MODE_REQUEST, (int)parameters.ForceMode);
                            }
                        }

                        // If bitrate is variable, set it to a random value every few frames
                        if (parameters.Bitrate < 0 && random.NextDouble() < 0.1)
                        {
                            int newBitrate = random.Next(6, parameters.ForceMode == OpusMode.MODE_SILK_ONLY ? 40 : 510);
                            concentusEncoder.Bitrate = (newBitrate * 1024);
                            opus_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_BITRATE_REQUEST, newBitrate * 1024);
                        }

                        Pointer<short> inputPacketWithOffset = Pointerize(inputPacket);
                        concentusTimer.Start();
                        // Encode with Concentus
                        concentusPacketSize = concentusEncoder.Encode(inputPacketWithOffset.Data, inputPacketWithOffset.Offset, frameSize, outputBuffer, BUFFER_OFFSET, 10000 - BUFFER_OFFSET);
                        concentusTimer.Stop();

                        if (concentusPacketSize <= 0)
                        {
                            returnVal.Message = "Invalid packet produced (" + concentusPacketSize + ") (frame " + frameCount + ")";
                            returnVal.Passed = false;
                            returnVal.FailureFrame = inputPacket;
                            return returnVal;
                        }
                        concentusEncoded = new byte[concentusPacketSize];
                        Array.Copy(outputBuffer, BUFFER_OFFSET, concentusEncoded, 0, concentusPacketSize);

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
                                if (ACTUALLY_COMPARE && opusPacketSize != concentusPacketSize)
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
                        for (int c = 0; ACTUALLY_COMPARE && c < concentusPacketSize; c++)
                        {
                            if (opusEncoded[c] != concentusEncoded[c])
                            {
                                returnVal.Message = "Encoded packets do not match (frame " + frameCount + ")";
                                returnVal.Passed = false;
                                returnVal.FailureFrame = inputPacket;
                                return returnVal;
                            }
                        }

                        // Ensure that the packet can be parsed back
                        try
                        {
                            Pointer<byte> concentusEncodedWithOffset = Pointerize(concentusEncoded);
                            OpusPacketInfo packetInfo = OpusPacketInfo.ParseOpusPacket(concentusEncodedWithOffset.Data, concentusEncodedWithOffset.Offset, concentusPacketSize);
                        }
                        catch (OpusException e)
                        {
                            returnVal.Message = "PACKETINFO: " + e.Message + " (frame " + frameCount + ")";
                            returnVal.Passed = false;
                            returnVal.FailureFrame = inputPacket;
                            return returnVal;
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
                catch (OpusException e)
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
                    if (random.Next(0, 100) < parameters.PacketLossPercent)
                    {
                        droppedPacket = true;
                        PacketTransmissionPattern.Enqueue("X");
                    }
                    PacketTransmissionPattern.Enqueue("O");

                    if (!droppedPacket)
                    {
                        // Decode with Concentus
                        Pointer<byte> concentusEncodedWithOffset = Pointerize(concentusEncoded);
                        Pointer<short> concentusDecodedWithOffset = Pointerize(concentusDecoded);
                        int concentusOutputFrameSize = concentusDecoder.Decode(
                            concentusEncodedWithOffset.Data,
                            concentusEncodedWithOffset.Offset,
                            concentusPacketSize,
                            concentusDecodedWithOffset.Data,
                            concentusDecodedWithOffset.Offset,
                            decodedFrameSize,
                            false);
                        concentusTimer.Start();
                        concentusDecoded = Unpointerize(concentusDecodedWithOffset, concentusDecoded.Length);
                        concentusTimer.Stop();

                        // Decode with Opus
                        unsafe
                        {
                            fixed (short* bdec = opusDecoded)
                            {
                                IntPtr decodedPtr = new IntPtr((void*)(bdec));
                                opusTimer.Start();
                                int opusOutputFrameSize = opus_decode(opusDecoder, concentusEncoded, concentusPacketSize, decodedPtr, decodedFrameSize, 0);
                                opusTimer.Stop();
                            }
                        }
                    }
                    else
                    {
                        bool useFEC = random.NextDouble() > 0.5;
                        // Decode with Concentus FEC
                        concentusTimer.Start();
                        int concentusOutputFrameSize = concentusDecoder.Decode(null, 0, 0, concentusDecoded, 0, decodedFrameSize, useFEC);
                        concentusTimer.Stop();

                        // Decode with Opus FEC
                        unsafe
                        {
                            fixed (short* bdec = opusDecoded)
                            {
                                IntPtr decodedPtr = new IntPtr((void*)(bdec));
                                opusTimer.Start();
                                int opusOutputFrameSize = opus_decode(opusDecoder, null, 0, decodedPtr, decodedFrameSize, useFEC ? 1 : 0);
                                opusTimer.Stop();
                            }
                        }
                    }

                    // Check for decoder parity
                    for (int c = 0; ACTUALLY_COMPARE && c < decodedFrameSizeStereo; c++)
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
                catch (OpusException e)
                {
                    returnVal.Message = "DECODER: " + e.Message + " (frame " + frameCount + ")";
                    returnVal.Passed = false;
                    returnVal.FailureFrame = inputPacket;
                    return returnVal;
                }
            }
            finally
            {
                opus_encoder_destroy(opusEncoder);
                opus_decoder_destroy(opusDecoder);
            }

            returnVal.Passed = true;
            returnVal.ConcentusTimeMs = concentusTimer.ElapsedMilliseconds;
            returnVal.OpusTimeMs = opusTimer.ElapsedMilliseconds;
            returnVal.Message = "Ok!";

            return returnVal;
        }

        public static TestResults RunSurroundFivePointOneTest(TestParameters partialParameters, short[] inputFile)
        {
            TestResults returnVal = new TestResults();
            byte[] channelMap = new byte[] { 0, 4, 1, 2, 3, 5 };
            const int CHANNELS = 6;
            const int ENCODER_SAMPLE_RATE = 48000;
            int streams;
            int coupled_streams;

            // Create Opus encoder
            IntPtr opusEncoder = IntPtr.Zero;
            IntPtr opusError;
            unsafe
            {
                fixed (byte* mappingPtr = channelMap)
                {
                    opusEncoder = opus_multistream_surround_encoder_create(ENCODER_SAMPLE_RATE, CHANNELS, 1, out streams, out coupled_streams, mappingPtr, (int)OpusApplication.OPUS_APPLICATION_AUDIO, out opusError);
                }
            }

            if ((int)opusError != 0)
            {
                returnVal.Message = "There was an error initializing the Opus encoder";
                returnVal.Passed = false;
                return returnVal;
            }

            if (partialParameters.Bitrate > 0)
            {
                opus_multistream_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_BITRATE_REQUEST, partialParameters.Bitrate * 1024);
            }

            opus_multistream_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_COMPLEXITY_REQUEST, partialParameters.Complexity);
            opus_multistream_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_VBR_REQUEST, partialParameters.UseVBR ? 1 : 0);
            opus_multistream_encoder_ctl(opusEncoder, OpusControl.OPUS_SET_VBR_CONSTRAINT_REQUEST, partialParameters.ConstrainedVBR ? 1 : 0);

            // Create Opus decoder
            IntPtr opusDecoder = IntPtr.Zero;
            unsafe
            {
                fixed (byte* mappingPtr = channelMap)
                {
                    opusDecoder = opus_multistream_decoder_create(partialParameters.DecoderSampleRate, CHANNELS, 4, 2, mappingPtr, out opusError);
                }
            }

            if ((int)opusError != 0)
            {
                returnVal.Message = "There was an error initializing the Opus decoder";
                returnVal.Passed = false;
                return returnVal;
            }

            // Create Concentus encoder
            OpusMSEncoder concentusEncoder = null;
            try
            {
                concentusEncoder = OpusMSEncoder.CreateSurround(ENCODER_SAMPLE_RATE, CHANNELS, 1, out streams, out coupled_streams, channelMap, OpusApplication.OPUS_APPLICATION_AUDIO);

                if (partialParameters.Bitrate > 0)
                {
                    concentusEncoder.Bitrate = (partialParameters.Bitrate * 1024);
                }
                concentusEncoder.Complexity = (partialParameters.Complexity);
                concentusEncoder.UseVBR = (partialParameters.UseVBR);
                concentusEncoder.UseConstrainedVBR = (partialParameters.ConstrainedVBR);
            }
            catch (OpusException e)
            {
                returnVal.Message = "There was an error initializing the Concentus encoder: " + e.Message;
                returnVal.Passed = false;
                return returnVal;
            }

            // Create Concentus decoder
            OpusMSDecoder concentusDecoder;
            try
            {
                concentusDecoder = new OpusMSDecoder(partialParameters.DecoderSampleRate, CHANNELS, 4, 2, channelMap);
            }
            catch (OpusException e)
            {
                returnVal.Message = "There was an error initializing the Concentus decoder: " + e.Message;
                returnVal.Passed = false;
                return returnVal;
            }

            // Number of paired samples (the audio length)
            int frameSize = (int)(partialParameters.FrameSize * partialParameters.SampleRate / 1000);
            // Number of actual samples in the array (the array length)
            int frameSizeSurround = frameSize * CHANNELS;
            int decodedFrameSize = (int)(partialParameters.FrameSize * partialParameters.DecoderSampleRate / 1000);
            int decodedFrameSizeSurround = decodedFrameSize * CHANNELS;

            returnVal.FrameLength = frameSize;

            int inputPointer = 0;
            byte[] outputBuffer = new byte[10000];
            short[] inputPacket = new short[frameSizeSurround];
            short[] opusDecoded = new short[decodedFrameSizeSurround];
            short[] concentusDecoded = new short[decodedFrameSizeSurround];
            int frameCount = 0;
            Stopwatch concentusTimer = new Stopwatch();
            Stopwatch opusTimer = new Stopwatch();

            byte[] concentusEncoded = null;
            int concentusPacketSize = 0;

            try
            {
                try
                {
                    while (inputPointer + frameSizeSurround < inputFile.Length)
                    {
                        returnVal.FrameCount = frameCount;
                        Array.Copy(inputFile, inputPointer, inputPacket, 0, frameSizeSurround);
                        inputPointer += frameSizeSurround;

                        Pointer<short> inputPacketWithOffset = Pointerize(inputPacket);
                        concentusTimer.Start();
                        // Encode with Concentus
                        concentusPacketSize = concentusEncoder.EncodeMultistream(inputPacketWithOffset.Data, inputPacketWithOffset.Offset, frameSize, outputBuffer, BUFFER_OFFSET, 10000 - BUFFER_OFFSET);
                        concentusTimer.Stop();

                        if (concentusPacketSize <= 0)
                        {
                            returnVal.Message = "Invalid packet produced (" + concentusPacketSize + ") (frame " + frameCount + ")";
                            returnVal.Passed = false;
                            returnVal.FailureFrame = inputPacket;
                            return returnVal;
                        }
                        concentusEncoded = new byte[concentusPacketSize];
                        Array.Copy(outputBuffer, BUFFER_OFFSET, concentusEncoded, 0, concentusPacketSize);

                        // Encode with Opus
                        byte[] opusEncoded;
                        unsafe
                        {
                            fixed (byte* benc = outputBuffer)
                            fixed (short* input = inputPacket)
                            {
                                opusTimer.Start();
                                int opusPacketSize = opus_multistream_encode(opusEncoder, input, frameSize, benc, 10000);
                                opusTimer.Stop();
                                if (ACTUALLY_COMPARE && opusPacketSize != concentusPacketSize)
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
                        for (int c = 0; ACTUALLY_COMPARE && c < concentusPacketSize; c++)
                        {
                            if (opusEncoded[c] != concentusEncoded[c])
                            {
                                returnVal.Message = "Encoded packets do not match (frame " + frameCount + ")";
                                returnVal.Passed = false;
                                returnVal.FailureFrame = inputPacket;
                                return returnVal;
                            }
                        }

                        // Ensure that the packet can be parsed back
                        // The decoder does this on its own, and anyways surround packets have their own weird format
                        //try
                        //{
                        //    Pointer<byte> concentusEncodedWithOffset = Pointerize(concentusEncoded);
                        //    OpusPacketInfo packetInfo = OpusPacketInfo.ParseOpusPacket(concentusEncodedWithOffset.Data, concentusEncodedWithOffset.Offset, concentusPacketSize);
                        //}
                        //catch (OpusException e)
                        //{
                        //    returnVal.Message = "PACKETINFO: " + e.Message + " (frame " + frameCount + ")";
                        //    returnVal.Passed = false;
                        //    returnVal.FailureFrame = inputPacket;
                        //    return returnVal;
                        //}
                    }
                }
                catch (OpusException e)
                {
                    returnVal.Message = "ENCODER: " + e.Message + " (frame " + frameCount + ")";
                    returnVal.Passed = false;
                    returnVal.FailureFrame = inputPacket;
                    return returnVal;
                }

                try
                {
                    // Decode with Concentus
                    Pointer<byte> concentusEncodedWithOffset = Pointerize(concentusEncoded);
                    Pointer<short> concentusDecodedWithOffset = Pointerize(concentusDecoded);
                    int concentusOutputFrameSize = concentusDecoder.DecodeMultistream(
                        concentusEncodedWithOffset.Data,
                        concentusEncodedWithOffset.Offset,
                        concentusPacketSize,
                        concentusDecodedWithOffset.Data,
                        concentusDecodedWithOffset.Offset,
                        decodedFrameSize,
                        false);
                    concentusTimer.Start();
                    concentusDecoded = Unpointerize(concentusDecodedWithOffset, concentusDecoded.Length);
                    concentusTimer.Stop();

                    // Decode with Opus
                    unsafe
                    {
                        fixed (short* outputPtr = opusDecoded)
                        fixed (byte* inputPtr = concentusEncoded)
                        {
                            opusTimer.Start();
                            int opusOutputFrameSize = opus_multistream_decode(opusDecoder, inputPtr, concentusPacketSize, outputPtr, decodedFrameSize, 0);
                            opusTimer.Stop();
                        }
                    }

                    // Check for decoder parity
                    for (int c = 0; ACTUALLY_COMPARE && c < decodedFrameSizeSurround; c++)
                    {
                        if (opusDecoded[c] != concentusDecoded[c])
                        {
                            returnVal.Message = "Decoded frames do not match (frame " + frameCount + ")";
                            returnVal.Passed = false;
                            returnVal.FailureFrame = inputPacket;
                            return returnVal;
                        }
                    }
                    frameCount++;
                }
                catch (OpusException e)
                {
                    returnVal.Message = "DECODER: " + e.Message + " (frame " + frameCount + ")";
                    returnVal.Passed = false;
                    returnVal.FailureFrame = inputPacket;
                    return returnVal;
                }
            }
            finally
            {
                opus_multistream_encoder_destroy(opusEncoder);
                opus_multistream_decoder_destroy(opusDecoder);
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

        internal static Pointer<T> Pointerize<T>(T[] array)
        {
            T[] newArray = new T[array.Length + BUFFER_OFFSET];
            Array.Copy(array, 0, newArray, BUFFER_OFFSET, array.Length);
            return newArray.GetPointer(BUFFER_OFFSET);
        }

        internal static T[] Unpointerize<T>(Pointer<T> array, int length)
        {
            T[] newArray = new T[length];
            array.MemCopyTo(newArray, 0, length);
            return newArray;
        }
    }
}
