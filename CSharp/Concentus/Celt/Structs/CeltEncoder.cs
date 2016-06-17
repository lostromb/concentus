using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    internal class CeltEncoder
    {
        internal CeltMode mode = null;     /**< Mode used by the encoder [Porting Note] Pointer*/
        internal int channels = 0;
        internal int stream_channels = 0;

        internal int force_intra = 0;
        internal int clip = 0;
        internal int disable_pf = 0;
        internal int complexity = 0;
        internal int upsample = 0;
        internal int start = 0;
        internal int end = 0;

        internal int bitrate = 0;
        internal int vbr = 0;
        internal int signalling = 0;

        /* If zero, VBR can do whatever it likes with the rate */
        internal int constrained_vbr = 0;
        internal int loss_rate = 0;
        internal int lsb_depth = 0;
        internal OpusFramesize variable_duration = 0;
        internal int lfe = 0;

        /* Everything beyond this point gets cleared on a reset */

        internal uint rng = 0;
        internal int spread_decision = 0;
        internal int delayedIntra = 0;
        internal int tonal_average = 0;
        internal int lastCodedBands = 0;
        internal int hf_average = 0;
        internal int tapset_decision = 0;

        internal int prefilter_period = 0;
        internal int prefilter_gain = 0;
        internal int prefilter_tapset = 0;
        internal int consec_transient = 0;
        internal AnalysisInfo analysis = new AnalysisInfo(); // fixme is this necessary? Huh?

        internal readonly Pointer<int> preemph_memE = Pointer.Malloc<int>(2);
        internal readonly Pointer<int> preemph_memD = Pointer.Malloc<int>(2);

        /* VBR-related parameters */
        internal int vbr_reservoir = 0;
        internal int vbr_drift = 0;
        internal int vbr_offset = 0;
        internal int vbr_count = 0;
        internal int overlap_max = 0;
        internal int stereo_saving = 0;
        internal int intensity = 0;
        internal Pointer<int> energy_mask = null;
        internal int spec_avg = 0;

        /// <summary>
        /// The original C++ defined in_mem as a single float[1] which was the "caboose"
        /// to the overall encoder struct, containing 5 separate variable-sized buffer
        /// spaces of heterogeneous datatypes. I have laid them out into separate variables here,
        /// but these were the original definitions:
        /// val32 in_mem[],        Size = channels*mode.overlap
        /// val32 prefilter_mem[], Size = channels*COMBFILTER_MAXPERIOD
        /// val16 oldBandE[],      Size = channels*mode.nbEBands
        /// val16 oldLogE[],       Size = channels*mode.nbEBands
        /// val16 oldLogE2[],      Size = channels*mode.nbEBands
        /// </summary>
        internal Pointer<int> in_mem = null;
        internal Pointer<int> prefilter_mem = null;
        internal Pointer<int> oldBandE = null;
        internal Pointer<int> oldLogE = null;
        internal Pointer<int> oldLogE2 = null;

        internal void Reset()
        {
            mode = null;
            channels = 0;
            stream_channels = 0;
            force_intra = 0;
            clip = 0;
            disable_pf = 0;
            complexity = 0;
            upsample = 0;
            start = 0;
            end = 0;
            bitrate = 0;
            vbr = 0;
            signalling = 0;
            constrained_vbr = 0;
            loss_rate = 0;
            lsb_depth = 0;
            variable_duration = 0;
            lfe = 0;
            PartialReset();
        }

        internal void PartialReset()
        {
            rng = 0;
            spread_decision = 0;
            delayedIntra = 0;
            tonal_average = 0;
            lastCodedBands = 0;
            hf_average = 0;
            tapset_decision = 0;
            prefilter_period = 0;
            prefilter_gain = 0;
            prefilter_tapset = 0;
            consec_transient = 0;
            analysis.Reset();
            preemph_memE.MemSet(0, 2);
            preemph_memD.MemSet(0, 2);
            vbr_reservoir = 0;
            vbr_drift = 0;
            vbr_offset = 0;
            vbr_count = 0;
            overlap_max = 0;
            stereo_saving = 0;
            intensity = 0;
            energy_mask = null;
            spec_avg = 0;
            in_mem = null;
            prefilter_mem = null;
            oldBandE = null;
            oldLogE = null;
            oldLogE2 = null;
        }
    }
}
