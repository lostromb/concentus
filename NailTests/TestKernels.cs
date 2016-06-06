using Concentus.Common.CPlusPlus;
using Concentus.Silk;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NailTests
{
    [TestClass]
    public class TestKernels
    {
        [TestMethod]
        public void Test_K2A()
        {
            Pointer<float> rc = Helpers.WrapWithArrayPointer<float>(Helpers.ConvertBytesToFloatArray(new uint[] {
                0xbf5f57e8U, 0x3e88ca43U, 0xbd844ba5U, 0x3d1448c6U, 0x3b2d2bf1U,
                0x3c1c006eU}));
            int order = 6;

            Pointer<float> A = Pointer.Malloc<float>(6);

            k2a.silk_k2a_FLP(A, rc, order);

            float[] expectedAOut = Helpers.ConvertBytesToFloatArray(new uint[] {
                0x3f8ffff0U, 0xbeb3a63eU, 0x3dd7ad2aU, 0xbd15c766U, 0x3c04366dU,
                0xbc1c006eU});

            Helpers.AssertArrayDataEquals<float>(expectedAOut, A);
        }
    }
}
