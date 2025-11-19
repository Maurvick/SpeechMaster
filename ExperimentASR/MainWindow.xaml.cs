using Microsoft.Win32;
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

            // запуск розпізнавання у окремому потоці
            var result = await Task.Run(() => _transcriber.Transcribe(file));

            OutputBox.Text = result;
        }
    }
}