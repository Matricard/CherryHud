using System;
using System.Diagnostics;
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
            return $"{ver.Major}.{ver.Minor}.{ver.Build}";
        }
        return "0.9.0";
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(string? specificRepo = null, CancellationToken ct = default)
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

            if (root.TryGetProperty("message", out var msgElement) && msgElement.GetString() == "Not Found")
            {
                info.ErrorMessage = $"Aucune version publiée (Release) n'a été trouvée sur GitHub ({usedRepo}).";
                return info;
            }

            if (!root.TryGetProperty("tag_name", out var tagElement))
            {
                info.ErrorMessage = "Format de release GitHub inattendu (tag_name manquant).";
                return info;
            }

            string rawTag = tagElement.GetString() ?? "";
            string cleanLatest = CleanVersionString(rawTag);
            info.LatestVersion = cleanLatest;
            info.ReleaseTitle = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? rawTag : rawTag;
            info.Changelog = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
            info.ReleaseUrl = root.TryGetProperty("html_url", out var htmlEl) ? htmlEl.GetString() ?? "" : "";

            // Find matching release asset (e.g. .dmg or .zip)
            if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
            {
                string? bestUrl = null;
                string? bestName = null;
                long bestSize = 0;

                foreach (var asset in assetsEl.EnumerateArray())
                {
                    string aName = asset.TryGetProperty("name", out var anEl) ? anEl.GetString() ?? "" : "";
                    string aUrl = asset.TryGetProperty("browser_download_url", out var auEl) ? auEl.GetString() ?? "" : "";
                    long aSize = asset.TryGetProperty("size", out var asEl) ? asEl.GetInt64() : 0;

                    if (aName.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase) ||
                        aName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                        aName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                    {
                        bestUrl = aUrl;
                        bestName = aName;
                        bestSize = aSize;

                        // Give priority to arm64 or mac builds
                        if (aName.Contains("arm64", StringComparison.OrdinalIgnoreCase) ||
                            aName.Contains("mac", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
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
            info.ErrorMessage = $"Erreur d'analyse des données de mise à jour : {ex.Message}";
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

        if (Version.TryParse(cleanLatest, out var vLatest) && Version.TryParse(cleanCurrent, out var vCurrent))
        {
            return vLatest > vCurrent;
        }

        // Compare component by component if Version.TryParse fails
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
        if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            v = v.Substring(1);
        }
        int dashIdx = v.IndexOf('-');
        if (dashIdx > 0)
        {
            v = v.Substring(0, dashIdx);
        }
        return v.Trim();
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
