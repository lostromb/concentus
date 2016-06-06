using Concentus.Common.CPlusPlus;
using Concentus.Common;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Silk
{
    public static class control_audio_bandwidth
    {
        /// <summary>
        /// Control internal sampling rate
        /// </summary>
        /// <param name="psEncC">I/O  Pointer to Silk encoder state</param>
        /// <param name="encControl">I    Control structure</param>
        /// <returns></returns>
        public static int silk_control_audio_bandwidth(silk_encoder_state psEncC, silk_EncControlStruct encControl)
        {
            int fs_kHz;
            int fs_Hz;

            fs_kHz = psEncC.fs_kHz;
            fs_Hz = Inlines.silk_SMULBB(fs_kHz, 1000);

            if (fs_Hz == 0)
            {
                /* Encoder has just been initialized */
                fs_Hz = Inlines.silk_min(psEncC.desiredInternal_fs_Hz, psEncC.API_fs_Hz);
                fs_kHz = Inlines.silk_DIV32_16(fs_Hz, 1000);
            }
            else if (fs_Hz > psEncC.API_fs_Hz || fs_Hz > psEncC.maxInternal_fs_Hz || fs_Hz < psEncC.minInternal_fs_Hz)
            {
                /* Make sure internal rate is not higher than external rate or maximum allowed, or lower than minimum allowed */
                fs_Hz = psEncC.API_fs_Hz;
                fs_Hz = Inlines.silk_min(fs_Hz, psEncC.maxInternal_fs_Hz);
                fs_Hz = Inlines.silk_max(fs_Hz, psEncC.minInternal_fs_Hz);
                fs_kHz = Inlines.silk_DIV32_16(fs_Hz, 1000);
            }
            else
            {
                /* State machine for the internal sampling rate switching */
                if (psEncC.sLP.transition_frame_no >= SilkConstants.TRANSITION_FRAMES)
                {
                    /* Stop transition phase */
                    psEncC.sLP.mode = 0;
                }

                if (psEncC.allow_bandwidth_switch != 0 || encControl.opusCanSwitch != 0)
                {
                    /* Check if we should switch down */
                    if (Inlines.silk_SMULBB(psEncC.fs_kHz, 1000) > psEncC.desiredInternal_fs_Hz)
                    {
                        /* Switch down */
                        if (psEncC.sLP.mode == 0)
                        {
                            /* New transition */
                            psEncC.sLP.transition_frame_no = SilkConstants.TRANSITION_FRAMES;

                            /* Reset transition filter state */
                            psEncC.sLP.In_LP_State.MemSet(0, 2);
                        }

                        if (encControl.opusCanSwitch != 0)
                        {
                            /* Stop transition phase */
                            psEncC.sLP.mode = 0;

                            /* Switch to a lower sample frequency */
                            fs_kHz = psEncC.fs_kHz == 16 ? 12 : 8;
                        }
                        else
                        {
                            if (psEncC.sLP.transition_frame_no <= 0)
                            {
                                encControl.switchReady = 1;
                                /* Make room for redundancy */
                                encControl.maxBits -= encControl.maxBits * 5 / (encControl.payloadSize_ms + 5);
                            }
                            else
                            {
                                /* Direction: down (at double speed) */
                                psEncC.sLP.mode = -2;
                            }
                        }
                    }
                    else
                    {
                        /* Check if we should switch up */
                        if (Inlines.silk_SMULBB(psEncC.fs_kHz, 1000) < psEncC.desiredInternal_fs_Hz)
                        {
                            /* Switch up */
                            if (encControl.opusCanSwitch != 0)
                            {
                                /* Switch to a higher sample frequency */
                                fs_kHz = psEncC.fs_kHz == 8 ? 12 : 16;

                                /* New transition */
                                psEncC.sLP.transition_frame_no = 0;

                                /* Reset transition filter state */
                                psEncC.sLP.In_LP_State.MemSet(0, 2);

                                /* Direction: up */
                                psEncC.sLP.mode = 1;
                            }
                            else
                            {
                                if (psEncC.sLP.mode == 0)
                                {
                                    encControl.switchReady = 1;
                                    /* Make room for redundancy */
                                    encControl.maxBits -= encControl.maxBits * 5 / (encControl.payloadSize_ms + 5);
                                }
                                else
                                {
                                    /* Direction: up */
                                    psEncC.sLP.mode = 1;
                                }
                            }
                        }
                        else
                        {
                            if (psEncC.sLP.mode < 0)
                            {
                                psEncC.sLP.mode = 1;
                            }
                        }
                    }
                }
            }

            return fs_kHz;
        }
    }
}
