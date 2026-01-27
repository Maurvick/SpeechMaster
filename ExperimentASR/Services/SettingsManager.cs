using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeechMaster.Services
{
    public class SettingsManager
    {
        private const string AppFolderName = "ExperimentASR";
        private const string SettingsFileName = "settings.json";

        private readonly string _settingsFilePath;

        private readonly Logger _logger = new();

        public event EventHandler? SettingsChanged;

		// TODO: This needs to be rewrited too many hardcoded strings
		public SettingsManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, AppFolderName);
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, SettingsFileName);

            // Initialize with defaults
            _modelSize = "base";
            _audioLanguage = "auto";
            _asrEngine = "whisper";
            _modelImplementation = "native";
		}

        // Backing fields (mutable)
        private string _modelSize;
        private string _audioLanguage;
        private string _asrEngine;
        private string _modelImplementation;

		// Public properties (read/write)
		public string WhisperModelSize
        {
            get => _modelSize;
            set
            {
                if (value == _modelSize) return;
                _modelSize = value;
                OnSettingsChanged();
            }
        }

        public string AudioLanguage
        {
            get => _audioLanguage;
            set
            {
                if (value == _audioLanguage) return;
                _audioLanguage = value;
                OnSettingsChanged();
            }
        }

        public string AsrEngine
        {
            get => _asrEngine;
            set
            {
                if (value == _asrEngine) return;
                _asrEngine = value;
                OnSettingsChanged();
            }
        }

        public string ModelImplementation
        {
            get => _modelImplementation;
            set
            {
                if (value == _modelImplementation) return;
                _modelImplementation = value;
                OnSettingsChanged();
            }
		}

		private void OnSettingsChanged()
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        // Persisted model used for JSON serialization
        private class PersistedSettings
        {
            [JsonPropertyName("whisperModelSize")]
            public string WhisperModelSize { get; set; } = "base";

            [JsonPropertyName("audioLanguage")]
            public string AudioLanguage { get; set; } = "auto";

            [JsonPropertyName("asrEngine")]
            public string AsrEngine { get; set; } = "whisper";

			[JsonPropertyName("modelImplementation")]
			public string ModelImplementation { get; set; } = "native";
		}

        public void LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _logger.LogInfo("Settings file not found. Using default settings.");
                    File.Create(_settingsFilePath).Dispose();
                }    

                var json = File.ReadAllText(_settingsFilePath);
                var persisted = JsonSerializer.Deserialize<PersistedSettings>(json);
                if (persisted == null) return;

                _modelSize = persisted.WhisperModelSize ?? _modelSize;
                _audioLanguage = persisted.AudioLanguage ?? _audioLanguage;
                _asrEngine = persisted.AsrEngine ?? _asrEngine;
                _modelImplementation = persisted.ModelImplementation ?? _modelImplementation;

				_logger.LogInfo("Settings loaded: " +
                    $"ModelSize={_modelSize}, AudioLanguage={_audioLanguage}, " +
                    $"AsrEngine={_asrEngine}, ModelImplementation={_modelImplementation}");

                OnSettingsChanged();
            }
            catch
            {
                // Ignore errors — keep defaults
            }
        }

        public void SaveSettings()
        {
            try
            {
                var persisted = new PersistedSettings
                {
                    WhisperModelSize = _modelSize,
                    AudioLanguage = _audioLanguage,
                    AsrEngine = _asrEngine,
					ModelImplementation = _modelImplementation
				};

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(persisted, options);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // For simplicity, swallow exceptions (could surface to UI later)
            }
        }

        public void ResetToDefaults()
        {
            _modelSize = "base";
            _audioLanguage = "auto";
            _asrEngine = "whisper";
            _modelImplementation = "native";
			OnSettingsChanged();
            SaveSettings();
        }
    }
}
