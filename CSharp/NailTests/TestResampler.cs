using Concentus.Common.CPlusPlus;
using Concentus.Silk;
using Concentus.Silk.Structs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NailTests
{
    [TestClass]
    public class TestResampler
    {
        private const int TEST_AUDIO_LENGTH = 6000;

        [TestMethod]
        public void Test_Resampler_Downsample48to16()
        {
            short[] data = Resample(48000, 16000, 1);
            Assert.AreEqual(2000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Downsample48to12()
        {
            short[] data = Resample(48000, 12000, 1);
            Assert.AreEqual(1500, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Downsample48to8()
        {
            short[] data = Resample(48000, 8000, 1);
            Assert.AreEqual(1000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Downsample24to16()
        {
            short[] data = Resample(24000, 16000, 1);
            Assert.AreEqual(4000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Downsample24to12()
        {
            short[] data = Resample(24000, 12000, 1);
            Assert.AreEqual(3000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Downsample24to8()
        {
            short[] data = Resample(24000, 8000, 1);
            Assert.AreEqual(2000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Downsample16to12()
        {
            short[] data = Resample(16000, 12000, 1);
            Assert.AreEqual(4500, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Downsample16to8()
        {
            short[] data = Resample(16000, 8000, 1);
            Assert.AreEqual(3000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Upsample8to12()
        {
            short[] data = Resample(8000, 12000, 0);
            Assert.AreEqual(9000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Upsample8to16()
        {
            short[] data = Resample(8000, 16000, 0);
            Assert.AreEqual(12000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Upsample8to24()
        {
            short[] data = Resample(8000, 24000, 0);
            Assert.AreEqual(18000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Upsample8to48()
        {
            short[] data = Resample(8000, 48000, 0);
            Assert.AreEqual(36000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Upsample12to16()
        {
            short[] data = Resample(12000, 16000, 0);
            Assert.AreEqual(8000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Upsample12to24()
        {
            short[] data = Resample(12000, 24000, 0);
            Assert.AreEqual(12000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Upsample12to48()
        {
            short[] data = Resample(12000, 48000, 0);
            Assert.AreEqual(24000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Upsample16to24()
        {
            short[] data = Resample(16000, 24000, 0);
            Assert.AreEqual(9000, data.Length);
        }

        [TestMethod]
        public void Test_Resampler_Upsample16to48()
        {
            short[] data = Resample(16000, 48000, 0);
            Assert.AreEqual(18000, data.Length);
        }

        private static short[] Resample(int inFreq, int outFreq, int forEncode)
        {
            int inDataLength = TEST_AUDIO_LENGTH;
            int expectedDataLength = inDataLength * outFreq / inFreq;

            short[] audio = new short[inDataLength];
            for (int c = 0; c < inDataLength; c++)
            {
                audio[c] = (short)(Math.Sin(c) * 10000);
            }
            
            silk_resampler_state_struct resamplerState = new silk_resampler_state_struct();
            Resampler.silk_resampler_init(resamplerState, inFreq, outFreq, forEncode);
            Pointer<short> inSamples = audio.GetPointer();
            Pointer<short> outSamples = Pointer.Malloc<short>(expectedDataLength);
            Resampler.silk_resampler(resamplerState, outSamples, inSamples, inDataLength);
            short[] returnVal = new short[expectedDataLength];
            outSamples.MemCopyTo(returnVal, 0, returnVal.Length);
            return returnVal;
        }
    }
}
