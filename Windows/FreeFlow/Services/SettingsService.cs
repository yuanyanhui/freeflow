using System;
using System.IO;
using Newtonsoft.Json;

namespace FreeFlow.Services;

public class Settings
{
    public string CustomVocabulary { get; set; } = "";
    public int SelectedMicrophoneIndex { get; set; } = 0;
}

public class SettingsService
{
    private readonly string _filePath;
    public Settings CurrentSettings { get; private set; }

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "FreeFlow");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "settings.json");
        CurrentSettings = Load();
    }

    public Settings Load()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
        }
        return new Settings();
    }

    public void Save()
    {
        var json = JsonConvert.SerializeObject(CurrentSettings);
        File.WriteAllText(_filePath, json);
    }
}
