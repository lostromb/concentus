using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

                complexityDisplay.Content = string.Format("{0} ({1})", Math.Round(e.NewValue), qualityMeasure);
            }
        }

        private void bitrateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (bitrateDisplay != null && bitrateDisplay.IsInitialized)
            {
                bitrateDisplay.Content = string.Format("{0} KBits/s", Math.Round(e.NewValue));
            }
        }
    }
}
