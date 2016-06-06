using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Opus.Enums
{
    public static class OpusError
    {
        /** No error*/
        public const int OPUS_OK = 0;

        /** One or more invalid/out of range arguments*/
        public const int OPUS_BAD_ARG = -1;

        /** Not enough bytes allocated in the buffer*/
        public const int OPUS_BUFFER_TOO_SMALL = -2;

        /** An internal error was detected*/
        public const int OPUS_INTERNAL_ERROR = -3;

        /** The compressed data passed is corrupted*/
        public const int OPUS_INVALID_PACKET = -4;

        /** Invalid/unsupported request number*/
        public const int OPUS_UNIMPLEMENTED = -5;

        /** An encoder or decoder structure is invalid or already freed*/
        public const int OPUS_INVALID_STATE = -6;

        /** Memory allocation has failed*/
        public const int OPUS_ALLOC_FAIL = -7;
    }
}
