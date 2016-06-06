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
    public class MLP
    {
        public int layers;
        public Pointer<int> topo;
        public Pointer<float> weights;
    }
}
