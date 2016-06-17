using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    /// <summary>
    /// Decoder state
    /// </summary>
    public class CELTDecoder
    {
        public CELTMode mode = new CELTMode();
        public int overlap = 0;
        public int channels = 0;
        public int stream_channels = 0;

        public int downsample = 0;
        public int start = 0;
        public int end = 0;
        public int signalling = 0;

        /* Everything beyond this point gets cleared on a reset */
        public uint rng = 0;
        public int error = 0;
        public int last_pitch_index = 0;
        public int loss_count = 0;
        public int postfilter_period = 0;
        public int postfilter_period_old = 0;
        public int postfilter_gain = 0;
        public int postfilter_gain_old = 0;
        public int postfilter_tapset = 0;
        public int postfilter_tapset_old = 0;

        public readonly Pointer<int> preemph_memD = Pointer.Malloc<int>(2);

        /// <summary>
        /// Scratch space used by the decoder. It is actually a variable-sized
        /// field that resulted in a variable-sized struct. There are 6 distinct regions inside.
        /// I have laid them out into separate variables here,
        /// but these were the original definitions:
        /// val32 decode_mem[],     Size = channels*(DECODE_BUFFER_SIZE+mode.overlap)
        /// val16 lpc[],            Size = channels*LPC_ORDER
        /// val16 oldEBands[],      Size = 2*mode.nbEBands
        /// val16 oldLogE[],        Size = 2*mode.nbEBands
        /// val16 oldLogE2[],       Size = 2*mode.nbEBands
        /// val16 backgroundLogE[], Size = 2*mode.nbEBands
        /// </summary>
        public Pointer<int> decode_mem = null;
        public Pointer<int> lpc = null;
        public Pointer<int> oldEBands = null;
        public Pointer<int> oldLogE = null;
        public Pointer<int> oldLogE2 = null;
        public Pointer<int> backgroundLogE = null;

        public void Reset()
        {
            mode.Reset();
            overlap = 0;
            channels = 0;
            stream_channels = 0;
            downsample = 0;
            start = 0;
            end = 0;
            signalling = 0;
            PartialReset();
        }

        public void PartialReset()
        {
            rng = 0;
            error = 0;
            last_pitch_index = 0;
            loss_count = 0;
            postfilter_period = 0;
            postfilter_period_old = 0;
            postfilter_gain = 0;
            postfilter_gain_old = 0;
            postfilter_tapset = 0;
            postfilter_tapset_old = 0;
            preemph_memD.MemSet(0, 2);
            decode_mem = null;
            lpc = null;
            oldEBands = null;
            oldLogE = null;
            oldLogE2 = null;
            backgroundLogE = null;
        }
    }
}
