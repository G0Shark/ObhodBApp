using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Material.Icons;
using ObhodBApp.Pages;
using ReactiveUI;
using SkiaSharp;

namespace ObhodBApp;

public partial class MainWindow : Window
{
    public bool isEnable;
    public TrayIcon trayIcon;
    NetworkInterface adapter = NetworkInterface.GetAllNetworkInterfaces()
        .FirstOrDefault(ni => ni.Name == "Meta")!;

    private ObservableCollection<double> line1Values = new()
    {
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
    };
    private ObservableCollection<double> line2Values = new()
    {
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
    };

    private long recvAllSecs = 1;
    private long sendAllSecs = 1;
    private int secs = 1;
    private ClashController controller;
    private Task updater;
    public AppSettings _appSettings;
    private bool _isWindowHidden;
    private ConfigManager _configManager;
    private string rulesFilePath;

    public MainWindow()
    {
        InitializeComponent();
        controller = new ClashController(this);
        
        _appSettings = AppSettings.Load();
        
        _configManager = new ConfigManager();
        _configManager.OnCreateConnection += CreateConn;

        var items = new List<ConnectionConfig>(_configManager.Configs);
        items.Add(new ConnectionConfig { Name = "Создать соединение" });

        ConfigCombo.ItemsSource = items;

        if (_configManager.Configs.Count > 0)
        {
            ConfigCombo.SelectedIndex = 0;
            MainBtn.IsEnabled = true;
        }

        ConfigCombo.SelectionChanged += ConfigSelected;

        if (_appSettings.checkForUpdates)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                new UpdateMsg();
            });
        }

        RulesFile();
        updater = MainUpdateTask();
        
        Chart.YAxes = new Axis[]
        {
            new Axis
            {
                Labeler = value => FormatBytes((long)value)
            }
        };

        var menu = new NativeMenu();
            
        menu.Add(new NativeMenuItem("Выход")
        {
            Command = ReactiveCommand.Create(() =>
            {
                Close();
            })
        });
            
        trayIcon = new TrayIcon
        {
            ToolTipText = "ObhodBApp",
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ObhodBApp/icon.ico"))),
            Menu = menu
        };

        trayIcon.Clicked += (sender, e) =>
        {
            if (_isWindowHidden)
            {
                Show();
                Activate();
                _isWindowHidden = false;
            }
            else
            {
                Hide();
                _isWindowHidden = true;
            }
        };
        
        trayIcon.IsVisible = true;
        
        Editor.Text =
            File.ReadAllText(rulesFilePath);

        CheckIpAdress();
        LogDelay();
    }

    private void ConfigSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ConfigCombo.SelectedItem is not ConnectionConfig selected)
            return;
        
        if (selected.Name == "Создать соединение")
        {
            _configManager.HandleSelection(selected.Name);
            return;
        }
        
        if (string.IsNullOrEmpty(rulesFilePath))
            RulesFile();
        
        controller.UpdateImports(rulesFilePath, selected.FilePath);
    }

    private void CreateConn()
    {
        CreateConnection w = new CreateConnection(ref _configManager);
        w.Show();
        w.Closed += (_, _) =>
        {
            var items = new List<ConnectionConfig>(_configManager.Configs);
            items.Add(new ConnectionConfig { Name = "Создать соединение" });

            ConfigCombo.ItemsSource = items;
            
            if (_configManager.Configs.Count > 0)
                MainBtn.IsEnabled = true;
        };
    }

    private void RulesFile()
    {
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
        
        rulesFilePath = Path.Combine(baseDir, "rules.yaml");

        if (!File.Exists(rulesFilePath))
            File.WriteAllText(rulesFilePath, "rules:\n  # Оставьте эту строчку, чтобы весь остальной трафик шёл через интернет\n  - MATCH,DIRECT");
    }
    
    private async Task LogDelay()
    {
        while (Logger.LogBlock == null)
            await Task.Delay(100);
        
        Logger.WriteLog(null, "INFO", "Тут будут логи от clash", Colors.Aqua, null);
        UpdateTray();
    }

    private void UpdateTray(string? time = "", string? total = "")
    {
        var menu = new NativeMenu();

        if (!isEnable)
        {
            menu.Add(new NativeMenuItem("Включить")
            {
                Command = ReactiveCommand.CreateFromTask(
                    async () =>
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ToggleClash();
                        });
                    },
                    outputScheduler: RxApp.MainThreadScheduler
                )
            });
            
            menu.Add(new NativeMenuItemSeparator());
            
            menu.Add(new NativeMenuItem("Выйти")
            {
                Command = ReactiveCommand.CreateFromTask(
                    async () =>
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Environment.Exit(0);
                        });
                    },
                    outputScheduler: RxApp.MainThreadScheduler
                )
            });

            trayIcon.Menu = menu;
            
            return;
        }
        
        menu.Add(new NativeMenuItem("Включить")
        {
            Command = ReactiveCommand.CreateFromTask(
                async () =>
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ToggleClash();
                    });
                },
                outputScheduler: RxApp.MainThreadScheduler
            )
        });

        menu.Add(new NativeMenuItemSeparator());
        
        menu.Add(new NativeMenuItem("Траффик - " + total));
        menu.Add(new NativeMenuItem("Время - " + time));
        
        menu.Add(new NativeMenuItemSeparator());
            
        menu.Add(new NativeMenuItem("Выйти")
        {
            Command = ReactiveCommand.CreateFromTask(
                async () =>
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Environment.Exit(0);
                    });
                },
                outputScheduler: RxApp.MainThreadScheduler
            )
        });

        trayIcon.Menu = menu;
    }
    
    private async Task MainUpdateTask()
    {
        while (true)
        {
            try
            {
                var stats1 = adapter.GetIPv4Statistics();
                long recv1 = stats1.BytesReceived;
                long sent1 = stats1.BytesSent;

                await Task.Delay(_appSettings.updateInterval);

                var stats2 = adapter.GetIPv4Statistics();
                long recv2 = stats2.BytesReceived;
                long sent2 = stats2.BytesSent;

                long recvPerSec = recv2 - recv1;
                long sentPerSec = sent2 - sent1;

                if (isEnable)
                {
                    recvAllSecs += recvPerSec;
                    sendAllSecs += sentPerSec;
                    secs += _appSettings.updateInterval;
                }

                long totalReceived = stats2.BytesReceived;
                long totalSent = stats2.BytesSent;
                long totalTraffic = totalReceived + totalSent;

                UpdateTray(TimeSpan.FromMilliseconds(secs).ToString(@"hh\:mm\:ss"), FormatBytes(totalTraffic));

                ErrPckText.Text = (stats1.IncomingPacketsWithErrors + stats1.OutgoingPacketsWithErrors).ToString();

                if (secs >= _appSettings.updateInterval)
                {
                    MUplText.Text = FormatBytes(sendAllSecs / (long)TimeSpan.FromMilliseconds(secs).TotalSeconds);
                    MDwlText.Text = FormatBytes(recvAllSecs / (long)TimeSpan.FromMilliseconds(secs).TotalSeconds);
                }
                Time.Text = TimeSpan.FromMilliseconds(secs).ToString(@"hh\:mm\:ss");

                UplText.Text = FormatBytes(sentPerSec);
                DwlText.Text = FormatBytes(recvPerSec);

                TrafficText.Text = FormatBytes(totalTraffic);

                line1Values.Add(recvPerSec);
                line2Values.Add(sentPerSec);

                while (line1Values.Count > _appSettings.updatesCount)
                    line1Values.RemoveAt(0);

                while (line2Values.Count > _appSettings.updatesCount)
                    line2Values.RemoveAt(0);

                Chart.Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Fill = new SolidColorPaint(new SKColor(SKColors.Green.Red, SKColors.Green.Green,
                            SKColors.Green.Blue, 60)),
                        Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 4 }, Values = line1Values,
                        GeometryFill = null, GeometryStroke = null, AnimationsSpeed = TimeSpan.Zero,
                        YToolTipLabelFormatter = point => { return FormatBytes((long)point.Model); }
                    },
                    new LineSeries<double>
                    {
                        Fill = new SolidColorPaint(new SKColor(SKColors.Orange.Red, SKColors.Orange.Green,
                            SKColors.Orange.Blue, 60)),
                        Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 4 }, Values = line2Values,
                        GeometryFill = null, GeometryStroke = null, AnimationsSpeed = TimeSpan.Zero,
                        YToolTipLabelFormatter = point => { return FormatBytes((long)point.Model); }
                    },
                };
            }
            catch (Exception ex)
            {
                Logger.WriteLog(null, "ERROR", ex.Message + " | " + ex.StackTrace, Colors.Red, null);
            }
        }
    }
    
    static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
    
    private async void MainBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        ToggleClash();
    }

    void ToggleClash()
    {
        if (!isEnable)
        {
            MainBtn.IsEnabled = false;
            MainBtnIcon.Animation = MaterialIconAnimation.FadeInOut;
            if (!controller.TryConfig())
            {
                MainBtn.Foreground = Brushes.Red;
                MainBtnIcon.Animation = MaterialIconAnimation.None;
                var notificationManager = new WindowNotificationManager(this)
                {
                    Position = NotificationPosition.TopRight
                };
                notificationManager.Show(
                    new Notification("Ошибка", "Конфиг содержит ошибки", NotificationType.Error)
                );
                MainBtn.IsEnabled = true;
            }
            controller.Start();
            secs = 0;
            recvAllSecs = 0;
            sendAllSecs = 0;
            trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ObhodBApp/enabled.ico")));
            TimeIcon.Animation = MaterialIconAnimation.None;
            MainBtnIcon.Animation = MaterialIconAnimation.Spin;
            isEnable = true;
            MainBtn.IsEnabled = true;

        }
        else
        {
            MainBtn.IsEnabled = false;
            MainBtnIcon.Animation = MaterialIconAnimation.FadeInOut;
            controller.Stop();
            trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ObhodBApp/icon.ico")));
            TimeIcon.Animation = MaterialIconAnimation.FadeInOut;
            MainBtn.Foreground = null;
            MainBtnIcon.Animation = MaterialIconAnimation.None;
            isEnable = false;
            MainBtn.IsEnabled = true;
        }
    }
    
    private async Task<string> CheckIpAdress()
    {
        try
        {
            using var client = new HttpClient();
            string json = await client.GetStringAsync("https://api.myip.com");
            var info = JsonSerializer.Deserialize<IpResponse>(json);

            if (info != null)
            {
                IpInfo.ClearValue(TextBlock.ForegroundProperty);
                IpIcon.ClearValue(TextBlock.ForegroundProperty);
                IpInfo.Text = $"Ваш IP: {info.ip} {GetFlagEmoji(info.cc)}";
                IpIcon.Animation = MaterialIconAnimation.None;
                return $"Ваш IP: {info.ip} {GetFlagEmoji(info.cc)}";
            }
        }
        catch (Exception ex)
        {
            IpInfo.Foreground = Brushes.Red;
            IpInfo.Text = ex.Message;
            IpIcon.Foreground = Brushes.Red;
            IpIcon.Animation = MaterialIconAnimation.FadeInOut;
        }
        
        return "";
    }
    
    public static string GetFlagEmoji(string countryCode)
    {
        countryCode = countryCode.ToUpperInvariant();
        
        int offset = 0x1F1E6 - 'A';

        string flag = string.Concat(
            char.ConvertFromUtf32(countryCode[0] + offset),
            char.ConvertFromUtf32(countryCode[1] + offset)
        );

        return flag;
    }
    
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_appSettings.goToTray)
        {
            base.OnClosing(e);
            e.Cancel = true;
            Hide();
        }
    }
    
    public void OnAppExit()
    {
        controller.Stop();
    }

    private void ClashConfigSave(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(rulesFilePath))
            RulesFile();
        
        string reserve =
            File.ReadAllText(rulesFilePath);

        File.WriteAllText(rulesFilePath, Editor.Text);
        
        if (!controller.TryConfig())
        {
            File.WriteAllText(rulesFilePath, reserve);
            CfgWait.Animation = MaterialIconAnimation.FadeInOut;
            CfgWait.Kind = MaterialIconKind.Error;
            return;
        }
        
        CfgWait.Animation = MaterialIconAnimation.FadeInOut;
        CfgWait.Kind = MaterialIconKind.Success;
    }

    private void ConfigChanged(object? sender, EventArgs e)
    {
        if (CfgWait.Animation != MaterialIconAnimation.Spin)
            CfgWait.Animation = MaterialIconAnimation.Spin;
        if (CfgWait.Kind != MaterialIconKind.CogClockwise)
            CfgWait.Kind = MaterialIconKind.CogClockwise;
    }

    public void Autostart()
    {
        ToggleClash();
        Hide();
    }
}

public class IpResponse
{
    public string ip { get; set; }
    public string country { get; set; }
    public string cc { get; set; }
}