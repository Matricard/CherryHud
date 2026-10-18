using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CherryHud.Services;

public class UpdateService
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    static UpdateService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CherryHud-Updater/1.0");
        HttpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
    }

    public static string GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        if (ver != null)
        {
            int build = ver.Build >= 0 ? ver.Build : 0;
            return $"{ver.Major}.{ver.Minor}.{build}";
        }
        return "1.0.0";
    }

    public static bool IsUpdateAvailable { get; private set; }
    public static string? LatestAvailableVersion { get; private set; }
    public static event Action<bool>? UpdateAvailabilityChanged;

    public static void SetUpdateAvailable(bool isAvailable, string? latestVersion = null)
    {
        bool changed = IsUpdateAvailable != isAvailable || (latestVersion != null && LatestAvailableVersion != latestVersion);
        IsUpdateAvailable = isAvailable;
        if (latestVersion != null)
        {
            LatestAvailableVersion = latestVersion;
        }
        if (changed)
        {
            UpdateAvailabilityChanged?.Invoke(isAvailable);
        }
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(string? specificRepo = null, CancellationToken ct = default)
    {
        var info = await CheckForUpdatesInternalAsync(specificRepo, ct).ConfigureAwait(false);
        SetUpdateAvailable(info.IsUpdateAvailable, info.LatestVersion);
        return info;
    }

    private async Task<UpdateInfo> CheckForUpdatesInternalAsync(string? specificRepo = null, CancellationToken ct = default)
    {
        string currentVer = GetCurrentVersion();
        var info = new UpdateInfo
        {
            CurrentVersion = currentVer,
            LatestVersion = currentVer
        };

        string primaryRepo = !string.IsNullOrWhiteSpace(specificRepo) ? specificRepo.Trim() : "Matricard/CherryHud";
        string fallbackRepo = "Matricard/MacTemp";

        string? json = null;
        string usedRepo = primaryRepo;

        try
        {
            json = await FetchReleaseJsonAsync(primaryRepo, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (primaryRepo != fallbackRepo)
            {
                try
                {
                    usedRepo = fallbackRepo;
                    json = await FetchReleaseJsonAsync(fallbackRepo, ct).ConfigureAwait(false);
                }
                catch (HttpRequestException fallbackHttpEx) when (fallbackHttpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // No releases published yet on either repository
                    info.LatestVersion = currentVer;
                    info.IsUpdateAvailable = false;
                    info.ReleaseTitle = "Aucune version publiée";
                    info.Changelog = "Aucune release n'a encore été publiée sur le dépôt GitHub. Vous utilisez la version de développement locale.";
                    return info;
                }
                catch (Exception fallbackEx)
                {
                    info.ErrorMessage = $"Impossible de contacter GitHub ({primaryRepo} / {fallbackRepo}) : {fallbackEx.Message}";
                    return info;
                }
            }
            else
            {
                info.LatestVersion = currentVer;
                info.IsUpdateAvailable = false;
                info.ReleaseTitle = "Aucune version publiée";
                info.Changelog = "Aucune release n'a encore été publiée sur le dépôt GitHub. Vous utilisez la version de développement locale.";
                return info;
            }
        }
        catch (Exception ex)
        {
            info.ErrorMessage = $"Erreur lors de la vérification : {ex.Message}";
            return info;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            info.ErrorMessage = "Réponse vide reçue de GitHub.";
            return info;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement releaseEl = default;
            bool hasRelease = false;

            if (root.ValueKind == JsonValueKind.Array)
            {
                string highestVer = "0.0.0";
                foreach (var rel in root.EnumerateArray())
                {
                    if (rel.TryGetProperty("draft", out var draftEl) && draftEl.GetBoolean())
                    {
                        continue;
                    }

                    if (rel.TryGetProperty("prerelease", out var prereleaseEl) && prereleaseEl.GetBoolean())
                    {
                        continue;
                    }

                    if (!rel.TryGetProperty("tag_name", out var tagEl))
                    {
                        continue;
                    }

                    string tag = tagEl.GetString() ?? "";
                    string v = CleanVersionString(tag);

                    if (!hasRelease)
                    {
                        releaseEl = rel;
                        highestVer = v;
                        hasRelease = true;
                    }
                    else if (IsNewerVersion(v, highestVer))
                    {
                        releaseEl = rel;
                        highestVer = v;
                    }
                }

                if (!hasRelease)
                {
                    info.LatestVersion = currentVer;
                    info.IsUpdateAvailable = false;
                    info.ReleaseTitle = LocalizationService.Instance.IsFrench ? "Aucune version publiée" : "No release published";
                    info.Changelog = LocalizationService.Instance.IsFrench
                        ? "Aucune release n'a encore été publiée sur le dépôt GitHub. Vous utilisez la version de développement locale."
                        : "No releases published yet on GitHub. You are using the local development build.";
                    return info;
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("message", out var msgElement) && msgElement.GetString() == "Not Found")
                {
                    info.ErrorMessage = LocalizationService.Instance.IsFrench
                        ? $"Aucune version publiée (Release) n'a été trouvée sur GitHub ({usedRepo})."
                        : $"No published releases found on GitHub ({usedRepo}).";
                    return info;
                }

                if (!root.TryGetProperty("tag_name", out _))
                {
                    info.ErrorMessage = LocalizationService.Instance.IsFrench
                        ? "Format de release GitHub inattendu (tag_name manquant)."
                        : "Unexpected GitHub release format (missing tag_name).";
                    return info;
                }

                if (root.TryGetProperty("prerelease", out var preEl) && preEl.GetBoolean())
                {
                    info.LatestVersion = currentVer;
                    info.IsUpdateAvailable = false;
                    info.ReleaseTitle = "Aucune version publiée";
                    info.Changelog = "Aucune release stable n'a encore été publiée sur le dépôt GitHub. Vous utilisez la version de développement locale.";
                    return info;
                }

                releaseEl = root;
                hasRelease = true;
            }
            else
            {
                info.ErrorMessage = LocalizationService.Instance.IsFrench
                    ? "Format de réponse GitHub inattendu."
                    : "Unexpected GitHub response format.";
                return info;
            }

            string rawTag = releaseEl.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "" : "";
            string cleanLatest = CleanVersionString(rawTag);
            info.LatestVersion = cleanLatest;
            info.ReleaseTitle = releaseEl.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? rawTag) : rawTag;
            if (string.IsNullOrWhiteSpace(info.ReleaseTitle)) info.ReleaseTitle = rawTag;
            info.Changelog = releaseEl.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
            info.ReleaseUrl = releaseEl.TryGetProperty("html_url", out var htmlEl) ? htmlEl.GetString() ?? "" : "";

            // Find matching release asset (e.g. .dmg or .zip)
            if (releaseEl.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
            {
                string? bestUrl = null;
                string? bestName = null;
                long bestSize = 0;

                bool isArm64 = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
                               System.Runtime.InteropServices.Architecture.Arm64;

                foreach (var asset in assetsEl.EnumerateArray())
                {
                    string aName = asset.TryGetProperty("name", out var anEl) ? anEl.GetString() ?? "" : "";
                    string aUrl = asset.TryGetProperty("browser_download_url", out var auEl) ? auEl.GetString() ?? "" : "";
                    long aSize = asset.TryGetProperty("size", out var asEl) ? asEl.GetInt64() : 0;

                    if (aName.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase) ||
                        aName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                        aName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bestUrl == null)
                        {
                            bestUrl = aUrl;
                            bestName = aName;
                            bestSize = aSize;
                        }

                        // Prefer matching architecture, prioritizing .zip for seamless in-app extraction
                        bool isZip = aName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                        bool isDmg = aName.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase);
                        bool isMatchingArch = isArm64
                            ? (aName.Contains("arm64", StringComparison.OrdinalIgnoreCase) || aName.Contains("aarch64", StringComparison.OrdinalIgnoreCase))
                            : (aName.Contains("x64", StringComparison.OrdinalIgnoreCase) || aName.Contains("x86_64", StringComparison.OrdinalIgnoreCase));

                        if (isZip && isMatchingArch)
                        {
                            bestUrl = aUrl;
                            bestName = aName;
                            bestSize = aSize;
                            break; // Perfect match for in-app update!
                        }

                        if (isDmg && isMatchingArch && (bestUrl == null || !bestName!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                        {
                            bestUrl = aUrl;
                            bestName = aName;
                            bestSize = aSize;
                        }
                        else if (isZip && bestUrl == null)
                        {
                            bestUrl = aUrl;
                            bestName = aName;
                            bestSize = aSize;
                        }
                        else if (isMatchingArch || isDmg)
                        {
                            bestUrl ??= aUrl;
                            bestName ??= aName;
                            if (bestSize == 0) bestSize = aSize;
                        }
                    }
                }

                info.DownloadUrl = bestUrl ?? info.ReleaseUrl;
                info.AssetName = bestName;
                info.AssetSizeBytes = bestSize;
            }
            else
            {
                info.DownloadUrl = info.ReleaseUrl;
            }

            info.IsUpdateAvailable = IsNewerVersion(cleanLatest, currentVer);
            return info;
        }
        catch (Exception ex)
        {
            info.ErrorMessage = LocalizationService.Instance.IsFrench
                ? $"Erreur d'analyse des données de mise à jour : {ex.Message}"
                : $"Failed to parse update data: {ex.Message}";
            return info;
        }
    }

    private static async Task<string> FetchReleaseJsonAsync(string repo, CancellationToken ct)
    {
        string url = $"https://api.github.com/repos/{repo}/releases/latest";
        using var response = await HttpClient.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    public static bool IsNewerVersion(string latestStr, string currentStr)
    {
        string cleanLatest = CleanVersionString(latestStr);
        string cleanCurrent = CleanVersionString(currentStr);

        var partsL = cleanLatest.Split('.');
        var partsC = cleanCurrent.Split('.');
        int max = Math.Max(partsL.Length, partsC.Length);
        for (int i = 0; i < max; i++)
        {
            int numL = i < partsL.Length && int.TryParse(partsL[i], out var n1) ? n1 : 0;
            int numC = i < partsC.Length && int.TryParse(partsC[i], out var n2) ? n2 : 0;
            if (numL > numC) return true;
            if (numL < numC) return false;
        }

        return false;
    }

    public static string CleanVersionString(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "0.0.0";
        v = v.Trim();

        var match = System.Text.RegularExpressions.Regex.Match(v, @"\d+(\.\d+)+");
        if (match.Success)
        {
            return match.Value;
        }

        var singleMatch = System.Text.RegularExpressions.Regex.Match(v, @"\d+");
        if (singleMatch.Success)
        {
            return singleMatch.Value;
        }

        return v;
    }

    public static string FormatBytes(long bytes, bool? isFrench = null)
    {
        bool fr = isFrench ?? LocalizationService.Instance.IsFrench;
        string k = fr ? "Ko" : "KB";
        string m = fr ? "Mo" : "MB";
        string g = fr ? "Go" : "GB";

        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " " + k;
        if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " " + m;
        return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " " + g;
    }

    public async Task DownloadAndInstallUpdateAsync(
        UpdateInfo info,
        IProgress<(double percent, long downloadedBytes, long totalBytes, string statusText)>? progress = null,
        CancellationToken ct = default)
    {
        bool fr = LocalizationService.Instance.IsFrench;
        if (string.IsNullOrWhiteSpace(info.DownloadUrl))
        {
            throw new InvalidOperationException(fr ? "Aucun lien de téléchargement direct n'est disponible." : "No direct download link available.");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "CherryHudUpdate");
        if (Directory.Exists(tempRoot))
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
        Directory.CreateDirectory(tempRoot);

        string fileName = !string.IsNullOrEmpty(info.AssetName) ? info.AssetName : "CherryHud_update.zip";
        string localFilePath = Path.Combine(tempRoot, fileName);

        progress?.Report((0, 0, info.AssetSizeBytes, fr ? "Démarrage du téléchargement..." : "Starting download..."));

        using var response = await HttpClient.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? info.AssetSizeBytes;
        using (var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            byte[] buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    double pct = (double)totalRead / totalBytes;
                    progress?.Report((pct, totalRead, totalBytes, fr
                        ? $"Téléchargement... {pct * 100:F0}% ({FormatBytes(totalRead)} / {FormatBytes(totalBytes)})"
                        : $"Downloading... {pct * 100:F0}% ({FormatBytes(totalRead)} / {FormatBytes(totalBytes)})"));
                }
                else
                {
                    progress?.Report((0, totalRead, 0, fr
                        ? $"Téléchargement... {FormatBytes(totalRead)}"
                        : $"Downloading... {FormatBytes(totalRead)}"));
                }
            }

            await fileStream.FlushAsync(ct).ConfigureAwait(false);
        }

        progress?.Report((1.0, totalBytes, totalBytes, fr ? "Extraction de la mise à jour..." : "Extracting update..."));

        string extractDir = Path.Combine(tempRoot, "extracted");
        Directory.CreateDirectory(extractDir);

        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var dittoProc = Process.Start(new ProcessStartInfo
            {
                FileName = "ditto",
                Arguments = $"-xk \"{localFilePath}\" \"{extractDir}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (dittoProc != null)
            {
                await dittoProc.WaitForExitAsync(ct).ConfigureAwait(false);
            }
        }
        else if (fileName.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase))
        {
            string mountDir = Path.Combine(tempRoot, "mount");
            Directory.CreateDirectory(mountDir);

            var attachProc = Process.Start(new ProcessStartInfo
            {
                FileName = "hdiutil",
                Arguments = $"attach \"{localFilePath}\" -mountpoint \"{mountDir}\" -nobrowse -readonly",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (attachProc != null)
            {
                await attachProc.WaitForExitAsync(ct).ConfigureAwait(false);
            }

            try
            {
                var appsOnDisk = Directory.GetDirectories(mountDir, "*.app");
                if (appsOnDisk.Length > 0)
                {
                    var cpProc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "cp",
                        Arguments = $"-R \"{appsOnDisk[0]}\" \"{extractDir}/\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    if (cpProc != null)
                    {
                        await cpProc.WaitForExitAsync(ct).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                var detachProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "hdiutil",
                    Arguments = $"detach \"{mountDir}\" -force",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (detachProc != null)
                {
                    await detachProc.WaitForExitAsync(ct).ConfigureAwait(false);
                }
            }
        }
        else
        {
            throw new NotSupportedException(fr
                ? $"Format de paquet non supporté pour l'installation automatique : {fileName}"
                : $"Unsupported package format for auto-installation: {fileName}");
        }

        var candidateApps = Directory.GetDirectories(extractDir, "*.app", SearchOption.AllDirectories);
        if (candidateApps.Length == 0)
        {
            throw new InvalidOperationException(fr
                ? "L'application CherryHud.app n'a pas été trouvée dans le paquet téléchargé."
                : "CherryHud.app was not found in the downloaded package.");
        }
        string sourceApp = candidateApps[0];

        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        string targetApp = "/Applications/CherryHud.app";
        int appIdx = exePath.IndexOf(".app/Contents/MacOS", StringComparison.OrdinalIgnoreCase);
        if (appIdx > 0)
        {
            targetApp = exePath.Substring(0, appIdx + 4);
        }

        progress?.Report((1.0, totalBytes, totalBytes, fr ? "Installation et redémarrage en cours..." : "Installing and restarting..."));

        string scriptPath = Path.Combine(tempRoot, "relaunch.sh");
        int currentPid = Environment.ProcessId;

        string script = $@"#!/bin/bash
PID={currentPid}
TARGET=""{targetApp}""
SOURCE=""{sourceApp}""
TEMP_ROOT=""{tempRoot}""

# Wait for old CherryHud process to terminate
while kill -0 $PID 2>/dev/null; do
    sleep 0.2
done

# Replace destination application
rm -rf ""$TARGET""
cp -R ""$SOURCE"" ""$TARGET""

# Remove Gatekeeper quarantine attribute
xattr -cr ""$TARGET"" 2>/dev/null || true

# Reopen the updated application
open ""$TARGET""

# Clean temporary update files
sleep 1
rm -rf ""$TEMP_ROOT""
";
        await File.WriteAllTextAsync(scriptPath, script, ct).ConfigureAwait(false);

        var chmodProc = Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"+x \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        if (chmodProc != null)
        {
            await chmodProc.WaitForExitAsync(ct).ConfigureAwait(false);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Environment.Exit(0);
    }

    public static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateService] Échec de l'ouverture du lien : {ex.Message}");
        }
    }
}
