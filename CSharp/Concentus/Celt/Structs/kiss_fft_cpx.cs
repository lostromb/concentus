using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    /// <summary>
    /// Complex numbers used in FFT calcs.
    /// TODO this should really really be a struct
    /// </summary>
    public class kiss_fft_cpx
    {
        public int r;
        public int i;

        public void Assign(kiss_fft_cpx other)
        {
            r = other.r;
            i = other.i;
        }

        public override string ToString()
        {
            return "{r=" + r + " i=" + i + " }";
        }

        /// <summary>
        /// Porting method that is needed because some parts of the code will arbitrarily cast int arrays into
        /// 2-element complex value arrays and vice versa. We should get rid of this as soon as possible because it's incredibly slow
        /// </summary>
        /// <param name="data"></param>
        /// <param name="numComplexValues"></param>
        /// <returns></returns>
        public static kiss_fft_cpx[] ConvertInterleavedIntArray(Pointer<int> data, int numComplexValues)
        {
            kiss_fft_cpx[] returnVal = new kiss_fft_cpx[numComplexValues];
            for (int c = 0; c < numComplexValues; c++)
            {
                returnVal[c] = new kiss_fft_cpx()
                {
                    r = data[(2 * c)],
                    i = data[(2 * c) + 1],
                };
            }
            return returnVal;
        }

        /// <summary>
        /// does the reverse of the above function
        /// </summary>
        /// <param name="complex"></param>
        /// <param name="interleaved"></param>
        /// <param name="numComplexValues"></param>
        public static void WriteComplexValuesToInterleavedIntArray(Pointer<kiss_fft_cpx> complex, Pointer<int> interleaved, int numComplexValues)
        {
            for (int c = 0; c < numComplexValues; c++)
            {
                interleaved[(2 * c)] = complex[c].r;
                interleaved[(2 * c) + 1] = complex[c].i;
            }
        }
    }
}
