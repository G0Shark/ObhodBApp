using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;
using YamlDotNet.RepresentationModel;

namespace ObhodBApp;

public class ClashController
{
    public MainWindow window;
    public Process clash;

    private string mainDir;

    public ClashController(MainWindow window)
    {
        this.window = window;

        mainDir = AppContext.BaseDirectory;
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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            //MakeProcessKillOnParentExit(clash);
        }
        
        clash.OutputDataReceived += async (sender, args) => { await Dispatcher.UIThread.InvokeAsync(() => { FormatAndOut(args.Data??""); }); };
        clash.ErrorDataReceived += async (sender, args) => { await Dispatcher.UIThread.InvokeAsync(() => { FormatAndOut(args.Data??""); }); };
        clash.Exited += (sender, args) =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddConsoleLine("[ ", Colors.White);
                AddConsoleLine("EXIT", Colors.OrangeRed);
                AddConsoleLine(" ] " + "Программа завершила работу с кодом " + clash.ExitCode + "\n", Colors.White);
                
                window.MainBtnIcon.Foreground = Brushes.Orange;
                window.MainBtnIcon.Animation = MaterialIconAnimation.None;
                window.isEnable = false;
            });
        };
        
        Dispatcher.UIThread.Invoke(() => { clash.Start(); });
        clash.BeginOutputReadLine();
        clash.BeginErrorReadLine();
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            window.MainBtnIcon.Foreground = Brushes.White;
        });
    }

    public void Stop()
    {
        try
        {
            clash.Kill();
        }
        catch (Exception e)
        {
            // ignored
        }
    }
    
    private void FormatAndOut(string logLine)
    {
        if (logLine == "")
            return;
        
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
        string route = rest.Contains("ObhodBlokirovok") ? "PROXY" : "DIRECT";
        string ruleInfo = "";
        var ruleMatch = Regex.Match(rest, @"(?<rule>\w+\([^\)]+\))");
        if (ruleMatch.Success) ruleInfo = ruleMatch.Groups["rule"].Value;
        var filterMatch = Regex.Match(rest, @"ObhodBlokirovok\[(?<f>[^\]]+)\]");
        if (filterMatch.Success)
            ruleInfo += $" [{filterMatch.Groups["f"].Value}]";
        AddConsoleLine("[ ", Colors.White);
        AddConsoleLine(protocol, protocol == "TCP" ? Colors.Cyan : Colors.DarkCyan);
        AddConsoleLine(" : ", Colors.White);
        AddConsoleLine(route == "DIRECT" ? "DIRECT" : "PROXY", route == "DIRECT" ? Colors.Orange : Colors.Green);
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
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var run = new Run
            {
                Text = text,
                Foreground = new SolidColorBrush(color)
            };
            window.ConsoleTextBlock.Inlines.Add(run);

            while (window.ConsoleTextBlock.Inlines.Count > 1000)
                window.ConsoleTextBlock.Inlines.RemoveAt(0);

            if (window.ConsoleTextBlock.Parent is ScrollViewer scroll)
            {
                void OnLayoutUpdated(object? sender, EventArgs e)
                {
                    scroll.ScrollToEnd();
                    window.ConsoleTextBlock.LayoutUpdated -= OnLayoutUpdated;
                }

                window.ConsoleTextBlock.LayoutUpdated += OnLayoutUpdated;
            }
        });
    }

    public void UpdateImports(string rulesPath, string proxyPath)
    {
        string configPath = Path.GetFullPath($"{mainDir}\\clash\\config.yaml");
        
        var yaml = new YamlStream();
        using (var reader = new StreamReader(configPath))
            yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;

        var importNode = new YamlSequenceNode
        {
            new YamlScalarNode(proxyPath),
            new YamlScalarNode(rulesPath)
        };
        
        if (root.Children.ContainsKey("import"))
        {
            root.Children[new YamlScalarNode("import")] = importNode;
        }
        else
        {
            var newChildren = new YamlMappingNode();

            foreach (var entry in root.Children)
            {
                if (entry.Key.ToString() == "proxy-groups")
                {
                    newChildren.Add("import", importNode);
                }
                newChildren.Add(entry.Key, entry.Value);
            }
            
            root.Children.Clear();
            foreach (var entry in newChildren.Children)
            {
                root.Add(entry.Key, entry.Value);
            }
        }

        using (var writer = new StreamWriter(configPath))
            yaml.Save(writer, assignAnchors: false);
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
    
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll")]
    static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, ref JOBOBJECT_BASIC_LIMIT_INFORMATION lpJobObjectInfo, int cbJobObjectInfoLength);

    [DllImport("kernel32.dll")]
    static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public IntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    const int JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    const int JobObjectExtendedLimitInformation = 9;

    public static void MakeProcessKillOnParentExit(Process process)
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        };
        SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref info, Marshal.SizeOf<JOBOBJECT_BASIC_LIMIT_INFORMATION>());
        AssignProcessToJobObject(job, process.Handle);
    }
}