using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConcentusDemo
{
    public class CodecStatistics
    {
        private double _avgDecay = 0.003;

        private double _averageBitrate = 0;

        public double Bitrate
        {
            get
            {
                return _averageBitrate;
            }
            set
            {
                _averageBitrate = (_averageBitrate * (1 - _avgDecay)) + (value * _avgDecay);
            }
        }

        private string _mode = "Hybrid";

        public string Mode
        {
            get
            {
                return _mode;
            }
            set
            {
                _mode = value;
            }
        }

        private int _bandwidth = 48000;

        public int Bandwidth
        {
            get
            {
                return _bandwidth;
            }
            set
            {
                _bandwidth = value;
            }
        }

        private double _avgEncodeSpeed = 1;

        public double EncodeSpeed
        {
            get
            {
                return _avgEncodeSpeed;
            }
            set
            {
                _avgEncodeSpeed = (_avgEncodeSpeed * (1 - _avgDecay)) + (value * _avgDecay);
            }
        }

        private double _avgDecodeSpeed = 1;

        public double DecodeSpeed
        {
            get
            {
                return _avgDecodeSpeed;
            }
            set
            {
                _avgDecodeSpeed = (_avgDecodeSpeed * (1 - _avgDecay)) + (value * _avgDecay);
            }
        }
    }
}
