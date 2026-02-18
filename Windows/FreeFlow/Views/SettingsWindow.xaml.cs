using System.Windows;
using FreeFlow.Services;
using NAudio.Wave;

namespace FreeFlow.Views;

public partial class SettingsWindow : Window
{
    private readonly CredentialService _credentialService;
    private readonly SettingsService _settingsService;

    public SettingsWindow()
    {
        InitializeComponent();
        _credentialService = new CredentialService();
        _settingsService = new SettingsService();

        LoadSettings();
    }

    private void LoadSettings()
    {
        ApiKeyBox.Password = _credentialService.GetApiKey() ?? "";

        var devices = AudioRecorderService.GetInputDevices();
        MicComboBox.ItemsSource = devices;
        if (_settingsService.CurrentSettings.SelectedMicrophoneIndex < devices.Length)
        {
            MicComboBox.SelectedIndex = _settingsService.CurrentSettings.SelectedMicrophoneIndex;
        }

        VocabularyBox.Text = _settingsService.CurrentSettings.CustomVocabulary;
        PostProcessingCheckBox.IsChecked = _settingsService.CurrentSettings.IsPostProcessingEnabled;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _credentialService.SaveApiKey(ApiKeyBox.Password);
        _settingsService.CurrentSettings.SelectedMicrophoneIndex = MicComboBox.SelectedIndex;
        _settingsService.CurrentSettings.CustomVocabulary = VocabularyBox.Text;
        _settingsService.CurrentSettings.IsPostProcessingEnabled = PostProcessingCheckBox.IsChecked ?? true;
        _settingsService.Save();

        MessageBox.Show("Settings saved successfully!", "FreeFlow", MessageBoxButton.OK, MessageBoxImage.Information);
        this.Close();
    }
}
