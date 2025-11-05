using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;

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
            FileName = $"{Path.GetFullPath($"{mainDir}\\clash\\clash.exe")}",
            Arguments = $"-d {Path.GetFullPath($"{mainDir}\\clash")}",
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
        clash.Exited += async (sender, args) =>
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddConsoleLine("[ ", Colors.White);
                AddConsoleLine("EXIT", Colors.OrangeRed);
                AddConsoleLine(" ] " + "Программа завершила работу с кодом " + clash.ExitCode + "\n", Colors.White);
                
                window.MainBtnIcon.Foreground = Brushes.Orange;
                window.MainBtnIcon.Animation = MaterialIconAnimation.None;
                window.isEnable = false;
            });
        };
        
        clash.Start();
        clash.BeginOutputReadLine();
        clash.BeginErrorReadLine();
    }

    public void Stop()
    {
        if (clash != null)
        {
            if (clash.HasExited) return;
            clash.Close();
            clash.WaitForExit();
            clash = null;
        }
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
        var m = Regex.Match(line, @"\[(?<proto>TCP|UDP)\]\s+(?<src>[^()]+)\((?<prog>[^)]+)\)\s+-->\s+(?<dst>\S+)\s+match\s+(?<rest>.+)");
        if (!m.Success) return;

        string protocol = m.Groups["proto"].Value;
        string program = m.Groups["prog"].Value;
        string destination = m.Groups["dst"].Value;
        string rest = m.Groups["rest"].Value;

        string route = rest.Contains("ObhodBlokirovok") ? "NGPN" : "DIRECT";
        string ruleInfo = "";

        var ruleMatch = Regex.Match(rest, @"(?<rule>\w+\([^\)]+\))");
        if (ruleMatch.Success) ruleInfo = ruleMatch.Groups["rule"].Value;

        var filterMatch = Regex.Match(rest, @"ObhodBlokirovok\[(?<f>[^\]]+)\]");
        if (filterMatch.Success)
            ruleInfo += $" [{filterMatch.Groups["f"].Value}]";

        // === вывод ===
        AddConsoleLine("[ ", Colors.White);
        AddConsoleLine(protocol, protocol == "TCP" ? Colors.Cyan : Colors.DarkCyan);
        AddConsoleLine(" : ", Colors.White);
        AddConsoleLine(route == "DIRECT" ? "DIRECT" : "NGPN", route == "DIRECT" ? Colors.Orange : Colors.Green);
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

    public bool TryConfig()
    {
        ProcessStartInfo clashsi = new ProcessStartInfo
        {
            FileName = $"{Path.GetFullPath($"{mainDir}\\clash\\clash.exe")}",
            Arguments = $"-d {Path.GetFullPath($"{mainDir}\\clash")} -t",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        Process testCfg = new Process
        {
            StartInfo = clashsi,
            EnableRaisingEvents = true
        };
        
        testCfg.Start();
        testCfg.WaitForExit();

        if (testCfg.StandardOutput.ReadToEnd().Contains("successful") ||
            testCfg.StandardError.ReadToEnd().Contains("successful"))
            return true;
        
        return false;
    }
}