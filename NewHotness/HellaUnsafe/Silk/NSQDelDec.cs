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

using System;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Inlines;
using static HellaUnsafe.Silk.NSQ;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.Tables;
using static HellaUnsafe.Silk.LPCAnalysisFilter;

namespace HellaUnsafe.Silk
{
    internal static unsafe class NSQDelDec
    {
        internal unsafe struct NSQ_del_dec_struct
        {
            internal fixed int sLPC_Q14[MAX_SUB_FRAME_LENGTH + NSQ_LPC_BUF_LENGTH];
            internal fixed int RandState[DECISION_DELAY];
            internal fixed int Q_Q10[DECISION_DELAY];
            internal fixed int Xq_Q14[DECISION_DELAY];
            internal fixed int Pred_Q15[DECISION_DELAY];
            internal fixed int Shape_Q14[DECISION_DELAY];
            internal fixed int sAR2_Q14[MAX_SHAPE_LPC_ORDER];
            internal int LF_AR_Q14;
            internal int Diff_Q14;
            internal int Seed;
            internal int SeedInit;
            internal int RD_Q10;
        };

        internal unsafe struct NSQ_sample_struct
        {
            internal int Q_Q10;
            internal int RD_Q10;
            internal int xq_Q14;
            internal int LF_AR_Q14;
            internal int Diff_Q14;
            internal int sLTP_shp_Q14;
            internal int LPC_exc_Q14;
        };

        internal unsafe struct NSQ_sample_pair
        {
            internal NSQ_sample_struct _0;
            internal NSQ_sample_struct _1;
        }

        internal static unsafe void silk_NSQ_del_dec(
            in silk_encoder_state* psEncC,                                      /* I    Encoder State                   */
            silk_nsq_state* NSQState,                                         /* I/O  NSQ state                       */
            SideInfoIndices* psIndices,                                   /* I/O  Quantization Indices            */
            short* x16,                                        /* I    Input                           */
            sbyte* pulses,                                     /* O    Quantized pulse signal          */
            in short* PredCoef_Q12,                                /* I    Short term prediction coefs     */
            in short* LTPCoef_Q14/*[ LTP_ORDER * MAX_NB_SUBFR ]*/,      /* I    Long term prediction coefs      */
            in short* AR_Q13/*[ MAX_NB_SUBFR * MAX_SHAPE_LPC_ORDER ]*/, /* I    Noise shaping coefs             */
            in int* HarmShapeGain_Q14/*[ MAX_NB_SUBFR ]*/,            /* I    Long term shaping coefs         */
            in int* Tilt_Q14/*[ MAX_NB_SUBFR ]*/,                     /* I    Spectral tilt                   */
            in int* LF_shp_Q14/*[ MAX_NB_SUBFR ]*/,                   /* I    Low frequency shaping coefs     */
            in int* Gains_Q16/*[ MAX_NB_SUBFR ]*/,                    /* I    Quantization step sizes         */
            in int* pitchL/*[ MAX_NB_SUBFR ]*/,                       /* I    Pitch lags                      */
            in int Lambda_Q10,                                   /* I    Rate/distortion tradeoff        */
            in int LTP_scale_Q14                                 /* I    LTP state scaling               */
        )
        {
            int i, k, lag, start_idx, LSF_interpolation_flag, Winner_ind, subfr;
            int last_smple_idx, smpl_buf_idx, decisionDelay;
            short* A_Q12, B_Q14, AR_shp_Q13;
            short* pxq;
            int[] sLTP_Q15_data;
            short[] sLTP_data;
            int[] x_sc_Q10_data;
            int[] delayedGain_Q10_data;

            int HarmShapeFIRPacked_Q14;
            int offset_Q10;
            int RDmin_Q10, Gain_Q10;
            NSQ_del_dec_struct[] psDelDec_data; // pointer to array of deldec structs, length == psEncC->nStatesDelayedDecision
            NSQ_del_dec_struct* psDD;

            /* Set unvoiced lag to the previous one, overwrite later for voiced */
            lag = NSQState->lagPrev;

            silk_assert(NSQState->prev_gain_Q16 != 0);

            /* Initialize delayed decision states */
            psDelDec_data = new NSQ_del_dec_struct[psEncC->nStatesDelayedDecision];
            fixed (NSQ_del_dec_struct* psDelDec = psDelDec_data)
            {
                OPUS_CLEAR((byte*)psDelDec, psEncC->nStatesDelayedDecision * sizeof(NSQ_del_dec_struct));
                for (k = 0; k < psEncC->nStatesDelayedDecision; k++)
                {
                    psDD = &psDelDec[k];
                    psDD->Seed = (k + psIndices->Seed) & 3;
                    psDD->SeedInit = psDD->Seed;
                    psDD->RD_Q10 = 0;
                    psDD->LF_AR_Q14 = NSQState->sLF_AR_shp_Q14;
                    psDD->Diff_Q14 = NSQState->sDiff_shp_Q14;
                    psDD->Shape_Q14[0] = NSQState->sLTP_shp_Q14[psEncC->ltp_mem_length - 1];
                    silk_memcpy(psDD->sLPC_Q14, NSQState->sLPC_Q14, NSQ_LPC_BUF_LENGTH * sizeof(int));
                    silk_memcpy(psDD->sAR2_Q14, NSQState->sAR2_Q14, MAX_SHAPE_LPC_ORDER * sizeof(int));
                }

                offset_Q10 = silk_Quantization_Offsets_Q10[psIndices->signalType >> 1][psIndices->quantOffsetType];
                smpl_buf_idx = 0; /* index of oldest samples */

                decisionDelay = silk_min_int(DECISION_DELAY, psEncC->subfr_length);

                /* For voiced frames limit the decision delay to lower than the pitch lag */
                if (psIndices->signalType == TYPE_VOICED)
                {
                    for (k = 0; k < psEncC->nb_subfr; k++)
                    {
                        decisionDelay = silk_min_int(decisionDelay, pitchL[k] - LTP_ORDER / 2 - 1);
                    }
                }
                else
                {
                    if (lag > 0)
                    {
                        decisionDelay = silk_min_int(decisionDelay, lag - LTP_ORDER / 2 - 1);
                    }
                }

                if (psIndices->NLSFInterpCoef_Q2 == 4)
                {
                    LSF_interpolation_flag = 0;
                }
                else
                {
                    LSF_interpolation_flag = 1;
                }

                sLTP_Q15_data = new int[psEncC->ltp_mem_length + psEncC->frame_length];
                sLTP_data = new short[psEncC->ltp_mem_length + psEncC->frame_length];
                x_sc_Q10_data = new int[psEncC->subfr_length];
                delayedGain_Q10_data = new int[DECISION_DELAY];
                fixed (int* sLTP_Q15 = sLTP_Q15_data)
                fixed (short* sLTP = sLTP_data)
                fixed (int* x_sc_Q10 = x_sc_Q10_data)
                fixed (int* delayedGain_Q10 = delayedGain_Q10_data)
                {
                    /* Set up pointers to start of sub frame */
                    pxq = &NSQState->xq[psEncC->ltp_mem_length];
                    NSQState->sLTP_shp_buf_idx = psEncC->ltp_mem_length;
                    NSQState->sLTP_buf_idx = psEncC->ltp_mem_length;
                    subfr = 0;
                    for (k = 0; k < psEncC->nb_subfr; k++)
                    {
                        A_Q12 = &PredCoef_Q12[((k >> 1) | (1 - LSF_interpolation_flag)) * MAX_LPC_ORDER];
                        B_Q14 = &LTPCoef_Q14[k * LTP_ORDER];
                        AR_shp_Q13 = &AR_Q13[k * MAX_SHAPE_LPC_ORDER];

                        /* Noise shape parameters */
                        silk_assert(HarmShapeGain_Q14[k] >= 0);
                        HarmShapeFIRPacked_Q14 = silk_RSHIFT(HarmShapeGain_Q14[k], 2);
                        HarmShapeFIRPacked_Q14 |= silk_LSHIFT((int)silk_RSHIFT(HarmShapeGain_Q14[k], 1), 16);

                        NSQState->rewhite_flag = 0;
                        if (psIndices->signalType == TYPE_VOICED)
                        {
                            /* Voiced */
                            lag = pitchL[k];

                            /* Re-whitening */
                            if ((k & (3 - silk_LSHIFT(LSF_interpolation_flag, 1))) == 0)
                            {
                                if (k == 2)
                                {
                                    /* RESET DELAYED DECISIONS */
                                    /* Find winner */
                                    RDmin_Q10 = psDelDec[0].RD_Q10;
                                    Winner_ind = 0;
                                    for (i = 1; i < psEncC->nStatesDelayedDecision; i++)
                                    {
                                        if (psDelDec[i].RD_Q10 < RDmin_Q10)
                                        {
                                            RDmin_Q10 = psDelDec[i].RD_Q10;
                                            Winner_ind = i;
                                        }
                                    }
                                    for (i = 0; i < psEncC->nStatesDelayedDecision; i++)
                                    {
                                        if (i != Winner_ind)
                                        {
                                            psDelDec[i].RD_Q10 += (silk_int32_MAX >> 4);
                                            silk_assert(psDelDec[i].RD_Q10 >= 0);
                                        }
                                    }

                                    /* Copy final part of signals from winner state to output and long-term filter states */
                                    psDD = &psDelDec[Winner_ind];
                                    last_smple_idx = smpl_buf_idx + decisionDelay;
                                    for (i = 0; i < decisionDelay; i++)
                                    {
                                        last_smple_idx = (last_smple_idx - 1) % DECISION_DELAY;
                                        if (last_smple_idx < 0) last_smple_idx += DECISION_DELAY;
                                        pulses[i - decisionDelay] = (sbyte)silk_RSHIFT_ROUND(psDD->Q_Q10[last_smple_idx], 10);
                                        pxq[i - decisionDelay] = (short)silk_SAT16(silk_RSHIFT_ROUND(
                                            silk_SMULWW(psDD->Xq_Q14[last_smple_idx], Gains_Q16[1]), 14));
                                        NSQState->sLTP_shp_Q14[NSQState->sLTP_shp_buf_idx - decisionDelay + i] = psDD->Shape_Q14[last_smple_idx];
                                    }

                                    subfr = 0;
                                }

                                /* Rewhiten with new A coefs */
                                start_idx = psEncC->ltp_mem_length - lag - psEncC->predictLPCOrder - LTP_ORDER / 2;
                                celt_assert(start_idx > 0);

                                silk_LPC_analysis_filter(&sLTP[start_idx], &NSQState->xq[start_idx + k * psEncC->subfr_length],
                                    A_Q12, psEncC->ltp_mem_length - start_idx, psEncC->predictLPCOrder);

                                NSQState->sLTP_buf_idx = psEncC->ltp_mem_length;
                                NSQState->rewhite_flag = 1;
                            }
                        }

                        silk_nsq_del_dec_scale_states(psEncC, NSQState, psDelDec, x16, x_sc_Q10, sLTP, sLTP_Q15, k,
                            psEncC->nStatesDelayedDecision, LTP_scale_Q14, Gains_Q16, pitchL, psIndices->signalType, decisionDelay);

                        silk_noise_shape_quantizer_del_dec(NSQState, psDelDec, psIndices->signalType, x_sc_Q10, pulses, pxq, sLTP_Q15,
                            delayedGain_Q10, A_Q12, B_Q14, AR_shp_Q13, lag, HarmShapeFIRPacked_Q14, Tilt_Q14[k], LF_shp_Q14[k],
                            Gains_Q16[k], Lambda_Q10, offset_Q10, psEncC->subfr_length, subfr++, psEncC->shapingLPCOrder,
                            psEncC->predictLPCOrder, psEncC->warping_Q16, psEncC->nStatesDelayedDecision, &smpl_buf_idx, decisionDelay);

                        x16 += psEncC->subfr_length;
                        pulses += psEncC->subfr_length;
                        pxq += psEncC->subfr_length;
                    }

                    /* Find winner */
                    RDmin_Q10 = psDelDec[0].RD_Q10;
                    Winner_ind = 0;
                    for (k = 1; k < psEncC->nStatesDelayedDecision; k++)
                    {
                        if (psDelDec[k].RD_Q10 < RDmin_Q10)
                        {
                            RDmin_Q10 = psDelDec[k].RD_Q10;
                            Winner_ind = k;
                        }
                    }

                    /* Copy final part of signals from winner state to output and long-term filter states */
                    psDD = &psDelDec[Winner_ind];
                    psIndices->Seed = (sbyte)psDD->SeedInit;
                    last_smple_idx = smpl_buf_idx + decisionDelay;
                    Gain_Q10 = silk_RSHIFT32(Gains_Q16[psEncC->nb_subfr - 1], 6);
                    for (i = 0; i < decisionDelay; i++)
                    {
                        last_smple_idx = (last_smple_idx - 1) % DECISION_DELAY;
                        if (last_smple_idx < 0) last_smple_idx += DECISION_DELAY;

                        pulses[i - decisionDelay] = (sbyte)silk_RSHIFT_ROUND(psDD->Q_Q10[last_smple_idx], 10);
                        pxq[i - decisionDelay] = (short)silk_SAT16(silk_RSHIFT_ROUND(
                            silk_SMULWW(psDD->Xq_Q14[last_smple_idx], Gain_Q10), 8));
                        NSQState->sLTP_shp_Q14[NSQState->sLTP_shp_buf_idx - decisionDelay + i] = psDD->Shape_Q14[last_smple_idx];
                    }
                    silk_memcpy(NSQState->sLPC_Q14, &psDD->sLPC_Q14[psEncC->subfr_length], NSQ_LPC_BUF_LENGTH * sizeof(int));
                    silk_memcpy(NSQState->sAR2_Q14, psDD->sAR2_Q14, MAX_SHAPE_LPC_ORDER * sizeof(int));

                    /* Update states */
                    NSQState->sLF_AR_shp_Q14 = psDD->LF_AR_Q14;
                    NSQState->sDiff_shp_Q14 = psDD->Diff_Q14;
                    NSQState->lagPrev = pitchL[psEncC->nb_subfr - 1];

                    /* Save quantized speech signal */
                    silk_memmove(NSQState->xq, &NSQState->xq[psEncC->frame_length], psEncC->ltp_mem_length * sizeof(short));
                    silk_memmove(NSQState->sLTP_shp_Q14, &NSQState->sLTP_shp_Q14[psEncC->frame_length], psEncC->ltp_mem_length * sizeof(int));
                }
            }
        }

        internal static unsafe void silk_noise_shape_quantizer_del_dec(
            silk_nsq_state      *NSQ,                   /* I/O  NSQ state                           */
            NSQ_del_dec_struct*  psDelDec,             /* I/O  Delayed decision states             */
            int            signalType,             /* I    Signal type                         */
            in int*    x_Q10,                /* I                                        */
            sbyte*           pulses,               /* O                                        */
            short*          xq,                   /* O                                        */
            int*          sLTP_Q15,             /* I/O  LTP filter state                    */
            int*          delayedGain_Q10,      /* I/O  Gain delay buffer                   */
            in short*    a_Q12,                /* I    Short term prediction coefs         */
            in short*    b_Q14,                /* I    Long term prediction coefs          */
            in short*    AR_shp_Q13,           /* I    Noise shaping coefs                 */
            int            lag,                    /* I    Pitch lag                           */
            int          HarmShapeFIRPacked_Q14, /* I                                        */
            int            Tilt_Q14,               /* I    Spectral tilt                       */
            int          LF_shp_Q14,             /* I                                        */
            int          Gain_Q16,               /* I                                        */
            int            Lambda_Q10,             /* I                                        */
            int            offset_Q10,             /* I                                        */
            int            length,                 /* I    Input length                        */
            int            subfr,                  /* I    Subframe number                     */
            int            shapingLPCOrder,        /* I    Shaping LPC filter order            */
            int            predictLPCOrder,        /* I    Prediction filter order             */
            int            warping_Q16,            /* I                                        */
            int            nStatesDelayedDecision, /* I    Number of states in decision tree   */
            int            *smpl_buf_idx,          /* I/O  Index to newest samples in buffers  */
            int            decisionDelay          /* I                                        */
        )
        {
            int     i, j, k, Winner_ind, RDmin_ind, RDmax_ind, last_smple_idx;
            int   Winner_rand_state;
            int   LTP_pred_Q14, LPC_pred_Q14, n_AR_Q14, n_LTP_Q14;
            int   n_LF_Q14, r_Q10, rr_Q10, rd1_Q10, rd2_Q10, RDmin_Q10, RDmax_Q10;
            int   q1_Q0, q1_Q10, q2_Q10, exc_Q14, LPC_exc_Q14, xq_Q14, Gain_Q10;
            int   tmp1, tmp2, sLF_AR_shp_Q14;
            int   *pred_lag_ptr, shp_lag_ptr, psLPC_Q14;

            
            NSQ_del_dec_struct *psDD;
            NSQ_sample_struct  *psSS;

            celt_assert( nStatesDelayedDecision > 0 );

            // nStates is often 4 or less, and the struct is small enough (64 bytes per pair) we can risk doing a stackalloc here
            NSQ_sample_pair* psSampleState = stackalloc NSQ_sample_pair[nStatesDelayedDecision];

            shp_lag_ptr  = &NSQ->sLTP_shp_Q14[ NSQ->sLTP_shp_buf_idx - lag + HARM_SHAPE_FIR_TAPS / 2 ];
            pred_lag_ptr = &sLTP_Q15[ NSQ->sLTP_buf_idx - lag + LTP_ORDER / 2 ];
            Gain_Q10     = silk_RSHIFT( Gain_Q16, 6 );

            for( i = 0; i < length; i++ ) {
                /* Perform common calculations used in all states */

                /* Long-term prediction */
                if( signalType == TYPE_VOICED ) {
                    /* Unrolled loop */
                    /* Avoids introducing a bias because silk_SMLAWB() always rounds to -inf */
                    LTP_pred_Q14 = 2;
                    LTP_pred_Q14 = silk_SMLAWB( LTP_pred_Q14, pred_lag_ptr[  0 ], b_Q14[ 0 ] );
                    LTP_pred_Q14 = silk_SMLAWB( LTP_pred_Q14, pred_lag_ptr[ -1 ], b_Q14[ 1 ] );
                    LTP_pred_Q14 = silk_SMLAWB( LTP_pred_Q14, pred_lag_ptr[ -2 ], b_Q14[ 2 ] );
                    LTP_pred_Q14 = silk_SMLAWB( LTP_pred_Q14, pred_lag_ptr[ -3 ], b_Q14[ 3 ] );
                    LTP_pred_Q14 = silk_SMLAWB( LTP_pred_Q14, pred_lag_ptr[ -4 ], b_Q14[ 4 ] );
                    LTP_pred_Q14 = silk_LSHIFT( LTP_pred_Q14, 1 );                          /* Q13 -> Q14 */
                    pred_lag_ptr++;
                } else {
                    LTP_pred_Q14 = 0;
                }

                /* Long-term shaping */
                if( lag > 0 ) {
                    /* Symmetric, packed FIR coefficients */
                    n_LTP_Q14 = silk_SMULWB( silk_ADD_SAT32( shp_lag_ptr[ 0 ], shp_lag_ptr[ -2 ] ), HarmShapeFIRPacked_Q14 );
                    n_LTP_Q14 = silk_SMLAWT( n_LTP_Q14, shp_lag_ptr[ -1 ], HarmShapeFIRPacked_Q14 );
                    n_LTP_Q14 = silk_SUB_LSHIFT32( LTP_pred_Q14, n_LTP_Q14, 2 );            /* Q12 -> Q14 */
                    shp_lag_ptr++;
                } else {
                    n_LTP_Q14 = 0;
                }

                for( k = 0; k < nStatesDelayedDecision; k++ ) {
                    /* Delayed decision state */
                    psDD = &psDelDec[ k ];

                    /* Sample state */
                    psSS = &psSampleState[ k ]._0;

                    /* Generate dither */
                    psDD->Seed = silk_RAND( psDD->Seed );

                    /* Pointer used in short term prediction and shaping */
                    psLPC_Q14 = &psDD->sLPC_Q14[ NSQ_LPC_BUF_LENGTH - 1 + i ];
                    /* Short-term prediction */
                    LPC_pred_Q14 = silk_noise_shape_quantizer_short_prediction(psLPC_Q14, a_Q12, predictLPCOrder);
                    LPC_pred_Q14 = silk_LSHIFT( LPC_pred_Q14, 4 );                              /* Q10 -> Q14 */

                    /* Noise shape feedback */
                    celt_assert( ( shapingLPCOrder & 1 ) == 0 );   /* check that order is even */
                    /* Output of lowpass section */
                    tmp2 = silk_SMLAWB( psDD->Diff_Q14, psDD->sAR2_Q14[ 0 ], warping_Q16 );
                    /* Output of allpass section */
                    tmp1 = silk_SMLAWB( psDD->sAR2_Q14[ 0 ], silk_SUB32_ovflw(psDD->sAR2_Q14[ 1 ], tmp2), warping_Q16 );
                    psDD->sAR2_Q14[ 0 ] = tmp2;
                    n_AR_Q14 = silk_RSHIFT( shapingLPCOrder, 1 );
                    n_AR_Q14 = silk_SMLAWB( n_AR_Q14, tmp2, AR_shp_Q13[ 0 ] );
                    /* Loop over allpass sections */
                    for( j = 2; j < shapingLPCOrder; j += 2 ) {
                        /* Output of allpass section */
                        tmp2 = silk_SMLAWB( psDD->sAR2_Q14[ j - 1 ], silk_SUB32_ovflw(psDD->sAR2_Q14[ j + 0 ], tmp1), warping_Q16 );
                        psDD->sAR2_Q14[ j - 1 ] = tmp1;
                        n_AR_Q14 = silk_SMLAWB( n_AR_Q14, tmp1, AR_shp_Q13[ j - 1 ] );
                        /* Output of allpass section */
                        tmp1 = silk_SMLAWB( psDD->sAR2_Q14[ j + 0 ], silk_SUB32_ovflw(psDD->sAR2_Q14[ j + 1 ], tmp2), warping_Q16 );
                        psDD->sAR2_Q14[ j + 0 ] = tmp2;
                        n_AR_Q14 = silk_SMLAWB( n_AR_Q14, tmp2, AR_shp_Q13[ j ] );
                    }
                    psDD->sAR2_Q14[ shapingLPCOrder - 1 ] = tmp1;
                    n_AR_Q14 = silk_SMLAWB( n_AR_Q14, tmp1, AR_shp_Q13[ shapingLPCOrder - 1 ] );

                    n_AR_Q14 = silk_LSHIFT( n_AR_Q14, 1 );                                      /* Q11 -> Q12 */
                    n_AR_Q14 = silk_SMLAWB( n_AR_Q14, psDD->LF_AR_Q14, Tilt_Q14 );              /* Q12 */
                    n_AR_Q14 = silk_LSHIFT( n_AR_Q14, 2 );                                      /* Q12 -> Q14 */

                    n_LF_Q14 = silk_SMULWB( psDD->Shape_Q14[ *smpl_buf_idx ], LF_shp_Q14 );     /* Q12 */
                    n_LF_Q14 = silk_SMLAWT( n_LF_Q14, psDD->LF_AR_Q14, LF_shp_Q14 );            /* Q12 */
                    n_LF_Q14 = silk_LSHIFT( n_LF_Q14, 2 );                                      /* Q12 -> Q14 */

                    /* Input minus prediction plus noise feedback                       */
                    /* r = x[ i ] - LTP_pred - LPC_pred + n_AR + n_Tilt + n_LF + n_LTP  */
                    tmp1 = silk_ADD_SAT32( n_AR_Q14, n_LF_Q14 );                                /* Q14 */
                    tmp2 = silk_ADD32_ovflw( n_LTP_Q14, LPC_pred_Q14 );                         /* Q13 */
                    tmp1 = silk_SUB_SAT32( tmp2, tmp1 );                                        /* Q13 */
                    tmp1 = silk_RSHIFT_ROUND( tmp1, 4 );                                        /* Q10 */

                    r_Q10 = silk_SUB32( x_Q10[ i ], tmp1 );                                     /* residual error Q10 */

                    /* Flip sign depending on dither */
                    if ( psDD->Seed < 0 ) {
                        r_Q10 = -r_Q10;
                    }
                    r_Q10 = silk_LIMIT_32( r_Q10, -(31 << 10), 30 << 10 );

                    /* Find two quantization level candidates and measure their rate-distortion */
                    q1_Q10 = silk_SUB32( r_Q10, offset_Q10 );
                    q1_Q0 = silk_RSHIFT( q1_Q10, 10 );
                    if (Lambda_Q10 > 2048) {
                        /* For aggressive RDO, the bias becomes more than one pulse. */
                        int rdo_offset = Lambda_Q10/2 - 512;
                        if (q1_Q10 > rdo_offset) {
                            q1_Q0 = silk_RSHIFT( q1_Q10 - rdo_offset, 10 );
                        } else if (q1_Q10 < -rdo_offset) {
                            q1_Q0 = silk_RSHIFT( q1_Q10 + rdo_offset, 10 );
                        } else if (q1_Q10 < 0) {
                            q1_Q0 = -1;
                        } else {
                            q1_Q0 = 0;
                        }
                    }
                    if( q1_Q0 > 0 ) {
                        q1_Q10  = silk_SUB32( silk_LSHIFT( q1_Q0, 10 ), QUANT_LEVEL_ADJUST_Q10 );
                        q1_Q10  = silk_ADD32( q1_Q10, offset_Q10 );
                        q2_Q10  = silk_ADD32( q1_Q10, 1024 );
                        rd1_Q10 = silk_SMULBB( q1_Q10, Lambda_Q10 );
                        rd2_Q10 = silk_SMULBB( q2_Q10, Lambda_Q10 );
                    } else if( q1_Q0 == 0 ) {
                        q1_Q10  = offset_Q10;
                        q2_Q10  = silk_ADD32( q1_Q10, 1024 - QUANT_LEVEL_ADJUST_Q10 );
                        rd1_Q10 = silk_SMULBB( q1_Q10, Lambda_Q10 );
                        rd2_Q10 = silk_SMULBB( q2_Q10, Lambda_Q10 );
                    } else if( q1_Q0 == -1 ) {
                        q2_Q10  = offset_Q10;
                        q1_Q10  = silk_SUB32( q2_Q10, 1024 - QUANT_LEVEL_ADJUST_Q10 );
                        rd1_Q10 = silk_SMULBB( -q1_Q10, Lambda_Q10 );
                        rd2_Q10 = silk_SMULBB(  q2_Q10, Lambda_Q10 );
                    } else {            /* q1_Q0 < -1 */
                        q1_Q10  = silk_ADD32( silk_LSHIFT( q1_Q0, 10 ), QUANT_LEVEL_ADJUST_Q10 );
                        q1_Q10  = silk_ADD32( q1_Q10, offset_Q10 );
                        q2_Q10  = silk_ADD32( q1_Q10, 1024 );
                        rd1_Q10 = silk_SMULBB( -q1_Q10, Lambda_Q10 );
                        rd2_Q10 = silk_SMULBB( -q2_Q10, Lambda_Q10 );
                    }
                    rr_Q10  = silk_SUB32( r_Q10, q1_Q10 );
                    rd1_Q10 = silk_RSHIFT( silk_SMLABB( rd1_Q10, rr_Q10, rr_Q10 ), 10 );
                    rr_Q10  = silk_SUB32( r_Q10, q2_Q10 );
                    rd2_Q10 = silk_RSHIFT( silk_SMLABB( rd2_Q10, rr_Q10, rr_Q10 ), 10 );

                    if( rd1_Q10 < rd2_Q10 ) {
                        psSS[ 0 ].RD_Q10 = silk_ADD32( psDD->RD_Q10, rd1_Q10 );
                        psSS[ 1 ].RD_Q10 = silk_ADD32( psDD->RD_Q10, rd2_Q10 );
                        psSS[ 0 ].Q_Q10  = q1_Q10;
                        psSS[ 1 ].Q_Q10  = q2_Q10;
                    } else {
                        psSS[ 0 ].RD_Q10 = silk_ADD32( psDD->RD_Q10, rd2_Q10 );
                        psSS[ 1 ].RD_Q10 = silk_ADD32( psDD->RD_Q10, rd1_Q10 );
                        psSS[ 0 ].Q_Q10  = q2_Q10;
                        psSS[ 1 ].Q_Q10  = q1_Q10;
                    }

                    /* Update states for best quantization */

                    /* Quantized excitation */
                    exc_Q14 = silk_LSHIFT32( psSS[ 0 ].Q_Q10, 4 );
                    if ( psDD->Seed < 0 ) {
                        exc_Q14 = -exc_Q14;
                    }

                    /* Add predictions */
                    LPC_exc_Q14 = silk_ADD32( exc_Q14, LTP_pred_Q14 );
                    xq_Q14      = silk_ADD32_ovflw( LPC_exc_Q14, LPC_pred_Q14 );

                    /* Update states */
                    psSS[ 0 ].Diff_Q14     = silk_SUB32_ovflw( xq_Q14, silk_LSHIFT32( x_Q10[ i ], 4 ) );
                    sLF_AR_shp_Q14         = silk_SUB32_ovflw( psSS[ 0 ].Diff_Q14, n_AR_Q14 );
                    psSS[ 0 ].sLTP_shp_Q14 = silk_SUB_SAT32( sLF_AR_shp_Q14, n_LF_Q14 );
                    psSS[ 0 ].LF_AR_Q14    = sLF_AR_shp_Q14;
                    psSS[ 0 ].LPC_exc_Q14  = LPC_exc_Q14;
                    psSS[ 0 ].xq_Q14       = xq_Q14;

                    /* Update states for second best quantization */

                    /* Quantized excitation */
                    exc_Q14 = silk_LSHIFT32( psSS[ 1 ].Q_Q10, 4 );
                    if ( psDD->Seed < 0 ) {
                        exc_Q14 = -exc_Q14;
                    }

                    /* Add predictions */
                    LPC_exc_Q14 = silk_ADD32( exc_Q14, LTP_pred_Q14 );
                    xq_Q14      = silk_ADD32_ovflw( LPC_exc_Q14, LPC_pred_Q14 );

                    /* Update states */
                    psSS[ 1 ].Diff_Q14     = silk_SUB32_ovflw( xq_Q14, silk_LSHIFT32( x_Q10[ i ], 4 ) );
                    sLF_AR_shp_Q14         = silk_SUB32_ovflw( psSS[ 1 ].Diff_Q14, n_AR_Q14 );
                    psSS[ 1 ].sLTP_shp_Q14 = silk_SUB_SAT32( sLF_AR_shp_Q14, n_LF_Q14 );
                    psSS[ 1 ].LF_AR_Q14    = sLF_AR_shp_Q14;
                    psSS[ 1 ].LPC_exc_Q14  = LPC_exc_Q14;
                    psSS[ 1 ].xq_Q14       = xq_Q14;
                }

                *smpl_buf_idx  = ( *smpl_buf_idx - 1 ) % DECISION_DELAY;
                if( *smpl_buf_idx < 0 ) *smpl_buf_idx += DECISION_DELAY;
                last_smple_idx = ( *smpl_buf_idx + decisionDelay ) % DECISION_DELAY;

                /* Find winner */
                RDmin_Q10 = psSampleState[0]._0.RD_Q10;
                Winner_ind = 0;
                for( k = 1; k < nStatesDelayedDecision; k++ ) {
                    if( psSampleState[ k ]._0.RD_Q10 < RDmin_Q10 ) {
                        RDmin_Q10  = psSampleState[ k ]._0.RD_Q10;
                        Winner_ind = k;
                    }
                }

                /* Increase RD values of expired states */
                Winner_rand_state = psDelDec[ Winner_ind ].RandState[ last_smple_idx ];
                for( k = 0; k < nStatesDelayedDecision; k++ ) {
                    if( psDelDec[ k ].RandState[ last_smple_idx ] != Winner_rand_state ) {
                        psSampleState[ k ]._0.RD_Q10 = silk_ADD32( psSampleState[ k ]._0.RD_Q10, silk_int32_MAX >> 4 );
                        psSampleState[ k ]._1.RD_Q10 = silk_ADD32( psSampleState[ k ]._1.RD_Q10, silk_int32_MAX >> 4 );
                        silk_assert( psSampleState[ k ]._0.RD_Q10 >= 0 );
                    }
                }

                /* Find worst in first set and best in second set */
                RDmax_Q10  = psSampleState[ 0 ]._0.RD_Q10;
                RDmin_Q10  = psSampleState[ 0 ]._1.RD_Q10;
                RDmax_ind = 0;
                RDmin_ind = 0;
                for( k = 1; k < nStatesDelayedDecision; k++ ) {
                    /* find worst in first set */
                    if( psSampleState[ k ]._0.RD_Q10 > RDmax_Q10 ) {
                        RDmax_Q10  = psSampleState[ k ]._0.RD_Q10;
                        RDmax_ind = k;
                    }
                    /* find best in second set */
                    if( psSampleState[ k ]._1.RD_Q10 < RDmin_Q10 ) {
                        RDmin_Q10  = psSampleState[ k ]._1.RD_Q10;
                        RDmin_ind = k;
                    }
                }

                /* Replace a state if best from second set outperforms worst in first set */
                if( RDmin_Q10 < RDmax_Q10 ) {
                    silk_memcpy( ( (int *)&psDelDec[ RDmax_ind ] ) + i,
                                 ( (int *)&psDelDec[ RDmin_ind ] ) + i, sizeof( NSQ_del_dec_struct ) - i * sizeof( int) );

                    // PORTING NOTE make sure this copy works as intended
                    //silk_memcpy( &psSampleState[ RDmax_ind ]._0, &psSampleState[ RDmin_ind ]._1, sizeof( NSQ_sample_struct ) );
                    psSampleState[RDmax_ind]._0 = psSampleState[RDmin_ind]._1;
                }

                /* Write samples from winner to output and long-term filter states */
                psDD = &psDelDec[ Winner_ind ];
                if( subfr > 0 || i >= decisionDelay ) {
                    pulses[  i - decisionDelay ] = (sbyte)silk_RSHIFT_ROUND( psDD->Q_Q10[ last_smple_idx ], 10 );
                    xq[ i - decisionDelay ] = (short)silk_SAT16( silk_RSHIFT_ROUND(
                        silk_SMULWW( psDD->Xq_Q14[ last_smple_idx ], delayedGain_Q10[ last_smple_idx ] ), 8 ) );
                    NSQ->sLTP_shp_Q14[ NSQ->sLTP_shp_buf_idx - decisionDelay ] = psDD->Shape_Q14[ last_smple_idx ];
                    sLTP_Q15[          NSQ->sLTP_buf_idx     - decisionDelay ] = psDD->Pred_Q15[  last_smple_idx ];
                }
                NSQ->sLTP_shp_buf_idx++;
                NSQ->sLTP_buf_idx++;

                /* Update states */
                for( k = 0; k < nStatesDelayedDecision; k++ ) {
                    psDD                                     = &psDelDec[ k ];
                    psSS                                     = &psSampleState[ k ]._0;
                    psDD->LF_AR_Q14                          = psSS->LF_AR_Q14;
                    psDD->Diff_Q14                           = psSS->Diff_Q14;
                    psDD->sLPC_Q14[ NSQ_LPC_BUF_LENGTH + i ] = psSS->xq_Q14;
                    psDD->Xq_Q14[    *smpl_buf_idx ]         = psSS->xq_Q14;
                    psDD->Q_Q10[     *smpl_buf_idx ]         = psSS->Q_Q10;
                    psDD->Pred_Q15[  *smpl_buf_idx ]         = silk_LSHIFT32( psSS->LPC_exc_Q14, 1 );
                    psDD->Shape_Q14[ *smpl_buf_idx ]         = psSS->sLTP_shp_Q14;
                    psDD->Seed                               = silk_ADD32_ovflw( psDD->Seed, silk_RSHIFT_ROUND( psSS->Q_Q10, 10 ) );
                    psDD->RandState[ *smpl_buf_idx ]         = psDD->Seed;
                    psDD->RD_Q10                             = psSS->RD_Q10;
                }
                delayedGain_Q10[     *smpl_buf_idx ]         = Gain_Q10;
            }
            /* Update LPC states */
            for( k = 0; k < nStatesDelayedDecision; k++ ) {
                psDD = &psDelDec[ k ];
                silk_memcpy( psDD->sLPC_Q14, &psDD->sLPC_Q14[ length ], NSQ_LPC_BUF_LENGTH * sizeof( int ) );
            }
        }

        internal static unsafe void silk_nsq_del_dec_scale_states(
            in silk_encoder_state *psEncC,               /* I    Encoder State                       */
            silk_nsq_state      *NSQ,                       /* I/O  NSQ state                           */
            NSQ_del_dec_struct*  psDelDec,                 /* I/O  Delayed decision states             */
            in short*    x16,                      /* I    Input                               */
            int*          x_sc_Q10,                 /* O    Input scaled with 1/Gain in Q10     */
            in short*    sLTP,                     /* I    Re-whitened LTP state in Q0         */
            int*          sLTP_Q15,                 /* O    LTP state matching scaled input     */
            int            subfr,                      /* I    Subframe number                     */
            int            nStatesDelayedDecision,     /* I    Number of del dec states            */
            in int      LTP_scale_Q14,              /* I    LTP state scaling                   */
            in int*    Gains_Q16/*[ MAX_NB_SUBFR ]*/,  /* I                                        */
            in int*      pitchL/*[ MAX_NB_SUBFR ]*/,     /* I    Pitch lag                           */
            in int      signal_type,                /* I    Signal type                         */
            in int      decisionDelay               /* I    Decision delay                      */
        )
        {
            int            i, k, lag;
            int          gain_adj_Q16, inv_gain_Q31, inv_gain_Q26;
            NSQ_del_dec_struct  *psDD;

            lag          = pitchL[ subfr ];
            inv_gain_Q31 = silk_INVERSE32_varQ( silk_max( Gains_Q16[ subfr ], 1 ), 47 );
            silk_assert( inv_gain_Q31 != 0 );

            /* Scale input */
            inv_gain_Q26 = silk_RSHIFT_ROUND( inv_gain_Q31, 5 );
            for( i = 0; i < psEncC->subfr_length; i++ ) {
                x_sc_Q10[ i ] = silk_SMULWW( x16[ i ], inv_gain_Q26 );
            }

            /* After rewhitening the LTP state is un-scaled, so scale with inv_gain_Q16 */
            if( NSQ->rewhite_flag != 0) {
                if( subfr == 0 ) {
                    /* Do LTP downscaling */
                    inv_gain_Q31 = silk_LSHIFT( silk_SMULWB( inv_gain_Q31, LTP_scale_Q14 ), 2 );
                }
                for( i = NSQ->sLTP_buf_idx - lag - LTP_ORDER / 2; i < NSQ->sLTP_buf_idx; i++ ) {
                    silk_assert( i < MAX_FRAME_LENGTH );
                    sLTP_Q15[ i ] = silk_SMULWB( inv_gain_Q31, sLTP[ i ] );
                }
            }

            /* Adjust for changing gain */
            if( Gains_Q16[ subfr ] != NSQ->prev_gain_Q16 ) {
                gain_adj_Q16 =  silk_DIV32_varQ( NSQ->prev_gain_Q16, Gains_Q16[ subfr ], 16 );

                /* Scale long-term shaping state */
                for( i = NSQ->sLTP_shp_buf_idx - psEncC->ltp_mem_length; i < NSQ->sLTP_shp_buf_idx; i++ ) {
                    NSQ->sLTP_shp_Q14[ i ] = silk_SMULWW( gain_adj_Q16, NSQ->sLTP_shp_Q14[ i ] );
                }

                /* Scale long-term prediction state */
                if( signal_type == TYPE_VOICED && NSQ->rewhite_flag == 0 ) {
                    for( i = NSQ->sLTP_buf_idx - lag - LTP_ORDER / 2; i < NSQ->sLTP_buf_idx - decisionDelay; i++ ) {
                        sLTP_Q15[ i ] = silk_SMULWW( gain_adj_Q16, sLTP_Q15[ i ] );
                    }
                }

                for( k = 0; k < nStatesDelayedDecision; k++ ) {
                    psDD = &psDelDec[ k ];

                    /* Scale scalar states */
                    psDD->LF_AR_Q14 = silk_SMULWW( gain_adj_Q16, psDD->LF_AR_Q14 );
                    psDD->Diff_Q14 = silk_SMULWW( gain_adj_Q16, psDD->Diff_Q14 );

                    /* Scale short-term prediction and shaping states */
                    for( i = 0; i < NSQ_LPC_BUF_LENGTH; i++ ) {
                        psDD->sLPC_Q14[ i ] = silk_SMULWW( gain_adj_Q16, psDD->sLPC_Q14[ i ] );
                    }
                    for( i = 0; i < MAX_SHAPE_LPC_ORDER; i++ ) {
                        psDD->sAR2_Q14[ i ] = silk_SMULWW( gain_adj_Q16, psDD->sAR2_Q14[ i ] );
                    }
                    for( i = 0; i < DECISION_DELAY; i++ ) {
                        psDD->Pred_Q15[  i ] = silk_SMULWW( gain_adj_Q16, psDD->Pred_Q15[  i ] );
                        psDD->Shape_Q14[ i ] = silk_SMULWW( gain_adj_Q16, psDD->Shape_Q14[ i ] );
                    }
                }

                /* Save inverse gain */
                NSQ->prev_gain_Q16 = Gains_Q16[ subfr ];
            }
        }
    }
}
