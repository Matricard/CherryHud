using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CherryHud.Services;

namespace CherryHud.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            var handle = TryGetPlatformHandle()?.Handle;
            string iconPath = FpsService.EnsureIconPath();
            FpsService.ShowAboutWindow(handle ?? IntPtr.Zero, iconPath);
        };

        Closed += (_, _) =>
        {
            FpsService.HideAboutWindow();
        };
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
