using System;

namespace CherryHud.Services;

public class UpdateInfo
{
    public bool IsUpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = "0.9.0";
    public string LatestVersion { get; set; } = "0.9.0";
    public string ReleaseTitle { get; set; } = string.Empty;
    public string Changelog { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public string? AssetName { get; set; }
    public long AssetSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}
