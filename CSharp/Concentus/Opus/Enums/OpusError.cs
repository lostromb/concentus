﻿/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2011 Xiph.Org Foundation
   Originally written by Jean-Marc Valin, Gregory Maxwell, Koen Vos,
   Timothy B. Terriberry, and the Opus open-source contributors
   Ported to C# by Logan Stromberg

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

namespace Concentus.Enums
{
    /// <summary>
    /// Note that since most API-level errors are detected and thrown as
    /// OpusExceptions, direct use of this class is not usually needed
    /// unless you need to interop with existing C-style error handlers.
    /// </summary>
    public static class OpusError
    {
        /// <summary>
        /// No error
        /// </summary>
        public const int OPUS_OK = 0;

        /// <summary>
        /// -1: One or more invalid/out of range arguments
        /// </summary>
        public const int OPUS_BAD_ARG = -1;

        /// <summary>
        /// -2: Not enough bytes allocated in the buffer
        /// </summary>
        public const int OPUS_BUFFER_TOO_SMALL = -2;

        /// <summary>
        /// -3: An public error was detected
        /// </summary>
        public const int OPUS_INTERNAL_ERROR = -3;

        /// <summary>
        /// -4: The compressed data passed is corrupted
        /// </summary>
        public const int OPUS_INVALID_PACKET = -4;

        /// <summary>
        /// -5: Invalid/unsupported request number
        /// </summary>
        public const int OPUS_UNIMPLEMENTED = -5;

        /// <summary>
        /// -6: An encoder or decoder structure is invalid or already freed
        /// </summary>
        public const int OPUS_INVALID_STATE = -6;

        /// <summary>
        /// -7: Memory allocation has failed (This is typically not possible in the C# implementation).
        /// </summary>
        public const int OPUS_ALLOC_FAIL = -7;

        /// <summary>
        /// -8: Used in rare cases where Concentus throws an error that is not covered by the
        /// original Opus spec.
        /// </summary>
        public const int CONCENTUS_UNKNOWN_ERROR = -100;
    }
}
