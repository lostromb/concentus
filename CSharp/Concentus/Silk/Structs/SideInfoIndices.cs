using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    internal class SideInfoIndices
    {
        internal readonly Pointer<sbyte> GainsIndices = Pointer.Malloc<sbyte>(SilkConstants.MAX_NB_SUBFR);
        internal readonly Pointer<sbyte> LTPIndex = Pointer.Malloc<sbyte>(SilkConstants.MAX_NB_SUBFR);
        internal readonly Pointer<sbyte> NLSFIndices = Pointer.Malloc<sbyte>(SilkConstants.MAX_LPC_ORDER + 1);
        internal short lagIndex = 0;
        internal sbyte contourIndex = 0;
        internal sbyte signalType = 0;
        internal sbyte quantOffsetType = 0;
        internal sbyte NLSFInterpCoef_Q2 = 0;
        internal sbyte PERIndex = 0;
        internal sbyte LTP_scaleIndex = 0;
        internal sbyte Seed = 0;

        internal void Reset()
        {
            GainsIndices.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            LTPIndex.MemSet(0, SilkConstants.MAX_NB_SUBFR);
            NLSFIndices.MemSet(0, SilkConstants.MAX_LPC_ORDER + 1);
            lagIndex = 0;
            contourIndex = 0;
            signalType = 0;
            quantOffsetType = 0;
            NLSFInterpCoef_Q2 = 0;
            PERIndex = 0;
            LTP_scaleIndex = 0;
            Seed = 0;
        }

        /// <summary>
        /// Overwrites this struct with values from another one. Equivalent to C struct assignment this = other
        /// </summary>
        /// <param name="other"></param>
        internal void Assign(SideInfoIndices other)
        {
            other.GainsIndices.MemCopyTo(this.GainsIndices, SilkConstants.MAX_NB_SUBFR);
            other.LTPIndex.MemCopyTo(this.LTPIndex, SilkConstants.MAX_NB_SUBFR);
            other.NLSFIndices.MemCopyTo(this.NLSFIndices, SilkConstants.MAX_LPC_ORDER + 1);
            this.lagIndex = other.lagIndex;
            this.contourIndex = other.contourIndex;
            this.signalType = other.signalType;
            this.quantOffsetType = other.quantOffsetType;
            this.NLSFInterpCoef_Q2 = other.NLSFInterpCoef_Q2;
            this.PERIndex = other.PERIndex;
            this.LTP_scaleIndex = other.LTP_scaleIndex;
            this.Seed = other.Seed;
        }
    }
}
