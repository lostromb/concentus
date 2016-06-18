using System;

namespace ConcentusDemo
{
    public class AudioMath
    {
        /// <summary>
        /// Converts interleaved byte samples (such as what you get from a capture device)
        /// into linear short samples (that are much easier to work with)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static short[] BytesToShorts(byte[] input)
        {
            return BytesToShorts(input, 0, input.Length);
        }

        /// <summary>
        /// Converts interleaved byte samples (such as what you get from a capture device)
        /// into linear short samples (that are much easier to work with)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static short[] BytesToShorts(byte[] input, int offset, int length)
        {
            short[] processedValues = new short[length / 2];
            for (int c = 0; c < processedValues.Length; c++)
            {
                processedValues[c] = (short)(((int)input[(c * 2) + offset]) << 0);
                processedValues[c] += (short)(((int)input[(c * 2) + 1 + offset]) << 8);
            }

            return processedValues;
        }

        /// <summary>
        /// Converts linear short samples into interleaved byte samples, for writing to a file, waveout device, etc.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static byte[] ShortsToBytes(short[] input)
        {
            return ShortsToBytes(input, 0, input.Length);
        }

        /// <summary>
        /// Converts linear short samples into interleaved byte samples, for writing to a file, waveout device, etc.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static byte[] ShortsToBytes(short[] input, int offset, int length)
        {
            byte[] processedValues = new byte[length * 2];
            for (int c = 0; c < length; c++)
            {
                processedValues[c * 2] = (byte)(input[c + offset] & 0xFF);
                processedValues[c * 2 + 1] = (byte)((input[c + offset] >> 8) & 0xFF);
            }

            return processedValues;
        }

        /// <summary>
        /// Returns the power-of-two value that is closest to the given value.
        /// ex: "100" returns "128", "4100" returns "4096", etc.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        internal static int NPOT(int val)
        {
            double logBase = Math.Log((double)val, 2);
            double upperBound = Math.Ceiling(logBase);
            int nearestPowerOfTwo = (int)Math.Pow(2, upperBound);
            return nearestPowerOfTwo;
        }

        // Curve calculation

        // Hermitian (smoothstep) curves
        private static readonly double[] _smoothStepTable = new double[5000];
        // Window function curves
        private static readonly double[] _nuttallWindow = new double[5000];
        private static readonly double[] _blackmanWindow = new double[5000];

        /// <summary>
        /// Precalculates a bunch of curves and tables upon class initialization
        /// </summary>
        static AudioMath()
        {
            // Precache the hermitian curve table
            for (int c = 0; c < _smoothStepTable.Length; c++)
            {
                double x = (double)c / _smoothStepTable.Length;
                _smoothStepTable[c] = (3 * x * x) - (2 * x * x * x);
            }
            // Blackman window table
            for (int c = 0; c < _blackmanWindow.Length; c++)
            {
                double x = ((double)c / _blackmanWindow.Length) - 0.5;
                double a0 = 7938d / 18606d;
                double a1 = 9240d / 18608d;
                double a2 = 1430d / 18606d;
                _blackmanWindow[c] = 1 - (a0 - (a1 * Math.Cos(2 * Math.PI * x)) + (a2 * Math.Cos(4 * Math.PI * x)));
            }
            // Nuttall window table
            for (int c = 0; c < _nuttallWindow.Length; c++)
            {
                double x = ((double)c / _nuttallWindow.Length) - 0.5;
                double a0 = 0.355768;
                double a1 = 0.487396;
                double a2 = 0.144232;
                double a3 = 0.012604;
                _nuttallWindow[c] = 1 - (a0 - (a1 * Math.Cos(2 * Math.PI * x)) + (a2 * Math.Cos(4 * Math.PI * x)) - (a3 * Math.Cos(6 * Math.PI * x)));
            }
        }

        internal static float GaussianWindow(float x)
        {
            float x1 = (x - 0.5f) * 2f;
            return (float)Math.Exp(0 - (x1 * x1) / 0.15);
        }

        internal static double BlackmanWindow(double x)
        {
            int idx = (int)(x * _blackmanWindow.Length);
            if (idx < 0 || idx >= _blackmanWindow.Length)
                return 0;
            return _blackmanWindow[idx];
        }

        internal static double NuttallWindow(double x)
        {
            int idx = (int)(x * _nuttallWindow.Length);
            if (idx < 0 || idx >= _nuttallWindow.Length)
                return 0;
            return _nuttallWindow[idx];
        }

        /// <summary>
        /// Models the basic smoothstep curve 3x^2 - 2x^3.
        /// This is a smoothed curve between (0, 0) and (1, 1).
        /// Any x < 0 or x > 1 will be clamped to the min/max value.
        /// </summary>
        /// <param name="x">The portion of the curve to return</param>
        /// <returns>The smoothed curve at that x-value</returns>
        internal static double SmoothStep(double x)
        {
            int idx = (int)(x * _smoothStepTable.Length);
            if (idx < 0)
                return 0;
            if (idx >= _smoothStepTable.Length)
                return 1;
            return _smoothStepTable[idx];
        }

        /// <summary>
        /// Normalizes a curve so that the highest peak is equal to 1.0
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        internal static double[] NormalizeCurveByPeak(double[] curve)
        {
            double[] returnVal = new double[curve.Length];
            double peak = 0.0001;
            foreach (double x in curve)
            {
                if (x > peak)
                    peak = x;
            }
            for (int c = 0; c < curve.Length; c++)
            {
                returnVal[c] = curve[c] / peak;
            }
            return returnVal;
        }

        /// <summary>
        /// Normalizes a curve so that its total mass is equal to 1.0
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        internal static double[] NormalizeCurveByMass(double[] curve)
        {
            double[] returnVal = new double[curve.Length];
            double mass = 0.00001;
            foreach (double x in curve)
            {
                mass += Math.Abs(x);
            }
            for (int c = 0; c < curve.Length; c++)
            {
                returnVal[c] = curve[c] / mass;
            }
            return returnVal;
        }

        internal static double HermitianLowpassWindow(double band, double cutoffFreq, double width)
        {
            double start = cutoffFreq - width;
            double end = cutoffFreq + width;
            if (band < start)
                return 1.0;
            if (band > end)
                return 0.0;
            double x = (band - start) / (width * 2);
            return SmoothStep(1 - x);
        }

        internal static double HermitianHighpassWindow(double band, double cutoffFreq, double width)
        {
            double start = cutoffFreq - width;
            double end = cutoffFreq + width;
            if (band < start)
                return 0.0;
            if (band > end)
                return 1.0;
            double x = (band - start) / (width * 2);
            return SmoothStep(x);
        }
    }
}
