using Microsoft.Win32;
using System.Diagnostics;
using Windows.ApplicationModel;

namespace NepDateWidget.Services;

/// <summary>
/// Manages the Windows "Start with Windows" startup entry.
/// Velopack / portable channel: HKCU\Software\Microsoft\Windows\CurrentVersion\Run, value "NepDateWidget".
/// MSIX / Store channel: Windows.ApplicationModel.StartupTask with ID "NepDateWidgetStartupTask".
/// The task ID must match the TaskId declared in Package.appxmanifest.
/// </summary>
public sealed class AutoStartService : IAutoStartService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName       = "NepDateWidget";
    internal const string StartupTaskId  = "NepDateWidgetStartupTask";

    // Cached at construction: StartupTask object for the MSIX channel.
    // Null when running unpackaged or when the task is not declared in the manifest.
    private readonly StartupTask? _startupTask;

    private static string? CurrentExePath =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName;

    public AutoStartService()
    {
        if (!Helpers.AppEnvironment.IsPackaged) return;
        try
        {
            // Run on a thread-pool thread to avoid deadlocking the UI thread
            // when synchronously blocking on a WinRT async operation.
            _startupTask = Task.Run(() =>
                StartupTask.GetAsync(StartupTaskId).AsTask()).GetAwaiter().GetResult();
        }
        catch
        {
            _startupTask = null;
        }
    }

    public bool IsEnabled
    {
        get
        {
            if (Helpers.AppEnvironment.IsPackaged)
            {
                return _startupTask?.State
                    is StartupTaskState.Enabled
                    or StartupTaskState.EnabledByPolicy;
            }
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
        if (Helpers.AppEnvironment.IsPackaged)
        {
            SetPackagedStartupState(enable);
            return;
        }
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

    private void SetPackagedStartupState(bool enable)
    {
        if (_startupTask is null) return;
        try
        {
            if (enable)
                // RequestEnableAsync may prompt the user or be overridden by policy.
                // Return value is informational; we treat all outcomes as best-effort.
                Task.Run(() => _startupTask.RequestEnableAsync().AsTask()).GetAwaiter().GetResult();
            else
                _startupTask.Disable();
        }
        catch
        {
            // Best-effort.
        }
    }

    /// <summary>
    /// If the registry value exists but points to an old EXE path (because the
    /// app was moved or updated to a new install location), rewrite it with the
    /// current path. No-op if the value is missing or already correct.
    /// In the MSIX channel Windows tracks the path automatically; this method is a no-op.
    /// </summary>
    public void RefreshIfStale()
    {
        // Windows updates the StartupTask exe reference on every MSIX package update.
        if (Helpers.AppEnvironment.IsPackaged) return;
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
    /// Splitting on space breaks paths with spaces - a real case for any
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
