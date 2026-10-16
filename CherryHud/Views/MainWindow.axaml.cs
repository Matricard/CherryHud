using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CherryHud.Services;
using CherryHud.ViewModels;

namespace CherryHud.Views;

public partial class MainWindow : Window
{
    private IntPtr? _nativeHandle;

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
                _nativeHandle = handle.Value;
                FpsService.ConfigureOverlayWindow(_nativeHandle.Value);
            }

            if (DataContext is MainWindowViewModel vm)
            {
                SetOverlayPosition(vm.OverlayPosition);
                ApplyVisibility(vm.IsOverlayVisible);
            }
        };

        SizeChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                SetOverlayPosition(vm.OverlayPosition);
            }
        };

        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        };

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MainWindowViewModel.IsOverlayVisible) ||
                        e.PropertyName == nameof(MainWindowViewModel.OverlayOpacity))
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            ApplyVisibility(vm.IsOverlayVisible);
                        });
                    }
                    else if (e.PropertyName == nameof(MainWindowViewModel.OverlayPosition))
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            SetOverlayPosition(vm.OverlayPosition);
                        });
                    }
                };

                SetOverlayPosition(vm.OverlayPosition);
                ApplyVisibility(vm.IsOverlayVisible);
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

    public void SetOverlayPosition(OverlayCorner corner)
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen == null) return;

        int margin = 8;
        int screenX = screen.Bounds.X;
        int screenY = screen.Bounds.Y;
        int screenW = screen.Bounds.Width;
        int screenH = screen.Bounds.Height;
        int winW = (int)(Bounds.Width > 0 ? Bounds.Width : (double.IsNaN(Width) ? 188 : Width));
        int winH = (int)(Bounds.Height > 0 ? Bounds.Height : (double.IsNaN(Height) ? 210 : Height));

        int x = corner switch
        {
            OverlayCorner.TopRight => screenX + screenW - winW - margin,
            OverlayCorner.BottomLeft => screenX + margin,
            OverlayCorner.BottomRight => screenX + screenW - winW - margin,
            _ => screenX + margin
        };

        int y = corner switch
        {
            OverlayCorner.BottomLeft => screenY + screenH - winH - margin,
            OverlayCorner.BottomRight => screenY + screenH - winH - margin,
            _ => screenY + margin
        };

        Position = new PixelPoint(x, y);
    }

    private void ApplyVisibility(bool visible)
    {
        double targetOpacity = DataContext is MainWindowViewModel vm ? vm.OverlayOpacity : 1.0;
        Opacity = visible ? targetOpacity : 0.0;
        IsHitTestVisible = visible;
        if (_nativeHandle.HasValue)
        {
            FpsService.SetOverlayVisible(_nativeHandle.Value, visible);
        }
    }
}