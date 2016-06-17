using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Noise shaping quantization state
    /// </summary>
    public class silk_nsq_state
    {
        /// <summary>
        /// Buffer for quantized output signal
        /// </summary>
        public readonly Pointer<short> xq = Pointer.Malloc<short>(2 * SilkConstants.MAX_FRAME_LENGTH);
        public readonly Pointer<int> sLTP_shp_Q14 = Pointer.Malloc<int>(2 * SilkConstants.MAX_FRAME_LENGTH);
        public readonly Pointer<int> sLPC_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_SUB_FRAME_LENGTH + SilkConstants.NSQ_LPC_BUF_LENGTH);
        public readonly Pointer<int> sAR2_Q14 = Pointer.Malloc<int>(SilkConstants.MAX_SHAPE_LPC_ORDER);
        public int sLF_AR_shp_Q14 = 0;
        public int lagPrev = 0;
        public int sLTP_buf_idx = 0;
        public int sLTP_shp_buf_idx = 0;
        public int rand_seed = 0;
        public int prev_gain_Q16 = 0;
        public int rewhite_flag = 0;

        public void Reset()
        {
            xq.MemSet(0, 2 * SilkConstants.MAX_FRAME_LENGTH);
            sLTP_shp_Q14.MemSet(0, 2 * SilkConstants.MAX_FRAME_LENGTH);
            sLPC_Q14.MemSet(0, SilkConstants.MAX_SUB_FRAME_LENGTH + SilkConstants.NSQ_LPC_BUF_LENGTH);
            sAR2_Q14.MemSet(0, SilkConstants.MAX_SHAPE_LPC_ORDER);
            sLF_AR_shp_Q14 = 0;
            lagPrev = 0;
            sLTP_buf_idx = 0;
            sLTP_shp_buf_idx = 0;
            rand_seed = 0;
            prev_gain_Q16 = 0;
            rewhite_flag = 0;
        }

        // Copies another nsq state to this one
        public void Assign(silk_nsq_state other)
        {
            this.sLF_AR_shp_Q14 = other.sLF_AR_shp_Q14;
            this.lagPrev = other.lagPrev;
            this.sLTP_buf_idx = other.sLTP_buf_idx;
            this.sLTP_shp_buf_idx = other.sLTP_shp_buf_idx;
            this.rand_seed = other.rand_seed;
            this.prev_gain_Q16 = other.prev_gain_Q16;
            this.rewhite_flag = other.rewhite_flag;
            other.xq.MemCopyTo(this.xq, 2 * SilkConstants.MAX_FRAME_LENGTH);
            other.sLTP_shp_Q14.MemCopyTo(this.sLTP_shp_Q14, 2 * SilkConstants.MAX_FRAME_LENGTH);
            other.sLPC_Q14.MemCopyTo(this.sLPC_Q14, SilkConstants.MAX_SUB_FRAME_LENGTH + SilkConstants.NSQ_LPC_BUF_LENGTH);
            other.sAR2_Q14.MemCopyTo(this.sAR2_Q14, SilkConstants.MAX_SHAPE_LPC_ORDER);
        }
    }
}
