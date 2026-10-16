using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MacTemp.Services;

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
    private static extern void FpsMonitor_RequestPermission();

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_Start();

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_Stop();

    [DllImport(LibraryName)]
    private static extern double FpsMonitor_GetFps();

    [DllImport(LibraryName)]
    private static extern IntPtr FpsMonitor_GetTrackedAppName();

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_GetGpuStats(out double outUtilization, out double outVramBytes);

    [DllImport(LibraryName)]
    private static extern void FpsMonitor_GetBatteryStats(out int outPercent, [MarshalAs(UnmanagedType.I1)] out bool outIsCharging);

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

    public FpsData GetFpsData()
    {
        try
        {
            bool hasPerm = FpsMonitor_HasPermission();
            double fps = FpsMonitor_GetFps();
            IntPtr ptr = FpsMonitor_GetTrackedAppName();
            string appName = ptr != IntPtr.Zero ? Marshal.PtrToStringUTF8(ptr) ?? "" : "";

            FpsMonitor_GetGpuStats(out double gpuUsage, out double vramBytes);
            FpsMonitor_GetBatteryStats(out int batPercent, out bool isCharging);

            double vramGb = vramBytes / (1024.0 * 1024.0 * 1024.0);
            int? battery = batPercent >= 0 ? batPercent : null;

            return new FpsData(fps, appName, hasPerm, gpuUsage, vramGb, battery, isCharging);
        }
        catch
        {
            return new FpsData(0.0, "", false, 0.0, 0.0, null, false);
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
