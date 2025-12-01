using System;
using System.Collections.Generic;
using System.Text;

namespace ExperimentASR.Services
{
    public class SettingsManager
    {
        private string _whisperModelSize = "base";

        public string WhisperModelSize
        {
            get { return _whisperModelSize; }
        }

        private string _audioLanguage = "auto";

        public string AudioLanguage
        {
            get { return _audioLanguage; }
        }

        private string _asrEngine = "whisper";

        public string AsrEngine
        {
            get { return _asrEngine; }
        }

        public SettingsManager()
        {
            // Constructor implementation
        }

        public void SaveSettings()
        {
            // Implementation for saving settings
        }

        public void LoadSettings()
        {
            // Implementation for loading settings
        }
    }
}
