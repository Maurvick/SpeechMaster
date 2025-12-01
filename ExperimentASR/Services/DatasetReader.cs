using ExperimentASR.Models;
using Python.Runtime;
using System.IO;
using System.Text.Json;

namespace ExperimentASR.Services
{
    public class DatasetReader
    {
        private readonly List<TestItem> _testItems = new();

        public async Task LoadDatasetAsync()
        {
            // Ініціалізуємо Python
            await Task.Run(() =>
            {
                PythonEngine.Initialize();
                using (Py.GIL())
                {
                    // Викликаємо Python-скрипт
                    dynamic sys = Py.Import("sys");
                    sys.path.append(Environment.CurrentDirectory);

                    dynamic extract = Py.Import("ExtractDataset");
                    extract.extract_to_json("dataset.json");
                }
            });

            // Читаємо JSON
            var json = await File.ReadAllTextAsync("dataset.json");
            var samples = JsonSerializer.Deserialize<List<DatasetSample>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            foreach (var sample in samples)
            {
                _testItems.Add(new TestItem
                {
                    AudioPath = sample.AudioPath,
                    ReferenceText = sample.Transcription
                });
            }
        }

        public IReadOnlyList<TestItem> TestItems => _testItems;
    }
}
