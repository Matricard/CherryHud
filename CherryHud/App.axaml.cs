using System;
using System.Threading.Tasks;
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
    private NativeMenuItem? _toggleVisibilityMenuItem;
    private NativeMenuItem? _autoModeMenuItem;
    private NativeMenuItem? _settingsMenuItem;
    private NativeMenuItem? _checkUpdatesMenuItem;
    private NativeMenuItem? _quitMenuItem;

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

            if (vm.SettingsService.Current.AutoCheckUpdates)
            {
                Task.Delay(5000).ContinueWith(_ =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        ShowUpdateWindow(autoCheckOnlyIfUpdate: true);
                    });
                });
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayMenu(MainWindowViewModel vm)
    {
        var icons = TrayIcon.GetIcons(this);
        if (icons != null && icons.Count > 0 && icons[0].Menu is NativeMenu menu)
        {
            if (menu.Items.Count >= 9)
            {
                _toggleVisibilityMenuItem = menu.Items[2] as NativeMenuItem;
                _autoModeMenuItem = menu.Items[3] as NativeMenuItem;
                _settingsMenuItem = menu.Items[5] as NativeMenuItem;
                _checkUpdatesMenuItem = menu.Items[6] as NativeMenuItem;
                _quitMenuItem = menu.Items[8] as NativeMenuItem;
            }

            // Sync menu state right before menu opens on macOS
            menu.NeedsUpdate += (_, _) =>
            {
                UpdateAutoModeMenuItem(vm.IsAutoModeActive);
                UpdateTrayMenuLanguage();
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

        LocalizationService.Instance.LanguageChanged += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateTrayMenuLanguage);
        };

        UpdateAutoModeMenuItem(vm.IsAutoModeActive);
        UpdateTrayMenuLanguage();
    }

    private void UpdateTrayMenuLanguage()
    {
        var loc = LocalizationService.Instance;
        if (_toggleVisibilityMenuItem != null) _toggleVisibilityMenuItem.Header = loc.TrayToggleVisibility;
        if (_autoModeMenuItem != null) _autoModeMenuItem.Header = loc.TrayAutoMode;
        if (_settingsMenuItem != null) _settingsMenuItem.Header = loc.TraySettings;
        if (_checkUpdatesMenuItem != null) _checkUpdatesMenuItem.Header = loc.TrayCheckUpdates;
        if (_quitMenuItem != null) _quitMenuItem.Header = loc.TrayQuit;
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

        MainWindowViewModel? mainVm = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
        {
            mainVm = vm;
        }

        var settingsService = mainVm?.SettingsService ?? new SettingsService();
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

    private UpdateWindow? _updateWindow;

    public void ShowUpdateWindow(bool autoCheckOnlyIfUpdate = false)
    {
        if (_updateWindow != null)
        {
            _updateWindow.Activate();
            return;
        }

        MainWindowViewModel? mainVm = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
        {
            mainVm = vm;
        }

        var settingsService = mainVm?.SettingsService ?? new SettingsService();
        var updateVm = new UpdateViewModel(new UpdateService(), settingsService);

        if (autoCheckOnlyIfUpdate)
        {
            // Background check: only show window if an update is available!
            _ = Task.Run(async () =>
            {
                var updateService = new UpdateService();
                var info = await updateService.CheckForUpdatesAsync(settingsService.Current.GitHubRepo);
                if (info.IsUpdateAvailable)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (_updateWindow == null)
                        {
                            _updateWindow = new UpdateWindow(updateVm);
                            _updateWindow.Closed += (_, _) => _updateWindow = null;
                        }
                        _updateWindow.Show();
                        _updateWindow.Activate();
                        _ = updateVm.CheckUpdatesCommand.ExecuteAsync(null);
                    });
                }
            });
            return;
        }

        _updateWindow = new UpdateWindow(updateVm);
        _updateWindow.Closed += (_, _) => _updateWindow = null;
        _updateWindow.Show();
        _updateWindow.Activate();
        _ = updateVm.CheckUpdatesCommand.ExecuteAsync(null);
    }

    private void CheckUpdates_OnClick(object? sender, EventArgs e)
    {
        ShowUpdateWindow(autoCheckOnlyIfUpdate: false);
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