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

using System;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Silk.Control;
using static HellaUnsafe.Silk.Define;
using static HellaUnsafe.Silk.Inlines;
using static HellaUnsafe.Silk.Macros;
using static HellaUnsafe.Silk.SigProcFIX;
using static HellaUnsafe.Silk.Structs;
using static HellaUnsafe.Silk.Tables;

namespace HellaUnsafe.Silk
{
    internal static unsafe class ControlAudioBandwidth
    {
        /* Control internal sampling rate */
        internal static unsafe int silk_control_audio_bandwidth(
            silk_encoder_state* psEncC,                        /* I/O  Pointer to Silk encoder state               */
            silk_EncControlStruct* encControl                     /* I    Control structure                           */
        )
        {
            int fs_kHz;
            int orig_kHz;
            int fs_Hz;

            orig_kHz = psEncC->fs_kHz;
            /* Handle a bandwidth-switching reset where we need to be aware what the last sampling rate was. */
            if (orig_kHz == 0)
            {
                orig_kHz = psEncC->sLP.saved_fs_kHz;
            }
            fs_kHz = orig_kHz;
            fs_Hz = silk_SMULBB(fs_kHz, 1000);
            if (fs_Hz == 0)
            {
                /* Encoder has just been initialized */
                fs_Hz = silk_min(psEncC->desiredInternal_fs_Hz, psEncC->API_fs_Hz);
                fs_kHz = silk_DIV32_16(fs_Hz, 1000);
            }
            else if (fs_Hz > psEncC->API_fs_Hz || fs_Hz > psEncC->maxInternal_fs_Hz || fs_Hz < psEncC->minInternal_fs_Hz)
            {
                /* Make sure internal rate is not higher than external rate or maximum allowed, or lower than minimum allowed */
                fs_Hz = psEncC->API_fs_Hz;
                fs_Hz = silk_min(fs_Hz, psEncC->maxInternal_fs_Hz);
                fs_Hz = silk_max(fs_Hz, psEncC->minInternal_fs_Hz);
                fs_kHz = silk_DIV32_16(fs_Hz, 1000);
            }
            else
            {
                /* State machine for the internal sampling rate switching */
                if (psEncC->sLP.transition_frame_no >= TRANSITION_FRAMES)
                {
                    /* Stop transition phase */
                    psEncC->sLP.mode = 0;
                }
                if (psEncC->allow_bandwidth_switch != 0 || encControl->opusCanSwitch != 0)
                {
                    /* Check if we should switch down */
                    if (silk_SMULBB(orig_kHz, 1000) > psEncC->desiredInternal_fs_Hz)
                    {
                        /* Switch down */
                        if (psEncC->sLP.mode == 0)
                        {
                            /* New transition */
                            psEncC->sLP.transition_frame_no = TRANSITION_FRAMES;

                            /* Reset transition filter state */
                            silk_memset(psEncC->sLP.In_LP_State, 0, 2 * sizeof(int));
                        }
                        if (encControl->opusCanSwitch != 0)
                        {
                            /* Stop transition phase */
                            psEncC->sLP.mode = 0;

                            /* Switch to a lower sample frequency */
                            fs_kHz = orig_kHz == 16 ? 12 : 8;
                        }
                        else
                        {
                            if (psEncC->sLP.transition_frame_no <= 0)
                            {
                                encControl->switchReady = 1;
                                /* Make room for redundancy */
                                encControl->maxBits -= encControl->maxBits * 5 / (encControl->payloadSize_ms + 5);
                            }
                            else
                            {
                                /* Direction: down (at double speed) */
                                psEncC->sLP.mode = -2;
                            }
                        }
                    }
                    else
                    /* Check if we should switch up */
                    if (silk_SMULBB(orig_kHz, 1000) < psEncC->desiredInternal_fs_Hz)
                    {
                        /* Switch up */
                        if (encControl->opusCanSwitch != 0)
                        {
                            /* Switch to a higher sample frequency */
                            fs_kHz = orig_kHz == 8 ? 12 : 16;

                            /* New transition */
                            psEncC->sLP.transition_frame_no = 0;

                            /* Reset transition filter state */
                            silk_memset(psEncC->sLP.In_LP_State, 0, 2 * sizeof(int));

                            /* Direction: up */
                            psEncC->sLP.mode = 1;
                        }
                        else
                        {
                            if (psEncC->sLP.mode == 0)
                            {
                                encControl->switchReady = 1;
                                /* Make room for redundancy */
                                encControl->maxBits -= encControl->maxBits * 5 / (encControl->payloadSize_ms + 5);
                            }
                            else
                            {
                                /* Direction: up */
                                psEncC->sLP.mode = 1;
                            }
                        }
                    }
                    else
                    {
                        if (psEncC->sLP.mode < 0)
                            psEncC->sLP.mode = 1;
                    }
                }
            }

            return fs_kHz;
        }
    }
}
