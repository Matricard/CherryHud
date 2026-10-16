using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Media;
using CherryHud.Services;

namespace CherryHud.Views;

public partial class SettingsWindow : Window
{
    private static readonly IBrush ActiveBrush = Brush.Parse("#8006070E");
    private static readonly IBrush InactiveBrush = Brush.Parse("#F2181A24");

    public SettingsWindow()
    {
        InitializeComponent();

        Activated += (_, _) =>
        {
            WindowBackgroundBorder.Background = ActiveBrush;
        };

        Deactivated += (_, _) =>
        {
            WindowBackgroundBorder.Background = InactiveBrush;
        };

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

        // Permet de déplacer la fenêtre en cliquant sur l'en-tête ou les zones de fond
        AddHandler(PointerPressedEvent, (sender, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (e.Source is Button || (e.Source as Avalonia.Visual)?.FindAncestorOfType<Button>() != null ||
                    e.Source is ToggleSwitch || (e.Source as Avalonia.Visual)?.FindAncestorOfType<ToggleSwitch>() != null ||
                    e.Source is ComboBox || (e.Source as Avalonia.Visual)?.FindAncestorOfType<ComboBox>() != null ||
                    e.Source is Slider || (e.Source as Avalonia.Visual)?.FindAncestorOfType<Slider>() != null ||
                    e.Source is TabItem || (e.Source as Avalonia.Visual)?.FindAncestorOfType<TabItem>() != null)
                {
                    return;
                }

                BeginMoveDrag(e);
            }
        }, RoutingStrategies.Bubble);
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
