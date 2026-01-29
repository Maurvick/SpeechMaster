using SpeechMaster.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ExperimentASR.Views
{
	public partial class SettingsWindow : Window
	{
		private readonly SettingsManager _settings;
		private readonly EngineManager _setupService = new();

		public SettingsWindow(SettingsManager settings)
		{
			InitializeComponent();
			_settings = settings;

			// 1. Ініціалізація ComboBox для ASR Engine
			comboAsrEngine.SelectedItem = comboAsrEngine.Items.Cast<ComboBoxItem>()
				.FirstOrDefault(i => (string)i.Content == _settings.AsrEngine);

			// 2. Ініціалізація ComboBox для Whisper Model Size (Robust check)
			// Шукаємо елемент, ігноруючи регістр (Base == base)
			СomboWhisperSize.SelectedItem = СomboWhisperSize.Items.Cast<ComboBoxItem>()
				.FirstOrDefault(i => string.Equals(i.Content.ToString(), _settings.WhisperModelSize, StringComparison.OrdinalIgnoreCase));

			// 3. Ініціалізація Implementation (Python/C++)
			comboModelImplementation.SelectedItem = comboModelImplementation.Items
				.Cast<ComboBoxItem>()
				.FirstOrDefault(i => (string)i.Tag == _settings.ModelImplementation);

			// Fallback
			if (comboModelImplementation.SelectedItem == null)
				comboModelImplementation.SelectedIndex = 0;

			txtAudioLanguage.Text = _settings.AudioLanguage;

			// 4. Перевірка наявності Whisper DLL
			CheckWhisperInstallation();

			// 5. Перевірка наявності моделей (оновлено)
			CheckInstalledModels();
		}

		private void CheckWhisperInstallation()
		{
			if (_setupService.IsEngineInstalled()) // Метод перейменовано в попередніх кроках
			{
				LblWhisperVersion.Text = "Whisper DLL: Installed";
				BtnOpenWhisperFolder.Visibility = Visibility.Visible;
			}
			else
			{
				LblWhisperVersion.Text = "Whisper DLL: Missing";
				BtnOpenWhisperFolder.Visibility = Visibility.Hidden;
			}
		}

		private void CheckInstalledModels()
		{
			// Допоміжна функція для активації пунктів меню
			void EnableIfInstalled(string modelName, WhisperModelType type)
			{
				var item = СomboWhisperSize.Items.Cast<ComboBoxItem>()
					.FirstOrDefault(i => string.Equals(i.Content.ToString(), modelName, StringComparison.OrdinalIgnoreCase));

				if (item != null)
				{
					// Якщо модель є на диску - додаємо позначку або просто логуємо
					// У цьому вікні ми зазвичай не блокуємо вибір (IsEnabled = false), 
					// бо користувач може захотіти вибрати модель, щоб вона скачалась автоматично при старті.
					// Але якщо ви хочете візуально виділити встановлені:

					bool isInstalled = _setupService.IsModelInstalled(type);
					if (isInstalled)
					{
						item.Content = $"{modelName} (Ready)";
					}
					else
					{
						item.Content = modelName; // Скидаємо назву, якщо раптом ні
					}
				}
			}

			// Перевіряємо всі типи
			EnableIfInstalled("tiny", WhisperModelType.Tiny);
			EnableIfInstalled("base", WhisperModelType.Base);   // Додано Base
			EnableIfInstalled("small", WhisperModelType.Small);
			EnableIfInstalled("medium", WhisperModelType.Medium);
		}

		private void ComboWhisperSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (СomboWhisperSize.SelectedItem is ComboBoxItem selectedItem)
			{
				// Очищаємо назву від "(Ready)" для перевірки
				string modelName = selectedItem.Content.ToString().Replace(" (Ready)", "").Trim();

				if (Enum.TryParse(modelName, true, out WhisperModelType modelType))
				{
					// Перевіряємо, чи є файл
					bool isInstalled = _setupService.IsModelInstalled(modelType);

					// Якщо НЕ встановлено -> показуємо кнопку Download
					// Якщо встановлено -> ховаємо
					BtnDownloadWhisperModel.Visibility = isInstalled ? Visibility.Collapsed : Visibility.Visible;

					// Якщо встановлено, міняємо текст кнопки на "Re-download" (опціонально) або ховаємо
				}
			}
		}
		private void BtnSave_Click(object sender, RoutedEventArgs e)
		{
			if (comboAsrEngine.SelectedItem is ComboBoxItem asrItem)
			{
				_settings.AsrEngine = asrItem.Content.ToString() ?? _settings.AsrEngine;
			}

			if (СomboWhisperSize.SelectedItem is ComboBoxItem sizeItem)
			{
				// Важливо: зберігаємо чисту назву без приписки "(Ready)", якщо ми її додали вище
				string rawContent = sizeItem.Content.ToString().Replace(" (Ready)", "").Trim();
				// Зберігаємо в нижньому регістрі для сумісності з Enum.TryParse
				_settings.WhisperModelSize = rawContent.ToLower();
			}

			if (comboModelImplementation.SelectedItem is ComboBoxItem implementItem)
			{
				_settings.ModelImplementation = implementItem.Content.ToString() ?? _settings.ModelImplementation;
			}

			_settings.AudioLanguage = string.IsNullOrWhiteSpace(txtAudioLanguage.Text) ? "auto" : txtAudioLanguage.Text.Trim();

			_settings.SaveSettings();

			DialogResult = true;
			Close();
		}

		private void BtnCancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void BtnReset_Click(object sender, RoutedEventArgs e)
		{
			_settings.ResetToDefaults();

			// Оновлюємо UI
			// Для Model Size скидаємо вибір на Base (або дефолт)
			СomboWhisperSize.SelectedIndex = 1; // Зазвичай Base це індекс 1, перевірте XAML

			txtAudioLanguage.Text = _settings.AudioLanguage;

			// Перезапускаємо перевірку відображення (Ready/Not Ready)
			CheckInstalledModels();
		}

		private void BtnOpenWhisperFolder_Click(object sender, RoutedEventArgs e)
		{
			// Використовуємо метод, який ми повернули в EngineManager
			string path = _setupService.GetWhisperFolderPath();
			if (System.IO.Directory.Exists(path))
			{
				Process.Start("explorer.exe", path);
			}
			else
			{
				MessageBox.Show("Folder does not exist yet.", "Info");
			}
		}

		private void BtnOpenVoskFolder_Click(object sender, RoutedEventArgs e)
		{
			// Open the folder where vosk models are stored
		}

		private async void BtnDownloadWhisperModel_Click(object sender, RoutedEventArgs e)
		{
			// Логіка ручного завантаження моделі
			if (СomboWhisperSize.SelectedItem is ComboBoxItem selectedItem)
			{
				string modelName = selectedItem.Content.ToString().Replace(" (Ready)", "").Trim();

				if (Enum.TryParse(modelName, true, out WhisperModelType modelType))
				{
					IsEnabled = false; // Блокуємо UI
					try
					{
						await _setupService.EnsureModelExistsAsync(modelType);
						MessageBox.Show($"Model {modelName} downloaded successfully!", "Success");
						CheckInstalledModels(); // Оновлюємо UI (додаємо напис Ready)
					}
					catch (Exception ex)
					{
						MessageBox.Show($"Download failed: {ex.Message}", "Error");
					}
					finally
					{
						IsEnabled = true;
					}
				}
			}
		}

		private void BtnDownloadVoskModel_Click(object sender, RoutedEventArgs e)
		{
			// Open the Vosk model download page
		}
	}
}