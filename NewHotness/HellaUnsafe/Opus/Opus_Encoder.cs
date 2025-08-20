using HellaUnsafe.Common;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Celt.KissFFT;
using static HellaUnsafe.Celt.MDCT;
using static HellaUnsafe.Celt.CeltH;
using static HellaUnsafe.Celt.CELTModeH;
using static HellaUnsafe.Celt.EntCode;
using static HellaUnsafe.Celt.MathOps;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.MLP;
using static HellaUnsafe.Opus.MLPData;
using static HellaUnsafe.Opus.Opus;
using static HellaUnsafe.Opus.Opus_Encoder;
using static HellaUnsafe.Opus.OpusPrivate;
using static HellaUnsafe.Silk.Control;
using static HellaUnsafe.Silk.DecAPI;
using static HellaUnsafe.Silk.Float.FloatCast;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Opus
{
    internal static unsafe class Opus_Encoder
    {
        internal static unsafe int is_digital_silence(in float* pcm, int frame_size, int channels, int lsb_depth)
        {
           int silence = 0;
           float sample_max = 0;
           sample_max = celt_maxabs16(pcm, frame_size*channels);
           silence = BOOL2INT(sample_max <= (float) 1 / (1 << lsb_depth));
           return silence;
        }
    }
}
