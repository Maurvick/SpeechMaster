using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExperimentASR.Views;
using ExperimentASR.Windows;
using Microsoft.Win32;
using SpeechMaster.Models.Transcription;
using SpeechMaster.Services;

namespace SpeechMaster
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly TranscribeService _transcribeSerivce = new();
        private readonly SettingsManager _settingsManager = new();
        private readonly TranscriptionQueueManager _queueManager = new();
        private readonly EngineManager _setupService = new();

		public ObservableCollection<TranscriptionHistoryItem> ProcessedItems { get; set; }

		public MainWindow()
        {
            InitializeComponent();
            // Bind the DataGrid to queue manager list
            QueueGrid.ItemsSource = _queueManager.Jobs;
			ProcessedItems = new ObservableCollection<TranscriptionHistoryItem>();
			// HistoryGrid.ItemsSource = ProcessedItems;
		}

		private void UpdateStatusText(string message)
		{
			// Marshals to UI thread
			Dispatcher.Invoke(() =>
			{
				StatusText.Text = message;
			});
		}

		// Unsubscribe from events on window close
		protected override void OnClosed(EventArgs e)
		{
			StatusService.Instance.OnStatusChanged -= UpdateStatusText;
			base.OnClosed(e);
		}

        // Check python ASR cli location
		private void GetAsrEngineLocation()
        {
            if (File.Exists(_transcribeSerivce.AsrEngineLocation))
            {
                // textAsrLocation.Text = "ASR Engine Location found: " + _transcribeSerivce.AsrEngineLocation;
            }
            else
            {
                // textAsrLocation.Text = "ASR Engine Location not found.";
                // textAsrLocation.Foreground = System.Windows.Media.Brushes.Red;
            }
            if (System.Numerics.Vector.IsHardwareAccelerated)
            {
                txtGPUAcceleration.Text = "GPU Acceleration: Yes";
            }
            else
            {
                txtGPUAcceleration.Text = "GPU Acceleration: No";
            }
        }

		// --- LIST UPDATE LOGIC ---

		private void AddToHistory(string filePath, string text)
		{
			var newItem = new TranscriptionHistoryItem
			{
				FileName = System.IO.Path.GetFileName(filePath),
				FullPath = filePath,
				TranscriptText = text,
				ProcessedDate = DateTime.Now
			};

			Dispatcher.Invoke(() =>
			{
				ProcessedItems.Insert(0, newItem); // Додаємо на початок списку
				// HistoryGrid.SelectedItem = newItem;
			});
		}

		private void OnTranscriptionFinished(object sender, TranscriptionFinishedEventArgs e)
		{
			if (e.Result.Status == "success")
			{
                AddToHistory(_queueManager.Jobs[0].FilePath, e.Result.Text);
			}
		}

		// --- Event Handlers ---

		private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			//if (HistoryGrid.SelectedItem is TranscriptionHistoryItem selectedItem)
			//{
			//	boxTranscriptOutput.Text = selectedItem.TranscriptText;

			//	txtSelectedFileStats.Text = $"File: {selectedItem.FileName} | Chars: {selectedItem.TranscriptText.Length}";
			//}
		}

		// Change the method name to match XAML, and use SelectionChangedEventArgs
		private void QueueGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// Check if the selected item is valid and cast it to your TranscriptionJob model
			if (QueueGrid.SelectedItem is SpeechMaster.Models.Transcription.TranscriptionJob selectedJob)
			{
				// If the result exists, show it. Otherwise, show the status.
				if (!string.IsNullOrEmpty(selectedJob.Result))
				{
					boxTranscriptOutput.Text = selectedJob.Result;
				}
				else
				{
					boxTranscriptOutput.Text = $"Status: {selectedJob.Status ?? "Pending..."}";
				}
			}
		}

		private void TranscribeService_TranscriptionStarted(object? sender, System.EventArgs e)
        {
            // Marshals to UI thread and start indeterminate progress
            Dispatcher.Invoke(() =>
            {
				progressTranscript.IsIndeterminate = true;
                progressTranscript.Visibility = Visibility.Visible;
                StatusText.Text = "Transcribing...";
            });
        }

        private void TranscribeService_TranscriptionFinished(object? sender, TranscriptionFinishedEventArgs e)
        {
            // Marshals to UI thread and stop progress
            Dispatcher.Invoke(() =>
            {
                btnStartTranscribe.Content = "▶";
				progressTranscript.IsIndeterminate = false;
                progressTranscript.Value = 0;
                progressTranscript.Visibility = Visibility.Visible;
                StatusText.Text = "Ready";
            });
        }

		// TODO: Support Drag and Drop of files into the window
		private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Audio files|*.wav;*.mp3;*.ogg;*.flac|All files|*.*";
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == true)
            {
                if (ofd.FileNames.Length > 1)
                {
                    // Multiple files selected - add to queue
                    foreach (var file in ofd.FileNames)
                    {
                        _queueManager.AddFile(file);
                    }
                    txtAudioFilePath.Text = $"{ofd.FileNames.Length} files added to queue.";
                }
                else
                {
                    // Single file selected
                    var file = ofd.FileName;
                    txtAudioFilePath.Text = file;
                    _queueManager.AddFile(file);
                }
            }
                
        }

        private async void BtnStartTranscribe_Click(object sender, RoutedEventArgs e)
        {
			var file = txtAudioFilePath.Text;

            if (string.IsNullOrWhiteSpace(file))
            {
                MessageBox.Show("Select audio file first.");
                return;
            }

            boxTranscriptOutput.Text = "Please wait, analizing file...";
            StatusService.Instance.UpdateStatus("Working...");
			btnStartTranscribe.Content = "⏹";
			blockStartTranscription.Text = "Stop Transcription";
			btnStartTranscribe.Background = System.Windows.Media.Brushes.OrangeRed;

			try
            {
                // Initiate transcription via TranscribeService
                await _queueManager.StartProcessing();
				// FIXME: This is probably processing only the first item in the queue
				var currentJob = _queueManager.Jobs[0];

                if (currentJob == null)
                {
                    var msg = "No transcript received.";
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    boxTranscriptOutput.Text = msg;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(currentJob.Result))
                {
					// TODO: It might be better to have text summarization functionality
					// TODO: Implement key phrases extract functionality for NLP analysis
					boxTranscriptOutput.Text = currentJob.Result;
					btnStartTranscribe.Content = "▶";
					blockStartTranscription.Text = "Start Transcription";
                    btnStartTranscribe.Background = System.Windows.Media.Brushes.DarkBlue;
				}
                else
                {
                    // If Transcriber sets Message on failure, show it; otherwise show fallback
                    var msg = currentJob.Status ?? "An error occurred during transcription process.";
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    boxTranscriptOutput.Text = msg;
                }
            }
            catch (FileNotFoundException fnfEx)
            {
                // e.g. audio file not found
                var msg = $"File not found: {fnfEx.Message}";
                MessageBox.Show(msg, "File not found", MessageBoxButton.OK, MessageBoxImage.Error);
                boxTranscriptOutput.Text = msg;
            }
            catch (System.ComponentModel.Win32Exception winEx)
            {
                // e.g. python executable not found or cannot be started
                var msg = $"Не вдалося запустити зовнішню програму: {winEx.Message}";
                MessageBox.Show(msg, "Start error", MessageBoxButton.OK, MessageBoxImage.Error);
                boxTranscriptOutput.Text = msg;
            }
            catch (Exception ex)
            {
                // Generic fallback
                var msg = $"An error occurred: {ex.Message}";
                MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                boxTranscriptOutput.Text = msg;
            }
        }

        private void BtnCancelTranscribe_Click(object sender, RoutedEventArgs e)
        {
            _queueManager.CancelProcessing();
        }

        private void BtnCopyText_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(boxTranscriptOutput.Text))
            {
                Clipboard.SetText(boxTranscriptOutput.Text);
            }
        }

        private void BtnSaveTxt_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Text files|*.txt|All files|*.*"
            };
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, boxTranscriptOutput.Text);
            }
        }

		private void ApplySettingsToUI()
        {
			// Dummy method to apply settings to UI
		}

		private async Task DownloadWhisper()
        {
            // Disable UI interactions while downloading
            IsEnabled = false;

            try
            {
                // Download whisper.dll if missing
                await _setupService.EnsureEngineExistsAsync();

                // Download default model if missing
                await _setupService.EnsureModelExistsAsync("ggml-base.bin");

                StatusService.Instance.UpdateStatus("Ready");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download Whisper: {ex.Message}", "Download Failed");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void SettingsManager_SettingsChanged(object? sender, System.EventArgs e)
        {
            // Apply settings to UI on UI thread
            Dispatcher.Invoke(ApplySettingsToUI);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load settings early so we can apply them to UI
            _settingsManager.LoadSettings();
            _settingsManager.SettingsChanged += SettingsManager_SettingsChanged;
            // Subscribe to service events
            _transcribeSerivce.TranscriptionStarted += TranscribeService_TranscriptionStarted;
            _transcribeSerivce.TranscriptionFinished += TranscribeService_TranscriptionFinished;
            // Subscribe to status updates
            StatusService.Instance.OnStatusChanged += UpdateStatusText;
            if (!_setupService.IsWhisperEngineInstalled() &&
                MessageBox.Show("Whisper ASR engine not found. Download and install it now?",
                    "Engine Missing", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                MessageBoxResult.Yes)
            {
                ProgressWindow progressWindow = new ProgressWindow
                {
                    Owner = this
                };
                progressWindow.Show();
                await DownloadWhisper();
                progressWindow.Close();
                return;
            }
            // Check tools availability
            GetAsrEngineLocation();
            if (FFmpegLoader.IsFfmpegAvailableInShell())
            {
                txtFFMPEGPath.Text = "FFMPEG: Installed";
            }
            else
            {
                if (MessageBox.Show("FFMPEG not found in system PATH. Install via winget now?",
                "FFMPEG Missing", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                MessageBoxResult.Yes)
                {
                    ProgressWindow progressWindow = new ProgressWindow
                    {
                        Owner = this
                    };
                    progressWindow.Show();
                    StatusService.Instance.UpdateStatus("Installing FFMPEG via winget...");
                    await Task.Run(() => FFmpegLoader.InstallFfmpegViaWinget());
                    StatusService.Instance.SetProgress(100);
                    StatusService.Instance.UpdateStatus("System Ready");
                    FFmpegLoader.RefreshEnvironmentPath();
                    progressWindow.Close();
                }
            }
		}

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
			// Unsubscribe from service events
			_transcribeSerivce.TranscriptionStarted -= TranscribeService_TranscriptionStarted;
            _transcribeSerivce.TranscriptionFinished -= TranscribeService_TranscriptionFinished;
		}

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuBenchmark_Click(object sender, RoutedEventArgs e)
        {
            BenchmarkWindow benchmarkWindow = new BenchmarkWindow
            {
                Owner = this
            };
            benchmarkWindow.ShowDialog();
        }

        private void MenuVideoDownloader_Click(object sender, RoutedEventArgs e)
        {
            MediaDownloadWindow videoDownloadWindow = new MediaDownloadWindow
			{
                Owner = this
            };
            videoDownloadWindow.ShowDialog();
        }

		private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
			// Initialize your other window (ensure you have created 'DetailsWindow.xaml')
			var settingsWindow = new SettingsWindow(_settingsManager)
			{
				Owner = this
			};

			var result = settingsWindow.ShowDialog();
			if (result == true)
			{
				// Settings were saved inside dialog. Ensure UI reflects them.
				ApplySettingsToUI();
			}
		}

		private void MenuLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs.txt");
                Process.Start(new ProcessStartInfo("notepad.exe", filePath) { UseShellExecute = false });
            }
            catch
            {
                MessageBox.Show("Could not open logs file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow
            {
                Owner = this
            };
            aboutWindow.ShowDialog();
		}
	}
}