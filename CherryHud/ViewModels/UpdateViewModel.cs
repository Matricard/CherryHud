using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CherryHud.Services;

namespace CherryHud.ViewModels;

public partial class UpdateViewModel : ViewModelBase
{
    private readonly UpdateService _updateService;
    private readonly ISettingsService _settingsService;

    public Action? CloseAction { get; set; }
    public LocalizationService Loc => LocalizationService.Instance;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _statusTitle = "Recherche de mises à jour...";

    [ObservableProperty]
    private string _statusMessage = "Connexion aux serveurs GitHub...";

    [ObservableProperty]
    private string _currentVersionText = $"Version actuelle : {UpdateService.GetCurrentVersion()}";

    [ObservableProperty]
    private string _latestVersionText = string.Empty;

    [ObservableProperty]
    private string _releaseTitle = string.Empty;

    [ObservableProperty]
    private string _changelog = string.Empty;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private bool _isUpToDate;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _downloadUrl;

    [ObservableProperty]
    private string? _releaseUrl;

    [ObservableProperty]
    private string? _assetInfoText;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadProgressText = string.Empty;

    private UpdateInfo? _latestUpdateInfo;
    private System.Threading.CancellationTokenSource? _downloadCts;

    public UpdateViewModel(UpdateService? updateService = null, ISettingsService? settingsService = null)
    {
        _updateService = updateService ?? new UpdateService();
        _settingsService = settingsService ?? new SettingsService();
        Loc.LanguageChanged += (_, _) => RefreshLocalizedState();
        RefreshLocalizedState();
    }

    private void RefreshLocalizedState()
    {
        bool fr = Loc.IsFrench;
        string curVer = _latestUpdateInfo?.CurrentVersion ?? UpdateService.GetCurrentVersion();
        CurrentVersionText = fr ? $"Version actuelle : {curVer}" : $"Current version: {curVer}";

        if (_latestUpdateInfo != null)
        {
            LatestVersionText = fr ? $"Dernière version : {_latestUpdateInfo.LatestVersion}" : $"Latest version: {_latestUpdateInfo.LatestVersion}";

            if (_latestUpdateInfo.AssetSizeBytes > 0 && !string.IsNullOrEmpty(_latestUpdateInfo.AssetName))
            {
                AssetInfoText = $"{(fr ? "Fichier disponible : " : "Available package: ")}{_latestUpdateInfo.AssetName} ({UpdateService.FormatBytes(_latestUpdateInfo.AssetSizeBytes)})";
            }
            else
            {
                AssetInfoText = _latestUpdateInfo.AssetName;
            }

            if (string.IsNullOrWhiteSpace(_latestUpdateInfo.Changelog))
            {
                Changelog = fr ? "Aucune note de version fournie." : "No release notes provided.";
            }

            if (HasError)
            {
                StatusTitle = fr ? "Vérification terminée" : "Check completed";
                StatusMessage = _latestUpdateInfo.ErrorMessage ?? (fr ? "Impossible de récupérer les informations de mise à jour." : "Unable to retrieve update information.");
            }
            else if (HasUpdate)
            {
                StatusTitle = fr ? "Mise à jour disponible !" : "Update Available!";
                StatusMessage = fr
                    ? $"Une nouvelle version de CherryHud (v{_latestUpdateInfo.LatestVersion}) est prête à être installée."
                    : $"A new version of CherryHud (v{_latestUpdateInfo.LatestVersion}) is ready to be installed.";
            }
            else if (IsUpToDate)
            {
                StatusTitle = fr ? "CherryHud est à jour !" : "CherryHud is up to date!";
                StatusMessage = fr
                    ? $"Vous disposez de la version la plus récente (v{_latestUpdateInfo.CurrentVersion})."
                    : $"You have the latest version (v{_latestUpdateInfo.CurrentVersion}).";
            }
        }
        else if (IsLoading)
        {
            StatusTitle = fr ? "Recherche de mises à jour..." : "Checking for updates...";
            StatusMessage = fr ? "Vérification de la dernière version sur GitHub..." : "Checking for latest release on GitHub...";
        }
        else if (IsDownloading)
        {
            StatusTitle = fr ? "Mise à jour en cours..." : "Updating CherryHud...";
            StatusMessage = fr ? "Téléchargement et installation automatique de CherryHud..." : "Downloading and installing CherryHud automatically...";
        }
    }

    [RelayCommand]
    public async Task CheckUpdatesAsync()
    {
        bool fr = Loc.IsFrench;
        IsLoading = true;
        IsDownloading = false;
        HasUpdate = false;
        IsUpToDate = false;
        HasError = false;
        StatusTitle = fr ? "Recherche de mises à jour..." : "Checking for updates...";
        StatusMessage = fr ? "Vérification de la dernière version sur GitHub..." : "Checking for latest release on GitHub...";

        string repo = _settingsService.Current.GitHubRepo;
        var info = await _updateService.CheckForUpdatesAsync(repo);
        _latestUpdateInfo = info;

        IsLoading = false;
        CurrentVersionText = fr ? $"Version actuelle : {info.CurrentVersion}" : $"Current version: {info.CurrentVersion}";
        LatestVersionText = fr ? $"Dernière version : {info.LatestVersion}" : $"Latest version: {info.LatestVersion}";
        ReleaseTitle = info.ReleaseTitle;
        Changelog = !string.IsNullOrWhiteSpace(info.Changelog) ? info.Changelog : (fr ? "Aucune note de version fournie." : "No release notes provided.");
        DownloadUrl = info.DownloadUrl;
        ReleaseUrl = info.ReleaseUrl;

        if (info.AssetSizeBytes > 0 && !string.IsNullOrEmpty(info.AssetName))
        {
            AssetInfoText = $"{(fr ? "Fichier disponible : " : "Available package: ")}{info.AssetName} ({UpdateService.FormatBytes(info.AssetSizeBytes)})";
        }
        else
        {
            AssetInfoText = info.AssetName;
        }

        if (info.HasError)
        {
            HasError = true;
            StatusTitle = fr ? "Vérification terminée" : "Check completed";
            StatusMessage = info.ErrorMessage ?? (fr ? "Impossible de récupérer les informations de mise à jour." : "Unable to retrieve update information.");
        }
        else if (info.IsUpdateAvailable)
        {
            HasUpdate = true;
            StatusTitle = fr ? "Mise à jour disponible !" : "Update Available!";
            StatusMessage = fr
                ? $"Une nouvelle version de CherryHud (v{info.LatestVersion}) est prête à être installée."
                : $"A new version of CherryHud (v{info.LatestVersion}) is ready to be installed.";
        }
        else
        {
            IsUpToDate = true;
            StatusTitle = fr ? "CherryHud est à jour !" : "CherryHud is up to date!";
            StatusMessage = fr
                ? $"Vous disposez de la version la plus récente (v{info.CurrentVersion})."
                : $"You have the latest version (v{info.CurrentVersion}).";
        }
    }

    [RelayCommand]
    public async Task InstallUpdateAsync()
    {
        if (_latestUpdateInfo == null || !_latestUpdateInfo.IsUpdateAvailable)
        {
            DownloadOrOpen();
            return;
        }

        bool fr = Loc.IsFrench;
        IsDownloading = true;
        HasUpdate = false;
        HasError = false;
        DownloadProgress = 0;
        DownloadProgressText = fr ? "Préparation du téléchargement..." : "Preparing download...";
        StatusTitle = fr ? "Mise à jour en cours..." : "Updating CherryHud...";
        StatusMessage = fr ? "Téléchargement et installation automatique de CherryHud..." : "Downloading and installing CherryHud automatically...";

        _downloadCts = new System.Threading.CancellationTokenSource();

        var progress = new Progress<(double percent, long downloadedBytes, long totalBytes, string statusText)>(p =>
        {
            DownloadProgress = p.percent * 100.0;
            DownloadProgressText = p.statusText;
        });

        try
        {
            await _updateService.DownloadAndInstallUpdateAsync(_latestUpdateInfo, progress, _downloadCts.Token);
        }
        catch (OperationCanceledException)
        {
            IsDownloading = false;
            HasUpdate = true;
            StatusTitle = fr ? "Mise à jour annulée" : "Update cancelled";
            StatusMessage = fr ? "Le téléchargement a été annulé." : "The download was cancelled.";
        }
        catch (Exception ex)
        {
            IsDownloading = false;
            HasUpdate = false;
            HasError = true;
            StatusTitle = fr ? "Erreur de mise à jour" : "Update error";
            StatusMessage = fr ? $"Impossible d'installer la mise à jour : {ex.Message}" : $"Failed to install update: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DownloadOrOpen()
    {
        string? targetUrl = DownloadUrl ?? ReleaseUrl;
        if (!string.IsNullOrEmpty(targetUrl))
        {
            UpdateService.OpenUrl(targetUrl);
        }
    }

    [RelayCommand]
    public void OpenRelease()
    {
        if (!string.IsNullOrEmpty(ReleaseUrl))
        {
            UpdateService.OpenUrl(ReleaseUrl);
        }
    }

    [RelayCommand]
    public void Close()
    {
        _downloadCts?.Cancel();
        CloseAction?.Invoke();
    }
}
