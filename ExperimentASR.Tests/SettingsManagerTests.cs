using System;
using System.IO;
using ExperimentASR.Services;
using Xunit;

namespace ExperimentASR.Tests
{
	namespace ExperimentASR.Tests
	{
		// Note: Since SettingsManager hardcodes the file path in the constructor,
		// these tests will interact with your real AppData folder. 
		// Ideally, pass the file path or an abstraction into the constructor to avoid this.
		public class SettingsManagerTests : IDisposable
		{
			private readonly SettingsManager _manager;
			private readonly string _settingsPath;

			public SettingsManagerTests()
			{
				_manager = new SettingsManager();

				// Reconstruct the path to clean up files after tests
				var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				_settingsPath = Path.Combine(appData, "ExperimentASR", "settings.json");
			}

			public void Dispose()
			{
				// Cleanup: Delete the settings file after each test to ensure isolation
				if (File.Exists(_settingsPath))
				{
					File.Delete(_settingsPath);
				}
			}

			[Fact]
			public void Constructor_InitializesProperties_WithNonNullValues()
			{
				// Arrange & Act
				var manager = new SettingsManager();

				// Assert - Check all properties are not null and not empty
				Assert.False(string.IsNullOrEmpty(manager.WhisperModelSize));
				Assert.False(string.IsNullOrEmpty(manager.AudioLanguage));
				Assert.False(string.IsNullOrEmpty(manager.AsrEngine));
				Assert.False(string.IsNullOrEmpty(manager.ModelImplementation));

				// Assert - Check specific defaults
				Assert.Equal("base", manager.WhisperModelSize);
				Assert.Equal("auto", manager.AudioLanguage);
			}

			[Fact]
			public void PropertySetter_UpdatesValue_AndRaisesEvent()
			{
				// Arrange
				var eventRaised = false;
				_manager.SettingsChanged += (sender, args) => eventRaised = true;

				// Act
				_manager.WhisperModelSize = "large";

				// Assert
				Assert.Equal("large", _manager.WhisperModelSize);
				Assert.True(eventRaised, "SettingsChanged event should fire when value changes.");
			}

			[Fact]
			public void PropertySetter_DoesNotRaiseEvent_IfValueIsIdentical()
			{
				// Arrange
				_manager.WhisperModelSize = "base"; // Ensure initial state
				var eventRaised = false;
				_manager.SettingsChanged += (sender, args) => eventRaised = true;

				// Act
				_manager.WhisperModelSize = "base"; // Set to same value

				// Assert
				Assert.False(eventRaised, "SettingsChanged event should NOT fire if value is unchanged.");
			}

			[Fact]
			public void LoadSettings_IgnoresNullValues_FromCorruptedJson()
			{
				// This test ensures that if the JSON file has nulls, 
				// the manager keeps the existing valid values (Does not become null).

				// Arrange
				// Create a JSON file where fields are explicitly null
				string jsonWithNulls = @"{
                ""whisperModelSize"": null,
                ""audioLanguage"": null,
                ""asrEngine"": ""vosk"", 
                ""modelImplementation"": null
            }";

				// Create directory if it doesn't exist (because Dispose might have deleted it)
				Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
				File.WriteAllText(_settingsPath, jsonWithNulls);

				// Set current memory values to something known
				_manager.WhisperModelSize = "medium";
				_manager.AudioLanguage = "en";

				// Act
				_manager.LoadSettings();

				// Assert
				// 1. Should NOT be null (ignored the null from JSON)
				// 2. Should retain the value from memory ("medium")
				Assert.NotNull(_manager.WhisperModelSize);
				Assert.Equal("medium", _manager.WhisperModelSize);

				// 3. Should update valid values from JSON ("vosk")
				Assert.Equal("vosk", _manager.AsrEngine);
			}

			[Fact]
			public void ResetToDefaults_RestoresNonNullDefaults()
			{
				// Arrange
				_manager.WhisperModelSize = "garbage_value";
				_manager.AudioLanguage = "fr";

				// Act
				_manager.ResetToDefaults();

				// Assert
				Assert.Equal("base", _manager.WhisperModelSize);
				Assert.Equal("auto", _manager.AudioLanguage);
				Assert.Equal("whisper", _manager.AsrEngine);
			}
		}
	}
}
