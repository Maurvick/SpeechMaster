using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ExperimentASR.Models;
using ExperimentASR.Services;
using ExperimentASR.Services.Engines;

namespace ExperimentASR.Views
{
	/// <summary>
	/// Interaction logic for BenchmarkWindow.xaml
	/// </summary>
	public partial class BenchmarkWindow : Window
    {
		private readonly DatasetLoader _datasetReader = new();
		private readonly EngineManager _engineManager = new();

		public BenchmarkWindow()
        { 
            InitializeComponent();
        }

        private async void btnBenchmark_Click(object sender, RoutedEventArgs e)
        {
			var engines = _engineManager.AvailableEngines;
			StatusService.Instance.UpdateStatus("Loading dataset...");

			StatusService.Instance.UpdateStatus($"{_datasetReader.TestItems.Count} " +
				$"samples loaded from dataset...");

			engines.AddRange(
			[
				new VoskEngine(),
			]);

			var results = new ObservableCollection<BenchmarkResult>();
			BenchmarkDataGrid.ItemsSource = results;

			var bench = new BenchmarkRunner();
			foreach (var engine in engines)
			{
				StatusService.Instance.UpdateStatus($"Testing {engine.Name}...");

				var benchmark = await bench.RunEngineBenchmarkAsync(engine, 
					_datasetReader.TestItems.Take(50));
				results.Add(benchmark);
			}

			StatusService.Instance.UpdateStatus("✅ Benchmark completed!");
		}
	}
}
