using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MacTemp.Services;
using MacTemp.ViewModels;

namespace MacTemp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen != null)
            {
                // Positionne dans le coin supérieur gauche absolu de l'écran (sans offset barre macOS)
                int x = screen.Bounds.X + 8;
                int y = screen.Bounds.Y + 8;
                Position = new PixelPoint(x, y);
            }

            var handle = TryGetPlatformHandle()?.Handle;
            if (handle.HasValue)
            {
                FpsService.ConfigureOverlayWindow(handle.Value);
            }
        };

        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        };

        Closed += (_, _) =>
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        };
    }
}