using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CherryHud.Services;
using CherryHud.ViewModels;

namespace CherryHud.Views;

public partial class MainWindow : Window
{
    private IntPtr? _nativeHandle;
    private int _isPositionUpdatePending = 0;

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
                SetOverlayPosition(vm.OverlayPosition, vm.ActiveGameBounds);
                ApplyVisibility(vm.IsOverlayVisible);
            }
        };

        SizeChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                SetOverlayPosition(vm.OverlayPosition, vm.ActiveGameBounds);
            }
        };

        // Déplacement standard de la fenêtre au clic-glisser (si autorisé)
        AddHandler(PointerPressedEvent, (sender, e) =>
        {
            if (DataContext is MainWindowViewModel vm && !vm.AllowOverlayDrag)
            {
                return;
            }

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (e.Source is Button || (e.Source as Visual)?.FindAncestorOfType<Button>() != null)
                {
                    return;
                }

                BeginMoveDrag(e);
            }
        }, RoutingStrategies.Bubble);

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
                    else if (e.PropertyName == nameof(MainWindowViewModel.ActiveGameBounds))
                    {
                        if (Interlocked.Exchange(ref _isPositionUpdatePending, 1) == 0)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                _isPositionUpdatePending = 0;
                                SetOverlayPosition(vm.OverlayPosition, vm.ActiveGameBounds);
                            }, DispatcherPriority.Send);
                        }
                    }
                    else if (e.PropertyName == nameof(MainWindowViewModel.OverlayPosition)
                          || e.PropertyName == nameof(MainWindowViewModel.OverlayTheme)
                          || e.PropertyName == nameof(MainWindowViewModel.AutoTrackGameWindow))
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            SetOverlayPosition(vm.OverlayPosition, vm.ActiveGameBounds);
                        });
                    }
                };

                SetOverlayPosition(vm.OverlayPosition, vm.ActiveGameBounds);
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

    public void SetOverlayPosition(OverlayCorner corner, GameWindowBounds? gameBounds = null)
    {
        gameBounds ??= (DataContext as MainWindowViewModel)?.ActiveGameBounds;

        int margin = 8;
        int targetX, targetY, targetW, targetH;
        bool isWindowedGame = false;

        if (gameBounds.HasValue)
        {
            targetX = gameBounds.Value.X;
            targetY = gameBounds.Value.Y;
            targetW = gameBounds.Value.Width;
            targetH = gameBounds.Value.Height;

            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen != null)
            {
                // Windowed mode if it does not cover the full display
                isWindowedGame = targetW < screen.Bounds.Width || targetH < screen.Bounds.Height;
            }
            else
            {
                isWindowedGame = true;
            }
        }
        else
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen == null) return;
            targetX = screen.Bounds.X;
            targetY = screen.Bounds.Y;
            targetW = screen.Bounds.Width;
            targetH = screen.Bounds.Height;
        }

        int winW = (int)(Bounds.Width > 0 ? Bounds.Width : (double.IsNaN(Width) ? 200 : Width));
        int winH = (int)(Bounds.Height > 0 ? Bounds.Height : (double.IsNaN(Height) ? 210 : Height));

        // When tracking a windowed game with title bar and buttons, position below the 32pt title bar
        int topOffset = isWindowedGame ? (32 + margin) : margin;

        int x = corner switch
        {
            OverlayCorner.TopRight => targetX + targetW - winW - margin,
            OverlayCorner.BottomLeft => targetX + margin,
            OverlayCorner.BottomRight => targetX + targetW - winW - margin,
            _ => targetX + margin
        };

        int y = corner switch
        {
            OverlayCorner.BottomLeft => targetY + targetH - winH - margin,
            OverlayCorner.BottomRight => targetY + targetH - winH - margin,
            _ => targetY + topOffset
        };

        var newPos = new PixelPoint(x, y);
        if (Position != newPos)
        {
            Position = newPos;
        }
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