using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeechMaster.Models;
using SpeechMaster.Models.Transcription;

namespace SpeechMaster.Services
{
	// FIXME: Incompleted class. Needs implementation or removal.
	public class HistoryService
	{
		private readonly AppDbContext _context;

		public HistoryService(AppDbContext context)
		{
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_context.Database.Migrate(); // автоматична міграція
		}

		public async Task SaveTranscriptionAsync(
			IAsrEngine engine,
			string audioPath,
			TranscriptionResult result,
			double duration,
			double wer,
			double rtf)
		{
			var entry = new TranscriptionEntry
			{
				DateTime = DateTime.Now,
				ModelName = engine.Name,
				AudioPath = audioPath,
				TranscriptionText = result.Text,
				Duration = duration,
				Wer = wer,
				Rtf = rtf
			};

			_context.Transcriptions.Add(entry);
			await _context.SaveChangesAsync();
		}

		public async Task SaveBenchmarkAsync(string datasetName, List<BenchmarkResult> results)
		{
			var json = JsonSerializer.Serialize(results);
			var entry = new BenchmarkEntry
			{
				DateTime = DateTime.Now,
				DatasetName = datasetName,
				ResultsJson = json
			};

			_context.Benchmarks.Add(entry);
			await _context.SaveChangesAsync();
		}
	}
}
