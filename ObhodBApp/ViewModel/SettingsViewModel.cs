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

    public int updateInterval { get; set; } = 1000;

    private static readonly string SettingsFilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
        }
    }

    public static SettingsViewModel Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                var defaultSettings = new SettingsViewModel();
                defaultSettings.Save();
                return defaultSettings;
            }

            string json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<SettingsViewModel>(json);

            return settings ?? new SettingsViewModel();
        }
        catch
        {
            return new SettingsViewModel();
        }
    }
}