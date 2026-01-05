using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ExperimentASR.Views
{
	/// <summary>
	/// Interaction logic for VideoDownload.xaml. 
	/// A window for downloading videos from various sources, for example YouTube.
    /// With options to download only transcription or whole video.
	/// </summary>
	public partial class VideoDownloadWindow : Window
    {
        public VideoDownloadWindow()
        {
            InitializeComponent();
        }
    }
}
