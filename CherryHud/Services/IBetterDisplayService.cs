using System.Collections.Generic;

namespace CherryHud.Services;

public record BetterDisplayMode(
    int ModeNumber,
    int Width,
    int Height,
    string RefreshRate,
    bool IsHiDpi,
    bool IsNative,
    bool IsCurrent,
    string RawLine)
{
    public string DisplayText =>
        $"{Width}x{Height}{(IsHiDpi ? " HiDPI" : "")}{(string.IsNullOrWhiteSpace(RefreshRate) ? "" : $" @ {RefreshRate}")}{(IsNative ? " (Native)" : "")}";
}

public interface IBetterDisplayService
{
    bool IsAvailable { get; }
    bool IsRunning { get; }
    bool IsResolutionSwapped { get; }

    string GetMainDisplayName();
    string GetCurrentResolution();
    int? GetCurrentDisplayModeNumber();
    IReadOnlyList<BetterDisplayMode> GetAvailableModes();
    BetterDisplayMode? GetNativeMode();

    bool SetDisplayMode(int modeNumber);
    bool SetResolution(string resolution);

    void OnGameStarted(AppSettings settings);
    void OnGameEnded(AppSettings settings);
    void RestoreDesktop(AppSettings settings);
}
