using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Concentus.Common.CPlusPlus;

namespace NailTests
{
    [TestClass]
    public class TestPointers
    {
        [TestMethod]
        public void Test_Iteration()
        {
            Pointer<int> p = new Pointer<int>(new int[] { 1, 2, 3, 4, 5 }, 1);
            Iterate(p);
            Assert.AreEqual(2, p[0]);
        }

        [TestMethod]
        public void Test_Repointing()
        {
            Pointer<int> p = new Pointer<int>(new int[] { 1, 2, 3, 4, 5 }, 1);
            Repoint(p);
            Assert.AreEqual(2, p[0]);
        }

        [TestMethod]
        public void Test_memmove_forward()
        {
            int[] array = new int[] { 1, 2, 3, 4, 5 };
            Pointer<int> p = new Pointer<int>(array, 0);
            p.MemMove(1, 4);
            int[] expectedOutput = { 1, 1, 2, 3, 4 };

            for (int c = 0; c < expectedOutput.Length; c++)
            {
                Assert.AreEqual(expectedOutput[c], array[c]);
            }
        }

        [TestMethod]
        public void Test_memmove_reverse()
        {
            int[] array = new int[] { 1, 2, 3, 4, 5 };
            Pointer<int> p = new Pointer<int>(array, 2);
            p.MemMove(-2, 3);
            int[] expectedOutput = { 3, 4, 5, 4, 5 };

            for (int c = 0; c < expectedOutput.Length; c++)
            {
                Assert.AreEqual(expectedOutput[c], array[c]);
            }
        }

        private void Iterate(Pointer<int> p)
        {
            int x;
            p = p.Iterate(out x);
            p = p.Iterate(out x);
            p = p.Iterate(out x);
        }

        private void Repoint(Pointer<int> p)
        {
            p = p.Point(3);
        }
    }
}
