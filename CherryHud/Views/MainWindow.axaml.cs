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

                if (DataContext is MainWindowViewModel currentVm)
                {
                    currentVm.IsUserDraggingOverlay = true;
                }

                BeginMoveDrag(e);
            }
        }, RoutingStrategies.Bubble);

        AddHandler(PointerReleasedEvent, (sender, e) =>
        {
            FinishOverlayDrag();
        }, RoutingStrategies.Bubble);

        AddHandler(PointerCaptureLostEvent, (sender, e) =>
        {
            FinishOverlayDrag();
        }, RoutingStrategies.Bubble);

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.NotifyOverlayDragFinished = () => Dispatcher.UIThread.Post(FinishOverlayDrag);

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
                        if (vm.IsUserDraggingOverlay) return;

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
                        if (vm.IsUserDraggingOverlay) return;

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

    private void FinishOverlayDrag()
    {
        if (DataContext is MainWindowViewModel vm && vm.IsUserDraggingOverlay)
        {
            vm.IsUserDraggingOverlay = false;

            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            int scrX = screen?.Bounds.X ?? 0;
            int scrY = screen?.Bounds.Y ?? 0;
            vm.SaveCustomPosition(Position.X, Position.Y, vm.ActiveGameBounds, scrX, scrY);
        }
    }

    public void SetOverlayPosition(OverlayCorner corner, GameWindowBounds? gameBounds = null)
    {
        var vm = DataContext as MainWindowViewModel;
        if (vm != null && vm.IsUserDraggingOverlay)
        {
            return;
        }

        gameBounds ??= vm?.ActiveGameBounds;

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

        int x;
        int y;

        if (corner == OverlayCorner.Custom)
        {
            if (gameBounds.HasValue)
            {
                x = targetX + (vm?.CustomWindowOffsetX ?? margin);
                y = targetY + (vm?.CustomWindowOffsetY ?? topOffset);
            }
            else
            {
                x = targetX + (vm?.CustomScreenOffsetX ?? margin);
                y = targetY + (vm?.CustomScreenOffsetY ?? margin);
            }
        }
        else
        {
            x = corner switch
            {
                OverlayCorner.TopRight => targetX + targetW - winW - margin,
                OverlayCorner.BottomLeft => targetX + margin,
                OverlayCorner.BottomRight => targetX + targetW - winW - margin,
                _ => targetX + margin
            };

            y = corner switch
            {
                OverlayCorner.BottomLeft => targetY + targetH - winH - margin,
                OverlayCorner.BottomRight => targetY + targetH - winH - margin,
                _ => targetY + topOffset
            };
        }

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