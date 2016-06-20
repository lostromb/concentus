using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ConcentusDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private AudioWorker _worker;
        private Thread _backgroundThread;
        private Timer _statisticsTimer;
        private readonly double[] _frameSizes = { 2.5, 5, 20, 40, 60 };

        private void Window_Initialized(object sender, EventArgs e)
        {
            _worker = new AudioWorker();
            _backgroundThread = new Thread(_worker.Run);
            _backgroundThread.IsBackground = true;
            _backgroundThread.Start();
            _statisticsTimer = new Timer(UpdateStatisticsDisplay, null, 0, 200);
        }

        private void complexitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Round the value to an int
            if (Math.Abs(e.NewValue - Math.Round(e.NewValue)) > 0.0001)
            {
                complexitySlider.Value = Math.Round(e.NewValue);
            }
            else if (complexityDisplay != null && complexityDisplay.IsInitialized)
            {
                // Already rounded, display the value
                string qualityMeasure = "Low";
                if (e.NewValue > 2)
                {
                    qualityMeasure = "Medium";
                }
                if (e.NewValue > 5)
                {
                    qualityMeasure = "High";
                }
                if (e.NewValue > 8)
                {
                    qualityMeasure = "Very High";
                }

                int newComplexity = (int)Math.Round(e.NewValue);
                _worker.UpdateComplexity(newComplexity);

                complexityDisplay.Content = string.Format("{0} ({1})", newComplexity, qualityMeasure);
                
            }
        }

        private void bitrateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (bitrateDisplay != null && bitrateDisplay.IsInitialized)
            {
                int newBitrate = (int)Math.Round(e.NewValue);
                _worker.UpdateBitrate(newBitrate);
                bitrateDisplay.Content = string.Format("{0} KBits/s", newBitrate);
            }
        }

        private void framesizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Round the value to an int
            if (Math.Abs(e.NewValue - Math.Round(e.NewValue)) > 0.0001)
            {
                framesizeSlider.Value = Math.Round(e.NewValue);
            }
            else if (framesizeDisplay != null && framesizeDisplay.IsInitialized)
            {
                // Already rounded, display the value
                double frameSize = _frameSizes[(int)Math.Round(e.NewValue)];
                _worker.UpdateFrameSize(frameSize);

                framesizeDisplay.Content = string.Format("{0:F1} ms", frameSize);
            }
        }

        private delegate void StringDelegate(string value);

        private void UpdateStatisticsLabel(string content)
        {
            statisticsLabel.Content = content;
        }

        private void UpdateStatisticsDisplay(object state)
        {
            CodecStatistics stats = _worker.GetStatistics();
            Dispatcher.Invoke(new StringDelegate(UpdateStatisticsLabel), string.Format("Encode {0:F1}x realtime | Decode {1:F1}x realtime | {2:F1}Kbit/s | {3} mode", stats.EncodeSpeed, stats.DecodeSpeed, stats.Bitrate, stats.Mode));
        }
    }
}
