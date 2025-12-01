using ExperimentASR.Models;
using System.IO;

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
        // todo: support flags and parameters
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

                    var _logParser = new LogParser();
                    var result = _logParser.Parse(output, error);

                    // Signal finish (success or domain result)
                    TranscriptionFinished?.Invoke(this, new TranscriptionFinishedEventArgs(result));
                    return result;
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
            }
        
        }

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
            return RunCommand(audioPath, _whisperModelSize, _audioLanguage);
        }
    }
}
