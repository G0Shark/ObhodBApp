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
    
    public void AddConsoleLine(string text, Color color)
    {
        var run = new Run
        {
            Text = text + "\n",
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