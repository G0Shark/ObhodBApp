using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Material.Icons;
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

    private long recvAllSecs = 0;
    private long sendAllSecs = 0;
    private int secs = 0;
    
    private ClashController controller;
    private Task updater;
    public AppSettings _appSettings;
    public MainWindow()
    {
        InitializeComponent();
        controller = new ClashController(this);
        
        _appSettings = AppSettings.Load();

        if (_appSettings.checkForUpdates)
        {
            var updateMsg = new UpdateMsg();
            updateMsg.Show();
        }
        
        updater = MainUpdateTask();
        
        Chart.YAxes = new Axis[]
        {
            new Axis
            {
                Labeler = value => FormatBytes((long)value)
            }
        };

        Editor.Text =
            File.ReadAllText($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!}\\clash\\config.yaml");

        CheckIpAdress();
    }
    private async Task MainUpdateTask()
    {
        while (true)
        {
            var stats1 = adapter.GetIPv4Statistics();
            long recv1 = stats1.BytesReceived;
            long sent1 = stats1.BytesSent;
            
            ErrPckText.Text = (stats1.IncomingPacketsWithErrors + stats1.OutgoingPacketsWithErrors).ToString();
            
            await Task.Delay(_appSettings.updateInterval);

            var stats2 = adapter.GetIPv4Statistics();
            long recv2 = stats2.BytesReceived;
            long sent2 = stats2.BytesSent;

            long recvPerSec = recv2 - recv1;
            long sentPerSec = sent2 - sent1;

            recvAllSecs += recvPerSec;
            sendAllSecs += sentPerSec;
            secs += _appSettings.updateInterval;
            
            MUplText.Text = FormatBytes(sendAllSecs/(long)TimeSpan.FromMilliseconds(secs).TotalSeconds);
            MDwlText.Text = FormatBytes(recvAllSecs/(long)TimeSpan.FromMilliseconds(secs).TotalSeconds);
            Time.Text = TimeSpan.FromMilliseconds(secs).ToString(@"hh\:mm\:ss");
            
            UplText.Text = FormatBytes(sentPerSec);
            DwlText.Text = FormatBytes(recvPerSec);
            
            long totalReceived = stats2.BytesReceived;
            long totalSent = stats2.BytesSent;
            long totalTraffic = totalReceived + totalSent;

            TrafficText.Text = FormatBytes(totalTraffic);
            
            line1Values.Add(recvPerSec);
            line2Values.Add(sentPerSec);

            while (line1Values.Count > _appSettings.updatesCount)
                line1Values.RemoveAt(0);
            
            while (line2Values.Count > _appSettings.updatesCount)
                line2Values.RemoveAt(0);
            
            Chart.Series = new ISeries[]
            {
                new LineSeries<double> { Fill = new SolidColorPaint(new SKColor(SKColors.Green.Red, SKColors.Green.Green, SKColors.Green.Blue, 60)), Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 4 }, Values = line1Values, GeometryFill = null, GeometryStroke = null, AnimationsSpeed = TimeSpan.Zero, YToolTipLabelFormatter = point =>
                {
                    return FormatBytes((long)point.Model);
                } },
                new LineSeries<double> { Fill = new SolidColorPaint(new SKColor(SKColors.Orange.Red, SKColors.Orange.Green, SKColors.Orange.Blue, 60)), Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 4 }, Values = line2Values, GeometryFill = null, GeometryStroke = null, AnimationsSpeed = TimeSpan.Zero, YToolTipLabelFormatter = point =>
                {
                    return FormatBytes((long)point.Model);
                } },
            };
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
        if (!isEnable)
        {
            MainBtn.IsEnabled = false;
            MainBtnIcon.Animation = MaterialIconAnimation.FadeInOut;
            controller.Start();
            secs = 0;
            recvAllSecs = 0;
            sendAllSecs = 0;
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
            TimeIcon.Animation = MaterialIconAnimation.FadeInOut;
            MainBtnIcon.Animation = MaterialIconAnimation.None;
            isEnable = false;
            MainBtn.IsEnabled = true;
        }
        
        CheckIpAdress();
    }
    
    private async Task CheckIpAdress()
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
            }
        }
        catch (Exception ex)
        {
            IpInfo.Foreground = Brushes.Red;
            IpInfo.Text = ex.Message;
            IpIcon.Foreground = Brushes.Red;
            IpIcon.Animation = MaterialIconAnimation.FadeInOut;
        }
    }
    
    public static string GetFlagEmoji(string countryCode)
    {
        if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2)
            throw new ArgumentException("Код страны должен состоять из двух букв (например, 'RU').");
        
        countryCode = countryCode.ToUpperInvariant();
        
        int offset = 0x1F1E6 - 'A';

        string flag = string.Concat(
            char.ConvertFromUtf32(countryCode[0] + offset),
            char.ConvertFromUtf32(countryCode[1] + offset)
        );

        return flag;
    }
    
    public void OnAppExit()
    {
        controller.Stop();
    }

    private void ClashConfigSave(object? sender, RoutedEventArgs e)
    {
        string cfgPath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!}\\clash\\config.yaml";
        string reserve =
            File.ReadAllText(cfgPath);

        File.WriteAllText(cfgPath, Editor.Text);
        
        if (!controller.TryConfig())
        {
            File.WriteAllText(cfgPath, reserve);
            CfgWait.Animation = MaterialIconAnimation.FadeInOut;
            CfgWait.Kind = MaterialIconKind.Error;
            return;
        }
        
        CfgWait.Animation = MaterialIconAnimation.FadeInOut;
        CfgWait.Kind = MaterialIconKind.Success;
        return;
    }

    private void ConfigChanged(object? sender, EventArgs e)
    {
        if (CfgWait.Animation != MaterialIconAnimation.Spin)
            CfgWait.Animation = MaterialIconAnimation.Spin;
        if (CfgWait.Kind != MaterialIconKind.CogClockwise)
            CfgWait.Kind = MaterialIconKind.CogClockwise;
    }
}

public class IpResponse
{
    public string ip { get; set; }
    public string country { get; set; }
    public string cc { get; set; } // country code (например "US")
}