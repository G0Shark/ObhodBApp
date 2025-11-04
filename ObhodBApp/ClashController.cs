using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;

namespace ObhodBApp;

public class ClashController
{
    public MainWindow window;
    public Process clash;

    private string mainDir;

    public ClashController(MainWindow window)
    {
        this.window = window;

        mainDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        FormatLogLine("time=\"2025-11-04T16:07:17.832319100+03:00\" level=info msg=\"[TCP] 198.18.0.1:56792(EpicGamesLauncher.exe) --> datarouter.ol.epicgames.com:443 match Match using DIRECT\"");
    }
    
    public void Start()
    {
        ProcessStartInfo clashsi = new ProcessStartInfo
        {
            FileName = $"{Path.GetFullPath($"{mainDir}clash/clash.exe")}",
            Arguments = $"-d {Path.GetFullPath($"{mainDir}clash")}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        clash = new Process
        {
            StartInfo = clashsi,
            EnableRaisingEvents = true
        };
        
        clash.OutputDataReceived += async (sender, args) => { await Dispatcher.UIThread.InvokeAsync(() => { FormatAndOut(args.Data); }); };
        clash.ErrorDataReceived += async (sender, args) => { await Dispatcher.UIThread.InvokeAsync(() => { FormatAndOut(args.Data); }); };
    }

    private void FormatAndOut(string logLine)
    {
        var result = ParseDefaultClashLogs(logLine);

        if (result.Level != "info")
        {
            AddConsoleLine("[ ", Colors.White);
            AddConsoleLine(result.Level.ToUpper(), Colors.Red);
            AddConsoleLine(" ] " + result.Msg + "\n", Colors.White);
            return;
        }
        
        FormatLogLine(result.Msg);
    }

    void FormatLogLine(string line)
    {
        // Парсим лог
        string pattern = @"\[(TCP|UDP)\] \d+\.\d+\.\d+\.\d+:\d+\(([^)]+)\) --> ([^ ]+) match (\w+)\(([^)]+)\)(?: using (\w+))?(?:\[(.+)\])?";
        var match = Regex.Match(line, pattern);

        if (!match.Success) ; //TODO: Логи создать для указания проблем

        string protocol = match.Groups[1].Value;
        string program = match.Groups[2].Value;
        string destination = match.Groups[3].Value;
        string ruleType = match.Groups[4].Value;       // ProcessName, DomainKeyword, DomainSuffix и т.д.
        string ruleValue = match.Groups[5].Value;      // имя процесса или домен
        string route = match.Groups[6].Success ? match.Groups[6].Value : "DIRECT"; // маршрут
        string extra = match.Groups[7].Success ? match.Groups[7].Value : "";

        string ruleInfo = ruleType;
        if (!string.IsNullOrEmpty(extra))
            ruleInfo += $"\\{extra}";

        AddConsoleLine("[ ", Colors.White);
        if (protocol == "TCP") AddConsoleLine("TCP", Colors.Cyan); else AddConsoleLine("UDP", Colors.DarkCyan);
        AddConsoleLine(" : ", Colors.White);
        if (route == "DIRECT") AddConsoleLine("DIRECT", Colors.Orange); else AddConsoleLine("NGPN", Colors.Green);
        AddConsoleLine(" ] " + program + " --> " + destination, Colors.White);
        AddConsoleLine($" ({ruleInfo})\n", Colors.Gray);
    }
    
    public static (string Time, string Level, string Msg) ParseDefaultClashLogs(string log)
    {
        // Регулярное выражение для разбора строки
        var regex = new Regex(@"time=""(?<time>.*?)""\s+level=(?<level>\w+)\s+msg=""(?<msg>.*)""");
        var match = regex.Match(log);
        
        if (match.Success)
        {
            return (
                match.Groups["time"].Value,
                match.Groups["level"].Value,
                match.Groups["msg"].Value
            );
        }
        else
        {
            return (null, null, null);
        }
    }
    
    public void AddConsoleLine(string text, Color color)
    {
        var run = new Run
        {
            Text = text,
            Foreground = new SolidColorBrush(color)
        };
        window.ConsoleTextBlock.Inlines.Add(run);

        while (window.ConsoleTextBlock.Inlines.Count > 1000)
        {
            window.ConsoleTextBlock.Inlines.RemoveAt(0);
        }
        
        if (window.ConsoleTextBlock.Parent is ScrollViewer scroll)
        {
            // Подписываемся на LayoutUpdated один раз
            void OnLayoutUpdated(object? sender, EventArgs e)
            {
                scroll.ScrollToEnd();   // Прокрутка вниз
                window.ConsoleTextBlock.LayoutUpdated -= OnLayoutUpdated;
            }

            window.ConsoleTextBlock.LayoutUpdated += OnLayoutUpdated;
        }
    }
}