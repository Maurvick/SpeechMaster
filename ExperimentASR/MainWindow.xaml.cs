using ExperimentASR.Services;
using Microsoft.Win32;
using NAudio.Wave;
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
        private TranscribeService transcribeSerivce = new TranscribeService();

        public MainWindow()
        {
            InitializeComponent();
            if (File.Exists(transcribeSerivce.AsrEngineLocation))
            {
                textAsrLocation.Text = "ASR Engine Location found: " + transcribeSerivce.AsrEngineLocation;
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

            // Subscribe to service events
            transcribeSerivce.TranscriptionStarted += TranscribeService_TranscriptionStarted;
            transcribeSerivce.TranscriptionFinished += TranscribeService_TranscriptionFinished;
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
            blockStatus.Text = "Working...";

            try
            {
                // запуск розпізнавання у окремому потоці
                var result = await Task.Run(() => transcribeSerivce.Transcribe(file));

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

        private int GetAudioFileDuration(string filePath)
        {
            // Synchronous read so callers get the actual duration
            using var reader = new AudioFileReader(filePath);
            TimeSpan duration = reader.TotalTime;
            return (int)duration.TotalSeconds;
        }

        //private async void MoveProgress()
        //{
        //    durationInSeconds = await Task.Run(() => GetAudioFileDuration(url)).ConfigureAwait(false);
        //    int audioDurationSeconds = _audioDurationInSeconds;
        //    if (audioDurationSeconds <= 0) return;
        //    int smallModelProccessingTime = 12;
        //    double processingSpeed = audioDurationSeconds / (double)smallModelProccessingTime;

        //    for (int i = 0; i <= audioDurationSeconds; i++)
        //    {
        //        int progress = (int)((i / (double)audioDurationSeconds) * 100);
        //        progressTranscript.Value = Math.Clamp(progress, 0, 100);
        //        await Task.Delay(Math.Max(1, (int)(100 / processingSpeed)));
        //    }

        //    await Task.Delay(500);
        //}

        private void TranscribeService_TranscriptionStarted(object? sender, System.EventArgs e)
        {
            // Marshals to UI thread and start indeterminate progress
            Dispatcher.Invoke(() =>
            {
                progressTranscript.IsIndeterminate = true;
                progressTranscript.Visibility = Visibility.Visible;
                blockStatus.Text = "Transcribing...";
            });
        }

        private void TranscribeService_TranscriptionFinished(object? sender, Services.TranscriptionFinishedEventArgs e)
        {
            // Marshals to UI thread and stop progress
            Dispatcher.Invoke(() =>
            {
                progressTranscript.IsIndeterminate = false;
                progressTranscript.Value = 0;
                progressTranscript.Visibility = Visibility.Visible;
                blockStatus.Text = "Ready";
            });
        }

        private void btnCancelTranscribe_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        }

        private void btnSaveSrt_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}