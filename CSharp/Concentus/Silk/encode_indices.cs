using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Silk
{
    public static class encode_indices
    {
        /// <summary>
        /// Encode side-information parameters to payload
        /// </summary>
        /// <param name="psEncC">I/O  Encoder state</param>
        /// <param name="psRangeEnc">I/O  Compressor data structure</param>
        /// <param name="FrameIndex">I    Frame number</param>
        /// <param name="encode_LBRR">I    Flag indicating LBRR data is being encoded</param>
        /// <param name="condCoding">I    The type of conditional coding to use</param>
        public static void silk_encode_indices(
            silk_encoder_state psEncC,
            ec_ctx psRangeEnc,
            int FrameIndex,
            int encode_LBRR,
            int condCoding)
        {
            int i, k, typeOffset;
            int encode_absolute_lagIndex, delta_lagIndex;
            short[] ec_ix = new short[SilkConstants.MAX_LPC_ORDER];
            byte[] pred_Q8 = new byte[SilkConstants.MAX_LPC_ORDER];
            SideInfoIndices psIndices;

            if (encode_LBRR != 0)
            {
                psIndices = psEncC.indices_LBRR[FrameIndex];
            }
            else {
                psIndices = psEncC.indices;
            }

            /*******************************************/
            /* Encode signal type and quantizer offset */
            /*******************************************/
            typeOffset = 2 * psIndices.signalType + psIndices.quantOffsetType;
            Inlines.OpusAssert(typeOffset >= 0 && typeOffset < 6);
            Inlines.OpusAssert(encode_LBRR == 0 || typeOffset >= 2);
            if (encode_LBRR != 0 || typeOffset >= 2)
            {
                EntropyCoder.ec_enc_icdf(psRangeEnc, typeOffset - 2, Tables.silk_type_offset_VAD_iCDF.GetPointer(), 8);
            }
            else
            {
                EntropyCoder.ec_enc_icdf(psRangeEnc, typeOffset, Tables.silk_type_offset_no_VAD_iCDF.GetPointer(), 8);
            }

            /****************/
            /* Encode gains */
            /****************/
            /* first subframe */
            if (condCoding == SilkConstants.CODE_CONDITIONALLY)
            {
                /* conditional coding */
                Inlines.OpusAssert(psIndices.GainsIndices[0] >= 0 && psIndices.GainsIndices[0] < SilkConstants.MAX_DELTA_GAIN_QUANT - SilkConstants.MIN_DELTA_GAIN_QUANT + 1);
                EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.GainsIndices[0], Tables.silk_delta_gain_iCDF.GetPointer(), 8);
            }
            else
            {
                /* independent coding, in two stages: MSB bits followed by 3 LSBs */
                Inlines.OpusAssert(psIndices.GainsIndices[0] >= 0 && psIndices.GainsIndices[0] < SilkConstants.N_LEVELS_QGAIN);
                EntropyCoder.ec_enc_icdf(psRangeEnc, Inlines.silk_RSHIFT(psIndices.GainsIndices[0], 3), Tables.silk_gain_iCDF[psIndices.signalType].GetPointer(), 8);
                EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.GainsIndices[0] & 7, Tables.silk_uniform8_iCDF.GetPointer(), 8);
            }

            /* remaining subframes */
            for (i = 1; i < psEncC.nb_subfr; i++)
            {
                Inlines.OpusAssert(psIndices.GainsIndices[i] >= 0 && psIndices.GainsIndices[i] < SilkConstants.MAX_DELTA_GAIN_QUANT - SilkConstants.MIN_DELTA_GAIN_QUANT + 1);
                EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.GainsIndices[i], Tables.silk_delta_gain_iCDF.GetPointer(), 8);
            }

            /****************/
            /* Encode NLSFs */
            /****************/
            EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.NLSFIndices[0], psEncC.psNLSF_CB.CB1_iCDF.Point((psIndices.signalType >> 1) * psEncC.psNLSF_CB.nVectors), 8);
            NLSF.silk_NLSF_unpack(ec_ix.GetPointer(), pred_Q8.GetPointer(), psEncC.psNLSF_CB, psIndices.NLSFIndices[0]);
            Inlines.OpusAssert(psEncC.psNLSF_CB.order == psEncC.predictLPCOrder);

            for (i = 0; i < psEncC.psNLSF_CB.order; i++)
            {
                if (psIndices.NLSFIndices[i + 1] >= SilkConstants.NLSF_QUANT_MAX_AMPLITUDE)
                {
                    EntropyCoder.ec_enc_icdf(psRangeEnc, 2 * SilkConstants.NLSF_QUANT_MAX_AMPLITUDE, psEncC.psNLSF_CB.ec_iCDF.Point(ec_ix[i]), 8);
                    EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.NLSFIndices[i + 1] - SilkConstants.NLSF_QUANT_MAX_AMPLITUDE, Tables.silk_NLSF_EXT_iCDF.GetPointer(), 8);
                }
                else if (psIndices.NLSFIndices[i + 1] <= 0 - SilkConstants.NLSF_QUANT_MAX_AMPLITUDE)
                {
                    EntropyCoder.ec_enc_icdf(psRangeEnc, 0, psEncC.psNLSF_CB.ec_iCDF.Point(ec_ix[i]), 8);
                    EntropyCoder.ec_enc_icdf(psRangeEnc, -psIndices.NLSFIndices[i + 1] - SilkConstants.NLSF_QUANT_MAX_AMPLITUDE, Tables.silk_NLSF_EXT_iCDF.GetPointer(), 8);
                }
                else
                {
                    EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.NLSFIndices[i + 1] + SilkConstants.NLSF_QUANT_MAX_AMPLITUDE, psEncC.psNLSF_CB.ec_iCDF.Point(ec_ix[i]), 8);
                }
            }

            /* Encode NLSF interpolation factor */
            if (psEncC.nb_subfr == SilkConstants.MAX_NB_SUBFR)
            {
                Inlines.OpusAssert(psIndices.NLSFInterpCoef_Q2 >= 0 && psIndices.NLSFInterpCoef_Q2 < 5);
                EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.NLSFInterpCoef_Q2, Tables.silk_NLSF_interpolation_factor_iCDF.GetPointer(), 8);
            }

            if (psIndices.signalType == SilkConstants.TYPE_VOICED)
            {
                /*********************/
                /* Encode pitch lags */
                /*********************/
                /* lag index */
                encode_absolute_lagIndex = 1;
                if (condCoding == SilkConstants.CODE_CONDITIONALLY && psEncC.ec_prevSignalType == SilkConstants.TYPE_VOICED)
                {
                    /* Delta Encoding */
                    delta_lagIndex = psIndices.lagIndex - psEncC.ec_prevLagIndex;

                    if (delta_lagIndex < -8 || delta_lagIndex > 11)
                    {
                        delta_lagIndex = 0;
                    }
                    else
                    {
                        delta_lagIndex = delta_lagIndex + 9;
                        encode_absolute_lagIndex = 0; /* Only use delta */
                    }

                    Inlines.OpusAssert(delta_lagIndex >= 0 && delta_lagIndex < 21);
                    EntropyCoder.ec_enc_icdf(psRangeEnc, delta_lagIndex, Tables.silk_pitch_delta_iCDF.GetPointer(), 8);
                }

                if (encode_absolute_lagIndex != 0)
                {
                    /* Absolute encoding */
                    int pitch_high_bits, pitch_low_bits;
                    pitch_high_bits = Inlines.silk_DIV32_16(psIndices.lagIndex, Inlines.silk_RSHIFT(psEncC.fs_kHz, 1));
                    pitch_low_bits = psIndices.lagIndex - Inlines.silk_SMULBB(pitch_high_bits, Inlines.silk_RSHIFT(psEncC.fs_kHz, 1));
                    Inlines.OpusAssert(pitch_low_bits < psEncC.fs_kHz / 2);
                    Inlines.OpusAssert(pitch_high_bits < 32);
                    EntropyCoder.ec_enc_icdf(psRangeEnc, pitch_high_bits, Tables.silk_pitch_lag_iCDF.GetPointer(), 8);
                    EntropyCoder.ec_enc_icdf(psRangeEnc, pitch_low_bits, psEncC.pitch_lag_low_bits_iCDF, 8);
                }
                psEncC.ec_prevLagIndex = psIndices.lagIndex;

                /* Countour index */
                Inlines.OpusAssert(psIndices.contourIndex >= 0);
                Inlines.OpusAssert((psIndices.contourIndex < 34 && psEncC.fs_kHz > 8 && psEncC.nb_subfr == 4) ||
                            (psIndices.contourIndex < 11 && psEncC.fs_kHz == 8 && psEncC.nb_subfr == 4) ||
                            (psIndices.contourIndex < 12 && psEncC.fs_kHz > 8 && psEncC.nb_subfr == 2) ||
                            (psIndices.contourIndex < 3 && psEncC.fs_kHz == 8 && psEncC.nb_subfr == 2));
                EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.contourIndex, psEncC.pitch_contour_iCDF, 8);

                /********************/
                /* Encode LTP gains */
                /********************/
                /* PERIndex value */
                Inlines.OpusAssert(psIndices.PERIndex >= 0 && psIndices.PERIndex < 3);
                EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.PERIndex, Tables.silk_LTP_per_index_iCDF.GetPointer(), 8);

                /* Codebook Indices */
                for (k = 0; k < psEncC.nb_subfr; k++)
                {
                    Inlines.OpusAssert(psIndices.LTPIndex[k] >= 0 && psIndices.LTPIndex[k] < (8 << psIndices.PERIndex));
                    EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.LTPIndex[k], Tables.silk_LTP_gain_iCDF_ptrs[psIndices.PERIndex].GetPointer(), 8);
                }

                /**********************/
                /* Encode LTP scaling */
                /**********************/
                if (condCoding == SilkConstants.CODE_INDEPENDENTLY)
                {
                    Inlines.OpusAssert(psIndices.LTP_scaleIndex >= 0 && psIndices.LTP_scaleIndex < 3);
                    EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.LTP_scaleIndex, Tables.silk_LTPscale_iCDF.GetPointer(), 8);
                }

                Inlines.OpusAssert(condCoding == 0 || psIndices.LTP_scaleIndex == 0);
            }

            psEncC.ec_prevSignalType = psIndices.signalType;

            /***************/
            /* Encode seed */
            /***************/
            Inlines.OpusAssert(psIndices.Seed >= 0 && psIndices.Seed < 4);
            EntropyCoder.ec_enc_icdf(psRangeEnc, psIndices.Seed, Tables.silk_uniform4_iCDF.GetPointer(), 8);
        }
    }
}
