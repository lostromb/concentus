using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConcentusDemo
{
    /// <summary>
    /// An implementation of the Lanczos 3 algorithm for interpolating discrete waveforms
    /// </summary>
    public class Lanczos
    {
        private static readonly double PISQ = Math.PI * Math.PI;
        private static double[] _cache = new double[600];

        static Lanczos()
        {
            for (int c = 0; c < _cache.Length; c++)
            {
                _cache[c] = double.NaN;
            }
        }

        public static short[] Resample(short[] input, int inputSampleRate, int outputSampleRate)
        {
            // check input
            if (inputSampleRate == outputSampleRate)
                return input;
            if (outputSampleRate == 0 || inputSampleRate == 0) // Prevent divide by zero
                return input;

            int degree = 3;
            int requiredSize = (int)((double)input.Length * (double)outputSampleRate / (double)inputSampleRate);
            short[] returnVal = new short[requiredSize];

            for (int c = 0; c < requiredSize; c++)
            {
                double x = ((double)c * (double)(input.Length - 1) / (double)requiredSize);
                double sigma = 0;
                for (int i = (int)Math.Floor(x - degree + 1); i < (int)Math.Floor(x + degree); i++)
                {
                    // clamp i so we don't overstep the input array
                    int clampedI = i;
                    if (i < 0)
                        clampedI = 0;
                    if (i >= input.Length)
                        clampedI = input.Length - 1;

                    sigma += input[clampedI] * Kernel(x - i, degree);
                }
                returnVal[c] = (short)sigma;
            }

            return returnVal;
        }

        private static double Kernel(double x, int a)
        {
            if (x == 0)
                return 1;
            if (x >= a || x <= (0 - a))
                return 0;
            int cacheIndex = (int)(x * 100) + 300;
            if (double.IsNaN(_cache[cacheIndex]))
                _cache[cacheIndex] = a * Math.Sin(Math.PI * x) * Math.Sin(Math.PI * x / a) / (PISQ * x * x);
            return _cache[cacheIndex];
        }
    }
}
