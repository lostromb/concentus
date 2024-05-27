/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2010 Xiph.Org Foundation
   Copyright (c) 2008 Gregory Maxwell
   Written by Jean-Marc Valin and Gregory Maxwell */
/*
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using HellaUnsafe.Common;
using static System.Math;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.Bands;
using static HellaUnsafe.Celt.Celt;
using static HellaUnsafe.Celt.CeltLPC;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.EntEnc;
using static HellaUnsafe.Celt.EntDec;
using static HellaUnsafe.Celt.Laplace;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Celt.Modes;
using static HellaUnsafe.Celt.Pitch;
using static HellaUnsafe.Celt.Rate;
using static HellaUnsafe.Celt.QuantBands;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.OpusDefines;

namespace HellaUnsafe.Celt
{
    internal static class CeltDecoder
    {
        /* The maximum pitch lag to allow in the pitch-based PLC. It's possible to save
           CPU time in the PLC pitch search by making this smaller than MAX_PERIOD. The
           current value corresponds to a pitch of 66.67 Hz. */
        internal const int PLC_PITCH_LAG_MAX = 720;

        /* The minimum pitch lag to allow in the pitch-based PLC. This corresponds to a
           pitch of 480 Hz. */
        internal const int PLC_PITCH_LAG_MIN = 100;
        internal const int DECODE_BUFFER_SIZE = 2048;
        internal const int PLC_UPDATE_FRAMES = 4;

        internal unsafe struct OpusCustomDecoder
        {
            internal StructRef<OpusCustomMode> mode;
            internal int overlap;
            internal int channels;
            internal int stream_channels;

            internal int downsample;
            internal int start, end;
            internal int signalling;
            internal int disable_inv;
            internal int complexity;

            /* Everything beyond this point gets cleared on a reset */
            internal uint rng;
            internal int error;
            internal int last_pitch_index;
            internal int loss_duration;
            internal int skip_plc;
            internal int postfilter_period;
            internal int postfilter_period_old;
            internal float postfilter_gain;
            internal float postfilter_gain_old;
            internal int postfilter_tapset;
            internal int postfilter_tapset_old;
            internal int prefilter_and_fold;

            internal fixed float preemph_memD[2];

            /// <summary>
            /// Scratch space used by the decoder. It is actually a variable-sized
            /// field that resulted in a variable-sized struct. There are 6 distinct regions inside.
            /// val32 decode_mem[],     Size = channels*(DECODE_BUFFER_SIZE+mode.overlap)
            /// val16 lpc[],            Size = channels*LPC_ORDER
            /// val16 oldEBands[],      Size = 2*mode.nbEBands
            /// val16 oldLogE[],        Size = 2*mode.nbEBands
            /// val16 oldLogE2[],       Size = 2*mode.nbEBands
            /// val16 backgroundLogE[], Size = 2*mode.nbEBands
            /// </summary>
            internal float[] _decode_mem; /* Size = channels*(DECODE_BUFFER_SIZE+mode.overlap) */
            /* opus_val16 lpc[],  Size = channels*CELT_LPC_ORDER */
            /* opus_val16 oldEBands[], Size = 2*mode.nbEBands */
            /* opus_val16 oldLogE[], Size = 2*mode.nbEBands */
            /* opus_val16 oldLogE2[], Size = 2*mode.nbEBands */
            /* opus_val16 backgroundLogE[], Size = 2*mode.nbEBands */
        };

        internal static void validate_celt_decoder(in OpusCustomDecoder st)
        {
            //ASSERT(st.mode == opus_custom_mode_create(48000, 960, null));
            ASSERT(st.overlap == 120);
            ASSERT(st.end <= 21);
            ASSERT(st.channels == 1 || st.channels == 2);
            ASSERT(st.stream_channels == 1 || st.stream_channels == 2);
            ASSERT(st.downsample > 0);
            ASSERT(st.start == 0 || st.start == 17);
            ASSERT(st.start < st.end);
            ASSERT(st.last_pitch_index <= PLC_PITCH_LAG_MAX);
            ASSERT(st.last_pitch_index >= PLC_PITCH_LAG_MIN || st.last_pitch_index == 0);
            ASSERT(st.postfilter_period < MAX_PERIOD);
            ASSERT(st.postfilter_period >= COMBFILTER_MINPERIOD || st.postfilter_period == 0);
            ASSERT(st.postfilter_period_old < MAX_PERIOD);
            ASSERT(st.postfilter_period_old >= COMBFILTER_MINPERIOD || st.postfilter_period_old == 0);
            ASSERT(st.postfilter_tapset <= 2);
            ASSERT(st.postfilter_tapset >= 0);
            ASSERT(st.postfilter_tapset_old <= 2);
            ASSERT(st.postfilter_tapset_old >= 0);
        }

        internal static int celt_decoder_init(ref OpusCustomDecoder st, int sampling_rate, int channels)
        {
            int ret;
            int err;
            ret = opus_custom_decoder_init(ref st, opus_custom_mode_create(48000, 960, out err), channels);
            if (ret != OPUS_OK)
                return ret;
            st.downsample = resampling_factor(sampling_rate);
            if (st.downsample == 0)
                return OPUS_BAD_ARG;
            else
                return OPUS_OK;
        }

        /// <summary>
        /// Gets the number of elements in _decode_mem for a given decoder
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="channels"></param>
        /// <returns></returns>
        internal static int opus_custom_decoder_get_memory_size(in OpusCustomMode mode, int channels)
        {
            return (channels * (DECODE_BUFFER_SIZE + mode.overlap))
                    + (channels * CELT_LPC_ORDER)
                    + (4 * 2 * mode.nbEBands);
        }

        internal static int opus_custom_decoder_init(ref OpusCustomDecoder st, StructRef<OpusCustomMode> mode, int channels)
        {
            if (channels < 0 || channels > 2)
                return OPUS_BAD_ARG;

            st = default;
            //OPUS_CLEAR((char*)st, opus_custom_decoder_get_size(mode, channels));

            st.mode = mode;
            st.overlap = mode.Value.overlap;
            st.stream_channels = st.channels = channels;

            st.downsample = 1;
            st.start = 0;
            st.end = st.mode.Value.effEBands;
            st.signalling = 1;
            st.disable_inv = channels == 1 ? 1 : 0;
            st._decode_mem = new float[opus_custom_decoder_get_memory_size(mode.Value, channels)];

            opus_custom_decoder_ctl(ref st, OPUS_RESET_STATE);

            return OPUS_OK;
        }

        internal static unsafe void deemphasis_stereo_simple(float** input, float* pcm, int N, in float coef0,
            float* mem)
        {
            float* x0;
            float* x1;
            float m0, m1;
            int j;
            x0 = input[0];
            x1 = input[1];
            m0 = mem[0];
            m1 = mem[1];
            for (j = 0; j < N; j++)
            {
                float tmp0, tmp1;
                /* Add VERY_SMALL to x[] first to reduce dependency chain. */
                tmp0 = x0[j] + VERY_SMALL + m0;
                tmp1 = x1[j] + VERY_SMALL + m1;
                m0 = MULT16_32_Q15(coef0, tmp0);
                m1 = MULT16_32_Q15(coef0, tmp1);
                pcm[2 * j] = SCALEOUT(SIG2WORD16(tmp0));
                pcm[2 * j + 1] = SCALEOUT(SIG2WORD16(tmp1));
            }
            mem[0] = m0;
            mem[1] = m1;
        }








        /// <summary>
        /// For int setters
        /// </summary>
        /// <param name="st"></param>
        /// <param name="request"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static int opus_custom_decoder_ctl(ref OpusCustomDecoder st, int request, int value)
        {
            switch (request)
            {
                case OPUS_SET_COMPLEXITY_REQUEST:
                    {
                        if (value < 0 || value > 10)
                        {
                            goto bad_arg;
                        }
                        st.complexity = value;
                    }
                    break;
                case CELT_SET_START_BAND_REQUEST:
                    {
                        if (value < 0 || value >= st.mode.Value.nbEBands)
                            goto bad_arg;
                        st.start = value;
                    }
                    break;
                case CELT_SET_END_BAND_REQUEST:
                    {
                        if (value < 1 || value > st.mode.Value.nbEBands)
                            goto bad_arg;
                        st.end = value;
                    }
                    break;
                case CELT_SET_CHANNELS_REQUEST:
                    {
                        if (value < 1 || value > 2)
                            goto bad_arg;
                        st.stream_channels = value;
                    }
                    break;
                case CELT_SET_SIGNALLING_REQUEST:
                    {
                        st.signalling = value;
                    }
                    break;
                case OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST:
                    {
                        if (value < 0 || value > 1)
                        {
                            goto bad_arg;
                        }
                        st.disable_inv = value;
                    }
                    break;
                default:
                    goto bad_request;
            }
            return OPUS_OK;
            bad_arg:
            return OPUS_BAD_ARG;
            bad_request:
            return OPUS_UNIMPLEMENTED;
        }

        /// <summary>
        /// For int getters
        /// </summary>
        /// <param name="st"></param>
        /// <param name="request"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static int opus_custom_decoder_ctl(ref OpusCustomDecoder st, int request, out int value)
        {
            value = 0;
            switch (request)
            {
                case OPUS_GET_COMPLEXITY_REQUEST:
                    {
                        value = st.complexity;
                    }
                    break;
                case CELT_GET_AND_CLEAR_ERROR_REQUEST:
                    {
                        value = st.error;
                        st.error = 0;
                    }
                    break;
                case OPUS_GET_LOOKAHEAD_REQUEST:
                    {
                        value = st.overlap / st.downsample;
                    }
                    break;
                case OPUS_GET_PITCH_REQUEST:
                    {
                        value = st.postfilter_period;
                    }
                    break;
                case OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST:
                    {
                        value = st.disable_inv;
                    }
                    break;
                default:
                    goto bad_request;
            }
            return OPUS_OK;
            bad_request:
            return OPUS_UNIMPLEMENTED;
        }

        /// <summary>
        /// For uint getters
        /// </summary>
        /// <param name="st"></param>
        /// <param name="request"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static int opus_custom_decoder_ctl(ref OpusCustomDecoder st, int request, out uint value)
        {
            value = 0;
            switch (request)
            {
                case OPUS_GET_FINAL_RANGE_REQUEST:
                    {
                        value = st.rng;
                    }
                    break;
                default:
                    goto bad_request;
            }
            return OPUS_OK;
            bad_request:
            return OPUS_UNIMPLEMENTED;
        }

        /// <summary>
        /// For reset state
        /// </summary>
        /// <param name="st"></param>
        /// <param name="request"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static unsafe int opus_custom_decoder_ctl(ref OpusCustomDecoder st, int request)
        {
            switch (request)
            {
                case OPUS_RESET_STATE:
                    {
                        int i;
                        float* lpc, oldBandE, oldLogE, oldLogE2;
                        fixed (float* mem = st._decode_mem)
                        {
                            lpc = mem + (DECODE_BUFFER_SIZE + st.overlap) * st.channels;
                            oldBandE = lpc + st.channels * CELT_LPC_ORDER;
                            oldLogE = oldBandE + 2 * st.mode.Value.nbEBands;
                            oldLogE2 = oldLogE + 2 * st.mode.Value.nbEBands;

                            st.rng = 0;
                            st.error = 0;
                            st.last_pitch_index = 0;
                            st.loss_duration = 0;
                            st.skip_plc = 0;
                            st.postfilter_period = 0;
                            st.postfilter_period_old = 0;
                            st.postfilter_gain = 0;
                            st.postfilter_gain_old = 0;
                            st.postfilter_tapset = 0;
                            st.postfilter_tapset_old = 0;
                            st.prefilter_and_fold = 0;
                            st.preemph_memD[0] = 0;
                            st.preemph_memD[1] = 0;
                            OPUS_CLEAR(mem, opus_custom_decoder_get_memory_size(st.mode.Value, st.channels));

                            for (i = 0; i < 2 * st.mode.Value.nbEBands; i++)
                                oldLogE[i] = oldLogE2[i] = -QCONST16(28.0f, DB_SHIFT);
                            st.skip_plc = 1;
                        }
                    }
                    break;
                default:
                    goto bad_request;
            }
            return OPUS_OK;
            bad_request:
            return OPUS_UNIMPLEMENTED;
        }

        /// <summary>
        /// For getting celt mode only
        /// </summary>
        /// <param name="st"></param>
        /// <param name="request"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static int opus_custom_decoder_ctl(ref OpusCustomDecoder st, int request, out StructRef<OpusCustomMode> value)
        {
            switch (request)
            {
                case CELT_GET_MODE_REQUEST:
                    {
                        value = st.mode;
                    }
                    break;
                default:
                    goto bad_request;
            }
            return OPUS_OK;
            bad_request:
            value = null;
            return OPUS_UNIMPLEMENTED;
        }
    }
}
