using System;
using System.IO;
using System.Text.Json;
using ReactiveUI;
using System.Reactive;

public class Settings
{
    public int UpdateInterval { get; set; }
}

public class SettingsViewModel : ReactiveObject
{
    private const string SettingsFileName = "settings.json";

    private int _updateInterval;
    public int UpdateInterval
    {
        get => _updateInterval;
        set => this.RaiseAndSetIfChanged(ref _updateInterval, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public SettingsViewModel()
    {
        SaveCommand = ReactiveCommand.Create(SaveSettings);
        LoadSettings();
    }

    private void SaveSettings()
    {
        var settings = new Settings
        {
            UpdateInterval = this.UpdateInterval
        };

        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFileName, json);
    }

    private void LoadSettings()
    {
        if (!File.Exists(SettingsFileName))
            return;

        try
        {
            string json = File.ReadAllText(SettingsFileName);
            var settings = JsonSerializer.Deserialize<Settings>(json);

            if (settings != null)
            {
                UpdateInterval = settings.UpdateInterval;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при загрузке настроек: {ex.Message}");
        }
    }
}