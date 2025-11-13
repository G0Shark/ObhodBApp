using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
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
    public bool IsEnable;
    public TrayIcon TrayIcon;
    NetworkInterface _adapter = NetworkInterface.GetAllNetworkInterfaces()
        .FirstOrDefault(ni => ni.Name == "Meta")!;

    private ObservableCollection<double> _line1Values = new()
    {
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
    };
    private ObservableCollection<double> _line2Values = new()
    {
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
    };

    private long _recvAllSecs = 1;
    private long _sendAllSecs = 1;
    private int _secs = 1;
    public ClashController Controller;
    private Task updater;
    public AppSettings AppSettings;
    private bool _isWindowHidden;
    private ConfigManager _configManager;
    private string _rulesFilePath;

    public MainWindow()
    {
        InitializeComponent();
        Controller = new ClashController(this);
        
        AppSettings = AppSettings.Load();
        
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

        if (AppSettings.checkForUpdates)
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
            
        TrayIcon = new TrayIcon
        {
            ToolTipText = "ObhodBApp",
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ObhodBApp/icon.ico"))),
            Menu = menu
        };

        TrayIcon.Clicked += (sender, e) =>
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
        
        TrayIcon.IsVisible = true;
        
        Editor.Text =
            File.ReadAllText(_rulesFilePath);

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
        
        if (string.IsNullOrEmpty(_rulesFilePath))
            RulesFile();
        
        Controller.UpdateImports(_rulesFilePath, selected.FilePath);
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

            ConfigCombo.SelectedIndex = 0;
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
        
        _rulesFilePath = Path.Combine(baseDir, "rules.yaml");

        if (!File.Exists(_rulesFilePath))
            File.WriteAllText(_rulesFilePath, "rules:\n  #Пример правила: - DOMAIN-KEYWORD,youtube.com\n\n\n  # Оставьте эту строчку, чтобы в программе корректно указывался ваш IP\n  - PROCESS-NAME,ObhodBApp.exe");
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

        if (!IsEnable)
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

            TrayIcon.Menu = menu;
            
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

        TrayIcon.Menu = menu;
    }
    
    private async Task MainUpdateTask()
    {
        while (true)
        {
            try
            {
                var stats1 = _adapter.GetIPv4Statistics();
                long recv1 = stats1.BytesReceived;
                long sent1 = stats1.BytesSent;

                await Task.Delay(AppSettings.updateInterval);

                var stats2 = _adapter.GetIPv4Statistics();
                long recv2 = stats2.BytesReceived;
                long sent2 = stats2.BytesSent;

                long recvPerSec = recv2 - recv1;
                long sentPerSec = sent2 - sent1;

                if (IsEnable)
                {
                    _recvAllSecs += recvPerSec;
                    _sendAllSecs += sentPerSec;
                    _secs += AppSettings.updateInterval;
                }

                long totalReceived = stats2.BytesReceived;
                long totalSent = stats2.BytesSent;
                long totalTraffic = totalReceived + totalSent;

                UpdateTray(TimeSpan.FromMilliseconds(_secs).ToString(@"hh\:mm\:ss"), FormatBytes(totalTraffic));

                ErrPckText.Text = (stats1.IncomingPacketsWithErrors + stats1.OutgoingPacketsWithErrors).ToString();

                if (_secs >= AppSettings.updateInterval)
                {
                    MUplText.Text = FormatBytes(_sendAllSecs / (long)TimeSpan.FromMilliseconds(_secs).TotalSeconds);
                    MDwlText.Text = FormatBytes(_recvAllSecs / (long)TimeSpan.FromMilliseconds(_secs).TotalSeconds);
                }
                Time.Text = TimeSpan.FromMilliseconds(_secs).ToString(@"hh\:mm\:ss");

                UplText.Text = FormatBytes(sentPerSec);
                DwlText.Text = FormatBytes(recvPerSec);

                TrafficText.Text = FormatBytes(totalTraffic);

                _line1Values.Add(recvPerSec);
                _line2Values.Add(sentPerSec);

                while (_line1Values.Count > AppSettings.updatesCount)
                    _line1Values.RemoveAt(0);

                while (_line2Values.Count > AppSettings.updatesCount)
                    _line2Values.RemoveAt(0);

                Chart.Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Fill = new SolidColorPaint(new SKColor(SKColors.Green.Red, SKColors.Green.Green,
                            SKColors.Green.Blue, 60)),
                        Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 4 }, Values = _line1Values,
                        GeometryFill = null, GeometryStroke = null, AnimationsSpeed = TimeSpan.Zero,
                        YToolTipLabelFormatter = point => { return FormatBytes((long)point.Model); }
                    },
                    new LineSeries<double>
                    {
                        Fill = new SolidColorPaint(new SKColor(SKColors.Orange.Red, SKColors.Orange.Green,
                            SKColors.Orange.Blue, 60)),
                        Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 4 }, Values = _line2Values,
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
        if (!IsEnable)
        {
            MainBtn.IsEnabled = false;
            MainBtnIcon.Animation = MaterialIconAnimation.FadeInOut;
            if (!Controller.TryConfig())
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
            Controller.Start();
            _secs = 0;
            _recvAllSecs = 0;
            _sendAllSecs = 0;
            TrayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ObhodBApp/enabled.ico")));
            TimeIcon.Animation = MaterialIconAnimation.None;
            MainBtnIcon.Animation = MaterialIconAnimation.Spin;
            IsEnable = true;
            MainBtn.IsEnabled = true;

        }
        else
        {
            MainBtn.IsEnabled = false;
            MainBtnIcon.Animation = MaterialIconAnimation.FadeInOut;
            Controller.Stop();
            TrayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ObhodBApp/icon.ico")));
            TimeIcon.Animation = MaterialIconAnimation.FadeInOut;
            MainBtn.Foreground = null;
            MainBtnIcon.Animation = MaterialIconAnimation.None;
            IsEnable = false;
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
        if (AppSettings.goToTray)
        {
            base.OnClosing(e);
            e.Cancel = true;
            Hide();
        }
    }
    
    public void OnAppExit()
    {
        Controller.Stop();
    }

    private void ClashConfigSave(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_rulesFilePath))
            RulesFile();
        
        string reserve =
            File.ReadAllText(_rulesFilePath);

        File.WriteAllText(_rulesFilePath, Editor.Text);
        
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