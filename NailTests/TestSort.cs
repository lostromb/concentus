using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Concentus.Silk;
using Concentus.Common.CPlusPlus;

namespace NailTests
{
    [TestClass]
    public class TestSort
    {
        [TestMethod]
        public void Test_silk_insertion_sort_increasing()
        {
            Pointer<int> a = Helpers.WrapWithArrayPointer(new int[] { 711785, 1646393, 98617, 2896969, 188569, 1527977, 4518841, 2811081, 1923049, 2791049, 141849, 3825193, 2633161, 3465961, 2761849, 3667289, 3415769, 1083209, 1720201, 3408489, 668089, 1044057, 1860601, 717929, 640825, 4932649, 1193945, 730361, 1694777, 1440265, 1087833, 1166201, });
            Pointer<int> indices = Pointer.Malloc<int>(100);
            Sort.silk_insertion_sort_increasing(a, indices, 32, 8);

            int[] expectedOutput = { 98617, 141849, 188569, 640825, 668089, 711785, 717929, 730361, 1923049, 2791049, 141849, 3825193, 2633161, 3465961, 2761849, 3667289, 3415769, 1083209, 1720201, 3408489, 668089, 1044057, 1860601, 717929, 640825, 4932649, 1193945, 730361, 1694777, 1440265, 1087833, 1166201, };
            Helpers.AssertArrayDataEquals(expectedOutput, a);
        }
    }
}
