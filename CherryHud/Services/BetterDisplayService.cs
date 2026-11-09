using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CherryHud.Services;

public class BetterDisplayService : IBetterDisplayService
{
    private static readonly string[] PossibleBinaryPaths = new[]
    {
        "/Applications/BetterDisplay.app/Contents/MacOS/BetterDisplay",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications/BetterDisplay.app/Contents/MacOS/BetterDisplay")
    };

    private readonly string? _binaryPath;
    private readonly Func<string, string>? _commandRunner;
    private bool _isResolutionSwapped;
    private readonly object _lock = new();

    public bool IsResolutionSwapped
    {
        get
        {
            lock (_lock)
            {
                return _isResolutionSwapped;
            }
        }
        internal set
        {
            lock (_lock)
            {
                _isResolutionSwapped = value;
            }
        }
    }

    public void SetResolutionSwappedForTesting(bool value)
    {
        lock (_lock)
        {
            _isResolutionSwapped = value;
        }
    }

    public BetterDisplayService(string? customBinaryPath = null, Func<string, string>? commandRunner = null)
    {
        _commandRunner = commandRunner;
        if (!string.IsNullOrEmpty(customBinaryPath))
        {
            _binaryPath = customBinaryPath;
        }
        else
        {
            _binaryPath = PossibleBinaryPaths.FirstOrDefault(File.Exists);
        }
    }

    public bool IsAvailable
    {
        get
        {
            if (_commandRunner != null) return true;
            return !string.IsNullOrEmpty(_binaryPath) && File.Exists(_binaryPath);
        }
    }

    public bool IsRunning
    {
        get
        {
            if (_commandRunner != null) return true; // In test environment
            if (!IsAvailable) return false;
            try
            {
                return Process.GetProcessesByName("BetterDisplay").Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    private string RunCommand(string arguments)
    {
        if (_commandRunner != null)
        {
            return _commandRunner(arguments);
        }

        if (!IsAvailable || string.IsNullOrEmpty(_binaryPath))
        {
            return string.Empty;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _binaryPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return string.Empty;

            if (!process.WaitForExit(3000))
            {
                try { process.Kill(); } catch { }
                return string.Empty;
            }

            return process.StandardOutput.ReadToEnd().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public string GetMainDisplayName()
    {
        string name = RunCommand("get -displayWithMainStatus -identifier=productName");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = RunCommand("get -displayWithMainStatus -identifier=name");
        }
        return name;
    }

    public string GetCurrentResolution()
    {
        return RunCommand("get -displayWithMainStatus -resolution");
    }

    public int? GetCurrentDisplayModeNumber()
    {
        string output = RunCommand("get -displayWithMainStatus -displayModeNumber");
        if (int.TryParse(output, out int modeNum))
        {
            return modeNum;
        }
        return null;
    }

    public IReadOnlyList<BetterDisplayMode> GetAvailableModes()
    {
        string output = RunCommand("get -displayWithMainStatus -displayModeList");
        return ParseDisplayModes(output);
    }

    public static IReadOnlyList<BetterDisplayMode> ParseDisplayModes(string rawOutput)
    {
        var modes = new List<BetterDisplayMode>();
        if (string.IsNullOrWhiteSpace(rawOutput)) return modes;

        var lineRegex = new Regex(@"^\s*(\d+)\s*-\s*(\d+)x(\d+)\s*(.*)$", RegexOptions.Compiled);
        var hzRegex = new Regex(@"(\d+(?:\.\d+)?Hz)", RegexOptions.Compiled);

        var lines = rawOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = lineRegex.Match(line);
            if (!match.Success) continue;

            if (!int.TryParse(match.Groups[1].Value, out int modeNumber)) continue;
            if (!int.TryParse(match.Groups[2].Value, out int width)) continue;
            if (!int.TryParse(match.Groups[3].Value, out int height)) continue;

            string rest = match.Groups[4].Value;
            bool isHiDpi = rest.IndexOf("HiDPI", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isNative = rest.IndexOf("Native", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isCurrent = rest.IndexOf("Current", StringComparison.OrdinalIgnoreCase) >= 0;

            string hz = "";
            var hzMatch = hzRegex.Match(rest);
            if (hzMatch.Success)
            {
                hz = hzMatch.Groups[1].Value;
            }

            modes.Add(new BetterDisplayMode(
                ModeNumber: modeNumber,
                Width: width,
                Height: height,
                RefreshRate: hz,
                IsHiDpi: isHiDpi,
                IsNative: isNative,
                IsCurrent: isCurrent,
                RawLine: line.Trim()
            ));
        }

        return modes;
    }

    public BetterDisplayMode? GetNativeMode()
    {
        var modes = GetAvailableModes();
        if (modes.Count == 0) return null;

        // Find the native mode with the highest resolution (Width * Height).
        // On monitors, HiDPI halves the logical resolution (e.g. 1720x720 Native),
        // while the physical native mode is 3440x1440 Default Native.
        return modes
            .Where(m => m.IsNative || m.RawLine.Contains("Native", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.RawLine.Contains("Default Native", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(m => m.Width * m.Height)
            .FirstOrDefault();
    }

    public bool SetDisplayMode(int modeNumber)
    {
        string result = RunCommand($"set -displayWithMainStatus -displayModeNumber={modeNumber}");
        return true;
    }

    public bool SetResolution(string resolution)
    {
        if (string.IsNullOrWhiteSpace(resolution)) return false;
        string result = RunCommand($"set -displayWithMainStatus -resolution={resolution.Trim()}");
        return true;
    }

    public void OnGameStarted(AppSettings settings)
    {
        lock (_lock)
        {
            if (!settings.BetterDisplayEnabled || !IsAvailable || !IsRunning) return;
            if (_isResolutionSwapped) return;

            int? currentMode = GetCurrentDisplayModeNumber();
            string currentRes = GetCurrentResolution();

            // Auto-memorize desktop mode if not already saved
            if (!settings.BetterDisplaySavedDesktopModeNumber.HasValue && currentMode.HasValue)
            {
                settings.BetterDisplaySavedDesktopModeNumber = currentMode.Value;
                settings.BetterDisplaySavedDesktopResolution = currentRes;
            }

            // Determine target game mode
            BetterDisplayMode? targetMode = null;
            if (settings.BetterDisplayTargetModeNumber.HasValue)
            {
                var modes = GetAvailableModes();
                targetMode = modes.FirstOrDefault(m => m.ModeNumber == settings.BetterDisplayTargetModeNumber.Value);
            }

            targetMode ??= GetNativeMode();

            if (targetMode != null)
            {
                // Only switch if we are not already at the target mode
                if (currentMode != targetMode.ModeNumber)
                {
                    SetDisplayMode(targetMode.ModeNumber);
                    _isResolutionSwapped = true;
                }
            }
        }
    }

    public void OnGameEnded(AppSettings settings)
    {
        lock (_lock)
        {
            if (!settings.BetterDisplayEnabled || !settings.BetterDisplayRestoreOnExit || !IsAvailable) return;
            if (!_isResolutionSwapped) return;

            RestoreDesktopInternal(settings);
            _isResolutionSwapped = false;
        }
    }

    public void RestoreDesktop(AppSettings settings)
    {
        lock (_lock)
        {
            if (!IsAvailable) return;
            RestoreDesktopInternal(settings);
            _isResolutionSwapped = false;
        }
    }

    private void RestoreDesktopInternal(AppSettings settings)
    {
        if (settings.BetterDisplaySavedDesktopModeNumber.HasValue)
        {
            SetDisplayMode(settings.BetterDisplaySavedDesktopModeNumber.Value);
        }
        else if (!string.IsNullOrWhiteSpace(settings.BetterDisplaySavedDesktopResolution))
        {
            SetResolution(settings.BetterDisplaySavedDesktopResolution);
        }
    }
}
