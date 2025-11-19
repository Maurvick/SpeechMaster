using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExperimentASR.Services
{
    public class Transcriber
    {
        private readonly string _pythonExe;
        private readonly string _scriptPath;

        public Transcriber(string pythonExe = "python", string scriptPath = "asr.py")
        {
            _pythonExe = pythonExe;
            _scriptPath = scriptPath;
        }

        public string Transcribe(string audioPath)
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
                Arguments = $"\"{_scriptPath}\" \"{audioPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
            {
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"Error during transcription: {error}");
                }
                return output.Trim();
            }
        }
    }
}
