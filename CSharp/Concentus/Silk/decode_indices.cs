using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System;
using System.Diagnostics;


namespace Concentus.Silk
{
    public static class decode_indices
    {
        /* Decode side-information parameters from payload */
        public static void silk_decode_indices(
            silk_decoder_state psDec,                         /* I/O  State                                       */
            ec_ctx psRangeDec,                    /* I/O  Compressor data structure                   */
            int FrameIndex,                     /* I    Frame number                                */
            int decode_LBRR,                    /* I    Flag indicating LBRR data is being decoded  */
            int condCoding                      /* I    The type of conditional coding to use       */
        )
        {
            int i, k, Ix;
            int decode_absolute_lagIndex, delta_lagIndex;
            Pointer<short> ec_ix = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);
            Pointer<byte> pred_Q8 = Pointer.Malloc<byte>(SilkConstants.MAX_LPC_ORDER);

            /*******************************************/
            /* Decode signal type and quantizer offset */
            /*******************************************/
            if (decode_LBRR != 0 || psDec.VAD_flags[FrameIndex] != 0)
            {
                Ix = EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_type_offset_VAD_iCDF.GetPointer(), 8) + 2;
            }
            else {
                Ix = EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_type_offset_no_VAD_iCDF.GetPointer(), 8);
            }
            psDec.indices.signalType = (sbyte)Inlines.silk_RSHIFT(Ix, 1);
            psDec.indices.quantOffsetType = (sbyte)(Ix & 1);

            /****************/
            /* Decode gains */
            /****************/
            /* First subframe */
            if (condCoding == SilkConstants.CODE_CONDITIONALLY)
            {
                /* Conditional coding */
                psDec.indices.GainsIndices[0] = (sbyte)EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_delta_gain_iCDF.GetPointer(), 8);
            }
            else {
                /* Independent coding, in two stages: MSB bits followed by 3 LSBs */
                psDec.indices.GainsIndices[0] = (sbyte)Inlines.silk_LSHIFT(EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_gain_iCDF[psDec.indices.signalType].GetPointer(), 8), 3);
                psDec.indices.GainsIndices[0] += (sbyte)EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_uniform8_iCDF.GetPointer(), 8);
            }

            /* Remaining subframes */
            for (i = 1; i < psDec.nb_subfr; i++)
            {
                psDec.indices.GainsIndices[i] = (sbyte)EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_delta_gain_iCDF.GetPointer(), 8);
            }

            /**********************/
            /* Decode LSF Indices */
            /**********************/
            psDec.indices.NLSFIndices[0] = (sbyte)EntropyCoder.ec_dec_icdf(psRangeDec, psDec.psNLSF_CB.CB1_iCDF.Point((psDec.indices.signalType >> 1) * psDec.psNLSF_CB.nVectors), 8);
            NLSF.silk_NLSF_unpack(ec_ix, pred_Q8, psDec.psNLSF_CB, psDec.indices.NLSFIndices[0]);
            Inlines.OpusAssert(psDec.psNLSF_CB.order == psDec.LPC_order);
            for (i = 0; i < psDec.psNLSF_CB.order; i++)
            {
                Ix = EntropyCoder.ec_dec_icdf(psRangeDec, psDec.psNLSF_CB.ec_iCDF.Point(ec_ix[i]), 8);
                if (Ix == 0)
                {
                    Ix -= EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_NLSF_EXT_iCDF.GetPointer(), 8);
                }
                else if (Ix == 2 * SilkConstants.NLSF_QUANT_MAX_AMPLITUDE)
                {
                    Ix += EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_NLSF_EXT_iCDF.GetPointer(), 8);
                }
                psDec.indices.NLSFIndices[i + 1] = (sbyte)(Ix - SilkConstants.NLSF_QUANT_MAX_AMPLITUDE);
            }

            /* Decode LSF interpolation factor */
            if (psDec.nb_subfr == SilkConstants.MAX_NB_SUBFR)
            {
                psDec.indices.NLSFInterpCoef_Q2 = (sbyte)EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_NLSF_interpolation_factor_iCDF.GetPointer(), 8);
            }
            else {
                psDec.indices.NLSFInterpCoef_Q2 = 4;
            }

            if (psDec.indices.signalType == SilkConstants.TYPE_VOICED)
            {
                /*********************/
                /* Decode pitch lags */
                /*********************/
                /* Get lag index */
                decode_absolute_lagIndex = 1;
                if (condCoding == SilkConstants.CODE_CONDITIONALLY && psDec.ec_prevSignalType == SilkConstants.TYPE_VOICED)
                {
                    /* Decode Delta index */
                    delta_lagIndex = (short)EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_pitch_delta_iCDF.GetPointer(), 8);
                    if (delta_lagIndex > 0)
                    {
                        delta_lagIndex = delta_lagIndex - 9;
                        psDec.indices.lagIndex = (short)(psDec.ec_prevLagIndex + delta_lagIndex);
                        decode_absolute_lagIndex = 0;
                    }
                }
                if (decode_absolute_lagIndex != 0)
                {
                    /* Absolute decoding */
                    psDec.indices.lagIndex = (short)(EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_pitch_lag_iCDF.GetPointer(), 8) * Inlines.silk_RSHIFT(psDec.fs_kHz, 1));
                    psDec.indices.lagIndex += (short)EntropyCoder.ec_dec_icdf(psRangeDec, psDec.pitch_lag_low_bits_iCDF, 8);
                }
                psDec.ec_prevLagIndex = psDec.indices.lagIndex;

                /* Get countour index */
                psDec.indices.contourIndex = (sbyte)EntropyCoder.ec_dec_icdf(psRangeDec, psDec.pitch_contour_iCDF, 8);

                /********************/
                /* Decode LTP gains */
                /********************/
                /* Decode PERIndex value */
                psDec.indices.PERIndex = (sbyte)EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_LTP_per_index_iCDF.GetPointer(), 8);

                for (k = 0; k < psDec.nb_subfr; k++)
                {
                    psDec.indices.LTPIndex[k] = (sbyte)EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_LTP_gain_iCDF_ptrs[psDec.indices.PERIndex].GetPointer(), 8);
                }

                /**********************/
                /* Decode LTP scaling */
                /**********************/
                if (condCoding == SilkConstants.CODE_INDEPENDENTLY)
                {
                    psDec.indices.LTP_scaleIndex = (sbyte)EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_LTPscale_iCDF.GetPointer(), 8);
                }
                else {
                    psDec.indices.LTP_scaleIndex = 0;
                }
            }
            psDec.ec_prevSignalType = psDec.indices.signalType;

            /***************/
            /* Decode seed */
            /***************/
            psDec.indices.Seed = (sbyte)EntropyCoder.ec_dec_icdf(psRangeDec, Tables.silk_uniform4_iCDF.GetPointer(), 8);
        }
    }
}
