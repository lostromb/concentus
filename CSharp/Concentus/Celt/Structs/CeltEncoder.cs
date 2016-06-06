using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Celt.Structs
{
    public class CELTEncoder
    {
        public CELTMode mode = null;     /**< Mode used by the encoder [Porting Note] Pointer*/
        public int channels = 0;
        public int stream_channels = 0;

        public int force_intra = 0;
        public int clip = 0;
        public int disable_pf = 0;
        public int complexity = 0;
        public int upsample = 0;
        public int start = 0;
        public int end = 0;

        public int bitrate = 0;
        public int vbr = 0;
        public int signalling = 0;

        /* If zero, VBR can do whatever it likes with the rate */
        public int constrained_vbr = 0;
        public int loss_rate = 0;
        public int lsb_depth = 0;
        public int variable_duration = 0;
        public int lfe = 0;
        public int arch = 0;

        /* Everything beyond this point gets cleared on a reset */

        public uint rng = 0;
        public int spread_decision = 0;
        public int delayedIntra = 0;
        public int tonal_average = 0;
        public int lastCodedBands = 0;
        public int hf_average = 0;
        public int tapset_decision = 0;

        public int prefilter_period = 0;
        public int prefilter_gain = 0;
        public int prefilter_tapset = 0;
        public int consec_transient = 0;
        public AnalysisInfo analysis = new AnalysisInfo(); // fixme is this necessary? Huh?

        public /*readonly*/ Pointer<int> preemph_memE = Pointer.Malloc<int>(2);
        public /*readonly*/ Pointer<int> preemph_memD = Pointer.Malloc<int>(2);

        /* VBR-related parameters */
        public int vbr_reservoir = 0;
        public int vbr_drift = 0;
        public int vbr_offset = 0;
        public int vbr_count = 0;
        public int overlap_max = 0;
        public int stereo_saving = 0;
        public int intensity = 0;
        public Pointer<int> energy_mask = null;
        public int spec_avg = 0;

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
        public Pointer<int> in_mem = null;
        public Pointer<int> prefilter_mem = null;
        public Pointer<int> oldBandE = null;
        public Pointer<int> oldLogE = null;
        public Pointer<int> oldLogE2 = null;

        public void Reset()
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
            arch = 0;
            PartialReset();
        }

        public void PartialReset()
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
