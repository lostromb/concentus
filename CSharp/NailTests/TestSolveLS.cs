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
    public class TestSolveLS
    {
        [TestMethod]
        public void Test_silk_LDL_FLP()
        {
            Pointer<float> A = Helpers.WrapWithArrayPointer<float>(Helpers.ConvertBytesToFloatArray(new uint[] {
                0x4dcf5d25U, 0x4a834347U, 0xc9536792U, 0xca85fafaU, 0xca1f9614U,
                0x4a834347U, 0x4dcf3b65U, 0x4a779240U, 0xc99a8b36U, 0xca8d1bfbU,
                0xc9536792U, 0x4a779240U, 0x4dcf214aU, 0x4a61b593U, 0xc9b3994bU,
                0xca85fafaU, 0xc99a8b36U, 0x4a61b593U, 0x4dcedb18U, 0x4a4cf509U,
                0xca1f9614U, 0xca8d1bfbU, 0xc9b3994bU, 0x4a4cf509U, 0x4dcec31dU
                }));

            int M = 5;

            Pointer<float> b = Helpers.WrapWithArrayPointer<float>(Helpers.ConvertBytesToFloatArray(new uint[] {
                0xc9ba2123U, 0x4a80cb31U, 0x4dc5e11cU, 0x4a4fe117U, 0xca3a16daU,
                0xccccccccU, 0xccccccccU, 0x4b9e3107U, 0xccccccccU, 0xccccccccU,
                0xccccccccU, 0xccccccccU, 0xccccccccU, 0xccccccccU, 0xccccccccU,
                0xccccccccU, 0xccccccccU, 0xccccccccU, 0xccccccccU, 0xccccccccU,
                0xccccccccU, 0xccccccccU, 0xccccccccU, 0xccccccccU, 0xccccccccU
                }));


            Pointer<float> x_out = Pointer.Malloc<float>(5);
            
            solve_LS.silk_solve_LDL_FLP(A, M, b, x_out);

            float[] expectedXOut = Helpers.ConvertBytesToFloatArray(new uint[] {
                0xbad69845U, 0x3a49c660U, 0x3f748fb4U, 0xb9917cbbU, 0xbb785826U,
                });

            Helpers.AssertArrayDataEquals(expectedXOut, x_out);
        }
    }
}
