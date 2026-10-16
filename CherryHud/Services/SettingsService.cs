using System;
using System.IO;
using System.Text.Json;

namespace CherryHud.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
    event EventHandler<AppSettings>? SettingsChanged;
}

public class SettingsService : ISettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CherryHud");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Current { get; private set; }

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService()
    {
        Current = Load();
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // Fallback to defaults
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
            }

            string json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(SettingsPath, json);
            SettingsChanged?.Invoke(this, Current);
        }
        catch
        {
            // Ignore write errors gracefully
        }
    }
}
