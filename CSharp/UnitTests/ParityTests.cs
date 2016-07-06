using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using ParityTest;

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
    }
}
