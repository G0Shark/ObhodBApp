using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace ObhodBApp;

public class ConfigService
{
    private readonly string _configFile = "configs.json";

    public ObservableCollection<AppConfig> Configs { get; private set; } = new();
    public AppConfig? CurrentConfig { get; set; }

    public void Load()
    {
        if (File.Exists(_configFile))
        {
            var json = File.ReadAllText(_configFile);
            var configs = JsonSerializer.Deserialize<ObservableCollection<AppConfig>>(json);
            if (configs != null)
                Configs = configs;
        }

        if (Configs.Count > 0)
            CurrentConfig = Configs[0];
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Configs, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configFile, json);
    }

    public void AddConfig(string name, string path)
    {
        var cfg = new AppConfig { Name = name, FilePath = path };
        Configs.Add(cfg);
        CurrentConfig = cfg;
        Save();
    }
}

public class AppConfig
{
    public string Name { get; set; } = "Новый конфиг";
    public string FilePath { get; set; } = "";
}