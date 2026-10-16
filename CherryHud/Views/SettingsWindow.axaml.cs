using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CherryHud.Services;

namespace CherryHud.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            var handle = TryGetPlatformHandle()?.Handle;
            if (handle.HasValue)
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
                if (!File.Exists(iconPath))
                {
                    iconPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "logo.png"));
                }
                FpsService.ShowAboutWindow(handle.Value, iconPath);
            }
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
