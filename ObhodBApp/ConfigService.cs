using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace ObhodBApp;

public class ConfigManager
{
    private readonly string _configFilePath;
    public List<ConnectionConfig> Configs { get; private set; } = new();

    public event Action? OnCreateConnection; 

    public ConfigManager(string configFilePath = "configs.json")
    {
        _configFilePath = configFilePath;
        LoadConfigs();
    }

    public void LoadConfigs()
    {
        if (!File.Exists(_configFilePath))
        {
            Configs = new List<ConnectionConfig>();
            SaveConfigs();
            return;
        }

        try
        {
            var json = File.ReadAllText(_configFilePath);
            Configs = JsonSerializer.Deserialize<List<ConnectionConfig>>(json) ?? new List<ConnectionConfig>();
        }
        catch
        {
            Configs = new List<ConnectionConfig>();
        }
    }

    public void SaveConfigs()
    {
        var json = JsonSerializer.Serialize(Configs, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configFilePath, json);
    }

    public void AddConfig(ConnectionConfig config)
    {
        Configs.Add(config);
        SaveConfigs();
    }

    public void HandleSelection(string selectedName)
    {
        if (selectedName == "Создать соединение")
            OnCreateConnection?.Invoke();
    }
}

public class ConnectionConfig
{
    public string Name { get; set; }
    public string FilePath { get; set; }

    public override string ToString() => Name;
}