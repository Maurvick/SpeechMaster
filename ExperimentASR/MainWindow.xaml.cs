using ExperimentASR.Models;
using ExperimentASR.Services;
using ExperimentASR.Services.Engines;
using ExperimentASR.Views;
using ExperimentASR.Windows;
using Microsoft.Win32;
using NAudio.Wave;
using Parquet.Schema;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Policy;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ExperimentASR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly TranscribeService _transcribeSerivce = new();
        private readonly SettingsManager _settingsManager = new();
        private readonly TranscriptionQueueManager _manager = new();
        private readonly EngineSetupService _setupService = new();

        public MainWindow()
        {
            InitializeComponent();
            // Bind the DataGrid to queue manager list
            QueueGrid.ItemsSource = _manager.Jobs;
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

		private void GetAsrEngineLocation()
        {
            if (File.Exists(_transcribeSerivce.AsrEngineLocation))
            {
                textAsrLocation.Text = "ASR Engine Location found: " + _transcribeSerivce.AsrEngineLocation;
            }
            else
            {
                textAsrLocation.Text = "ASR Engine Location not found.";
                textAsrLocation.Foreground = System.Windows.Media.Brushes.Red;
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

        private void GetFFMPEGLocation()
        {
			// TODO: this is does not work as intended, needs fixing
			try
			{
                ProcessStartInfo processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C set", // This command lists all env vars
                    UseShellExecute = false, // Must be false to modify Environment
                    RedirectStandardOutput = true
                };

                if (processStartInfo.Environment.ContainsKey("FFMPEG"))
                {

                    txtFFMPEGPath.Text = "FFMPEG Path: " + 
                        processStartInfo.Environment["FFMPEG"];
                }
            }
            catch
            {
                return;
            }
        }

        // TODO: this is unused, remove or implement
        private async void UpdateProgressBar()
        {
            int audioDurationSeconds = await Task.Run(() => 
                AudioHelper.GetAudioFileDuration(txtAudioFilePath.Text)).ConfigureAwait(false);
            if (audioDurationSeconds <= 0) return;
            int smallModelProccessingTime = 12;
            double processingSpeed = audioDurationSeconds / (double)smallModelProccessingTime;

            for (int i = 0; i <= audioDurationSeconds; i++)
            {
                int progress = (int)((i / (double)audioDurationSeconds) * 100);
                progressTranscript.Value = Math.Clamp(progress, 0, 100);
                await Task.Delay(Math.Max(1, (int)(100 / processingSpeed)));
            }

            await Task.Delay(500);
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

        private void TranscribeService_TranscriptionFinished(object? sender, Models.TranscriptionFinishedEventArgs e)
        {
            // Marshals to UI thread and stop progress
            Dispatcher.Invoke(() =>
            {
                progressTranscript.IsIndeterminate = false;
                progressTranscript.Value = 0;
                progressTranscript.Visibility = Visibility.Visible;
                StatusText.Text = "Ready";
            });
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
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
                        _manager.AddFile(file);
                    }
                    txtAudioFilePath.Text = $"{ofd.FileNames.Length} files added to queue.";
                }
                else
                {
                    // Single file selected
                    var file = ofd.FileName;
                    txtAudioFilePath.Text = file;
                    _manager.AddFile(file);
                }
            }
                
        }

        // Start Transcription
        private async void Transcribe_Click(object sender, RoutedEventArgs e)
        {
            var file = txtAudioFilePath.Text;

            if (string.IsNullOrWhiteSpace(file))
            {
                MessageBox.Show("Select audio file first.");
                return;
            }

            boxTranscriptOutput.Text = "Please wait, analizing file...";
            StatusService.Instance.UpdateStatus("Working...");

            try
            {
                // Initiate transcription via TranscribeService
                await _manager.StartProcessing();
                var currentJob = _manager.Jobs.First();

                if (currentJob == null)
                {
                    var msg = "No transcript received.";
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    boxTranscriptOutput.Text = msg;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(currentJob.Result))
                {
                    boxTranscriptOutput.Text = currentJob.Result;
                }
                else
                {
                    // If Transcriber sets Message on failure, show it; otherwise show fallback
                    var msg = currentJob.Status ?? "Під час розпізнавання сталася помилка.";
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    boxTranscriptOutput.Text = msg;
                }
            }
            catch (FileNotFoundException fnfEx)
            {
                // e.g. audio file not found
                var msg = $"Файл не знайдено: {fnfEx.Message}";
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
                var msg = $"Помилка під час розпізнавання: {ex.Message}";
                MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                boxTranscriptOutput.Text = msg;
            }
        }

        private void btnCancelTranscribe_Click(object sender, RoutedEventArgs e)
        {
            _manager.CancelProcessing();
        }

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(boxTranscriptOutput.Text))
            {
                Clipboard.SetText(boxTranscriptOutput.Text);
            }
        }

        private void btnSaveTxt_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog();
            sfd.Filter = "Text files|*.txt|All files|*.*";
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, boxTranscriptOutput.Text);
            }
        }

        private void btnSaveSrt_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnSummarizeText_Click(object sender, RoutedEventArgs e)
        {

        }   

        private void comboModelSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
        }

        private void MoreOptions_Click(object sender, RoutedEventArgs e)
        {
            
        }

        // TODO: implement applying settings to UI
        private void ApplySettingsToUI()
        {
 
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
            if (!_setupService.IsEngineInstalled())
            {
                if (MessageBox.Show("Whisper ASR engine not found. Download and install it now?", 
                    "Engine Missing", MessageBoxButton.YesNo, MessageBoxImage.Question) == 
                    MessageBoxResult.Yes)
                {
					ProgressWindow progressWindow = new ProgressWindow
                    {
                        Owner = this
                    };
					progressWindow.Show();
					await DownloadWhisper();
                    return;
				}
			}
            // Check tools availability
            GetAsrEngineLocation();
            GetFFMPEGLocation();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
			// Unsubscribe from service events
			_transcribeSerivce.TranscriptionStarted -= TranscribeService_TranscriptionStarted;
            _transcribeSerivce.TranscriptionFinished -= TranscribeService_TranscriptionFinished;
		}

        private void menuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void menuBenchmark_Click(object sender, RoutedEventArgs e)
        {
            BenchmarkWindow benchmarkWindow = new BenchmarkWindow
            {
                Owner = this
            };
            benchmarkWindow.ShowDialog();
        }

        private void menuSettings_Click(object sender, RoutedEventArgs e)
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

		private void menuLogs_Click(object sender, RoutedEventArgs e)
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

        private void menuAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow
            {
                Owner = this
            };
            aboutWindow.ShowDialog();
		}
	}
}