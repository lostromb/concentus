using Concentus.Celt.Enums;
using Concentus.Celt.Structs;
using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Opus.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Concentus.Celt
{
    internal static class celt_decoder
    {
        internal static int celt_decoder_init(CeltDecoder st, int sampling_rate, int channels)
        {
            int ret;
            ret = opus_custom_decoder_init(st, Modes.opus_custom_mode_create(48000, 960, null), channels);
            if (ret != OpusError.OPUS_OK)
                return ret;
            st.downsample = Celt.resampling_factor(sampling_rate);
            if (st.downsample == 0)
                return OpusError.OPUS_BAD_ARG;
            else
                return OpusError.OPUS_OK;
        }

        internal static int opus_custom_decoder_init(CeltDecoder st, CeltMode mode, int channels)
        {
            if (channels < 0 || channels > 2)
                return OpusError.OPUS_BAD_ARG;

            if (st == null)
                return OpusError.OPUS_ALLOC_FAIL;
            
            st.Reset();
            
            st.mode = mode;
            st.overlap = mode.overlap;
            st.stream_channels = st.channels = channels;

            st.downsample = 1;
            st.start = 0;
            st.end = st.mode.effEBands;
            st.signalling = 1;

            st.loss_count = 0;

            // fixme is this necessary if we just call decoder_ctrl right there anyways?
            st.decode_mem = Pointer.Malloc<int>(channels * (CeltConstants.DECODE_BUFFER_SIZE + mode.overlap));
            st.lpc = Pointer.Malloc<int>(channels * CeltConstants.LPC_ORDER);
            st.oldEBands = Pointer.Malloc<int>(2 * mode.nbEBands);
            st.oldLogE = Pointer.Malloc<int>(2 * mode.nbEBands);
            st.oldLogE2 = Pointer.Malloc<int>(2 * mode.nbEBands);
            st.backgroundLogE = Pointer.Malloc<int>(2 * mode.nbEBands);
            
            opus_custom_decoder_ctl(st, OpusControl.OPUS_RESET_STATE);

            return OpusError.OPUS_OK;
        }



        internal static void deemphasis(Pointer<Pointer<int>> input, Pointer<short> pcm, int N, int C, int downsample, Pointer<int> coef,
              Pointer<int> mem, int accum)
        {
            int c;
            int Nd;
            int apply_downsampling = 0;
            int coef0;
            Pointer<int> scratch = Pointer.Malloc<int>(N);
            coef0 = coef[0];
            Nd = N / downsample;
            c = 0; do
            {
                int j;
                Pointer<int> x;
                Pointer<short> y;
                int m = mem[c];
                x = input[c];
                y = pcm.Point(c);
                if (downsample > 1)
                {
                    /* Shortcut for the standard (non-custom modes) case */
                    for (j = 0; j < N; j++)
                    {
                        int tmp = x[j] + m + CeltConstants.VERY_SMALL;
                        m = Inlines.MULT16_32_Q15(coef0, tmp);
                        scratch[j] = tmp;
                    }
                    apply_downsampling = 1;
                }
                else {
                    /* Shortcut for the standard (non-custom modes) case */
                    if (accum != 0) // should never hit this branch?
                    {
                        for (j = 0; j < N; j++)
                        {
                            int tmp = x[j] + m + CeltConstants.VERY_SMALL;
                            m = Inlines.MULT16_32_Q15(coef0, tmp);
                            y[j * C] = Inlines.SAT16(Inlines.ADD32(y[j * C], Inlines.SCALEOUT(Inlines.SIG2WORD16(tmp))));
                        }
                    }
                    else
                    {
                        for (j = 0; j < N; j++)
                        {
                            int tmp = unchecked(x[j] + m + CeltConstants.VERY_SMALL); // Opus bug: This can overflow.
                            if (x[j] > 0 && m > 0 && tmp < 0) // I have hacked it to saturate to INT_MAXVALUE
                            {
                                tmp = int.MaxValue;
                                m = int.MaxValue;
                            }
                            else
                            {
                                m = Inlines.MULT16_32_Q15(coef0, tmp);
                            }
                            y[j * C] = Inlines.SCALEOUT(Inlines.SIG2WORD16(tmp));
                        }
                    }
                }
                mem[c] = m;

                if (apply_downsampling != 0)
                {
                    /* Perform down-sampling */
                    {
                        for (j = 0; j < Nd; j++)
                            y[j * C] = Inlines.SCALEOUT(Inlines.SIG2WORD16(scratch[j * downsample]));
                    }
                }
            } while (++c < C);

        }
        internal static void celt_synthesis(CeltMode mode, Pointer<int> X, Pointer<Pointer<int>> out_syn,
                            Pointer<int> oldBandE, int start, int effEnd, int C, int CC,
                            int isTransient, int LM, int downsample,
                            int silence)
        {
            int c, i;
            int M;
            int b;
            int B;
            int N, NB;
            int shift;
            int nbEBands;
            int overlap;
            Pointer<int> freq;

            overlap = mode.overlap;
            nbEBands = mode.nbEBands;
            N = mode.shortMdctSize << LM;
            freq = Pointer.Malloc<int>(N); /**< Interleaved signal MDCTs */
            M = 1 << LM;

            if (isTransient != 0)
            {
                B = M;
                NB = mode.shortMdctSize;
                shift = mode.maxLM;
            }
            else {
                B = 1;
                NB = mode.shortMdctSize << LM;
                shift = mode.maxLM - LM;
            }

            if (CC == 2 && C == 1)
            {
                /* Copying a mono streams to two channels */
                Pointer<int> freq2;
                Bands.denormalise_bands(mode, X, freq, oldBandE, start, effEnd, M,
                      downsample, silence);
                /* Store a temporary copy in the output buffer because the IMDCT destroys its input. */
                freq2 = out_syn[1].Point(overlap / 2);
                freq.MemCopyTo(freq2, N);
                for (b = 0; b < B; b++)
                    MDCT.clt_mdct_backward(mode.mdct, freq2.Point(b), out_syn[0].Point(NB * b), mode.window, overlap, shift, B);
                for (b = 0; b < B; b++)
                    MDCT.clt_mdct_backward(mode.mdct, freq.Point(b), out_syn[1].Point(NB * b), mode.window, overlap, shift, B);
            }
            else if (CC == 1 && C == 2)
            {
                /* Downmixing a stereo stream to mono */
                Pointer<int> freq2;
                freq2 = out_syn[0].Point(overlap / 2);
                Bands.denormalise_bands(mode, X, freq, oldBandE, start, effEnd, M,
                      downsample, silence);
                /* Use the output buffer as temp array before downmixing. */
                Bands.denormalise_bands(mode, X.Point(N), freq2, oldBandE.Point(nbEBands), start, effEnd, M,
                      downsample, silence);
                for (i = 0; i < N; i++)
                    freq[i] = Inlines.HALF32(Inlines.ADD32(freq[i], freq2[i]));
                for (b = 0; b < B; b++)
                    MDCT.clt_mdct_backward(mode.mdct, freq.Point(b), out_syn[0].Point(NB * b), mode.window, overlap, shift, B);
            }
            else {
                /* Normal case (mono or stereo) */
                c = 0; do
                {
                    Bands.denormalise_bands(mode, X.Point(c * N), freq, oldBandE.Point(c * nbEBands), start, effEnd, M,
                          downsample, silence);
                    for (b = 0; b < B; b++)
                        MDCT.clt_mdct_backward(mode.mdct, freq.Point(b), out_syn[c].Point(NB * b), mode.window, overlap, shift, B);
                } while (++c < CC);
            }

        }

        internal static void tf_decode(int start, int end, int isTransient, Pointer<int> tf_res, int LM, EntropyCoder dec)
        {
            int i, curr, tf_select;
            int tf_select_rsv;
            int tf_changed;
            int logp;
            uint budget;
            uint tell;

            budget = dec.storage * 8;
            tell = (uint)dec.ec_tell();
            logp = isTransient != 0 ? 2 : 4;
            tf_select_rsv = (LM > 0 && tell + logp + 1 <= budget) ? 1 : 0;
            budget -= (uint)tf_select_rsv;
            tf_changed = curr = 0;
            for (i = start; i < end; i++)
            {
                if (tell + logp <= budget)
                {
                    curr ^= dec.ec_dec_bit_logp((uint)logp);
                    tell = (uint)dec.ec_tell();
                    tf_changed |= curr;
                }
                tf_res[i] = curr;
                logp = isTransient != 0 ? 4 : 5;
            }
            tf_select = 0;
            if (tf_select_rsv != 0 &&
              Tables.tf_select_table[LM][4 * isTransient + 0 + tf_changed] !=
              Tables.tf_select_table[LM][4 * isTransient + 2 + tf_changed])
            {
                tf_select = dec.ec_dec_bit_logp(1);
            }
            for (i = start; i < end; i++)
            {
                tf_res[i] = Tables.tf_select_table[LM][4 * isTransient + 2 * tf_select + tf_res[i]];
            }
        }
        
        internal static int celt_plc_pitch_search(Pointer<Pointer<int>> decode_mem, int C)
        {
            BoxedValue<int> pitch_index = new BoxedValue<int>();
            Pointer<int> lp_pitch_buf = Pointer.Malloc<int>(CeltConstants.DECODE_BUFFER_SIZE >> 1);
            Pitch.pitch_downsample(decode_mem, lp_pitch_buf,
                  CeltConstants.DECODE_BUFFER_SIZE, C);
            Pitch.pitch_search(lp_pitch_buf.Point(CeltConstants.PLC_PITCH_LAG_MAX >> 1), lp_pitch_buf,
                  CeltConstants.DECODE_BUFFER_SIZE - CeltConstants.PLC_PITCH_LAG_MAX,
                  CeltConstants.PLC_PITCH_LAG_MAX - CeltConstants.PLC_PITCH_LAG_MIN, pitch_index);
            pitch_index.Val = CeltConstants.PLC_PITCH_LAG_MAX - pitch_index.Val;

            return pitch_index.Val;
        }

        internal static void celt_decode_lost(CeltDecoder st, int N, int LM)
        {
            int c;
            int i;
            int C = st.channels;
            Pointer<Pointer<int>> decode_mem = Pointer.Malloc<Pointer<int>>(2);
            Pointer<Pointer<int>> out_syn = Pointer.Malloc<Pointer<int>>(2);
            Pointer<int> lpc;
            Pointer<int> oldBandE, oldLogE, oldLogE2, backgroundLogE;
            CeltMode mode; // porting note: pointer
            int nbEBands;
            int overlap;
            int start;
            int loss_count;
            int noise_based;
            Pointer<short> eBands;
            
            mode = st.mode;
            nbEBands = mode.nbEBands;
            overlap = mode.overlap;
            eBands = mode.eBands;

            c = 0; do
            {
                decode_mem[c] = st.decode_mem.Point(c * (CeltConstants.DECODE_BUFFER_SIZE + overlap));
                out_syn[c] = decode_mem[c].Point(CeltConstants.DECODE_BUFFER_SIZE - N);
            } while (++c < C);

            // fixme: can remove these temp pointers
            lpc = st.lpc;
            oldBandE = st.oldEBands;
            oldLogE = st.oldLogE;
            oldLogE2 = st.oldLogE2;
            backgroundLogE = st.backgroundLogE;

            loss_count = st.loss_count;
            start = st.start;
            noise_based = (loss_count >= 5 || start != 0) ? 1 : 0;
            if (noise_based != 0)
            {
                /* Noise-based PLC/CNG */
                Pointer<int> X;
                uint seed;
                int end;
                int effEnd;
                int decay;
                end = st.end;
                effEnd = Inlines.IMAX(start, Inlines.IMIN(end, mode.effEBands));

                X = Pointer.Malloc<int>(C * N);   /**< Interleaved normalised MDCTs */

                /* Energy decay */
                decay = loss_count == 0 ? Inlines.QCONST16(1.5f, CeltConstants.DB_SHIFT) : Inlines.QCONST16(0.5f, CeltConstants.DB_SHIFT);
                c = 0; do
                {
                    for (i = start; i < end; i++)
                        oldBandE[c * nbEBands + i] = Inlines.MAX16(backgroundLogE[c * nbEBands + i], oldBandE[c * nbEBands + i] - decay);
                } while (++c < C);
                seed = st.rng;
                for (c = 0; c < C; c++)
                {
                    for (i = start; i < effEnd; i++)
                    {
                        int j;
                        int boffs;
                        int blen;
                        boffs = N * c + (eBands[i] << LM);
                        blen = (eBands[i + 1] - eBands[i]) << LM;
                        for (j = 0; j < blen; j++)
                        {
                            seed = Bands.celt_lcg_rand(seed);
                            X[boffs + j] = (unchecked((int)seed) >> 20);
                        }

                        VQ.renormalise_vector(X.Point(boffs), blen, CeltConstants.Q15ONE);
                    }
                }
                st.rng = seed;

                c = 0;
                do
                {
                    decode_mem[c].Point(N).MemMove(0 - N, CeltConstants.DECODE_BUFFER_SIZE - N + (overlap >> 1));
                } while (++c < C);

                celt_synthesis(mode, X, out_syn, oldBandE, start, effEnd, C, C, 0, LM, st.downsample, 0);
            }
            else
            {
                /* Pitch-based PLC */
                Pointer<int> window;
                int fade = CeltConstants.Q15ONE;
                int pitch_index;
                Pointer<int> etmp;
                Pointer<int> exc;

                if (loss_count == 0)
                {
                    st.last_pitch_index = pitch_index = celt_plc_pitch_search(decode_mem, C);
                }
                else {
                    pitch_index = st.last_pitch_index;
                    fade = Inlines.QCONST16(.8f, 15);
                }

                etmp = Pointer.Malloc<int>(overlap);
                exc = Pointer.Malloc<int>(CeltConstants.MAX_PERIOD);
                window = mode.window;
                c = 0; do
                {
                    int decay;
                    int attenuation;
                    int S1 = 0;
                    Pointer<int> buf;
                    int extrapolation_offset;
                    int extrapolation_len;
                    int exc_length;
                    int j;

                    buf = decode_mem[c];
                    for (i = 0; i < CeltConstants.MAX_PERIOD; i++)
                    {
                        exc[i] = Inlines.ROUND16(buf[CeltConstants.DECODE_BUFFER_SIZE - CeltConstants.MAX_PERIOD + i], CeltConstants.SIG_SHIFT);
                    }

                    if (loss_count == 0)
                    {
                        Pointer<int> ac = Pointer.Malloc<int>(CeltConstants.LPC_ORDER + 1);
                        /* Compute LPC coefficients for the last MAX_PERIOD samples before
                           the first loss so we can work in the excitation-filter domain. */
                        CeltLPC._celt_autocorr(exc, ac, window, overlap,
                               CeltConstants.LPC_ORDER, CeltConstants.MAX_PERIOD);
                        /* Add a noise floor of -40 dB. */
                        ac[0] += Inlines.SHR32(ac[0], 13);
                        /* Use lag windowing to stabilize the Levinson-Durbin recursion. */
                        for (i = 1; i <= CeltConstants.LPC_ORDER; i++)
                        {
                            /*ac[i] *= exp(-.5*(2*M_PI*.002*i)*(2*M_PI*.002*i));*/
                            ac[i] -= Inlines.MULT16_32_Q15(2 * i * i, ac[i]);
                        }
                        CeltLPC._celt_lpc(lpc.Point(c * CeltConstants.LPC_ORDER), ac, CeltConstants.LPC_ORDER);
                    }
                    /* We want the excitation for 2 pitch periods in order to look for a
                       decaying signal, but we can't get more than MAX_PERIOD. */
                    exc_length = Inlines.IMIN(2 * pitch_index, CeltConstants.MAX_PERIOD);
                    /* Initialize the LPC history with the samples just before the start
                       of the region for which we're computing the excitation. */
                    {
                        Pointer<int> lpc_mem = Pointer.Malloc<int>(CeltConstants.LPC_ORDER);
                        for (i = 0; i < CeltConstants.LPC_ORDER; i++)
                        {
                            lpc_mem[i] =
                                  Inlines.ROUND16(buf[CeltConstants.DECODE_BUFFER_SIZE - exc_length - 1 - i], CeltConstants.SIG_SHIFT);
                        }

                        /* Compute the excitation for exc_length samples before the loss. */
                        Kernels.celt_fir(exc.Point(CeltConstants.MAX_PERIOD - exc_length), lpc.Point(c * CeltConstants.LPC_ORDER),
                              exc.Point(CeltConstants.MAX_PERIOD - exc_length), exc_length, CeltConstants.LPC_ORDER, lpc_mem);
                    }

                    /* Check if the waveform is decaying, and if so how fast.
                       We do this to avoid adding energy when concealing in a segment
                       with decaying energy. */
                    {
                        int E1 = 1, E2 = 1;
                        int decay_length;
                        int shift = Inlines.IMAX(0, 2 * Inlines.celt_zlog2(Inlines.celt_maxabs16(exc.Point(CeltConstants.MAX_PERIOD - exc_length), exc_length)) - 20);
                        decay_length = exc_length >> 1;
                        for (i = 0; i < decay_length; i++)
                        {
                            int e;
                            e = exc[CeltConstants.MAX_PERIOD - decay_length + i];
                            E1 += Inlines.SHR32(Inlines.MULT16_16(e, e), shift);
                            e = exc[CeltConstants.MAX_PERIOD - 2 * decay_length + i];
                            E2 += Inlines.SHR32(Inlines.MULT16_16(e, e), shift);
                        }
                        E1 = Inlines.MIN32(E1, E2);
                        decay = Inlines.celt_sqrt(Inlines.frac_div32(Inlines.SHR32(E1, 1), E2));
                    }

                    /* Move the decoder memory one frame to the left to give us room to
                       add the data for the new frame. We ignore the overlap that extends
                       past the end of the buffer, because we aren't going to use it. */
                    buf.Point(N).MemMove(0 - N, CeltConstants.DECODE_BUFFER_SIZE - N);

                    /* Extrapolate from the end of the excitation with a period of
                       "pitch_index", scaling down each period by an additional factor of
                       "decay". */
                    extrapolation_offset = CeltConstants.MAX_PERIOD - pitch_index;
                    /* We need to extrapolate enough samples to cover a complete MDCT
                       window (including overlap/2 samples on both sides). */
                    extrapolation_len = N + overlap;
                    /* We also apply fading if this is not the first loss. */
                    attenuation = Inlines.MULT16_16_Q15(fade, decay);
                    for (i = j = 0; i < extrapolation_len; i++, j++)
                    {
                        int tmp;
                        if (j >= pitch_index)
                        {
                            j -= pitch_index;
                            attenuation = Inlines.MULT16_16_Q15(attenuation, decay);
                        }
                        buf[CeltConstants.DECODE_BUFFER_SIZE - N + i] =
                              Inlines.SHL32((Inlines.MULT16_16_Q15(attenuation,
                                    exc[extrapolation_offset + j])), CeltConstants.SIG_SHIFT);
                        /* Compute the energy of the previously decoded signal whose
                           excitation we're copying. */
                        tmp = Inlines.ROUND16(
                              buf[CeltConstants.DECODE_BUFFER_SIZE - CeltConstants.MAX_PERIOD - N + extrapolation_offset + j],
                              CeltConstants.SIG_SHIFT);
                        S1 += Inlines.SHR32(Inlines.MULT16_16(tmp, tmp), 8);
                    }

                    {
                        Pointer<int> lpc_mem = Pointer.Malloc<int>(CeltConstants.LPC_ORDER);
                        /* Copy the last decoded samples (prior to the overlap region) to
                           synthesis filter memory so we can have a continuous signal. */
                        for (i = 0; i < CeltConstants.LPC_ORDER; i++)
                            lpc_mem[i] = Inlines.ROUND16(buf[CeltConstants.DECODE_BUFFER_SIZE - N - 1 - i], CeltConstants.SIG_SHIFT);
                        /* Apply the synthesis filter to convert the excitation back into
                           the signal domain. */
                        CeltLPC.celt_iir(buf.Point(CeltConstants.DECODE_BUFFER_SIZE - N), lpc.Point(c * CeltConstants.LPC_ORDER),
                              buf.Point(CeltConstants.DECODE_BUFFER_SIZE - N), extrapolation_len, CeltConstants.LPC_ORDER,
                              lpc_mem);
                    }

                    /* Check if the synthesis energy is higher than expected, which can
                       happen with the signal changes during our window. If so,
                       attenuate. */
                    {
                        int S2 = 0;
                        for (i = 0; i < extrapolation_len; i++)
                        {
                            int tmp = Inlines.ROUND16(buf[CeltConstants.DECODE_BUFFER_SIZE - N + i], CeltConstants.SIG_SHIFT);
                            S2 += Inlines.SHR32(Inlines.MULT16_16(tmp, tmp), 8);
                        }
                        /* This checks for an "explosion" in the synthesis. */
                        /* The float test is written this way to catch NaNs in the output
                           of the IIR filter at the same time. */
                        if (!(S1 > 0.2f * S2))
                        {
                            for (i = 0; i < extrapolation_len; i++)
                                buf[CeltConstants.DECODE_BUFFER_SIZE - N + i] = 0;
                        }
                        else if (S1 < S2)
                        {
                            int ratio = Inlines.celt_sqrt(Inlines.frac_div32(Inlines.SHR32(S1, 1) + 1, S2 + 1));
                            for (i = 0; i < overlap; i++)
                            {
                                int tmp_g = CeltConstants.Q15ONE
                                      - Inlines.MULT16_16_Q15(window[i], CeltConstants.Q15ONE - ratio);
                                buf[CeltConstants.DECODE_BUFFER_SIZE - N + i] =
                                      Inlines.MULT16_32_Q15(tmp_g, buf[CeltConstants.DECODE_BUFFER_SIZE - N + i]);
                            }
                            for (i = overlap; i < extrapolation_len; i++)
                            {
                                buf[CeltConstants.DECODE_BUFFER_SIZE - N + i] =
                                      Inlines.MULT16_32_Q15(ratio, buf[CeltConstants.DECODE_BUFFER_SIZE - N + i]);
                            }
                        }
                    }

                    /* Apply the pre-filter to the MDCT overlap for the next frame because
                       the post-filter will be re-applied in the decoder after the MDCT
                       overlap. */
                    Celt.comb_filter(etmp, buf.Point(CeltConstants.DECODE_BUFFER_SIZE),
                         st.postfilter_period, st.postfilter_period, overlap,
                         -st.postfilter_gain, -st.postfilter_gain,
                         st.postfilter_tapset, st.postfilter_tapset, null, 0);

                    /* Simulate TDAC on the concealed audio so that it blends with the
                       MDCT of the next frame. */
                    for (i = 0; i < overlap / 2; i++)
                    {
                        buf[CeltConstants.DECODE_BUFFER_SIZE + i] =
                           Inlines.MULT16_32_Q15(window[i], etmp[overlap - 1 - i])
                           + Inlines.MULT16_32_Q15(window[overlap - i - 1], etmp[i]);
                    }
                } while (++c < C);
            }

            st.loss_count = loss_count + 1;
        }

        internal static int celt_decode_with_ec(CeltDecoder st, Pointer<byte> data,
              int len, Pointer<short> pcm, int frame_size, EntropyCoder dec, int accum)
        {
            int c, i, N;
            int spread_decision;
            int bits;
            Pointer<int> X;
            Pointer<int> fine_quant;
            Pointer<int> pulses;
            Pointer<int> cap;
            Pointer<int> offsets;
            Pointer<int> fine_priority;
            Pointer<int> tf_res;
            Pointer<byte> collapse_masks;
            Pointer<Pointer<int>> decode_mem = Pointer.Malloc<Pointer<int>>(2);
            Pointer<Pointer<int>> out_syn = Pointer.Malloc<Pointer<int>>(2);
            Pointer<int> lpc;
            Pointer<int> oldBandE, oldLogE, oldLogE2, backgroundLogE;

            int shortBlocks;
            int isTransient;
            int intra_ener;
            int CC = st.channels;
            int LM, M;
            int start;
            int end;
            int effEnd;
            int codedBands;
            int alloc_trim;
            int postfilter_pitch;
            int postfilter_gain;
            int intensity = 0;
            int dual_stereo = 0;
            int total_bits;
            int balance;
            int tell;
            int dynalloc_logp;
            int postfilter_tapset;
            int anti_collapse_rsv;
            int anti_collapse_on = 0;
            int silence;
            int C = st.stream_channels;
            CeltMode mode; // porting note: pointer
            int nbEBands;
            int overlap;
            Pointer<short> eBands;

            mode = st.mode;
            nbEBands = mode.nbEBands;
            overlap = mode.overlap;
            eBands = mode.eBands;
            start = st.start;
            end = st.end;
            frame_size *= st.downsample;

            lpc = st.lpc;
            oldBandE = st.oldEBands;
            oldLogE = st.oldLogE;
            oldLogE2 = st.oldLogE2;
            backgroundLogE = st.backgroundLogE;

            {
                for (LM = 0; LM <= mode.maxLM; LM++)
                    if (mode.shortMdctSize << LM == frame_size)
                        break;
                if (LM > mode.maxLM)
                    return OpusError.OPUS_BAD_ARG;
            }
            M = 1 << LM;

            if (len < 0 || len > 1275 || pcm == null)
                return OpusError.OPUS_BAD_ARG;

            N = M * mode.shortMdctSize;
            c = 0; do
            {
                decode_mem[c] = st.decode_mem.Point(c * (CeltConstants.DECODE_BUFFER_SIZE + overlap));
                out_syn[c] = decode_mem[c].Point(CeltConstants.DECODE_BUFFER_SIZE - N);
            } while (++c < CC);

            effEnd = end;
            if (effEnd > mode.effEBands)
                effEnd = mode.effEBands;

            if (data == null || len <= 1)
            {
                celt_decode_lost(st, N, LM);
                deemphasis(out_syn, pcm, N, CC, st.downsample, mode.preemph.GetPointer(), st.preemph_memD, accum);

                return frame_size / st.downsample;
            }

            if (dec == null)
            {
                // If no entropy decoder was passed into this function, we need to create
                // a new one here for local use only. It only exists in this function scope.
                dec = new EntropyCoder();
                dec.ec_dec_init(data, (uint)len);
            }

            if (C == 1)
            {
                for (i = 0; i < nbEBands; i++)
                    oldBandE[i] = Inlines.MAX16(oldBandE[i], oldBandE[nbEBands + i]);
            }

            total_bits = len * 8;
            tell = dec.ec_tell();

            if (tell >= total_bits)
                silence = 1;
            else if (tell == 1)
                silence = dec.ec_dec_bit_logp(15);
            else
                silence = 0;

            if (silence != 0)
            {
                /* Pretend we've read all the remaining bits */
                tell = len * 8;
                dec.nbits_total += tell - dec.ec_tell();
            }

            postfilter_gain = 0;
            postfilter_pitch = 0;
            postfilter_tapset = 0;
            if (start == 0 && tell + 16 <= total_bits)
            {
                if (dec.ec_dec_bit_logp(1) != 0)
                {
                    int qg, octave;
                    octave = (int)dec.ec_dec_uint(6);
                    postfilter_pitch = (16 << octave) + (int)dec.ec_dec_bits(4 + (uint)octave) - 1;
                    qg = (int)dec.ec_dec_bits(3);
                    if (dec.ec_tell() + 2 <= total_bits)
                        postfilter_tapset = dec.ec_dec_icdf(Tables.tapset_icdf.GetPointer(), 2);
                    postfilter_gain = Inlines.QCONST16(.09375f, 15) * (qg + 1);
                }
                tell = dec.ec_tell();
            }

            if (LM > 0 && tell + 3 <= total_bits)
            {
                isTransient = dec.ec_dec_bit_logp(3);
                tell = dec.ec_tell();
            }
            else
                isTransient = 0;

            if (isTransient != 0)
                shortBlocks = M;
            else
                shortBlocks = 0;

            /* Decode the global flags (first symbols in the stream) */
            intra_ener = tell + 3 <= total_bits ? dec.ec_dec_bit_logp(3) : 0;
            /* Get band energies */
            QuantizeBands.unquant_coarse_energy(mode, start, end, oldBandE,
                  intra_ener, dec, C, LM);

            tf_res = Pointer.Malloc<int>(nbEBands);
            tf_decode(start, end, isTransient, tf_res, LM, dec);

            tell = dec.ec_tell();
            spread_decision = Spread.SPREAD_NORMAL;
            if (tell + 4 <= total_bits)
                spread_decision = dec.ec_dec_icdf(Tables.spread_icdf.GetPointer(), 5);

            cap = Pointer.Malloc<int>(nbEBands);

            Celt.init_caps(mode, cap, LM, C);

            offsets = Pointer.Malloc<int>(nbEBands);

            dynalloc_logp = 6;
            total_bits <<= EntropyCoder.BITRES;
            tell = (int)dec.ec_tell_frac();
            for (i = start; i < end; i++)
            {
                int width, quanta;
                int dynalloc_loop_logp;
                int boost;
                width = C * (eBands[i + 1] - eBands[i]) << LM;
                /* quanta is 6 bits, but no more than 1 bit/sample
                   and no less than 1/8 bit/sample */
                quanta = Inlines.IMIN(width << EntropyCoder.BITRES, Inlines.IMAX(6 << EntropyCoder.BITRES, width));
                dynalloc_loop_logp = dynalloc_logp;
                boost = 0;
                while (tell + (dynalloc_loop_logp << EntropyCoder.BITRES) < total_bits && boost < cap[i])
                {
                    int flag;
                    flag = dec.ec_dec_bit_logp((uint)dynalloc_loop_logp);
                    tell = (int)dec.ec_tell_frac();
                    if (flag == 0)
                        break;
                    boost += quanta;
                    total_bits -= quanta;
                    dynalloc_loop_logp = 1;
                }
                offsets[i] = boost;
                /* Making dynalloc more likely */
                if (boost > 0)
                    dynalloc_logp = Inlines.IMAX(2, dynalloc_logp - 1);
            }

           fine_quant = Pointer.Malloc<int>(nbEBands);
            alloc_trim = tell + (6 << EntropyCoder.BITRES) <= total_bits ?
                  dec.ec_dec_icdf(Tables.trim_icdf.GetPointer(), 7) : 5;

            bits = (((int)len * 8) << EntropyCoder.BITRES) - (int)dec.ec_tell_frac() - 1;
            anti_collapse_rsv = isTransient != 0 && LM >= 2 && bits >= ((LM + 2) << EntropyCoder.BITRES) ? (1 << EntropyCoder.BITRES) : 0;
            bits -= anti_collapse_rsv;

            pulses = Pointer.Malloc<int>(nbEBands);
            fine_priority = Pointer.Malloc<int>(nbEBands);

            BoxedValue<int> boxed_intensity = new BoxedValue<int>(intensity);
            BoxedValue<int> boxed_dual_stereo = new BoxedValue<int>(dual_stereo);
            BoxedValue<int> boxed_balance = new BoxedValue<int>();
            codedBands = Rate.compute_allocation(mode, start, end, offsets, cap,
                  alloc_trim, boxed_intensity, boxed_dual_stereo, bits, boxed_balance, pulses,
                  fine_quant, fine_priority, C, LM, dec, 0, 0, 0);
            intensity = boxed_intensity.Val;
            dual_stereo = boxed_dual_stereo.Val;
            balance = boxed_balance.Val;

            QuantizeBands.unquant_fine_energy(mode, start, end, oldBandE, fine_quant, dec, C);

            c = 0;
            do
            {
                decode_mem[c].Point(N).MemMove(0 - N, CeltConstants.DECODE_BUFFER_SIZE - N + overlap / 2);
            } while (++c < CC);

            /* Decode fixed codebook */
            collapse_masks = Pointer.Malloc<byte>(C * nbEBands);

            X = Pointer.Malloc<int>(C * N);   /**< Interleaved normalised MDCTs */

            BoxedValue<uint> boxed_rng = new BoxedValue<uint>(st.rng);
            Bands.quant_all_bands(0, mode, start, end, X, C == 2 ? X.Point(N) : null, collapse_masks,
                  null, pulses, shortBlocks, spread_decision, dual_stereo, intensity, tf_res,
                  len * (8 << EntropyCoder.BITRES) - anti_collapse_rsv, balance, dec, LM, codedBands, boxed_rng);
            st.rng = boxed_rng.Val;

            if (anti_collapse_rsv > 0)
            {
                anti_collapse_on = (int)dec.ec_dec_bits(1);
            }

            QuantizeBands.unquant_energy_finalise(mode, start, end, oldBandE,
                  fine_quant, fine_priority, len * 8 - dec.ec_tell(), dec, C);

            if (anti_collapse_on != 0)
                Bands.anti_collapse(mode, X, collapse_masks, LM, C, N,
                      start, end, oldBandE, oldLogE, oldLogE2, pulses, st.rng);

            if (silence != 0)
            {
                for (i = 0; i < C * nbEBands; i++)
                    oldBandE[i] = -Inlines.QCONST16(28.0f, CeltConstants.DB_SHIFT);
            }

            celt_synthesis(mode, X, out_syn, oldBandE, start, effEnd,
                           C, CC, isTransient, LM, st.downsample, silence);

            c = 0; do
            {
                st.postfilter_period = Inlines.IMAX(st.postfilter_period, CeltConstants.COMBFILTER_MINPERIOD);
                st.postfilter_period_old = Inlines.IMAX(st.postfilter_period_old, CeltConstants.COMBFILTER_MINPERIOD);
                Celt.comb_filter(out_syn[c], out_syn[c], st.postfilter_period_old, st.postfilter_period, mode.shortMdctSize,
                      st.postfilter_gain_old, st.postfilter_gain, st.postfilter_tapset_old, st.postfilter_tapset,
                      mode.window, overlap);
                if (LM != 0)
                {
                    Celt.comb_filter(out_syn[c].Point(mode.shortMdctSize), out_syn[c].Point(mode.shortMdctSize), st.postfilter_period, postfilter_pitch, N - mode.shortMdctSize,
                          st.postfilter_gain, postfilter_gain, st.postfilter_tapset, postfilter_tapset,
                          mode.window, overlap);
                }

            } while (++c < CC);
            st.postfilter_period_old = st.postfilter_period;
            st.postfilter_gain_old = st.postfilter_gain;
            st.postfilter_tapset_old = st.postfilter_tapset;
            st.postfilter_period = postfilter_pitch;
            st.postfilter_gain = postfilter_gain;
            st.postfilter_tapset = postfilter_tapset;
            if (LM != 0)
            {
                st.postfilter_period_old = st.postfilter_period;
                st.postfilter_gain_old = st.postfilter_gain;
                st.postfilter_tapset_old = st.postfilter_tapset;
            }

            if (C == 1)
            {
                oldBandE.MemCopyTo(oldBandE.Point(nbEBands), nbEBands);
            }

            /* In case start or end were to change */
            if (isTransient == 0)
            {
                int max_background_increase;
                oldLogE.MemCopyTo(oldLogE2, 2 * nbEBands);
                oldBandE.MemCopyTo(oldLogE, 2 * nbEBands);
                /* In normal circumstances, we only allow the noise floor to increase by
                   up to 2.4 dB/second, but when we're in DTX, we allow up to 6 dB
                   increase for each update.*/
                if (st.loss_count < 10)
                    max_background_increase = M * Inlines.QCONST16(0.001f, CeltConstants.DB_SHIFT);
                else
                    max_background_increase = Inlines.QCONST16(1.0f, CeltConstants.DB_SHIFT);
                for (i = 0; i < 2 * nbEBands; i++)
                    backgroundLogE[i] = Inlines.MIN16(backgroundLogE[i] + max_background_increase, oldBandE[i]);
            }
            else {
                for (i = 0; i < 2 * nbEBands; i++)
                    oldLogE[i] = Inlines.MIN16(oldLogE[i], oldBandE[i]);
            }
            c = 0; do
            {
                for (i = 0; i < start; i++)
                {
                    oldBandE[c * nbEBands + i] = 0;
                    oldLogE[c * nbEBands + i] = oldLogE2[c * nbEBands + i] = -Inlines.QCONST16(28.0f, CeltConstants.DB_SHIFT);
                }
                for (i = end; i < nbEBands; i++)
                {
                    oldBandE[c * nbEBands + i] = 0;
                    oldLogE[c * nbEBands + i] = oldLogE2[c * nbEBands + i] = -Inlines.QCONST16(28.0f, CeltConstants.DB_SHIFT);
                }
            } while (++c < 2);
            st.rng = dec.rng;

            deemphasis(out_syn, pcm, N, CC, st.downsample, mode.preemph.GetPointer(), st.preemph_memD, accum);
            st.loss_count = 0;

            if (dec.ec_tell() > 8 * len)
                return OpusError.OPUS_INTERNAL_ERROR;
            if (dec.ec_get_error() != 0)
                st.error = 1;
            return frame_size / st.downsample;
        }

        internal static int opus_custom_decoder_ctl(CeltDecoder st, int request, params object[] vargs)
        {
            switch (request)
            {
                case CeltControl.CELT_SET_START_BAND_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value < 0 || value >= st.mode.nbEBands)
                            return OpusError.OPUS_BAD_ARG;
                        st.start = value;
                    }
                    break;
                case CeltControl.CELT_SET_END_BAND_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value < 1 || value > st.mode.nbEBands)
                            return OpusError.OPUS_BAD_ARG;
                        st.end = value;
                    }
                    break;
                case CeltControl.CELT_SET_CHANNELS_REQUEST:
                    {
                        int value = (int)vargs[0];
                        if (value < 1 || value > 2)
                            return OpusError.OPUS_BAD_ARG;
                        st.stream_channels = value;
                    }
                    break;
                case CeltControl.CELT_GET_AND_CLEAR_ERROR_REQUEST:
                    {
                        BoxedValue<int> value = (BoxedValue<int>)vargs[0];
                        if (value == null)
                            return OpusError.OPUS_BAD_ARG;
                        value.Val = st.error;
                        st.error = 0;
                    }
                    break;
                case OpusControl.OPUS_GET_LOOKAHEAD_REQUEST:
                    {
                        BoxedValue<int> value = (BoxedValue<int>)vargs[0];
                        if (value == null)
                            return OpusError.OPUS_BAD_ARG;
                        value.Val = st.overlap / st.downsample;
                    }
                    break;
                case OpusControl.OPUS_RESET_STATE:
                    {
                        int i;

                        st.PartialReset();

                        // We have to reconstitute the dynamic buffers here. fixme: this could be better implemented
                        st.decode_mem = Pointer.Malloc<int>(st.channels * (CeltConstants.DECODE_BUFFER_SIZE + st.mode.overlap));
                        st.lpc = Pointer.Malloc<int>(st.channels * CeltConstants.LPC_ORDER);
                        st.oldEBands = Pointer.Malloc<int>(2 * st.mode.nbEBands);
                        st.oldLogE = Pointer.Malloc<int>(2 * st.mode.nbEBands);
                        st.oldLogE2 = Pointer.Malloc<int>(2 * st.mode.nbEBands);
                        st.backgroundLogE = Pointer.Malloc<int>(2 * st.mode.nbEBands);

                        for (i = 0; i < 2 * st.mode.nbEBands; i++)
                            st.oldLogE[i] = st.oldLogE2[i] = -Inlines.QCONST16(28.0f, CeltConstants.DB_SHIFT);
                    }
                    break;
                case OpusControl.OPUS_GET_PITCH_REQUEST:
                    {
                        BoxedValue<int> value = (BoxedValue<int>)vargs[0];
                        if (value == null)
                            return OpusError.OPUS_BAD_ARG;
                        value.Val = st.postfilter_period;
                    }
                    break;
                case CeltControl.CELT_GET_MODE_REQUEST:
                    {
                        BoxedValue<CeltMode> value = (BoxedValue<CeltMode>)vargs[0];
                        if (value == null)
                            return OpusError.OPUS_BAD_ARG;
                        value.Val = st.mode;
                    }
                    break;
                case CeltControl.CELT_SET_SIGNALLING_REQUEST:
                    {
                        int value = (int)vargs[0];
                        st.signalling = value;
                    }
                    break;
                case OpusControl.OPUS_GET_FINAL_RANGE_REQUEST:
                    {
                        BoxedValue<uint> value = (BoxedValue<uint>)vargs[0];
                        if (value == null)
                            return OpusError.OPUS_BAD_ARG;
                        value.Val = st.rng;
                    }
                    break;
                default:
                    return OpusError.OPUS_UNIMPLEMENTED;
            }
            return OpusError.OPUS_OK;
        }
    }
}
