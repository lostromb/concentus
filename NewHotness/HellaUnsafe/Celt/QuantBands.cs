/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2009 Xiph.Org Foundation
   Written by Jean-Marc Valin */
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
using static System.Math;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.Modes;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.EntEnc;
using static HellaUnsafe.Celt.EntDec;
using static HellaUnsafe.Celt.Laplace;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Celt.Rate;
using static HellaUnsafe.Common.CRuntime;

namespace HellaUnsafe.Celt
{
    internal static class QuantBands
    {
        /* Mean energy in each band quantized in Q4 and converted back to float */
        internal static float[] eMeans = {
              6.437500f, 6.250000f, 5.750000f, 5.312500f, 5.062500f,
              4.812500f, 4.500000f, 4.375000f, 4.875000f, 4.687500f,
              4.562500f, 4.437500f, 4.875000f, 4.625000f, 4.312500f,
              4.500000f, 4.375000f, 4.625000f, 4.750000f, 4.437500f,
              3.750000f, 3.750000f, 3.750000f, 3.750000f, 3.750000f
        };

        internal static readonly float[] pred_coef = { 29440f / 32768f, 26112f / 32768f, 21248f / 32768f, 16384f / 32768f };
        internal static readonly float[] beta_coef = { 30147f / 32768f, 22282f / 32768f, 12124f / 32768f, 6554f / 32768f };
        internal static readonly float beta_intra = 4915f / 32768f;

        /*Parameters of the Laplace-like probability models used for the coarse energy.
          There is one pair of parameters for each frame size, prediction type
           (inter/intra), and band number.
          The first number of each pair is the probability of 0, and the second is the
           decay rate, both in Q8 precision.*/
        internal static readonly byte[][][] e_prob_model = {
           /*120 sample frames.*/
           new byte[][] {
              /*Inter*/
              new byte[] {
                  72, 127,  65, 129,  66, 128,  65, 128,  64, 128,  62, 128,  64, 128,
                  64, 128,  92,  78,  92,  79,  92,  78,  90,  79, 116,  41, 115,  40,
                 114,  40, 132,  26, 132,  26, 145,  17, 161,  12, 176,  10, 177,  11
              },
              /*Intra*/
              new byte[] {
                  24, 179,  48, 138,  54, 135,  54, 132,  53, 134,  56, 133,  55, 132,
                  55, 132,  61, 114,  70,  96,  74,  88,  75,  88,  87,  74,  89,  66,
                  91,  67, 100,  59, 108,  50, 120,  40, 122,  37,  97,  43,  78,  50
              }
           },
           /*240 sample frames.*/
           new byte[][] {
              /*Inter*/
              new byte[] {
                  83,  78,  84,  81,  88,  75,  86,  74,  87,  71,  90,  73,  93,  74,
                  93,  74, 109,  40, 114,  36, 117,  34, 117,  34, 143,  17, 145,  18,
                 146,  19, 162,  12, 165,  10, 178,   7, 189,   6, 190,   8, 177,   9
              },
              /*Intra*/
              new byte[] {
                  23, 178,  54, 115,  63, 102,  66,  98,  69,  99,  74,  89,  71,  91,
                  73,  91,  78,  89,  86,  80,  92,  66,  93,  64, 102,  59, 103,  60,
                 104,  60, 117,  52, 123,  44, 138,  35, 133,  31,  97,  38,  77,  45
              }
           },
           /*480 sample frames.*/
           new byte[][] {
            /*Inter*/
            new byte[] {
                  61,  90,  93,  60, 105,  42, 107,  41, 110,  45, 116,  38, 113,  38,
                 112,  38, 124,  26, 132,  27, 136,  19, 140,  20, 155,  14, 159,  16,
                 158,  18, 170,  13, 177,  10, 187,   8, 192,   6, 175,   9, 159,  10
              },
              /*Intra*/
              new byte[] {
                  21, 178,  59, 110,  71,  86,  75,  85,  84,  83,  91,  66,  88,  73,
                  87,  72,  92,  75,  98,  72, 105,  58, 107,  54, 115,  52, 114,  55,
                 112,  56, 129,  51, 132,  40, 150,  33, 140,  29,  98,  35,  77,  42
              }
           },
           /*960 sample frames.*/
           new byte[][] {
              /*Inter*/
              new byte[] {
                  42, 121,  96,  66, 108,  43, 111,  40, 117,  44, 123,  32, 120,  36,
                 119,  33, 127,  33, 134,  34, 139,  21, 147,  23, 152,  20, 158,  25,
                 154,  26, 166,  21, 173,  16, 184,  13, 184,  10, 150,  13, 139,  15
              },
              /*Intra*/
              new byte[] {
                  22, 178,  63, 114,  74,  82,  84,  83,  92,  82, 103,  62,  96,  72,
                  96,  67, 101,  73, 107,  72, 113,  55, 118,  52, 125,  52, 118,  52,
                 117,  55, 135,  49, 137,  39, 157,  32, 145,  29,  97,  33,  77,  40
              }
           }
        };

        internal static readonly byte[] small_energy_icdf = { 2, 1, 0 };

        internal static unsafe float loss_distortion(in float* eBands, float* oldEBands, int start, int end, int len, int C)
        {
            int c, i;
            float dist = 0;
            c = 0; do
            {
                for (i = start; i < end; i++)
                {
                    float d = SUB16(SHR16(eBands[i + c * len], 3), SHR16(oldEBands[i + c * len], 3));
                    dist = MAC16_16(dist, d, d);
                }
            } while (++c < C);
            return MIN32(200, SHR32(dist, 2 * DB_SHIFT - 6));
        }

        internal static unsafe int quant_coarse_energy_impl(in CeltCustomMode m, int start, int end,
              in float* eBands, float* oldEBands,
              int budget, int tell,
              ReadOnlySpan<byte> prob_model, float* error, ref ec_ctx enc, in byte* ecbuf,
              int C, int LM, int intra, float max_decay, int lfe)
        {
            int i, c;
            int badness = 0;
            Span<float> prev = stackalloc float[2];
            prev.Clear();
            float coef;
            float beta;

            if (tell + 3 <= budget)
                ec_enc_bit_logp(ref enc, ecbuf, intra, 3);
            if (intra != 0)
            {
                coef = 0;
                beta = beta_intra;
            }
            else
            {
                beta = beta_coef[LM];
                coef = pred_coef[LM];
            }

            /* Encode at a fixed coarse resolution */
            for (i = start; i < end; i++)
            {
                c = 0;
                do
                {
                    int bits_left;
                    int qi, qi0;
                    float q;
                    float x;
                    float f, tmp;
                    float oldE;
                    float decay_bound;
                    x = eBands[i + c * m.nbEBands];
                    oldE = MAX16(-QCONST16(9.0f, DB_SHIFT), oldEBands[i + c * m.nbEBands]);
                    f = x - coef * oldE - prev[c];
                    /* Rounding to nearest integer here is really important! */
                    qi = (int)Floor(.5f + f);
                    decay_bound = MAX16(-QCONST16(28.0f, DB_SHIFT), oldEBands[i + c * m.nbEBands]) - max_decay;
                    /* Prevent the energy from going down too quickly (e.g. for bands
                       that have just one bin) */
                    if (qi < 0 && x < decay_bound)
                    {
                        qi += (int)SHR16(SUB16(decay_bound, x), DB_SHIFT);
                        if (qi > 0)
                            qi = 0;
                    }
                    qi0 = qi;
                    /* If we don't have enough bits to encode all the energy, just assume
                        something safe. */
                    tell = ec_tell(enc);
                    bits_left = budget - tell - 3 * C * (end - i);
                    if (i != start && bits_left < 30)
                    {
                        if (bits_left < 24)
                            qi = IMIN(1, qi);
                        if (bits_left < 16)
                            qi = IMAX(-1, qi);
                    }
                    if (lfe != 0 && i >= 2)
                        qi = IMIN(qi, 0);
                    if (budget - tell >= 15)
                    {
                        int pi;
                        pi = 2 * IMIN(i, 20);
                        ec_laplace_encode(ref enc, ecbuf, &qi,
                              (uint)(prob_model[pi] << 7), prob_model[pi + 1] << 6);
                    }
                    else if (budget - tell >= 2)
                    {
                        qi = IMAX(-1, IMIN(qi, 1));
                        ec_enc_icdf(ref enc, ecbuf, 2 * qi ^ -((qi < 0) ? 1 : 0), small_energy_icdf, 2);
                    }
                    else if (budget - tell >= 1)
                    {
                        qi = IMIN(0, qi);
                        ec_enc_bit_logp(ref enc, ecbuf, -qi, 1);
                    }
                    else
                        qi = -1;
                    error[i + c * m.nbEBands] = PSHR32(f, 7) - SHL16(qi, DB_SHIFT);
                    badness += Abs(qi0 - qi);
                    q = (float)SHL32(EXTEND32(qi), DB_SHIFT);

                    tmp = PSHR32(MULT16_16(coef, oldE), 8) + prev[c] + SHL32(q, 7);
                    oldEBands[i + c * m.nbEBands] = PSHR32(tmp, 7);
                    prev[c] = prev[c] + SHL32(q, 7) - MULT16_16(beta, PSHR32(q, 8));
                } while (++c < C);
            }
            return lfe != 0 ? 0 : badness;
        }

        internal static unsafe void quant_coarse_energy(in CeltCustomMode m, int start, int end, int effEnd,
              in float* eBands, float* oldEBands, uint budget,
              float* error, ref ec_ctx enc, in byte* ecbuf, int C, int LM, int nbAvailableBytes,
              int force_intra, ref float delayedIntra, int two_pass, int loss_rate, int lfe)
        {
            int intra;
            float max_decay;
            ec_ctx enc_start_state;
            uint tell;
            int badness1 = 0;
            int intra_bias;
            float new_distortion;

            intra = (force_intra != 0 || (two_pass == 0 && delayedIntra > 2 * C * (end - start) && nbAvailableBytes > (end - start) * C)) ? 1 : 0;
            intra_bias = (int)((budget * delayedIntra * loss_rate) / (C * 512));
            new_distortion = loss_distortion(eBands, oldEBands, start, effEnd, m.nbEBands, C);

            tell = (uint)ec_tell(enc);
            if (tell + 3 > budget)
                two_pass = intra = 0;

            max_decay = QCONST16(16.0f, DB_SHIFT);
            if (end - start > 10)
            {
                max_decay = MIN32(max_decay, .125f * nbAvailableBytes);
            }
            if (lfe != 0)
                max_decay = QCONST16(3.0f, DB_SHIFT);
            enc_start_state = enc; // PORTING NOTE: STRUCT COPY BY VALUE

            // Porting note: merged two pinned buffers into one allocation
            float[] scratchBuf = new float[2 * C * m.nbEBands];
            fixed (float* scratchPtr = scratchBuf)
            {
                float* oldEBands_intra = scratchPtr;
                float* error_intra = scratchPtr + (C * m.nbEBands);
                OPUS_COPY(oldEBands_intra, oldEBands, C * m.nbEBands);

                if (two_pass != 0 || intra != 0)
                {
                    badness1 = quant_coarse_energy_impl(m, start, end, eBands, oldEBands_intra, (int)budget,
                          (int)tell, e_prob_model[LM][1], error_intra, ref enc, ecbuf, C, LM, 1, max_decay, lfe);
                }

                if (intra == 0)
                {
                    byte* intra_buf;
                    ec_ctx enc_intra_state;
                    int tell_intra;
                    uint nstart_bytes;
                    uint nintra_bytes;
                    uint save_bytes;
                    int badness2;
                    byte* intra_bits;

                    tell_intra = (int)ec_tell_frac(enc);

                    enc_intra_state = enc; // PORTING NOTE: STRUCT COPY

                    nstart_bytes = ec_range_bytes(enc_start_state);
                    nintra_bytes = ec_range_bytes(enc_intra_state);
                    intra_buf = ecbuf + nstart_bytes;
                    save_bytes = nintra_bytes - nstart_bytes;
                    byte[] save_bytes_buf = new byte[save_bytes];
                    fixed (byte* save_bytes_ptr = save_bytes_buf)
                    {
                        intra_bits = save_bytes_ptr;
                        /* Copy bits from intra bit-stream */
                        OPUS_COPY(intra_bits, intra_buf, nintra_bytes - nstart_bytes);

                        enc = enc_start_state; // STRUCT COPY

                        badness2 = quant_coarse_energy_impl(m, start, end, eBands, oldEBands, (int)budget,
                              (int)tell, e_prob_model[LM][intra], error, ref enc, ecbuf, C, LM, 0, max_decay, lfe);

                        if (two_pass != 0 && (badness1 < badness2 || (badness1 == badness2 && ((int)ec_tell_frac(enc)) + intra_bias > tell_intra)))
                        {
                            enc = enc_intra_state; // STRUCT COPY
                            /* Copy intra bits to bit-stream */
                            OPUS_COPY(intra_buf, intra_bits, nintra_bytes - nstart_bytes);
                            OPUS_COPY(oldEBands, oldEBands_intra, C * m.nbEBands);
                            OPUS_COPY(error, error_intra, C * m.nbEBands);
                            intra = 1;
                        }
                    }
                }
                else
                {
                    OPUS_COPY(oldEBands, oldEBands_intra, C * m.nbEBands);
                    OPUS_COPY(error, error_intra, C * m.nbEBands);
                }

                if (intra != 0)
                    delayedIntra = new_distortion;
                else
                    delayedIntra = ADD32(MULT16_32_Q15(MULT16_16_Q15(pred_coef[LM], pred_coef[LM]), delayedIntra),
                    new_distortion);
            }
        }

        internal static unsafe void quant_fine_energy(
            in CeltCustomMode m, int start, int end, float* oldEBands, float* error,
            int* fine_quant, ref ec_ctx enc, in byte* ecbuf, int C)
        {
            int i, c;

            /* Encode finer resolution */
            for (i = start; i < end; i++)
            {
                short frac = (short)(1 << fine_quant[i]);
                if (fine_quant[i] <= 0)
                    continue;
                c = 0;
                do
                {
                    int q2;
                    float offset;
                    q2 = (int)Floor((error[i + c * m.nbEBands] + .5f) * frac);
                    if (q2 > frac - 1)
                        q2 = frac - 1;
                    if (q2 < 0)
                        q2 = 0;
                    ec_enc_bits(ref enc, ecbuf, (uint)q2, (uint)fine_quant[i]);
                    offset = (q2 + .5f) * (1 << (14 - fine_quant[i])) * (1.0f / 16384) - .5f;
                    oldEBands[i + c * m.nbEBands] += offset;
                    error[i + c * m.nbEBands] -= offset;
                    /*printf ("%f ", error[i] - offset);*/
                } while (++c < C);
            }
        }

        internal static unsafe void quant_energy_finalise(
            in CeltCustomMode m, int start, int end, float* oldEBands, float* error, int* fine_quant,
            int* fine_priority, int bits_left, ref ec_ctx enc, in byte* ecbuf, int C)
        {
            int i, prio, c;

            /* Use up the remaining bits */
            for (prio = 0; prio < 2; prio++)
            {
                for (i = start; i < end && bits_left >= C; i++)
                {
                    if (fine_quant[i] >= MAX_FINE_BITS || fine_priority[i] != prio)
                        continue;
                    c = 0;
                    do
                    {
                        int q2;
                        float offset;
                        q2 = error[i + c * m.nbEBands] < 0 ? 0 : 1;
                        ec_enc_bits(ref enc, ecbuf, (uint)q2, 1);
                        offset = (q2 - .5f) * (1 << (14 - fine_quant[i] - 1)) * (1.0f / 16384);
                        oldEBands[i + c * m.nbEBands] += offset;
                        error[i + c * m.nbEBands] -= offset;
                        bits_left--;
                    } while (++c < C);
                }
            }
        }

        internal static unsafe void unquant_coarse_energy(in CeltCustomMode m, int start, int end, float* oldEBands,
            int intra, ref ec_ctx dec, in byte* ecbuf, int C, int LM)
        {
            ReadOnlySpan<byte> prob_model = e_prob_model[LM][intra];
            int i, c;
            Span<float> prev = stackalloc float[2];
            prev.Clear();
            float coef;
            float beta;
            int budget;
            int tell;

            if (intra != 0)
            {
                coef = 0;
                beta = beta_intra;
            }
            else
            {
                beta = beta_coef[LM];
                coef = pred_coef[LM];
            }

            budget = (int)(dec.storage * 8);

            /* Decode at a fixed coarse resolution */
            for (i = start; i < end; i++)
            {
                c = 0;
                do
                {
                    int qi;
                    float q;
                    float tmp;
                    /* It would be better to express this invariant as a
                       test on C at function entry, but that isn't enough
                       to make the static analyzer happy. */
                    ASSERT(c < 2);
                    tell = ec_tell(dec);
                    if (budget - tell >= 15)
                    {
                        int pi;
                        pi = 2 * IMIN(i, 20);
                        qi = ec_laplace_decode(ref dec, ecbuf, 
                              (uint)(prob_model[pi] << 7), prob_model[pi + 1] << 6);
                    }
                    else if (budget - tell >= 2)
                    {
                        qi = ec_dec_icdf(ref dec, ecbuf, small_energy_icdf, 2);
                        qi = (qi >> 1) ^ -(qi & 1);
                    }
                    else if (budget - tell >= 1)
                    {
                        qi = -ec_dec_bit_logp(ref dec, ecbuf, 1);
                    }
                    else
                        qi = -1;
                    q = (float)SHL32(EXTEND32(qi), DB_SHIFT);

                    oldEBands[i + c * m.nbEBands] = MAX16(-QCONST16(9.0f, DB_SHIFT), oldEBands[i + c * m.nbEBands]);
                    tmp = PSHR32(MULT16_16(coef, oldEBands[i + c * m.nbEBands]), 8) + prev[c] + SHL32(q, 7);
                    oldEBands[i + c * m.nbEBands] = PSHR32(tmp, 7);
                    prev[c] = prev[c] + SHL32(q, 7) - MULT16_16(beta, PSHR32(q, 8));
                } while (++c < C);
            }
        }

        internal static unsafe void unquant_fine_energy(
            in CeltCustomMode m, int start, int end, float* oldEBands,
            int* fine_quant, ref ec_ctx dec, in byte* ecbuf, int C)
        {
            int i, c;
            /* Decode finer resolution */
            for (i = start; i < end; i++)
            {
                if (fine_quant[i] <= 0)
                    continue;
                c = 0;
                do
                {
                    int q2;
                    float offset;
                    q2 = (int)ec_dec_bits(ref dec, ecbuf, (uint)fine_quant[i]);
                    offset = (q2 + .5f) * (1 << (14 - fine_quant[i])) * (1.0f / 16384) - .5f;
                    oldEBands[i + c * m.nbEBands] += offset;
                } while (++c < C);
            }
        }

        internal static unsafe void unquant_energy_finalise(in CeltCustomMode m, int start, int end, float* oldEBands,
            int* fine_quant, int* fine_priority, int bits_left, ref ec_ctx dec, in byte* ecbuf, int C)
        {
            int i, prio, c;

            /* Use up the remaining bits */
            for (prio = 0; prio < 2; prio++)
            {
                for (i = start; i < end && bits_left >= C; i++)
                {
                    if (fine_quant[i] >= MAX_FINE_BITS || fine_priority[i] != prio)
                        continue;
                    c = 0;
                    do
                    {
                        int q2;
                        float offset;
                        q2 = (int)ec_dec_bits(ref dec, ecbuf, 1U);
                        offset = (q2 - .5f) * (1 << (14 - fine_quant[i] - 1)) * (1.0f / 16384);
                        oldEBands[i + c * m.nbEBands] += offset;
                        bits_left--;
                    } while (++c < C);
                }
            }
        }

        internal static unsafe void amp2Log2(in CeltCustomMode m, int effEnd, int end,
              float* bandE, float* bandLogE, int C)
        {
            int c, i;
            c = 0;
            do
            {
                for (i = 0; i < effEnd; i++)
                {
                    bandLogE[i + c * m.nbEBands] =
                          celt_log2(bandE[i + c * m.nbEBands])
                          - SHL16((float)eMeans[i], 6);
                }
                for (i = effEnd; i < end; i++)
                    bandLogE[c * m.nbEBands + i] = -QCONST16(14.0f, DB_SHIFT);
            } while (++c < C);
        }
    }
}
