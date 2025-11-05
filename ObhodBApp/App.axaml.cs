using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Material.Icons;

namespace ObhodBApp;

public partial class App : Application
{
    private MainWindow mainWindow;
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
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        mainWindow.OnAppExit();
    }
}