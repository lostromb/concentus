using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus
{
    public class OpusException : Exception
    {
        public OpusException() : base() { }
        public OpusException(string message) : base(message) { }
    }
}
