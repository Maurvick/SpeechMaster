using ExperimentASR.Services;
using System.Linq;
using System.Windows;

namespace ExperimentASR.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsManager _settings;

        public SettingsWindow(SettingsManager settings)
        {
            InitializeComponent();
            _settings = settings;

            // Initialize controls from settings
            comboAsrEngine.SelectedItem = comboAsrEngine.Items.Cast<System.Windows.Controls.ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Content == _settings.AsrEngine);

            comboWhisperSize.SelectedItem = comboWhisperSize.Items.Cast<System.Windows.Controls.ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Content == _settings.WhisperModelSize);
			// This all case sensitive, so xml content must match exactly
			// Checking by Tag to avoid issues with localization
			comboModelImplementation.SelectedItem = comboModelImplementation.Items
				.Cast<System.Windows.Controls.ComboBoxItem>()
				.FirstOrDefault(i => (string)i.Tag == _settings.ModelImplementation);

			// Fallback: If nothing matches (e.g. settings are corrupt), select the first one
			if (comboModelImplementation.SelectedItem == null)
				comboModelImplementation.SelectedIndex = 0;

			txtAudioLanguage.Text = _settings.AudioLanguage;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (comboAsrEngine.SelectedItem is System.Windows.Controls.ComboBoxItem asrItem)
            {
                _settings.AsrEngine = asrItem.Content.ToString() ?? _settings.AsrEngine;
            }

            if (comboWhisperSize.SelectedItem is System.Windows.Controls.ComboBoxItem sizeItem)
            {
                _settings.WhisperModelSize = sizeItem.Content.ToString() ?? _settings.WhisperModelSize;
            }

			if (comboModelImplementation.SelectedItem is System.Windows.Controls.ComboBoxItem implementItem)
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

            // Update UI immediately
            comboAsrEngine.SelectedItem = comboAsrEngine.Items.Cast<System.Windows.Controls.ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Content == _settings.AsrEngine);

            comboWhisperSize.SelectedItem = comboWhisperSize.Items.Cast<System.Windows.Controls.ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Content == _settings.WhisperModelSize);

            comboModelImplementation.SelectedItem = comboModelImplementation.Items.Cast<System.Windows.Controls.ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Content == _settings.ModelImplementation);

			txtAudioLanguage.Text = _settings.AudioLanguage;
        }
    }
}