using Microsoft.Win32;
using System.Diagnostics;

namespace NepDateWidget.Services;

/// <summary>
/// Manages the Windows "Start with Windows" registry entry.
/// Key: HKCU\Software\Microsoft\Windows\CurrentVersion\Run
/// Value name: "NepDateWidget"
/// Value data: full path to this executable.
///
/// This key is the standard Windows mechanism and is visible in
/// Task Manager → Startup Apps.
/// </summary>
public sealed class AutoStartService : IAutoStartService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NepDateWidget";

    private static string? CurrentExePath =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName;

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
                if (key?.GetValue(ValueName) is not string stored) return false;

                // Verify the stored entry still points to this executable so the
                // UI toggle reflects reality after a move/reinstall.
                var exePath = CurrentExePath;
                if (string.IsNullOrEmpty(exePath)) return false;

                var storedExe = ParseStoredExePath(stored);
                return string.Equals(storedExe, exePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    public void SetEnabled(bool enable)
    {
        try
        {
            // CreateSubKey creates the key if it doesn't exist; OpenSubKey would
            // return null and silently skip the write.
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath, writable: true);

            if (enable)
            {
                var exePath = CurrentExePath ?? string.Empty;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Auto-start is best-effort - never crash the app if registry is locked.
        }
    }

    /// <summary>
    /// If the registry value exists but points to an old EXE path (because the
    /// app was moved or updated to a new install location), rewrite it with the
    /// current path. No-op if the value is missing or already correct.
    /// </summary>
    public void RefreshIfStale()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string stored) return;

            var exePath = CurrentExePath;
            if (string.IsNullOrEmpty(exePath)) return;

            var storedExe = ParseStoredExePath(stored);
            if (!string.Equals(storedExe, exePath, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, $"\"{exePath}\"");
        }
        catch
        {
            // Best-effort.
        }
    }

    /// <summary>
    /// Extract the executable path from a Run-key value. Run-key values can be:
    ///   - A plain unquoted path:   <c>C:\path\app.exe</c>
    ///   - A quoted path:           <c>"C:\Program Files\App\app.exe"</c>
    ///   - A quoted path + args:    <c>"C:\path\app.exe" --silent</c>
    ///   - An unquoted path + args (legal only when the path has no spaces).
    /// Splitting on space breaks paths with spaces — a real case for any
    /// install under <c>C:\Program Files</c> or <c>C:\My Apps</c>.
    /// </summary>
    internal static string ParseStoredExePath(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var s = raw.TrimStart();
        if (s.Length == 0) return string.Empty;

        if (s[0] == '"')
        {
            int end = s.IndexOf('"', 1);
            return end > 0 ? s.Substring(1, end - 1) : s.Substring(1);
        }

        // Unquoted: take everything up to the first space (legal only when the
        // path has no spaces, which is the only case this branch can correctly
        // disambiguate).
        int sp = s.IndexOf(' ');
        return sp < 0 ? s : s.Substring(0, sp);
    }
}
