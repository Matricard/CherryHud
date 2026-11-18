using System;
using System.IO;
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
                    visual.FindAncestorOfType<ScrollBar>() != null)
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

        var acBox = this.FindControl<AutoCompleteBox>("AppNameAutoCompleteBox");
        var openBtn = this.FindControl<Button>("OpenDropdownButton");

        if (acBox != null)
        {
            acBox.GotFocus += (_, _) =>
            {
                if (DataContext is ViewModels.SettingsViewModel vm)
                {
                    vm.RefreshRunningApps();
                }
            };

            acBox.PointerPressed += (_, _) =>
            {
                if (string.IsNullOrEmpty(acBox.Text))
                {
                    if (DataContext is ViewModels.SettingsViewModel vm)
                    {
                        vm.RefreshRunningApps();
                    }
                    acBox.IsDropDownOpen = true;
                }
            };

            acBox.SelectionChanged += (_, _) =>
            {
                if (acBox.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
                {
                    acBox.Text = selected;
                    if (DataContext is ViewModels.SettingsViewModel vm)
                    {
                        vm.NewAppNameInput = selected;
                    }
                    acBox.IsDropDownOpen = false;
                }
            };

            acBox.DropDownClosed += (_, _) =>
            {
                if (acBox.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
                {
                    acBox.Text = selected;
                    if (DataContext is ViewModels.SettingsViewModel vm)
                    {
                        vm.NewAppNameInput = selected;
                    }
                }
            };
        }

        if (openBtn != null && acBox != null)
        {
            openBtn.Click += (_, _) =>
            {
                if (DataContext is ViewModels.SettingsViewModel vm)
                {
                    vm.RefreshRunningApps();
                }
                acBox.IsDropDownOpen = !acBox.IsDropDownOpen;
                if (acBox.IsDropDownOpen)
                {
                    acBox.Focus();
                }
            };
        }
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
