using System.IO;

namespace NepDateWidget.Helpers;

/// <summary>
/// Single source of truth for where the app stores user data.
///
/// Resolution rules (first match wins):
///   1. If a file named <c>portable.flag</c> exists beside the running EXE,
///      data lives in <c>{exeDir}\AppData\</c> (portable mode, single-folder install).
///   2. Otherwise data lives in <c>%LOCALAPPDATA%\NepDateWidget\AppData\</c>
///      (installed mode). The <c>AppData</c> subfolder is mandatory because
///      Velopack installs the app itself into <c>%LOCALAPPDATA%\NepDateWidget\</c>
///      (with <c>current\</c>, <c>packages\</c>, <c>Update.exe</c>) - we must
///      stay out of that folder so Velopack uninstall / update operations do not
///      touch user data.
///
/// On first launch in installed mode, files from any prior beside-EXE
/// <c>AppData\</c> folder are migrated automatically.
///
/// File names produced:
///   - settings.json
///   - reminders.json
///   - notes.json
///   - nepdate.log
/// </summary>
public static class AppPaths
{
    public const string PortableFlagFile = "portable.flag";
    public const string InstalledFolderName = "NepDateWidget";
    public const string DataSubfolder = "AppData";

    private static readonly Lazy<string> _exeDir = new(ResolveExeDir);
    private static readonly Lazy<bool> _isPortable = new(() => File.Exists(Path.Combine(_exeDir.Value, PortableFlagFile)));
    private static readonly Lazy<string> _dataDir = new(ResolveDataDir);

    public static string ExeDirectory => _exeDir.Value;
    public static bool IsPortable => _isPortable.Value;
    public static string DataDirectory => _dataDir.Value;

    public static string SettingsPath  => Path.Combine(DataDirectory, "settings.json");
    public static string RemindersPath => Path.Combine(DataDirectory, "reminders.json");
    public static string NotesPath     => Path.Combine(DataDirectory, "notes.json");
    public static string LogPath       => Path.Combine(DataDirectory, "nepdate.log");

    private static string ResolveExeDir()
    {
        var exePath = Environment.ProcessPath
                   ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        var dir = exePath is not null ? Path.GetDirectoryName(exePath) : null;
        return dir ?? AppContext.BaseDirectory;
    }

    private static string ResolveDataDir()
    {
        string target = IsPortable
            ? Path.Combine(_exeDir.Value, DataSubfolder)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                InstalledFolderName,
                DataSubfolder);

        Directory.CreateDirectory(target);
        return target;
    }

    /// <summary>
    /// One-time migration: if data files exist in old locations
    /// (beside EXE in <c>AppData\</c> when running installed, or with old names),
    /// move them into the resolved <see cref="DataDirectory"/>.
    /// Best-effort; never throws.
    /// </summary>
    public static void MigrateLegacyData()
    {
        try
        {
            var target = DataDirectory;

            // 1. If we're in installed mode, migrate files from beside-EXE \AppData\ if present.
            if (!IsPortable)
            {
                var sideAppData = Path.Combine(_exeDir.Value, DataSubfolder);
                if (Directory.Exists(sideAppData) &&
                    !string.Equals(sideAppData, target, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var fileName in new[] { "settings.json", "reminders.json", "notes.json", "nepdate.log" })
                    {
                        var src = Path.Combine(sideAppData, fileName);
                        var dst = Path.Combine(target, fileName);
                        TryMove(src, dst);
                    }
                }

                // 1b. Migrate from any prior dev build that wrote data straight into
                //     %LOCALAPPDATA%\NepDateWidget\ (without the AppData subfolder).
                //     Velopack now owns that root, so move our files into AppData\.
                var legacyInstalled = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    InstalledFolderName);
                if (!string.Equals(legacyInstalled, target, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var fileName in new[] { "settings.json", "reminders.json", "notes.json", "nepdate.log" })
                    {
                        var src = Path.Combine(legacyInstalled, fileName);
                        var dst = Path.Combine(target, fileName);
                        TryMove(src, dst);
                    }
                }
            }

            // 2. Migrate older filenames within the resolved folder.
            TryMove(Path.Combine(target, "NepDateWidget.settings.json"), SettingsPath);
            TryMove(Path.Combine(target, "widget.settings.json"),        SettingsPath);
            TryMove(Path.Combine(target, "NepDateWidget.reminders.json"), RemindersPath);

            // 3. Migrate from very old beside-EXE flat layout.
            TryMove(Path.Combine(_exeDir.Value, "NepDateWidget.settings.json"), SettingsPath);
            TryMove(Path.Combine(_exeDir.Value, "widget.settings.json"),        SettingsPath);
            TryMove(Path.Combine(_exeDir.Value, "NepDateWidget.reminders.json"), RemindersPath);
            TryMove(Path.Combine(_exeDir.Value, "nepdate.log"),                  LogPath);
        }
        catch
        {
            // Migration is opportunistic; never block startup.
        }
    }

    private static void TryMove(string source, string destination)
    {
        try
        {
            if (!File.Exists(source)) return;
            if (File.Exists(destination)) return;
            File.Move(source, destination);
        }
        catch { /* best-effort */ }
    }
}
