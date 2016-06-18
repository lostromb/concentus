using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConcentusDemo
{
    public class CodecStatistics
    {
        private double _averageBitrate = 0;

        public double Bitrate
        {
            get
            {
                return _averageBitrate;
            }
            set
            {
                _averageBitrate = (_averageBitrate * 0.9) + (value * 0.1);
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
                _avgEncodeSpeed = (_avgEncodeSpeed * 0.9) + (value * 0.1);
            }
        }
    }
}
