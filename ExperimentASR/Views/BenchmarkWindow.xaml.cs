using SpeechMaster.Models;
using SpeechMaster.Services;
using SpeechMaster.Services.Engines;
using System.Collections.ObjectModel;
using System.Windows;

namespace ExperimentASR.Views
{
	/// <summary>
	/// Interaction logic for BenchmarkWindow.xaml
	/// </summary>
	public partial class BenchmarkWindow : Window
    {
		private readonly List<AsrEngine> _asrEngines = new();
		private readonly EngineManager _engineSetupService = new();

		public BenchmarkWindow()
        { 
            InitializeComponent();
        }

        private async void BtnBenchmark_Click(object sender, RoutedEventArgs e)
        {
			if (_engineSetupService.IsWhisperEngineInstalled())
			{
				_asrEngines.Add(new WhisperEngine());
			}
			StatusService.Instance.UpdateStatus("Loading dataset...");

			var results = new ObservableCollection<BenchmarkResult>();
			BenchmarkDataGrid.ItemsSource = results;

			foreach (var engine in _asrEngines)
			{
				StatusService.Instance.UpdateStatus($"Testing {engine.Name}...");
			}

			StatusService.Instance.UpdateStatus("✅ Benchmark completed!");
		}
	}
}
