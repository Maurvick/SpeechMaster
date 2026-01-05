using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace ExperimentASR.Services
{
	public class EngineSetupService
	{
		// Link to a specific stable release of whisper.cpp for Windows x64
		// You can check for newer versions here: https://github.com/ggerganov/whisper.cpp/releases
		private const string EngineDownloadUrl = "https://github.com/ggerganov/whisper.cpp/releases/download/v1.7.1/whisper-bin-x64.zip";

		// Base URL for GGML models (HuggingFace is reliable for this)
		private const string ModelBaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

		private readonly string _baseDir;
		private readonly HttpClient _httpClient;

		public EngineSetupService()
		{
			_baseDir = AppDomain.CurrentDomain.BaseDirectory;
			_httpClient = new HttpClient();
		}

        public bool IsEngineInstalled()
        {
            string dllPath = Path.Combine(_baseDir, "whisper.dll");
            string modelPath = Path.Combine(_baseDir, "Models", "ggml-base.bin");

            // Check if both the engine and at least one model exist
            return File.Exists(dllPath) && File.Exists(modelPath);
        }

        public async Task EnsureEngineExistsAsync()
		{
			string dllPath = Path.Combine(_baseDir, "whisper.dll");

			if (File.Exists(dllPath))
			{
				StatusService.Instance.Update("Engine integrity check: OK");
				return;
			}

			try
			{
				StatusService.Instance.Update("Downloading Whisper Engine (Native)...");

				string zipPath = Path.Combine(_baseDir, "engine_temp.zip");

				// 1. Download Zip
				await DownloadFileAsync(EngineDownloadUrl, zipPath);

				// 2. Extract DLL
				StatusService.Instance.Update("Extracting Engine components...");
				ExtractDllFromZip(zipPath, "whisper.dll", dllPath);

				// 3. Cleanup
				if (File.Exists(zipPath)) File.Delete(zipPath);

				StatusService.Instance.Update("Engine installed successfully.");
			}
			catch (Exception ex)
			{
				StatusService.Instance.Update($"Engine Setup Failed: {ex.Message}");
				throw; // Rethrow to stop app startup if critical
			}
		}

		public async Task EnsureModelExistsAsync(string modelName = "ggml-base.bin")
		{
			string modelPath = Path.Combine(_baseDir, "Models", modelName);
			string directory = Path.GetDirectoryName(modelPath);

			if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

			if (File.Exists(modelPath)) return;

			string url = $"{ModelBaseUrl}{modelName}";
			StatusService.Instance.Update($"Downloading Model: {modelName}...");

			await DownloadFileAsync(url, modelPath);

			StatusService.Instance.Update($"Model {modelName} ready.");
		}

		private async Task DownloadFileAsync(string url, string destinationPath)
		{
			using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
			{
				response.EnsureSuccessStatusCode();

				var totalBytes = response.Content.Headers.ContentLength ?? -1L;
				var canReportProgress = totalBytes != -1;

				using (var contentStream = await response.Content.ReadAsStreamAsync())
				using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
				{
					var buffer = new byte[8192];
					long totalRead = 0;
					int bytesRead;

					while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
					{
						await fileStream.WriteAsync(buffer, 0, bytesRead);
						totalRead += bytesRead;

						if (canReportProgress)
						{
							// Report progress every ~1MB or so to avoid spamming UI
							double progress = Math.Round((double)totalRead / totalBytes * 100, 0);
							if (totalRead % (1024 * 1024) == 0) // Update every 1MB
							{
								// Using StatusService to notify UI
								StatusService.Instance.Update($"Downloading... {progress}%");
							}
						}
					}
				}
			}
		}

		private void ExtractDllFromZip(string zipPath, string fileNameToExtract, string destinationPath)
		{
			using (ZipArchive archive = ZipFile.OpenRead(zipPath))
			{
				var entry = archive.GetEntry(fileNameToExtract);
				if (entry != null)
				{
					entry.ExtractToFile(destinationPath, overwrite: true);
				}
				else
				{
					throw new FileNotFoundException($"Could not find {fileNameToExtract} inside the downloaded zip.");
				}
			}
		}
	}
}