using Concentus.Opus.Enums;
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
            LoadTestFile(16, false);
            LoadTestFile(48, false);
            LoadTestFile(8, true);
            LoadTestFile(16, true);
            LoadTestFile(48, true);

            int[] Applications = new int[] { OpusApplication.OPUS_APPLICATION_AUDIO, OpusApplication.OPUS_APPLICATION_VOIP, OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY };
            int[] Bitrates = new int[] { 8, 16, 32, 64, 256 };
            int[] Channels = new int[] { 1, 2 };
            int[] Complexities = new int[] { 0, 5, 10 };
            int[] SampleRates = new int[] { 8000, 16000, 48000 };
            double[] FrameSizes = new double[] { 20, 60 };

            IList<TestParameters> allTests = new List<TestParameters>();

            for (int app_idx = 0; app_idx < Applications.Length; app_idx++)
            {
                for (int bit_idx = 0; bit_idx < Bitrates.Length; bit_idx++)
                {
                    for (int chan_idx = 0; chan_idx < Channels.Length; chan_idx++)
                    {
                        for (int cpx_idx = 0; cpx_idx < Complexities.Length; cpx_idx++)
                        {
                            for (int sr_idx = 0; sr_idx < SampleRates.Length; sr_idx++)
                            {
                                for (int fs_idx = 0; fs_idx < FrameSizes.Length; fs_idx++)
                                {
                                    allTests.Add(new TestParameters()
                                    {
                                        Application = Applications[app_idx],
                                        Bitrate = Bitrates[bit_idx],
                                        Channels = Channels[chan_idx],
                                        Complexity = Complexities[cpx_idx],
                                        PacketLossPercent = 0,
                                        SampleRate = SampleRates[sr_idx],
                                        FrameSize = FrameSizes[fs_idx]
                                    });
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

            foreach (TestParameters p in allTestsRandom)
            {
                Console.Write("{0}\t{1}\tCpx={2}\t{3}Kbps\t{4}Khz\t{5}Ms\tPLC {6}% ... ",
                    PrintApplication(p.Application),
                    p.Channels == 1 ? "Mono  " : "Stereo",
                    p.Complexity,
                    p.Bitrate,
                    p.SampleRate / 1000,
                    p.FrameSize,
                    p.PacketLossPercent);

                string response = TestDriver.RunTest(p, GetTestSample(p));

                if (string.IsNullOrEmpty(response))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Ok!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("FAIL: " + response);
                }
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        private static string PrintApplication(int app)
        {
            if (app == OpusApplication.OPUS_APPLICATION_AUDIO)
                return "Music   ";
            else if (app == OpusApplication.OPUS_APPLICATION_VOIP)
                return "Voip    ";
            else if (app == OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY)
                return "LowDelay";
            return "???";
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
