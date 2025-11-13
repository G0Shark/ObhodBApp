using System;
using System.Collections.Generic;
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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
        
        clashsi.Environment["SAFE_PATHS"] = baseDir;
        
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
                window.IsEnable = false;
            });
        };
        
        clash.Start();
        clash.BeginOutputReadLine();
        clash.BeginErrorReadLine();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            IntPtr job = CreateJobObject(IntPtr.Zero, null);

            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            IntPtr infoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(info));
            Marshal.StructureToPtr(info, infoPtr, false);

            SetInformationJobObject(job, JobObjectInfoType.JobObjectExtendedLimitInformation, infoPtr, (uint)Marshal.SizeOf(info));
            
            AssignProcessToJobObject(job, clash.Handle);
        }
        
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

    public void ReverseProxy()
    {
        string configPath = Path.GetFullPath($"{mainDir}\\clash\\config.yaml");

        string yamlText = File.ReadAllText(configPath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<Dictionary<string, object>>(yamlText);

        var rules = (config["rules"] as List<object>) ?? new List<object>();
        var newRules = new List<object>();

        foreach (var ruleObj in rules)
        {
            if (ruleObj is string rule)
            {
                if (rule.Contains("RULE-SET,my_rules,PROXY"))
                    newRules.Add(rule.Replace("PROXY", "DIRECT"));
                else if (rule.Contains("RULE-SET,my_rules,DIRECT"))
                    newRules.Add(rule.Replace("DIRECT", "PROXY"));
                else if (rule.Contains("MATCH,PROXY"))
                    newRules.Add(rule.Replace("PROXY", "DIRECT"));
                else if (rule.Contains("MATCH,DIRECT"))
                    newRules.Add(rule.Replace("DIRECT", "PROXY"));
                else
                    newRules.Add(rule);
            }
            else
            {
                newRules.Add(ruleObj);
            }
        }

        config["rules"] = newRules;

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .Build();

        string output = serializer.Serialize(config);
        File.WriteAllText(configPath, output);
    }
    
    public void UpdateImports(string rulesPath, string proxyPath)
    {
        string configFile = Path.GetFullPath($"{mainDir}\\clash\\config.yaml");
        
        string yaml = File.ReadAllText(configFile);
        
        yaml = Regex.Replace(
            yaml,
            @"(proxy-providers:\s*\r?\n\s*main:\s*\r?\n(?:.*\r?\n)*?\s*path:\s*)([^\r\n]+)",
            $"$1{proxyPath.Replace("\\", "/")}",
            RegexOptions.IgnoreCase
        );
        
        yaml = Regex.Replace(
            yaml,
            @"(rule-providers:\s*\r?\n\s*my_rules:\s*\r?\n(?:.*\r?\n)*?\s*path:\s*)([^\r\n]+)",
            $"$1{rulesPath.Replace("\\", "/")}",
            RegexOptions.IgnoreCase
        );

        File.WriteAllText(configFile, yaml);
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
        
        clashsi.Environment["SAFE_PATHS"] = baseDir;
        
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
    static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    enum JobObjectInfoType
    {
        JobObjectExtendedLimitInformation = 9
    }

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

    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
}