using ExperimentASR.Models;
using System.IO;

namespace ExperimentASR.Services
{
    public class TranscribeService
    {
        private readonly string _pythonExe = "python";
        private readonly string _scriptPath = "./Scripts/asr_engine.py";
        private readonly string _audioLanguage;
        private readonly string _whisperModelSize;
        private string _rawPythonOutput;

        LogParser _logParser = new LogParser();

        // Events
        public event EventHandler? TranscriptionStarted;
        public event EventHandler<TranscriptionFinishedEventArgs>? TranscriptionFinished;

        public TranscribeService(string audioLanguage = "none", string whisperModelSize = "tiny")
        {
            _audioLanguage = audioLanguage;
            _whisperModelSize = whisperModelSize;
        }

        public string AsrEngineLocation
        {
            get { return _scriptPath; }
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

            // Signal start
            TranscriptionStarted?.Invoke(this, EventArgs.Empty);

            using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
            {
                try
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    _rawPythonOutput = output;
                    process.WaitForExit();

                    var result = _logParser.Parse(output, error);

                    // Signal finish (success or domain result)
                    TranscriptionFinished?.Invoke(this, new TranscriptionFinishedEventArgs(result));
                    return result;
                }
                catch (Exception ex)
                {
                    var errorResult = new TranscriptResult
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
    }

    // Event args for finished event
    public class TranscriptionFinishedEventArgs : EventArgs
    {
        public TranscriptResult Result { get; }
        public TranscriptionFinishedEventArgs(TranscriptResult result) => Result = result;
    }
}
