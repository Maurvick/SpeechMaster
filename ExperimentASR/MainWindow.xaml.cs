using ExperimentASR.Services;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ExperimentASR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Transcriber _transcriber = new Transcriber();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Audio files|*.wav;*.mp3;*.ogg;*.flac|All files|*.*";

            if (ofd.ShowDialog() == true)
                AudioPath.Text = ofd.FileName;
        }

        private async void Transcribe_Click(object sender, RoutedEventArgs e)
        {
            var file = AudioPath.Text;

            if (string.IsNullOrWhiteSpace(file))
            {
                MessageBox.Show("Спочатку оберіть аудіо.");
                return;
            }

            OutputBox.Text = "Зачекайте, аналіз триває...";
            lblTranscriptStatus.Content = "Working...";
            lblTranscriptStatus.Foreground = Brushes.Orange;


            try
            {
                // запуск розпізнавання у окремому потоці
                var result = await Task.Run(() => _transcriber.Transcribe(file));

                if (result == null)
                {
                    var msg = "Результат відсутній.";
                    MessageBox.Show(msg, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    OutputBox.Text = msg;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(result.Transcript))
                {
                    OutputBox.Text = result.Transcript;
                }
                else
                {
                    // If Transcriber sets Message on failure, show it; otherwise show fallback
                    var msg = result.Message ?? "Під час розпізнавання сталася помилка.";
                    MessageBox.Show(msg, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    OutputBox.Text = msg;
                }
            }
            catch (FileNotFoundException fnfEx)
            {
                // e.g. audio file not found
                var msg = $"Файл не знайдено: {fnfEx.Message}";
                MessageBox.Show(msg, "Файл не знайдено", MessageBoxButton.OK, MessageBoxImage.Error);
                OutputBox.Text = msg;
            }
            catch (System.ComponentModel.Win32Exception winEx)
            {
                // e.g. python executable not found or cannot be started
                var msg = $"Не вдалося запустити зовнішню програму: {winEx.Message}";
                MessageBox.Show(msg, "Помилка запуску", MessageBoxButton.OK, MessageBoxImage.Error);
                OutputBox.Text = msg;
            }
            catch (Exception ex)
            {
                // Generic fallback
                var msg = $"Помилка під час розпізнавання: {ex.Message}";
                MessageBox.Show(msg, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                OutputBox.Text = msg;
            }
        }

        private void btnCancelTranscribe_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}