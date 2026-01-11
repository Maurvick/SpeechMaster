using ExperimentASR.Models;
using Python.Runtime;
using System.IO;
using System.Text.Json;

namespace ExperimentASR.Services
{
    public class DatasetLoader
    {
        private readonly List<AudioReferenceItem> _testItems = new();

        public async Task LoadDatasetAsync(string path)
        {
            await Task.Run(() =>
            {
                PythonEngine.Initialize();
                using (Py.GIL())
                {
                    dynamic sys = Py.Import("sys");
                    sys.path.append(Environment.CurrentDirectory);

                    dynamic extract = Py.Import("ExtractDataset");
                    extract.extract_to_json(path);
                }
            });

            var json = await File.ReadAllTextAsync(path);
            var samples = JsonSerializer.Deserialize<List<DatasetSample>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            foreach (var sample in samples)
            {
                _testItems.Add(new AudioReferenceItem
                {
                    AudioPath = sample.AudioPath,
                    ReferenceText = sample.Transcription
                });
            }
        }

        public IReadOnlyList<AudioReferenceItem> TestItems => _testItems;
    }
}
