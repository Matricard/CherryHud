using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
                RegisterMetricCards();
                ApplyMetricCardsOrder(vm.MetricOrder);
                vm.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(vm.MetricOrder))
                    {
                        if (!OrderMatchesContainer(vm.MetricOrder))
                        {
                            ApplyMetricCardsOrder(vm.MetricOrder);
                        }
                    }
                };
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

        // Permet de déplacer la fenêtre en cliquant sur l'en-tête (barre de titre) ou le fond direct,
        // sans interférer avec les contrôles interactifs, les menus déroulants/popups ou les feux tricolores
        AddHandler(PointerPressedEvent, (sender, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var pt = e.GetCurrentPoint(this).Position;

            // Zone des feux tricolores macOS en haut à gauche (x: 0..100, y: 0..44) :
            // Laisser macOS gérer nativement les clics sur Fermer, Réduire et Agrandir
            if (pt.Y <= 44 && pt.X <= 100)
            {
                return;
            }

            // Ne jamais déplacer la fenêtre si le clic provient d'un contrôle interactif, d'une liste ou d'un popup/dropdown
            if (e.Source is Avalonia.Visual visual)
            {
                if (visual.FindAncestorOfType<Button>() != null ||
                    visual.FindAncestorOfType<AutoCompleteBox>() != null ||
                    visual.FindAncestorOfType<TextBox>() != null ||
                    visual.FindAncestorOfType<ToggleSwitch>() != null ||
                    visual.FindAncestorOfType<ComboBox>() != null ||
                    visual.FindAncestorOfType<Slider>() != null ||
                    visual.FindAncestorOfType<TabItem>() != null ||
                    visual.FindAncestorOfType<TabControl>() != null ||
                    visual.FindAncestorOfType<ListBoxItem>() != null ||
                    visual.FindAncestorOfType<ListBox>() != null ||
                    visual.FindAncestorOfType<SelectingItemsControl>() != null ||
                    visual.FindAncestorOfType<Popup>() != null ||
                    visual.FindAncestorOfType<OverlayPopupHost>() != null ||
                    visual.FindAncestorOfType<PopupRoot>() != null ||
                    visual.FindAncestorOfType<ScrollViewer>() != null ||
                    visual.FindAncestorOfType<ScrollBar>() != null ||
                    visual.FindAncestorOfType<Border>()?.Classes.Contains("metric-card") == true)
                {
                    return;
                }
            }

            // Seul l'en-tête (barre de titre) ou le fond direct de la fenêtre permet le déplacement
            if (pt.Y <= 44 || e.Source == WindowBackgroundBorder || e.Source == this)
            {
                BeginMoveDrag(e);
            }
        }, RoutingStrategies.Bubble);

        var txtBox = this.FindControl<TextBox>("AppNameTextBox");
        if (txtBox != null)
        {
            txtBox.GotFocus += (_, _) =>
            {
                if (DataContext is ViewModels.SettingsViewModel vm)
                {
                    vm.RefreshRunningApps();
                }
            };
        }
    }

    private void OnSuggestionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control ctrl && ctrl.DataContext is string appName && !string.IsNullOrWhiteSpace(appName))
        {
            if (DataContext is ViewModels.SettingsViewModel vm)
            {
                vm.SelectRunningApp(appName);
            }

            var txtBox = this.FindControl<TextBox>("AppNameTextBox");
            txtBox?.Focus();
            e.Handled = true;
        }
    }

    private void OnSuggestionButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control ctrl && ctrl.DataContext is string appName && !string.IsNullOrWhiteSpace(appName))
        {
            if (DataContext is ViewModels.SettingsViewModel vm)
            {
                vm.SelectRunningApp(appName);
            }

            var txtBox = this.FindControl<TextBox>("AppNameTextBox");
            txtBox?.Focus();
        }
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private readonly System.Collections.Generic.Dictionary<string, Border> _metricCards = new();
    private Border? _draggedCard;
    private Point _dragStartPoint;
    private bool _isDraggingCard;

    private void RegisterMetricCards()
    {
        if (_metricCards.Count == AppSettings.DefaultMetricOrder.Length) return;

        _metricCards.Clear();
        foreach (var id in AppSettings.DefaultMetricOrder)
        {
            var card = this.FindControl<Border>($"MetricCard_{id}");
            if (card != null)
            {
                _metricCards[id] = card;
            }
        }
    }

    public void ApplyMetricCardsOrder(IEnumerable<string> order)
    {
        var container = this.FindControl<StackPanel>("MetricsCardsContainer");
        if (container == null) return;

        container.Children.Clear();
        foreach (var id in order)
        {
            if (_metricCards.TryGetValue(id, out var card))
            {
                container.Children.Add(card);
            }
        }
    }

    private bool OrderMatchesContainer(IReadOnlyList<string> order)
    {
        var container = this.FindControl<StackPanel>("MetricsCardsContainer");
        if (container == null || container.Children.Count != order.Count) return false;
        for (int i = 0; i < order.Count; i++)
        {
            if (container.Children[i] is Control c && (c.Tag as string) != order[i])
                return false;
        }
        return true;
    }

    private void SyncMetricOrderFromContainer()
    {
        var container = this.FindControl<StackPanel>("MetricsCardsContainer");
        if (container == null) return;

        var order = new System.Collections.Generic.List<string>();
        foreach (var child in container.Children)
        {
            if (child is Control c && c.Tag is string id && !string.IsNullOrEmpty(id))
            {
                order.Add(id);
            }
        }

        if (DataContext is ViewModels.SettingsViewModel vm)
        {
            vm.SaveMetricOrder(order);
        }
    }

    private void OnMetricCardDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (sender is Visual v)
        {
            var container = this.FindControl<StackPanel>("MetricsCardsContainer");
            var card = v.FindAncestorOfType<Border>();
            while (card != null && card.Parent != container)
            {
                card = (card.Parent as Visual)?.FindAncestorOfType<Border>();
            }

            if (card != null && container != null && card.Parent == container)
            {
                _draggedCard = card;
                _dragStartPoint = e.GetPosition(container);
                _isDraggingCard = false;
                e.Pointer.Capture(v as Control);
                e.Handled = true;
            }
        }
    }

    private void OnMetricCardDragHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedCard == null) return;

        var container = this.FindControl<StackPanel>("MetricsCardsContainer");
        if (container == null) return;

        var currentPoint = e.GetPosition(container);

        if (!_isDraggingCard)
        {
            if (Math.Abs(currentPoint.Y - _dragStartPoint.Y) > 3)
            {
                _isDraggingCard = true;
                _draggedCard.Opacity = 0.65;
                _draggedCard.BorderBrush = Brush.Parse("#00E5FF");
            }
        }

        if (_isDraggingCard)
        {
            int currentIndex = container.Children.IndexOf(_draggedCard);
            if (currentIndex < 0) return;

            for (int i = 0; i < container.Children.Count; i++)
            {
                if (i == currentIndex) continue;

                var child = container.Children[i] as Control;
                if (child == null || !child.IsVisible) continue;

                var bounds = child.Bounds;
                if (currentPoint.Y >= bounds.Top && currentPoint.Y <= bounds.Bottom)
                {
                    container.Children.Move(currentIndex, i);
                    SyncMetricOrderFromContainer();
                    break;
                }
            }
            e.Handled = true;
        }
    }

    private void OnMetricCardDragHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedCard != null)
        {
            _draggedCard.Opacity = 1.0;
            _draggedCard.BorderBrush = Brush.Parse("#1AFFFFFF");
            e.Pointer.Capture(null);
            _draggedCard = null;
            _isDraggingCard = false;
            SyncMetricOrderFromContainer();
            e.Handled = true;
        }
    }

    private void OnMetricMoveUpClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Visual v)
        {
            var container = this.FindControl<StackPanel>("MetricsCardsContainer");
            var card = v.FindAncestorOfType<Border>();
            while (card != null && card.Parent != container)
            {
                card = (card.Parent as Visual)?.FindAncestorOfType<Border>();
            }

            if (card != null && container != null)
            {
                int index = container.Children.IndexOf(card);
                if (index > 0)
                {
                    container.Children.Move(index, index - 1);
                    SyncMetricOrderFromContainer();
                }
            }
        }
        e.Handled = true;
    }

    private void OnMetricMoveDownClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Visual v)
        {
            var container = this.FindControl<StackPanel>("MetricsCardsContainer");
            var card = v.FindAncestorOfType<Border>();
            while (card != null && card.Parent != container)
            {
                card = (card.Parent as Visual)?.FindAncestorOfType<Border>();
            }

            if (card != null && container != null)
            {
                int index = container.Children.IndexOf(card);
                if (index >= 0 && index < container.Children.Count - 1)
                {
                    container.Children.Move(index, index + 1);
                    SyncMetricOrderFromContainer();
                }
            }
        }
        e.Handled = true;
    }
}
