using System;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ObhodBApp;

//Нацелен на логи программы, не всего
//для клэша будет отдельный логгер в ClashController.cs
public static class Logger
{
    public static TextBlock? LogBlock;

    public static void WriteLog(string? name, string level, string message, Color color, Color? nameColor)
    {
        Log($"[{level}]: {message}");
        
        Write("[ ", Colors.White);
        if (name != null && nameColor != null)
        {
            Write(name, (Color)nameColor);
            Write(" : ", Colors.White);
        }
        Write(level, color);
        Write(" ] " + message + "\n", Colors.White);
    }

    public static void Log(string text, string logtype = "log")
    {
        string mainDir = AppContext.BaseDirectory;
        
        if (!Directory.Exists($"{mainDir}\\logs"))
            Directory.CreateDirectory($"{mainDir}\\logs");
        
        File.AppendAllText($"{mainDir}\\logs\\{logtype}.txt", $"{DateTime.Now} | {text}\n");
    }
    
    private static void Write(string text, Color color)
    {
        if (LogBlock == null)
            return;
        
        var run = new Run
        {
            Text = text,
            Foreground = new SolidColorBrush(color)
        };
        LogBlock.Inlines.Add(run);

        while (LogBlock.Inlines.Count > 1000)
        {
            LogBlock.Inlines.RemoveAt(0);
        }
        
        if (LogBlock.Parent is ScrollViewer scroll)
        {
            void OnLayoutUpdated(object? sender, EventArgs e)
            {
                scroll.ScrollToEnd();
                LogBlock.LayoutUpdated -= OnLayoutUpdated;
            }

            LogBlock.LayoutUpdated += OnLayoutUpdated;
        }
    }
}