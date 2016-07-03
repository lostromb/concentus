using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParityTest
{
    public class Program
    {
        private static IDictionary<string, short[]> testSamples = new Dictionary<string, short[]>();

        public static void Main(string[] args)
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

            OpusApplication[] Applications = new OpusApplication[] { OpusApplication.OPUS_APPLICATION_AUDIO, OpusApplication.OPUS_APPLICATION_VOIP, OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY };
            int[] Bitrates = new int[] { -1, 6, 16, 20, 32, 64 };
            int[] Channels = new int[] { 1, 2 };
            int[] Complexities = new int[] { 0, 5, 10 };
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

            Console.WriteLine("Preparing " + numTestCases + " test cases");

            // Shuffle the test list
            if (true)
            {
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
            }

            double concentusTime = 0.001;
            double opusTime = 0;
            int passedTests = 0;
            int testsRun = 0;

            foreach (TestParameters p in allTestsRandom)
            {
                testsRun++;
                Console.Write("{0,5} {1} {2} Cpx={3,2} {4}Kbps {5,2}Khz {6,3} Ms PLC {7,2}% {8} {9} {10}... ",
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

                if (response.Passed)
                {
                    passedTests++;
                    if (passedTests > 7)
                    {
                        concentusTime *= 0.999;
                        opusTime *= 0.999;
                        concentusTime += response.ConcentusTimeMs;
                        opusTime += response.OpusTimeMs;
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("{0} (Speed {1:F2}% Pass {2:F2}%)", response.Message, (opusTime * 100 / concentusTime), ((double)passedTests * 100 / testsRun));
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("FAIL: " + response.Message);
                    Console.ForegroundColor = ConsoleColor.Gray;

                    //if (response.FrameCount == 0)
                    //{
                    //    PrintShortArray(response.FailureFrame);
                    //    Console.WriteLine(response.FrameLength);
                    //    Console.ReadLine();
                    //}
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("All tests FINISHED");
            Console.WriteLine("{0} out of {1} tests passed ({2:F2}%)", passedTests, numTestCases, ((double)passedTests * 100 / numTestCases));
            Console.WriteLine("Speed benchmark was {0:F2}%", (opusTime * 100 / concentusTime));
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

        private static void PrintShortArray(short[] array)
        {
            Console.Write("new short[] { ");
            int col = 0;
            for (int c = 0; c < array.Length; c++)
            {
                Console.Write("{0}", array[c]);
                if (c != (array.Length - 1))
                {
                    Console.Write(",");
                }
                if (++col > 16)
                {
                    Console.Write("\n");
                    col = 0;
                }
            }

            Console.Write("}\n");
        }

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
    }
}
