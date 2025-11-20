using ExperimentASR.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace ExperimentASR.Services
{
    public class Transcriber
    {
        private readonly string _pythonExe;
        private readonly string _scriptPath;
        private readonly string _audioLanguage;
        private readonly string _whisperModelSize;

        public Transcriber(string pythonExe = "python", string scriptPath = "asr.py", 
            string audioLanguage = "en", string whisperModelSize = "tiny")
        {
            _pythonExe = pythonExe;
            _scriptPath = scriptPath;
            _audioLanguage = audioLanguage;
            _whisperModelSize = whisperModelSize;
        }

        public TranscriptResult Transcribe(string audioPath)
        {
            if (!File.Exists(audioPath))
            {
                throw new FileNotFoundException("Audio file not found.", audioPath);
            }
            if (!File.Exists(_scriptPath))
            {
                throw new FileNotFoundException("asr.py not found.", _scriptPath);
            }

            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _pythonExe,
                Arguments = $"\"{_scriptPath}\" \"{audioPath}\" \"{_audioLanguage}\"",
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
                    process.WaitForExit();

                    // If Python printed JSON, parse it
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        var doc = JsonDocument.Parse(output);

                        if (!doc.RootElement.TryGetProperty("status", out var statusEl))
                        {
                            return new TranscriptResult
                            {
                                Status = "error",
                                Message = "Failed to parse Json document. Status element not found."
                            };
                        }

                        // If status is a simple string:
                        if (statusEl.ValueKind == JsonValueKind.String)
                        {
                            string? statusValue = statusEl.GetString();
                            if (statusValue == "error")
                            {
                                string message = doc.RootElement.TryGetProperty("message", out var messageEl) && messageEl.ValueKind == JsonValueKind.String
                                    ? messageEl.GetString() ?? "Unknown error"
                                    : "Unknown error";
                                return new TranscriptResult
                                {
                                    Status = "error",
                                    Message = message
                                };
                            }
                            else if (statusValue == "info")
                            {
                                string message = doc.RootElement.TryGetProperty("message", out var messageEl) && messageEl.ValueKind == JsonValueKind.String
                                    ? messageEl.GetString() ?? ""
                                    : "";
                                return new TranscriptResult
                                {
                                    Status = "info",
                                    Message = message
                                };
                            }
                            else if (statusValue == "success" || statusValue == "ok")
                            {
                                string transcript = doc.RootElement.TryGetProperty("transcript", out var transcriptEl) && transcriptEl.ValueKind == JsonValueKind.String
                                    ? transcriptEl.GetString() ?? ""
                                    : "";
                                return new TranscriptResult
                                {
                                    Status = "success",
                                    Transcript = transcript
                                };
                            }
                        }
                        // If status is an object: enumerate keys and values
                        else if (statusEl.ValueKind == JsonValueKind.Object)
                        {
                            var sb = new StringBuilder();
                            foreach (var prop in statusEl.EnumerateObject())
                            {
                                string key = prop.Name;
                                string val = prop.Value.ValueKind == JsonValueKind.String
                                    ? prop.Value.GetString() ?? ""
                                    : prop.Value.ToString();
                                sb.AppendLine($"{key}: {val}");
                            }
                            // sb.ToString() contains keys and values from the status object
                        }

                        try
                        {
                            return JsonSerializer.Deserialize<TranscriptResult>(output);
                        }
                        catch
                        {
                            return new TranscriptResult
                            {
                                Status = "error",
                                Message = "Invalid JSON from script:\n" + output
                            };
                        }
                    }

                    // Python may have crashed before printing JSON
                    return new TranscriptResult
                    {
                        Status = "error",
                        Message = "Python error:\n" + error
                    };

                }
                catch (Exception ex)
                {
                    return new TranscriptResult
                    {
                        Status = "error",
                        Message = "Failed to start Python process: " + ex.Message
                    };
                }
            }
        }
    }
}
