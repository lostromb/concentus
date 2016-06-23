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
        public OpusException(string message, int opus_error_code) : base(message + ": " + CodecHelpers.opus_strerror(opus_error_code)) { }
    }
}
