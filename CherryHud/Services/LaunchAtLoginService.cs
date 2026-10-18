using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CherryHud.Services;

public static class LaunchAtLoginService
{
    private const string ServiceLabel = "com.matricard.cherryhud";
    private static readonly string LaunchAgentDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "LaunchAgents"
    );
    private static readonly string LaunchAgentPlistPath = Path.Combine(LaunchAgentDir, $"{ServiceLabel}.plist");

    [DllImport("libfpsmonitor.dylib", EntryPoint = "FpsMonitor_SetLaunchAtLogin")]
    private static extern int NativeSetLaunchAtLogin(int enable);

    [DllImport("libfpsmonitor.dylib", EntryPoint = "FpsMonitor_GetLaunchAtLogin")]
    private static extern int NativeGetLaunchAtLogin();

    public static bool IsLaunchAtLoginEnabled()
    {
        try
        {
            if (NativeGetLaunchAtLogin() == 1)
            {
                return true;
            }
        }
        catch
        {
            // Fallback to checking LaunchAgent plist
        }

        return File.Exists(LaunchAgentPlistPath);
    }

    public static void SetLaunchAtLogin(bool enable)
    {
        bool nativeSuccess = false;
        try
        {
            int result = NativeSetLaunchAtLogin(enable ? 1 : 0);
            if (result == 1)
            {
                nativeSuccess = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LaunchAtLogin] Native SMAppService error: {ex.Message}");
            nativeSuccess = false;
        }

        // If disabling, always clean up any legacy/fallback LaunchAgent plist
        if (!enable)
        {
            try
            {
                if (File.Exists(LaunchAgentPlistPath))
                {
                    File.Delete(LaunchAgentPlistPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LaunchAtLogin] Error deleting LaunchAgent: {ex.Message}");
            }
            return;
        }

        // If native SMAppService succeeded, clean up fallback LaunchAgent plist if it existed
        if (nativeSuccess)
        {
            if (File.Exists(LaunchAgentPlistPath))
            {
                try { File.Delete(LaunchAgentPlistPath); } catch { }
            }
            return;
        }

        // Fallback: Create LaunchAgent plist
        try
        {
            if (!Directory.Exists(LaunchAgentDir))
            {
                Directory.CreateDirectory(LaunchAgentDir);
            }

            string appPath = GetAppBundlePath() ?? "/Applications/CherryHud.app";

            string plistContent = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>{ServiceLabel}</string>
                <key>ProgramArguments</key>
                <array>
                    <string>/usr/bin/open</string>
                    <string>-a</string>
                    <string>{appPath}</string>
                </array>
                <key>RunAtLoad</key>
                <true/>
                <key>ProcessType</key>
                <string>Interactive</string>
            </dict>
            </plist>
            """;

            File.WriteAllText(LaunchAgentPlistPath, plistContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LaunchAtLogin] Error creating LaunchAgent: {ex.Message}");
        }
    }

    public static string? GetAppBundlePath()
    {
        try
        {
            var mainModule = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(mainModule))
            {
                int appIdx = mainModule.IndexOf(".app", StringComparison.OrdinalIgnoreCase);
                if (appIdx > 0)
                {
                    return mainModule.Substring(0, appIdx + 4);
                }
            }

            var baseDir = AppContext.BaseDirectory;
            int baseAppIdx = baseDir.IndexOf(".app", StringComparison.OrdinalIgnoreCase);
            if (baseAppIdx > 0)
            {
                return baseDir.Substring(0, baseAppIdx + 4);
            }

            if (Directory.Exists("/Applications/CherryHud.app"))
            {
                return "/Applications/CherryHud.app";
            }
        }
        catch
        {
        }

        return null;
    }
}
