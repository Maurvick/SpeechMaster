using System.Windows;
using ExperimentASR.Services;
using Vanara.PInvoke;

namespace ExperimentASR.Windows
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
			// Subscribe to our global status service
			StatusService.Instance.OnStatusChanged += UpdateStatus;
            StatusService.Instance.OnProgressChanged += SetProgress;
		}

		private void UpdateStatus(string message)
		{
			Dispatcher.Invoke(() => StatusText.Text = message);
		}

        private void SetProgress(double value)
        {
            Dispatcher.Invoke(() => MyProgressBar.Value = value);
		}

		private async void RunProcess()
        {
            // This loop simulates a heavy task
            for (int i = 0; i <= 100; i += 10)
            {
                // Update the UI
                MyProgressBar.Value = i;
                StatusText.Text = $"Loading data... {i}%";

                // Wait 500ms to simulate work (do not use Thread.Sleep)
                await Task.Delay(500);
            }

            // Task complete
            StatusText.Text = "Operation Complete!";
            await Task.Delay(1000); // Pause so user sees "Complete"

            // Close the window
            this.Close();
        }
    }
}