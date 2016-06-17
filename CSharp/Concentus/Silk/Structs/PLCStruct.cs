using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Struct for Packet Loss Concealment
    /// </summary>
    internal class PLCStruct
    {
        internal int pitchL_Q8 = 0;                          /* Pitch lag to use for voiced concealment                          */
        internal readonly Pointer<short> LTPCoef_Q14 = Pointer.Malloc<short>(SilkConstants.LTP_ORDER);           /* LTP coeficients to use for voiced concealment                    */
        internal readonly Pointer<short> prevLPC_Q12 = Pointer.Malloc<short>(SilkConstants.MAX_LPC_ORDER);
        internal int last_frame_lost = 0;                    /* Was previous frame lost                                          */
        internal int rand_seed = 0;                          /* Seed for unvoiced signal generation                              */
        internal short randScale_Q14 = 0;                      /* Scaling of unvoiced random signal                                */
        internal int conc_energy = 0;
        internal int conc_energy_shift = 0;
        internal short prevLTP_scale_Q14 = 0;
        internal readonly Pointer<int> prevGain_Q16 = Pointer.Malloc<int>(2);
        internal int fs_kHz = 0;
        internal int nb_subfr = 0;
        internal int subfr_length = 0;

        internal void Reset()
        {
            pitchL_Q8 = 0;
            LTPCoef_Q14.MemSet(0, SilkConstants.LTP_ORDER);
            prevLPC_Q12.MemSet(0, SilkConstants.MAX_LPC_ORDER);
            last_frame_lost = 0;
            rand_seed = 0;
            randScale_Q14 = 0;
            conc_energy = 0;
            conc_energy_shift = 0;
            prevLTP_scale_Q14 = 0;
            prevGain_Q16.MemSet(0, 2);
            fs_kHz = 0;
            nb_subfr = 0;
            subfr_length = 0;
        }
    }
}
