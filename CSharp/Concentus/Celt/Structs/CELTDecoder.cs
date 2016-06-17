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
    internal class CeltDecoder
    {
        internal CeltMode mode = new CeltMode();
        internal int overlap = 0;
        internal int channels = 0;
        internal int stream_channels = 0;

        internal int downsample = 0;
        internal int start = 0;
        internal int end = 0;
        internal int signalling = 0;

        /* Everything beyond this point gets cleared on a reset */
        internal uint rng = 0;
        internal int error = 0;
        internal int last_pitch_index = 0;
        internal int loss_count = 0;
        internal int postfilter_period = 0;
        internal int postfilter_period_old = 0;
        internal int postfilter_gain = 0;
        internal int postfilter_gain_old = 0;
        internal int postfilter_tapset = 0;
        internal int postfilter_tapset_old = 0;

        internal readonly Pointer<int> preemph_memD = Pointer.Malloc<int>(2);

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
        internal Pointer<int> decode_mem = null;
        internal Pointer<int> lpc = null;
        internal Pointer<int> oldEBands = null;
        internal Pointer<int> oldLogE = null;
        internal Pointer<int> oldLogE2 = null;
        internal Pointer<int> backgroundLogE = null;

        internal void Reset()
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

        internal void PartialReset()
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
