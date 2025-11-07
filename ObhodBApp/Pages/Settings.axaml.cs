using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ObhodBApp.Pages;

public partial class Settings : UserControl
{
    public AppSettings CurrentAppSettings { get; set; }

    public Settings()
    {
        InitializeComponent();
        CurrentAppSettings = AppSettings.Load();
        DataContext = CurrentAppSettings;
    }

    private void Save(object? sender, RoutedEventArgs e)
    {
        CurrentAppSettings.Save();
        var window = VisualRoot as MainWindow;
        window._appSettings = CurrentAppSettings;
    }
}