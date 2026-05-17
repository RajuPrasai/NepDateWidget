using System.IO;

namespace NepDateWidget.Helpers;

/// <summary>
/// Single source of truth for where the app stores user data.
///
/// Resolution rules (first match wins):
///   1. If a file named <c>portable.flag</c> exists beside the running EXE,
///      data lives in <c>{exeDir}\AppData\</c> (portable mode, single-folder install).
///   2. For MSIX packaged builds, data lives in the physical LocalCache path:
///      <c>%LOCALAPPDATA%\Packages\{PackageFamilyName}\LocalCache\Local\NepDateWidget.Store\AppData\</c>
///      The MSIX runtime transparently redirects %LOCALAPPDATA% writes to that path.
///      Using the physical path directly ensures that unpackaged processes
///      (Process.Start, Explorer, Shell.Application) can find the files.
///   3. Otherwise data lives in <c>%LOCALAPPDATA%\NepDateWidget\AppData\</c>
///      (installed/dev mode).
///
/// On first Store launch, data from any prior GitHub Releases install
/// is copied from <c>%LOCALAPPDATA%\NepDateWidget\AppData\</c> automatically.
///
/// Directory layout under <c>AppData\</c>:
///   config/  - user-editable configuration files
///     settings.json, localization.json, shortcuts.json, scripts.json
///   data/    - user content
///     notes.json, reminders.json, documents.json, run-history.json, Documents/
///   (root)   - operational files
///     runtime.json, nepdate.log
/// </summary>
public static class AppPaths
{
    public const string PortableFlagFile = "portable.flag";
    public const string DataSubfolder = "AppData";
    // Channel-aware: "NepDateWidget" for unpackaged/portable, "NepDateWidget.Store" for MSIX.
    public static string InstalledFolderName => AppEnvironment.DataFolderName;

    private static readonly Lazy<string> _exeDir = new(ResolveExeDir);
    private static readonly Lazy<bool> _isPortable = new(() => File.Exists(Path.Combine(_exeDir.Value, PortableFlagFile)));
    private static readonly Lazy<string> _dataDir = new(ResolveDataDir);

    public static string ExeDirectory => _exeDir.Value;
    public static bool IsPortable => _isPortable.Value;
    public static string DataDirectory => _dataDir.Value;

    // Subdirectories - created eagerly in ResolveDataDir.
    private static string ConfigDir => Path.Combine(DataDirectory, "config");
    private static string UserDataDir => Path.Combine(DataDirectory, "data");

    // config/: user-editable configuration files
    public static string SettingsPath => Path.Combine(ConfigDir, "settings.json");
    public static string LocalizationPath => Path.Combine(ConfigDir, "localization.json");
    public static string ShortcutsPath => Path.Combine(ConfigDir, "shortcuts.json");
    public static string ScriptsPath => Path.Combine(ConfigDir, "scripts.json");

    // Shipped default config files - located next to the EXE in Resources/configs/.
    // These are read-only at runtime; the app copies from here to AppData on first launch
    // and merges new keys/entries on subsequent launches.
    public static string DefaultsDirectory => Path.Combine(ExeDirectory, "Resources", "configs");
    public static string DefaultLocalizationPath => Path.Combine(DefaultsDirectory, "localization.json");
    public static string DefaultShortcutsPath => Path.Combine(DefaultsDirectory, "shortcuts.json");
    public static string DefaultRunHistoryPath => Path.Combine(DefaultsDirectory, "run-history.json");
    public static string DefaultSettingsPath => Path.Combine(DefaultsDirectory, "settings.json");
    public static string DefaultScriptsPath => Path.Combine(DefaultsDirectory, "scripts.json");

    // data/: user content
    public static string NotesPath => Path.Combine(UserDataDir, "notes.json");
    public static string RemindersPath => Path.Combine(UserDataDir, "reminders.json");
    public static string DocumentsPath => Path.Combine(UserDataDir, "documents.json");
    public static string RunHistoryPath => Path.Combine(UserDataDir, "run-history.json");
    public static string DocumentsFilesDirectory => Path.Combine(UserDataDir, "Documents");

    // root: operational files (not user-edited)
    public static string AppStatePath => Path.Combine(DataDirectory, "runtime.json");
    public static string LogPath => Path.Combine(DataDirectory, "nepdate.log");

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
                ResolveLocalAppData(),
                InstalledFolderName,
                DataSubfolder);

        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.Combine(target, "config"));
        Directory.CreateDirectory(Path.Combine(target, "data"));
        return target;
    }

    /// <summary>
    /// Returns the physical %LOCALAPPDATA% root that is addressable by unpackaged
    /// processes (Process.Start, Explorer, Shell.Application).
    ///
    /// For MSIX packaged Desktop Bridge apps, the runtime transparently redirects all
    /// %LOCALAPPDATA% writes to a private per-user, per-package location:
    ///   <c>%LOCALAPPDATA%\Packages\{PackageFamilyName}\LocalCache\Local\</c>
    /// This is true on Windows 10 1809 through Windows 11 (copy-on-write on 1809,
    /// full redirect with merge on 1903+). The DESTINATION PATH is the same on all
    /// supported versions.
    ///
    /// Computing the physical path directly and using it for every stored path and
    /// every Process.Start / pintohome call ensures that unpackaged processes receive
    /// a real filesystem path they can actually open.
    ///
    /// PackageFamilyName (publisher hash + app name, NO version/architecture) is
    /// stable across app updates, so this path survives Store updates unchanged.
    ///
    /// For unpackaged builds (dev / portable) the standard folder is returned unchanged.
    /// </summary>
    private static string ResolveLocalAppData()
    {
        var standard = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!AppEnvironment.IsPackaged)
        {
            return standard;
        }

        var pfn = AppEnvironment.PackageFamilyName;
        if (pfn is null)
        {
            return standard; // defensive: packaged but PFN unavailable
        }

        return Path.Combine(standard, "Packages", pfn, "LocalCache", "Local");
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

            // 0. Store first-run: copy user data from the prior GitHub Releases install's
            //    AppData dir so a user switching channels keeps their notes and settings.
            //    This is a copy (not move) so the old install is unaffected if still present.
            if (AppEnvironment.IsPackaged && !IsPortable)
            {
                var priorData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NepDateWidget",
                    DataSubfolder);
                if (Directory.Exists(priorData))
                {
                    foreach (var subDir in new[] { "config", "data" })
                    {
                        var srcDir = Path.Combine(priorData, subDir);
                        var dstDir = Path.Combine(target, subDir);
                        if (!Directory.Exists(srcDir))
                        {
                            continue;
                        }

                        foreach (var srcFile in Directory.GetFiles(srcDir))
                        {
                            TryCopy(srcFile, Path.Combine(dstDir, Path.GetFileName(srcFile)));
                        }
                    }
                    // Root-level files: nepdate.log, runtime.json
                    foreach (var srcFile in Directory.GetFiles(priorData))
                    {
                        TryCopy(srcFile, Path.Combine(target, Path.GetFileName(srcFile)));
                    }
                }
            }

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
                //     Move our files into AppData\.
                var legacyInstalled = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NepDateWidget");
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
            TryMove(Path.Combine(target, "widget.settings.json"), SettingsPath);
            TryMove(Path.Combine(target, "NepDateWidget.reminders.json"), RemindersPath);

            // 3. Migrate from very old beside-EXE flat layout.
            TryMove(Path.Combine(_exeDir.Value, "NepDateWidget.settings.json"), SettingsPath);
            TryMove(Path.Combine(_exeDir.Value, "widget.settings.json"), SettingsPath);
            TryMove(Path.Combine(_exeDir.Value, "NepDateWidget.reminders.json"), RemindersPath);
            TryMove(Path.Combine(_exeDir.Value, "nepdate.log"), LogPath);

            // 4. Migrate from flat AppData/ root to subdirectory layout.
            foreach (var fn in new[] { "settings.json", "localization.json", "shortcuts.json", "scripts.json" })
            {
                TryMove(Path.Combine(target, fn), Path.Combine(target, "config", fn));
            }

            foreach (var fn in new[] { "notes.json", "reminders.json", "documents.json", "run-history.json" })
            {
                TryMove(Path.Combine(target, fn), Path.Combine(target, "data", fn));
            }

            TryMoveDir(Path.Combine(target, "Documents"), Path.Combine(target, "data", "Documents"));

            // Remove doc-search-history.json - feature removed.
            TryDelete(Path.Combine(target, "doc-search-history.json"));
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
            if (!File.Exists(source))
            {
                return;
            }

            if (File.Exists(destination))
            {
                return;
            }

            File.Move(source, destination);
        }
        catch { /* best-effort */ }
    }

    private static void TryCopy(string source, string destination)
    {
        try
        {
            if (!File.Exists(source))
            {
                return;
            }

            if (File.Exists(destination))
            {
                return;
            }

            File.Copy(source, destination, overwrite: false);
        }
        catch { /* best-effort */ }
    }

    private static void TryMoveDir(string source, string destination)
    {
        try
        {
            if (!Directory.Exists(source))
            {
                return;
            }

            if (Directory.Exists(destination))
            {
                return;
            }

            Directory.Move(source, destination);
        }
        catch { /* best-effort */ }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
    }
}
