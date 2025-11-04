using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Material.Icons;
using SkiaSharp;

namespace ObhodBApp;

public partial class MainWindow : Window
{
    private bool iss = false;
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
    public MainWindow()
    {
        InitializeComponent();
        controller = new ClashController(this);
        
        _ = testbytes();
        
        Chart.YAxes = new Axis[]
        {
            new Axis
            {
                Labeler = value => FormatBytes((long)value)
            }
        };
    }
    private async Task testbytes()
    {
        while (true)
        {
            var stats1 = adapter.GetIPv4Statistics();
            long recv1 = stats1.BytesReceived;
            long sent1 = stats1.BytesSent;
            
            ErrPckText.Text = (stats1.IncomingPacketsWithErrors + stats1.OutgoingPacketsWithErrors).ToString();
            
            await Task.Delay(1000);

            var stats2 = adapter.GetIPv4Statistics();
            long recv2 = stats2.BytesReceived;
            long sent2 = stats2.BytesSent;

            long recvPerSec = recv2 - recv1;
            long sentPerSec = sent2 - sent1;

            recvAllSecs += recvPerSec;
            sendAllSecs += sentPerSec;
            secs++;
            
            MUplText.Text = FormatBytes(sendAllSecs/secs);
            MDwlText.Text = FormatBytes(recvAllSecs/secs);
            Time.Text = TimeSpan.FromSeconds(secs).ToString(@"hh\:mm\:ss");
            
            UplText.Text = FormatBytes(sentPerSec) + "\\s";
            DwlText.Text = FormatBytes(recvPerSec) + "\\s";
            
            long totalReceived = stats2.BytesReceived;
            long totalSent = stats2.BytesSent;
            long totalTraffic = totalReceived + totalSent;

            TrafficText.Text = FormatBytes(totalTraffic);
            
            line1Values.Add(recvPerSec);
            line2Values.Add(sentPerSec);

            // Ограничиваем длину графика до 20 точек
            if (line1Values.Count > 20) line1Values.RemoveAt(0);
            if (line2Values.Count > 20) line2Values.RemoveAt(0);
            
            Chart.Series = new ISeries[]
            {
                new LineSeries<double> { Fill = new SolidColorPaint(new SKColor(SKColors.Green.Red, SKColors.Green.Green, SKColors.Green.Blue, 60)), Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 4 }, Values = line1Values, GeometryFill = null, GeometryStroke = null, AnimationsSpeed = TimeSpan.Zero, YToolTipLabelFormatter = point =>
                {
                    // point.PrimaryValue содержит значение Y
                    return FormatBytes((long)point.Model);
                } },
                new LineSeries<double> { Fill = new SolidColorPaint(new SKColor(SKColors.Orange.Red, SKColors.Orange.Green, SKColors.Orange.Blue, 60)), Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 4 }, Values = line2Values, GeometryFill = null, GeometryStroke = null, AnimationsSpeed = TimeSpan.Zero, YToolTipLabelFormatter = point =>
                {
                    // point.PrimaryValue содержит значение Y
                    return FormatBytes((long)point.Model);
                } },
            };
        }
    }
    
    static string FormatBytes(long bytes)
    {
        // Удобное форматирование
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
        try
        {
            using var client = new HttpClient();
            string json = await client.GetStringAsync("https://api.myip.com");
            var info = JsonSerializer.Deserialize<IpResponse>(json);

            if (info != null)
            {
                string flagEmoji = CountryCodeToEmoji(info.cc);
                IpInfo.Text = $"Ваш IP: {info.ip} {info.cc}";
            }
        }
        catch (Exception ex)
        {
            IpInfo.Text = "❌ Ошибка: " + ex.Message;
        }
        
        if (!iss)
        {
            MainBtn.IsEnabled = false;
            MainBtnIcon.Animation = MaterialIconAnimation.FadeInOut;
            nigga().Wait();
            MainBtnIcon.Animation = MaterialIconAnimation.Spin;
            iss = true;
            MainBtn.IsEnabled = true;
        }
        else
        {
            MainBtn.IsEnabled = false;
            MainBtnIcon.Animation = MaterialIconAnimation.FadeInOut;
            nigga().Wait();
            MainBtnIcon.Animation = MaterialIconAnimation.None;
            iss = false;
            MainBtn.IsEnabled = true;
        }
    }

    private static string CountryCodeToEmoji(string countryCode)
    {
        if (string.IsNullOrEmpty(countryCode))
            return "🏳️";

        countryCode = countryCode.ToUpper();
        int offset = 0x1F1E6 - 'A';
        char first = (char)(countryCode[0] + offset);
        char second = (char)(countryCode[1] + offset);
        return $"{first}{second}";
    }
    
    private async Task nigga()
    {
        Thread.Sleep(1);
    }
}

public class IpResponse
{
    public string ip { get; set; }
    public string country { get; set; }
    public string cc { get; set; } // country code (например "US")
}