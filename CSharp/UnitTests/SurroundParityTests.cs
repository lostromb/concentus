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
    public class SurroundParityTests
    {
        private static short[] FivePointOneTestSample;

        private static void LoadTestFile()
        {
            string fileName = "48Khz 5.1.raw";
            byte[] file = File.ReadAllBytes(fileName);
            short[] samples = TestDriver.BytesToShorts(file);
            FivePointOneTestSample = samples;
        }

        [ClassInitialize]
        public static void TestInitialize(TestContext context)
        {
            LoadTestFile();
        }

        private void RunParityTest(TestParameters p)
        {
            TestResults results = TestDriver.RunSurroundFivePointOneTest(p, FivePointOneTestSample);
            Assert.IsTrue(results.Passed);
        }

        [TestMethod]
        public void TestSurroundSilkBasic()
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
                SampleRate = 48000,
                UseDTX = false,
                UseVBR = true
            });
        }

        [TestMethod]
        public void TestSurroundCeltBasic()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = 64,
                Complexity = 10,
                ConstrainedVBR = false,
                ForceMode = Concentus.Enums.OpusMode.MODE_CELT_ONLY,
                FrameSize = 20,
                UseVBR = true
            });
        }

        [TestMethod]
        public void TestSurroundCeltVBR()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Complexity = 10,
                ConstrainedVBR = true,
                ForceMode = Concentus.Enums.OpusMode.MODE_CELT_ONLY,
                FrameSize = 20,
                UseVBR = true
            });
        }

        [TestMethod]
        public void TestSurroundBitrateTransitions()
        {
            RunParityTest(new TestParameters()
            {
                Application = Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                Bitrate = -1,
                Complexity = 10,
                ConstrainedVBR = true,
                ForceMode = Concentus.Enums.OpusMode.MODE_AUTO,
                FrameSize = 20,
                UseVBR = true
            });
        }

        [TestMethod]
        public void SurroundShotgunTest()
        {
            OpusApplication[] Applications = new OpusApplication[] { OpusApplication.OPUS_APPLICATION_AUDIO, OpusApplication.OPUS_APPLICATION_VOIP };
            int[] Bitrates = new int[] { -1, 6, 16, 20, 32, 64, 500 };
            int[] Complexities = new int[] { 0, 2, 4, 6, 8, 10 };
            double[] FrameSizes = new double[] { 5, 10, 20, 40, 60 };
            OpusMode[] ForceModes = new OpusMode[] { OpusMode.MODE_AUTO, OpusMode.MODE_CELT_ONLY, OpusMode.MODE_SILK_ONLY };
            int[] VBRModes = new int[] { 0, 1, 2 };

            IList<TestParameters> allTests = new List<TestParameters>();

            for (int app_idx = 0; app_idx < Applications.Length; app_idx++)
            {
                for (int fs_idx = 0; fs_idx < FrameSizes.Length; fs_idx++)
                {
                    for (int cpx_idx = 0; cpx_idx < Complexities.Length; cpx_idx++)
                    {
                        for (int bit_idx = 0; bit_idx < Bitrates.Length; bit_idx++)
                        {
                            for (int fm_idx = 0; fm_idx < ForceModes.Length; fm_idx++)
                            {
                                for (int vbr_idx = 0; vbr_idx < VBRModes.Length; vbr_idx++)
                                {
                                    TestParameters newParams = new TestParameters()
                                    {
                                        Application = Applications[app_idx],
                                        Bitrate = Bitrates[bit_idx],
                                        Complexity = Complexities[cpx_idx],
                                        FrameSize = FrameSizes[fs_idx],
                                        ForceMode = ForceModes[fm_idx],
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
                                    // Low bitrate + CBR + tiny framesize is dumb and breaks the native code also
                                    if (newParams.FrameSize <= 5 && !newParams.UseVBR && newParams.Bitrate < 40)
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
                Console.WriteLine("{0,5} {1} {2} Cpx={3,2} {4}Kbps {5,2}Khz {6,3} Ms {7} {8}... ",
                    testsRun,
                    PrintApplication(p.Application),
                    "5.1",
                    p.Complexity,
                    p.Bitrate > 0 ? string.Format("{0,3}", p.Bitrate) : "VAR",
                    p.SampleRate / 1000,
                    p.FrameSize,
                    PrintVBRMode(p),
                    PrintForceMode(p.ForceMode));

                TestResults response = TestDriver.RunSurroundFivePointOneTest(p, FivePointOneTestSample);
                Assert.IsTrue(response.Passed, response.Message);
                if (testsRun > 50) break;
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
