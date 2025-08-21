using HellaUnsafe.Opus;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.Opus_Encoder;

namespace ParityTest
{
    public unsafe class TestDriver
    {
        private const string OPUS_TARGET_DLL = "opus-1.5.2-x64-float-debug.dll";
        private const bool ACTUALLY_COMPARE = true;

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

        private static Opus_Encoder.OpusEncoder* CreateConcentusEncoder(TestParameters parameters)
        {
            int concentusError = 0;
            Opus_Encoder.OpusEncoder* concentusEncoder = Opus_Encoder.opus_encoder_create(parameters.SampleRate, parameters.Channels, parameters.Application, &concentusError);
            if (concentusError != 0)
            {
                throw new Exception("There was an error initializing the Concentus decoder");
            }

            if (parameters.Bitrate > 0)
            {
                Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusDefines.OPUS_SET_BITRATE_REQUEST, parameters.Bitrate * 1024);
            }
            Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusDefines.OPUS_SET_COMPLEXITY_REQUEST, parameters.Complexity);
            Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusDefines.OPUS_SET_DTX_REQUEST, parameters.UseDTX ? 1 : 0);
            Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusDefines.OPUS_SET_SIGNAL_REQUEST, parameters.Signal);
            if (parameters.PacketLossPercent > 0)
            {
                Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusDefines.OPUS_SET_PACKET_LOSS_PERC_REQUEST, parameters.PacketLossPercent);
                Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusDefines.OPUS_SET_INBAND_FEC_REQUEST, 1);
            }
            if (parameters.ForceMode != OpusDefines.OPUS_AUTO)
            {
                Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusPrivate.OPUS_SET_FORCE_MODE_REQUEST, parameters.ForceMode);
            }
            Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusDefines.OPUS_SET_VBR_REQUEST, parameters.UseVBR ? 1 : 0);
            Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusDefines.OPUS_SET_VBR_CONSTRAINT_REQUEST, parameters.ConstrainedVBR ? 1 : 0);
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
                opus_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_BITRATE_REQUEST, parameters.Bitrate * 1024);
            }
            opus_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_COMPLEXITY_REQUEST, parameters.Complexity);
            opus_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_DTX_REQUEST, parameters.UseDTX ? 1 : 0);
            opus_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_SIGNAL_REQUEST, parameters.Signal);
            if (parameters.PacketLossPercent > 0)
            {
                opus_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_PACKET_LOSS_PERC_REQUEST, parameters.PacketLossPercent);
                opus_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_INBAND_FEC_REQUEST, 1);
            }
            if (parameters.ForceMode != OpusDefines.OPUS_AUTO)
            {
                opus_encoder_ctl(opusEncoder, OpusPrivate.OPUS_SET_FORCE_MODE_REQUEST, parameters.ForceMode);
            }
            opus_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_VBR_REQUEST, parameters.UseVBR ? 1 : 0);
            opus_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_VBR_CONSTRAINT_REQUEST, parameters.ConstrainedVBR ? 1 : 0);

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
            Opus_Encoder.OpusEncoder* concentusEncoder = null;
            Opus_Encoder.OpusEncoder* concentusEncoderWithoutFEC = null;
            try
            {
                concentusEncoder = CreateConcentusEncoder(parameters);

                if (parameters.PacketLossPercent > 0)
                {
                    concentusEncoderWithoutFEC = CreateConcentusEncoder(parameters);
                    Opus_Encoder.opus_encoder_ctl(concentusEncoderWithoutFEC, OpusDefines.OPUS_SET_INBAND_FEC_REQUEST, 0);
                }
            }
            catch (Exception e)
            {
                returnVal.Message = "There was an error initializing the Concentus encoder: " + e.Message;
                returnVal.Passed = false;
                return returnVal;
            }

            // Create Concentus decoder
            Opus_Decoder.OpusDecoder* concentusDecoder;
            int concentusError = 0;
            concentusDecoder = Opus_Decoder.opus_decoder_create(parameters.DecoderSampleRate, parameters.DecoderChannels, &concentusError);
            if (concentusError != 0)
            {
                returnVal.Message = "There was an error initializing the Concentus decoder";
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
                        if (parameters.ForceMode != OpusDefines.OPUS_AUTO && random.NextDouble() < 0.2)
                        {
                            if (random.NextDouble() < 0.5)
                            {
                                Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusPrivate.OPUS_SET_FORCE_MODE_REQUEST, OpusDefines.OPUS_AUTO);
                                if (concentusEncoderWithoutFEC != null)
                                {
                                    Opus_Encoder.opus_encoder_ctl(concentusEncoderWithoutFEC, OpusPrivate.OPUS_SET_FORCE_MODE_REQUEST, OpusDefines.OPUS_AUTO);
                                }

                                opus_encoder_ctl(opusEncoder, OpusPrivate.OPUS_SET_FORCE_MODE_REQUEST, OpusDefines.OPUS_AUTO);
                            }
                            else
                            {
                                Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusPrivate.OPUS_SET_FORCE_MODE_REQUEST, parameters.ForceMode);
                                if (concentusEncoderWithoutFEC != null)
                                {
                                    Opus_Encoder.opus_encoder_ctl(concentusEncoderWithoutFEC, OpusPrivate.OPUS_SET_FORCE_MODE_REQUEST, parameters.ForceMode);
                                }

                                opus_encoder_ctl(opusEncoder, OpusPrivate.OPUS_SET_FORCE_MODE_REQUEST, parameters.ForceMode);
                            }
                        }

                        // If bitrate is variable, set it to a random value every few frames
                        if (parameters.Bitrate < 0 && random.NextDouble() < 0.1)
                        {
                            int newBitrate = random.Next(6, parameters.ForceMode == OpusPrivate.MODE_SILK_ONLY ? 40 : 510);
                            Opus_Encoder.opus_encoder_ctl(concentusEncoder, OpusDefines.OPUS_SET_BITRATE_REQUEST, newBitrate * 1024);
                            opus_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_BITRATE_REQUEST, newBitrate * 1024);
                        }

                        fixed (short* inputFramePtr = inputPacket)
                        fixed (byte* outputBufferPtr = outputBuffer)
                        {
                            concentusTimer.Start();
                            // Encode with Concentus
                            concentusPacketSize = Opus_Encoder.opus_encode(concentusEncoder, inputFramePtr, frameSize, outputBufferPtr, 10000);
                            concentusTimer.Stop();
                        }

                        if (concentusPacketSize <= 0)
                        {
                            returnVal.Message = "Invalid packet produced (error " + concentusPacketSize + " " + Opus.opus_strerror(concentusPacketSize)+ ") (frame " + frameCount + ")";
                            returnVal.Passed = false;
                            returnVal.FailureFrame = inputPacket;
                            return returnVal;
                        }
                        concentusEncoded = new byte[concentusPacketSize];
                        Array.Copy(outputBuffer, 0, concentusEncoded, 0, concentusPacketSize);

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
                                fixed (byte* truePacket = opusEncoded)
                                fixed (byte* myPacket = concentusEncoded)
                                {
                                    PrintF("Expected packet: ");
                                    PrintByteArray(truePacket, concentusPacketSize);
                                    PrintF("Actual packet:   ");
                                    PrintByteArray(myPacket, concentusPacketSize);
                                }
                                returnVal.Message = "Encoded packets do not match (frame " + frameCount + ")";
                                returnVal.Passed = false;
                                returnVal.FailureFrame = inputPacket;
                                return returnVal;
                            }
                        }

                        // Ensure that the packet can be parsed back
                        //try
                        //{
                        //    OpusPacketInfo packetInfo = OpusPacketInfo.ParseOpusPacket(concentusEncodedWithOffset.Data, concentusEncodedWithOffset.Offset, concentusPacketSize);
                        //}
                        //catch (Exception e)
                        //{
                        //    returnVal.Message = "PACKETINFO: " + e.Message + " (frame " + frameCount + ")";
                        //    returnVal.Passed = false;
                        //    returnVal.FailureFrame = inputPacket;
                        //    return returnVal;
                        //}

                        if (concentusEncoderWithoutFEC != null)
                        {
                            // Encode again without FEC and verify that there is a difference
                            int packetSizeWithoutFEC;
                            fixed (short* inputFramePtr = inputPacket)
                            fixed (byte* outputBufferPtr = outputBuffer)
                            {
                                packetSizeWithoutFEC = Opus_Encoder.opus_encode(concentusEncoderWithoutFEC, inputFramePtr, frameSize, outputBufferPtr, 10000);
                            }

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
                    if (random.Next(0, 100) < parameters.PacketLossPercent)
                    {
                        droppedPacket = true;
                        PacketTransmissionPattern.Enqueue("X");
                    }
                    PacketTransmissionPattern.Enqueue("O");

                    if (!droppedPacket)
                    {
                        // Decode with Concentus
                        int concentusOutputFrameSize;
                        fixed (byte* concentusEncodedPtr = concentusEncoded)
                        fixed (short* concentusDecodedPtr = concentusDecoded)
                        {
                            concentusTimer.Start();
                            concentusOutputFrameSize = Opus_Decoder.opus_decode(
                                concentusDecoder,
                                concentusEncodedPtr,
                                concentusPacketSize,
                                concentusDecodedPtr,
                                decodedFrameSize,
                                0);
                            concentusTimer.Stop();
                        }

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

                        int concentusOutputFrameSize;
                        fixed (byte* concentusEncodedPtr = concentusEncoded)
                        fixed (short* concentusDecodedPtr = concentusDecoded)
                        {
                            concentusTimer.Start();
                            concentusOutputFrameSize = Opus_Decoder.opus_decode(
                                concentusDecoder,
                                null,
                                0,
                                concentusDecodedPtr,
                                decodedFrameSize,
                                useFEC ? 1 : 0);
                            concentusTimer.Stop();
                        }

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
                catch (Exception e)
                {
                    returnVal.Message = "DECODER: " + e.Message + " (frame " + frameCount + ")";
                    returnVal.Passed = false;
                    returnVal.FailureFrame = inputPacket;
                    return returnVal;
                }
            }
            finally
            {
                Opus_Encoder.opus_encoder_destroy(concentusEncoder);
                Opus_Decoder.opus_decoder_destroy(concentusDecoder);
                opus_encoder_destroy(opusEncoder);
                opus_decoder_destroy(opusDecoder);
            }

            returnVal.Passed = true;
            returnVal.ConcentusTimeMs = concentusTimer.ElapsedMilliseconds;
            returnVal.OpusTimeMs = opusTimer.ElapsedMilliseconds;
            returnVal.Message = "Ok!";

            return returnVal;
        }

        //public static TestResults RunSurroundFivePointOneTest(TestParameters partialParameters, short[] inputFile)
        //{
        //    TestResults returnVal = new TestResults();
        //    byte[] channelMap = new byte[] { 0, 4, 1, 2, 3, 5 };
        //    const int CHANNELS = 6;
        //    const int ENCODER_SAMPLE_RATE = 48000;
        //    int streams;
        //    int coupled_streams;

        //    // Create Opus encoder
        //    IntPtr opusEncoder = IntPtr.Zero;
        //    IntPtr opusError;
        //    unsafe
        //    {
        //        fixed (byte* mappingPtr = channelMap)
        //        {
        //            opusEncoder = opus_multistream_surround_encoder_create(ENCODER_SAMPLE_RATE, CHANNELS, 1, out streams, out coupled_streams, mappingPtr, (int)OpusApplication.OPUS_APPLICATION_AUDIO, out opusError);
        //        }
        //    }

        //    if ((int)opusError != 0)
        //    {
        //        returnVal.Message = "There was an error initializing the Opus encoder";
        //        returnVal.Passed = false;
        //        return returnVal;
        //    }

        //    if (partialParameters.Bitrate > 0)
        //    {
        //        opus_multistream_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_BITRATE_REQUEST, partialParameters.Bitrate * 1024);
        //    }

        //    opus_multistream_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_COMPLEXITY_REQUEST, partialParameters.Complexity);
        //    opus_multistream_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_VBR_REQUEST, partialParameters.UseVBR ? 1 : 0);
        //    opus_multistream_encoder_ctl(opusEncoder, OpusDefines.OPUS_SET_VBR_CONSTRAINT_REQUEST, partialParameters.ConstrainedVBR ? 1 : 0);

        //    // Create Opus decoder
        //    IntPtr opusDecoder = IntPtr.Zero;
        //    unsafe
        //    {
        //        fixed (byte* mappingPtr = channelMap)
        //        {
        //            opusDecoder = opus_multistream_decoder_create(partialParameters.DecoderSampleRate, CHANNELS, 4, 2, mappingPtr, out opusError);
        //        }
        //    }

        //    if ((int)opusError != 0)
        //    {
        //        returnVal.Message = "There was an error initializing the Opus decoder";
        //        returnVal.Passed = false;
        //        return returnVal;
        //    }

        //    // Create Concentus encoder
        //    OpusMSEncoder concentusEncoder = null;
        //    try
        //    {
        //        concentusEncoder = OpusMSEncoder.CreateSurround(ENCODER_SAMPLE_RATE, CHANNELS, 1, out streams, out coupled_streams, channelMap, OpusApplication.OPUS_APPLICATION_AUDIO);

        //        if (partialParameters.Bitrate > 0)
        //        {
        //            concentusEncoder.Bitrate = (partialParameters.Bitrate * 1024);
        //        }
        //        concentusEncoder.Complexity = (partialParameters.Complexity);
        //        concentusEncoder.UseVBR = (partialParameters.UseVBR);
        //        concentusEncoder.UseConstrainedVBR = (partialParameters.ConstrainedVBR);
        //    }
        //    catch (Exception e)
        //    {
        //        returnVal.Message = "There was an error initializing the Concentus encoder: " + e.Message;
        //        returnVal.Passed = false;
        //        return returnVal;
        //    }

        //    // Create Concentus decoder
        //    OpusMSDecoder concentusDecoder;
        //    try
        //    {
        //        concentusDecoder = new OpusMSDecoder(partialParameters.DecoderSampleRate, CHANNELS, 4, 2, channelMap);
        //    }
        //    catch (Exception e)
        //    {
        //        returnVal.Message = "There was an error initializing the Concentus decoder: " + e.Message;
        //        returnVal.Passed = false;
        //        return returnVal;
        //    }

        //    // Number of paired samples (the audio length)
        //    int frameSize = (int)(partialParameters.FrameSize * partialParameters.SampleRate / 1000);
        //    // Number of actual samples in the array (the array length)
        //    int frameSizeSurround = frameSize * CHANNELS;
        //    int decodedFrameSize = (int)(partialParameters.FrameSize * partialParameters.DecoderSampleRate / 1000);
        //    int decodedFrameSizeSurround = decodedFrameSize * CHANNELS;

        //    returnVal.FrameLength = frameSize;

        //    int inputPointer = 0;
        //    byte[] outputBuffer = new byte[10000];
        //    short[] inputPacket = new short[frameSizeSurround];
        //    short[] opusDecoded = new short[decodedFrameSizeSurround];
        //    short[] concentusDecoded = new short[decodedFrameSizeSurround];
        //    int frameCount = 0;
        //    Stopwatch concentusTimer = new Stopwatch();
        //    Stopwatch opusTimer = new Stopwatch();

        //    byte[] concentusEncoded = null;
        //    int concentusPacketSize = 0;

        //    try
        //    {
        //        try
        //        {
        //            while (inputPointer + frameSizeSurround < inputFile.Length)
        //            {
        //                returnVal.FrameCount = frameCount;
        //                Array.Copy(inputFile, inputPointer, inputPacket, 0, frameSizeSurround);
        //                inputPointer += frameSizeSurround;

        //                Pointer<short> inputPacketWithOffset = Pointerize(inputPacket);
        //                concentusTimer.Start();
        //                // Encode with Concentus
        //                concentusPacketSize = concentusEncoder.EncodeMultistream(inputPacketWithOffset.Data, inputPacketWithOffset.Offset, frameSize, outputBuffer, BUFFER_OFFSET, 10000 - BUFFER_OFFSET);
        //                concentusTimer.Stop();

        //                if (concentusPacketSize <= 0)
        //                {
        //                    returnVal.Message = "Invalid packet produced (" + concentusPacketSize + ") (frame " + frameCount + ")";
        //                    returnVal.Passed = false;
        //                    returnVal.FailureFrame = inputPacket;
        //                    return returnVal;
        //                }
        //                concentusEncoded = new byte[concentusPacketSize];
        //                Array.Copy(outputBuffer, BUFFER_OFFSET, concentusEncoded, 0, concentusPacketSize);

        //                // Encode with Opus
        //                byte[] opusEncoded;
        //                unsafe
        //                {
        //                    fixed (byte* benc = outputBuffer)
        //                    fixed (short* input = inputPacket)
        //                    {
        //                        opusTimer.Start();
        //                        int opusPacketSize = opus_multistream_encode(opusEncoder, input, frameSize, benc, 10000);
        //                        opusTimer.Stop();
        //                        if (ACTUALLY_COMPARE && opusPacketSize != concentusPacketSize)
        //                        {
        //                            returnVal.Message = "Output packet sizes do not match (frame " + frameCount + ")";
        //                            returnVal.Passed = false;
        //                            returnVal.FailureFrame = inputPacket;
        //                            return returnVal;
        //                        }
        //                        opusEncoded = new byte[opusPacketSize];
        //                        Array.Copy(outputBuffer, opusEncoded, opusPacketSize);
        //                    }
        //                }

        //                // Check for encoder parity
        //                for (int c = 0; ACTUALLY_COMPARE && c < concentusPacketSize; c++)
        //                {
        //                    if (opusEncoded[c] != concentusEncoded[c])
        //                    {
        //                        returnVal.Message = "Encoded packets do not match (frame " + frameCount + ")";
        //                        returnVal.Passed = false;
        //                        returnVal.FailureFrame = inputPacket;
        //                        return returnVal;
        //                    }
        //                }

        //                // Ensure that the packet can be parsed back
        //                // The decoder does this on its own, and anyways surround packets have their own weird format
        //                //try
        //                //{
        //                //    Pointer<byte> concentusEncodedWithOffset = Pointerize(concentusEncoded);
        //                //    OpusPacketInfo packetInfo = OpusPacketInfo.ParseOpusPacket(concentusEncodedWithOffset.Data, concentusEncodedWithOffset.Offset, concentusPacketSize);
        //                //}
        //                //catch (Exception e)
        //                //{
        //                //    returnVal.Message = "PACKETINFO: " + e.Message + " (frame " + frameCount + ")";
        //                //    returnVal.Passed = false;
        //                //    returnVal.FailureFrame = inputPacket;
        //                //    return returnVal;
        //                //}
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            returnVal.Message = "ENCODER: " + e.Message + " (frame " + frameCount + ")";
        //            returnVal.Passed = false;
        //            returnVal.FailureFrame = inputPacket;
        //            return returnVal;
        //        }

        //        try
        //        {
        //            // Decode with Concentus
        //            Pointer<byte> concentusEncodedWithOffset = Pointerize(concentusEncoded);
        //            Pointer<short> concentusDecodedWithOffset = Pointerize(concentusDecoded);
        //            int concentusOutputFrameSize = concentusDecoder.DecodeMultistream(
        //                concentusEncodedWithOffset.Data,
        //                concentusEncodedWithOffset.Offset,
        //                concentusPacketSize,
        //                concentusDecodedWithOffset.Data,
        //                concentusDecodedWithOffset.Offset,
        //                decodedFrameSize,
        //                false);
        //            concentusTimer.Start();
        //            concentusDecoded = Unpointerize(concentusDecodedWithOffset, concentusDecoded.Length);
        //            concentusTimer.Stop();

        //            // Decode with Opus
        //            unsafe
        //            {
        //                fixed (short* outputPtr = opusDecoded)
        //                fixed (byte* inputPtr = concentusEncoded)
        //                {
        //                    opusTimer.Start();
        //                    int opusOutputFrameSize = opus_multistream_decode(opusDecoder, inputPtr, concentusPacketSize, outputPtr, decodedFrameSize, 0);
        //                    opusTimer.Stop();
        //                }
        //            }

        //            // Check for decoder parity
        //            for (int c = 0; ACTUALLY_COMPARE && c < decodedFrameSizeSurround; c++)
        //            {
        //                if (opusDecoded[c] != concentusDecoded[c])
        //                {
        //                    returnVal.Message = "Decoded frames do not match (frame " + frameCount + ")";
        //                    returnVal.Passed = false;
        //                    returnVal.FailureFrame = inputPacket;
        //                    return returnVal;
        //                }
        //            }
        //            frameCount++;
        //        }
        //        catch (Exception e)
        //        {
        //            returnVal.Message = "DECODER: " + e.Message + " (frame " + frameCount + ")";
        //            returnVal.Passed = false;
        //            returnVal.FailureFrame = inputPacket;
        //            return returnVal;
        //        }
        //    }
        //    finally
        //    {
        
        //        Opus_Encoder.opus_multistream_encoder_destroy(concentusEncoder);
        //        Opus_Decoder.opus_multistream_decoder_destroy(concentusDecoder);
        //        opus_multistream_encoder_destroy(opusEncoder);
        //        opus_multistream_decoder_destroy(opusDecoder);
        //    }

        //    returnVal.Passed = true;
        //    returnVal.ConcentusTimeMs = concentusTimer.ElapsedMilliseconds;
        //    returnVal.OpusTimeMs = opusTimer.ElapsedMilliseconds;
        //    returnVal.Message = "Ok!";

        //    return returnVal;
        //}

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
