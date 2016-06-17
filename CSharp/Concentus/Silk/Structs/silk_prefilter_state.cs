using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Prefilter state
    /// </summary>
    public class silk_prefilter_state
    {
        public readonly Pointer<short> sLTP_shp = Pointer.Malloc<short>(SilkConstants.LTP_BUF_LENGTH);
        public readonly Pointer<int> sAR_shp = Pointer.Malloc<int>(SilkConstants.MAX_SHAPE_LPC_ORDER + 1);
        public int sLTP_shp_buf_idx = 0;
        public int sLF_AR_shp_Q12 = 0;
        public int sLF_MA_shp_Q12 = 0;
        public int sHarmHP_Q2 = 0;
        public int rand_seed = 0;
        public int lagPrev = 0;

        public silk_prefilter_state()
        {

        }

        public void Reset()
        {
            sLTP_shp.MemSet(0, SilkConstants.LTP_BUF_LENGTH);
            sAR_shp.MemSet(0, SilkConstants.MAX_SHAPE_LPC_ORDER + 1);
            sLTP_shp_buf_idx = 0;
            sLF_AR_shp_Q12 = 0;
            sLF_MA_shp_Q12 = 0;
            sHarmHP_Q2 = 0;
            rand_seed = 0;
            lagPrev = 0;
        }
    }
}
