using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CherryHud.Services;

public sealed class FpsService : IFpsService, IDisposable
{
    private const string LibraryName = "fpsmonitor";

    static FpsService()
    {
        NativeLibrary.SetDllImportResolver(typeof(FpsService).Assembly, DllImportResolver);
    }

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == LibraryName)
        {
            string baseDir = AppContext.BaseDirectory;
            string localPath = Path.Combine(baseDir, "libfpsmonitor.dylib");
            if (File.Exists(localPath) && NativeLibrary.TryLoad(localPath, out IntPtr handle))
            {
                return handle;
            }

            // Dev / repo relative path fallback
            string repoNativePath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Native", "libfpsmonitor.dylib"));
            if (File.Exists(repoNativePath) && NativeLibrary.TryLoad(repoNativePath, out handle))
            {
                return handle;
            }

            if (NativeLibrary.TryLoad("libfpsmonitor.dylib", assembly, searchPath, out handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    [DllImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool FpsMonitor_HasPermission();

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_CheckPermission();

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_RequestPermission();

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_Start();

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_Stop();

    [DllImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool FpsMonitor_IsGameActive();

    [DllImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool FpsMonitor_IsLaptop();

    [DllImport(LibraryName)]
    private static extern double FpsMonitor_GetFps();

    [DllImport(LibraryName)]
    private static extern double FpsMonitor_GetCurrentFrametime();

    [DllImport(LibraryName)]
    private static extern double FpsMonitor_GetPeakFrametime();

    [DllImport(LibraryName)]
    private static extern IntPtr FpsMonitor_GetTrackedAppName();

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_GetGpuStats(out double outUtilization, out double outVramBytes);

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_GetBatteryStats(out int outPercent, [MarshalAs(UnmanagedType.I1)] out bool outIsCharging);

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_ConfigureOverlayWindow(IntPtr viewOrWindowPtr);

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_SetOverlayVisible(IntPtr viewOrWindowPtr, [MarshalAs(UnmanagedType.I1)] bool visible);

    public static void ConfigureOverlayWindow(IntPtr viewOrWindowPtr)
    {
        try
        {
            FpsMonitor_ConfigureOverlayWindow(viewOrWindowPtr);
        }
        catch
        {
            // Fallback gracefully if native method unavailable
        }
    }

    public static void SetOverlayVisible(IntPtr viewOrWindowPtr, bool visible)
    {
        try
        {
            FpsMonitor_SetOverlayVisible(viewOrWindowPtr, visible);
        }
        catch
        {
        }
    }

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_SetApplicationIcon([MarshalAs(UnmanagedType.LPUTF8Str)] string iconPath);

    public static void SetApplicationIcon(string iconPath)
    {
        try
        {
            if (!string.IsNullOrEmpty(iconPath))
            {
                FpsMonitor_SetApplicationIcon(iconPath);
            }
        }
        catch
        {
        }
    }

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_ShowAboutWindow(IntPtr windowPtr, [MarshalAs(UnmanagedType.LPUTF8Str)] string iconPath);

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_MinimizeWindow(IntPtr windowPtr);

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_HideAboutWindow();

    public static void ShowAboutWindow(IntPtr windowPtr, string iconPath)
    {
        try
        {
            FpsMonitor_ShowAboutWindow(windowPtr, iconPath ?? string.Empty);
        }
        catch
        {
        }
    }

    public static void MinimizeWindow(IntPtr windowPtr)
    {
        try
        {
            FpsMonitor_MinimizeWindow(windowPtr);
        }
        catch
        {
        }
    }

    public static void HideAboutWindow()
    {
        try
        {
            FpsMonitor_HideAboutWindow();
        }
        catch
        {
        }
    }

    public static string EnsureIconPath()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string[] candidates = [
                Path.Combine(baseDir, "Assets", "CherryHud.icns"),
                Path.Combine(baseDir, "Assets", "logo.png"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Assets", "CherryHud.icns")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Assets", "logo.png")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "CherryHud", "Assets", "CherryHud.icns")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "CherryHud", "Assets", "logo.png"))
            ];

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Extract from Avalonia embedded resource to temp if not found on disk
            string tempIcon = Path.Combine(Path.GetTempPath(), "cherryhud_app_logo.png");
            if (File.Exists(tempIcon)) return tempIcon;

            var uri = new Uri("avares://CherryHud/Assets/logo.png");
            if (Avalonia.Platform.AssetLoader.Exists(uri))
            {
                using var stream = Avalonia.Platform.AssetLoader.Open(uri);
                using var fileStream = File.Create(tempIcon);
                stream.CopyTo(fileStream);
                return tempIcon;
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private bool _isStarted;
    private bool _isDisposed;

    public void Start()
    {
        if (_isStarted) return;
        _isStarted = true;
        try
        {
            FpsMonitor_Start();
        }
        catch
        {
            // Ignore if library cannot be invoked
        }
    }

    public void Stop()
    {
        if (!_isStarted) return;
        _isStarted = false;
        try
        {
            FpsMonitor_Stop();
        }
        catch
        {
            // Ignore
        }
    }

    public void RequestPermission()
    {
        try
        {
            FpsMonitor_RequestPermission();
        }
        catch
        {
            // Ignore
        }
    }

    public void CheckPermission()
    {
        try
        {
            FpsMonitor_CheckPermission();
        }
        catch
        {
            // Ignore
        }
    }

    public (bool HasPermission, double Fps, double CurrentFrametimeMs, double PeakFrametimeMs) GetFrametimeSample()
    {
        try
        {
            return (
                FpsMonitor_HasPermission(),
                FpsMonitor_GetFps(),
                FpsMonitor_GetCurrentFrametime(),
                FpsMonitor_GetPeakFrametime()
            );
        }
        catch
        {
            return (false, 0.0, 0.0, 0.0);
        }
    }

    public bool IsGameActive
    {
        get
        {
            try
            {
                return FpsMonitor_IsGameActive();
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool IsLaptop()
    {
        try
        {
            return FpsMonitor_IsLaptop();
        }
        catch
        {
            return false;
        }
    }

    public bool IsLaptopHardware => IsLaptop();
    bool IFpsService.IsLaptop => IsLaptop();

    public FpsData GetFpsData()
    {
        try
        {
            bool hasPerm = FpsMonitor_HasPermission();
            bool isGameActive = FpsMonitor_IsGameActive();
            double fps = FpsMonitor_GetFps();
            double currentFt = FpsMonitor_GetCurrentFrametime();
            double peakFt = FpsMonitor_GetPeakFrametime();

            IntPtr ptr = FpsMonitor_GetTrackedAppName();
            string appName = ptr != IntPtr.Zero ? Marshal.PtrToStringUTF8(ptr) ?? "" : "";

            FpsMonitor_GetGpuStats(out double gpuUsage, out double vramBytes);
            FpsMonitor_GetBatteryStats(out int batPercent, out bool isCharging);

            double vramGb = vramBytes / (1024.0 * 1024.0 * 1024.0);
            int? battery = batPercent >= 0 ? batPercent : null;

            return new FpsData(fps, currentFt, peakFt, appName, hasPerm, gpuUsage, vramGb, battery, isCharging, isGameActive);
        }
        catch
        {
            return new FpsData(0.0, 0.0, 0.0, "", false, 0.0, 0.0, null, false, false);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}
