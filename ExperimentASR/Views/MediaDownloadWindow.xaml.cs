using System.Windows;

namespace ExperimentASR.Views
{
	/// <summary>
	/// Interaction logic for VideoDownload.xaml. 
	/// A window for downloading videos from various sources, for example YouTube.
    /// With options to download only transcription or whole video.
	/// </summary>
	public partial class MediaDownloadWindow : Window
    {
		// TODO: Currently unused. Implement download functionality.
		// TODO: Support multiple video download.
		private readonly List<string> allowedLinks = new()
			{
			"youtube.com",
		};

		public MediaDownloadWindow()
        {
            InitializeComponent();
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
			if (string.IsNullOrWhiteSpace(TxtUrl.Text))
			{
				MessageBox.Show("Please enter a valid URL.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			// To be implemented: Start the download process based on the URL and options selected
			if (!allowedLinks.Any(link => TxtUrl.Text.Contains(link)))
			{
				MessageBox.Show("The provided URL is not supported.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
			// Close the window without doing anything
		}

		private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
			// To be implemented: Open a folder browser dialog to select download location
		}
	}
}
