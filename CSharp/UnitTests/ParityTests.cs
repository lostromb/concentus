using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using ParityTest;
using Concentus.Enums;
using System.Linq;
using System.Runtime.InteropServices;
using Concentus.Structs;

namespace UnitTests
{
    [TestClass]
    public class ParityTests
    {
        private static IDictionary<string, short[]> testSamples = new Dictionary<string, short[]>();
        
        private static string GetTestFileName(int band, bool stereo)
        {
            return string.Format("{0}Khz {1}.raw", band, stereo ? "Stereo" : "Mono"); ;
        }

        private static void LoadTestFile(int band, bool stereo)
        {
            string fileName = GetTestFileName(band, stereo);
            byte[] file = File.ReadAllBytes(fileName);
            short[] samples = TestDriver.BytesToShorts(file);
            testSamples.Add(fileName, samples);
        }

        private static short[] GetTestSample(TestParameters parameters)
        {
            string key = GetTestFileName(parameters.SampleRate / 1000, parameters.Channels == 2);
            return testSamples[key];
        }

        [ClassInitialize]
        public static void TestInitialize(TestContext context)
        {
            LoadTestFile(8, false);
            LoadTestFile(12, false);
            LoadTestFile(16, false);
            LoadTestFile(24, false);
            LoadTestFile(48, false);
            LoadTestFile(8, true);
            LoadTestFile(12, true);
            LoadTestFile(16, true);
            LoadTestFile(24, true);
            LoadTestFile(48, true);
        }

        private void RunParityTest(TestParameters p)
        {
            TestResults results = TestDriver.RunTest(p, GetTestSample(p));
            Assert.IsTrue(results.Passed);
        }

        [TestMethod]
        public void TestSilkBasic()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                Bitrate = 12,
                Channels = 2,
                Complexity = 10,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_SILK_ONLY,
                FrameSize = 10,
                PacketLossPercent = 0,
                SampleRate = 16000,
                UseDTX = false,
                UseVBR = true
            });
        }

        [TestMethod]
        public void TestCeltBasic()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = 64,
                Channels = 2,
                Complexity = 10,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_CELT_ONLY,
                FrameSize = 20,
                PacketLossPercent = 0,
                SampleRate = 48000,
                UseDTX = false,
                UseVBR = true
            });
        }

        [TestMethod]
        public void TestCeltVBR()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Channels = 2,
                Complexity = 10,
                ConstrainedVBR = true,
                ForceMode = Concentus.Enums.OpusMode.MODE_CELT_ONLY,
                FrameSize = 20,
                PacketLossPercent = 0,
                SampleRate = 48000,
                UseDTX = false,
                UseVBR = true
            });
        }

        [TestMethod]
        public void TestBitrateTransitions()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Channels = 2,
                Complexity = 10,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_AUTO,
                FrameSize = 20,
                PacketLossPercent = 0,
                SampleRate = 48000,
                UseDTX = false,
                UseVBR = true
            });
        }

        [TestMethod]
        public void Test12KhzBug()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY,
                Bitrate = 64,
                Channels = 2,
                Complexity = 10,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_CELT_ONLY,
                FrameSize = 2.5,
                PacketLossPercent = 0,
                SampleRate = 12000,
                UseDTX = false,
                UseVBR = false
            });
        }

        [TestMethod]
        public void TestDTX()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                Bitrate = 30,
                Channels = 2,
                Complexity = 0,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_SILK_ONLY,
                FrameSize = 20,
                PacketLossPercent = 0,
                SampleRate = 8000,
                UseDTX = true,
                UseVBR = false
            });
        }

        [TestMethod]
        public void TestPLC()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                Bitrate = 30,
                Channels = 2,
                Complexity = 0,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_SILK_ONLY,
                FrameSize = 20,
                PacketLossPercent = 20,
                SampleRate = 48000,
                UseDTX = false,
                UseVBR = false
            });
        }

        [TestMethod]
        public void TestBug1()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                Bitrate = -1,
                Channels = 2,
                Complexity = 0,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_AUTO,
                FrameSize = 20,
                PacketLossPercent = 20,
                SampleRate = 8000,
                UseDTX = true,
                UseVBR = false
            });
        }

        [TestMethod]
        public void TestBug2()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Channels = 2,
                Complexity = 0,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_CELT_ONLY,
                FrameSize = 10,
                PacketLossPercent = 20,
                SampleRate = 48000,
                UseDTX = true,
                UseVBR = true
            });
        }

        [Ignore]
        [TestMethod]
        public void TestBug3()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                Bitrate = 20,
                Channels = 1,
                Complexity = 0,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_AUTO,
                FrameSize = 60,
                PacketLossPercent = 0,
                SampleRate = 48000,
                UseDTX = false,
                UseVBR = false,
                DecoderSampleRate = 24000,
                DecoderChannels = 1,
            });
        }

        [TestMethod]
        public void TestBug4()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                Bitrate = 16,
                Channels = 1,
                Complexity = 0,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_CELT_ONLY,
                FrameSize = 60,
                PacketLossPercent = 20,
                SampleRate = 12000,
                UseDTX = true,
                UseVBR = false
            });
        }

        [TestMethod]
        public void TestDecodeTo8Khz()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Channels = 1,
                Complexity = 10,
                ForceMode = Concentus.Enums.OpusMode.MODE_AUTO,
                FrameSize = 20,
                SampleRate = 48000,
                DecoderChannels = 2,
                DecoderSampleRate = 8000
            });
        }

        [TestMethod]
        public void TestDecodeTo16Khz()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Channels = 1,
                Complexity = 10,
                ForceMode = Concentus.Enums.OpusMode.MODE_AUTO,
                FrameSize = 20,
                SampleRate = 48000,
                DecoderChannels = 2,
                DecoderSampleRate = 16000
            });
        }

        [TestMethod]
        public void TestDecodeTo24Khz()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Channels = 1,
                Complexity = 10,
                ForceMode = Concentus.Enums.OpusMode.MODE_AUTO,
                FrameSize = 20,
                SampleRate = 48000,
                DecoderChannels = 2,
                DecoderSampleRate = 24000
            });
        }

        [TestMethod]
        public void TestDecodeTo8KhzMono()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Channels = 2,
                Complexity = 10,
                ForceMode = Concentus.Enums.OpusMode.MODE_AUTO,
                FrameSize = 20,
                SampleRate = 48000,
                DecoderChannels = 1,
                DecoderSampleRate = 8000
            });
        }

        [TestMethod]
        public void TestDecodeTo16KhzMono()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Channels = 2,
                Complexity = 10,
                ForceMode = Concentus.Enums.OpusMode.MODE_AUTO,
                FrameSize = 20,
                SampleRate = 48000,
                DecoderChannels = 1,
                DecoderSampleRate = 16000
            });
        }

        [TestMethod]
        public void TestDecodeTo24KhzMono()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Channels = 2,
                Complexity = 10,
                ForceMode = Concentus.Enums.OpusMode.MODE_AUTO,
                FrameSize = 20,
                SampleRate = 48000,
                DecoderChannels = 1,
                DecoderSampleRate = 24000
            });
        }

        [TestMethod]
        public void ShotgunTest()
        {
            OpusApplication[] Applications = new OpusApplication[] { OpusApplication.OPUS_APPLICATION_AUDIO, OpusApplication.OPUS_APPLICATION_VOIP, OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY };
            int[] Bitrates = new int[] { -1, 6, 16, 20, 32, 64, 500 };
            int[] Channels = new int[] { 1, 2 };
            int[] Complexities = new int[] { 0, 2, 4, 6, 8, 10 };
            int[] SampleRates = new int[] { 8000, 12000, 16000, 24000, 48000 };
            double[] FrameSizes = new double[] { 2.5, 5, 10, 20, 40, 60 };
            int[] PacketLosses = new int[] { 0, 20 };
            OpusMode[] ForceModes = new OpusMode[] { OpusMode.MODE_AUTO, OpusMode.MODE_CELT_ONLY, OpusMode.MODE_SILK_ONLY };
            bool[] DTXModes = new bool[] { false, true };
            int[] VBRModes = new int[] { 0, 1, 2 };

            IList<TestParameters> allTests = new List<TestParameters>();

            for (int app_idx = 0; app_idx < Applications.Length; app_idx++)
            {
                for (int plc_idx = 0; plc_idx < PacketLosses.Length; plc_idx++)
                {
                    for (int chan_idx = 0; chan_idx < Channels.Length; chan_idx++)
                    {
                        for (int sr_idx = 0; sr_idx < SampleRates.Length; sr_idx++)
                        {
                            for (int fs_idx = 0; fs_idx < FrameSizes.Length; fs_idx++)
                            {
                                for (int cpx_idx = 0; cpx_idx < Complexities.Length; cpx_idx++)
                                {
                                    for (int bit_idx = 0; bit_idx < Bitrates.Length; bit_idx++)
                                    {
                                        for (int fm_idx = 0; fm_idx < ForceModes.Length; fm_idx++)
                                        {
                                            for (int dtx_idx = 0; dtx_idx < DTXModes.Length; dtx_idx++)
                                            {
                                                for (int vbr_idx = 0; vbr_idx < VBRModes.Length; vbr_idx++)
                                                {
                                                    TestParameters newParams = new TestParameters()
                                                    {
                                                        Application = Applications[app_idx],
                                                        Bitrate = Bitrates[bit_idx],
                                                        Channels = Channels[chan_idx],
                                                        Complexity = Complexities[cpx_idx],
                                                        PacketLossPercent = PacketLosses[plc_idx],
                                                        SampleRate = SampleRates[sr_idx],
                                                        FrameSize = FrameSizes[fs_idx],
                                                        ForceMode = ForceModes[fm_idx],
                                                        UseDTX = DTXModes[dtx_idx]
                                                    };
                                                    if (VBRModes[vbr_idx] == 0)
                                                    {
                                                        newParams.UseVBR = false;
                                                        newParams.ConstrainedVBR = false;
                                                    }
                                                    else if (VBRModes[vbr_idx] == 1)
                                                    {
                                                        newParams.UseVBR = true;
                                                        newParams.ConstrainedVBR = false;
                                                    }
                                                    else if (VBRModes[vbr_idx] == 2)
                                                    {
                                                        newParams.UseVBR = true;
                                                        newParams.ConstrainedVBR = true;
                                                    }

                                                    // Validate params
                                                    if (newParams.Bitrate > 40 || newParams.FrameSize < 10)
                                                    {
                                                        // No FEC outside of SILK mode
                                                        if (newParams.PacketLossPercent > 0)
                                                        {
                                                            continue;
                                                        }
                                                        // No DTX outside of SILK mode
                                                        if (newParams.UseDTX)
                                                        {
                                                            continue;
                                                        }
                                                        if (newParams.ForceMode == OpusMode.MODE_SILK_ONLY)
                                                        {
                                                            continue;
                                                        }
                                                    }
                                                    // Constrained VBR only applies to CELT
                                                    if (newParams.ForceMode == OpusMode.MODE_SILK_ONLY && newParams.ConstrainedVBR)
                                                    {
                                                        continue;
                                                    }
                                                    // 12Khz + 2.5ms triggers an opus bug for now
                                                    if (newParams.SampleRate == 12000 && newParams.FrameSize < 5)
                                                    {
                                                        continue;
                                                    }

                                                    allTests.Add(newParams);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            TestParameters[] allTestsRandom = allTests.ToArray();
            int numTestCases = allTests.Count;

            // Shuffle the test list
            TestParameters temp;
            int a;
            int b;
            Random rand = new Random();
            for (int c = 0; c < numTestCases; c++)
            {
                a = rand.Next(numTestCases);
                b = rand.Next(numTestCases);
                temp = allTestsRandom[a];
                allTestsRandom[a] = allTestsRandom[b];
                allTestsRandom[b] = temp;
            }
            
            int testsRun = 0;
            foreach (TestParameters p in allTestsRandom)
            {
                testsRun++;
                Console.WriteLine("{0,5} {1} {2} Cpx={3,2} {4}Kbps {5,2}Khz {6,3} Ms PLC {7,2}% {8} {9} {10}... ",
                    testsRun,
                    PrintApplication(p.Application),
                    p.Channels == 1 ? "Mono  " : "Stereo",
                    p.Complexity,
                    p.Bitrate > 0 ? string.Format("{0,3}", p.Bitrate) : "VAR",
                    p.SampleRate / 1000,
                    p.FrameSize,
                    p.PacketLossPercent,
                    PrintVBRMode(p),
                    p.UseDTX ? "DTX" : "   ",
                    PrintForceMode(p.ForceMode));
                TestResults response = TestDriver.RunTest(p, GetTestSample(p));
                Assert.IsTrue(response.Passed, response.Message);
                if (testsRun > 50) break;
            }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 2)]
        public class NAudioStyleBuffer
        {
            [FieldOffset(0)]
            public int numberOfBytes;
            [FieldOffset(4)]
            public byte[] byteBuffer;
            [FieldOffset(4)]
            public float[] floatBuffer;
            [FieldOffset(4)]
            public short[] shortBuffer;
        }

        [TestMethod]
        public void TestInt16ArrayCastingFromNAudio()
        {
            TestParameters encodeParams = new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = 12,
                Channels = 2,
                Complexity = 10,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_SILK_ONLY,
                FrameSize = 20,
                PacketLossPercent = 0,
                SampleRate = 48000,
                UseDTX = true,
                UseVBR = true
            };

            short[] inputAudio = GetTestSample(encodeParams);
            byte[] encodedScratch = new byte[1500];

            OpusEncoder encoder = new OpusEncoder(encodeParams.SampleRate, encodeParams.Channels, encodeParams.Application);
            OpusDecoder decoder = new OpusDecoder(encodeParams.DecoderSampleRate, encodeParams.DecoderChannels);

            int samplesPerChannelPerInputPacket = (int)Math.Round(encodeParams.SampleRate * encodeParams.FrameSize / 1000);
            int samplesPerInputPacket = samplesPerChannelPerInputPacket * encodeParams.Channels;
            int samplesPerChannelPerOutputPacket = (int)Math.Round(encodeParams.DecoderSampleRate * encodeParams.FrameSize / 1000);
            int samplesPerOutputPacket = samplesPerChannelPerOutputPacket * encodeParams.DecoderChannels;

            NAudioStyleBuffer fakeBuffer = new NAudioStyleBuffer()
            {
                numberOfBytes = samplesPerChannelPerOutputPacket * sizeof(short) * encodeParams.Channels,
                byteBuffer = new byte[samplesPerChannelPerOutputPacket * sizeof(short) * encodeParams.Channels],
            };

            for (int inIdx = 0; inIdx <= inputAudio.Length - samplesPerInputPacket; inIdx += samplesPerInputPacket)
            {
                int encodeResult = encoder.Encode(inputAudio, inIdx, 480, encodedScratch, 0, encodedScratch.Length);
                Assert.IsTrue(encodeResult > 0);
                int decodeResult = decoder.Decode(encodedScratch, 0, encodeResult, fakeBuffer.shortBuffer, 0, samplesPerChannelPerOutputPacket);
                Assert.IsTrue(decodeResult > 0);
            }
        }

        private static string PrintApplication(OpusApplication app)
        {
            if (app == OpusApplication.OPUS_APPLICATION_AUDIO)
                return "Music   ";
            else if (app == OpusApplication.OPUS_APPLICATION_VOIP)
                return "Voip    ";
            return "LowDelay";
        }

        private static string PrintVBRMode(TestParameters p)
        {
            if (p.UseVBR)
            {
                if (p.ConstrainedVBR)
                    return "CVBR";
                else
                    return "VBR ";
            }
            return "    ";
        }

        private static string PrintForceMode(OpusMode mode)
        {
            if (mode == OpusMode.MODE_CELT_ONLY)
                return "CELT";
            if (mode == OpusMode.MODE_SILK_ONLY)
                return "SILK";
            return "    ";
        }

    }
}
