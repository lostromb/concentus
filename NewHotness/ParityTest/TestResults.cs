using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParityTest
{
    public class TestResults
    {
        public bool Passed = false;
        public string Message = string.Empty;
        public double ConcentusTimeMs = 0;
        public double OpusTimeMs = 0;
        public int FrameLength = 0;
        public int FrameCount = 0;
        public short[] FailureFrame = null;
    }
}
