using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExperimentASR.Views;
using ExperimentASR.Windows;
using FFMpegCore;
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
			Dispatcher.Invoke(() =>
			{
				StatusText.Text = message;
			});
		}

		protected override void OnClosed(EventArgs e)
		{
			StatusService.Instance.OnStatusChanged -= UpdateStatusText;
			base.OnClosed(e);
		}

		private void GetAsrEngineLocation()
		{
			if (File.Exists(_transcribeSerivce.AsrEngineLocation))
			{
				// textAsrLocation.Text = "ASR Engine Location found: " + ...
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
				ProcessedItems.Insert(0, newItem);
			});
		}

		private void OnTranscriptionFinished(object sender, TranscriptionFinishedEventArgs e)
		{
			if (e.Result.Status == "success")
			{
				// Note: This logic assumes single item processing or needs adjustment for batch
				if (_queueManager.Jobs.Count > 0)
				{
					AddToHistory(_queueManager.Jobs[0].FilePath, e.Result.Text);
				}
			}
		}

		// --- Event Handlers ---

		private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// Implementation hidden
		}

		private void QueueGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (QueueGrid.SelectedItem is TranscriptionJob selectedJob)
			{
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
			Dispatcher.Invoke(() =>
			{
				progressTranscript.IsIndeterminate = true;
				progressTranscript.Visibility = Visibility.Visible;
				StatusText.Text = "Transcribing...";
			});
		}

		private void TranscribeService_TranscriptionFinished(object? sender, TranscriptionFinishedEventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				btnStartTranscribe.Content = "▶";
				progressTranscript.IsIndeterminate = false;
				progressTranscript.Value = 0;
				progressTranscript.Visibility = Visibility.Visible;
				StatusText.Text = "Ready";
			});
		}

		private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
		{
			var ofd = new OpenFileDialog();
			ofd.Filter = "Audio files|*.wav;*.mp3;*.ogg;*.flac|All files|*.*";
			ofd.Multiselect = true;
			if (ofd.ShowDialog() == true)
			{
				if (ofd.FileNames.Length > 1)
				{
					foreach (var file in ofd.FileNames)
					{
						_queueManager.AddFile(file);
					}
					txtAudioFilePath.Text = $"{ofd.FileNames.Length} files added to queue.";
				}
				else
				{
					var file = ofd.FileName;
					txtAudioFilePath.Text = file;
					_queueManager.AddFile(file);
				}
			}
		}

		private async void BtnStartTranscribe_Click(object sender, RoutedEventArgs e)
		{
			var file = txtAudioFilePath.Text;

			// Якщо черга порожня і файл не вибрано
			if (string.IsNullOrWhiteSpace(file) && _queueManager.Jobs.Count == 0)
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
				// --- ВИПРАВЛЕННЯ ПОМИЛКИ StartProcessing ---

				// 1. Отримуємо налаштування розміру моделі (наприклад, "small")
				string modelSizeString = _settingsManager.WhisperModelSize ?? "base";

				// 2. Конвертуємо рядок у Enum WhisperModelType
				// Якщо конвертація не вдалася, використовуємо 'Base' за замовчуванням
				if (!Enum.TryParse(modelSizeString, true, out WhisperModelType selectedModel))
				{
					selectedModel = WhisperModelType.Base;
				}

				// 3. Передаємо Enum у метод StartProcessing
				await _queueManager.StartProcessing(selectedModel);

				// -------------------------------------------

				// Відображення результату (для першого файлу в черзі)
				if (_queueManager.Jobs.Count > 0)
				{
					var currentJob = _queueManager.Jobs[0];

					if (!string.IsNullOrWhiteSpace(currentJob.Result))
					{
						boxTranscriptOutput.Text = currentJob.Result;
						// Повертаємо кнопку в нормальний стан
						btnStartTranscribe.Content = "▶";
						blockStartTranscription.Text = "Start Transcription";
						btnStartTranscribe.Background = System.Windows.Media.Brushes.DarkBlue;
					}
					else
					{
						// Якщо статус не Done, показуємо помилку або поточний статус
						var msg = currentJob.Status ?? "An error occurred during transcription.";
						boxTranscriptOutput.Text = msg;
					}
				}
			}
			catch (Exception ex)
			{
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
			IsEnabled = false;

			try
			{
				// Завантажуємо Engine
				await _setupService.EnsureEngineExistsAsync();

				// --- ВИПРАВЛЕННЯ 2: Використовуємо Enum для моделі ---
				await _setupService.EnsureModelExistsAsync(WhisperModelType.Base);
				// -----------------------------------------------------

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
			Dispatcher.Invoke(ApplySettingsToUI);
		}

		private void QueueManager_ProcessingStarted(object? sender, EventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				// Вмикаємо індикацію
				progressTranscript.IsIndeterminate = true;
				progressTranscript.Visibility = Visibility.Visible;
				StatusText.Text = "Processing Queue...";

			    // btnStartTranscribe.Content = "⏹";
				// blockStartTranscription.Text = "Stop";
				// btnStartTranscribe.Background = System.Windows.Media.Brushes.OrangeRed;
			});
		}

		private void QueueManager_ProcessingFinished(object? sender, EventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				// Вимикаємо індикацію
				progressTranscript.IsIndeterminate = false;
				progressTranscript.Value = 0; 
											  

				StatusText.Text = "Ready";

				// btnStartTranscribe.Content = "▶";
				// blockStartTranscription.Text = "Start Transcription";
				// btnStartTranscribe.Background = System.Windows.Media.Brushes.DarkBlue;
			});
		}

		private async void Window_Loaded(object sender, RoutedEventArgs e)
		{
			_settingsManager.LoadSettings();
			_settingsManager.SettingsChanged += SettingsManager_SettingsChanged;

			_queueManager.QueueProcessingStarted += QueueManager_ProcessingStarted;
			_queueManager.QueueProcessingFinished += QueueManager_ProcessingFinished;

			StatusService.Instance.OnStatusChanged += UpdateStatusText;

			// --- ВИПРАВЛЕННЯ 3: Перейменовано метод (IsEngineInstalled) ---
			if (!_setupService.IsEngineInstalled() &&
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
			// --------------------------------------------------------------

			GetAsrEngineLocation();

			// FFmpeg check block...
			if (FFmpegLoader.IsFfmpegAvailableInShell())
			{
				txtFFMPEGPath.Text = "FFMPEG: Installed";
			}
			else
			{
				// Logic for FFmpeg installation...
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			_transcribeSerivce.TranscriptionStarted -= TranscribeService_TranscriptionStarted;
			_transcribeSerivce.TranscriptionFinished -= TranscribeService_TranscriptionFinished;
		}

		private void MenuExit_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}

		private void MenuBenchmark_Click(object sender, RoutedEventArgs e)
		{
			BenchmarkWindow benchmarkWindow = new BenchmarkWindow { Owner = this };
			benchmarkWindow.ShowDialog();
		}

		private void MenuVideoDownloader_Click(object sender, RoutedEventArgs e)
		{
			MediaDownloadWindow videoDownloadWindow = new MediaDownloadWindow { Owner = this };
			videoDownloadWindow.ShowDialog();
		}

		private void MenuSettings_Click(object sender, RoutedEventArgs e)
		{
			var settingsWindow = new SettingsWindow(_settingsManager) { Owner = this };
			var result = settingsWindow.ShowDialog();
			if (result == true)
			{
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
			AboutWindow aboutWindow = new AboutWindow { Owner = this };
			aboutWindow.ShowDialog();
		}

		private void MenuConvertVideo_Click(object sender, RoutedEventArgs e)
		{
			var ofd = new OpenFileDialog
			{
				Filter = "Video files (*.mp4)|*.mp4",
				Multiselect = true
			};

			if (ofd.ShowDialog() != true) return;

			try
			{
				foreach (var inputFile in ofd.FileNames)
				{
					// Set up save dialog for the output
					var sfd = new SaveFileDialog
					{
						FileName = Path.GetFileNameWithoutExtension(inputFile),
						Filter = "MP3 Audio|*.mp3|WAV Audio|*.wav",
						Title = $"Export audio from {Path.GetFileName(inputFile)}"
					};

					if (sfd.ShowDialog() != true) continue;

					string outputFile = sfd.FileName;
					string extension = Path.GetExtension(outputFile).ToLower();

					// Perform conversion based on chosen output extension
					if (extension == ".mp3")
					{
						FFMpeg.ExtractAudio(inputFile, outputFile);
					}
					else if (extension == ".wav")
					{
						FFMpegArguments
							.FromFileInput(inputFile)
							.OutputToFile(outputFile, true, options => options
								.WithAudioSamplingRate(44100))
							.ProcessSynchronously();
					}

					// Open folder after completion
					Process.Start("explorer.exe", $"/select,\"{outputFile}\"");
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Technical error during processing: {ex.Message}", "Conversion Failed",
					MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}
}