using Concentus.Common.CPlusPlus;
using Concentus.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Concentus.Silk
{
    internal static class GainQuantization
    {
        private static readonly int OFFSET = ((SilkConstants.MIN_QGAIN_DB * 128) / 6 + 16 * 128);
        private static readonly int SCALE_Q16 = ((65536 * (SilkConstants.N_LEVELS_QGAIN - 1)) / (((SilkConstants.MAX_QGAIN_DB - SilkConstants.MIN_QGAIN_DB) * 128) / 6));
        private static readonly int INV_SCALE_Q16 = ((65536 * (((SilkConstants.MAX_QGAIN_DB - SilkConstants.MIN_QGAIN_DB) * 128) / 6)) / (SilkConstants.N_LEVELS_QGAIN - 1));

        /// <summary>
        /// Gain scalar quantization with hysteresis, uniform on log scale
        /// </summary>
        /// <param name="ind">O    gain indices [MAX_NB_SUBFR]</param>
        /// <param name="gain_Q16">I/O  gains (quantized out) [MAX_NB_SUBFR]</param>
        /// <param name="prev_ind">I/O  last index in previous frame. [Porting note] original implementation passed this as an int8*</param>
        /// <param name="conditional">I    first gain is delta coded if 1</param>
        /// <param name="nb_subfr">I    number of subframes</param>
        internal static void silk_gains_quant(
            Pointer<sbyte> ind,
            Pointer<int> gain_Q16,
            BoxedValue<sbyte> prev_ind,
            int conditional,
            int nb_subfr)
        {
            int k, double_step_size_threshold;

            for (k = 0; k < nb_subfr; k++)
            {
                // Debug.WriteLine("2a 0x{0:x}", (uint)gain_Q16[k]);
                /* Convert to log scale, scale, floor() */
                ind[k] = Inlines.CHOP8(Inlines.silk_SMULWB(SCALE_Q16, Inlines.silk_lin2log(gain_Q16[k]) - OFFSET));

                /* Round towards previous quantized gain (hysteresis) */
                if (ind[k] < prev_ind.Val)
                {
                    ind[k]++;
                }

                ind[k] = Inlines.CHOP8(Inlines.silk_LIMIT_int(ind[k], 0, SilkConstants.N_LEVELS_QGAIN - 1));

                /* Compute delta indices and limit */
                if (k == 0 && conditional == 0)
                {
                    /* Full index */
                    ind[k] = Inlines.CHOP8(Inlines.silk_LIMIT_int(ind[k], prev_ind.Val + SilkConstants.MIN_DELTA_GAIN_QUANT, SilkConstants.N_LEVELS_QGAIN - 1));
                    prev_ind.Val = ind[k];
                }
                else
                {
                    /* Delta index */
                    ind[k] = Inlines.CHOP8(ind[k] - prev_ind.Val);

                    /* Double the quantization step size for large gain increases, so that the max gain level can be reached */
                    double_step_size_threshold = 2 * SilkConstants.MAX_DELTA_GAIN_QUANT - SilkConstants.N_LEVELS_QGAIN + prev_ind.Val;
                    if (ind[k] > double_step_size_threshold)
                    {
                        ind[k] = Inlines.CHOP8(double_step_size_threshold + Inlines.silk_RSHIFT(ind[k] - double_step_size_threshold + 1, 1));
                    }

                    ind[k] = Inlines.CHOP8(Inlines.silk_LIMIT_int(ind[k], SilkConstants.MIN_DELTA_GAIN_QUANT, SilkConstants.MAX_DELTA_GAIN_QUANT));

                    /* Accumulate deltas */
                    if (ind[k] > double_step_size_threshold)
                    {
                        prev_ind.Val += Inlines.CHOP8(Inlines.silk_LSHIFT(ind[k], 1) - double_step_size_threshold);
                    }
                    else
                    {
                        prev_ind.Val += ind[k];
                    }

                    /* Shift to make non-negative */
                    ind[k] -= SilkConstants.MIN_DELTA_GAIN_QUANT;
                    // Debug.WriteLine("2b 0x{0:x}", (uint)ind[k]);
                }

                /* Scale and convert to linear scale */
                gain_Q16[k] = Inlines.silk_log2lin(Inlines.silk_min_32(Inlines.silk_SMULWB(INV_SCALE_Q16, prev_ind.Val) + OFFSET, 3967)); /* 3967 = 31 in Q7 */
            }
        }

        /// <summary>
        /// Gains scalar dequantization, uniform on log scale
        /// </summary>
        /// <param name="gain_Q16">O    quantized gains [MAX_NB_SUBFR]</param>
        /// <param name="ind">I    gain indices [MAX_NB_SUBFR]</param>
        /// <param name="prev_ind">I/O  last index in previous frame [Porting note] original implementation passed this as an int8*</param>
        /// <param name="conditional">I    first gain is delta coded if 1</param>
        /// <param name="nb_subfr">I    number of subframes</param>
        internal static void silk_gains_dequant(
            Pointer<int> gain_Q16,
            Pointer<sbyte> ind,
            BoxedValue<sbyte> prev_ind,
            int conditional,
            int nb_subfr)
        {
            int k, ind_tmp, double_step_size_threshold;

            for (k = 0; k < nb_subfr; k++)
            {
                if (k == 0 && conditional == 0)
                {
                    /* Gain index is not allowed to go down more than 16 steps (~21.8 dB) */
                    prev_ind.Val = Inlines.CHOP8(Inlines.silk_max_int(ind[k], prev_ind.Val - 16));
                }
                else
                {
                    /* Delta index */
                    ind_tmp = ind[k] + SilkConstants.MIN_DELTA_GAIN_QUANT;

                    /* Accumulate deltas */
                    double_step_size_threshold = 2 * SilkConstants.MAX_DELTA_GAIN_QUANT - SilkConstants.N_LEVELS_QGAIN + prev_ind.Val;
                    if (ind_tmp > double_step_size_threshold)
                    {
                        prev_ind.Val += Inlines.CHOP8(Inlines.silk_LSHIFT(ind_tmp, 1) - double_step_size_threshold);
                    }
                    else
                    {
                        prev_ind.Val += Inlines.CHOP8(ind_tmp);
                    }
                }

                prev_ind.Val = Inlines.CHOP8(Inlines.silk_LIMIT_int(prev_ind.Val, 0, SilkConstants.N_LEVELS_QGAIN - 1));

                /* Scale and convert to linear scale */
                gain_Q16[k] = Inlines.silk_log2lin(Inlines.silk_min_32(Inlines.silk_SMULWB(INV_SCALE_Q16, prev_ind.Val) + OFFSET, 3967)); /* 3967 = 31 in Q7 */
            }
        }

        /// <summary>
        /// Compute unique identifier of gain indices vector
        /// </summary>
        /// <param name="ind">I    gain indices [MAX_NB_SUBFR]</param>
        /// <param name="nb_subfr">I    number of subframes</param>
        /// <returns>unique identifier of gains</returns>
        internal static int silk_gains_ID(Pointer<sbyte> ind, int nb_subfr)
        {
            int k;
            int gainsID;

            gainsID = 0;
            for (k = 0; k < nb_subfr; k++)
            {
                gainsID = Inlines.silk_ADD_LSHIFT32(ind[k], gainsID, 8);
            }

            return gainsID;
        }
    }
}
