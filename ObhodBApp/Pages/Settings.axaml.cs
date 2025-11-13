using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Win32.TaskScheduler;

namespace ObhodBApp.Pages;

public partial class Settings : UserControl
{
    public AppSettings CurrentAppSettings { get; set; }

    public Settings()
    {
        InitializeComponent();
        CurrentAppSettings = AppSettings.Load();
        DataContext = CurrentAppSettings;
        Ver.Text = $"ObhodBApp, сделанная G0Shark. Версия {AppInfo.InformationalVersion.Split('+').First()}.";
    }

    private void Save(object? sender, RoutedEventArgs e)
    {
        CurrentAppSettings.Save();
        var window = VisualRoot as MainWindow;
        window.AppSettings = CurrentAppSettings;
    }

    private void CheckToggle(object? sender, RoutedEventArgs e)
    {
        CurrentAppSettings.Save();
        var window = VisualRoot as MainWindow;
        window.AppSettings = CurrentAppSettings;
        
        if (sender is ToggleSwitch checkBox)
        {
            bool isChecked = (bool)checkBox.IsChecked!;
            
            if (isChecked)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (TaskService ts = new TaskService())
                    {
                        TaskDefinition td = ts.NewTask();
                        td.RegistrationInfo.Description = "ObhodBApp, автозапуск с правами Администратора (необходимо для Clash), отключить возможно в настройках программы.";

                        td.Principal.RunLevel = TaskRunLevel.Highest;
                        td.Principal.LogonType = TaskLogonType.InteractiveToken;

                        td.Triggers.Add(new LogonTrigger { Delay = TimeSpan.FromSeconds(5) });
                        td.Actions.Add(new ExecAction(Path.GetFullPath("ObhodBApp.exe"), "--autostart", AppDomain.CurrentDomain.BaseDirectory));

                        ts.RootFolder.RegisterTaskDefinition("ObhodBApp", td);
                    }
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string serviceContent = $@"
                    [Unit]
                    Description=ObhodBApp
                    After=network.target

                    [Service]
                    Type=simple
                    ExecStart={AppDomain.CurrentDomain.BaseDirectory+"/ObhodBApp"}
                    Restart=on-failure

                    [Install]
                    WantedBy=multi-user.target
                    ";

                    string servicePath = "/etc/systemd/system/ObhodBApp.service";

                    File.WriteAllText(servicePath, serviceContent);
                    
                    RunCommand("systemctl daemon-reload");
                    RunCommand("systemctl enable ObhodBApp.service");
                }
            }
            else
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (TaskService ts = new TaskService())
                    {
                        var task = ts.FindTask("ObhodBApp", true);
                        if (task != null)
                        {
                            ts.RootFolder.DeleteTask("ObhodBApp");
                        }
                    }
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    if (!File.Exists("/etc/systemd/system/ObhodBApp.service"))
                    {
                        Console.WriteLine("Сервис не найден.");
                        return;
                    }
                    
                    RunCommand("systemctl disable ObhodBApp.service");
                    File.Delete("/etc/systemd/system/ObhodBApp.service");
                    RunCommand("systemctl daemon-reload");
                }
            }
        }
    }
    
    private static void RunCommand(string command)
    {
        var processInfo = new ProcessStartInfo("bash", $"-c \"{command}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        process.WaitForExit();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        if (!string.IsNullOrEmpty(output)) Console.WriteLine(output);
        if (!string.IsNullOrEmpty(error)) Console.WriteLine(error);
    }

    private void ReverseProxy(object? sender, RoutedEventArgs e)
    {
        CurrentAppSettings.Save();
        var window = VisualRoot as MainWindow;
        window.AppSettings = CurrentAppSettings;
        window.Controller.ReverseProxy();
    }
}