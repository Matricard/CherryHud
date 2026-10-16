using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MacTemp.Services;
using MacTemp.ViewModels;

namespace MacTemp.Views;

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
                ApplyVisibility(vm.IsOverlayVisible);
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
                    if (e.PropertyName == nameof(MainWindowViewModel.IsOverlayVisible))
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            ApplyVisibility(vm.IsOverlayVisible);
                        });
                    }
                };

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

    private void ApplyVisibility(bool visible)
    {
        Opacity = visible ? 1.0 : 0.0;
        IsHitTestVisible = visible;
        if (_nativeHandle.HasValue)
        {
            FpsService.SetOverlayVisible(_nativeHandle.Value, visible);
        }
    }
}