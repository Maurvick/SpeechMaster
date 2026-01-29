using SpeechMaster.Models.Transcription;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechMaster.Services
{
	public class TranscriptionQueueManager
	{
		private readonly TranscribeService _transcribeService;
		private readonly EngineManager _engineManager; // Додаємо менеджер двигуна

		public ObservableCollection<TranscriptionJob> Jobs { get; set; }
			= new ObservableCollection<TranscriptionJob>();

		private CancellationTokenSource _cts;
		private bool _isProcessing = false;

		public event EventHandler? QueueProcessingStarted;
		public event EventHandler? QueueProcessingFinished;
		public TranscriptionQueueManager()
		{
			_transcribeService = new TranscribeService();
			_engineManager = new EngineManager(); // Ініціалізуємо
		}

		public void AddFile(string path)
		{
			// Перевіряємо, чи файл вже є в черзі, щоб уникнути дублікатів
			if (Jobs.Any(j => j.FilePath == path)) return;

			Jobs.Add(new TranscriptionJob
			{
				FileName = System.IO.Path.GetFileName(path),
				FilePath = path,
				Status = "Pending",
				Result = ""
			});
		}

		// Приймаємо тип моделі, обраний в UI (ComboBox)
		public async Task StartProcessing(WhisperModelType selectedModel)
		{
			if (_isProcessing) return;
			_isProcessing = true;
			_cts = new CancellationTokenSource();

			QueueProcessingStarted?.Invoke(this, EventArgs.Empty);

			try
			{
				// 1. ПЕРЕВІРКА ТА ЗАВАНТАЖЕННЯ МОДЕЛІ
				// Перед тим як почати, переконаємось, що файл моделі існує.
				// Якщо користувач обрав "Medium", а її немає — вона скачається тут.
				// Можна оновити статус першого джоба, щоб показати процес.
				if (Jobs.Any(j => j.Status == "Pending"))
				{
					var firstJob = Jobs.First(j => j.Status == "Pending");
					string oldStatus = firstJob.Status;
					firstJob.Status = $"Downloading {selectedModel} model...";

					try
					{
						await _engineManager.EnsureEngineExistsAsync();
						await _engineManager.EnsureModelExistsAsync(selectedModel);
						firstJob.Status = oldStatus; // Повертаємо статус
					}
					catch (Exception ex)
					{
						firstJob.Status = "Model Error";
						firstJob.Result = ex.Message;
						return; // Зупиняємо процес, якщо модель не вдалося скачати
					}
				}

				// 2. ОБРОБКА ЧЕРГИ
				while (Jobs.Any(j => j.Status == "Pending"))
				{
					if (_cts.Token.IsCancellationRequested) break;

					var currentJob = Jobs.First(j => j.Status == "Pending");
					currentJob.Status = "Processing...";

					// Передаємо модель у метод транскрипції
					var result = await StartTranscriptionAsync(currentJob.FilePath, selectedModel);

					if (_cts.Token.IsCancellationRequested)
					{
						currentJob.Status = "Pending";
						break;
					}

					if (result.Status == "success")
					{
						currentJob.Result = result.Text;
						currentJob.Status = "Done";
					}
					else
					{
						currentJob.Status = "Error";
						currentJob.Result = result.Message; // Показуємо помилку в полі результату або логах
					}
				}
			}
			catch (Exception ex)
			{
				// Логування критичних помилок черги
				System.Diagnostics.Debug.WriteLine($"Queue Error: {ex.Message}");
			}
			finally
			{
				_isProcessing = false;
				_cts?.Dispose();
				_cts = null;

				// 3. ВИКЛИКАЄМО ПОДІЮ ЗАВЕРШЕННЯ (навіть якщо була помилка або скасування)
				QueueProcessingFinished?.Invoke(this, EventArgs.Empty);
			}
		}

		public void CancelProcessing()
		{
			_cts?.Cancel();
		}

		private async Task<TranscriptionResult> StartTranscriptionAsync(string path, WhisperModelType modelType)
		{
			// Виконуємо транскрипцію в окремому потоці, щоб не блокувати UI.
			// Передаємо обрану модель у сервіс.
			return await Task.Run(() => _transcribeService.Transcribe(path, modelType));
		}
	}
}