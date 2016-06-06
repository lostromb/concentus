using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Concentus.Silk;
using Concentus.Common;
using Concentus.Common.CPlusPlus;

namespace NailTests
{
    [TestClass]
    public class TestGainQuant
    {
        [TestMethod]
        public void Test_silk_gains_quant_0()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 0, 0, 0, 0 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 195362816, 524877824, 14417920, 12845056 });
            sbyte in_prev_ind = 10;
            int in_conditional = 0;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 49, 10, 0, 0 };
            int[] expected_gain_Q16 =
            new int[] { 185597952, 480247808, 253755392, 135266304 };
            sbyte expected_prev_ind = 47;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_1()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 49, 10, 0, 0 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 11469312, 11005952, 11927552, 11333120 });
            sbyte in_prev_ind = 47;
            int in_conditional = 1;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 0, 0, 0, 1 };
            int[] expected_gain_Q16 =
            new int[] { 71827456, 38273024, 20316160, 12713984 };
            sbyte expected_prev_ind = 32;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_2()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 0, 0, 0, 1 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 10842368, 11387648, 11862016, 11251200 });
            sbyte in_prev_ind = 32;
            int in_conditional = 0;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 31, 4, 4, 4 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 10813440 };
            sbyte expected_prev_ind = 31;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_3()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 31, 4, 4, 4 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 10433280, 11469312, 10269696, 10896896 });
            sbyte in_prev_ind = 31;
            int in_conditional = 1;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 4, 4, 4, 4 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 10813440 };
            sbyte expected_prev_ind = 31;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_4()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 4, 4, 4, 4 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 10896896, 10924032, 10787840, 10896896 });
            sbyte in_prev_ind = 31;
            int in_conditional = 0;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 31, 4, 4, 4 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 10813440 };
            sbyte expected_prev_ind = 31;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_5()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 31, 4, 4, 4 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 10924032, 10515200, 11442176, 11060480 });
            sbyte in_prev_ind = 31;
            int in_conditional = 1;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 4, 4, 4, 4 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 10813440 };
            sbyte expected_prev_ind = 31;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_6()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 4, 4, 4, 4 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 10351616, 10188032, 9206528, 11033088 });
            sbyte in_prev_ind = 31;
            int in_conditional = 0;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 31, 4, 3, 5 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 9240576, 10813440 };
            sbyte expected_prev_ind = 31;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_7()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 31, 4, 3, 5 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 10869504, 10569728, 11605760, 10324224 });
            sbyte in_prev_ind = 31;
            int in_conditional = 1;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 4, 4, 4, 4 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 10813440 };
            sbyte expected_prev_ind = 31;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_8()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 4, 4, 4, 4 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 9724416, 10760448, 10924032, 10188032 });
            sbyte in_prev_ind = 31;
            int in_conditional = 0;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 31, 4, 4, 4 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 10813440 };
            sbyte expected_prev_ind = 31;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_9()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 31, 4, 4, 4 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 9288192, 9560832, 9778944, 9424640 });
            sbyte in_prev_ind = 31;
            int in_conditional = 1;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 3, 4, 4, 4 };
            int[] expected_gain_Q16 =
            new int[] { 9240576, 9240576, 9240576, 9240576 };
            sbyte expected_prev_ind = 30;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_10()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 3, 4, 4, 4 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 11796480, 11796480, 11442176, 12255232 });
            sbyte in_prev_ind = 30;
            int in_conditional = 0;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 31, 4, 4, 4 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 10813440 };
            sbyte expected_prev_ind = 31;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_11()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 31, 4, 4, 4 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 11087616, 10651392, 9860864, 11333120 });
            sbyte in_prev_ind = 31;
            int in_conditional = 1;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 4, 4, 4, 4 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 10813440 };
            sbyte expected_prev_ind = 31;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_12()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 4, 4, 4, 4 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 11251200, 12255232, 9533696, 8688384 });
            sbyte in_prev_ind = 31;
            int in_conditional = 0;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 31, 4, 4, 3 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 9240576 };
            sbyte expected_prev_ind = 30;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_13()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 31, 4, 4, 3 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 11169536, 10869504, 9860864, 10651392 });
            sbyte in_prev_ind = 30;
            int in_conditional = 1;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 5, 4, 4, 4 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 10813440 };
            sbyte expected_prev_ind = 31;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_14()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 5, 4, 4, 4 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 10487808, 9560832, 9997056, 129695744 });
            sbyte in_prev_ind = 31;
            int in_conditional = 0;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 31, 4, 4, 19 };
            int[] expected_gain_Q16 =
            new int[] { 10813440, 10813440, 10813440, 115867648 };
            sbyte expected_prev_ind = 46;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }

        [TestMethod]
        public void Test_silk_gains_quant_15()
        {
            Pointer<sbyte> in_ind = Helpers.WrapWithArrayPointer<sbyte>(
            new sbyte[] { 31, 4, 4, 19 });
            Pointer<int> in_gain_Q16 = Helpers.WrapWithArrayPointer<int>(
            new int[] { 473235456, 440008704, 270991360, 462159872 });
            sbyte in_prev_ind = 46;
            int in_conditional = 1;
            int in_nb_subfr = 4;
            BoxedValue<sbyte> through_prev_ind = new BoxedValue<sbyte>(in_prev_ind);
            GainQuantization.silk_gains_quant(in_ind, in_gain_Q16, through_prev_ind, in_conditional, in_nb_subfr);
            sbyte[] expected_ind =
            new sbyte[] { 12, 4, 2, 6 };
            int[] expected_gain_Q16 =
            new int[] { 406847488, 406847488, 295698432, 406847488 };
            sbyte expected_prev_ind = 54;
            Assert.AreEqual(expected_prev_ind, through_prev_ind.Val);
            Helpers.AssertArrayDataEquals(expected_ind, in_ind);
            Helpers.AssertArrayDataEquals(expected_gain_Q16, in_gain_Q16);
        }
    }
}
