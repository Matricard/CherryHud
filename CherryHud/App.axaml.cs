using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CherryHud.Services;
using CherryHud.ViewModels;
using CherryHud.Views;

namespace CherryHud;

public partial class App : Application
{
    private NativeMenuItem? _autoModeMenuItem;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);

            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };

            string iconPath = FpsService.EnsureIconPath();
            if (!string.IsNullOrEmpty(iconPath))
            {
                FpsService.SetApplicationIcon(iconPath);
            }

            SetupTrayMenu(vm);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayMenu(MainWindowViewModel vm)
    {
        var icons = TrayIcon.GetIcons(this);
        if (icons != null && icons.Count > 0 && icons[0].Menu is NativeMenu menu)
        {
            foreach (var item in menu.Items)
            {
                if (item is NativeMenuItem nativeItem && nativeItem.Header?.Contains("Mode Auto") == true)
                {
                    _autoModeMenuItem = nativeItem;
                    break;
                }
            }

            // Sync menu state right before menu opens on macOS
            menu.NeedsUpdate += (_, _) =>
            {
                UpdateAutoModeMenuItem(vm.IsAutoModeActive);
            };
        }

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsAutoModeActive))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateAutoModeMenuItem(vm.IsAutoModeActive);
                });
            }
        };

        UpdateAutoModeMenuItem(vm.IsAutoModeActive);
    }

    private void UpdateAutoModeMenuItem(bool isAutoActive)
    {
        if (_autoModeMenuItem != null)
        {
            _autoModeMenuItem.IsChecked = isAutoActive;
        }
    }

    private void ToggleVisibility_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            if (desktop.MainWindow.DataContext is MainWindowViewModel vm)
            {
                vm.ToggleVisibility();
                UpdateAutoModeMenuItem(vm.IsAutoModeActive);
            }
            else
            {
                desktop.MainWindow.IsVisible = !desktop.MainWindow.IsVisible;
            }
        }
    }

    private void ResetAutoMode_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
        {
            vm.ToggleAutoGameMode();
            UpdateAutoModeMenuItem(vm.IsAutoModeActive);
        }
    }

    private SettingsWindow? _settingsWindow;

    private void Settings_OnClick(object? sender, EventArgs e)
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        string iconPath = FpsService.EnsureIconPath();
        FpsService.ShowAboutWindow(IntPtr.Zero, iconPath);

        MainWindowViewModel? mainVm = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
        {
            mainVm = vm;
        }

        var settingsService = new SettingsService();
        var fpsService = new FpsService();
        var settingsVm = new SettingsViewModel(settingsService, mainVm, fpsService);

        _settingsWindow = new SettingsWindow
        {
            DataContext = settingsVm
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void Quit_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
        else
        {
            Environment.Exit(0);
        }
    }
}