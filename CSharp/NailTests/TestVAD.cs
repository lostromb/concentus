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
    public class TestVAD
    {
        [TestMethod]
        public void Test_silk_VAD_GetSA_Q8_c_1()
        {
            silk_encoder_state through_psEncC = new silk_encoder_state();
            through_psEncC.subfr_length = 60;
            through_psEncC.nb_subfr = 4;
            through_psEncC.frame_length = 240;
            through_psEncC.predictLPCOrder = 10;
            through_psEncC.fs_kHz = 12;
            through_psEncC.arch = 0;
            through_psEncC.warping_Q16 = 0;
            through_psEncC.shapingLPCOrder = 8;
            through_psEncC.input_tilt_Q15 = 0;
            through_psEncC.speech_activity_Q8 = 0;
            through_psEncC.sVAD.AnaState = new Pointer<int>(new int[] { 0,0,0,0,0,0,0,
                0});
            through_psEncC.sVAD.AnaState1 = new Pointer<int>(new int[] { 0,0,0,0,0,0,0,
                0});
            through_psEncC.sVAD.AnaState2 = new Pointer<int>(new int[] { 0,0,0,0,0,0,25600,
                25600});
            through_psEncC.sVAD.XnrgSubfr = new Pointer<int>(new int[] { 0,0,0,0,25600,25600,25600,
                25600,0,5000,2500,1600,1200,429496,
                858993,1342177});
            through_psEncC.sVAD.NrgRatioSmth_Q8 = new Pointer<int>(new int[] { 25600,25600,25600,25600,0,5000,2500,
                1600,1200,429496,858993,1342177,1789569,50,
                25,16});
            through_psEncC.sVAD.HPstate = 0;
            through_psEncC.sVAD.NL = new Pointer<int>(new int[] { 5000,2500,1600,1200,429496,858993,1342177,
                1789569,50,25,16,12,15,0,
                0,0});
            through_psEncC.sVAD.inv_NL = new Pointer<int>(new int[] { 429496,858993,1342177,1789569,50,25,16,
                12,15,0,0,0,0,0,
                0,0});
            through_psEncC.sVAD.NoiseLevelBias = new Pointer<int>(new int[] { 50,25,16,12,15,0,0,
                0,0,0,0,0,0,0,
                0,0});
            through_psEncC.sVAD.counter = 15;
            Pointer<short> in_pIn = Helpers.WrapWithArrayPointer<short>(
            new short[] { 0,0,0,0,-1,-176,261,-926,10448,
                25391,17381,15286,10080,8220,5052,3748,1498,731,
                -714,-1185,-2118,-2527,-2903,-3062,-3460,-3634,-3870,
                -3983,-3880,-3862,-3908,-3807,-3821,-3704,-3626,-3539,
                -3581,-3408,-3344,-3265,-3105,-3034,-2905,-2850,-2678,
                -2667,-2417,-2309,-2360,-2302,-2173,-2093,-1935,-1901,
                -1791,-1755,-1707,-1455,-1459,-1398,-1361,-1242,-1150,
                -1137,-1035,-877,-854,-739,-755,-701,-613,-623,
                -488,-450,-433,-383,-345,-237,-304,-227,-191,
                -59,-33,-18,21,-53,-4,172,34,129,
                261,269,258,287,213,220,279,161,368,
                367,395,444,414,391,455,363,389,462,
                461,528,431,376,429,516,516,620,443,
                514,533,450,544,480,373,415,464,481,
                389,594,547,487,412,446,470,478,383,
                498,517,451,437,395,394,406,401,403,
                373,320,368,352,317,268,306,374,409,
                336,381,291,268,236,346,302,315,202,
                157,322,217,244,168,169,217,217,241,
                259,259,152,209,184,111,276,201,130,
                130,187,130,110,129,111,131,28,55,
                58,89,49,87,101,-66,19,-58,69,
                33,121,108,48,-16,120,91,24,-33,
                30,24,21,65,53,-30,59,63,28,
                62,1,-45,-14,-3,52,24,-45,-159,
                86,-92,39,-36,-15,7,-124,69,-59,
                38,46,-38,-110,-57,-6,-37,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,240,0,2,
                0,0,0,2,0,1,0,0,0,
                0,0,0,0,0,0,27127,-1,-5268,
                -1,0,0,0,0,0,0,0,0,
                3182,-1,-23965,-2,-17064,-2,1303,-1,24006,
                -1,-17806,-1,-16133,-1,-20851,-1,-8136,-1,
                14441,0,-29504,0,-26035,0,4911,0,-2011,
                -2,-1288,-3,22560,-2,19039,0,23747,2,
                13561,3,26394,2,29472,0,-9411,-2,28890,
                -2,6566,-1,19584,0,8022,1,-15593,0,
                24093,-1,9956,-2,2680,-2,-1472,-2,4796,
                0,-16910,0,-1387,0,-15342,0,5716,0,
                -71,-113,-141,-97,-22,0,20,1,-22,
                -87,7627,18411,22579,23696,23461,22643,21529,20294,
                19155,17970,16778,15776,14813,13826,12774,11854,11062,
                10240,9450,8758,8073,7336,6716,6207,5677,5168,
                4724,4182,3648,3110,2585,2245,1974,1619,1260,
                937,633,364,3,0,480,0,0,4,
                36,0,1,0,48,0,12,0,10,
                0,10144,303,0,0,0,0,0,0,
                1,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0});
            int expected_input_tilt_Q15 = 32468;
            int expected_speech_activity_Q8 = 255;
            int[] expected_input_quality_bands_Q15 =
            new int[] { 23731, 23731, 23731, 23731 };

            VAD.silk_VAD_GetSA_Q8_c(through_psEncC, in_pIn);

            Assert.AreEqual(expected_input_tilt_Q15, through_psEncC.input_tilt_Q15);
            Assert.AreEqual(expected_speech_activity_Q8, through_psEncC.speech_activity_Q8);
            Helpers.AssertArrayDataEquals(expected_input_quality_bands_Q15.GetPointer(), through_psEncC.input_quality_bands_Q15, 4);
        }

        [TestMethod]
        public void Test_silk_VAD_GetSA_Q8_c_2()
        {
            silk_encoder_state through_psEncC = new silk_encoder_state();
            through_psEncC.subfr_length = 60;
            through_psEncC.nb_subfr = 4;
            through_psEncC.frame_length = 240;
            through_psEncC.predictLPCOrder = 10;
            through_psEncC.fs_kHz = 12;
            through_psEncC.arch = 0;
            through_psEncC.warping_Q16 = 0;
            through_psEncC.shapingLPCOrder = 8;
            through_psEncC.input_tilt_Q15 = 32468;
            through_psEncC.speech_activity_Q8 = 255;
            through_psEncC.sVAD.AnaState = new Pointer<int>(new int[] { -49954,16133,62356,-109076,21097,-8295,103,
                27});
            through_psEncC.sVAD.AnaState1 = new Pointer<int>(new int[] { 62356,-109076,21097,-8295,103,27,139,
                809});
            through_psEncC.sVAD.AnaState2 = new Pointer<int>(new int[] { 21097,-8295,103,27,139,809,25596,
                25674});
            through_psEncC.sVAD.XnrgSubfr = new Pointer<int>(new int[] { 103,27,139,809,25596,25674,26033,
                25989,65520,9949,4978,3192,2393,215843,
                431328,672720});
            through_psEncC.sVAD.NrgRatioSmth_Q8 = new Pointer<int>(new int[] { 25596,25674,26033,25989,65520,9949,4978,
                3192,2393,215843,431328,672720,897084,50,
                25,16});
            through_psEncC.sVAD.HPstate = -16;
            through_psEncC.sVAD.NL = new Pointer<int>(new int[] { 9949,4978,3192,2393,215843,431328,672720,
                897084,50,25,16,12,16,196898,
                -30736644,9305902});
            through_psEncC.sVAD.inv_NL = new Pointer<int>(new int[] { 215843,431328,672720,897084,50,25,16,
                12,16,196898,-30736644,9305902,7143239,-2358950,
                -1441446,-24379677});
            through_psEncC.sVAD.NoiseLevelBias = new Pointer<int>(new int[] { 50,25,16,12,16,196898,-30736644,
                9305902,7143239,-2358950,-1441446,-24379677,11271971,5701483,
                33358186,66913102});
            through_psEncC.sVAD.counter = 16;
            Pointer<short> in_pIn = Helpers.WrapWithArrayPointer<short>(
            new short[] { -37,112,43,-69,-58,-14,-102,4,-84,
                48,-72,-86,35,-49,-15,-59,-18,-74,
                -20,-16,-27,7,-11,-85,-120,-37,31,
                -38,43,-30,-55,-33,-1,-78,-29,-64,
                125,53,-102,25,39,-59,-20,-34,-33,
                -21,-57,-69,65,42,3,23,1,-3,
                -92,-62,-24,41,-11,-41,-17,-21,-30,
                19,-3,-88,-6,6,24,-67,45,150,
                11,24,-40,-140,-41,2,-124,60,38,
                66,-30,-85,-4,47,-41,-100,80,-75,
                -27,98,68,20,-93,-75,43,11,-45,
                -89,27,-2,-62,-26,35,138,-22,25,
                35,63,15,-18,-20,-122,-48,-14,-56,
                -19,-81,37,72,-8,40,104,24,-10,
                -50,43,57,50,-10,-31,-19,71,-18,
                49,-76,-40,32,-15,16,23,-84,-70,
                -48,-39,116,-58,29,-106,-22,-51,-25,
                149,70,76,79,-19,-14,97,20,-59,
                -58,-14,59,57,26,-36,22,39,25,
                -5,23,1,75,-35,30,-34,-33,14,
                19,-111,-40,32,-12,53,1,-72,-30,
                -50,17,-60,38,-58,11,168,23,-21,
                -69,13,-28,-21,17,22,63,-26,-9,
                25,-78,11,13,-13,-1,-26,8,155,
                4,41,-44,-115,-75,-97,3,10,24,
                68,-8,-4,87,45,29,-35,-47,-6,
                -1,18,28,82,-29,-70,-52,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,240,0,2,
                0,1,0,2,0,1,0,0,0,
                0,0,1,0,0,0,27451,-2,16442,
                1,0,0,0,0,0,0,0,0,
                22531,1,14849,2,8127,2,-21909,0,-11538,
                -2,1660,-2,-23748,-2,-5911,-1,-14413,0,
                -15739,0,5911,0,11377,-1,25135,-2,10924,
                -2,-15147,-2,5810,0,27549,1,-12691,1,
                7666,1,134,0,6195,-1,-8657,-2,-25955,
                -1,-13862,0,26530,1,-7973,0,-21412,-1,
                -6422,-2,-2800,-2,-21132,-1,-17501,0,31680,
                1,32405,1,-13715,0,-20119,-1,-23419,-2,
                42,130,54,28,57,87,57,-72,-41,
                100,-155,-92,8,64,32,11,63,6,
                -65,-126,-186,-77,-84,-88,56,76,20,
                -56,-92,-3,50,-23,-36,-23,-80,-42,
                5,-67,-77,6,-18,-63,-21,-27,-63,
                -58,-64,-76,3,0,480,0,0,4,
                36,0,1,0,48,0,12,0,10,
                0,10144,303,0,0,0,0,0,0,
                1,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0});
            int expected_input_tilt_Q15 = 0;
            int expected_speech_activity_Q8 = 4;
            int[] expected_input_quality_bands_Q15 =
            new int[] { 23731, 23731, 23731, 23731 };

            VAD.silk_VAD_GetSA_Q8_c(through_psEncC, in_pIn);

            Assert.AreEqual(expected_input_tilt_Q15, through_psEncC.input_tilt_Q15);
            Assert.AreEqual(expected_speech_activity_Q8, through_psEncC.speech_activity_Q8);
            Helpers.AssertArrayDataEquals(expected_input_quality_bands_Q15.GetPointer(), through_psEncC.input_quality_bands_Q15, 4);
        }

        [TestMethod]
        public void Test_silk_VAD_GetSA_Q8_c_3()
        {
            silk_encoder_state through_psEncC = new silk_encoder_state();
            through_psEncC.subfr_length = 60;
            through_psEncC.nb_subfr = 4;
            through_psEncC.frame_length = 240;
            through_psEncC.predictLPCOrder = 10;
            through_psEncC.fs_kHz = 12;
            through_psEncC.arch = 0;
            through_psEncC.warping_Q16 = 0;
            through_psEncC.shapingLPCOrder = 8;
            through_psEncC.input_tilt_Q15 = 0;
            through_psEncC.speech_activity_Q8 = 4;
            through_psEncC.sVAD.AnaState = new Pointer<int>(new int[] { -66567,-99001,59074,37603,64548,-12515,10,
                60});
            through_psEncC.sVAD.AnaState1 = new Pointer<int>(new int[] { 59074,37603,64548,-12515,10,60,161,
                519});
            through_psEncC.sVAD.AnaState2 = new Pointer<int>(new int[] { 64548,-12515,10,60,161,519,25596,
                25674});
            through_psEncC.sVAD.XnrgSubfr = new Pointer<int>(new int[] { 10,60,161,519,25596,25674,26033,
                25989,5,853,804,2053,2492,2516438,
                2667773,1045717});
            through_psEncC.sVAD.NrgRatioSmth_Q8 = new Pointer<int>(new int[] { 25596,25674,26033,25989,5,853,804,
                2053,2492,2516438,2667773,1045717,861455,50,
                25,16});
            through_psEncC.sVAD.HPstate = 5;
            through_psEncC.sVAD.NL = new Pointer<int>(new int[] { 853,804,2053,2492,2516438,2667773,1045717,
                861455,50,25,16,12,17,22872374,
                30802333,3605015});
            through_psEncC.sVAD.inv_NL = new Pointer<int>(new int[] { 2516438,2667773,1045717,861455,50,25,16,
                12,17,22872374,30802333,3605015,36110830,9765461,
                -327649,34537892});
            through_psEncC.sVAD.NoiseLevelBias = new Pointer<int>(new int[] { 50,25,16,12,17,22872374,30802333,
                3605015,36110830,9765461,-327649,34537892,39846414,41484890,
                37093500,12124810});
            through_psEncC.sVAD.counter = 17;
            Pointer<short> in_pIn = Helpers.WrapWithArrayPointer<short>(
            new short[] { -52,43,18,-3,37,20,55,37,15,
                -46,-162,-44,2,46,36,-2,-7,-85,
                32,-8,-4,76,-107,-86,108,33,12,
                112,-26,-19,85,3,1,-56,-52,-25,
                1,43,-21,46,28,34,81,-16,-51,
                -83,-94,-118,-60,77,-11,-1,-83,137,
                43,24,5,-80,8,17,132,98,-94,
                -132,-33,61,-8,-81,7,56,-85,-7,
                90,-45,-10,30,69,-1,45,-64,-46,
                44,0,116,0,4,42,65,-117,27,
                -84,-72,-2,70,-20,75,45,48,-196,
                -47,-13,-105,-109,86,91,41,22,-8,
                56,6,-18,1,52,12,42,-19,-32,
                -51,69,62,-128,65,71,90,-35,-26,
                -17,37,-57,-87,-58,-20,-5,-34,-12,
                121,50,8,48,-7,-17,37,3,-47,
                -40,-6,12,-4,44,-1,91,-45,-89,
                4,-31,-66,-11,-48,65,147,-17,6,
                -2,15,-21,-59,-13,66,11,7,-98,
                -6,28,59,-4,32,-167,-51,12,160,
                -75,-13,24,-80,-61,-7,58,51,40,
                -51,74,-5,-9,56,7,-46,25,-7,
                61,0,-51,30,-51,12,-80,-27,60,
                -11,-39,-65,30,74,46,57,-66,-80,
                -45,46,62,5,-34,-1,-17,-26,-25,
                67,91,1,98,-25,-88,-16,-32,-26,
                16,31,30,35,-49,15,115,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,240,0,2,
                0,0,0,2,0,1,0,0,0,
                0,0,0,0,0,0,25784,-2,19072,
                0,0,0,0,0,0,0,0,0,
                -2079,0,-15551,1,-29886,1,29424,0,3221,
                -1,10954,-2,18166,-2,25532,-1,-18472,0,
                14458,1,-11086,0,7966,0,-19273,-1,-4114,
                -1,-15281,0,-21659,1,-4347,1,16110,1,
                -1591,-1,-6260,-2,-30827,-2,-15738,-2,22248,
                -1,8283,0,-13502,0,-8625,0,14586,0,
                22366,-1,-16152,-2,-15822,-2,22176,-1,27975,
                0,28968,1,-7104,1,17871,1,-20696,-1,
                -40,-32,33,68,60,66,102,106,87,
                73,115,9,-38,-50,-52,-43,-80,-137,
                -191,-171,-128,-53,-53,-51,10,-25,-27,
                37,45,9,31,72,23,31,80,61,
                0,-28,-31,3,46,6,-13,-22,-29,
                -68,-91,-108,3,0,480,0,0,4,
                36,0,1,0,48,0,12,0,10,
                0,10144,303,0,0,0,0,1,0,
                1,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0});
            int expected_input_tilt_Q15 = 0;
            int expected_speech_activity_Q8 = 3;
            int[] expected_input_quality_bands_Q15 =
            new int[] { 23731, 23731, 23731, 23731 };

            VAD.silk_VAD_GetSA_Q8_c(through_psEncC, in_pIn);

            Assert.AreEqual(expected_input_tilt_Q15, through_psEncC.input_tilt_Q15);
            Assert.AreEqual(expected_speech_activity_Q8, through_psEncC.speech_activity_Q8);
            Helpers.AssertArrayDataEquals(expected_input_quality_bands_Q15.GetPointer(), through_psEncC.input_quality_bands_Q15, 4);
        }

        [TestMethod]
        public void Test_silk_VAD_GetSA_Q8_c_4()
        {
            silk_encoder_state through_psEncC = new silk_encoder_state();
            through_psEncC.subfr_length = 60;
            through_psEncC.nb_subfr = 4;
            through_psEncC.frame_length = 240;
            through_psEncC.predictLPCOrder = 10;
            through_psEncC.fs_kHz = 12;
            through_psEncC.arch = 0;
            through_psEncC.warping_Q16 = 0;
            through_psEncC.shapingLPCOrder = 8;
            through_psEncC.input_tilt_Q15 = 0;
            through_psEncC.speech_activity_Q8 = 3;
            through_psEncC.sVAD.AnaState = new Pointer<int>(new int[] { -116196,12147,161117,9381,-16104,-26081,10,
                24});
            through_psEncC.sVAD.AnaState1 = new Pointer<int>(new int[] { 161117,9381,-16104,-26081,10,24,323,
                532});
            through_psEncC.sVAD.AnaState2 = new Pointer<int>(new int[] { -16104,-26081,10,24,323,532,25596,
                25674});
            through_psEncC.sVAD.XnrgSubfr = new Pointer<int>(new int[] { 10,24,323,532,25596,25674,26033,
                25989,6,364,635,1710,2553,5893621,
                3380916,1255214});
            through_psEncC.sVAD.NrgRatioSmth_Q8 = new Pointer<int>(new int[] { 25596,25674,26033,25989,6,364,635,
                1710,2553,5893621,3380916,1255214,841105,50,
                25,16});
            through_psEncC.sVAD.HPstate = 6;
            through_psEncC.sVAD.NL = new Pointer<int>(new int[] { 364,635,1710,2553,5893621,3380916,1255214,
                841105,50,25,16,12,18,-9764833,
                2490339,2883613});
            through_psEncC.sVAD.inv_NL = new Pointer<int>(new int[] { 5893621,3380916,1255214,841105,50,25,16,
                12,18,-9764833,2490339,2883613,-7208921,4784130,
                -2818087,-1900514});
            through_psEncC.sVAD.NoiseLevelBias = new Pointer<int>(new int[] { 50,25,16,12,18,-9764833,2490339,
                2883613,-7208921,4784130,-2818087,-1900514,7798727,-2293678,
                1179597,-2818001});
            through_psEncC.sVAD.counter = 18;
            Pointer<short> in_pIn = Helpers.WrapWithArrayPointer<short>(
            new short[] { 115,29,-53,-28,-4,20,-11,122,26,
                -69,-41,-6,-17,61,-143,33,-54,41,
                12,-133,9,-63,-3,38,-47,15,52,
                41,-42,-91,73,12,31,34,94,17,
                -38,-81,90,67,-68,28,44,-26,-1,
                2,-22,6,-35,-62,67,-47,-74,2,
                5,-35,-4,13,148,62,35,-20,-63,
                45,37,26,-28,-72,-91,-134,-33,-4,
                91,15,34,1,-47,9,-67,-7,-47,
                -44,130,-11,-67,75,-25,-43,-19,41,
                -9,65,3,-5,-65,-12,-42,33,36,
                52,32,-16,-46,34,0,-3,-5,-44,
                59,40,-15,-25,118,66,22,27,-71,
                20,-9,-47,-53,59,-108,66,7,60,
                -13,30,34,-80,28,-10,-26,-13,-43,
                2,79,-36,10,-70,43,42,60,61,
                27,20,41,-55,-11,-28,-25,-68,-136,
                -44,83,-19,45,6,24,-120,-37,43,
                -61,37,65,53,-39,-1,7,108,-53,
                -84,-1,38,24,54,87,41,-30,2,
                -62,-25,-4,-102,-9,39,-27,-20,-3,
                -126,-9,108,-70,88,64,77,8,-108,
                -10,35,16,-3,-27,-66,-76,7,-91,
                -27,51,24,-41,107,89,82,16,14,
                29,69,-80,-80,47,18,115,32,-42,
                -88,-11,-26,-18,-39,-18,-23,26,48,
                0,-10,-3,-33,51,15,-37,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,240,0,2,
                0,1,0,2,0,1,0,0,0,
                0,0,0,0,0,0,23613,0,-8026,
                -1,0,0,0,0,0,0,0,0,
                -29821,-1,-6450,-2,-11472,-2,30985,-1,22751,
                0,7788,1,24688,1,-759,0,18609,0,
                -21342,-1,20357,-1,22136,-1,-9518,-1,22573,
                0,20058,0,598,0,-23552,-1,4480,-1,
                -25280,-2,-20066,-2,26552,-1,29703,0,17324,
                1,18576,1,-20756,0,2155,0,-24229,-1,
                -25276,-1,6304,0,-18226,0,-11752,0,11573,
                0,27767,-1,25018,-1,-12646,-1,8709,0,
                18,70,29,-47,-110,-81,-54,19,24,
                -5,32,34,-4,-34,-37,-100,-176,-122,
                22,41,66,36,-93,-96,-67,-71,52,
                130,71,4,5,24,-44,-63,-130,-157,
                -60,-41,30,82,-23,-65,-10,-14,-29,
                -79,-23,88,3,0,480,0,0,4,
                36,0,1,0,48,0,12,0,10,
                0,10144,303,0,0,0,0,2,0,
                1,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0});
            int expected_input_tilt_Q15 = 0;
            int expected_speech_activity_Q8 = 2;
            int[] expected_input_quality_bands_Q15 =
            new int[] { 23731, 23731, 23731, 23731 };

            VAD.silk_VAD_GetSA_Q8_c(through_psEncC, in_pIn);

            Assert.AreEqual(expected_input_tilt_Q15, through_psEncC.input_tilt_Q15);
            Assert.AreEqual(expected_speech_activity_Q8, through_psEncC.speech_activity_Q8);
            Helpers.AssertArrayDataEquals(expected_input_quality_bands_Q15.GetPointer(), through_psEncC.input_quality_bands_Q15, 4);
        }

        [TestMethod]
        public void Test_silk_VAD_GetSA_Q8_c_5()
        {
            silk_encoder_state through_psEncC = new silk_encoder_state();
            through_psEncC.subfr_length = 60;
            through_psEncC.nb_subfr = 4;
            through_psEncC.frame_length = 240;
            through_psEncC.predictLPCOrder = 10;
            through_psEncC.fs_kHz = 12;
            through_psEncC.arch = 0;
            through_psEncC.warping_Q16 = 0;
            through_psEncC.shapingLPCOrder = 8;
            through_psEncC.input_tilt_Q15 = 0;
            through_psEncC.speech_activity_Q8 = 2;
            through_psEncC.sVAD.AnaState = new Pointer<int>(new int[] { 78693,23773,-51268,-866,-13481,28914,59,
                72});
            through_psEncC.sVAD.AnaState1 = new Pointer<int>(new int[] { -51268,-866,-13481,28914,59,72,226,
                545});
            through_psEncC.sVAD.AnaState2 = new Pointer<int>(new int[] { -13481,28914,59,72,226,545,25596,
                25674});
            through_psEncC.sVAD.XnrgSubfr = new Pointer<int>(new int[] { 59,72,226,545,25596,25674,26033,
                25989,65525,261,419,1488,2589,8200855,
                5116691,1443147});
            through_psEncC.sVAD.NrgRatioSmth_Q8 = new Pointer<int>(new int[] { 25596,25674,26033,25989,65525,261,419,
                1488,2589,8200855,5116691,1443147,829449,50,
                25,16});
            through_psEncC.sVAD.HPstate = -11;
            through_psEncC.sVAD.NL = new Pointer<int>(new int[] { 261,419,1488,2589,8200855,5116691,1443147,
                829449,50,25,16,12,19,-3932129,
                -7471069,3342364});
            through_psEncC.sVAD.inv_NL = new Pointer<int>(new int[] { 8200855,5116691,1443147,829449,50,25,16,
                12,19,-3932129,-7471069,3342364,-2359341,-1507314,
                3014782,-2228287});
            through_psEncC.sVAD.NoiseLevelBias = new Pointer<int>(new int[] { 50,25,16,12,19,-3932129,-7471069,
                3342364,-2359341,-1507314,3014782,-2228287,3473356,1703911,
                2687013,-3997741});
            through_psEncC.sVAD.counter = 19;
            Pointer<short> in_pIn = Helpers.WrapWithArrayPointer<short>(
            new short[] { -37,-75,18,54,5,15,-7,-56,87,
                17,34,-41,-27,-58,-9,10,-71,-43,
                41,30,77,40,-14,-10,-76,-42,-10,
                -13,1,192,103,23,-81,-52,49,49,
                16,7,-113,-176,2,8,-74,30,-10,
                -27,62,24,8,97,-7,-29,8,-71,
                45,51,89,23,-6,-7,-65,3,23,
                5,-39,12,-59,-6,-73,-67,9,-39,
                -11,43,94,-18,-4,57,-37,-5,-29,
                72,-33,-48,64,124,23,26,-30,14,
                -75,-13,68,-49,-87,-53,-17,62,33,
                -19,-66,83,72,-55,38,101,4,-33,
                -44,-12,-81,47,-17,12,107,-48,-70,
                60,-101,110,79,-25,-84,-57,-41,9,
                116,46,18,-82,-58,-146,20,-97,25,
                16,23,10,52,87,14,37,-80,-6,
                18,4,51,-60,101,7,15,-68,-36,
                26,4,80,-77,18,91,34,-105,-34,
                96,-34,76,-33,-4,-90,-58,3,18,
                11,39,56,63,-12,-63,11,-54,-11,
                -5,50,41,-57,16,32,-31,4,21,
                -31,58,-43,-54,-53,52,44,-50,25,
                51,13,-62,-6,-23,-79,25,-164,-30,
                -16,35,24,86,96,30,91,1,-87,
                -32,-40,-13,43,-45,-52,34,9,19,
                -92,-45,158,84,-9,-6,-68,45,62,
                -67,-5,-26,-104,-55,36,4,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,240,0,2,
                0,0,0,2,0,1,0,0,0,
                0,0,0,0,0,0,-11942,0,-14452,
                -2,0,0,0,0,0,0,0,0,
                10490,-2,-11793,-2,-31168,-1,5165,0,25320,
                0,3643,0,21347,-1,-9259,-2,-6953,-2,
                -24733,-1,-9147,0,-14457,1,-13677,1,-11788,
                0,20919,-1,10538,-2,8196,-2,9407,-1,
                27452,0,10577,1,-1158,0,-3239,-1,-1570,
                -2,-12885,-2,25059,-1,12587,0,-28869,0,
                24674,0,-6820,-1,26263,-1,9439,-1,27506,
                -1,4334,0,-21978,0,5646,1,21259,1,
                110,55,74,149,162,109,52,41,-10,
                -26,108,120,73,21,-32,5,58,68,
                48,20,-15,-56,-36,14,-47,-77,-30,
                -9,-25,-20,-50,-109,-78,-56,8,68,
                55,22,-37,-80,-51,-6,11,-10,-80,
                -171,-159,0,3,0,480,0,0,4,
                36,0,1,0,48,0,12,0,10,
                0,10144,303,1,0,1,0,3,0,
                1,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0});
            int expected_input_tilt_Q15 = 0;
            int expected_speech_activity_Q8 = 2;
            int[] expected_input_quality_bands_Q15 =
            new int[] { 23731, 23731, 23731, 23731 };

            VAD.silk_VAD_GetSA_Q8_c(through_psEncC, in_pIn);

            Assert.AreEqual(expected_input_tilt_Q15, through_psEncC.input_tilt_Q15);
            Assert.AreEqual(expected_speech_activity_Q8, through_psEncC.speech_activity_Q8);
            Helpers.AssertArrayDataEquals(expected_input_quality_bands_Q15.GetPointer(), through_psEncC.input_quality_bands_Q15, 4);
        }

        [TestMethod]
        public void Test_silk_VAD_GetSA_Q8_c_6()
        {
            silk_encoder_state through_psEncC = new silk_encoder_state();
            through_psEncC.subfr_length = 60;
            through_psEncC.nb_subfr = 4;
            through_psEncC.frame_length = 240;
            through_psEncC.predictLPCOrder = 10;
            through_psEncC.fs_kHz = 12;
            through_psEncC.arch = 0;
            through_psEncC.warping_Q16 = 0;
            through_psEncC.shapingLPCOrder = 8;
            through_psEncC.input_tilt_Q15 = 0;
            through_psEncC.speech_activity_Q8 = 2;
            through_psEncC.sVAD.AnaState = new Pointer<int>(new int[] { -146072,62803,4427,-64868,67232,-45347,10,
                88});
            through_psEncC.sVAD.AnaState1 = new Pointer<int>(new int[] { 4427,-64868,67232,-45347,10,88,233,
                581});
            through_psEncC.sVAD.AnaState2 = new Pointer<int>(new int[] { 67232,-45347,10,88,233,581,25596,
                25674});
            through_psEncC.sVAD.XnrgSubfr = new Pointer<int>(new int[] { 10,88,233,581,25596,25674,26033,
                25989,5,233,399,1349,2633,9183750,
                5380234,1591716});
            through_psEncC.sVAD.NrgRatioSmth_Q8 = new Pointer<int>(new int[] { 25596,25674,26033,25989,5,233,399,
                1349,2633,9183750,5380234,1591716,815345,50,
                25,16});
            through_psEncC.sVAD.HPstate = 5;
            through_psEncC.sVAD.NL = new Pointer<int>(new int[] { 233,399,1349,2633,9183750,5380234,1591716,
                815345,50,25,16,12,20,-393185,
                -2424867,3211287});
            through_psEncC.sVAD.inv_NL = new Pointer<int>(new int[] { 9183750,5380234,1591716,815345,50,25,16,
                12,20,-393185,-2424867,3211287,3473380,-2555854,
                -2555879,7405524});
            through_psEncC.sVAD.NoiseLevelBias = new Pointer<int>(new int[] { 50,25,16,12,20,-393185,-2424867,
                3211287,3473380,-2555854,-2555879,7405524,1638447,-4784187,
                3735577,-2752456});
            through_psEncC.sVAD.counter = 20;
            Pointer<short> in_pIn = Helpers.WrapWithArrayPointer<short>(
            new short[] { 4,-50,-11,-32,-19,66,116,68,-24,
                11,-22,44,-61,-59,87,88,-48,-99,
                40,34,23,80,39,-60,-60,14,-11,
                -17,16,54,37,-14,-25,-13,-54,-5,
                -73,-15,-24,-25,69,-26,-39,17,101,
                106,93,90,30,-90,-10,-60,23,-92,
                -99,5,53,-15,-26,125,-3,-48,8,
                -16,-80,-27,81,-102,-88,68,101,52,
                -86,79,-12,-85,38,40,73,-29,-15,
                -12,-26,24,-105,-115,133,80,-9,-28,
                84,7,-43,-36,-50,27,4,81,25,
                36,-46,-20,-26,3,42,37,-70,-18,
                -59,-111,17,14,94,67,-14,-2,-15,
                17,141,45,-44,-73,-71,62,-30,-17,
                74,-134,-67,-42,31,61,56,26,-28,
                76,32,-17,-24,-29,-16,-67,46,-24,
                33,-13,12,-9,-25,-142,12,-12,4,
                64,37,-98,43,86,-17,-138,61,-19,
                62,53,65,12,-93,-37,-13,85,66,
                7,40,-101,51,17,-66,12,-12,31,
                -11,22,-62,-31,3,7,-61,25,-3,
                -62,-5,79,13,104,-125,-98,-15,-40,
                -6,118,30,147,-6,-102,-35,-4,91,
                -23,34,-84,-4,-4,-19,81,3,74,
                -14,75,64,-30,-29,29,-58,-74,-61,
                26,-40,-55,29,28,-29,8,15,-1,
                17,2,5,-26,-21,40,38,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,240,0,2,
                0,1,0,2,0,1,0,0,0,
                0,0,0,0,0,0,22692,-2,-10728,
                1,0,0,0,0,0,0,0,0,
                9872,0,23747,0,14553,0,-11884,-1,22491,
                -1,3805,-1,21965,-1,8573,0,-5930,0,
                -3357,0,24057,0,-12746,-1,-18427,-1,18184,
                0,-3616,0,-1356,0,15457,0,19679,-1,
                -31909,-2,9559,-2,-16655,-2,13957,0,28607,
                1,-16954,1,18245,1,13471,0,17576,-1,
                12601,-1,-16978,-1,20347,0,-12476,0,9314,
                1,-6013,0,2522,0,-11456,-2,486,-2,
                -67,-28,60,169,177,75,31,36,30,
                4,31,-20,-46,-8,54,37,-30,-26,
                -45,-56,15,94,103,8,-44,-18,-46,
                -58,-94,-95,-44,-44,-19,56,90,92,
                116,98,80,125,60,-52,-74,-67,-46,
                -83,-129,-98,3,0,480,0,0,4,
                36,0,1,0,48,0,12,0,10,
                0,10144,303,1,0,0,0,4,0,
                1,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0});
            int expected_input_tilt_Q15 = 0;
            int expected_speech_activity_Q8 = 3;
            int[] expected_input_quality_bands_Q15 =
            new int[] { 23731, 23731, 23731, 23731 };

            VAD.silk_VAD_GetSA_Q8_c(through_psEncC, in_pIn);

            Assert.AreEqual(expected_input_tilt_Q15, through_psEncC.input_tilt_Q15);
            Assert.AreEqual(expected_speech_activity_Q8, through_psEncC.speech_activity_Q8);
            Helpers.AssertArrayDataEquals(expected_input_quality_bands_Q15.GetPointer(), through_psEncC.input_quality_bands_Q15, 4);
        }
    }
}
