using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ExperimentASR.Services
{
	public class EngineSetupService
	{
		// 1. ENGINE: Whisper.dll (GitHub)
		private const string EngineDownloadUrl = "https://github.com/ggml-org/whisper.cpp/releases/download/v1.8.2/whisper-bin-x64.zip";

		// 2. MODEL: ggml-base.bin (HuggingFace)
		// We MUST separate this from the engine folder logic. Models are standalone files.
		private const string ModelBaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";
		private const string DefaultModelName = "ggml-base.bin";

		private readonly string _baseDir;
		private readonly HttpClient _httpClient;

		public EngineSetupService()
		{
			_baseDir = AppDomain.CurrentDomain.BaseDirectory;
			_httpClient = new HttpClient();
			// User-Agent is often required by GitHub API/Downloads to prevent 403 errors
			_httpClient.DefaultRequestHeaders.Add("User-Agent", "ExperimentASR-Downloader");
		}

		public bool IsEngineInstalled()
		{
			string dllPath = Path.Combine(_baseDir, "whisper.dll");
			string modelPath = Path.Combine(_baseDir, "Models", DefaultModelName);
			return File.Exists(dllPath) && File.Exists(modelPath);
		}

		public async Task EnsureEngineExistsAsync()
		{
			string dllPath = Path.Combine(_baseDir, "whisper.dll");

			if (File.Exists(dllPath))
			{
				StatusService.Instance.UpdateStatus("Engine integrity check: OK");
				return;
			}

			string zipPath = Path.Combine(_baseDir, "engine_temp.zip");

			try
			{
				StatusService.Instance.SetProgress(25);
				StatusService.Instance.UpdateStatus("Downloading Whisper Engine from GitHub...");

				// 1. Download the Zip
				await DownloadFileAsync(EngineDownloadUrl, zipPath);
				StatusService.Instance.SetProgress(50);

				// 2. Extract specific file from the specific folder structure
				StatusService.Instance.UpdateStatus("Extracting whisper.dll...");

				// The structure inside zip is: whisper-bin-x64/release/whisper.dll
				ExtractDllFromZip(zipPath, "whisper.dll", dllPath);

				StatusService.Instance.SetProgress(75);
				StatusService.Instance.UpdateStatus("Engine installed successfully.");
			}
			catch (Exception ex)
			{
				StatusService.Instance.UpdateStatus($"Engine Setup Failed: {ex.Message}");
				throw;
			}
			finally
			{
				// Always clean up the zip
				if (File.Exists(zipPath)) File.Delete(zipPath);
			}
		}

		public async Task EnsureModelExistsAsync(string modelName = DefaultModelName)
		{
			string modelFolder = Path.Combine(_baseDir, "Models");
			string modelPath = Path.Combine(modelFolder, modelName);

			if (!Directory.Exists(modelFolder)) Directory.CreateDirectory(modelFolder);

			if (File.Exists(modelPath))
			{
				StatusService.Instance.UpdateStatus("Model check: OK");
				return;
			}

			// Correct URL construction: Base + Filename (e.g. "ggml-base.bin")
			string url = $"{ModelBaseUrl}{modelName}";

			StatusService.Instance.SetProgress(85);
			StatusService.Instance.UpdateStatus($"Downloading Model: {modelName}...");

			try
			{
				await DownloadFileAsync(url, modelPath);
				StatusService.Instance.SetProgress(100);
				StatusService.Instance.UpdateStatus($"Model {modelName} ready.");
			}
			catch (Exception ex)
			{
				// Clean up partial file if download failed
				if (File.Exists(modelPath)) File.Delete(modelPath);
				StatusService.Instance.UpdateStatus($"Model download error: {ex.Message}");
				throw;
			}
		}

		private async Task DownloadFileAsync(string url, string destinationPath)
		{
			using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
			{
				if (!response.IsSuccessStatusCode)
				{
					throw new HttpRequestException($"Download failed. Status: {response.StatusCode}, URL: {url}");
				}

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
							double progress = Math.Round((double)totalRead / totalBytes * 100, 0);
							// Reduce UI updates to every 100KB to prevent lag
							if (totalRead % (1024 * 100) == 0)
							{
								StatusService.Instance.UpdateStatus($"Downloading... {progress}%");
								StatusService.Instance.SetProgress(progress);
							}
						}
					}
				}
			}
		}

		private void ExtractDllFromZip(string zipPath, string targetFileName, string destinationPath)
		{
			using (ZipArchive archive = ZipFile.OpenRead(zipPath))
			{
				// We search for the file ending with "whisper.dll" to handle the folder structure:
				// "whisper-bin-x64/release/whisper.dll"
				var entry = archive.Entries.FirstOrDefault(e =>
					e.FullName.EndsWith(targetFileName, StringComparison.OrdinalIgnoreCase));

				if (entry != null)
				{
					// Extract to the root application folder (destinationPath)
					entry.ExtractToFile(destinationPath, overwrite: true);
				}
				else
				{
					// Debugging info if file not found
					var structure = string.Join("\n", archive.Entries.Select(e => e.FullName));
					throw new FileNotFoundException($"Could not find '{targetFileName}' in zip.\nContents:\n{structure}");
				}
			}
		}
	}
}