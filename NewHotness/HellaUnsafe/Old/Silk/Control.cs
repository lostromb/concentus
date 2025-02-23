using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HellaUnsafe.Silk
{
    internal static class Control
    {
        internal const int FLAG_DECODE_NORMAL = 0;
        internal const int FLAG_PACKET_LOST = 1;
        internal const int FLAG_DECODE_LBRR = 2;

        /***********************************************/
        /* Structure for controlling encoder operation */
        /***********************************************/
        internal unsafe struct silk_EncControlStruct
        {
            /* I:   Number of channels; 1/2                                                         */
            internal int nChannelsAPI;

            /* I:   Number of channels; 1/2                                                         */
            internal int nChannelsInternal;

            /* I:   Input signal sampling rate in Hertz; 8000/12000/16000/24000/32000/44100/48000   */
            internal int API_sampleRate;

            /* I:   Maximum internal sampling rate in Hertz; 8000/12000/16000                       */
            internal int maxInternalSampleRate;

            /* I:   Minimum internal sampling rate in Hertz; 8000/12000/16000                       */
            internal int minInternalSampleRate;

            /* I:   Soft request for internal sampling rate in Hertz; 8000/12000/16000              */
            internal int desiredInternalSampleRate;

            /* I:   Number of samples per packet in milliseconds; 10/20/40/60                       */
            internal int payloadSize_ms;

            /* I:   Bitrate during active speech in bits/second; internally limited                 */
            internal int bitRate;

            /* I:   Uplink packet loss in percent (0-100)                                           */
            internal int packetLossPercentage;

            /* I:   Complexity mode; 0 is lowest, 10 is highest complexity                          */
            internal int complexity;

            /* I:   Flag to enable in-band Forward Error Correction (FEC); 0/1                      */
            internal int useInBandFEC;

            /* I:   Flag to enable in-band Deep REDundancy (DRED); 0/1                              */
            internal int useDRED;

            /* I:   Flag to actually code in-band Forward Error Correction (FEC) in the current packet; 0/1 */
            internal int LBRR_coded;

            /* I:   Flag to enable discontinuous transmission (DTX); 0/1                            */
            internal int useDTX;

            /* I:   Flag to use constant bitrate                                                    */
            internal int useCBR;

            /* I:   Maximum number of bits allowed for the frame                                    */
            internal int maxBits;

            /* I:   Causes a smooth downmix to mono                                                 */
            internal int toMono;

            /* I:   Opus encoder is allowing us to switch bandwidth                                 */
            internal int opusCanSwitch;

            /* I: Make frames as independent as possible (but still use LPC)                        */
            internal int reducedDependency;

            /* O:   Internal sampling rate used, in Hertz; 8000/12000/16000                         */
            internal int internalSampleRate;

            /* O: Flag that bandwidth switching is allowed (because low voice activity)             */
            internal int allowBandwidthSwitch;

            /* O:   Flag that SILK runs in WB mode without variable LP filter (use for switching between WB/SWB/FB) */
            internal int inWBmodeWithoutVariableLP;

            /* O:   Stereo width */
            internal int stereoWidth_Q14;

            /* O:   Tells the Opus encoder we're ready to switch                                    */
            internal int switchReady;

            /* O: SILK Signal type */
            internal int signalType;

            /* O: SILK offset (dithering) */
            internal int offset;
        }

        /**************************************************************************/
        /* Structure for controlling decoder operation and reading decoder status */
        /**************************************************************************/
        internal unsafe struct silk_DecControlStruct
        {
            /* I:   Number of channels; 1/2                                                         */
            internal int nChannelsAPI;

            /* I:   Number of channels; 1/2                                                         */
            internal int nChannelsInternal;

            /* I:   Output signal sampling rate in Hertz; 8000/12000/16000/24000/32000/44100/48000  */
            internal int API_sampleRate;

            /* I:   Internal sampling rate used, in Hertz; 8000/12000/16000                         */
            internal int internalSampleRate;

            /* I:   Number of samples per packet in milliseconds; 10/20/40/60                       */
            internal int payloadSize_ms;

            /* O:   Pitch lag of previous frame (0 if unvoiced), measured in samples at 48 kHz      */
            internal int prevPitchLag;

            /* I:   Enable Deep PLC                                                                 */
            internal int enable_deep_plc;
        }
    }
}
