using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Silk.Structs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace NailTests
{
    public static class Helpers
    {
        /// <summary>
        /// Takes the input array, embeds it in the middle of a large buffer, and returns an arraysegment that points to the data.
        /// This is to ensure that all of our buffer index / offset code is accurate
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="actualData"></param>
        /// <returns></returns>
        public static Pointer<T> WrapWithArrayPointer<T>(T[] actualData)
        {
            /*T[] field = new T[actualData.Length * 2];
            Random rand = new Random();
            int startIndex = rand.Next(0, actualData.Length - 1);
            Array.Copy(actualData, 0, field, startIndex, actualData.Length);
            return new Pointer<T>(field, startIndex);*/
            return new Pointer<T>(actualData);
        }

        /// <summary>
        /// Asserts that an array segment exactly equals the specified subarray. The segment's current offset and count are used.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expectedOutput"></param>
        /// <param name="actualOutput"></param>
        public static void AssertArrayDataEquals<T>(T[] expectedOutput, Pointer<T> actualOutput)
        {
            // If write range is null, it means the function that was tested never actually output any new data to this array
            Assert.IsNotNull(actualOutput.WriteRange, "Function under scrutiny did not actually alter its output vector");

            for (int c = 0; c < Math.Min(expectedOutput.Length, actualOutput.WriteRange.Item2); c++)
            {
                Assert.AreEqual(expectedOutput[c], actualOutput[c]);
            }
        }

        /// <summary>
        /// Asserts that an array segment exactly equals the specified subarray. The segment's current offset and count are used.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expectedOutput"></param>
        /// <param name="actualOutput"></param>
        public static void AssertPointerDataEquals<T>(Pointer<T> expectedOutput, Pointer<T> actualOutput, bool expectChange = true)
        {
            if (expectedOutput == null && actualOutput == null)
            {
                return;
            }

            Assert.IsNotNull(expectedOutput);
            Assert.IsNotNull(actualOutput);

            // If write range is null, it means the function that was tested never actually output any new data to this array
            if (actualOutput.WriteRange == null)
            {
                if (expectChange)
                    Assert.IsNotNull(actualOutput.WriteRange, "Function under scrutiny did not actually alter its output vector");
                else
                    return;
            }

            for (int c = 0; c < Math.Min(expectedOutput.Length, actualOutput.WriteRange.Item2); c++)
            {
                Assert.AreEqual(expectedOutput[c], actualOutput[c]);
            }
        }

        /// <summary>
        /// Asserts that an array segment exactly equals the specified subarray. The segment's current offset and count are used.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expectedOutput"></param>
        /// <param name="actualOutput"></param>
        public static void AssertArrayDataEquals<T>(Pointer<T> expectedOutput, Pointer<T> actualOutput, int compareLength)
        {
            for (int c = 0; c < compareLength; c++)
            {
                Assert.AreEqual(expectedOutput[c], actualOutput[c]);
            }
        }

        /// <summary>
        /// Tests for equality given the existence of different float precision
        /// </summary>
        /// <param name="expectedOutput"></param>
        /// <param name="actualOutput"></param>
        public static void AssertArrayDataIsClose(float[] expectedOutput, Pointer<float> actualOutput)
        {
            for (int c = 0; c < actualOutput.WriteRange.Item2; c++)
            {
                Assert.AreEqual(expectedOutput[c], actualOutput[c], Math.Abs(expectedOutput[c] * 0.001f));
            }
        }

        public static float[] ConvertBytesToFloatArray(uint[] input)
        {
            float[] returnVal = new float[input.Length];
            for (int c = 0; c < input.Length; c++)
            {
                returnVal[c] = BitConverter.ToSingle(BitConverter.GetBytes(input[c]), 0);
            }
            return returnVal;
        }

        public static void AssertEcCtxEquals(ec_ctx expected, ec_ctx actual)
        {
            AssertArrayDataEquals(expected.buf, actual.buf, (int)expected.storage);
            Assert.AreEqual(expected.end_offs, actual.end_offs);
            Assert.AreEqual(expected.end_window, actual.end_window);
            Assert.AreEqual(expected.nend_bits, actual.nend_bits);
            Assert.AreEqual(expected.nbits_total, actual.nbits_total);
            Assert.AreEqual(expected.offs, actual.offs);
            Assert.AreEqual(expected.rng, actual.rng);
            Assert.AreEqual(expected.val, actual.val);
            Assert.AreEqual(expected.ext, actual.ext);
            Assert.AreEqual(expected.rem, actual.rem);
            Assert.AreEqual(expected.error, actual.error);
        }

        public static void AssertSilkEncControlEquals(silk_encoder_control expected, silk_encoder_control actual)
        {
            AssertPointerDataEquals(expected.Gains_Q16, actual.Gains_Q16, false);
            AssertPointerDataEquals(expected.PredCoef_Q12, actual.PredCoef_Q12, false);
            AssertPointerDataEquals(expected.LTPCoef_Q14, actual.LTPCoef_Q14, false);
            Assert.AreEqual(expected.LTP_scale_Q14, actual.LTP_scale_Q14);
            AssertPointerDataEquals(expected.pitchL, actual.pitchL, false);
            AssertPointerDataEquals(expected.AR1_Q13, actual.AR1_Q13, false);
            AssertPointerDataEquals(expected.AR2_Q13, actual.AR2_Q13, false);
            AssertPointerDataEquals(expected.LF_shp_Q14, actual.LF_shp_Q14, false);
            AssertPointerDataEquals(expected.GainsPre_Q14, actual.GainsPre_Q14, false);
            AssertPointerDataEquals(expected.HarmBoost_Q14, actual.HarmBoost_Q14, false);
            AssertPointerDataEquals(expected.Tilt_Q14, actual.Tilt_Q14, false);
            AssertPointerDataEquals(expected.HarmShapeGain_Q14, actual.HarmShapeGain_Q14, false);
            Assert.AreEqual(expected.Lambda_Q10, actual.Lambda_Q10);
            Assert.AreEqual(expected.input_quality_Q14, actual.input_quality_Q14);
            Assert.AreEqual(expected.coding_quality_Q14, actual.coding_quality_Q14);
            Assert.AreEqual(expected.sparseness_Q8, actual.sparseness_Q8);
            Assert.AreEqual(expected.predGain_Q16, actual.predGain_Q16);
            Assert.AreEqual(expected.LTPredCodGain_Q7, actual.LTPredCodGain_Q7);
            AssertPointerDataEquals(expected.ResNrg, actual.ResNrg, false);
            AssertPointerDataEquals(expected.ResNrgQ, actual.ResNrgQ, false);
            AssertPointerDataEquals(expected.GainsUnq_Q16, actual.GainsUnq_Q16, false);
            Assert.AreEqual(expected.lastGainIndexPrev, actual.lastGainIndexPrev);
        }

        public static void AssertSilkEncStateEquals(silk_encoder_state_fix expected, silk_encoder_state_fix actual)
        {
            AssertSilkEncStateEquals(expected.sCmn, actual.sCmn);
            AssertSilkShapeStateEquals(expected.sShape, actual.sShape);
            AssertSilkPrefilterStateEquals(expected.sPrefilt, actual.sPrefilt);
            AssertPointerDataEquals(expected.x_buf, actual.x_buf, false);
            Assert.AreEqual(expected.LTPCorr_Q15, actual.LTPCorr_Q15);
        }

        public static void AssertSilkEncStateEquals(silk_encoder_state expected, silk_encoder_state actual)
        {
            AssertPointerDataEquals(expected.In_HP_State, actual.In_HP_State, false);
            Assert.AreEqual(expected.variable_HP_smth1_Q15, actual.variable_HP_smth1_Q15);
            Assert.AreEqual(expected.variable_HP_smth2_Q15, actual.variable_HP_smth2_Q15);
            AssertSilkLPStateEquals(expected.sLP, actual.sLP);
            AssertSilkVADStateEquals(expected.sVAD, actual.sVAD);
            AssertSilkNSQStateEquals(expected.sNSQ, actual.sNSQ);
            //AssertPointerDataEquals(expected.prev_NLSFq_Q15, actual.prev_NLSFq_Q15, false);
            Assert.AreEqual(expected.speech_activity_Q8, actual.speech_activity_Q8);
            Assert.AreEqual(expected.allow_bandwidth_switch, actual.allow_bandwidth_switch);
            Assert.AreEqual(expected.LBRRprevLastGainIndex, actual.LBRRprevLastGainIndex);
            Assert.AreEqual(expected.prevSignalType, actual.prevSignalType);
            Assert.AreEqual(expected.prevLag, actual.prevLag);
            Assert.AreEqual(expected.pitch_LPC_win_length, actual.pitch_LPC_win_length);
            Assert.AreEqual(expected.max_pitch_lag, actual.max_pitch_lag);
            Assert.AreEqual(expected.API_fs_Hz, actual.API_fs_Hz);
            Assert.AreEqual(expected.prev_API_fs_Hz, actual.prev_API_fs_Hz);
            Assert.AreEqual(expected.maxInternal_fs_Hz, actual.maxInternal_fs_Hz);
            Assert.AreEqual(expected.minInternal_fs_Hz, actual.minInternal_fs_Hz);
            Assert.AreEqual(expected.desiredInternal_fs_Hz, actual.desiredInternal_fs_Hz);
            Assert.AreEqual(expected.fs_kHz, actual.fs_kHz);
            Assert.AreEqual(expected.nb_subfr, actual.nb_subfr);
            Assert.AreEqual(expected.frame_length, actual.frame_length);
            Assert.AreEqual(expected.subfr_length, actual.subfr_length);
            Assert.AreEqual(expected.ltp_mem_length, actual.ltp_mem_length);
            Assert.AreEqual(expected.la_pitch, actual.la_pitch);
            Assert.AreEqual(expected.la_shape, actual.la_shape);
            Assert.AreEqual(expected.shapeWinLength, actual.shapeWinLength);
            Assert.AreEqual(expected.TargetRate_bps, actual.TargetRate_bps);
            Assert.AreEqual(expected.PacketSize_ms, actual.PacketSize_ms);
            Assert.AreEqual(expected.PacketLoss_perc, actual.PacketLoss_perc);
            Assert.AreEqual(expected.frameCounter, actual.frameCounter);
            Assert.AreEqual(expected.Complexity, actual.Complexity);
            Assert.AreEqual(expected.nStatesDelayedDecision, actual.nStatesDelayedDecision);
            Assert.AreEqual(expected.useInterpolatedNLSFs, actual.useInterpolatedNLSFs);
            Assert.AreEqual(expected.shapingLPCOrder, actual.shapingLPCOrder);
            Assert.AreEqual(expected.predictLPCOrder, actual.predictLPCOrder);
            Assert.AreEqual(expected.pitchEstimationComplexity, actual.pitchEstimationComplexity);
            Assert.AreEqual(expected.pitchEstimationLPCOrder, actual.pitchEstimationLPCOrder);
            Assert.AreEqual(expected.pitchEstimationThreshold_Q16, actual.pitchEstimationThreshold_Q16);
            Assert.AreEqual(expected.LTPQuantLowComplexity, actual.LTPQuantLowComplexity);
            Assert.AreEqual(expected.mu_LTP_Q9, actual.mu_LTP_Q9);
            Assert.AreEqual(expected.sum_log_gain_Q7, actual.sum_log_gain_Q7);
            Assert.AreEqual(expected.NLSF_MSVQ_Survivors, actual.NLSF_MSVQ_Survivors);
            Assert.AreEqual(expected.first_frame_after_reset, actual.first_frame_after_reset);
            Assert.AreEqual(expected.controlled_since_last_payload, actual.controlled_since_last_payload);
            Assert.AreEqual(expected.warping_Q16, actual.warping_Q16);
            Assert.AreEqual(expected.useCBR, actual.useCBR);
            Assert.AreEqual(expected.prefillFlag, actual.prefillFlag);
            AssertPointerDataEquals(expected.pitch_lag_low_bits_iCDF, actual.pitch_lag_low_bits_iCDF, false);
            AssertPointerDataEquals(expected.pitch_contour_iCDF, actual.pitch_contour_iCDF, false);
            AssertSilkNLSFStateEquals(expected.psNLSF_CB, actual.psNLSF_CB);
            AssertPointerDataEquals(expected.input_quality_bands_Q15, actual.input_quality_bands_Q15, false);
            Assert.AreEqual(expected.input_tilt_Q15, actual.input_tilt_Q15);
            Assert.AreEqual(expected.SNR_dB_Q7, actual.SNR_dB_Q7);
            AssertPointerDataEquals(expected.VAD_flags, actual.VAD_flags, false);
            Assert.AreEqual(expected.LBRR_flag, actual.LBRR_flag);
            AssertPointerDataEquals(expected.LBRR_flags, actual.LBRR_flags, false);
            AssertSilkSideInfoIndicesEquals(expected.indices, actual.indices);
            AssertPointerDataEquals(expected.pulses, actual.pulses, false);
            Assert.AreEqual(expected.arch, actual.arch);
            AssertPointerDataEquals(expected.inputBuf, actual.inputBuf, false);
            Assert.AreEqual(expected.inputBufIx, actual.inputBufIx);
            Assert.AreEqual(expected.nFramesPerPacket, actual.nFramesPerPacket);
            Assert.AreEqual(expected.nFramesEncoded, actual.nFramesEncoded);
            Assert.AreEqual(expected.nChannelsAPI, actual.nChannelsAPI);
            Assert.AreEqual(expected.nChannelsInternal, actual.nChannelsInternal);
            Assert.AreEqual(expected.channelNb, actual.channelNb);
            Assert.AreEqual(expected.frames_since_onset, actual.frames_since_onset);
            Assert.AreEqual(expected.ec_prevSignalType, actual.ec_prevSignalType);
            Assert.AreEqual(expected.ec_prevLagIndex, actual.ec_prevLagIndex);
            AssertSilkResamplerStateEquals(expected.resampler_state, actual.resampler_state);
            Assert.AreEqual(expected.useDTX, actual.useDTX);
            Assert.AreEqual(expected.inDTX, actual.inDTX);
            Assert.AreEqual(expected.noSpeechCounter, actual.noSpeechCounter);
            Assert.AreEqual(expected.useInBandFEC, actual.useInBandFEC);
            Assert.AreEqual(expected.LBRR_enabled, actual.LBRR_enabled);
            Assert.AreEqual(expected.LBRR_GainIncreases, actual.LBRR_GainIncreases);
            //AssertPointerDataEquals(expected.Pointer < SideInfoIndices > indices_LBRR, actual.Pointer < SideInfoIndices > indices_LBRR);
            //AssertPointerDataEquals(expected.Pointer < Pointer < sbyte >> pulses_LBRR, actual.Pointer < Pointer < sbyte >> pulses_LBRR);
        }

        public static void AssertSilkNLSFStateEquals(silk_NLSF_CB_struct expected, silk_NLSF_CB_struct actual)
        {
            Assert.AreEqual(expected.nVectors, actual.nVectors);
            Assert.AreEqual(expected.order, actual.order);
            Assert.AreEqual(expected.quantStepSize_Q16, actual.quantStepSize_Q16);
            Assert.AreEqual(expected.invQuantStepSize_Q6, actual.invQuantStepSize_Q6);
            AssertPointerDataEquals(expected.CB1_NLSF_Q8, actual.CB1_NLSF_Q8, false);
            AssertPointerDataEquals(expected.CB1_iCDF, actual.CB1_iCDF, false);
            AssertPointerDataEquals(expected.pred_Q8, actual.pred_Q8, false);
            AssertPointerDataEquals(expected.ec_sel, actual.ec_sel, false);
            AssertPointerDataEquals(expected.ec_iCDF, actual.ec_iCDF, false);
            AssertPointerDataEquals(expected.ec_Rates_Q5, actual.ec_Rates_Q5, false);
            AssertPointerDataEquals(expected.deltaMin_Q15, actual.deltaMin_Q15, false);
        }

        public static void AssertSilkSideInfoIndicesEquals(SideInfoIndices expected, SideInfoIndices actual)
        {
            AssertPointerDataEquals(expected.GainsIndices, actual.GainsIndices, false);
            AssertPointerDataEquals(expected.LTPIndex, actual.LTPIndex, false);
            AssertPointerDataEquals(expected.NLSFIndices, actual.NLSFIndices, false);
            Assert.AreEqual(expected.lagIndex, actual.lagIndex);
            Assert.AreEqual(expected.contourIndex, actual.contourIndex);
            Assert.AreEqual(expected.signalType, actual.signalType);
            Assert.AreEqual(expected.quantOffsetType, actual.quantOffsetType);
            Assert.AreEqual(expected.NLSFInterpCoef_Q2, actual.NLSFInterpCoef_Q2);
            Assert.AreEqual(expected.PERIndex, actual.PERIndex);
            Assert.AreEqual(expected.LTP_scaleIndex, actual.LTP_scaleIndex);
            Assert.AreEqual(expected.Seed, actual.Seed);
        }

        public static void AssertSilkResamplerStateEquals(silk_resampler_state_struct expected, silk_resampler_state_struct actual)
        {
            AssertPointerDataEquals(expected.sIIR, actual.sIIR, false);
            AssertPointerDataEquals(expected.sFIR_i32, actual.sFIR_i32, false);
            AssertPointerDataEquals(expected.sFIR_i16, actual.sFIR_i16, false);
            AssertPointerDataEquals(expected.delayBuf, actual.delayBuf, false);
            Assert.AreEqual(expected.resampler_function, actual.resampler_function);
            Assert.AreEqual(expected.batchSize, actual.batchSize);
            Assert.AreEqual(expected.invRatio_Q16, actual.invRatio_Q16);
            Assert.AreEqual(expected.FIR_Order, actual.FIR_Order);
            Assert.AreEqual(expected.FIR_Fracs, actual.FIR_Fracs);
            Assert.AreEqual(expected.Fs_in_kHz, actual.Fs_in_kHz);
            Assert.AreEqual(expected.Fs_out_kHz, actual.Fs_out_kHz);
            Assert.AreEqual(expected.inputDelay, actual.inputDelay);
            AssertPointerDataEquals(expected.Coefs, actual.Coefs, false);
        }

        public static void AssertSilkShapeStateEquals(silk_shape_state expected, silk_shape_state actual)
        {
            Assert.AreEqual(expected.LastGainIndex, actual.LastGainIndex);
            Assert.AreEqual(expected.HarmBoost_smth_Q16, actual.HarmBoost_smth_Q16);
            Assert.AreEqual(expected.HarmShapeGain_smth_Q16, actual.HarmShapeGain_smth_Q16);
            Assert.AreEqual(expected.Tilt_smth_Q16, actual.Tilt_smth_Q16);
        }

        public static void AssertSilkPrefilterStateEquals(silk_prefilter_state expected, silk_prefilter_state actual)
        {
            AssertPointerDataEquals(expected.sLTP_shp, actual.sLTP_shp, false);
            AssertPointerDataEquals(expected.sAR_shp, actual.sAR_shp, false);
            Assert.AreEqual(expected.sLTP_shp_buf_idx, actual.sLTP_shp_buf_idx);
            Assert.AreEqual(expected.sLF_AR_shp_Q12, actual.sLF_AR_shp_Q12);
            Assert.AreEqual(expected.sLF_MA_shp_Q12, actual.sLF_MA_shp_Q12);
            Assert.AreEqual(expected.sHarmHP_Q2, actual.sHarmHP_Q2);
            Assert.AreEqual(expected.rand_seed, actual.rand_seed);
            Assert.AreEqual(expected.lagPrev, actual.lagPrev);
        }

        public static void AssertSilkLPStateEquals(silk_LP_state expected, silk_LP_state actual)
        {
            AssertPointerDataEquals(expected.In_LP_State, actual.In_LP_State, false);
            Assert.AreEqual(expected.transition_frame_no, actual.transition_frame_no);
            Assert.AreEqual(expected.mode, actual.mode);
        }

        public static void AssertSilkVADStateEquals(silk_VAD_state expected, silk_VAD_state actual)
        {
            AssertPointerDataEquals(expected.AnaState, actual.AnaState, false);
            AssertPointerDataEquals(expected.AnaState1, actual.AnaState1, false);
            AssertPointerDataEquals(expected.AnaState2, actual.AnaState2, false);
            AssertPointerDataEquals(expected.XnrgSubfr, actual.XnrgSubfr, false);
            AssertPointerDataEquals(expected.NrgRatioSmth_Q8, actual.NrgRatioSmth_Q8, false);
            Assert.AreEqual(expected.HPstate, actual.HPstate);
            AssertPointerDataEquals(expected.NL, actual.NL, false);
            AssertPointerDataEquals(expected.inv_NL, actual.inv_NL, false);
            AssertPointerDataEquals(expected.NoiseLevelBias, actual.NoiseLevelBias, false);
            Assert.AreEqual(expected.counter, actual.counter);
        }

        public static void AssertSilkNSQStateEquals(silk_nsq_state expected, silk_nsq_state actual)
        {
            AssertPointerDataEquals(expected.xq, actual.xq, false);
            AssertPointerDataEquals(expected.sLTP_shp_Q14, actual.sLTP_shp_Q14, false);
            AssertPointerDataEquals(expected.sLPC_Q14, actual.sLPC_Q14, false);
            AssertPointerDataEquals(expected.sAR2_Q14, actual.sAR2_Q14, false);
            Assert.AreEqual(expected.sLF_AR_shp_Q14, actual.sLF_AR_shp_Q14);
            Assert.AreEqual(expected.lagPrev, actual.lagPrev);
            Assert.AreEqual(expected.sLTP_buf_idx, actual.sLTP_buf_idx);
            Assert.AreEqual(expected.sLTP_shp_buf_idx, actual.sLTP_shp_buf_idx);
            Assert.AreEqual(expected.rand_seed, actual.rand_seed);
            Assert.AreEqual(expected.prev_gain_Q16, actual.prev_gain_Q16);
            Assert.AreEqual(expected.rewhite_flag, actual.rewhite_flag);
        }

        public static void AssertCeltEncoderStateEquals(CELTEncoder expected, CELTEncoder actual)
        {
            Assert.AreEqual(expected.channels, actual.channels);
            Assert.AreEqual(expected.stream_channels, actual.stream_channels);
            Assert.AreEqual(expected.force_intra, actual.force_intra);
            Assert.AreEqual(expected.clip, actual.clip);
            Assert.AreEqual(expected.disable_pf, actual.disable_pf);
            Assert.AreEqual(expected.complexity, actual.complexity);
            Assert.AreEqual(expected.upsample, actual.upsample);
            Assert.AreEqual(expected.start, actual.start);
            Assert.AreEqual(expected.end, actual.end);
            Assert.AreEqual(expected.bitrate, actual.bitrate);
            Assert.AreEqual(expected.vbr, actual.vbr);
            Assert.AreEqual(expected.signalling, actual.signalling);
            Assert.AreEqual(expected.constrained_vbr, actual.constrained_vbr);
            Assert.AreEqual(expected.loss_rate, actual.loss_rate);
            Assert.AreEqual(expected.lsb_depth, actual.lsb_depth);
            Assert.AreEqual(expected.variable_duration, actual.variable_duration);
            Assert.AreEqual(expected.lfe, actual.lfe);
            Assert.AreEqual(expected.arch, actual.arch);
            Assert.AreEqual(expected.rng, actual.rng);
            Assert.AreEqual(expected.spread_decision, actual.spread_decision);
            Assert.AreEqual(expected.delayedIntra, actual.delayedIntra);
            Assert.AreEqual(expected.tonal_average, actual.tonal_average);
            Assert.AreEqual(expected.lastCodedBands, actual.lastCodedBands);
            Assert.AreEqual(expected.hf_average, actual.hf_average);
            Assert.AreEqual(expected.tapset_decision, actual.tapset_decision);
            Assert.AreEqual(expected.prefilter_period, actual.prefilter_period);
            Assert.AreEqual(expected.prefilter_gain, actual.prefilter_gain);
            Assert.AreEqual(expected.prefilter_tapset, actual.prefilter_tapset);
            Assert.AreEqual(expected.consec_transient, actual.consec_transient);
            AssertPointerDataEquals(expected.preemph_memE, actual.preemph_memE, false);
            AssertPointerDataEquals(expected.preemph_memD, actual.preemph_memD, false);
            Assert.AreEqual(expected.vbr_reservoir, actual.vbr_reservoir);
            Assert.AreEqual(expected.vbr_drift, actual.vbr_drift);
            Assert.AreEqual(expected.vbr_offset, actual.vbr_offset);
            Assert.AreEqual(expected.vbr_count, actual.vbr_count);
            Assert.AreEqual(expected.overlap_max, actual.overlap_max);
            Assert.AreEqual(expected.stereo_saving, actual.stereo_saving);
            Assert.AreEqual(expected.intensity, actual.intensity);
            // AssertPointerDataEquals(expected.energy_mask, actual.energy_mask, false);
            Assert.AreEqual(expected.spec_avg, actual.spec_avg);
            AssertPointerDataEquals(expected.in_mem, actual.in_mem, false);
            AssertPointerDataEquals(expected.prefilter_mem, actual.prefilter_mem, false);
            AssertPointerDataEquals(expected.oldBandE, actual.oldBandE, false);
            AssertPointerDataEquals(expected.oldLogE, actual.oldLogE, false);
            AssertPointerDataEquals(expected.oldLogE2, actual.oldLogE2, false);
        }
    }
}
