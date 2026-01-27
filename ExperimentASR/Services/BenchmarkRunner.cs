using SpeechMaster.Models;
using System.Diagnostics;

namespace SpeechMaster.Services
{
	// FIXME: Incompleted class. Crucial for project.
	public class BenchmarkRunner
    {
		private readonly HistoryService _historyService;

        public BenchmarkRunner(HistoryService historyService)
        {
            _historyService = historyService;
		}

		public async Task<List<BenchmarkResult>> RunBenchmarkAsync(List<IAsrEngine> engines, 
            List<DatasetItem> testItems)
        {
            var results = new List<BenchmarkResult>();

            foreach (var engine in engines)
            {
                double totalWer = 0, totalCer = 0, totalRtf = 0;
                int count = 0;

                await Parallel.ForEachAsync(testItems, async (item, ct) =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    var hypothesis = await engine.TranscribeAsync(item.AudioPath);
                    stopwatch.Stop();

                    string normRef = UkrainianNormalizer.Normalize(item.ReferenceText);
                    string normHyp = UkrainianNormalizer.Normalize(hypothesis.Text);

                    double wer = WerCalculator.CalculateWer(normRef, normHyp);
                    double cer = WerCalculator.CalculateCer(normRef, normHyp);
                    double rtf = stopwatch.Elapsed.TotalSeconds / item.Duration;

                    //await _historyService.SaveTranscriptionAsync(
                    //    new TranscriptionResult(normHyp, hypothesis.Segments),
                    //    engine.Name,
                    //    item.AudioPath,
                    //    item.Duration,
                    //    wer,
                    //    rtf);

                    totalWer += wer;
                    totalCer += cer;
                    totalRtf += rtf;
                    count++;
                });

                results.Add(new BenchmarkResult
                {
                    ModelName = engine.Name,
                    AverageWer = totalWer / count,
                    AverageCer = totalCer / count,
                    AverageRtf = totalRtf / count,
                    TestCount = count
                });
            }

            await _historyService.SaveBenchmarkAsync("Common Voice uk", results);

            return results;
        }
    }
}
