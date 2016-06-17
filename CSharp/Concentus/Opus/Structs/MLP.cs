using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Structs
{
    /// <summary>
    /// state object for multi-layer perceptron
    /// </summary>
    internal class MLP
    {
        internal int layers;
        internal Pointer<int> topo;
        internal Pointer<float> weights;
    }
}
