using HellaUnsafe.Common;
using static HellaUnsafe.Celt.StaticModes;
using static HellaUnsafe.Celt.KissFFT;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Celt.MDCT;

namespace HellaUnsafe.Celt
{
    internal static unsafe class CeltMode
    {
        internal const int MAX_PERIOD = 1024;

        internal static readonly short* eband5ms = NativeArray.AllocateGlobal(new short[] {
            /*0  200 400 600 800  1k 1.2 1.4 1.6  2k 2.4 2.8 3.2  4k 4.8 5.6 6.8  8k 9.6 12k 15.6 */
              0,  1,  2,  3,  4,  5,  6,  7,  8, 10, 12, 14, 16, 20, 24, 28, 34, 40, 48, 60, 78, 100
            });

        /* Bit allocation table in units of 1/32 bit/sample (0.1875 dB SNR) */
        internal static readonly byte* band_allocation = NativeArray.AllocateGlobal(new byte[] {
            /*0  200 400 600 800  1k 1.2 1.4 1.6  2k 2.4 2.8 3.2  4k 4.8 5.6 6.8  8k 9.6 12k 15.6 */
              0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
             90, 80, 75, 69, 63, 56, 49, 40, 34, 29, 20, 18, 10,  0,  0,  0,  0,  0,  0,  0,  0,
            110,100, 90, 84, 78, 71, 65, 58, 51, 45, 39, 32, 26, 20, 12,  0,  0,  0,  0,  0,  0,
            118,110,103, 93, 86, 80, 75, 70, 65, 59, 53, 47, 40, 31, 23, 15,  4,  0,  0,  0,  0,
            126,119,112,104, 95, 89, 83, 78, 72, 66, 60, 54, 47, 39, 32, 25, 17, 12,  1,  0,  0,
            134,127,120,114,103, 97, 91, 85, 78, 72, 66, 60, 54, 47, 41, 35, 29, 23, 16, 10,  1,
            144,137,130,124,113,107,101, 95, 88, 82, 76, 70, 64, 57, 51, 45, 39, 33, 26, 15,  1,
            152,145,138,132,123,117,111,105, 98, 92, 86, 80, 74, 67, 61, 55, 49, 43, 36, 20,  1,
            162,155,148,142,133,127,121,115,108,102, 96, 90, 84, 77, 71, 65, 59, 53, 46, 30,  1,
            172,165,158,152,143,137,131,125,118,112,106,100, 94, 87, 81, 75, 69, 63, 56, 45, 20,
            200,200,200,200,200,200,200,200,198,193,188,183,178,173,168,163,158,153,148,129,104,
            });

        internal unsafe struct PulseCache
        {
            internal int size;
            internal short* index;
            internal byte* bits;
            internal byte* caps;
        }

        internal unsafe struct CeltCustomMode
        {
            internal int Fs;
            internal int overlap;

            internal int nbEBands;
            internal int effEBands;
            internal fixed float preemph[4];
            internal short* eBands;   /**< Definition for each "pseudo-critical band" */

            internal int maxLM;
            internal int nbShortMdcts;
            internal int shortMdctSize;

            internal int nbAllocVectors; /**< Number of lines in the matrix below */
            internal byte* allocVectors;   /**< Number of bits in each band for several rates */
            internal short* logN;

            internal float* window;
            internal mdct_lookup mdct;
            internal PulseCache cache;
        };

        internal static CeltCustomMode* opus_custom_mode_create(int Fs, int frame_size, out int error)
        {
            int i;
            for (i = 0; i < TOTAL_MODES; i++)
            {
                int j;
                for (j = 0; j < 4; j++)
                {
                    if (Fs == static_mode_list[i]->Fs &&
                          frame_size << j == static_mode_list[i]->shortMdctSize * static_mode_list[i]->nbShortMdcts)
                    {
                        error = OPUS_OK;
                        return static_mode_list[i];
                    }
                }
            }

            error = OPUS_BAD_ARG;
            return null;
        }
    }
}
