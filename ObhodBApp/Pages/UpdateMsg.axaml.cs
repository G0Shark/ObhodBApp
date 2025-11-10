using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ObhodBApp;

public partial class UpdateMsg : Window
{
    public UpdateMsg()
    {
        InitializeComponent();

        NewMethod();
    }

    private async void NewMethod()
    {
        try
        {
            using HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            client.DefaultRequestHeaders.Add("User-Agent", "C# App");

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(
                    "https://raw.githubusercontent.com/G0Shark/ObhodBApp/refs/heads/main/ObhodBApp/version.json");
            request.Method = "GET";

            string content = "";
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (Stream responseStream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        content = reader.ReadToEnd();
                    }
                }
                else
                {
                    Logger.WriteLog("UPDATE", "ERROR", $"Неверный ответ сервера: {response.StatusCode}", Colors.Red,
                        Colors.Purple);
                }

                var info = JsonSerializer.Deserialize<UpdateInfo>(content);

                if (info == null) return;

                var current = Version.Parse(AppInfo.FileVersion);
                var latest = Version.Parse(info.version);

                if (latest > current)
                {
                    Logger.WriteLog("UPDATE", "INFO", $"Доступно новое обновление", Colors.Aqua, Colors.Purple);

                    tVersion.Text = current + " ==> " + latest;
                    tComment.Text = info.comment; 
                }
                else
                {
                    Logger.WriteLog("UPDATE", "INFO", $"У вас последняя версия", Colors.Aqua, Colors.Purple);
                    Close();
                }
            }
        }
        catch
        {
            Logger.WriteLog("UPDATE", "ERROR", $"Ошибка", Colors.Red,
                Colors.Purple);
        }
    }

    private void GetUpdate(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/G0Shark/ObhodBApp/releases/latest") { UseShellExecute = true });
    }

    private void Close(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DontShowAgain(object? sender, RoutedEventArgs e)
    {
        var s = AppSettings.Load();
        s.checkForUpdates = false;
        s.Save();
        Close();
    }
}

public class UpdateInfo
{
    public string version { get; set; } = "";
    public string comment { get; set; } = "";
}