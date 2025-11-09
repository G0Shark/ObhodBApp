using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;

namespace ObhodBApp.Pages;

public partial class CreateConnection : Window
{
    private ConfigManager _configManager;
    public CreateConnection(ref ConfigManager configManager)
    {
        InitializeComponent();
        Console.WriteLine("dsdsdsd");
        _configManager = configManager;
        Console.WriteLine("dsdsdsdfdff");
    }

    private async Task<string?> GetClipboardTextAsync()
    {
        var clipboard = GetTopLevel(this)?.Clipboard;

        if (clipboard != null)
        {
            string? text = await clipboard.GetTextAsync();
            return text;
        }

        return null;
    }
    
    private async void Selected(object? sender, SelectionChangedEventArgs e)
    {
        //TODO: Остальные варианты
        Console.WriteLine("helloing");
        switch (OptionsListBox.SelectedIndex)
        {
            case 0:
                string clipboardText = await GetClipboardTextAsync()??"";
                var notificationManager = new WindowNotificationManager(this)
                {
                    Position = NotificationPosition.TopRight
                };
                try
                {
                    string c = ClashProxyConverter.ConvertMultipleToClashYaml(clipboardText);

                    string baseDir;

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        baseDir = Path.Combine(documentsPath, "ObhodBApp");
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        baseDir = Path.Combine(homePath, ".config", "ObhodBApp");
                    }
                    else
                    {
                        throw new PlatformNotSupportedException("Поддерживаются только Windows и Linux.");
                    }
                    if (!Directory.Exists(baseDir))
                        Directory.CreateDirectory(baseDir);
                    string name = ClashProxyConverter.GetProfileName(clipboardText);
                    string filePath = Path.Combine(baseDir, name+".yaml");

                    if (File.Exists(filePath))
                    {
                        notificationManager.Show(
                            new Notification("Ошибка", "Такое имя уже занято", NotificationType.Error)
                        );
                        break;
                    }
                    
                    File.WriteAllText(filePath, c);
                    
                    _configManager.AddConfig(new ConnectionConfig(){FilePath = filePath, Name = name});
                    
                    Close();
                }
                catch
                {
                    notificationManager.Show(
                        new Notification("Ошибка", "Конфиг не поддерживается", NotificationType.Error)
                    );
                }
                break;
        }
    }
}