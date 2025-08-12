﻿/***********************************************************************
Copyright (c) 2006-2011, Skype Limited. All rights reserved.
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
- Redistributions of source code must retain the above copyright notice,
this list of conditions and the following disclaimer.
- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.
- Neither the name of Internet Society, IETF or IETF Trust, nor the
names of specific contributors, may be used to endorse or promote
products derived from this software without specific prior written
permission.
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
***********************************************************************/

namespace HellaUnsafe.Silk
{
    internal static class Errors
    {
        /******************/
        /* Error messages */
        /******************/
        internal const int SILK_NO_ERROR =                               0;

        /**************************/
        /* Encoder error messages */
        /**************************/

        /* Input length is not a multiple of 10 ms, or length is longer than the packet length */
        internal const int SILK_ENC_INPUT_INVALID_NO_OF_SAMPLES =        -101;

        /* Sampling frequency not 8000, 12000 or 16000 Hertz */
        internal const int SILK_ENC_FS_NOT_SUPPORTED =                   -102;

        /* Packet size not 10, 20, 40, or 60 ms */
        internal const int SILK_ENC_PACKET_SIZE_NOT_SUPPORTED =          -103;

        /* Allocated payload buffer too short */
        internal const int SILK_ENC_PAYLOAD_BUF_TOO_SHORT =              -104;

        /* Loss rate not between 0 and 100 percent */
        internal const int SILK_ENC_INVALID_LOSS_RATE =                  -105;

        /* Complexity setting not valid, use 0...10 */
        internal const int SILK_ENC_INVALID_COMPLEXITY_SETTING =         -106;

        /* Inband FEC setting not valid, use 0 or 1 */
        internal const int SILK_ENC_INVALID_INBAND_FEC_SETTING =         -107;

        /* DTX setting not valid, use 0 or 1 */
        internal const int SILK_ENC_INVALID_DTX_SETTING =                -108;

        /* CBR setting not valid, use 0 or 1 */
        internal const int SILK_ENC_INVALID_CBR_SETTING =                -109;

        /* Internal encoder error */
        internal const int SILK_ENC_INTERNAL_ERROR =                     -110;

        /* Internal encoder error */
        internal const int SILK_ENC_INVALID_NUMBER_OF_CHANNELS_ERROR =   -111;

        /**************************/
        /* Decoder error messages */
        /**************************/

        /* Output sampling frequency lower than internal decoded sampling frequency */
        internal const int SILK_DEC_INVALID_SAMPLING_FREQUENCY =         -200;

        /* Payload size exceeded the maximum allowed 1024 bytes */
        internal const int SILK_DEC_PAYLOAD_TOO_LARGE =                  -201;

        /* Payload has bit errors */
        internal const int SILK_DEC_PAYLOAD_ERROR =                      -202;

        /* Payload has bit errors */
        internal const int SILK_DEC_INVALID_FRAME_SIZE =                 -203;
    }
}
