using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ObhodBApp.Pages;

public partial class ErrorMiddleware : Window
{
    public ErrorMiddleware(Exception ex)
    {
        InitializeComponent();
        
        BaseDescription.Text = ex.Message;
        FullInfo.Text = ex.StackTrace;
    }
}