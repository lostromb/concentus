/***********************************************************************
Copyright (c) 2006-2011, Skype Limited. All rights reserved.
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
- Redistributions of source code must retain the above copyright notice,
this list of conditions and the following disclaimer.
- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.
- Neither the name of Internet Society, IETF or IETF Trust, nor the
names of specific contributors, may be used to endorse or promote
products derived from this software without specific prior written
permission.
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
***********************************************************************/

using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.CNG;
using static HellaUnsafe.Silk.Control;
using static HellaUnsafe.Silk.DecodeCore;
using static HellaUnsafe.Silk.DecodeIndices;
using static HellaUnsafe.Silk.DecodeParameters;
using static HellaUnsafe.Silk.DecodePulses;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.PLC;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;

namespace HellaUnsafe.Silk
{
    internal static unsafe class DecodeFrame
    {
        /****************/
        /* Decode frame */
        /****************/
        internal static unsafe int silk_decode_frame(
            silk_decoder_state          *psDec,                         /* I/O  Pointer to Silk decoder state               */
            ec_ctx                      *psRangeDec,                    /* I/O  Compressor data structure                   */
            short*                  pOut,                         /* O    Pointer to output speech frame              */
            int                  *pN,                            /* O    Pointer to size of output frame             */
            int                    lostFlag,                       /* I    0: no loss, 1 loss, 2 decode fec            */
            int                    condCoding                     /* I    The type of conditional coding to use       */
        )
        {
            silk_decoder_control psDecCtrl_data = new silk_decoder_control();
            silk_decoder_control* psDecCtrl = &psDecCtrl_data; // not strictly necessary to get a pointer because it's on the stack
            int L, mv_len, ret = 0;

            L = psDec->frame_length;
            psDecCtrl->LTP_scale_Q14 = 0;

            /* Safety checks */
            celt_assert( L > 0 && L <= MAX_FRAME_LENGTH );

            if(   lostFlag == FLAG_DECODE_NORMAL ||
                ( lostFlag == FLAG_DECODE_LBRR && psDec->LBRR_flags[ psDec->nFramesDecoded ] == 1 ) )
            {
                short[] pulses_data = new short[(L + SHELL_CODEC_FRAME_LENGTH - 1) &
                               ~(SHELL_CODEC_FRAME_LENGTH - 1)];
                fixed (short* pulses = pulses_data)
                {
                    /*********************************************/
                    /* Decode quantization indices of side info  */
                    /*********************************************/
                    silk_decode_indices(psDec, psRangeDec, psDec->nFramesDecoded, lostFlag, condCoding);

                    /*********************************************/
                    /* Decode quantization indices of excitation */
                    /*********************************************/
                    silk_decode_pulses(psRangeDec, pulses, psDec->indices.signalType,
                            psDec->indices.quantOffsetType, psDec->frame_length);

                    /********************************************/
                    /* Decode parameters and pulse signal       */
                    /********************************************/
                    silk_decode_parameters(psDec, psDecCtrl, condCoding);

                    /********************************************************/
                    /* Run inverse NSQ                                      */
                    /********************************************************/
                    silk_decode_core(psDec, psDecCtrl, pOut, pulses);

                    /*************************/
                    /* Update output buffer. */
                    /*************************/
                    celt_assert(psDec->ltp_mem_length >= psDec->frame_length);
                    mv_len = psDec->ltp_mem_length - psDec->frame_length;
                    silk_memmove(psDec->outBuf, &psDec->outBuf[psDec->frame_length], mv_len * sizeof(short));
                    silk_memcpy(&psDec->outBuf[mv_len], pOut, psDec->frame_length * sizeof(short));


                    /********************************************************/
                    /* Update PLC state                                     */
                    /********************************************************/
                    silk_PLC(psDec, psDecCtrl, pOut, 0);

                    psDec->lossCnt = 0;
                    psDec->prevSignalType = psDec->indices.signalType;
                    celt_assert(psDec->prevSignalType >= 0 && psDec->prevSignalType <= 2);

                    /* A frame has been decoded without errors */
                    psDec->first_frame_after_reset = 0;
                }
            } else {
                /* Handle packet loss by extrapolation */
                silk_PLC( psDec, psDecCtrl, pOut, 1);

                /*************************/
                /* Update output buffer. */
                /*************************/
                celt_assert( psDec->ltp_mem_length >= psDec->frame_length );
                mv_len = psDec->ltp_mem_length - psDec->frame_length;
                silk_memmove( psDec->outBuf, &psDec->outBuf[ psDec->frame_length ], mv_len * sizeof(short) );
                silk_memcpy( &psDec->outBuf[ mv_len ], pOut, psDec->frame_length * sizeof( short ) );
            }

            /************************************************/
            /* Comfort noise generation / estimation        */
            /************************************************/
            silk_CNG( psDec, psDecCtrl, pOut, L );

            /****************************************************************/
            /* Ensure smooth connection of extrapolated and good frames     */
            /****************************************************************/
            silk_PLC_glue_frames( psDec, pOut, L );

            /* Update some decoder state variables */
            psDec->lagPrev = psDecCtrl->pitchL[ psDec->nb_subfr - 1 ];

            /* Set output frame length */
            *pN = L;

            return ret;
        }
    }
}
