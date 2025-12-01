using ExperimentASR.Models;
using ExperimentASR.Services;
using Microsoft.Win32;
using NAudio.Wave;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Policy;
using System.Windows;
using System.Windows.Controls;

namespace ExperimentASR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly TranscribeService _transcribeSerivce = new TranscribeService();
        private readonly DatasetReader _datasetReader = new(); // або HuggingFaceDatasetLoader

        public MainWindow()
        {
            InitializeComponent();
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

                    txtFFMPEGPath.Text = "FFMPEG Path: " + processStartInfo.Environment["FFMPEG"];
                }
            }
            catch
            {
                return;
            }
        }

        private async void UpdateProgressBar()
        {
            int audioDurationSeconds = await Task.Run(() => AudioHelper.GetAudioFileDuration(txtAudioFilePath.Text)).ConfigureAwait(false);
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Check tools availability
            GetAsrEngineLocation();
            GetFFMPEGLocation();
            // Subscribe to service events
            _transcribeSerivce.TranscriptionStarted += TranscribeService_TranscriptionStarted;
            _transcribeSerivce.TranscriptionFinished += TranscribeService_TranscriptionFinished;
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Audio files|*.wav;*.mp3;*.ogg;*.flac|All files|*.*";

            if (ofd.ShowDialog() == true)
                txtAudioFilePath.Text = ofd.FileName;
        }

        private async void Transcribe_Click(object sender, RoutedEventArgs e)
        {
            var file = txtAudioFilePath.Text;

            if (string.IsNullOrWhiteSpace(file))
            {
                MessageBox.Show("Select audio file first.");
                return;
            }

            boxTranscriptOutput.Text = "Please wait, analizing file...";
            StatusText.Text = "Working...";

            try
            {
                // Initiate transcription via TranscribeService
                var result = await Task.Run(() => _transcribeSerivce.Transcribe(file));

                if (result == null)
                {
                    var msg = "No transcript received.";
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    boxTranscriptOutput.Text = msg;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(result.Transcript))
                {
                    boxTranscriptOutput.Text = result.Transcript;
                }
                else
                {
                    // If Transcriber sets Message on failure, show it; otherwise show fallback
                    var msg = result.Message ?? "Під час розпізнавання сталася помилка.";
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            // save text to file
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

        private void comboModelSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 1. Safety Check: If the UI hasn't finished loading, these controls are null.
            if (radioWhisperBase == null) return;

            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                // 2. Safety Check: Ensure Content isn't null before ToString()
                if (selectedItem.Content == null) return;

                string selectedModel = selectedItem.Content.ToString();

                if (selectedModel == "Whisper")
                {
                    radioWhisperBase.IsEnabled = true;
                    radioWhisperTiny.IsEnabled = true;
                    radioWhisperSmall.IsEnabled = true;
                    radioWhisperMedium.IsEnabled = true;
                }
                else
                {
                    // Optional: logic to disable them if not Whisper
                    radioWhisperBase.IsEnabled = false;
                    radioWhisperTiny.IsEnabled = false;
                    radioWhisperSmall.IsEnabled = false;
                    radioWhisperMedium.IsEnabled = false;
                }
            }
        }

        private void MoreOptions_Click(object sender, RoutedEventArgs e)
        {
            // Initialize your other window (ensure you have created 'DetailsWindow.xaml')
            

            // Use .Show() to open it and let the user click back to the main window
            

            // OR use .ShowDialog() to force the user to close the new window 
            // before returning to the main one (good for settings/modals)
            // detailsWindow.ShowDialog();
        }

        private async void btnBenchmark_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Завантаження датасету OpenTTS-Mykyta...";

            // Варіант 1: Python.NET
            await _datasetReader.LoadDatasetAsync();

            // Варіант 2: HTTP API  
            // var loader = new HuggingFaceDatasetLoader();
            // _testItems = await loader.LoadDatasetAsync(150);

            StatusText.Text = $"✅ Завантажено {_datasetReader.TestItems.Count} семплів";

            // Ініціалізуємо двигуни
            //_engines.AddRange(new AsrEngine[]
            //{
            //    new WhisperCppEngine("models/ggml-large-v3-turbo.bin"),
            //    new VoskEngine("vosk-model-uk"),
            //    new SileroOnnxEngine("silero_stt_uk_v5.onnx")
            //});

            var results = new ObservableCollection<BenchmarkResult>();
            BenchmarkDataGrid.ItemsSource = results;
            var bench = new BenchmarkRunner();
            foreach (var engine in _engines)
            {
                StatusText.Text = $"Тестую {engine.Name}...";

                var benchmark = await bench.RunEngineBenchmark(engine, _datasetReader.TestItems.Take(50));
                results.Add(benchmark);
            }

            StatusText.Text = "✅ Бенчмарк завершено!";
        }
    }
}