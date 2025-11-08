using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Material.Icons;
using ObhodBApp.Pages;

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
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Dispatcher.UIThread.UnhandledException += OnUIThreadException;
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string[] args = desktop.Args??[];
            
            mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            desktop.Exit += OnExit;
            
            if (args.Length > 0)
            {
                if (args[0] == "--autostart")
                    mainWindow.Autostart();
            }

            Logger.LogBlock = mainWindow.ConsoleTextBlock; //оч важная строчка
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    //TODO: ErrMesage вызывает ошибки удрал пока что
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = (Exception)e.ExceptionObject;
        Logger.Log(ex.ToString(), "error.txt");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Log(e.Exception.ToString(), "error.txt");
        e.SetObserved();
    }

    private void OnUIThreadException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Log(e.Exception.ToString(), "error.txt");
        e.Handled = true;
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        mainWindow.OnAppExit();
        Environment.Exit(0);
    }
}