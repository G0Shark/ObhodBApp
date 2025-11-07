using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ObhodBApp.Pages;

public partial class Settings : UserControl
{
    public SettingsViewModel currentSettings { get; set; }

    public Settings()
    {
        InitializeComponent();
        currentSettings = SettingsViewModel.Load();
        DataContext = currentSettings;
    }

    private void Save(object? sender, RoutedEventArgs e)
    {
        currentSettings.Save();
    }
}