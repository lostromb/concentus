using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Concentus.Silk;
using Concentus.Common;

namespace NailTests
{
    [TestClass]
    public class TestInlines
    {
        [TestMethod]
        public void Test_BSR()
        {
            ulong output;
            ulong mask = 12345;
            byte returnVal = Inlines.BitScanReverse(out output, mask);
            Assert.AreEqual(1, returnVal);
            Assert.AreEqual(13UL, output);

            mask = 99999;
            returnVal = Inlines.BitScanReverse(out output, mask);
            Assert.AreEqual(1, returnVal);
            Assert.AreEqual(16UL, output);

            mask = 0;
            returnVal = Inlines.BitScanReverse(out output, mask);
            Assert.AreEqual(0, returnVal);

            mask = 0x80000000;
            returnVal = Inlines.BitScanReverse(out output, mask);
            Assert.AreEqual(1, returnVal);
            Assert.AreEqual(31UL, output);

            mask = 0x40000000;
            returnVal = Inlines.BitScanReverse(out output, mask);
            Assert.AreEqual(1, returnVal);
            Assert.AreEqual(30UL, output);

            mask = 0x100000000;
            returnVal = Inlines.BitScanReverse(out output, mask);
            Assert.AreEqual(0, returnVal);
        }

        [TestMethod]
        public void Test_CLZ32()
        {
            Assert.AreEqual(15, Inlines.silk_CLZ32(99999));
            Assert.AreEqual(18, Inlines.silk_CLZ32(12345));
        }

        [TestMethod]
        public void Test_SQRT_APPROX()
        {
            Assert.AreEqual(2, Inlines.silk_SQRT_APPROX(10));
            Assert.AreEqual(5, Inlines.silk_SQRT_APPROX(40));
            Assert.AreEqual(30, Inlines.silk_SQRT_APPROX(1000));
            Assert.AreEqual(242, Inlines.silk_SQRT_APPROX(60000));
            Assert.AreEqual(311, Inlines.silk_SQRT_APPROX(100000));
        }

        [TestMethod]
        public void Test_MLA_ovflw()
        {
            Assert.AreEqual(145, Inlines.silk_MLA_ovflw(10, 15, 9));
            Assert.AreEqual(1260489494, Inlines.silk_MLA_ovflw(12345, 55555, 99999));
            Assert.AreEqual(1409965408, Inlines.silk_MLA_ovflw(99999, 99999, 99999));
            Assert.AreEqual(1774919424, Inlines.silk_MLA_ovflw(99999999, 99999999, 99999999));
        }

        [TestMethod]
        public void Test_ROR32()
        {
            Assert.AreEqual(0x00FFFF00, Inlines.silk_ROR32(0x00FFFF00, 0));
            Assert.AreEqual(0x02000000, Inlines.silk_ROR32(0x40000000, 5));
            Assert.AreEqual(0x00100000, Inlines.silk_ROR32(0x40000000, 10));
            Assert.AreEqual(0x40000000, Inlines.silk_ROR32(0x40000000, 32));
            Assert.AreEqual(0xffffffff, Inlines.silk_ROR32(0xFFFFFFFF, 4));
            Assert.AreEqual(0xffffffff, Inlines.silk_ROR32(0xFFFFFFFF, -8));
            Assert.AreEqual(-2036400127, Inlines.silk_ROR32(99999, -16));
        }

        [TestMethod]
        public void Test_SMLATT()
        {
            Assert.AreEqual(10, Inlines.silk_SMLATT(10, 15, 9));
            Assert.AreEqual(12345, Inlines.silk_SMLATT(12345, 55555, 99999));
            Assert.AreEqual(100000, Inlines.silk_SMLATT(99999, 99999, 99999));
            Assert.AreEqual(102325624, Inlines.silk_SMLATT(99999999, 99999999, 99999999));
            Assert.AreEqual(-99995, Inlines.silk_SMLATT(-99999, -99999, -99999));
        }

        [TestMethod]
        public void Test_SMLALBB()
        {
            Assert.AreEqual(145, Inlines.silk_SMLALBB(10, 15, 9));
            Assert.AreEqual(246900, Inlines.silk_SMLALBB(12345, 19, 12345));
            Assert.AreEqual(285174, Inlines.silk_SMLALBB(99999, 12345, 15));
            Assert.AreEqual(10000000086414L, Inlines.silk_SMLALBB(9999999999999L, 7, 12345));
            Assert.AreEqual(2741319, Inlines.silk_SMLALBB(99999L, 345, 7656));
            Assert.AreEqual(56342288102, Inlines.silk_SMLALBB(56342342345L, 123, -441));
            Assert.AreEqual(-21739070, Inlines.silk_SMLALBB(-4534L, -17656, 1231));
            Assert.AreEqual(-15129, Inlines.silk_SMLALBB(0L, -123, 123));
            Assert.AreEqual(3474, Inlines.silk_SMLALBB(3411L, 9, 7));
        }

        [TestMethod]
        public void Test_SMLABB_ovflw()
        {
            Assert.AreEqual(145, Inlines.silk_SMLABB_ovflw(10, 15, 9));
            Assert.AreEqual(-222210, Inlines.silk_SMLABB_ovflw(12345, 19, -12345));
            Assert.AreEqual(-85176, Inlines.silk_SMLABB_ovflw(99999, -12345, 15));
            Assert.AreEqual(-913584, Inlines.silk_SMLABB_ovflw(-999999, 7, 12345));
        }

        [TestMethod]
        public void Test_SMLABB()
        {
            Assert.AreEqual(145, Inlines.silk_SMLABB(10, 15, 9));
            Assert.AreEqual(246900, Inlines.silk_SMLABB(12345, 19, 12345));
            Assert.AreEqual(285174, Inlines.silk_SMLABB(99999, 12345, 15));
            Assert.AreEqual(-99913584, Inlines.silk_SMLABB(-99999999, 7, 12345));
            Assert.AreEqual(2741319, Inlines.silk_SMLABB(99999, 345, 7656));
            Assert.AreEqual(563369180, Inlines.silk_SMLABB(563423423, 123, -441));
            Assert.AreEqual(-21739070, Inlines.silk_SMLABB(-4534, -17656, 1231));
            Assert.AreEqual(-15129, Inlines.silk_SMLABB(0, -123, 123));
            Assert.AreEqual(3474, Inlines.silk_SMLABB(3411, 9, 7));
        }

        [TestMethod]
        public void Test_MLA()
        {
            //Assert.AreEqual(1409965408, Inlines.silk_MLA(99999, 99999, 99999));
            Assert.AreEqual(145368, Inlines.silk_MLA(56345, 89023, 1));
            //Assert.AreEqual(1573674194, Inlines.silk_MLA(-454, -12677656, 1231));
            Assert.AreEqual(-15129, Inlines.silk_MLA(0, -123, 123));
            //Assert.AreEqual(195941403, Inlines.silk_MLA(34, 999999, 111111111));
        }

        [TestMethod]
        public void Test_SMULBB()
        {
            Assert.AreEqual(965531329, Inlines.silk_SMULBB(99999, 99999));
            Assert.AreEqual(-7564193, Inlines.silk_SMULBB(56345, 823));
            Assert.AreEqual(-570782736, Inlines.silk_SMULBB(-45994, -12677656));
            Assert.AreEqual(0, Inlines.silk_SMULBB(0, -123));
            Assert.AreEqual(576606, Inlines.silk_SMULBB(34, 999999));
        }

        [TestMethod]
        public void Test_SMULWB()
        {
            Assert.AreEqual(0, Inlines.silk_SMULWB(0, -24290));
            Assert.AreEqual(-3763194, Inlines.silk_SMULWB(113442816, -2174));
            Assert.AreEqual(-4217955, Inlines.silk_SMULWB(132579328, -2085));
            Assert.AreEqual(2863801, Inlines.silk_SMULWB(-86330303, -2174));
            Assert.AreEqual(2746561, Inlines.silk_SMULWB(-86330303, -2085));
            Assert.AreEqual(-3508737, Inlines.silk_SMULWB(113442816, -2027));
            Assert.AreEqual(-3997448, Inlines.silk_SMULWB(132579328, -1976));
            Assert.AreEqual(-2919515, Inlines.silk_SMULWB(94392351, -2027));
            Assert.AreEqual(-2846059, Inlines.silk_SMULWB(94392351, -1976));
            Assert.AreEqual(-2570535, Inlines.silk_SMULWB(113442816, -1485));
            Assert.AreEqual(-3653538, Inlines.silk_SMULWB(132579328, -1806));
            Assert.AreEqual(1036420, Inlines.silk_SMULWB(-45739303, -1485));
            Assert.AreEqual(1260455, Inlines.silk_SMULWB(-45739303, -1806));
            Assert.AreEqual(-1601175, Inlines.silk_SMULWB(113442816, -925));
            Assert.AreEqual(-2872660, Inlines.silk_SMULWB(132579328, -1420));
            Assert.AreEqual(129060, Inlines.silk_SMULWB(-9143925, -925));
            Assert.AreEqual(198125, Inlines.silk_SMULWB(-9143925, -1420));
            Assert.AreEqual(-773757, Inlines.silk_SMULWB(113442816, -447));
            Assert.AreEqual(-2116058, Inlines.silk_SMULWB(132579328, -1046));
            Assert.AreEqual(-157974, Inlines.silk_SMULWB(23160904, -447));
            Assert.AreEqual(-369665, Inlines.silk_SMULWB(23160904, -1046));
            Assert.AreEqual(62316, Inlines.silk_SMULWB(113442816, 36));
            Assert.AreEqual(-1919827, Inlines.silk_SMULWB(132579328, -949));
            Assert.AreEqual(-656, Inlines.silk_SMULWB(-1193543, 36));
            Assert.AreEqual(17283, Inlines.silk_SMULWB(-1193543, -949));
            Assert.AreEqual(962436, Inlines.silk_SMULWB(113442816, 556));
            Assert.AreEqual(-2126173, Inlines.silk_SMULWB(132579328, -1051));
            Assert.AreEqual(-103841, Inlines.silk_SMULWB(-12239783, 556));
            Assert.AreEqual(196289, Inlines.silk_SMULWB(-12239783, -1051));
            Assert.AreEqual(1505970, Inlines.silk_SMULWB(113442816, 870));
            Assert.AreEqual(-1877344, Inlines.silk_SMULWB(132579328, -928));
            Assert.AreEqual(8069, Inlines.silk_SMULWB(607884, 870));
            Assert.AreEqual(-8608, Inlines.silk_SMULWB(607884, -928));
            Assert.AreEqual(1706766, Inlines.silk_SMULWB(113442816, 986));
            Assert.AreEqual(-788970, Inlines.silk_SMULWB(132579328, -390));
            Assert.AreEqual(174123, Inlines.silk_SMULWB(11573411, 986));
            Assert.AreEqual(-68873, Inlines.silk_SMULWB(11573411, -390));
            Assert.AreEqual(1992381, Inlines.silk_SMULWB(113442816, 1151));
            Assert.AreEqual(374255, Inlines.silk_SMULWB(132579328, 185));
            Assert.AreEqual(-112289, Inlines.silk_SMULWB(-6393514, 1151));
            Assert.AreEqual(-18049, Inlines.silk_SMULWB(-6393514, 185));
            Assert.AreEqual(104556, Inlines.silk_SMULWB(-3958528, -1731));
            Assert.AreEqual(283231, Inlines.silk_SMULWB(-9175424, -2023));
            Assert.AreEqual(131314, Inlines.silk_SMULWB(-3958528, -2174));
            Assert.AreEqual(291912, Inlines.silk_SMULWB(-9175424, -2085));
            Assert.AreEqual(122435, Inlines.silk_SMULWB(-3958528, -2027));
            Assert.AreEqual(276651, Inlines.silk_SMULWB(-9175424, -1976));
            Assert.AreEqual(89697, Inlines.silk_SMULWB(-3958528, -1485));
            Assert.AreEqual(252850, Inlines.silk_SMULWB(-9175424, -1806));
            Assert.AreEqual(55872, Inlines.silk_SMULWB(-3958528, -925));
            Assert.AreEqual(198808, Inlines.silk_SMULWB(-9175424, -1420));
            Assert.AreEqual(26999, Inlines.silk_SMULWB(-3958528, -447));
            Assert.AreEqual(146446, Inlines.silk_SMULWB(-9175424, -1046));
            Assert.AreEqual(-2175, Inlines.silk_SMULWB(-3958528, 36));
            Assert.AreEqual(132865, Inlines.silk_SMULWB(-9175424, -949));
            Assert.AreEqual(-33584, Inlines.silk_SMULWB(-3958528, 556));
            Assert.AreEqual(147146, Inlines.silk_SMULWB(-9175424, -1051));
            Assert.AreEqual(-52551, Inlines.silk_SMULWB(-3958528, 870));
            Assert.AreEqual(129925, Inlines.silk_SMULWB(-9175424, -928));
            Assert.AreEqual(-59557, Inlines.silk_SMULWB(-3958528, 986));
            Assert.AreEqual(54602, Inlines.silk_SMULWB(-9175424, -390));
            Assert.AreEqual(-69524, Inlines.silk_SMULWB(-3958528, 1151));
            Assert.AreEqual(-25902, Inlines.silk_SMULWB(-9175424, 185));
        }

        [TestMethod]
        public void Test_LSHIFT()
        {
            Assert.AreEqual(unchecked((int)0xa80), Inlines.silk_LSHIFT32(unchecked((int)0x15), 7));
            Assert.AreEqual(unchecked((int)0x2f400), Inlines.silk_LSHIFT32(unchecked((int)0x2f4), 8));
            Assert.AreEqual(unchecked((int)0xa80), Inlines.silk_LSHIFT32(unchecked((int)0x15), 7));
            Assert.AreEqual(unchecked((int)0x2f400), Inlines.silk_LSHIFT32(unchecked((int)0x2f4), 8));
            Assert.AreEqual(unchecked((int)0xfa00000), Inlines.silk_LSHIFT32(unchecked((int)0x3e80), 14));
            Assert.AreEqual(unchecked((int)0x10000), Inlines.silk_LSHIFT32(unchecked((int)0x4000), 2));
            Assert.AreEqual(unchecked((int)0xff824ae0), Inlines.silk_LSHIFT32(unchecked((int)0xffc12570), 1));
            Assert.AreEqual(unchecked((int)0xff507d34), Inlines.silk_LSHIFT32(unchecked((int)0xffa83e9a), 1));
            Assert.AreEqual(unchecked((int)0xfee98802), Inlines.silk_LSHIFT32(unchecked((int)0xff74c401), 1));
            Assert.AreEqual(unchecked((int)0xfda32e18), Inlines.silk_LSHIFT32(unchecked((int)0xfed1970c), 1));
            Assert.AreEqual(unchecked((int)0xffa1ce74), Inlines.silk_LSHIFT32(unchecked((int)0xffd0e73a), 1));
            Assert.AreEqual(unchecked((int)0xfded2dce), Inlines.silk_LSHIFT32(unchecked((int)0xfef696e7), 1));
            Assert.AreEqual(unchecked((int)0x1d7a3b8), Inlines.silk_LSHIFT32(unchecked((int)0xebd1dc), 1));
            Assert.AreEqual(unchecked((int)0x680000), Inlines.silk_LSHIFT32(unchecked((int)0x68), 16));
            Assert.AreEqual(unchecked((int)0x0), Inlines.silk_LSHIFT32(unchecked((int)0x0), 9));
            Assert.AreEqual(unchecked((int)0xd000), Inlines.silk_LSHIFT32(unchecked((int)0x68), 9));
            Assert.AreEqual(unchecked((int)0x0), Inlines.silk_LSHIFT32(unchecked((int)0x0), 7));
            Assert.AreEqual(unchecked((int)0x183b80), Inlines.silk_LSHIFT32(unchecked((int)0x3077), 7));
            Assert.AreEqual(unchecked((int)0xfe6c0000), Inlines.silk_LSHIFT32(unchecked((int)0xfffffe6c), 16));
            Assert.AreEqual(unchecked((int)0xf9530000), Inlines.silk_LSHIFT32(unchecked((int)0xfffff953), 16));
            Assert.AreEqual(unchecked((int)0xfffcd800), Inlines.silk_LSHIFT32(unchecked((int)0xfffffe6c), 9));
        }

        [TestMethod]
        public void Test_RSHIFT()
        {
            Assert.AreEqual(12499, Inlines.silk_RSHIFT(99999, 3));
            Assert.AreEqual(880, Inlines.silk_RSHIFT(56345, 6));
            Assert.AreEqual(-1, Inlines.silk_RSHIFT(-45994, 16));
            Assert.AreEqual(0, Inlines.silk_RSHIFT(78, 30));
            Assert.AreEqual(-1, Inlines.silk_RSHIFT(-34, 12));
            Assert.AreEqual(unchecked((int)0x28), Inlines.silk_RSHIFT32(unchecked((int)0x140), 3));
            Assert.AreEqual(unchecked((int)0xa0), Inlines.silk_RSHIFT32(unchecked((int)0x140), 1));
            Assert.AreEqual(unchecked((int)0x50), Inlines.silk_RSHIFT32(unchecked((int)0x140), 2));
            Assert.AreEqual(unchecked((int)0x28), Inlines.silk_RSHIFT32(unchecked((int)0x140), 3));
            Assert.AreEqual(unchecked((int)0xa0), Inlines.silk_RSHIFT32(unchecked((int)0x140), 1));
            Assert.AreEqual(unchecked((int)0x50), Inlines.silk_RSHIFT32(unchecked((int)0xa0), 1));
            Assert.AreEqual(unchecked((int)0x28), Inlines.silk_RSHIFT32(unchecked((int)0x50), 1));
            Assert.AreEqual(unchecked((int)0xeee), Inlines.silk_RSHIFT32(unchecked((int)0x1ddc), 1));
            Assert.AreEqual(unchecked((int)0xd4f), Inlines.silk_RSHIFT32(unchecked((int)0x1a9f), 1));
            Assert.AreEqual(unchecked((int)0x102), Inlines.silk_RSHIFT32(unchecked((int)0x205), 1));
            Assert.AreEqual(unchecked((int)0xfffffffa), Inlines.silk_RSHIFT32(unchecked((int)0xffffeb66), 10));
            Assert.AreEqual(unchecked((int)0x11c2), Inlines.silk_RSHIFT32(unchecked((int)0x470a94), 10));
            Assert.AreEqual(unchecked((int)0xcf2), Inlines.silk_RSHIFT32(unchecked((int)0x33ca94), 10));
            Assert.AreEqual(unchecked((int)0x8), Inlines.silk_RSHIFT32(unchecked((int)0x10), 1));
            Assert.AreEqual(unchecked((int)0x8), Inlines.silk_RSHIFT32(unchecked((int)0x10), 1));
            Assert.AreEqual(unchecked((int)0x9), Inlines.silk_RSHIFT32(unchecked((int)0x247e), 10));
            Assert.AreEqual(unchecked((int)0x17fa), Inlines.silk_RSHIFT32(unchecked((int)0x5fea94), 10));
            Assert.AreEqual(unchecked((int)0x1d02), Inlines.silk_RSHIFT32(unchecked((int)0x740a94), 10));
            Assert.AreEqual(unchecked((int)0x0), Inlines.silk_RSHIFT32(unchecked((int)0x38a), 10));
            Assert.AreEqual(unchecked((int)0x363), Inlines.silk_RSHIFT32(unchecked((int)0xd8e74), 10));
            Assert.AreEqual(unchecked((int)0x2b2), Inlines.silk_RSHIFT32(unchecked((int)0xaca74), 10));
        }

        [TestMethod]
        public void Test_RAND()
        {
            Assert.AreEqual(1103947680, Inlines.silk_RAND(1));
            Assert.AreEqual(1300261845, Inlines.silk_RAND(2));
            Assert.AreEqual(1496576010, Inlines.silk_RAND(3));
            Assert.AreEqual(1692890175, Inlines.silk_RAND(4));
        }

        [TestMethod]
        public void Test_lin2log()
        {
            Assert.AreEqual(0, Inlines.silk_lin2log(1));
            Assert.AreEqual(1739, Inlines.silk_lin2log(12345));
            Assert.AreEqual(2126, Inlines.silk_lin2log(99999));
            Assert.AreEqual(2588, Inlines.silk_lin2log(1234567));
            Assert.AreEqual(4095, Inlines.silk_lin2log(-1234567));
        }

        [TestMethod]
        public void Test_log2lin()
        {
            Assert.AreEqual(0, Inlines.silk_log2lin(-5366441));
            Assert.AreEqual(4384, Inlines.silk_log2lin(1549));
            Assert.AreEqual(80896, Inlines.silk_log2lin(2088));
            Assert.AreEqual(30080, Inlines.silk_log2lin(1904));
            Assert.AreEqual(23855104, Inlines.silk_log2lin(3137));
            Assert.AreEqual(52690944, Inlines.silk_log2lin(3283));
            Assert.AreEqual(28049408, Inlines.silk_log2lin(3167));
            Assert.AreEqual(28049408, Inlines.silk_log2lin(3167));
            Assert.AreEqual(32768000, Inlines.silk_log2lin(3196));
            Assert.AreEqual(71827456, Inlines.silk_log2lin(3341));
            Assert.AreEqual(38273024, Inlines.silk_log2lin(3225));
            Assert.AreEqual(38273024, Inlines.silk_log2lin(3225));
        }

        [TestMethod]
        public void silk_RSHIFT_uint()
        {
            Assert.AreEqual(124U, Inlines.silk_RSHIFT_uint(999, 3));
            Assert.AreEqual(880U, Inlines.silk_RSHIFT_uint(56345, 6));
            Assert.AreEqual(70U, Inlines.silk_RSHIFT_uint(4599400, 16));
            Assert.AreEqual(0U, Inlines.silk_RSHIFT_uint(78234, 30));
            Assert.AreEqual(830U, Inlines.silk_RSHIFT_uint(3400000, 12));

            Assert.AreEqual(0x1fe0001fU, Inlines.silk_RSHIFT_uint(0xFF0000FF, 3));
            Assert.AreEqual(0x03fc0003U, Inlines.silk_RSHIFT_uint(0xFF0000FF, 6));
            Assert.AreEqual(0x0000ff00U, Inlines.silk_RSHIFT_uint(0xFF0000FF, 16));
            Assert.AreEqual(0x00000003U, Inlines.silk_RSHIFT_uint(0xFF0000FF, 30));
            Assert.AreEqual(0x000ff000U, Inlines.silk_RSHIFT_uint(0xFF0000FF, 12));
        }

        [TestMethod]
        public void silk_ADD_RSHIFT_uint()
        {
            Assert.AreEqual(100123U, Inlines.silk_ADD_RSHIFT_uint(99999, 999, 3));
            Assert.AreEqual(58273U, Inlines.silk_ADD_RSHIFT_uint(56345, 123423, 6));
            Assert.AreEqual(64564U, Inlines.silk_ADD_RSHIFT_uint(64564, 45994, 16));
            Assert.AreEqual(999U, Inlines.silk_ADD_RSHIFT_uint(999, 78, 30));
            Assert.AreEqual(1272U, Inlines.silk_ADD_RSHIFT_uint(999, 1121211, 12));

            Assert.AreEqual(0xff00017bU, Inlines.silk_ADD_RSHIFT_uint(0xFF0000FF, 999, 3));
            Assert.AreEqual(0xff000887U, Inlines.silk_ADD_RSHIFT_uint(0xFF0000FF, 123423, 6));
            Assert.AreEqual(0xff0000ffU, Inlines.silk_ADD_RSHIFT_uint(0xFF0000FF, 45994, 16));
            Assert.AreEqual(0xff0000ffU, Inlines.silk_ADD_RSHIFT_uint(0xFF0000FF, 78, 30));
            Assert.AreEqual(0xff000210U, Inlines.silk_ADD_RSHIFT_uint(0xFF0000FF, 1121211, 12));
        }

        [TestMethod]
        public void Test_SILK_FIX_CONST()
        {
            Assert.AreEqual(0, Inlines.SILK_FIX_CONST(0.0001f, 4));
            Assert.AreEqual(8, Inlines.SILK_FIX_CONST(0.4999f, 4));
            Assert.AreEqual(8, Inlines.SILK_FIX_CONST(0.5000f, 4));
            Assert.AreEqual(8, Inlines.SILK_FIX_CONST(0.5001f, 4));
            Assert.AreEqual(0, Inlines.SILK_FIX_CONST(0.18f, 0));
            Assert.AreEqual(0, Inlines.SILK_FIX_CONST(0.18f, 1));
            Assert.AreEqual(1, Inlines.SILK_FIX_CONST(0.18f, 2));
            Assert.AreEqual(2, Inlines.SILK_FIX_CONST(1.0f, 1));
            Assert.AreEqual(2000, Inlines.SILK_FIX_CONST(1000.1f, 1));
            Assert.AreEqual(1, Inlines.SILK_FIX_CONST(0.562f, 1));
            Assert.AreEqual(-20, Inlines.SILK_FIX_CONST(-10.565f, 1));
            Assert.AreEqual(-199997, Inlines.SILK_FIX_CONST(-99999.0f, 1));
            Assert.AreEqual(16, Inlines.SILK_FIX_CONST(1.0f, 4));
            Assert.AreEqual(16002, Inlines.SILK_FIX_CONST(1000.1f, 4));
            Assert.AreEqual(9, Inlines.SILK_FIX_CONST(0.562f, 4));
            Assert.AreEqual(-168, Inlines.SILK_FIX_CONST(-10.565f, 4));
            Assert.AreEqual(-1599983, Inlines.SILK_FIX_CONST(-99999.0f, 4));
            Assert.AreEqual(1024, Inlines.SILK_FIX_CONST(1.0f, 10));
            Assert.AreEqual(1024102, Inlines.SILK_FIX_CONST(1000.1f, 10));
            Assert.AreEqual(575, Inlines.SILK_FIX_CONST(0.562f, 10));
            Assert.AreEqual(-10818, Inlines.SILK_FIX_CONST(-10.565f, 10));
            Assert.AreEqual(-102398975, Inlines.SILK_FIX_CONST(-99999.0f, 10));
        }

        [TestMethod]
        public void Test_CHOP16()
        {
            try
            {
                Inlines.CHOP16(1111111);
                Assert.Fail();
            }
            catch (OverflowException) { }
        }

        [TestMethod]
        public void Test_EC_ILOG()
        {
            Assert.AreEqual(8, Inlines.EC_ILOG(0x80));
            Assert.AreEqual(16, Inlines.EC_ILOG(0x8000));
            Assert.AreEqual(14, Inlines.EC_ILOG(0x3333));
            Assert.AreEqual(32, Inlines.EC_ILOG(0xFFFFFFFF));
            Assert.AreEqual(31, Inlines.EC_ILOG(0x7FFFFFFF));
            Assert.AreEqual(30, Inlines.EC_ILOG(0x3FFFFFFF));
        }

        [TestMethod]
        public void Test_EC_MINI()
        {
            Assert.AreEqual(0x0000003AU, Inlines.EC_MINI(58, 919));
            Assert.AreEqual(0x00000006U, Inlines.EC_MINI(123, 6));
            Assert.AreEqual(0x000000FFU, Inlines.EC_MINI(8888, 0xFF));
            Assert.AreEqual(0x0000FFFFU, Inlines.EC_MINI(0xFFFF0000, 0x0000FFFF));
            Assert.AreEqual(0x7FFFFFFFU, Inlines.EC_MINI(0x7FFFFFFF, 0x80000000));
        }

        [TestMethod]
        public void Test_silk_INVERSE32_varQ()
        {
            Assert.AreEqual(0x00000295U, (uint)Inlines.silk_INVERSE32_varQ(99, 16));
            Assert.AreEqual(0x00003333U, (uint)Inlines.silk_INVERSE32_varQ(5, 16));
            Assert.AreEqual(0x00000003U, (uint)Inlines.silk_INVERSE32_varQ(322, 10));
            Assert.AreEqual(0xe0000000U, (uint)Inlines.silk_INVERSE32_varQ(-2, 30));
            Assert.AreEqual(0xffffffdeU, (uint)Inlines.silk_INVERSE32_varQ(-123, 12));
        }

        [TestMethod]
        public void Test_silk_DIV32_varQ()
        {
            Assert.AreEqual(0x65a0, Inlines.silk_DIV32_varQ(99999, 123, 5));
            Assert.AreEqual(0xfa, Inlines.silk_DIV32_varQ(566654, 4523, 1));
            Assert.AreEqual(0x1bdbb55, Inlines.silk_DIV32_varQ(342322, 12, 10));
            Assert.AreEqual(0x7ffff000, Inlines.silk_DIV32_varQ(0x7FFFFFFF, 0x0000FFFF, 26));
            Assert.AreEqual(0x504b, Inlines.silk_DIV32_varQ(-123333, -24, 2));
        }
        
        // just making sure that casts between int / uint is bit-exact
        [TestMethod]
        public void Test_cast_uint_to_int()
        {
            uint a = 0xFFAABBCCU;
            int b = (int)a;
            Assert.AreEqual("FFAABBCC", b.ToString("X"));
        }

        [TestMethod]
        public void Test_cast_int_to_uint()
        {
            int a = -10;
            uint b = (uint)a;
            Assert.AreEqual("FFFFFFF6", b.ToString("X"));
        }

        [TestMethod]
        public void Test_silk_LSHIFT_SAT32()
        {
            Assert.AreEqual(556007424, Inlines.silk_LSHIFT_SAT32(8484, 16));
            Assert.AreEqual(931659776, Inlines.silk_LSHIFT_SAT32(3554, 18));
            Assert.AreEqual(678166528, Inlines.silk_LSHIFT_SAT32(2587, 18));
            Assert.AreEqual(587464704, Inlines.silk_LSHIFT_SAT32(2241, 18));
            Assert.AreEqual(26345472, Inlines.silk_LSHIFT_SAT32(402, 16));
            Assert.AreEqual(59113472, Inlines.silk_LSHIFT_SAT32(902, 16));
            Assert.AreEqual(27721728, Inlines.silk_LSHIFT_SAT32(423, 16));
            Assert.AreEqual(30146560, Inlines.silk_LSHIFT_SAT32(460, 16));
            Assert.AreEqual(34166784, Inlines.silk_LSHIFT_SAT32(133464, 8));
            Assert.AreEqual(76662784, Inlines.silk_LSHIFT_SAT32(299464, 8));
            Assert.AreEqual(35951616, Inlines.silk_LSHIFT_SAT32(140436, 8));
            Assert.AreEqual(39096320, Inlines.silk_LSHIFT_SAT32(152720, 8));
            Assert.AreEqual(44355072, Inlines.silk_LSHIFT_SAT32(173262, 8));
            Assert.AreEqual(99523072, Inlines.silk_LSHIFT_SAT32(388762, 8));
            Assert.AreEqual(46672128, Inlines.silk_LSHIFT_SAT32(182313, 8));
            Assert.AreEqual(50754560, Inlines.silk_LSHIFT_SAT32(198260, 8));
            Assert.AreEqual(57630720, Inlines.silk_LSHIFT_SAT32(225120, 8));
            Assert.AreEqual(129310720, Inlines.silk_LSHIFT_SAT32(505120, 8));
            Assert.AreEqual(60641280, Inlines.silk_LSHIFT_SAT32(236880, 8));
            Assert.AreEqual(65945600, Inlines.silk_LSHIFT_SAT32(257600, 8));
            Assert.AreEqual(74919936, Inlines.silk_LSHIFT_SAT32(292656, 8));
            Assert.AreEqual(168103936, Inlines.silk_LSHIFT_SAT32(656656, 8));
            Assert.AreEqual(78833664, Inlines.silk_LSHIFT_SAT32(307944, 8));
            Assert.AreEqual(85729280, Inlines.silk_LSHIFT_SAT32(334880, 8));
            Assert.AreEqual(97354752, Inlines.silk_LSHIFT_SAT32(380292, 8));
            Assert.AreEqual(218442752, Inlines.silk_LSHIFT_SAT32(853292, 8));
            Assert.AreEqual(102440448, Inlines.silk_LSHIFT_SAT32(400158, 8));
            Assert.AreEqual(111400960, Inlines.silk_LSHIFT_SAT32(435160, 8));
            Assert.AreEqual(126478848, Inlines.silk_LSHIFT_SAT32(494058, 8));
            Assert.AreEqual(283790848, Inlines.silk_LSHIFT_SAT32(1108558, 8));
            Assert.AreEqual(133085952, Inlines.silk_LSHIFT_SAT32(519867, 8));
            Assert.AreEqual(144727040, Inlines.silk_LSHIFT_SAT32(565340, 8));
            Assert.AreEqual(980942848, Inlines.silk_LSHIFT_SAT32(7484, 17));
            Assert.AreEqual(1300496384, Inlines.silk_LSHIFT_SAT32(9922, 17));
            Assert.AreEqual(46956544, Inlines.silk_LSHIFT_SAT32(1433, 15));
            Assert.AreEqual(37142528, Inlines.silk_LSHIFT_SAT32(2267, 14));
            Assert.AreEqual(195362816, Inlines.silk_LSHIFT_SAT32(2981, 16));
            Assert.AreEqual(524877824, Inlines.silk_LSHIFT_SAT32(8009, 16));
            Assert.AreEqual(14417920, Inlines.silk_LSHIFT_SAT32(220, 16));
            Assert.AreEqual(12845056, Inlines.silk_LSHIFT_SAT32(196, 16));
            Assert.AreEqual(34750464, Inlines.silk_LSHIFT_SAT32(8484, 12));
            Assert.AreEqual(33425408, Inlines.silk_LSHIFT_SAT32(16321, 11));
            Assert.AreEqual(33990656, Inlines.silk_LSHIFT_SAT32(16597, 11));
            Assert.AreEqual(31733760, Inlines.silk_LSHIFT_SAT32(15495, 11));
            Assert.AreEqual(11469312, Inlines.silk_LSHIFT_SAT32(44802, 8));
            Assert.AreEqual(11005952, Inlines.silk_LSHIFT_SAT32(42992, 8));
            Assert.AreEqual(11927552, Inlines.silk_LSHIFT_SAT32(182, 16));
            Assert.AreEqual(11333120, Inlines.silk_LSHIFT_SAT32(44270, 8));
            Assert.AreEqual(29274112, Inlines.silk_LSHIFT_SAT32(14294, 11));
            Assert.AreEqual(32733184, Inlines.silk_LSHIFT_SAT32(15983, 11));
            Assert.AreEqual(35952640, Inlines.silk_LSHIFT_SAT32(17555, 11));
            Assert.AreEqual(32040960, Inlines.silk_LSHIFT_SAT32(15645, 11));
            Assert.AreEqual(10842368, Inlines.silk_LSHIFT_SAT32(42353, 8));
            Assert.AreEqual(11387648, Inlines.silk_LSHIFT_SAT32(44483, 8));
            Assert.AreEqual(11862016, Inlines.silk_LSHIFT_SAT32(181, 16));
            Assert.AreEqual(11251200, Inlines.silk_LSHIFT_SAT32(43950, 8));
            Assert.AreEqual(30349312, Inlines.silk_LSHIFT_SAT32(14819, 11));
            Assert.AreEqual(31580160, Inlines.silk_LSHIFT_SAT32(15420, 11));
            Assert.AreEqual(29503488, Inlines.silk_LSHIFT_SAT32(14406, 11));
            Assert.AreEqual(31348736, Inlines.silk_LSHIFT_SAT32(15307, 11));
            Assert.AreEqual(10433280, Inlines.silk_LSHIFT_SAT32(40755, 8));
            Assert.AreEqual(11469312, Inlines.silk_LSHIFT_SAT32(44802, 8));
            Assert.AreEqual(10269696, Inlines.silk_LSHIFT_SAT32(40116, 8));
            Assert.AreEqual(10896896, Inlines.silk_LSHIFT_SAT32(42566, 8));
            Assert.AreEqual(30656512, Inlines.silk_LSHIFT_SAT32(14969, 11));
            Assert.AreEqual(28735488, Inlines.silk_LSHIFT_SAT32(14031, 11));
            Assert.AreEqual(32348160, Inlines.silk_LSHIFT_SAT32(15795, 11));
            Assert.AreEqual(33040384, Inlines.silk_LSHIFT_SAT32(16133, 11));
            Assert.AreEqual(10896896, Inlines.silk_LSHIFT_SAT32(42566, 8));
            Assert.AreEqual(10924032, Inlines.silk_LSHIFT_SAT32(42672, 8));
            Assert.AreEqual(10787840, Inlines.silk_LSHIFT_SAT32(42140, 8));
            Assert.AreEqual(10896896, Inlines.silk_LSHIFT_SAT32(42566, 8));
            Assert.AreEqual(524288, Inlines.silk_LSHIFT_SAT32(16, 15));
            Assert.AreEqual(31426560, Inlines.silk_LSHIFT_SAT32(15345, 11));
            Assert.AreEqual(30502912, Inlines.silk_LSHIFT_SAT32(14894, 11));
            Assert.AreEqual(33554432, Inlines.silk_LSHIFT_SAT32(16384, 11));
            Assert.AreEqual(30734336, Inlines.silk_LSHIFT_SAT32(15007, 11));
            Assert.AreEqual(10924032, Inlines.silk_LSHIFT_SAT32(42672, 8));
            Assert.AreEqual(10515200, Inlines.silk_LSHIFT_SAT32(41075, 8));
            Assert.AreEqual(11442176, Inlines.silk_LSHIFT_SAT32(44696, 8));
            Assert.AreEqual(11060480, Inlines.silk_LSHIFT_SAT32(43205, 8));
            Assert.AreEqual(30812160, Inlines.silk_LSHIFT_SAT32(15045, 11));
            Assert.AreEqual(27119616, Inlines.silk_LSHIFT_SAT32(13242, 11));
            Assert.AreEqual(26812416, Inlines.silk_LSHIFT_SAT32(13092, 11));
            Assert.AreEqual(32040960, Inlines.silk_LSHIFT_SAT32(15645, 11));
            Assert.AreEqual(10351616, Inlines.silk_LSHIFT_SAT32(40436, 8));
            Assert.AreEqual(10188032, Inlines.silk_LSHIFT_SAT32(39797, 8));
            Assert.AreEqual(9206528, Inlines.silk_LSHIFT_SAT32(35963, 8));
            Assert.AreEqual(11033088, Inlines.silk_LSHIFT_SAT32(43098, 8));
            Assert.AreEqual(32657408, Inlines.silk_LSHIFT_SAT32(15946, 11));
            Assert.AreEqual(31348736, Inlines.silk_LSHIFT_SAT32(15307, 11));
            Assert.AreEqual(30965760, Inlines.silk_LSHIFT_SAT32(15120, 11));
            Assert.AreEqual(28504064, Inlines.silk_LSHIFT_SAT32(13918, 11));
            Assert.AreEqual(10869504, Inlines.silk_LSHIFT_SAT32(42459, 8));
            Assert.AreEqual(10569728, Inlines.silk_LSHIFT_SAT32(41288, 8));
            Assert.AreEqual(11605760, Inlines.silk_LSHIFT_SAT32(45335, 8));
            Assert.AreEqual(10324224, Inlines.silk_LSHIFT_SAT32(40329, 8));
            Assert.AreEqual(28428288, Inlines.silk_LSHIFT_SAT32(13881, 11));
            Assert.AreEqual(29810688, Inlines.silk_LSHIFT_SAT32(14556, 11));
            Assert.AreEqual(28196864, Inlines.silk_LSHIFT_SAT32(13768, 11));
            Assert.AreEqual(9724416, Inlines.silk_LSHIFT_SAT32(37986, 8));
            Assert.AreEqual(10760448, Inlines.silk_LSHIFT_SAT32(42033, 8));
            Assert.AreEqual(10924032, Inlines.silk_LSHIFT_SAT32(42672, 8));
            Assert.AreEqual(10188032, Inlines.silk_LSHIFT_SAT32(39797, 8));
            Assert.AreEqual(26580992, Inlines.silk_LSHIFT_SAT32(12979, 11));
            Assert.AreEqual(25888768, Inlines.silk_LSHIFT_SAT32(12641, 11));
            Assert.AreEqual(28350464, Inlines.silk_LSHIFT_SAT32(13843, 11));
            Assert.AreEqual(26734592, Inlines.silk_LSHIFT_SAT32(13054, 11));
            Assert.AreEqual(9288192, Inlines.silk_LSHIFT_SAT32(36282, 8));
            Assert.AreEqual(9560832, Inlines.silk_LSHIFT_SAT32(37347, 8));
            Assert.AreEqual(9778944, Inlines.silk_LSHIFT_SAT32(38199, 8));
            Assert.AreEqual(9424640, Inlines.silk_LSHIFT_SAT32(36815, 8));
            Assert.AreEqual(1966080, Inlines.silk_LSHIFT_SAT32(60, 15));
            Assert.AreEqual(32657408, Inlines.silk_LSHIFT_SAT32(15946, 11));
            Assert.AreEqual(31887360, Inlines.silk_LSHIFT_SAT32(15570, 11));
            Assert.AreEqual(32272384, Inlines.silk_LSHIFT_SAT32(15758, 11));
            Assert.AreEqual(35080192, Inlines.silk_LSHIFT_SAT32(17129, 11));
            Assert.AreEqual(11796480, Inlines.silk_LSHIFT_SAT32(180, 16));
            Assert.AreEqual(11442176, Inlines.silk_LSHIFT_SAT32(44696, 8));
            Assert.AreEqual(12255232, Inlines.silk_LSHIFT_SAT32(187, 16));
            Assert.AreEqual(688128, Inlines.silk_LSHIFT_SAT32(21, 15));
            Assert.AreEqual(33771520, Inlines.silk_LSHIFT_SAT32(16490, 11));
            Assert.AreEqual(28043264, Inlines.silk_LSHIFT_SAT32(13693, 11));
            Assert.AreEqual(28504064, Inlines.silk_LSHIFT_SAT32(13918, 11));
            Assert.AreEqual(31502336, Inlines.silk_LSHIFT_SAT32(15382, 11));
            Assert.AreEqual(11087616, Inlines.silk_LSHIFT_SAT32(43311, 8));
            Assert.AreEqual(10651392, Inlines.silk_LSHIFT_SAT32(41607, 8));
            Assert.AreEqual(9860864, Inlines.silk_LSHIFT_SAT32(38519, 8));
            Assert.AreEqual(11333120, Inlines.silk_LSHIFT_SAT32(44270, 8));
            Assert.AreEqual(32579584, Inlines.silk_LSHIFT_SAT32(15908, 11));
            Assert.AreEqual(33662976, Inlines.silk_LSHIFT_SAT32(16437, 11));
            Assert.AreEqual(25966592, Inlines.silk_LSHIFT_SAT32(12679, 11));
            Assert.AreEqual(25812992, Inlines.silk_LSHIFT_SAT32(12604, 11));
            Assert.AreEqual(11251200, Inlines.silk_LSHIFT_SAT32(43950, 8));
            Assert.AreEqual(12255232, Inlines.silk_LSHIFT_SAT32(187, 16));
            Assert.AreEqual(9533696, Inlines.silk_LSHIFT_SAT32(37241, 8));
            Assert.AreEqual(8688384, Inlines.silk_LSHIFT_SAT32(33939, 8));
            Assert.AreEqual(1638400, Inlines.silk_LSHIFT_SAT32(50, 15));
            Assert.AreEqual(31119360, Inlines.silk_LSHIFT_SAT32(15195, 11));
            Assert.AreEqual(30119936, Inlines.silk_LSHIFT_SAT32(14707, 11));
            Assert.AreEqual(27736064, Inlines.silk_LSHIFT_SAT32(13543, 11));
            Assert.AreEqual(29196288, Inlines.silk_LSHIFT_SAT32(14256, 11));
            Assert.AreEqual(11169536, Inlines.silk_LSHIFT_SAT32(43631, 8));
            Assert.AreEqual(10869504, Inlines.silk_LSHIFT_SAT32(42459, 8));
            Assert.AreEqual(9860864, Inlines.silk_LSHIFT_SAT32(38519, 8));
            Assert.AreEqual(10651392, Inlines.silk_LSHIFT_SAT32(41607, 8));
            Assert.AreEqual(31348736, Inlines.silk_LSHIFT_SAT32(15307, 11));
            Assert.AreEqual(29734912, Inlines.silk_LSHIFT_SAT32(14519, 11));
            Assert.AreEqual(29042688, Inlines.silk_LSHIFT_SAT32(14181, 11));
            Assert.AreEqual(622329856, Inlines.silk_LSHIFT_SAT32(9496, 16));
            Assert.AreEqual(10487808, Inlines.silk_LSHIFT_SAT32(40968, 8));
            Assert.AreEqual(9560832, Inlines.silk_LSHIFT_SAT32(37347, 8));
            Assert.AreEqual(9997056, Inlines.silk_LSHIFT_SAT32(39051, 8));
            Assert.AreEqual(129695744, Inlines.silk_LSHIFT_SAT32(1979, 16));
            Assert.AreEqual(1293549568, Inlines.silk_LSHIFT_SAT32(9869, 17));
            Assert.AreEqual(1234173952, Inlines.silk_LSHIFT_SAT32(9416, 17));
            Assert.AreEqual(1150418944, Inlines.silk_LSHIFT_SAT32(8777, 17));
            Assert.AreEqual(1290010624, Inlines.silk_LSHIFT_SAT32(9842, 17));
            Assert.AreEqual(473235456, Inlines.silk_LSHIFT_SAT32(7221, 16));
            Assert.AreEqual(440008704, Inlines.silk_LSHIFT_SAT32(6714, 16));
            Assert.AreEqual(270991360, Inlines.silk_LSHIFT_SAT32(4135, 16));
            Assert.AreEqual(462159872, Inlines.silk_LSHIFT_SAT32(7052, 16));
            Assert.AreEqual(1157496832, Inlines.silk_LSHIFT_SAT32(8831, 17));
            Assert.AreEqual(1338900480, Inlines.silk_LSHIFT_SAT32(10215, 17));
            Assert.AreEqual(365232128, Inlines.silk_LSHIFT_SAT32(5573, 16));
            Assert.AreEqual(27262976, Inlines.silk_LSHIFT_SAT32(1664, 14));
            Assert.AreEqual(220004352, Inlines.silk_LSHIFT_SAT32(3357, 16));
            Assert.AreEqual(576978944, Inlines.silk_LSHIFT_SAT32(8804, 16));
            Assert.AreEqual(70778880, Inlines.silk_LSHIFT_SAT32(1080, 16));
            Assert.AreEqual(8203008, Inlines.silk_LSHIFT_SAT32(32043, 8));
            Assert.AreEqual(2260992, Inlines.silk_LSHIFT_SAT32(69, 15));
            Assert.AreEqual(27885568, Inlines.silk_LSHIFT_SAT32(6808, 12));
            Assert.AreEqual(25042944, Inlines.silk_LSHIFT_SAT32(12228, 11));
            Assert.AreEqual(23967744, Inlines.silk_LSHIFT_SAT32(11703, 11));
            Assert.AreEqual(23646208, Inlines.silk_LSHIFT_SAT32(11546, 11));
            Assert.AreEqual(8797440, Inlines.silk_LSHIFT_SAT32(34365, 8));
            Assert.AreEqual(8933888, Inlines.silk_LSHIFT_SAT32(34898, 8));
            Assert.AreEqual(9015552, Inlines.silk_LSHIFT_SAT32(35217, 8));
            Assert.AreEqual(8715776, Inlines.silk_LSHIFT_SAT32(34046, 8));
            Assert.AreEqual(1132986368, Inlines.silk_LSHIFT_SAT32(8644, 17));
            Assert.AreEqual(1370357760, Inlines.silk_LSHIFT_SAT32(10455, 17));
            Assert.AreEqual(1373765632, Inlines.silk_LSHIFT_SAT32(10481, 17));
            Assert.AreEqual(1495924736, Inlines.silk_LSHIFT_SAT32(11413, 17));
            Assert.AreEqual(213254144, Inlines.silk_LSHIFT_SAT32(3254, 16));
            Assert.AreEqual(543817728, Inlines.silk_LSHIFT_SAT32(8298, 16));
            Assert.AreEqual(290193408, Inlines.silk_LSHIFT_SAT32(4428, 16));
            Assert.AreEqual(1328414720, Inlines.silk_LSHIFT_SAT32(10135, 17));
            Assert.AreEqual(1461059584, Inlines.silk_LSHIFT_SAT32(11147, 17));
            Assert.AreEqual(1283063808, Inlines.silk_LSHIFT_SAT32(9789, 17));
            Assert.AreEqual(64356352, Inlines.silk_LSHIFT_SAT32(1964, 15));
            Assert.AreEqual(5044, Inlines.silk_LSHIFT_SAT32(2522, 1));
            Assert.AreEqual(2920, Inlines.silk_LSHIFT_SAT32(1460, 1));
            Assert.AreEqual(2028, Inlines.silk_LSHIFT_SAT32(1014, 1));
            Assert.AreEqual(896316, Inlines.silk_LSHIFT_SAT32(448158, 1));
            Assert.AreEqual(-29760, Inlines.silk_LSHIFT_SAT32(-1860, 4));
            Assert.AreEqual(-320, Inlines.silk_LSHIFT_SAT32(-20, 4));
            Assert.AreEqual(-42032, Inlines.silk_LSHIFT_SAT32(-2627, 4));
            Assert.AreEqual(-240, Inlines.silk_LSHIFT_SAT32(-15, 4));
            Assert.AreEqual(-28144, Inlines.silk_LSHIFT_SAT32(-1759, 4));
            Assert.AreEqual(-320, Inlines.silk_LSHIFT_SAT32(-20, 4));
            Assert.AreEqual(32400, Inlines.silk_LSHIFT_SAT32(2025, 4));
            Assert.AreEqual(240, Inlines.silk_LSHIFT_SAT32(15, 4));
            Assert.AreEqual(234160128, Inlines.silk_LSHIFT_SAT32(3573, 16));
            Assert.AreEqual(257490944, Inlines.silk_LSHIFT_SAT32(3929, 16));
            Assert.AreEqual(227999744, Inlines.silk_LSHIFT_SAT32(3479, 16));
            Assert.AreEqual(47382528, Inlines.silk_LSHIFT_SAT32(723, 16));
            Assert.AreEqual(31956992, Inlines.silk_LSHIFT_SAT32(3901, 13));
            Assert.AreEqual(29655040, Inlines.silk_LSHIFT_SAT32(7240, 12));
            Assert.AreEqual(25888768, Inlines.silk_LSHIFT_SAT32(12641, 11));
            Assert.AreEqual(24428544, Inlines.silk_LSHIFT_SAT32(11928, 11));
            Assert.AreEqual(12189696, Inlines.silk_LSHIFT_SAT32(186, 16));
            Assert.AreEqual(9342720, Inlines.silk_LSHIFT_SAT32(36495, 8));
            Assert.AreEqual(9724416, Inlines.silk_LSHIFT_SAT32(37986, 8));
            Assert.AreEqual(8470272, Inlines.silk_LSHIFT_SAT32(33087, 8));
            Assert.AreEqual(57315328, Inlines.silk_LSHIFT_SAT32(13993, 12));
            Assert.AreEqual(1269170176, Inlines.silk_LSHIFT_SAT32(9683, 17));
            Assert.AreEqual(1454112768, Inlines.silk_LSHIFT_SAT32(11094, 17));
            Assert.AreEqual(1325006848, Inlines.silk_LSHIFT_SAT32(10109, 17));
            Assert.AreEqual(15204352, Inlines.silk_LSHIFT_SAT32(232, 16));
            Assert.AreEqual(499056640, Inlines.silk_LSHIFT_SAT32(7615, 16));
            Assert.AreEqual(339935232, Inlines.silk_LSHIFT_SAT32(5187, 16));
            Assert.AreEqual(533528576, Inlines.silk_LSHIFT_SAT32(8141, 16));
            Assert.AreEqual(1499463680, Inlines.silk_LSHIFT_SAT32(11440, 17));
            Assert.AreEqual(1391198208, Inlines.silk_LSHIFT_SAT32(10614, 17));
            Assert.AreEqual(1377304576, Inlines.silk_LSHIFT_SAT32(10508, 17));
            Assert.AreEqual(1147011072, Inlines.silk_LSHIFT_SAT32(8751, 17));
            Assert.AreEqual(5384, Inlines.silk_LSHIFT_SAT32(2692, 1));
            Assert.AreEqual(45352, Inlines.silk_LSHIFT_SAT32(22676, 1));
            Assert.AreEqual(44112, Inlines.silk_LSHIFT_SAT32(22056, 1));
            Assert.AreEqual(1574, Inlines.silk_LSHIFT_SAT32(787, 1));
            Assert.AreEqual(120256, Inlines.silk_LSHIFT_SAT32(7516, 4));
            Assert.AreEqual(149824, Inlines.silk_LSHIFT_SAT32(9364, 4));
            Assert.AreEqual(608, Inlines.silk_LSHIFT_SAT32(38, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(1264, Inlines.silk_LSHIFT_SAT32(79, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(-1376, Inlines.silk_LSHIFT_SAT32(-86, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(220594176, Inlines.silk_LSHIFT_SAT32(3366, 16));
            Assert.AreEqual(205848576, Inlines.silk_LSHIFT_SAT32(3141, 16));
            Assert.AreEqual(204013568, Inlines.silk_LSHIFT_SAT32(3113, 16));
            Assert.AreEqual(168624128, Inlines.silk_LSHIFT_SAT32(2573, 16));
            Assert.AreEqual(8060928, Inlines.silk_LSHIFT_SAT32(246, 15));
            Assert.AreEqual(39550976, Inlines.silk_LSHIFT_SAT32(1207, 15));
            Assert.AreEqual(28884992, Inlines.silk_LSHIFT_SAT32(3526, 13));
            Assert.AreEqual(28807168, Inlines.silk_LSHIFT_SAT32(7033, 12));
            Assert.AreEqual(26198016, Inlines.silk_LSHIFT_SAT32(12792, 11));
            Assert.AreEqual(12517376, Inlines.silk_LSHIFT_SAT32(191, 16));
            Assert.AreEqual(9479168, Inlines.silk_LSHIFT_SAT32(37028, 8));
            Assert.AreEqual(9588224, Inlines.silk_LSHIFT_SAT32(37454, 8));
            Assert.AreEqual(10624256, Inlines.silk_LSHIFT_SAT32(41501, 8));
            Assert.AreEqual(21466112, Inlines.silk_LSHIFT_SAT32(20963, 10));
            Assert.AreEqual(373129216, Inlines.silk_LSHIFT_SAT32(11387, 15));
            Assert.AreEqual(1314521088, Inlines.silk_LSHIFT_SAT32(10029, 17));
            Assert.AreEqual(1495924736, Inlines.silk_LSHIFT_SAT32(11413, 17));
            Assert.AreEqual(7088128, Inlines.silk_LSHIFT_SAT32(27688, 8));
            Assert.AreEqual(71630848, Inlines.silk_LSHIFT_SAT32(1093, 16));
            Assert.AreEqual(552534016, Inlines.silk_LSHIFT_SAT32(8431, 16));
            Assert.AreEqual(294584320, Inlines.silk_LSHIFT_SAT32(4495, 16));
            Assert.AreEqual(1335361536, Inlines.silk_LSHIFT_SAT32(10188, 17));
            Assert.AreEqual(1457520640, Inlines.silk_LSHIFT_SAT32(11120, 17));
            Assert.AreEqual(1461059584, Inlines.silk_LSHIFT_SAT32(11147, 17));
            Assert.AreEqual(1335361536, Inlines.silk_LSHIFT_SAT32(10188, 17));
            Assert.AreEqual(508828, Inlines.silk_LSHIFT_SAT32(254414, 1));
            Assert.AreEqual(26380, Inlines.silk_LSHIFT_SAT32(13190, 1));
            Assert.AreEqual(13668, Inlines.silk_LSHIFT_SAT32(6834, 1));
            Assert.AreEqual(48544, Inlines.silk_LSHIFT_SAT32(24272, 1));
            Assert.AreEqual(71056, Inlines.silk_LSHIFT_SAT32(4441, 4));
            Assert.AreEqual(128384, Inlines.silk_LSHIFT_SAT32(8024, 4));
            Assert.AreEqual(-4832, Inlines.silk_LSHIFT_SAT32(-302, 4));
            Assert.AreEqual(-16, Inlines.silk_LSHIFT_SAT32(-1, 4));
            Assert.AreEqual(1984, Inlines.silk_LSHIFT_SAT32(124, 4));
            Assert.AreEqual(16, Inlines.silk_LSHIFT_SAT32(1, 4));
            Assert.AreEqual(6720, Inlines.silk_LSHIFT_SAT32(420, 4));
            Assert.AreEqual(64, Inlines.silk_LSHIFT_SAT32(4, 4));
            Assert.AreEqual(517537792, Inlines.silk_LSHIFT_SAT32(7897, 16));
            Assert.AreEqual(248905728, Inlines.silk_LSHIFT_SAT32(3798, 16));
            Assert.AreEqual(247660544, Inlines.silk_LSHIFT_SAT32(3779, 16));
            Assert.AreEqual(226754560, Inlines.silk_LSHIFT_SAT32(3460, 16));
            Assert.AreEqual(939130880, Inlines.silk_LSHIFT_SAT32(7165, 17));
            Assert.AreEqual(382238720, Inlines.silk_LSHIFT_SAT32(11665, 15));
            Assert.AreEqual(1314521088, Inlines.silk_LSHIFT_SAT32(10029, 17));
            Assert.AreEqual(1143472128, Inlines.silk_LSHIFT_SAT32(8724, 17));
            Assert.AreEqual(186974208, Inlines.silk_LSHIFT_SAT32(2853, 16));
            Assert.AreEqual(74055680, Inlines.silk_LSHIFT_SAT32(1130, 16));
            Assert.AreEqual(547291136, Inlines.silk_LSHIFT_SAT32(8351, 16));
            Assert.AreEqual(223084544, Inlines.silk_LSHIFT_SAT32(3404, 16));
            Assert.AreEqual(72318976, Inlines.silk_LSHIFT_SAT32(2207, 15));
            Assert.AreEqual(1269170176, Inlines.silk_LSHIFT_SAT32(9683, 17));
            Assert.AreEqual(1272578048, Inlines.silk_LSHIFT_SAT32(9709, 17));
            Assert.AreEqual(66519040, Inlines.silk_LSHIFT_SAT32(2030, 15));
            Assert.AreEqual(17825792, Inlines.silk_LSHIFT_SAT32(272, 16));
            Assert.AreEqual(465829888, Inlines.silk_LSHIFT_SAT32(7108, 16));
            Assert.AreEqual(371326976, Inlines.silk_LSHIFT_SAT32(5666, 16));
            Assert.AreEqual(18153472, Inlines.silk_LSHIFT_SAT32(277, 16));
            Assert.AreEqual(1139933184, Inlines.silk_LSHIFT_SAT32(8697, 17));
            Assert.AreEqual(1321467904, Inlines.silk_LSHIFT_SAT32(10082, 17));
            Assert.AreEqual(358285312, Inlines.silk_LSHIFT_SAT32(5467, 16));
            Assert.AreEqual(926875648, Inlines.silk_LSHIFT_SAT32(14143, 16));
            Assert.AreEqual(346, Inlines.silk_LSHIFT_SAT32(173, 1));
            Assert.AreEqual(440042, Inlines.silk_LSHIFT_SAT32(220021, 1));
            Assert.AreEqual(11362, Inlines.silk_LSHIFT_SAT32(5681, 1));
            Assert.AreEqual(300, Inlines.silk_LSHIFT_SAT32(150, 1));
            Assert.AreEqual(-10048, Inlines.silk_LSHIFT_SAT32(-628, 4));
            Assert.AreEqual(-304, Inlines.silk_LSHIFT_SAT32(-19, 4));
            Assert.AreEqual(41792, Inlines.silk_LSHIFT_SAT32(2612, 4));
            Assert.AreEqual(83600, Inlines.silk_LSHIFT_SAT32(5225, 4));
            Assert.AreEqual(12976, Inlines.silk_LSHIFT_SAT32(811, 4));
            Assert.AreEqual(160, Inlines.silk_LSHIFT_SAT32(10, 4));
            Assert.AreEqual(-16064, Inlines.silk_LSHIFT_SAT32(-1004, 4));
            Assert.AreEqual(-288, Inlines.silk_LSHIFT_SAT32(-18, 4));
            Assert.AreEqual(215678976, Inlines.silk_LSHIFT_SAT32(3291, 16));
            Assert.AreEqual(552534016, Inlines.silk_LSHIFT_SAT32(8431, 16));
            Assert.AreEqual(70320128, Inlines.silk_LSHIFT_SAT32(1073, 16));
            Assert.AreEqual(177340416, Inlines.silk_LSHIFT_SAT32(2706, 16));
            Assert.AreEqual(1314521088, Inlines.silk_LSHIFT_SAT32(10029, 17));
            Assert.AreEqual(665976832, Inlines.silk_LSHIFT_SAT32(10162, 16));
            Assert.AreEqual(676462592, Inlines.silk_LSHIFT_SAT32(10322, 16));
            Assert.AreEqual(1321467904, Inlines.silk_LSHIFT_SAT32(10082, 17));
            Assert.AreEqual(524877824, Inlines.silk_LSHIFT_SAT32(8009, 16));
            Assert.AreEqual(128450560, Inlines.silk_LSHIFT_SAT32(1960, 16));
            Assert.AreEqual(128712704, Inlines.silk_LSHIFT_SAT32(1964, 16));
            Assert.AreEqual(540344320, Inlines.silk_LSHIFT_SAT32(8245, 16));
            Assert.AreEqual(934150144, Inlines.silk_LSHIFT_SAT32(7127, 17));
            Assert.AreEqual(381026304, Inlines.silk_LSHIFT_SAT32(11628, 15));
            Assert.AreEqual(1314521088, Inlines.silk_LSHIFT_SAT32(10029, 17));
            Assert.AreEqual(1143472128, Inlines.silk_LSHIFT_SAT32(8724, 17));
            Assert.AreEqual(183894016, Inlines.silk_LSHIFT_SAT32(2806, 16));
            Assert.AreEqual(73596928, Inlines.silk_LSHIFT_SAT32(1123, 16));
            Assert.AreEqual(549060608, Inlines.silk_LSHIFT_SAT32(8378, 16));
            Assert.AreEqual(222429184, Inlines.silk_LSHIFT_SAT32(3394, 16));
            Assert.AreEqual(70156288, Inlines.silk_LSHIFT_SAT32(2141, 15));
            Assert.AreEqual(1262092288, Inlines.silk_LSHIFT_SAT32(9629, 17));
            Assert.AreEqual(1269170176, Inlines.silk_LSHIFT_SAT32(9683, 17));
            Assert.AreEqual(64978944, Inlines.silk_LSHIFT_SAT32(1983, 15));
            Assert.AreEqual(16187392, Inlines.silk_LSHIFT_SAT32(247, 16));
            Assert.AreEqual(465829888, Inlines.silk_LSHIFT_SAT32(7108, 16));
            Assert.AreEqual(370475008, Inlines.silk_LSHIFT_SAT32(5653, 16));
            Assert.AreEqual(16580608, Inlines.silk_LSHIFT_SAT32(253, 16));
            Assert.AreEqual(1269170176, Inlines.silk_LSHIFT_SAT32(9683, 17));
            Assert.AreEqual(1509949440, Inlines.silk_LSHIFT_SAT32(11520, 17));
            Assert.AreEqual(408027136, Inlines.silk_LSHIFT_SAT32(6226, 16));
            Assert.AreEqual(1052377088, Inlines.silk_LSHIFT_SAT32(16058, 16));
            Assert.AreEqual(336, Inlines.silk_LSHIFT_SAT32(168, 1));
            Assert.AreEqual(430986, Inlines.silk_LSHIFT_SAT32(215493, 1));
            Assert.AreEqual(10938, Inlines.silk_LSHIFT_SAT32(5469, 1));
            Assert.AreEqual(406, Inlines.silk_LSHIFT_SAT32(203, 1));
            Assert.AreEqual(-11456, Inlines.silk_LSHIFT_SAT32(-716, 4));
            Assert.AreEqual(-400, Inlines.silk_LSHIFT_SAT32(-25, 4));
            Assert.AreEqual(39072, Inlines.silk_LSHIFT_SAT32(2442, 4));
            Assert.AreEqual(78160, Inlines.silk_LSHIFT_SAT32(4885, 4));
            Assert.AreEqual(11536, Inlines.silk_LSHIFT_SAT32(721, 4));
            Assert.AreEqual(112, Inlines.silk_LSHIFT_SAT32(7, 4));
            Assert.AreEqual(-20896, Inlines.silk_LSHIFT_SAT32(-1306, 4));
            Assert.AreEqual(-608, Inlines.silk_LSHIFT_SAT32(-38, 4));
            Assert.AreEqual(216924160, Inlines.silk_LSHIFT_SAT32(3310, 16));
            Assert.AreEqual(547291136, Inlines.silk_LSHIFT_SAT32(8351, 16));
            Assert.AreEqual(70582272, Inlines.silk_LSHIFT_SAT32(1077, 16));
            Assert.AreEqual(182583296, Inlines.silk_LSHIFT_SAT32(2786, 16));
            Assert.AreEqual(1506410496, Inlines.silk_LSHIFT_SAT32(11493, 17));
            Assert.AreEqual(764477440, Inlines.silk_LSHIFT_SAT32(11665, 16));
            Assert.AreEqual(762052608, Inlines.silk_LSHIFT_SAT32(11628, 16));
            Assert.AreEqual(1502871552, Inlines.silk_LSHIFT_SAT32(11466, 17));
            Assert.AreEqual(536870912, Inlines.silk_LSHIFT_SAT32(8192, 16));
            Assert.AreEqual(131203072, Inlines.silk_LSHIFT_SAT32(2002, 16));
            Assert.AreEqual(130875392, Inlines.silk_LSHIFT_SAT32(1997, 16));
            Assert.AreEqual(536870912, Inlines.silk_LSHIFT_SAT32(8192, 16));
            Assert.AreEqual(1069547520, Inlines.silk_LSHIFT_SAT32(8160, 17));
            Assert.AreEqual(421625856, Inlines.silk_LSHIFT_SAT32(12867, 15));
            Assert.AreEqual(1492516864, Inlines.silk_LSHIFT_SAT32(11387, 17));
            Assert.AreEqual(1283063808, Inlines.silk_LSHIFT_SAT32(9789, 17));
            Assert.AreEqual(187826176, Inlines.silk_LSHIFT_SAT32(2866, 16));
            Assert.AreEqual(73859072, Inlines.silk_LSHIFT_SAT32(1127, 16));
            Assert.AreEqual(536870912, Inlines.silk_LSHIFT_SAT32(8192, 16));
            Assert.AreEqual(224919552, Inlines.silk_LSHIFT_SAT32(3432, 16));
            Assert.AreEqual(79527936, Inlines.silk_LSHIFT_SAT32(2427, 15));
            Assert.AreEqual(1429602304, Inlines.silk_LSHIFT_SAT32(10907, 17));
            Assert.AreEqual(1447034880, Inlines.silk_LSHIFT_SAT32(11040, 17));
            Assert.AreEqual(73433088, Inlines.silk_LSHIFT_SAT32(2241, 15));
            Assert.AreEqual(17825792, Inlines.silk_LSHIFT_SAT32(272, 16));
            Assert.AreEqual(471990272, Inlines.silk_LSHIFT_SAT32(7202, 16));
            Assert.AreEqual(377487360, Inlines.silk_LSHIFT_SAT32(5760, 16));
            Assert.AreEqual(16973824, Inlines.silk_LSHIFT_SAT32(259, 16));
            Assert.AreEqual(1272578048, Inlines.silk_LSHIFT_SAT32(9709, 17));
            Assert.AreEqual(1513357312, Inlines.silk_LSHIFT_SAT32(11546, 17));
            Assert.AreEqual(410517504, Inlines.silk_LSHIFT_SAT32(6264, 16));
            Assert.AreEqual(1057292288, Inlines.silk_LSHIFT_SAT32(16133, 16));
            Assert.AreEqual(270, Inlines.silk_LSHIFT_SAT32(135, 1));
            Assert.AreEqual(433726, Inlines.silk_LSHIFT_SAT32(216863, 1));
            Assert.AreEqual(9788, Inlines.silk_LSHIFT_SAT32(4894, 1));
            Assert.AreEqual(756, Inlines.silk_LSHIFT_SAT32(378, 1));
            Assert.AreEqual(-16320, Inlines.silk_LSHIFT_SAT32(-1020, 4));
            Assert.AreEqual(-512, Inlines.silk_LSHIFT_SAT32(-32, 4));
            Assert.AreEqual(36352, Inlines.silk_LSHIFT_SAT32(2272, 4));
            Assert.AreEqual(72720, Inlines.silk_LSHIFT_SAT32(4545, 4));
            Assert.AreEqual(9456, Inlines.silk_LSHIFT_SAT32(591, 4));
            Assert.AreEqual(96, Inlines.silk_LSHIFT_SAT32(6, 4));
            Assert.AreEqual(-22832, Inlines.silk_LSHIFT_SAT32(-1427, 4));
            Assert.AreEqual(-1200, Inlines.silk_LSHIFT_SAT32(-75, 4));
            Assert.AreEqual(217513984, Inlines.silk_LSHIFT_SAT32(3319, 16));
            Assert.AreEqual(549060608, Inlines.silk_LSHIFT_SAT32(8378, 16));
            Assert.AreEqual(70582272, Inlines.silk_LSHIFT_SAT32(1077, 16));
            Assert.AreEqual(183894016, Inlines.silk_LSHIFT_SAT32(2806, 16));
            Assert.AreEqual(1509949440, Inlines.silk_LSHIFT_SAT32(11520, 17));
            Assert.AreEqual(766967808, Inlines.silk_LSHIFT_SAT32(11703, 16));
            Assert.AreEqual(762052608, Inlines.silk_LSHIFT_SAT32(11628, 16));
            Assert.AreEqual(1499463680, Inlines.silk_LSHIFT_SAT32(11440, 17));
            Assert.AreEqual(536870912, Inlines.silk_LSHIFT_SAT32(8192, 16));
            Assert.AreEqual(131792896, Inlines.silk_LSHIFT_SAT32(2011, 16));
            Assert.AreEqual(131530752, Inlines.silk_LSHIFT_SAT32(2007, 16));
            Assert.AreEqual(536870912, Inlines.silk_LSHIFT_SAT32(8192, 16));
            Assert.AreEqual(1067057152, Inlines.silk_LSHIFT_SAT32(8141, 17));
            Assert.AreEqual(420380672, Inlines.silk_LSHIFT_SAT32(12829, 15));
            Assert.AreEqual(1488977920, Inlines.silk_LSHIFT_SAT32(11360, 17));
            Assert.AreEqual(1279524864, Inlines.silk_LSHIFT_SAT32(9762, 17));
            Assert.AreEqual(187432960, Inlines.silk_LSHIFT_SAT32(2860, 16));
            Assert.AreEqual(73203712, Inlines.silk_LSHIFT_SAT32(1117, 16));
            Assert.AreEqual(543817728, Inlines.silk_LSHIFT_SAT32(8298, 16));
            Assert.AreEqual(223084544, Inlines.silk_LSHIFT_SAT32(3404, 16));
            Assert.AreEqual(81920000, Inlines.silk_LSHIFT_SAT32(2500, 15));
            Assert.AreEqual(1426194432, Inlines.silk_LSHIFT_SAT32(10881, 17));
            Assert.AreEqual(1443627008, Inlines.silk_LSHIFT_SAT32(11014, 17));
            Assert.AreEqual(76251136, Inlines.silk_LSHIFT_SAT32(2327, 15));
            Assert.AreEqual(19595264, Inlines.silk_LSHIFT_SAT32(299, 16));
            Assert.AreEqual(470745088, Inlines.silk_LSHIFT_SAT32(7183, 16));
            Assert.AreEqual(377487360, Inlines.silk_LSHIFT_SAT32(5760, 16));
            Assert.AreEqual(17498112, Inlines.silk_LSHIFT_SAT32(267, 16));
            Assert.AreEqual(1269170176, Inlines.silk_LSHIFT_SAT32(9683, 17));
            Assert.AreEqual(1912864768, Inlines.silk_LSHIFT_SAT32(14594, 17));
            Assert.AreEqual(1922564096, Inlines.silk_LSHIFT_SAT32(7334, 18));
            Assert.AreEqual(1932263424, Inlines.silk_LSHIFT_SAT32(7371, 18));
            Assert.AreEqual(216924160, Inlines.silk_LSHIFT_SAT32(3310, 16));
            Assert.AreEqual(576978944, Inlines.silk_LSHIFT_SAT32(8804, 16));
            Assert.AreEqual(578748416, Inlines.silk_LSHIFT_SAT32(8831, 16));
            Assert.AreEqual(1912602624, Inlines.silk_LSHIFT_SAT32(7296, 18));
            Assert.AreEqual(1882980352, Inlines.silk_LSHIFT_SAT32(7183, 18));
            Assert.AreEqual(1897922560, Inlines.silk_LSHIFT_SAT32(7240, 18));
            Assert.AreEqual(1789788160, Inlines.silk_LSHIFT_SAT32(13655, 17));
            Assert.AreEqual(29796, Inlines.silk_LSHIFT_SAT32(14898, 1));
            Assert.AreEqual(30698, Inlines.silk_LSHIFT_SAT32(15349, 1));
            Assert.AreEqual(30516, Inlines.silk_LSHIFT_SAT32(15258, 1));
            Assert.AreEqual(34460, Inlines.silk_LSHIFT_SAT32(17230, 1));
            Assert.AreEqual(-624, Inlines.silk_LSHIFT_SAT32(-39, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(576, Inlines.silk_LSHIFT_SAT32(36, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(432, Inlines.silk_LSHIFT_SAT32(27, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(-384, Inlines.silk_LSHIFT_SAT32(-24, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(248315904, Inlines.silk_LSHIFT_SAT32(3789, 16));
            Assert.AreEqual(242745344, Inlines.silk_LSHIFT_SAT32(3704, 16));
            Assert.AreEqual(243990528, Inlines.silk_LSHIFT_SAT32(3723, 16));
            Assert.AreEqual(274530304, Inlines.silk_LSHIFT_SAT32(4189, 16));
            Assert.AreEqual(1077149696, Inlines.silk_LSHIFT_SAT32(8218, 17));
            Assert.AreEqual(421625856, Inlines.silk_LSHIFT_SAT32(12867, 15));
            Assert.AreEqual(1485438976, Inlines.silk_LSHIFT_SAT32(11333, 17));
            Assert.AreEqual(1279524864, Inlines.silk_LSHIFT_SAT32(9762, 17));
            Assert.AreEqual(189857792, Inlines.silk_LSHIFT_SAT32(2897, 16));
            Assert.AreEqual(73400320, Inlines.silk_LSHIFT_SAT32(1120, 16));
            Assert.AreEqual(542048256, Inlines.silk_LSHIFT_SAT32(8271, 16));
            Assert.AreEqual(223674368, Inlines.silk_LSHIFT_SAT32(3413, 16));
            Assert.AreEqual(79101952, Inlines.silk_LSHIFT_SAT32(2414, 15));
            Assert.AreEqual(1426194432, Inlines.silk_LSHIFT_SAT32(10881, 17));
            Assert.AreEqual(2001207296, Inlines.silk_LSHIFT_SAT32(7634, 18));
            Assert.AreEqual(2006188032, Inlines.silk_LSHIFT_SAT32(7653, 18));
            Assert.AreEqual(19922944, Inlines.silk_LSHIFT_SAT32(304, 16));
            Assert.AreEqual(476905472, Inlines.silk_LSHIFT_SAT32(7277, 16));
            Assert.AreEqual(409272320, Inlines.silk_LSHIFT_SAT32(6245, 16));
            Assert.AreEqual(582221824, Inlines.silk_LSHIFT_SAT32(8884, 16));
            Assert.AreEqual(2001207296, Inlines.silk_LSHIFT_SAT32(7634, 18));
            Assert.AreEqual(1966866432, Inlines.silk_LSHIFT_SAT32(7503, 18));
            Assert.AreEqual(1922564096, Inlines.silk_LSHIFT_SAT32(7334, 18));
            Assert.AreEqual(1917583360, Inlines.silk_LSHIFT_SAT32(7315, 18));
            Assert.AreEqual(27028, Inlines.silk_LSHIFT_SAT32(13514, 1));
            Assert.AreEqual(28356, Inlines.silk_LSHIFT_SAT32(14178, 1));
            Assert.AreEqual(29570, Inlines.silk_LSHIFT_SAT32(14785, 1));
            Assert.AreEqual(29360, Inlines.silk_LSHIFT_SAT32(14680, 1));
            Assert.AreEqual(-1472, Inlines.silk_LSHIFT_SAT32(-92, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(288, Inlines.silk_LSHIFT_SAT32(18, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(448, Inlines.silk_LSHIFT_SAT32(28, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(704, Inlines.silk_LSHIFT_SAT32(44, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(266141696, Inlines.silk_LSHIFT_SAT32(4061, 16));
            Assert.AreEqual(261226496, Inlines.silk_LSHIFT_SAT32(3986, 16));
            Assert.AreEqual(252575744, Inlines.silk_LSHIFT_SAT32(3854, 16));
            Assert.AreEqual(298909696, Inlines.silk_LSHIFT_SAT32(4561, 16));
            Assert.AreEqual(1652031488, Inlines.silk_LSHIFT_SAT32(12604, 17));
            Assert.AreEqual(769392640, Inlines.silk_LSHIFT_SAT32(11740, 16));
            Assert.AreEqual(759562240, Inlines.silk_LSHIFT_SAT32(11590, 16));
            Assert.AreEqual(1492516864, Inlines.silk_LSHIFT_SAT32(11387, 17));
            Assert.AreEqual(32186, Inlines.silk_LSHIFT_SAT32(16093, 1));
            Assert.AreEqual(59376, Inlines.silk_LSHIFT_SAT32(29688, 1));
            Assert.AreEqual(51934, Inlines.silk_LSHIFT_SAT32(25967, 1));
            Assert.AreEqual(384146, Inlines.silk_LSHIFT_SAT32(192073, 1));
            Assert.AreEqual(-53456, Inlines.silk_LSHIFT_SAT32(-3341, 4));
            Assert.AreEqual(-576, Inlines.silk_LSHIFT_SAT32(-36, 4));
            Assert.AreEqual(5168, Inlines.silk_LSHIFT_SAT32(323, 4));
            Assert.AreEqual(16, Inlines.silk_LSHIFT_SAT32(1, 4));
            Assert.AreEqual(8384, Inlines.silk_LSHIFT_SAT32(524, 4));
            Assert.AreEqual(48, Inlines.silk_LSHIFT_SAT32(3, 4));
            Assert.AreEqual(-12576, Inlines.silk_LSHIFT_SAT32(-786, 4));
            Assert.AreEqual(-17744, Inlines.silk_LSHIFT_SAT32(-1109, 4));
            Assert.AreEqual(319029248, Inlines.silk_LSHIFT_SAT32(4868, 16));
            Assert.AreEqual(138084352, Inlines.silk_LSHIFT_SAT32(2107, 16));
            Assert.AreEqual(137232384, Inlines.silk_LSHIFT_SAT32(2094, 16));
            Assert.AreEqual(540344320, Inlines.silk_LSHIFT_SAT32(8245, 16));
            Assert.AreEqual(1064566784, Inlines.silk_LSHIFT_SAT32(8122, 17));
            Assert.AreEqual(417923072, Inlines.silk_LSHIFT_SAT32(12754, 15));
            Assert.AreEqual(1509949440, Inlines.silk_LSHIFT_SAT32(11520, 17));
            Assert.AreEqual(1956904960, Inlines.silk_LSHIFT_SAT32(7465, 18));
            Assert.AreEqual(187432960, Inlines.silk_LSHIFT_SAT32(2860, 16));
            Assert.AreEqual(72941568, Inlines.silk_LSHIFT_SAT32(1113, 16));
            Assert.AreEqual(542048256, Inlines.silk_LSHIFT_SAT32(8271, 16));
            Assert.AreEqual(580452352, Inlines.silk_LSHIFT_SAT32(8857, 16));
            Assert.AreEqual(2006188032, Inlines.silk_LSHIFT_SAT32(7653, 18));
            Assert.AreEqual(2030829568, Inlines.silk_LSHIFT_SAT32(7747, 18));
            Assert.AreEqual(2025848832, Inlines.silk_LSHIFT_SAT32(7728, 18));
            Assert.AreEqual(2015887360, Inlines.silk_LSHIFT_SAT32(7690, 18));
            Assert.AreEqual(41116, Inlines.silk_LSHIFT_SAT32(20558, 1));
            Assert.AreEqual(19596, Inlines.silk_LSHIFT_SAT32(9798, 1));
            Assert.AreEqual(10264, Inlines.silk_LSHIFT_SAT32(5132, 1));
            Assert.AreEqual(28476, Inlines.silk_LSHIFT_SAT32(14238, 1));
            Assert.AreEqual(208, Inlines.silk_LSHIFT_SAT32(13, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(-6496, Inlines.silk_LSHIFT_SAT32(-406, 4));
            Assert.AreEqual(-32, Inlines.silk_LSHIFT_SAT32(-2, 4));
            Assert.AreEqual(11872, Inlines.silk_LSHIFT_SAT32(742, 4));
            Assert.AreEqual(192, Inlines.silk_LSHIFT_SAT32(12, 4));
            Assert.AreEqual(1888, Inlines.silk_LSHIFT_SAT32(118, 4));
            Assert.AreEqual(16, Inlines.silk_LSHIFT_SAT32(1, 4));
            Assert.AreEqual(269287424, Inlines.silk_LSHIFT_SAT32(4109, 16));
            Assert.AreEqual(283246592, Inlines.silk_LSHIFT_SAT32(4322, 16));
            Assert.AreEqual(260571136, Inlines.silk_LSHIFT_SAT32(3976, 16));
            Assert.AreEqual(255066112, Inlines.silk_LSHIFT_SAT32(3892, 16));
            Assert.AreEqual(2006188032, Inlines.silk_LSHIFT_SAT32(7653, 18));
            Assert.AreEqual(1553596416, Inlines.silk_LSHIFT_SAT32(11853, 17));
            Assert.AreEqual(409272320, Inlines.silk_LSHIFT_SAT32(6245, 16));
            Assert.AreEqual(1052377088, Inlines.silk_LSHIFT_SAT32(16058, 16));
            Assert.AreEqual(24412, Inlines.silk_LSHIFT_SAT32(12206, 1));
            Assert.AreEqual(39954, Inlines.silk_LSHIFT_SAT32(19977, 1));
            Assert.AreEqual(216602, Inlines.silk_LSHIFT_SAT32(108301, 1));
            Assert.AreEqual(30296, Inlines.silk_LSHIFT_SAT32(15148, 1));
            Assert.AreEqual(-48176, Inlines.silk_LSHIFT_SAT32(-3011, 4));
            Assert.AreEqual(-512, Inlines.silk_LSHIFT_SAT32(-32, 4));
            Assert.AreEqual(-49344, Inlines.silk_LSHIFT_SAT32(-3084, 4));
            Assert.AreEqual(-496, Inlines.silk_LSHIFT_SAT32(-31, 4));
            Assert.AreEqual(11552, Inlines.silk_LSHIFT_SAT32(722, 4));
            Assert.AreEqual(64, Inlines.silk_LSHIFT_SAT32(4, 4));
            Assert.AreEqual(13120, Inlines.silk_LSHIFT_SAT32(820, 4));
            Assert.AreEqual(64, Inlines.silk_LSHIFT_SAT32(4, 4));
            Assert.AreEqual(318111744, Inlines.silk_LSHIFT_SAT32(4854, 16));
            Assert.AreEqual(247070720, Inlines.silk_LSHIFT_SAT32(3770, 16));
            Assert.AreEqual(76218368, Inlines.silk_LSHIFT_SAT32(1163, 16));
            Assert.AreEqual(175177728, Inlines.silk_LSHIFT_SAT32(2673, 16));
            Assert.AreEqual(1506410496, Inlines.silk_LSHIFT_SAT32(11493, 17));
            Assert.AreEqual(766967808, Inlines.silk_LSHIFT_SAT32(11703, 16));
            Assert.AreEqual(762052608, Inlines.silk_LSHIFT_SAT32(11628, 16));
            Assert.AreEqual(1617559552, Inlines.silk_LSHIFT_SAT32(12341, 17));
            Assert.AreEqual(549060608, Inlines.silk_LSHIFT_SAT32(8378, 16));
            Assert.AreEqual(133038080, Inlines.silk_LSHIFT_SAT32(2030, 16));
            Assert.AreEqual(132710400, Inlines.silk_LSHIFT_SAT32(2025, 16));
            Assert.AreEqual(554303488, Inlines.silk_LSHIFT_SAT32(8458, 16));
            Assert.AreEqual(1898053632, Inlines.silk_LSHIFT_SAT32(14481, 17));
            Assert.AreEqual(1942224896, Inlines.silk_LSHIFT_SAT32(7409, 18));
            Assert.AreEqual(1971585024, Inlines.silk_LSHIFT_SAT32(7521, 18));
            Assert.AreEqual(1986527232, Inlines.silk_LSHIFT_SAT32(7578, 18));
            Assert.AreEqual(298342, Inlines.silk_LSHIFT_SAT32(149171, 1));
            Assert.AreEqual(34880, Inlines.silk_LSHIFT_SAT32(17440, 1));
            Assert.AreEqual(27250, Inlines.silk_LSHIFT_SAT32(13625, 1));
            Assert.AreEqual(26872, Inlines.silk_LSHIFT_SAT32(13436, 1));
            Assert.AreEqual(-13008, Inlines.silk_LSHIFT_SAT32(-813, 4));
            Assert.AreEqual(-11200, Inlines.silk_LSHIFT_SAT32(-700, 4));
            Assert.AreEqual(-672, Inlines.silk_LSHIFT_SAT32(-42, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(-736, Inlines.silk_LSHIFT_SAT32(-46, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(1360, Inlines.silk_LSHIFT_SAT32(85, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(557776896, Inlines.silk_LSHIFT_SAT32(8511, 16));
            Assert.AreEqual(298909696, Inlines.silk_LSHIFT_SAT32(4561, 16));
            Assert.AreEqual(298057728, Inlines.silk_LSHIFT_SAT32(4548, 16));
            Assert.AreEqual(299827200, Inlines.silk_LSHIFT_SAT32(4575, 16));
            Assert.AreEqual(2015887360, Inlines.silk_LSHIFT_SAT32(7690, 18));
            Assert.AreEqual(2035548160, Inlines.silk_LSHIFT_SAT32(7765, 18));
            Assert.AreEqual(1642201088, Inlines.silk_LSHIFT_SAT32(12529, 17));
            Assert.AreEqual(1514274816, Inlines.silk_LSHIFT_SAT32(11553, 17));
            Assert.AreEqual(25430, Inlines.silk_LSHIFT_SAT32(12715, 1));
            Assert.AreEqual(17194, Inlines.silk_LSHIFT_SAT32(8597, 1));
            Assert.AreEqual(14504, Inlines.silk_LSHIFT_SAT32(7252, 1));
            Assert.AreEqual(494212, Inlines.silk_LSHIFT_SAT32(247106, 1));
            Assert.AreEqual(2272, Inlines.silk_LSHIFT_SAT32(142, 4));
            Assert.AreEqual(16, Inlines.silk_LSHIFT_SAT32(1, 4));
            Assert.AreEqual(-5888, Inlines.silk_LSHIFT_SAT32(-368, 4));
            Assert.AreEqual(-16, Inlines.silk_LSHIFT_SAT32(-1, 4));
            Assert.AreEqual(11024, Inlines.silk_LSHIFT_SAT32(689, 4));
            Assert.AreEqual(176, Inlines.silk_LSHIFT_SAT32(11, 4));
            Assert.AreEqual(66896, Inlines.silk_LSHIFT_SAT32(4181, 4));
            Assert.AreEqual(133824, Inlines.silk_LSHIFT_SAT32(8364, 4));
            Assert.AreEqual(319029248, Inlines.silk_LSHIFT_SAT32(4868, 16));
            Assert.AreEqual(337313792, Inlines.silk_LSHIFT_SAT32(5147, 16));
            Assert.AreEqual(255066112, Inlines.silk_LSHIFT_SAT32(3892, 16));
            Assert.AreEqual(629342208, Inlines.silk_LSHIFT_SAT32(9603, 16));
            Assert.AreEqual(1661861888, Inlines.silk_LSHIFT_SAT32(12679, 17));
            Assert.AreEqual(1563426816, Inlines.silk_LSHIFT_SAT32(11928, 17));
            Assert.AreEqual(1558446080, Inlines.silk_LSHIFT_SAT32(11890, 17));
            Assert.AreEqual(1661861888, Inlines.silk_LSHIFT_SAT32(12679, 17));
            Assert.AreEqual(18134, Inlines.silk_LSHIFT_SAT32(9067, 1));
            Assert.AreEqual(44472, Inlines.silk_LSHIFT_SAT32(22236, 1));
            Assert.AreEqual(43450, Inlines.silk_LSHIFT_SAT32(21725, 1));
            Assert.AreEqual(952, Inlines.silk_LSHIFT_SAT32(476, 1));
            Assert.AreEqual(38944, Inlines.silk_LSHIFT_SAT32(2434, 4));
            Assert.AreEqual(368, Inlines.silk_LSHIFT_SAT32(23, 4));
            Assert.AreEqual(-27216, Inlines.silk_LSHIFT_SAT32(-1701, 4));
            Assert.AreEqual(-288, Inlines.silk_LSHIFT_SAT32(-18, 4));
            Assert.AreEqual(-27952, Inlines.silk_LSHIFT_SAT32(-1747, 4));
            Assert.AreEqual(-288, Inlines.silk_LSHIFT_SAT32(-18, 4));
            Assert.AreEqual(-33296, Inlines.silk_LSHIFT_SAT32(-2081, 4));
            Assert.AreEqual(-240, Inlines.silk_LSHIFT_SAT32(-15, 4));
            Assert.AreEqual(233504768, Inlines.silk_LSHIFT_SAT32(3563, 16));
            Assert.AreEqual(212008960, Inlines.silk_LSHIFT_SAT32(3235, 16));
            Assert.AreEqual(209518592, Inlines.silk_LSHIFT_SAT32(3197, 16));
            Assert.AreEqual(223084544, Inlines.silk_LSHIFT_SAT32(3404, 16));
            Assert.AreEqual(1514274816, Inlines.silk_LSHIFT_SAT32(11553, 17));
            Assert.AreEqual(769392640, Inlines.silk_LSHIFT_SAT32(11740, 16));
            Assert.AreEqual(37584896, Inlines.silk_LSHIFT_SAT32(2294, 14));
            Assert.AreEqual(28803072, Inlines.silk_LSHIFT_SAT32(3516, 13));
            Assert.AreEqual(1264, Inlines.silk_LSHIFT_SAT32(632, 1));
            Assert.AreEqual(802, Inlines.silk_LSHIFT_SAT32(401, 1));
            Assert.AreEqual(274312, Inlines.silk_LSHIFT_SAT32(137156, 1));
            Assert.AreEqual(878150, Inlines.silk_LSHIFT_SAT32(439075, 1));
            Assert.AreEqual(-41968, Inlines.silk_LSHIFT_SAT32(-2623, 4));
            Assert.AreEqual(-448, Inlines.silk_LSHIFT_SAT32(-28, 4));
            Assert.AreEqual(-7632, Inlines.silk_LSHIFT_SAT32(-477, 4));
            Assert.AreEqual(-16, Inlines.silk_LSHIFT_SAT32(-1, 4));
            Assert.AreEqual(11792, Inlines.silk_LSHIFT_SAT32(737, 4));
            Assert.AreEqual(480, Inlines.silk_LSHIFT_SAT32(30, 4));
            Assert.AreEqual(19824, Inlines.silk_LSHIFT_SAT32(1239, 4));
            Assert.AreEqual(112, Inlines.silk_LSHIFT_SAT32(7, 4));
            Assert.AreEqual(290193408, Inlines.silk_LSHIFT_SAT32(4428, 16));
            Assert.AreEqual(143327232, Inlines.silk_LSHIFT_SAT32(2187, 16));
            Assert.AreEqual(13434880, Inlines.silk_LSHIFT_SAT32(205, 16));
            Assert.AreEqual(31391744, Inlines.silk_LSHIFT_SAT32(479, 16));
            Assert.AreEqual(27965440, Inlines.silk_LSHIFT_SAT32(13655, 11));
            Assert.AreEqual(27658240, Inlines.silk_LSHIFT_SAT32(13505, 11));
            Assert.AreEqual(25274368, Inlines.silk_LSHIFT_SAT32(12341, 11));
            Assert.AreEqual(1042546688, Inlines.silk_LSHIFT_SAT32(15908, 16));
            Assert.AreEqual(9751808, Inlines.silk_LSHIFT_SAT32(38093, 8));
            Assert.AreEqual(9560832, Inlines.silk_LSHIFT_SAT32(37347, 8));
            Assert.AreEqual(9070080, Inlines.silk_LSHIFT_SAT32(35430, 8));
            Assert.AreEqual(204013568, Inlines.silk_LSHIFT_SAT32(3113, 16));
            Assert.AreEqual(1495924736, Inlines.silk_LSHIFT_SAT32(11413, 17));
            Assert.AreEqual(1612578816, Inlines.silk_LSHIFT_SAT32(12303, 17));
            Assert.AreEqual(1622540288, Inlines.silk_LSHIFT_SAT32(12379, 17));
            Assert.AreEqual(1514274816, Inlines.silk_LSHIFT_SAT32(11553, 17));
            Assert.AreEqual(534773760, Inlines.silk_LSHIFT_SAT32(8160, 16));
            Assert.AreEqual(483065856, Inlines.silk_LSHIFT_SAT32(7371, 16));
            Assert.AreEqual(367853568, Inlines.silk_LSHIFT_SAT32(5613, 16));
            Assert.AreEqual(532283392, Inlines.silk_LSHIFT_SAT32(8122, 16));
            Assert.AreEqual(1652031488, Inlines.silk_LSHIFT_SAT32(12604, 17));
            Assert.AreEqual(1506410496, Inlines.silk_LSHIFT_SAT32(11493, 17));
            Assert.AreEqual(408027136, Inlines.silk_LSHIFT_SAT32(6226, 16));
            Assert.AreEqual(33030144, Inlines.silk_LSHIFT_SAT32(2016, 14));
            Assert.AreEqual(12, Inlines.silk_LSHIFT_SAT32(6, 1));
            Assert.AreEqual(1260, Inlines.silk_LSHIFT_SAT32(630, 1));
            Assert.AreEqual(6546, Inlines.silk_LSHIFT_SAT32(3273, 1));
            Assert.AreEqual(33642, Inlines.silk_LSHIFT_SAT32(16821, 1));
            Assert.AreEqual(-55184, Inlines.silk_LSHIFT_SAT32(-3449, 4));
            Assert.AreEqual(-560, Inlines.silk_LSHIFT_SAT32(-35, 4));
            Assert.AreEqual(-41936, Inlines.silk_LSHIFT_SAT32(-2621, 4));
            Assert.AreEqual(-448, Inlines.silk_LSHIFT_SAT32(-28, 4));
            Assert.AreEqual(20224, Inlines.silk_LSHIFT_SAT32(1264, 4));
            Assert.AreEqual(112, Inlines.silk_LSHIFT_SAT32(7, 4));
            Assert.AreEqual(11184, Inlines.silk_LSHIFT_SAT32(699, 4));
            Assert.AreEqual(96, Inlines.silk_LSHIFT_SAT32(6, 4));
            Assert.AreEqual(271908864, Inlines.silk_LSHIFT_SAT32(4149, 16));
            Assert.AreEqual(246415360, Inlines.silk_LSHIFT_SAT32(3760, 16));
            Assert.AreEqual(81068032, Inlines.silk_LSHIFT_SAT32(1237, 16));
            Assert.AreEqual(14483456, Inlines.silk_LSHIFT_SAT32(221, 16));
            Assert.AreEqual(29962240, Inlines.silk_LSHIFT_SAT32(7315, 12));
            Assert.AreEqual(25120768, Inlines.silk_LSHIFT_SAT32(12266, 11));
            Assert.AreEqual(185679872, Inlines.silk_LSHIFT_SAT32(11333, 14));
            Assert.AreEqual(1686372352, Inlines.silk_LSHIFT_SAT32(6433, 18));
            Assert.AreEqual(8164608, Inlines.silk_LSHIFT_SAT32(31893, 8));
            Assert.AreEqual(9315584, Inlines.silk_LSHIFT_SAT32(36389, 8));
            Assert.AreEqual(32768000, Inlines.silk_LSHIFT_SAT32(500, 16));
            Assert.AreEqual(564789248, Inlines.silk_LSHIFT_SAT32(8618, 16));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8724, 18));
            Assert.AreEqual(2129133568, Inlines.silk_LSHIFT_SAT32(8122, 18));
            Assert.AreEqual(2079850496, Inlines.silk_LSHIFT_SAT32(7934, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8192, 18));
            Assert.AreEqual(229408, Inlines.silk_LSHIFT_SAT32(28676, 3));
            Assert.AreEqual(282464, Inlines.silk_LSHIFT_SAT32(35308, 3));
            Assert.AreEqual(297184, Inlines.silk_LSHIFT_SAT32(37148, 3));
            Assert.AreEqual(275896, Inlines.silk_LSHIFT_SAT32(34487, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-39776, Inlines.silk_LSHIFT_SAT32(-2486, 4));
            Assert.AreEqual(-30976, Inlines.silk_LSHIFT_SAT32(-1936, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-54080, Inlines.silk_LSHIFT_SAT32(-3380, 4));
            Assert.AreEqual(-44000, Inlines.silk_LSHIFT_SAT32(-2750, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-53920, Inlines.silk_LSHIFT_SAT32(-3370, 4));
            Assert.AreEqual(-44640, Inlines.silk_LSHIFT_SAT32(-2790, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-61920, Inlines.silk_LSHIFT_SAT32(-3870, 4));
            Assert.AreEqual(-48528, Inlines.silk_LSHIFT_SAT32(-3033, 4));
            Assert.AreEqual(796459008, Inlines.silk_LSHIFT_SAT32(12153, 16));
            Assert.AreEqual(816185344, Inlines.silk_LSHIFT_SAT32(12454, 16));
            Assert.AreEqual(779223040, Inlines.silk_LSHIFT_SAT32(11890, 16));
            Assert.AreEqual(784203776, Inlines.silk_LSHIFT_SAT32(11966, 16));
            Assert.AreEqual(2099511296, Inlines.silk_LSHIFT_SAT32(8009, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(9975, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(10694, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8857, 18));
            Assert.AreEqual(273832, Inlines.silk_LSHIFT_SAT32(34229, 3));
            Assert.AreEqual(108744, Inlines.silk_LSHIFT_SAT32(13593, 3));
            Assert.AreEqual(357624, Inlines.silk_LSHIFT_SAT32(44703, 3));
            Assert.AreEqual(504952, Inlines.silk_LSHIFT_SAT32(63119, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-53488, Inlines.silk_LSHIFT_SAT32(-3343, 4));
            Assert.AreEqual(-44224, Inlines.silk_LSHIFT_SAT32(-2764, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-81392, Inlines.silk_LSHIFT_SAT32(-5087, 4));
            Assert.AreEqual(-55440, Inlines.silk_LSHIFT_SAT32(-3465, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-9984, Inlines.silk_LSHIFT_SAT32(-624, 4));
            Assert.AreEqual(-14496, Inlines.silk_LSHIFT_SAT32(-906, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(21792, Inlines.silk_LSHIFT_SAT32(1362, 4));
            Assert.AreEqual(43584, Inlines.silk_LSHIFT_SAT32(2724, 4));
            Assert.AreEqual(784203776, Inlines.silk_LSHIFT_SAT32(11966, 16));
            Assert.AreEqual(840761344, Inlines.silk_LSHIFT_SAT32(12829, 16));
            Assert.AreEqual(808779776, Inlines.silk_LSHIFT_SAT32(12341, 16));
            Assert.AreEqual(889978880, Inlines.silk_LSHIFT_SAT32(13580, 16));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8857, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8884, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(9363, 18));
            Assert.AreEqual(54256, Inlines.silk_LSHIFT_SAT32(6782, 3));
            Assert.AreEqual(60552, Inlines.silk_LSHIFT_SAT32(7569, 3));
            Assert.AreEqual(54648, Inlines.silk_LSHIFT_SAT32(6831, 3));
            Assert.AreEqual(60352, Inlines.silk_LSHIFT_SAT32(7544, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-61568, Inlines.silk_LSHIFT_SAT32(-3848, 4));
            Assert.AreEqual(-45712, Inlines.silk_LSHIFT_SAT32(-2857, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-62928, Inlines.silk_LSHIFT_SAT32(-3933, 4));
            Assert.AreEqual(-46416, Inlines.silk_LSHIFT_SAT32(-2901, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-61888, Inlines.silk_LSHIFT_SAT32(-3868, 4));
            Assert.AreEqual(-45888, Inlines.silk_LSHIFT_SAT32(-2868, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-62864, Inlines.silk_LSHIFT_SAT32(-3929, 4));
            Assert.AreEqual(-46432, Inlines.silk_LSHIFT_SAT32(-2902, 4));
            Assert.AreEqual(643301376, Inlines.silk_LSHIFT_SAT32(9816, 16));
            Assert.AreEqual(721813504, Inlines.silk_LSHIFT_SAT32(11014, 16));
            Assert.AreEqual(646774784, Inlines.silk_LSHIFT_SAT32(9869, 16));
            Assert.AreEqual(727056384, Inlines.silk_LSHIFT_SAT32(11094, 16));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(10561, 18));
            Assert.AreEqual(2139095040, Inlines.silk_LSHIFT_SAT32(8160, 18));
            Assert.AreEqual(2104492032, Inlines.silk_LSHIFT_SAT32(8028, 18));
            Assert.AreEqual(410888, Inlines.silk_LSHIFT_SAT32(51361, 3));
            Assert.AreEqual(111872, Inlines.silk_LSHIFT_SAT32(13984, 3));
            Assert.AreEqual(342968, Inlines.silk_LSHIFT_SAT32(42871, 3));
            Assert.AreEqual(269616, Inlines.silk_LSHIFT_SAT32(33702, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(20080, Inlines.silk_LSHIFT_SAT32(1255, 4));
            Assert.AreEqual(40160, Inlines.silk_LSHIFT_SAT32(2510, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-70768, Inlines.silk_LSHIFT_SAT32(-4423, 4));
            Assert.AreEqual(-51088, Inlines.silk_LSHIFT_SAT32(-3193, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-45120, Inlines.silk_LSHIFT_SAT32(-2820, 4));
            Assert.AreEqual(-40576, Inlines.silk_LSHIFT_SAT32(-2536, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-54512, Inlines.silk_LSHIFT_SAT32(-3407, 4));
            Assert.AreEqual(-45040, Inlines.silk_LSHIFT_SAT32(-2815, 4));
            Assert.AreEqual(833421312, Inlines.silk_LSHIFT_SAT32(12717, 16));
            Assert.AreEqual(843251712, Inlines.silk_LSHIFT_SAT32(12867, 16));
            Assert.AreEqual(803864576, Inlines.silk_LSHIFT_SAT32(12266, 16));
            Assert.AreEqual(791543808, Inlines.silk_LSHIFT_SAT32(12078, 16));
            Assert.AreEqual(2139095040, Inlines.silk_LSHIFT_SAT32(8160, 18));
            Assert.AreEqual(2129133568, Inlines.silk_LSHIFT_SAT32(8122, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(10561, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(10082, 18));
            Assert.AreEqual(53800, Inlines.silk_LSHIFT_SAT32(6725, 3));
            Assert.AreEqual(268408, Inlines.silk_LSHIFT_SAT32(33551, 3));
            Assert.AreEqual(262336, Inlines.silk_LSHIFT_SAT32(32792, 3));
            Assert.AreEqual(556040, Inlines.silk_LSHIFT_SAT32(69505, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-62144, Inlines.silk_LSHIFT_SAT32(-3884, 4));
            Assert.AreEqual(-46272, Inlines.silk_LSHIFT_SAT32(-2892, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-57120, Inlines.silk_LSHIFT_SAT32(-3570, 4));
            Assert.AreEqual(-45872, Inlines.silk_LSHIFT_SAT32(-2867, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-56560, Inlines.silk_LSHIFT_SAT32(-3535, 4));
            Assert.AreEqual(-46352, Inlines.silk_LSHIFT_SAT32(-2897, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(22080, Inlines.silk_LSHIFT_SAT32(1380, 4));
            Assert.AreEqual(44160, Inlines.silk_LSHIFT_SAT32(2760, 4));
            Assert.AreEqual(764477440, Inlines.silk_LSHIFT_SAT32(11665, 16));
            Assert.AreEqual(791543808, Inlines.silk_LSHIFT_SAT32(12078, 16));
            Assert.AreEqual(830930944, Inlines.silk_LSHIFT_SAT32(12679, 16));
            Assert.AreEqual(936771584, Inlines.silk_LSHIFT_SAT32(14294, 16));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8884, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8857, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8884, 18));
            Assert.AreEqual(51184, Inlines.silk_LSHIFT_SAT32(6398, 3));
            Assert.AreEqual(57216, Inlines.silk_LSHIFT_SAT32(7152, 3));
            Assert.AreEqual(51120, Inlines.silk_LSHIFT_SAT32(6390, 3));
            Assert.AreEqual(57304, Inlines.silk_LSHIFT_SAT32(7163, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-61520, Inlines.silk_LSHIFT_SAT32(-3845, 4));
            Assert.AreEqual(-45776, Inlines.silk_LSHIFT_SAT32(-2861, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-62528, Inlines.silk_LSHIFT_SAT32(-3908, 4));
            Assert.AreEqual(-46000, Inlines.silk_LSHIFT_SAT32(-2875, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-61936, Inlines.silk_LSHIFT_SAT32(-3871, 4));
            Assert.AreEqual(-46160, Inlines.silk_LSHIFT_SAT32(-2885, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-62928, Inlines.silk_LSHIFT_SAT32(-3933, 4));
            Assert.AreEqual(-46208, Inlines.silk_LSHIFT_SAT32(-2888, 4));
            Assert.AreEqual(645005312, Inlines.silk_LSHIFT_SAT32(9842, 16));
            Assert.AreEqual(727056384, Inlines.silk_LSHIFT_SAT32(11094, 16));
            Assert.AreEqual(643301376, Inlines.silk_LSHIFT_SAT32(9816, 16));
            Assert.AreEqual(728760320, Inlines.silk_LSHIFT_SAT32(11120, 16));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8192, 18));
            Assert.AreEqual(1897922560, Inlines.silk_LSHIFT_SAT32(7240, 18));
            Assert.AreEqual(1543766016, Inlines.silk_LSHIFT_SAT32(11778, 17));
            Assert.AreEqual(1652031488, Inlines.silk_LSHIFT_SAT32(12604, 17));
            Assert.AreEqual(39288, Inlines.silk_LSHIFT_SAT32(4911, 3));
            Assert.AreEqual(181016, Inlines.silk_LSHIFT_SAT32(22627, 3));
            Assert.AreEqual(372220, Inlines.silk_LSHIFT_SAT32(186110, 1));
            Assert.AreEqual(20498, Inlines.silk_LSHIFT_SAT32(10249, 1));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-66880, Inlines.silk_LSHIFT_SAT32(-4180, 4));
            Assert.AreEqual(-49856, Inlines.silk_LSHIFT_SAT32(-3116, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-25072, Inlines.silk_LSHIFT_SAT32(-1567, 4));
            Assert.AreEqual(-33904, Inlines.silk_LSHIFT_SAT32(-2119, 4));
            Assert.AreEqual(-45424, Inlines.silk_LSHIFT_SAT32(-2839, 4));
            Assert.AreEqual(-65248, Inlines.silk_LSHIFT_SAT32(-4078, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(732299264, Inlines.silk_LSHIFT_SAT32(11174, 16));
            Assert.AreEqual(549060608, Inlines.silk_LSHIFT_SAT32(8378, 16));
            Assert.AreEqual(550830080, Inlines.silk_LSHIFT_SAT32(8405, 16));
            Assert.AreEqual(291110912, Inlines.silk_LSHIFT_SAT32(4442, 16));
            Assert.AreEqual(1509949440, Inlines.silk_LSHIFT_SAT32(11520, 17));
            Assert.AreEqual(1622540288, Inlines.silk_LSHIFT_SAT32(12379, 17));
            Assert.AreEqual(1514274816, Inlines.silk_LSHIFT_SAT32(11553, 17));
            Assert.AreEqual(100146, Inlines.silk_LSHIFT_SAT32(50073, 1));
            Assert.AreEqual(24906, Inlines.silk_LSHIFT_SAT32(12453, 1));
            Assert.AreEqual(14102, Inlines.silk_LSHIFT_SAT32(7051, 1));
            Assert.AreEqual(48106, Inlines.silk_LSHIFT_SAT32(24053, 1));
            Assert.AreEqual(9232, Inlines.silk_LSHIFT_SAT32(577, 4));
            Assert.AreEqual(7232, Inlines.silk_LSHIFT_SAT32(452, 4));
            Assert.AreEqual(-7536, Inlines.silk_LSHIFT_SAT32(-471, 4));
            Assert.AreEqual(-32, Inlines.silk_LSHIFT_SAT32(-2, 4));
            Assert.AreEqual(6896, Inlines.silk_LSHIFT_SAT32(431, 4));
            Assert.AreEqual(96, Inlines.silk_LSHIFT_SAT32(6, 4));
            Assert.AreEqual(7488, Inlines.silk_LSHIFT_SAT32(468, 4));
            Assert.AreEqual(80, Inlines.silk_LSHIFT_SAT32(5, 4));
            Assert.AreEqual(302448640, Inlines.silk_LSHIFT_SAT32(4615, 16));
            Assert.AreEqual(213843968, Inlines.silk_LSHIFT_SAT32(3263, 16));
            Assert.AreEqual(216268800, Inlines.silk_LSHIFT_SAT32(3300, 16));
            Assert.AreEqual(200933376, Inlines.silk_LSHIFT_SAT32(3066, 16));
            Assert.AreEqual(1073741824, Inlines.silk_LSHIFT_SAT32(8192, 17));
            Assert.AreEqual(45432832, Inlines.silk_LSHIFT_SAT32(2773, 14));
            Assert.AreEqual(34856960, Inlines.silk_LSHIFT_SAT32(4255, 13));
            Assert.AreEqual(29657088, Inlines.silk_LSHIFT_SAT32(14481, 11));
            Assert.AreEqual(218759168, Inlines.silk_LSHIFT_SAT32(3338, 16));
            Assert.AreEqual(12713984, Inlines.silk_LSHIFT_SAT32(194, 16));
            Assert.AreEqual(11305728, Inlines.silk_LSHIFT_SAT32(44163, 8));
            Assert.AreEqual(11823872, Inlines.silk_LSHIFT_SAT32(46187, 8));
            Assert.AreEqual(26120192, Inlines.silk_LSHIFT_SAT32(12754, 11));
            Assert.AreEqual(26734592, Inlines.silk_LSHIFT_SAT32(13054, 11));
            Assert.AreEqual(753205248, Inlines.silk_LSHIFT_SAT32(11493, 16));
            Assert.AreEqual(1482031104, Inlines.silk_LSHIFT_SAT32(11307, 17));
            Assert.AreEqual(9179136, Inlines.silk_LSHIFT_SAT32(35856, 8));
            Assert.AreEqual(9778944, Inlines.silk_LSHIFT_SAT32(38199, 8));
            Assert.AreEqual(134217728, Inlines.silk_LSHIFT_SAT32(2048, 16));
            Assert.AreEqual(564789248, Inlines.silk_LSHIFT_SAT32(8618, 16));
            Assert.AreEqual(1647050752, Inlines.silk_LSHIFT_SAT32(12566, 17));
            Assert.AreEqual(1558446080, Inlines.silk_LSHIFT_SAT32(11890, 17));
            Assert.AreEqual(1553596416, Inlines.silk_LSHIFT_SAT32(11853, 17));
            Assert.AreEqual(1656881152, Inlines.silk_LSHIFT_SAT32(12641, 17));
            Assert.AreEqual(285868032, Inlines.silk_LSHIFT_SAT32(4362, 16));
            Assert.AreEqual(556007424, Inlines.silk_LSHIFT_SAT32(8484, 16));
            Assert.AreEqual(552534016, Inlines.silk_LSHIFT_SAT32(8431, 16));
            Assert.AreEqual(291962880, Inlines.silk_LSHIFT_SAT32(4455, 16));
            Assert.AreEqual(1516896256, Inlines.silk_LSHIFT_SAT32(11573, 17));
            Assert.AreEqual(766967808, Inlines.silk_LSHIFT_SAT32(11703, 16));
            Assert.AreEqual(36814848, Inlines.silk_LSHIFT_SAT32(2247, 14));
            Assert.AreEqual(27033600, Inlines.silk_LSHIFT_SAT32(3300, 13));
            Assert.AreEqual(550830080, Inlines.silk_LSHIFT_SAT32(8405, 16));
            Assert.AreEqual(135069696, Inlines.silk_LSHIFT_SAT32(2061, 16));
            Assert.AreEqual(10842368, Inlines.silk_LSHIFT_SAT32(42353, 8));
            Assert.AreEqual(8851968, Inlines.silk_LSHIFT_SAT32(34578, 8));
            Assert.AreEqual(25427968, Inlines.silk_LSHIFT_SAT32(12416, 11));
            Assert.AreEqual(22337536, Inlines.silk_LSHIFT_SAT32(10907, 11));
            Assert.AreEqual(1482031104, Inlines.silk_LSHIFT_SAT32(11307, 17));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8325, 18));
            Assert.AreEqual(8715776, Inlines.silk_LSHIFT_SAT32(34046, 8));
            Assert.AreEqual(9669888, Inlines.silk_LSHIFT_SAT32(37773, 8));
            Assert.AreEqual(256901120, Inlines.silk_LSHIFT_SAT32(3920, 16));
            Assert.AreEqual(904724480, Inlines.silk_LSHIFT_SAT32(13805, 16));
            Assert.AreEqual(1991245824, Inlines.silk_LSHIFT_SAT32(7596, 18));
            Assert.AreEqual(1981546496, Inlines.silk_LSHIFT_SAT32(7559, 18));
            Assert.AreEqual(2030829568, Inlines.silk_LSHIFT_SAT32(7747, 18));
            Assert.AreEqual(1971585024, Inlines.silk_LSHIFT_SAT32(7521, 18));
            Assert.AreEqual(278264, Inlines.silk_LSHIFT_SAT32(34783, 3));
            Assert.AreEqual(358568, Inlines.silk_LSHIFT_SAT32(44821, 3));
            Assert.AreEqual(62224, Inlines.silk_LSHIFT_SAT32(7778, 3));
            Assert.AreEqual(364432, Inlines.silk_LSHIFT_SAT32(45554, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-37584, Inlines.silk_LSHIFT_SAT32(-2349, 4));
            Assert.AreEqual(-29744, Inlines.silk_LSHIFT_SAT32(-1859, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-64672, Inlines.silk_LSHIFT_SAT32(-4042, 4));
            Assert.AreEqual(-48272, Inlines.silk_LSHIFT_SAT32(-3017, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-59168, Inlines.silk_LSHIFT_SAT32(-3698, 4));
            Assert.AreEqual(-43728, Inlines.silk_LSHIFT_SAT32(-2733, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-58400, Inlines.silk_LSHIFT_SAT32(-3650, 4));
            Assert.AreEqual(-47312, Inlines.silk_LSHIFT_SAT32(-2957, 4));
            Assert.AreEqual(774307840, Inlines.silk_LSHIFT_SAT32(11815, 16));
            Assert.AreEqual(833421312, Inlines.silk_LSHIFT_SAT32(12717, 16));
            Assert.AreEqual(741015552, Inlines.silk_LSHIFT_SAT32(11307, 16));
            Assert.AreEqual(764477440, Inlines.silk_LSHIFT_SAT32(11665, 16));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(9257, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(10668, 18));
            Assert.AreEqual(1937244160, Inlines.silk_LSHIFT_SAT32(7390, 18));
            Assert.AreEqual(554320, Inlines.silk_LSHIFT_SAT32(69290, 3));
            Assert.AreEqual(573288, Inlines.silk_LSHIFT_SAT32(71661, 3));
            Assert.AreEqual(54552, Inlines.silk_LSHIFT_SAT32(6819, 3));
            Assert.AreEqual(61016, Inlines.silk_LSHIFT_SAT32(7627, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-2400, Inlines.silk_LSHIFT_SAT32(-150, 4));
            Assert.AreEqual(-4800, Inlines.silk_LSHIFT_SAT32(-300, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-13280, Inlines.silk_LSHIFT_SAT32(-830, 4));
            Assert.AreEqual(-21408, Inlines.silk_LSHIFT_SAT32(-1338, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-62512, Inlines.silk_LSHIFT_SAT32(-3907, 4));
            Assert.AreEqual(-46560, Inlines.silk_LSHIFT_SAT32(-2910, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-63216, Inlines.silk_LSHIFT_SAT32(-3951, 4));
            Assert.AreEqual(-46656, Inlines.silk_LSHIFT_SAT32(-2916, 4));
            Assert.AreEqual(887554048, Inlines.silk_LSHIFT_SAT32(13543, 16));
            Assert.AreEqual(848166912, Inlines.silk_LSHIFT_SAT32(12942, 16));
            Assert.AreEqual(686882816, Inlines.silk_LSHIFT_SAT32(10481, 16));
            Assert.AreEqual(746258432, Inlines.silk_LSHIFT_SAT32(11387, 16));
            Assert.AreEqual(1932263424, Inlines.silk_LSHIFT_SAT32(7371, 18));
            Assert.AreEqual(1986527232, Inlines.silk_LSHIFT_SAT32(7578, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(10508, 18));
            Assert.AreEqual(60416, Inlines.silk_LSHIFT_SAT32(7552, 3));
            Assert.AreEqual(66656, Inlines.silk_LSHIFT_SAT32(8332, 3));
            Assert.AreEqual(57248, Inlines.silk_LSHIFT_SAT32(7156, 3));
            Assert.AreEqual(405896, Inlines.silk_LSHIFT_SAT32(50737, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-61488, Inlines.silk_LSHIFT_SAT32(-3843, 4));
            Assert.AreEqual(-45696, Inlines.silk_LSHIFT_SAT32(-2856, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-63088, Inlines.silk_LSHIFT_SAT32(-3943, 4));
            Assert.AreEqual(-46624, Inlines.silk_LSHIFT_SAT32(-2914, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-61488, Inlines.silk_LSHIFT_SAT32(-3843, 4));
            Assert.AreEqual(-45584, Inlines.silk_LSHIFT_SAT32(-2849, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-33216, Inlines.silk_LSHIFT_SAT32(-2076, 4));
            Assert.AreEqual(-31152, Inlines.silk_LSHIFT_SAT32(-1947, 4));
            Assert.AreEqual(632815616, Inlines.silk_LSHIFT_SAT32(9656, 16));
            Assert.AreEqual(742719488, Inlines.silk_LSHIFT_SAT32(11333, 16));
            Assert.AreEqual(636289024, Inlines.silk_LSHIFT_SAT32(9709, 16));
            Assert.AreEqual(803864576, Inlines.silk_LSHIFT_SAT32(12266, 16));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8192, 18));
            Assert.AreEqual(2011168768, Inlines.silk_LSHIFT_SAT32(7672, 18));
            Assert.AreEqual(1956904960, Inlines.silk_LSHIFT_SAT32(7465, 18));
            Assert.AreEqual(2040528896, Inlines.silk_LSHIFT_SAT32(7784, 18));
            Assert.AreEqual(67352, Inlines.silk_LSHIFT_SAT32(8419, 3));
            Assert.AreEqual(321072, Inlines.silk_LSHIFT_SAT32(40134, 3));
            Assert.AreEqual(335832, Inlines.silk_LSHIFT_SAT32(41979, 3));
            Assert.AreEqual(296416, Inlines.silk_LSHIFT_SAT32(37052, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-45968, Inlines.silk_LSHIFT_SAT32(-2873, 4));
            Assert.AreEqual(-35712, Inlines.silk_LSHIFT_SAT32(-2232, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-54288, Inlines.silk_LSHIFT_SAT32(-3393, 4));
            Assert.AreEqual(-44096, Inlines.silk_LSHIFT_SAT32(-2756, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-53616, Inlines.silk_LSHIFT_SAT32(-3351, 4));
            Assert.AreEqual(-44480, Inlines.silk_LSHIFT_SAT32(-2780, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-61856, Inlines.silk_LSHIFT_SAT32(-3866, 4));
            Assert.AreEqual(-48544, Inlines.silk_LSHIFT_SAT32(-3034, 4));
            Assert.AreEqual(747962368, Inlines.silk_LSHIFT_SAT32(11413, 16));
            Assert.AreEqual(808779776, Inlines.silk_LSHIFT_SAT32(12341, 16));
            Assert.AreEqual(774307840, Inlines.silk_LSHIFT_SAT32(11815, 16));
            Assert.AreEqual(786628608, Inlines.silk_LSHIFT_SAT32(12003, 16));
            Assert.AreEqual(1966866432, Inlines.silk_LSHIFT_SAT32(7503, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(9816, 18));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(10188, 18));
            Assert.AreEqual(1932263424, Inlines.silk_LSHIFT_SAT32(7371, 18));
            Assert.AreEqual(314016, Inlines.silk_LSHIFT_SAT32(39252, 3));
            Assert.AreEqual(107152, Inlines.silk_LSHIFT_SAT32(13394, 3));
            Assert.AreEqual(358688, Inlines.silk_LSHIFT_SAT32(44836, 3));
            Assert.AreEqual(621016, Inlines.silk_LSHIFT_SAT32(77627, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-53824, Inlines.silk_LSHIFT_SAT32(-3364, 4));
            Assert.AreEqual(-44384, Inlines.silk_LSHIFT_SAT32(-2774, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-80976, Inlines.silk_LSHIFT_SAT32(-5061, 4));
            Assert.AreEqual(-55184, Inlines.silk_LSHIFT_SAT32(-3449, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-10128, Inlines.silk_LSHIFT_SAT32(-633, 4));
            Assert.AreEqual(-14720, Inlines.silk_LSHIFT_SAT32(-920, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(21888, Inlines.silk_LSHIFT_SAT32(1368, 4));
            Assert.AreEqual(43776, Inlines.silk_LSHIFT_SAT32(2736, 4));
            Assert.AreEqual(779223040, Inlines.silk_LSHIFT_SAT32(11890, 16));
            Assert.AreEqual(845676544, Inlines.silk_LSHIFT_SAT32(12904, 16));
            Assert.AreEqual(813694976, Inlines.silk_LSHIFT_SAT32(12416, 16));
            Assert.AreEqual(882573312, Inlines.silk_LSHIFT_SAT32(13467, 16));
            Assert.AreEqual(1932263424, Inlines.silk_LSHIFT_SAT32(7371, 18));
            Assert.AreEqual(1937244160, Inlines.silk_LSHIFT_SAT32(7390, 18));
            Assert.AreEqual(1932263424, Inlines.silk_LSHIFT_SAT32(7371, 18));
            Assert.AreEqual(2094792704, Inlines.silk_LSHIFT_SAT32(7991, 18));
            Assert.AreEqual(63232, Inlines.silk_LSHIFT_SAT32(7904, 3));
            Assert.AreEqual(70744, Inlines.silk_LSHIFT_SAT32(8843, 3));
            Assert.AreEqual(63328, Inlines.silk_LSHIFT_SAT32(7916, 3));
            Assert.AreEqual(60520, Inlines.silk_LSHIFT_SAT32(7565, 3));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-61808, Inlines.silk_LSHIFT_SAT32(-3863, 4));
            Assert.AreEqual(-45936, Inlines.silk_LSHIFT_SAT32(-2871, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-62976, Inlines.silk_LSHIFT_SAT32(-3936, 4));
            Assert.AreEqual(-46352, Inlines.silk_LSHIFT_SAT32(-2897, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-61488, Inlines.silk_LSHIFT_SAT32(-3843, 4));
            Assert.AreEqual(-45808, Inlines.silk_LSHIFT_SAT32(-2863, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(-62976, Inlines.silk_LSHIFT_SAT32(-3936, 4));
            Assert.AreEqual(-46336, Inlines.silk_LSHIFT_SAT32(-2896, 4));
            Assert.AreEqual(643301376, Inlines.silk_LSHIFT_SAT32(9816, 16));
            Assert.AreEqual(727056384, Inlines.silk_LSHIFT_SAT32(11094, 16));
            Assert.AreEqual(641531904, Inlines.silk_LSHIFT_SAT32(9789, 16));
            Assert.AreEqual(737476608, Inlines.silk_LSHIFT_SAT32(11253, 16));
            Assert.AreEqual(2147221504, Inlines.silk_LSHIFT_SAT32(8618, 18));
            Assert.AreEqual(1873543168, Inlines.silk_LSHIFT_SAT32(14294, 17));
            Assert.AreEqual(1902903296, Inlines.silk_LSHIFT_SAT32(7259, 18));
            Assert.AreEqual(1912602624, Inlines.silk_LSHIFT_SAT32(7296, 18));
            Assert.AreEqual(229736, Inlines.silk_LSHIFT_SAT32(28717, 3));
            Assert.AreEqual(278884, Inlines.silk_LSHIFT_SAT32(139442, 1));
            Assert.AreEqual(157992, Inlines.silk_LSHIFT_SAT32(78996, 1));
            Assert.AreEqual(24922, Inlines.silk_LSHIFT_SAT32(12461, 1));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 0));
            Assert.AreEqual(63584, Inlines.silk_LSHIFT_SAT32(3974, 4));
            Assert.AreEqual(127184, Inlines.silk_LSHIFT_SAT32(7949, 4));
            Assert.AreEqual(62496, Inlines.silk_LSHIFT_SAT32(3906, 4));
            Assert.AreEqual(125008, Inlines.silk_LSHIFT_SAT32(7813, 4));
            Assert.AreEqual(38816, Inlines.silk_LSHIFT_SAT32(2426, 4));
            Assert.AreEqual(45120, Inlines.silk_LSHIFT_SAT32(2820, 4));
            Assert.AreEqual(-16, Inlines.silk_LSHIFT_SAT32(-1, 4));
            Assert.AreEqual(0, Inlines.silk_LSHIFT_SAT32(0, 4));
            Assert.AreEqual(904724480, Inlines.silk_LSHIFT_SAT32(13805, 16));
            Assert.AreEqual(789118976, Inlines.silk_LSHIFT_SAT32(12041, 16));
            Assert.AreEqual(561250304, Inlines.silk_LSHIFT_SAT32(8564, 16));
            Assert.AreEqual(330366976, Inlines.silk_LSHIFT_SAT32(5041, 16));
        }
    }
}
