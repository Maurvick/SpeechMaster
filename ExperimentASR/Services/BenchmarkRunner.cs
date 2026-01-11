using ExperimentASR.Models;
using System.Diagnostics;

namespace ExperimentASR.Services
{
    public static class BenchmarkRunner
    {
        public static async Task<BenchmarkResult> RunEngineBenchmarkAsync(AsrEngine engine, IEnumerable<AudioReferenceItem> testItems)
        {
            double totalWer = 0;
            long totalTime = 0;
            int count = 0;

            foreach (var item in testItems)
            {
                var stopwatch = Stopwatch.StartNew();
                var hypothesis = await engine.TranscribeAsync(item.AudioPath);
                stopwatch.Stop();

                var wer = WerCalculator.CalculateWer(item.ReferenceText, hypothesis);
                totalWer += wer;
                totalTime += stopwatch.ElapsedMilliseconds;
                count++;
            }

            return new BenchmarkResult
            {
                ModelName = engine.Name,
                AverageWer = totalWer / count,
                AverageRtf = (double)totalTime / (testItems.Sum(x => AudioHelper.GetAudioFileDuration(x.AudioPath))),
                TestsCount = count
            };
        }
    }
}
