using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ObhodBApp.Pages;

public partial class Settings : UserControl
{
    public Settings()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }
}