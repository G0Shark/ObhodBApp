using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Material.Icons;

namespace ObhodBApp;

public partial class App : Application
{
    private MainWindow mainWindow;
    private TrayIcon? _trayIcon;
    private bool _isWindowHidden = false;
    public override void Initialize()
    {
        //Windows admin на манифесте
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var psi = new ProcessStartInfo("sudo", Process.GetCurrentProcess().MainModule!.FileName)
            {
                UseShellExecute = true
            };
            try { Process.Start(psi); Environment.Exit(0); }
            catch { return; }
        }
        
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            desktop.Exit += OnExit;

            Logger.LogBlock = mainWindow.ConsoleTextBlock; //оч важная строчка
            
            UpdateMsg updater = new();
            updater.Show();

            var menu = new NativeMenu();
            
            menu.Add(new NativeMenuItem("Переключение")
            {
                Command = ReactiveUI.ReactiveCommand.Create(() =>
                {
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.MainWindow?.Show();
                        desktop.MainWindow?.Activate();
                    }
                })
            });

            menu.Add(new NativeMenuItemSeparator());
            
            menu.Add(new NativeMenuItem("Выход")
            {
                Command = ReactiveUI.ReactiveCommand.Create(() =>
                {
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        desktop.Shutdown();
                })
            });
            
            _trayIcon = new TrayIcon
            {
                ToolTipText = "ObhodBApp",
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ObhodBApp/icon.ico"))),
                Menu = menu
            };

            _trayIcon.Clicked += (sender, e) =>
            {
                if (_isWindowHidden)
                {
                    mainWindow.Show();
                    mainWindow.Activate();
                    _isWindowHidden = false;
                }
                else
                {
                    mainWindow.Hide();
                    _isWindowHidden = true;
                }
            };

            _trayIcon.IsVisible = true;
            
            mainWindow.trayIcon = _trayIcon;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        mainWindow.OnAppExit();
    }
}