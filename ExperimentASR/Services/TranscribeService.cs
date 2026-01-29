using SpeechMaster.Models.Transcription;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpeechMaster.Services
{
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
		private readonly string _audioLanguage;
		private readonly string _asrEngine;

		// --- Diagnostics ---
		private string _rawProcessOutput = "";
		private readonly string _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");

		// --- Events ---
		public event EventHandler? TranscriptionStarted;
		public event EventHandler<TranscriptionFinishedEventArgs>? TranscriptionFinished;

		public TranscribeService()
		{
			SettingsManager settings = new SettingsManager();
			// _whisperModelSize більше не читаємо тут
			_audioLanguage = settings.AudioLanguage;
			_asrEngine = settings.AsrEngine;
		}

		public string AsrEngineLocation => _asrEngine == "whisper.cpp" ? _whisperCppExePath : _scriptPath;

		/// <summary>
		/// Main function. Тепер приймає modelType.
		/// </summary>
		/// 
		public TranscriptionResult Transcribe(string audioPath, WhisperModelType modelType)
		{
			if (!File.Exists(audioPath))
				throw new FileNotFoundException("Audio file not found.", audioPath);

			TranscriptionStarted?.Invoke(this, EventArgs.Empty);
			_logger.LogInfo($"Starting transcription: {audioPath} | Engine: {_asrEngine} | Model: {modelType}");

			// Передаємо модель далі
			return RunCommand(audioPath, _asrEngine, modelType);
		}

		public virtual TranscriptionResult Transcribe(string audioPath)
		{
			throw new NotImplementedException("Use Transcribe with modelType parameter.");
		}

		/// <summary>
		/// Segmentation logic. Тепер прокидує modelType у кожен шматок.
		/// </summary>
		public TranscriptionResult TranscribeLongFile(string sourceFile, double totalDurationSec, WhisperModelType modelType)
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
				// 1. Split Audio (FFmpeg)
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
					StatusService.Instance.UpdateStatus($"Processing part {i + 1} of {chunks.Count} using {modelType}...");

					// ВАЖЛИВО: Передаємо обрану модель для обробки кожного шматка
					var chunkResult = RunCommand(chunkPath, _asrEngine, modelType);

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

			TranscriptionFinished?.Invoke(this, new TranscriptionFinishedEventArgs(combinedResult));
			return combinedResult;
		}

		// --- Core Execution Logic ---

		private TranscriptionResult RunCommand(string filePath, string engine, WhisperModelType modelType)
		{
			if (engine.Contains("whisper") && !engine.Contains("openai"))
			{
				return RunWhisperCppProcess(filePath, modelType);
			}
			else
			{
				return RunPythonProcess(filePath, modelType);
			}
		}

		// --- Implementation: Whisper.cpp ---

		internal TranscriptionResult RunWhisperCppProcess(string filePath, WhisperModelType modelType)
		{
			// 1. Формуємо ім'я файлу на основі Enum
			string modelFileName = modelType switch
			{
				WhisperModelType.Tiny => "ggml-tiny.bin",
				WhisperModelType.Base => "ggml-base.bin",
				WhisperModelType.Small => "ggml-small.bin",
				WhisperModelType.Medium => "ggml-medium.bin",
				_ => "ggml-base.bin"
			};

			string modelPath = Path.Combine(_modelsBasePath, modelFileName);

			if (!File.Exists(_whisperCppExePath))
				return ErrorResult($"Whisper executable not found at {_whisperCppExePath}.");

			// Перевіряємо наявність саме тієї моделі, яку обрали
			if (!File.Exists(modelPath))
				return ErrorResult($"Model file not found at {modelPath}. Please download the {modelType} model first.");

			string expectedJsonOutput = filePath + ".json";
			if (File.Exists(expectedJsonOutput)) File.Delete(expectedJsonOutput);

			string args = $"-m \"{modelPath}\" -f \"{filePath}\" -oj -l {_audioLanguage}";

			var startInfo = new ProcessStartInfo
			{
				FileName = _whisperCppExePath,
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
						File.Delete(expectedJsonOutput);
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
				using (JsonDocument doc = JsonDocument.Parse(jsonContent))
				{
					var result = new TranscriptionResult("", new List<Segment>()) { Status = "success", Text = "" };
					var root = doc.RootElement;

					if (root.TryGetProperty("transcription", out JsonElement transcriptionArray))
					{
						foreach (var item in transcriptionArray.EnumerateArray())
						{
							string text = item.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
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
			// ... (Без змін) ...
			if (element.TryGetProperty(propertyName, out JsonElement val))
			{
				if (val.ValueKind == JsonValueKind.Number) return val.GetDouble();
				if (val.ValueKind == JsonValueKind.String)
				{
					if (TimeSpan.TryParse(val.GetString(), out TimeSpan ts)) return ts.TotalSeconds;
					if (double.TryParse(val.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sec)) return sec;
				}
			}
			return 0.0;
		}

		// --- Implementation: Python (Legacy) ---

		private TranscriptionResult RunPythonProcess(string filePath, WhisperModelType modelType)
		{
			// Перетворюємо Enum в рядок ("base", "small") для Python скрипта
			string modelSizeStr = modelType.ToString().ToLower();

			string args = $"\"{_scriptPath}\" \"{filePath}\" \"{modelSizeStr}\"";

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
			// ... (Без змін) ...
			if (string.IsNullOrWhiteSpace(output))
				return ErrorResult(!string.IsNullOrWhiteSpace(error) ? $"Python error: {error}" : "No output received.");

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

		private TranscriptionResult ErrorResult(string message)
		{
			// ... (Без змін) ...
			_logger.LogError(message);
			return new TranscriptionResult("", new List<Segment>())
			{
				Status = "error",
				Message = message
			};
		}
	}
}