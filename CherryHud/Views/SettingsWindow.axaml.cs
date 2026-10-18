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
    private static readonly IBrush ActiveBrush = Brush.Parse("#9E030408");
    private static readonly IBrush InactiveBrush = Brush.Parse("#F613141E");

    public SettingsWindow()
    {
        InitializeComponent();

        Activated += (_, _) =>
        {
            WindowBackgroundBorder.Background = ActiveBrush;
            if (DataContext is ViewModels.SettingsViewModel vm)
            {
                vm.CheckPermissionStatus();
            }
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
            if (DataContext is ViewModels.SettingsViewModel vm)
            {
                vm.CheckPermissionStatus();
                vm.StartPermissionMonitoring();
            }
        };

        Closed += (_, _) =>
        {
            if (DataContext is ViewModels.SettingsViewModel vm)
            {
                vm.StopPermissionMonitoring();
            }
            FpsService.HideAboutWindow();
        };

        KeyDown += (_, e) =>
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta) && e.Key == Key.M)
            {
                var handle = TryGetPlatformHandle()?.Handle;
                if (handle.HasValue)
                {
                    FpsService.MinimizeWindow(handle.Value);
                }
                WindowState = WindowState.Minimized;
                e.Handled = true;
            }
        };

        // Permet de déplacer la fenêtre en cliquant sur l'en-tête ou les zones de fond,
        // sans interférer avec les feux tricolores natifs de macOS
        AddHandler(PointerPressedEvent, (sender, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var pt = e.GetCurrentPoint(this).Position;

                // Zone des feux tricolores macOS en haut à gauche (x: 0..100, y: 0..44) :
                // Laisser macOS gérer nativement les clics sur Fermer, Réduire et Agrandir
                if (pt.Y <= 44 && pt.X <= 100)
                {
                    return;
                }

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
