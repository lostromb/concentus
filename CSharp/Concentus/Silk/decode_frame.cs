using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Silk.Enums;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Silk
{
    public static class decode_frame
    {
        /****************/
        /* Decode frame */
        /****************/
        public static int silk_decode_frame(
            silk_decoder_state psDec,                         /* I/O  Pointer to Silk decoder state               */
            ec_ctx psRangeDec,                    /* I/O  Compressor data structure                   */
            Pointer<short> pOut,                         /* O    Pointer to output speech frame              */
            BoxedValue<int> pN,                            /* O    Pointer to size of output frame             */
            int lostFlag,                       /* I    0: no loss, 1 loss, 2 decode fec            */
            int condCoding,                     /* I    The type of conditional coding to use       */
            int arch                            /* I    Run-time architecture                       */
        )
        {
            // [porting note] this is a pointer to a heap struct, not a stack variable
            silk_decoder_control psDecCtrl = new silk_decoder_control();
            int L, mv_len, ret = 0;

            L = psDec.frame_length;
            psDecCtrl.LTP_scale_Q14 = 0;

            /* Safety checks */
            Inlines.OpusAssert(L > 0 && L <= SilkConstants.MAX_FRAME_LENGTH);

            if (lostFlag == DecoderAPIFlag.FLAG_DECODE_NORMAL ||
                (lostFlag == DecoderAPIFlag.FLAG_DECODE_LBRR && psDec.LBRR_flags[psDec.nFramesDecoded] == 1))
            {
                Pointer<short> pulses = Pointer.Malloc<short>((L + SilkConstants.SHELL_CODEC_FRAME_LENGTH - 1) & ~(SilkConstants.SHELL_CODEC_FRAME_LENGTH - 1));
                /*********************************************/
                /* Decode quantization indices of side info  */
                /*********************************************/
                decode_indices.silk_decode_indices(psDec, psRangeDec, psDec.nFramesDecoded, lostFlag, condCoding);

                /*********************************************/
                /* Decode quantization indices of excitation */
                /*********************************************/
                decode_pulses.silk_decode_pulses(psRangeDec, pulses, psDec.indices.signalType,
                        psDec.indices.quantOffsetType, psDec.frame_length);

                /********************************************/
                /* Decode parameters and pulse signal       */
                /********************************************/
                decode_parameters.silk_decode_parameters(psDec, psDecCtrl, condCoding);

                /********************************************************/
                /* Run inverse NSQ                                      */
                /********************************************************/
                decode_core.silk_decode_core(psDec, psDecCtrl, pOut, pulses, arch);

                /********************************************************/
                /* Update PLC state                                     */
                /********************************************************/
                PLC.silk_PLC(psDec, psDecCtrl, pOut, 0, arch);

                psDec.lossCnt = 0;
                psDec.prevSignalType = psDec.indices.signalType;
                Inlines.OpusAssert(psDec.prevSignalType >= 0 && psDec.prevSignalType <= 2);

                /* A frame has been decoded without errors */
                psDec.first_frame_after_reset = 0;
            }
            else
            {
                /* Handle packet loss by extrapolation */
                PLC.silk_PLC(psDec, psDecCtrl, pOut, 1, arch);
            }

            /*************************/
            /* Update output buffer. */
            /*************************/
            Inlines.OpusAssert(psDec.ltp_mem_length >= psDec.frame_length);
            mv_len = psDec.ltp_mem_length - psDec.frame_length;
            // FIXME CHECK THIS
            // silk_memmove(psDec.outBuf, &psDec.outBuf[psDec.frame_length], mv_len * sizeof(short));
            psDec.outBuf.Point(psDec.frame_length).MemMove(0 - psDec.frame_length, mv_len);
            pOut.MemCopyTo(psDec.outBuf.Point(mv_len), psDec.frame_length);

            /************************************************/
            /* Comfort noise generation / estimation        */
            /************************************************/
            CNG.silk_CNG(psDec, psDecCtrl, pOut, L);

            /****************************************************************/
            /* Ensure smooth connection of extrapolated and good frames     */
            /****************************************************************/
            PLC.silk_PLC_glue_frames(psDec, pOut, L);

            /* Update some decoder state variables */
            psDec.lagPrev = psDecCtrl.pitchL[psDec.nb_subfr - 1];

            /* Set output frame length */
            pN.Val = L;
            
            return ret;
        }
    }
}
