using System;
using System.IO;
using System.Text.Json;
using ReactiveUI;
using System.Reactive;

public class Settings
{
    public int UpdateInterval { get; set; }
}

public class AppSettings : ReactiveObject
{
    private const string SettingsFileName = "settings.json";

    public int updateInterval { get; set; } = 1000;
    public bool checkForUpdates { get; set; } = true; 
    public int updatesCount { get; set; } = 20;
    public bool goToTray { get; set; } = true;
    public bool Autostart {  get; set; } = false;

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

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                var defaultSettings = new AppSettings();
                defaultSettings.Save();
                return defaultSettings;
            }

            string json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}