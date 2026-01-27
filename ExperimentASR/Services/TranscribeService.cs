using SpeechMaster.Models.Transcription;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpeechMaster.Services
{
	// TODO: Support SRT export, multiple ASR engines, etc
	// TODO: Support different model sizes (base, small, medium, large)
	// FIXME: Use TranscriptStarted and TranscriptFinished events in UI
	public class TranscribeService
	{
		private readonly Logger _logger = new Logger();

		// --- Python Configuration ---
		private readonly string _pythonExe = "python";
		private readonly string _scriptPath = "./Scripts/asr_engine.py";

		// --- Whisper.cpp Configuration ---
		private readonly string _whisperCppExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "whisper", "whisper-cli.exe");
		private readonly string _modelsBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");

		// --- Settings ---
		private readonly string _whisperModelSize;
		private readonly string _audioLanguage;
		private readonly string _asrEngine; // "openai_whisper" (python) or "whisper.cpp"

		// --- Diagnostics ---
		private string _rawProcessOutput = "";
		private readonly string _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");

		// --- Events ---
		public event EventHandler? TranscriptionStarted;
		public event EventHandler<TranscriptionFinishedEventArgs>? TranscriptionFinished;

		public TranscribeService()
		{
			SettingsManager settings = new SettingsManager();
			_whisperModelSize = settings.WhisperModelSize; // for ex: "base", "small", "large"
			_audioLanguage = settings.AudioLanguage;       // for ex: "uk", "en", "auto"
			_asrEngine = settings.AsrEngine;
		}

		public string AsrEngineLocation => _asrEngine == "whisper.cpp" ? _whisperCppExePath : _scriptPath;

		/// <summary>
		/// Main function. Decides to call direct transcription 
		/// or segmentation based on file duration.
		/// </summary>
		public TranscriptionResult Transcribe(string audioPath)
		{
			if (!File.Exists(audioPath))
				throw new FileNotFoundException("Audio file not found.", audioPath);

			TranscriptionStarted?.Invoke(this, EventArgs.Empty);
			_logger.LogInfo($"Starting transcription: {audioPath} | Engine: {_asrEngine} | Model: {_whisperModelSize}");

			// Тут можна додати перевірку тривалості для виклику TranscribeLongFile
			return RunCommand(audioPath, _asrEngine);
		}

		/// <summary>
		/// Segmentation logic (FFmpeg -> Chunks -> Transcribe -> Merge)
		/// </summary>
		public TranscriptionResult TranscribeLongFile(string sourceFile, double totalDurationSec)
		{
			_logger.LogInfo($"Large file detected ({totalDurationSec}s). Starting segmentation...");

			string tempFolder = Path.Combine(Path.GetTempPath(), "SpeechMaster_Chunks", Guid.NewGuid().ToString());
			Directory.CreateDirectory(tempFolder);

			var combinedResult = new TranscriptionResult("", new List<Segment>())
			{
				Status = "success",
				Text = "",
				Segments = new List<Segment>()
			};

			try
			{
				// 1. Split Audio (FFmpeg) -> 16kHz WAV
				string chunkPattern = Path.Combine(tempFolder, "chunk_%03d.wav");
				var startInfo = new ProcessStartInfo
				{
					FileName = _ffmpegPath,
					Arguments = $"-i \"{sourceFile}\" -f segment -segment_time 300 -c:a pcm_s16le -ar 16000 -ac 1 \"{chunkPattern}\"",
					UseShellExecute = false,
					CreateNoWindow = true
				};

				using (var proc = Process.Start(startInfo))
				{
					proc?.WaitForExit();
				}

				// 2. Process Chunks
				var chunks = Directory.GetFiles(tempFolder, "chunk_*.wav").OrderBy(f => f).ToList();

				for (int i = 0; i < chunks.Count; i++)
				{
					string chunkPath = chunks[i];
					StatusService.Instance.UpdateStatus($"Processing part {i + 1} of {chunks.Count}...");

					// Calling universal RunCommand method
					var chunkResult = RunCommand(chunkPath, _asrEngine);

					if (chunkResult.Status == "success")
					{
						double timeOffset = i * 300.0;
						combinedResult.Text += " " + chunkResult.Text;

						if (chunkResult.Segments != null)
						{
							foreach (var seg in chunkResult.Segments)
							{
								seg.Start += timeOffset;
								seg.End += timeOffset;
								combinedResult.Segments.Add(seg);
							}
						}
					}
					else
					{
						_logger.LogError($"Failed to transcribe chunk {i}: {chunkResult.Message}");
					}
				}
			}
			catch (Exception ex)
			{
				return new TranscriptionResult("", new List<Segment>())
				{
					Status = "error",
					Message = $"Segmentation failed: {ex.Message}"
				};
			}
			finally
			{
				if (Directory.Exists(tempFolder))
					Directory.Delete(tempFolder, true);
			}

			// Signal finish for the whole operation
			TranscriptionFinished?.Invoke(this, new TranscriptionFinishedEventArgs(combinedResult));
			return combinedResult;
		}

		// --- Core Execution Logic ---

		private TranscriptionResult RunCommand(string filePath, string engine)
		{
			// Перевіряємо, чи engine містить "whisper" (може бути "whisper.cpp" або просто "whisper" залежно від конфігу)
			if (engine.Contains("whisper") && !engine.Contains("openai"))
			{
				return RunWhisperCppProcess(filePath);
			}
			else
			{
				return RunPythonProcess(filePath);
			}
		}

		// --- Implementation: Whisper.cpp ---

		private TranscriptionResult RunWhisperCppProcess(string filePath)
		{
			// Construct path to folder (ggml-base.bin, ggml-large.bin тощо)
			string modelPath = Path.Combine(_modelsBasePath, $"ggml-{_whisperModelSize}.bin");

			if (!File.Exists(_whisperCppExePath))
				return ErrorResult($"Whisper executable not found at {_whisperCppExePath}. Please run Engine Setup.");
			if (!File.Exists(modelPath))
				return ErrorResult($"Model file not found at {modelPath}. Please run Engine Setup.");

			// -m: model, -f: file, -oj: JSON output, -l: language
			// whisper-cli.exe з прапором -oj створює файл: <filePath>.json
			string expectedJsonOutput = filePath + ".json";

			// Delete existing JSON output if any
			if (File.Exists(expectedJsonOutput)) File.Delete(expectedJsonOutput);

			string args = $"-m \"{modelPath}\" -f \"{filePath}\" -oj -l {_audioLanguage}";

			var startInfo = new ProcessStartInfo
			{
				FileName = _whisperCppExePath,
				Arguments = args,
				RedirectStandardOutput = true, // Whisper пише логи в stdout/stderr
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using (var process = new Process { StartInfo = startInfo })
			{
				try
				{
					process.Start();
					// Reading logs without blocking buffer
					string stdOut = process.StandardOutput.ReadToEnd();
					string stdErr = process.StandardError.ReadToEnd();
					_rawProcessOutput = $"STDOUT: {stdOut}\nSTDERR: {stdErr}";

					process.WaitForExit();

					if (process.ExitCode != 0)
					{
						return ErrorResult($"Whisper.cpp failed (Exit Code {process.ExitCode}). Raw output: {_rawProcessOutput}");
					}

					if (File.Exists(expectedJsonOutput))
					{
						string jsonContent = File.ReadAllText(expectedJsonOutput);
						File.Delete(expectedJsonOutput); // Cleanup
						return ParseWhisperCppJson(jsonContent);
					}
					else
					{
						return ErrorResult("Whisper.cpp finished but JSON output file is missing.");
					}
				}
				catch (Exception ex)
				{
					return ErrorResult($"Execution exception: {ex.Message}");
				}
			}
		}

		public TranscriptionResult ParseWhisperCppJson(string jsonContent)
		{
			try
			{
				// Whisper.cpp JSON output format:
				// { "transcription": [ { "from": "...", "to": "...", "text": "..." } ] }
				using (JsonDocument doc = JsonDocument.Parse(jsonContent))
				{
					var result = new TranscriptionResult("", new List<Segment>()) { Status = "success", Text = "" };
					var root = doc.RootElement;

					if (root.TryGetProperty("transcription", out JsonElement transcriptionArray))
					{
						foreach (var item in transcriptionArray.EnumerateArray())
						{
							string text = item.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";

							// Getting time stamps
							double start = ExtractTime(item, "from");
							double end = ExtractTime(item, "to");

							text = text.Trim();

							result.Text += text + " ";
							result.Segments.Add(new Segment { Start = start, End = end, Text = text });
						}
					}
					result.Text = result.Text.Trim();
					return result;
				}
			}
			catch (Exception ex)
			{
				return ErrorResult($"JSON Parsing failed: {ex.Message}");
			}
		}

		public double ExtractTime(JsonElement element, string propertyName)
		{
			if (element.TryGetProperty(propertyName, out JsonElement val))
			{
				if (val.ValueKind == JsonValueKind.Number) return val.GetDouble();
				if (val.ValueKind == JsonValueKind.String)
				{
					// Whisper CLI output format handling
					if (TimeSpan.TryParse(val.GetString(), out TimeSpan ts))
					{
						return ts.TotalSeconds;
					}
					// Fallback for string numbers like "1.5"
					if (double.TryParse(val.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sec))
					{
						return sec;
					}
				}
			}
			return 0.0;
		}

		// --- Implementation: Python (Legacy) ---

		private TranscriptionResult RunPythonProcess(string filePath)
		{
			string args = $"\"{_scriptPath}\" \"{filePath}\" \"{_whisperModelSize}\"";

			var startInfo = new ProcessStartInfo
			{
				FileName = _pythonExe,
				Arguments = args,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using (var process = new Process { StartInfo = startInfo })
			{
				try
				{
					process.Start();
					string output = process.StandardOutput.ReadToEnd();
					string error = process.StandardError.ReadToEnd();
					_rawProcessOutput = output;
					process.WaitForExit();

					return ParsePythonOutput(output, error);
				}
				catch (Exception ex)
				{
					return ErrorResult($"Python process failed: {ex.Message}");
				}
			}
		}

		private TranscriptionResult ParsePythonOutput(string output, string error)
		{
			if (string.IsNullOrWhiteSpace(output))
			{
				return ErrorResult(!string.IsNullOrWhiteSpace(error) ? $"Python error: {error}" : "No output received.");
			}

			try
			{
				var options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true,
					ReadCommentHandling = JsonCommentHandling.Skip,
					AllowTrailingCommas = true
				};
				var result = JsonSerializer.Deserialize<TranscriptionResult>(output, options);

				if (result == null) throw new JsonException("Result was null.");

				// Normalization
				if (string.Equals(result.Status, "ok", StringComparison.OrdinalIgnoreCase)) result.Status = "success";
				result.Message ??= "";
				result.Text ??= "";
				if (result.Segments == null) result.Segments = new List<Segment>();

				return result;
			}
			catch (JsonException ex)
			{
				return ErrorResult($"Invalid JSON format from Python. Error: {ex.Message}\nRaw: {output}");
			}
		}

		// --- Helpers ---

		private TranscriptionResult ErrorResult(string message)
		{
			_logger.LogError(message);
			return new TranscriptionResult("", new List<Segment>())
			{
				Status = "error",
				Message = message
			};
		}
	}
}