/* Copyright (C) 2001 Erik de Castro Lopo <erikd AT mega-nerd DOT com> */
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

/* Version 1.1 */

using System.Numerics;
using static HellaUnsafe.Celt.Arch;

namespace HellaUnsafe.Silk.Float
{
    internal static class FloatCast
    {
        internal static int float2int(float value)
        {
            // OPT we can potentially vectorize the conversion
            return (int)value;
        }

        internal static short FLOAT2INT16(float x)
        {
            // OPT same here, see function below
            x = x * float_SCALE;
            x = MAX32(x, -32768);
            x = MIN32(x, 32767);
            return (short)float2int(x);
        }

        //private const short INT16_MIN_SHORT = 0 - 0x7FFF; // -32767
        //private const short INT16_MAX_SHORT = 0x7FFF;     //  32767
        //private const int INT24_MIN_INT = 0 - 0x7FFFFF;   // -8388607
        //private const int INT24_MAX_INT = 0x7FFFFF;       //  8388607
        //private const int INT32_MIN_INT = 0 - 0x7FFFFFFF; // -2147483647
        //private const int INT32_MAX_INT = 0x7FFFFFFF;     //  2147483647

        //private const float INT16_MIN_FLOAT = INT16_MIN_SHORT;
        //private const float INT16_MAX_FLOAT = INT16_MAX_SHORT;
        //private const float INT24_MIN_FLOAT = INT24_MIN_INT;
        //private const float INT24_MAX_FLOAT = INT24_MAX_INT;
        //private const float INT32_MIN_FLOAT = INT32_MIN_INT;
        //private const float INT32_MAX_FLOAT = INT32_MAX_INT;

        //private readonly static Vector<int> ClampVecInt16Max;
        //private readonly static Vector<int> ClampVecInt16Min;

        //static FloatCast()
        //{
        //    if (Vector.IsHardwareAccelerated)
        //    {
        //        ClampVecInt16Max = new Vector<int>(INT16_MAX_SHORT);
        //        ClampVecInt16Min = new Vector<int>(INT16_MIN_SHORT);
        //    }
        //}

        ///// <summary>
        ///// Converts audio samples from 32-bit float to 16-bit int.
        ///// </summary>
        ///// <param name="input">The input buffer</param>
        ///// <param name="in_offset">The absolute offset when reading from input buffer</param>
        ///// <param name="output">The output buffer</param>
        ///// <param name="out_offset">The absolute offset when writing to output buffer</param>
        ///// <param name="samples">The number of TOTAL samples to process (not per-channel)</param>
        ///// <param name="clamp">If true, clamp high values to +-32767</param>
        //public static void ConvertSamples_FloatToInt16(float[] input, int in_offset, short[] output, int out_offset, int samples)
        //{
        //    if (Vector.IsHardwareAccelerated)
        //    {
        //        int blockSize = Vector<float>.Count * 2;
        //        int idx = 0;
        //        int stop = samples - (samples % blockSize);
        //        while (idx < stop)
        //        {
        //            // we have to do the processing of two vectors at once because at the end,
        //            // we narrow the clamped int32 vector into int16 and there's not an easy way to
        //            // extract only the first half of the vector
        //            Vector.Narrow(
        //                Vector.Max(
        //                    Vector.Min(
        //                        Vector.ConvertToInt32(
        //                            Vector.Multiply<float>(INT16_MAX_FLOAT, new Vector<float>(input, in_offset + idx))),
        //                        ClampVecInt16Max),
        //                    ClampVecInt16Min),
        //                Vector.Max(
        //                    Vector.Min(
        //                        Vector.ConvertToInt32(
        //                            Vector.Multiply<float>(INT16_MAX_FLOAT, new Vector<float>(input, in_offset + idx + Vector<float>.Count))),
        //                        ClampVecInt16Max),
        //                    ClampVecInt16Min))
        //                .CopyTo(output, idx + out_offset);
        //            idx += blockSize;
        //        }

        //        while (idx < samples)
        //        {
        //            float num = input[idx + in_offset] * INT16_MAX_FLOAT;
        //            if (num > INT16_MAX_FLOAT)
        //            {
        //                num = INT16_MAX_FLOAT;
        //            }
        //            else if (num < INT16_MIN_FLOAT)
        //            {
        //                num = INT16_MIN_FLOAT;
        //            }

        //            output[idx + out_offset] = (short)num;
        //            idx++;
        //        }
        //    }
        //    else
        //    {
        //        for (int c = 0; c < samples; c++)
        //        {
        //            float sample = input[c + in_offset] * INT16_MAX_FLOAT;
        //            if (float.IsNaN(sample) || float.IsInfinity(sample))
        //            {
        //                output[c + out_offset] = INT16_MIN_SHORT; // 0 would make more sense but this is to keep parity with the vectorized behavior
        //            }
        //            else if (sample >= INT16_MAX_FLOAT)
        //            {
        //                output[c + out_offset] = INT16_MAX_SHORT;
        //            }
        //            else if (sample <= INT16_MIN_FLOAT)
        //            {
        //                output[c + out_offset] = INT16_MIN_SHORT;
        //            }
        //            else
        //            {
        //                output[c + out_offset] = (short)sample;
        //            }
        //        }
        //    }
        //}
    }
}
