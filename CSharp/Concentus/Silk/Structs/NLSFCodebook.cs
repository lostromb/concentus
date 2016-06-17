using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk.Structs
{
    /// <summary>
    /// Structure containing NLSF codebook
    /// </summary>
    internal class NLSFCodebook
    {
        internal short nVectors = 0;

        internal short order = 0;

        /// <summary>
        /// Quantization step size
        /// </summary>
        internal short quantStepSize_Q16 = 0;

        /// <summary>
        /// Inverse quantization step size
        /// </summary>
        internal short invQuantStepSize_Q6 = 0;

        /// <summary>
        /// POINTER
        /// </summary>
        internal Pointer<byte> CB1_NLSF_Q8 = null;

        /// <summary>
        /// POINTER
        /// </summary>
        internal Pointer<byte> CB1_iCDF = null;

        /// <summary>
        /// POINTER to Backward predictor coefs [ order ]
        /// </summary>
        internal Pointer<byte> pred_Q8 = null;

        /// <summary>
        /// POINTER to Indices to entropy coding tables [ order ]
        /// </summary>
        internal Pointer<byte> ec_sel = null;

        /// <summary>
        /// POINTER
        /// </summary>
        internal Pointer<byte> ec_iCDF = null;

        /// <summary>
        /// POINTER
        /// </summary>
        internal Pointer<byte> ec_Rates_Q5 = null;

        /// <summary>
        /// POINTER
        /// </summary>
        internal Pointer<short> deltaMin_Q15 = null;
        
        internal void Reset()
        {
            nVectors = 0;
            order = 0;
            quantStepSize_Q16 = 0;
            invQuantStepSize_Q6 = 0;
            CB1_NLSF_Q8 = null;
            CB1_iCDF = null;
            pred_Q8 = null;
            ec_sel = null;
            ec_iCDF = null;
            ec_Rates_Q5 = null;
            deltaMin_Q15 = null;
        }
    }
}
