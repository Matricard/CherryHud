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

    public UpdateViewModel(UpdateService? updateService = null, ISettingsService? settingsService = null)
    {
        _updateService = updateService ?? new UpdateService();
        _settingsService = settingsService ?? new SettingsService();
    }

    [RelayCommand]
    public async Task CheckUpdatesAsync()
    {
        IsLoading = true;
        HasUpdate = false;
        IsUpToDate = false;
        HasError = false;
        StatusTitle = "Recherche de mises à jour...";
        StatusMessage = "Vérification de la dernière version sur GitHub...";

        string repo = _settingsService.Current.GitHubRepo;
        var info = await _updateService.CheckForUpdatesAsync(repo);

        IsLoading = false;
        CurrentVersionText = $"Version actuelle : {info.CurrentVersion}";
        LatestVersionText = $"Dernière version : {info.LatestVersion}";
        ReleaseTitle = info.ReleaseTitle;
        Changelog = !string.IsNullOrWhiteSpace(info.Changelog) ? info.Changelog : "Aucune note de version fournie.";
        DownloadUrl = info.DownloadUrl;
        ReleaseUrl = info.ReleaseUrl;

        if (info.AssetSizeBytes > 0 && !string.IsNullOrEmpty(info.AssetName))
        {
            double mb = info.AssetSizeBytes / (1024.0 * 1024.0);
            AssetInfoText = $"{info.AssetName} ({mb:F1} Mo)";
        }
        else
        {
            AssetInfoText = info.AssetName;
        }

        if (info.HasError)
        {
            HasError = true;
            StatusTitle = "Vérification terminée";
            StatusMessage = info.ErrorMessage ?? "Impossible de récupérer les informations de mise à jour.";
        }
        else if (info.IsUpdateAvailable)
        {
            HasUpdate = true;
            StatusTitle = "Mise à jour disponible !";
            StatusMessage = $"Une nouvelle version de CherryHud (v{info.LatestVersion}) est prête au téléchargement.";
        }
        else
        {
            IsUpToDate = true;
            StatusTitle = "CherryHud est à jour !";
            StatusMessage = $"Vous disposez de la version la plus récente (v{info.CurrentVersion}).";
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
        CloseAction?.Invoke();
    }
}
