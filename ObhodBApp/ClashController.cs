using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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