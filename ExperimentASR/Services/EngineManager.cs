using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SpeechMaster.Models; // Переконайтесь, що тут є ваші моделі статусів
using SpeechMaster.Services.Engines;

namespace SpeechMaster.Services
{
	// 1. Додаємо перелік доступних моделей
	public enum WhisperModelType
	{
		Tiny,
		Base,
		Small,
		Medium
	}

	public class EngineManager
	{
		private const string RepoApiUrl = "https://api.github.com/repos/ggerganov/whisper.cpp/releases/latest";
		// Використовуємо huggingface для прямих посилань на .bin файли
		private const string ModelBaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

		private readonly string _baseDir;
		private readonly HttpClient _httpClient;
		private readonly string _toolsDir;
		private readonly string _whisperExePath;
		private readonly string _modelsDir;

		// 2. Словник для мапінгу Enum -> Назва файлу
		private readonly Dictionary<WhisperModelType, string> _modelFileNames = new()
		{
			{ WhisperModelType.Tiny, "ggml-tiny.bin" },
			{ WhisperModelType.Base, "ggml-base.bin" },
			{ WhisperModelType.Small, "ggml-small.bin" },
			{ WhisperModelType.Medium, "ggml-medium.bin" }
		};

		private List<AsrEngine> _engines = new();

		public EngineManager() : this(null, null) { }

		public EngineManager(string baseDir, HttpClient httpClient)
		{
			_engines.AddRange([new WhisperEngine()]);

			_baseDir = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
			_httpClient = httpClient ?? new HttpClient();

			if (httpClient == null)
			{
				_httpClient.DefaultRequestHeaders.Add("User-Agent", "SpeechMaster-App");
			}

			_toolsDir = Path.Combine(_baseDir, "Tools", "whisper");
			_modelsDir = Path.Combine(_baseDir, "Models"); // Виніс окремо шлях до моделей
			_whisperExePath = Path.Combine(_toolsDir, "whisper-cli.exe");
		}

		public string GetWhisperFolderPath()
		{
			return _toolsDir;
		}

		/// <summary>
		/// Checks if BOTH the Whisper engine and the default model are installed.
		/// </summary>
		public bool IsWhisperEngineInstalled()
		{
			return IsEngineInstalled();
		}

		/// <summary>
		/// Повертає повний шлях до файлу обраної моделі.
		/// </summary>
		public string GetModelPath(WhisperModelType type)
		{
			return Path.Combine(_modelsDir, _modelFileNames[type]);
		}

		/// <summary>
		/// Перевіряє, чи встановлено двигун (exe).
		/// </summary>
		public bool IsEngineInstalled()
		{
			return File.Exists(_whisperExePath);
		}

		/// <summary>
		/// Перевіряє, чи завантажено конкретну модель.
		/// </summary>
		public bool IsModelInstalled(WhisperModelType type)
		{
			return File.Exists(GetModelPath(type));
		}

		public async Task EnsureEngineExistsAsync()
		{
			if (!Directory.Exists(_toolsDir)) Directory.CreateDirectory(_toolsDir);

			if (File.Exists(_whisperExePath))
			{
				// Перевірка цілісності DLL
				var dllFiles = Directory.GetFiles(_toolsDir, "*.dll");
				if (dllFiles.Length > 0) return;
			}

			string zipPath = Path.Combine(_baseDir, "engine_temp.zip");

			try
			{
				StatusService.Instance.SetProgress(10);
				StatusService.Instance.UpdateStatus("Checking GitHub for latest version...");

				string downloadUrl = await GetLatestDownloadUrlAsync();

				StatusService.Instance.SetProgress(30);
				StatusService.Instance.UpdateStatus("Downloading Whisper Engine...");
				await DownloadFileAsync(downloadUrl, zipPath);

				StatusService.Instance.SetProgress(60);
				StatusService.Instance.UpdateStatus("Extracting engine files...");
				ExtractAllFilesFromZip(zipPath, _toolsDir);

				StatusService.Instance.SetProgress(100);
				StatusService.Instance.UpdateStatus("Engine installed successfully.");
			}
			catch (Exception ex)
			{
				StatusService.Instance.UpdateStatus($"Engine Setup Failed: {ex.Message}");
				if (File.Exists(_whisperExePath)) File.Delete(_whisperExePath);
				throw;
			}
			finally
			{
				if (File.Exists(zipPath)) File.Delete(zipPath);
			}
		}

		/// <summary>
		/// Завантажує обрану модель, якщо її немає.
		/// </summary>
		public async Task EnsureModelExistsAsync(WhisperModelType modelType)
		{
			if (!Directory.Exists(_modelsDir)) Directory.CreateDirectory(_modelsDir);

			string fileName = _modelFileNames[modelType];
			string modelPath = GetModelPath(modelType);

			if (File.Exists(modelPath))
			{
				StatusService.Instance.UpdateStatus($"Model {fileName} is ready.");
				return;
			}

			StatusService.Instance.UpdateStatus($"Downloading Model: {fileName}...");

			// Скидаємо прогрес перед завантаженням моделі
			StatusService.Instance.SetProgress(0);

			try
			{
				string url = $"{ModelBaseUrl}{fileName}";
				await DownloadFileAsync(url, modelPath);
				StatusService.Instance.UpdateStatus($"Model {fileName} downloaded.");
			}
			catch (Exception)
			{
				if (File.Exists(modelPath)) File.Delete(modelPath);
				throw;
			}
		}

		// --- Private Helpers (залишились майже без змін) ---

		private void ExtractAllFilesFromZip(string zipPath, string targetDirectory)
		{
			using (ZipArchive archive = ZipFile.OpenRead(zipPath))
			{
				foreach (var entry in archive.Entries)
				{
					if (string.IsNullOrEmpty(entry.Name)) continue;

					if (entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
						entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
					{
						string destinationPath = Path.Combine(targetDirectory, entry.Name);
						entry.ExtractToFile(destinationPath, overwrite: true);
					}
				}
			}

			if (!File.Exists(Path.Combine(targetDirectory, "whisper-cli.exe")))
			{
				throw new FileNotFoundException("Critical file 'whisper-cli.exe' not found.");
			}
		}

		private async Task<string> GetLatestDownloadUrlAsync()
		{
			try
			{
				// Додаємо User-Agent, бо GitHub API вимагає його
				if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
				{
					_httpClient.DefaultRequestHeaders.Add("User-Agent", "SpeechMaster-Client");
				}

				string jsonResponse = await _httpClient.GetStringAsync(RepoApiUrl);
				using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
				{
					if (doc.RootElement.TryGetProperty("assets", out JsonElement assets))
					{
						foreach (JsonElement asset in assets.EnumerateArray())
						{
							string name = asset.GetProperty("name").GetString() ?? "";
							// Шукаємо версію для Windows x64 (bin-x64)
							if (name.Contains("bin-x64", StringComparison.OrdinalIgnoreCase) &&
								name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
							{
								return asset.GetProperty("browser_download_url").GetString();
							}
						}
					}
				}
				throw new Exception("Could not find 'bin-x64' zip asset.");
			}
			catch (Exception ex)
			{
				throw new Exception($"GitHub API Error: {ex.Message}");
			}
		}

		private async Task DownloadFileAsync(string url, string destinationPath)
		{
			using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
			{
				if (!response.IsSuccessStatusCode)
					throw new HttpRequestException($"Download failed. Status: {response.StatusCode}");

				var totalBytes = response.Content.Headers.ContentLength ?? -1L;

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
						if (totalBytes != -1) // Оновлюємо прогрес, тільки якщо знаємо розмір
						{
							double progress = Math.Round((double)totalRead / totalBytes * 100, 0);
							StatusService.Instance.SetProgress(progress);
						}
					}
				}
			}
		}
	}
}