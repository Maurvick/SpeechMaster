using ExperimentASR.Models;
using System.IO;
using System.Text.Json;

namespace ExperimentASR.Services
{
    public class TranscribeService
    {
        private readonly string _pythonExe = "python";
        private readonly string _scriptPath = "./Scripts/asr_engine.py";
        private readonly string _whisperModelSize;
        private readonly string _audioLanguage;
        private readonly string _asrEngine;

        // Store raw output for error reporting
        private string _rawPythonOutput = "";

        FileLogger _logger = new FileLogger();

		// Path to FFmpeg (ensure this matches where you store it)
		private readonly string _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");

		// Events
		public event EventHandler? TranscriptionStarted;
        public event EventHandler<TranscriptionFinishedEventArgs>? TranscriptionFinished;

        public TranscribeService()
        {
            SettingsManager settings = new SettingsManager();
            _whisperModelSize = settings.WhisperModelSize;
            _audioLanguage = settings.AudioLanguage;
            _asrEngine = settings.AsrEngine;
        }

        public string AsrEngineLocation
        {
            get { return _scriptPath; }
        }

		private TranscriptionResult TranscribeLongFile(string sourceFile, double totalDurationSec)
		{
			_logger.LogInfo($"Large file detected ({totalDurationSec}s). Starting segmentation...");

			// 1. Create a temporary folder for chunks
			string tempFolder = Path.Combine(Path.GetTempPath(), "ExperimentASR_Chunks", Guid.NewGuid().ToString());
			Directory.CreateDirectory(tempFolder);

			var combinedResult = new TranscriptionResult
			{
				Status = "success",
				Transcript = "",
				Segments = new List<Segment>()
			};

			try
			{
				// 2. Split Audio using FFmpeg
				// -f segment: Enables splitting
				// -segment_time 300: Split every 300 seconds (5 mins)
				// -c:a pcm_s16le -ar 16000: Convert to WAV 16kHz immediately (Optimized for Whisper)
				string chunkPattern = Path.Combine(tempFolder, "chunk_%03d.wav");

				var startInfo = new System.Diagnostics.ProcessStartInfo
				{
					FileName = _ffmpegPath,
					Arguments = $"-i \"{sourceFile}\" -f segment -segment_time 300 -c:a pcm_s16le -ar 16000 -ac 1 \"{chunkPattern}\"",
					UseShellExecute = false,
					CreateNoWindow = true
				};

				using (var proc = System.Diagnostics.Process.Start(startInfo))
				{
					proc.WaitForExit();
				}

				// 3. Process Chunks Sequentially
				var chunks = Directory.GetFiles(tempFolder, "chunk_*.wav").OrderBy(f => f).ToList();

				for (int i = 0; i < chunks.Count; i++)
				{
					string chunkPath = chunks[i];

					// Notify UI about specific chunk progress
					// We calculate a global percentage: (ChunkIndex / TotalChunks) * 100
					double globalProgress = ((double)i / chunks.Count) * 100;
					// You might need to expose an event or use your StatusService here
					// StatusService.Instance.SetProgress(globalProgress); 
					StatusService.Instance.UpdateStatus($"Processing part {i + 1} of {chunks.Count}...");

					// Transcribe this specific chunk
					var chunkResult = RunCommand(chunkPath, _asrEngine);

					if (chunkResult.Status == "success")
					{
						// 4. MERGE LOGIC
						// Calculate the time offset for this chunk (e.g., Chunk 2 starts at 300s)
						double timeOffset = i * 300.0;

						// Append text
						combinedResult.Transcript += " " + chunkResult.Transcript;

						// Append Segments with adjusted timestamps
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
				return new TranscriptionResult
				{
					Status = "error",
					Message = $"Segmentation failed: {ex.Message}"
				};
			}
			finally
			{
				// 5. Cleanup Temp Files
				if (Directory.Exists(tempFolder))
					Directory.Delete(tempFolder, true);
			}

			return combinedResult;
		}
    }

        private TranscriptionResult ParseOutput(string output, string error)
        {
            // 1. Handle cases where the script crashed or produced no output
            if (string.IsNullOrWhiteSpace(output))
            {
                return new TranscriptionResult
                {
                    Status = "error",
                    Message = !string.IsNullOrWhiteSpace(error)
                        ? $"Python error: {error}"
                        : "No output received from process."
                };
            }

            try
            {
                // 2. Configure options to be forgiving with casing (e.g., "Status" vs "status")
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                // 3. Deserialize directly. This replaces the manual "TryGetProperty" checks.
                var result = JsonSerializer.Deserialize<TranscriptionResult>(output, options);

                if (result == null)
                {
                    throw new JsonException("Result was null.");
                }

                // 4. Normalize the data (Business Logic)

                // Map "ok" to "success" to match your previous logic
                if (string.Equals(result.Status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    result.Status = "success";
                }

                // Ensure fields are not null
                result.Message ??= "";
                result.Transcript ??= "";

                // If deserialization worked but status is missing, flag it
                if (string.IsNullOrEmpty(result.Status))
                {
                    result.Status = "error";
                    result.Message = "Parsed JSON but found no 'status' field.";
                }

                return result;
            }
            catch (JsonException ex)
            {
                // 5. Handle Malformed JSON
                return new TranscriptionResult
                {
                    Status = "error",
                    Message = $"Invalid JSON format. Error: {ex.Message}\nRaw output: {output}"
                };
            }
        }

        // Basic usage
        private TranscriptionResult RunCommand(string filePath)
        {
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _pythonExe,
                Arguments = $"\"{_scriptPath}\" \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
            {
                try
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    _rawPythonOutput = output;
                    process.WaitForExit();
                    var result = ParseOutput(output, error);
                    // Signal finish (success or domain result)
                    TranscriptionFinished?.Invoke(this, new TranscriptionFinishedEventArgs(result));
                }
                catch (Exception ex)
                {
                    var errorResult = new TranscriptionResult
                    {
                        Status = "error",
                        Message = $"Failed to start Python process: {ex.Message}. \nRaw python script output: {_rawPythonOutput}"
                    };
                    // Signal finish with error
                    TranscriptionFinished ?.Invoke(this, new TranscriptionFinishedEventArgs(errorResult));
                    return errorResult;
                }
                return new TranscriptionResult
                {
                    Status = "success",
                    Message = "Transcription completed."
                };
            }
        }

        private TranscriptionResult RunCommand(string filePath, string model)
        {
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _pythonExe,
                Arguments = $"\"{_scriptPath}\" \"{filePath}\" \"{model}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
            {
                try
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    _rawPythonOutput = output;
                    process.WaitForExit();
                    var result = ParseOutput(output, error);
                    // Signal finish (success or domain result)
                    TranscriptionFinished ?.Invoke(this, new TranscriptionFinishedEventArgs(result));
                }
                catch (Exception ex)
                {
                    var errorResult = new TranscriptionResult
                    {
                        Status = "error",
                        Message = $"Failed to start Python process: {ex.Message}. \nRaw python script output: {_rawPythonOutput}"
                    };
                    // Signal finish with error
                    TranscriptionFinished?. Invoke(this, new TranscriptionFinishedEventArgs(errorResult));
                    return errorResult;
                }
                _logger.LogInfo($"Transcription completed for file: {filePath} using model: {model}");
                return new TranscriptionResult
                {
                    Status = "success",
                    Message = "Transcription completed."
                };
            }
        }

        private TranscriptionResult RunCommand(string filePath, string model, 
            string size, string language)
        {
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _pythonExe,
                Arguments = $"\"{_scriptPath}\" \"{filePath}\" \"{model}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
            {
                try
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    _rawPythonOutput = output;
                    process.WaitForExit();

                    var result = ParseOutput(output, error);

                    // Signal finish (success or domain result)
                    TranscriptionFinished?.Invoke(this, new TranscriptionFinishedEventArgs(result));
                }
                catch (Exception ex)
                {
                    var errorResult = new TranscriptionResult
                    {
                        Status = "error",
                        Message = $"Failed to start Python process: {ex.Message}. \nRaw python script output: {_rawPythonOutput}"
                    };

                    // Signal finish with error
                    TranscriptionFinished?.Invoke(this, new TranscriptionFinishedEventArgs(errorResult));
                    return errorResult;
                }
                return new TranscriptionResult
                {
                    Status = "success",
                    Message = "Transcription completed."
                };
            }
        }
        
        // TODO: I need to make better CLI interface for testing.
        // Also, make CLI message about current working asr models
        // so it will be easier to extend it with other models
        public TranscriptionResult Transcribe(string audioPath)
        {
            if (!File.Exists(audioPath))
            {
                throw new FileNotFoundException("Audio file not found.", audioPath);
            }
            if (!File.Exists(_scriptPath))
            {
                throw new FileNotFoundException("asr.py not found.", _scriptPath);
            }
            // Signal transcription start
            TranscriptionStarted?.Invoke(this, EventArgs.Empty);
            _logger.LogInfo($"Starting transcription for file: {audioPath} using ASR Engine: {_asrEngine}");
            // Start script with parameters via StartProcess
            return RunCommand(audioPath, _asrEngine);
        }
    }
}
