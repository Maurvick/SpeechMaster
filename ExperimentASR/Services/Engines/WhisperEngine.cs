using System.Diagnostics;

namespace ExperimentASR.Models
{
    public class WhisperEngine : AsrEngine
    {
        private readonly string _modelPath;
        public WhisperEngine(string modelPath = "models/ggml-large-v3-turbo.bin")
        {
            Name = "Whisper large-v3-turbo (whisper.cpp)";
            SupportsGpu = true;
            _modelPath = modelPath;
        }

        public override async Task<string> TranscribeAsync(string audioPath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "whisper.cpp/main.exe",
                    Arguments = $"-m {_modelPath} -f \"{audioPath}\" --output-txt --no-timestamps",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

			// whisper.cpp parse ouptput
			return output.Trim();
        }
    }
}
