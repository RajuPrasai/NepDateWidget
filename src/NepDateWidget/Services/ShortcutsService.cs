using NepDateWidget.Helpers;
using NepDateWidget.Models;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;

namespace NepDateWidget.Services;

/// <summary>
/// Loads and merges RunBox prefix shortcuts from shortcuts.json.
/// Built-in shortcuts are always the base; user entries override or remove them.
/// File is watched via FileSystemWatcher; changes hot-reload without restarting.
///
/// Thread-safety: _prefixes and _siteNames are replaced atomically on the UI thread
/// (via the captured SynchronizationContext). Reads from the UI thread are safe.
/// </summary>
public sealed class ShortcutsService : IShortcutsService, IDisposable
{
    // ── Defaults (embedded resource) ──────────────────────────────────────────

    private const string DefaultResourceName = "NepDateWidget.shortcuts.default.json";

    private static string ReadDefaultJson()
    {
        var asm = typeof(ShortcutsService).Assembly;
        using var stream = asm.GetManifestResourceStream(DefaultResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{DefaultResourceName}' not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Parses the embedded defaults and returns the merged prefix/name dictionaries.
    /// Used by the no-file fallback path and test infrastructure.
    /// </summary>
    internal static (IReadOnlyDictionary<string, string> Prefixes, IReadOnlyDictionary<string, string> SiteNames) LoadDefaults()
    {
        var prefixes  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var siteNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var items = JsonSerializer.Deserialize<List<UserShortcut>>(ReadDefaultJson(), JsonOptions);
            if (items is not null)
            {
                foreach (var item in items)
                {
                    if (item.Disabled || string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Url))
                        continue;
                    var url  = item.Url.Replace("{query}", "{0}", StringComparison.OrdinalIgnoreCase);
                    var name = string.IsNullOrWhiteSpace(item.Name) ? item.Key : item.Name!;
                    prefixes[item.Key]  = url;
                    siteNames[item.Key] = name;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"shortcuts defaults: failed to parse embedded resource: {ex.Message}");
        }
        return (prefixes, siteNames);
    }

    // ── JSON + validation ─────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Regex ValidKey =
        new(@"^[a-zA-Z0-9]+$", RegexOptions.Compiled);

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly string _path;
    private readonly string _watchDir;
    private readonly SynchronizationContext? _syncContext;
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;

    private Dictionary<string, string> _prefixes;
    private Dictionary<string, string> _siteNames;

    public IReadOnlyDictionary<string, string> Prefixes     => _prefixes;
    public IReadOnlyDictionary<string, string> PrefixSiteNames => _siteNames;

    public event EventHandler? ShortcutsChanged;

    // ── Construction ──────────────────────────────────────────────────────────

    public ShortcutsService(string path)
    {
        _path     = path;
        _watchDir = Path.GetDirectoryName(path)!;
        _syncContext = SynchronizationContext.Current;

        // Empty until Load() is called.
        _prefixes  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _siteNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    // ── IShortcutsService ─────────────────────────────────────────────────────

    public void Load()
    {
        if (!File.Exists(_path))
            SeedFile();
        LoadFromFile();
        SetupWatcher();
    }

    private void SeedFile()
    {
        try
        {
            Directory.CreateDirectory(_watchDir);
            File.WriteAllText(_path, ReadDefaultJson(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Log.Warn($"shortcuts.json: failed to create default file: {ex.Message}");
        }
    }

    // ── Load + merge ──────────────────────────────────────────────────────────

    private void LoadFromFile()
    {
        var prefixes  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var siteNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_path))
        {
            _prefixes  = prefixes;
            _siteNames = siteNames;
            return;
        }

        try
        {
            var json  = File.ReadAllText(_path);
            var items = JsonSerializer.Deserialize<List<UserShortcut>>(json, JsonOptions);

            if (items is not null)
            {
                // Process in order; later entries with the same key win (last-write-wins).
                foreach (var item in items)
                {
                    if (!ValidateItem(item)) continue;

                    if (item.Disabled)
                    {
                        prefixes.Remove(item.Key);
                        siteNames.Remove(item.Key);
                        continue;
                    }

                    // Normalize {query} → {0} for string.Format compatibility.
                    var url  = item.Url!.Replace("{query}", "{0}", StringComparison.OrdinalIgnoreCase);
                    var name = string.IsNullOrWhiteSpace(item.Name) ? item.Key : item.Name!;

                    prefixes[item.Key]  = url;
                    siteNames[item.Key] = name;
                }
            }

            _prefixes  = prefixes;
            _siteNames = siteNames;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"shortcuts.json: load failed ({ex.GetType().Name}: {ex.Message}); no shortcuts loaded.");
            _prefixes  = prefixes;
            _siteNames = siteNames;
        }
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private static bool ValidateItem(UserShortcut item)
    {
        if (string.IsNullOrWhiteSpace(item.Key) || !ValidKey.IsMatch(item.Key))
        {
            Log.Warn($"shortcuts.json: invalid key '{item.Key}' (must be non-empty, letters/digits only), skipping.");
            return false;
        }

        // Disabled entries only need a valid key.
        if (item.Disabled) return true;

        if (string.IsNullOrWhiteSpace(item.Url))
        {
            Log.Warn($"shortcuts.json: key '{item.Key}' has no URL, skipping.");
            return false;
        }

        var normalized = item.Url.Replace("{query}", "{0}", StringComparison.OrdinalIgnoreCase);
        int count = CountOccurrences(normalized, "{0}");
        if (count != 1)
        {
            Log.Warn($"shortcuts.json: key '{item.Key}' URL must contain exactly one {{query}} placeholder (found {count}), skipping.");
            return false;
        }

        return true;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }

    // ── FileSystemWatcher ─────────────────────────────────────────────────────

    private void SetupWatcher()
    {
        if (!Directory.Exists(_watchDir))
        {
            Log.Warn($"shortcuts.json: data directory does not exist, file watching is disabled.");
            return;
        }

        _debounceTimer = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(_watchDir, Path.GetFileName(_path))
        {
            NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += (_, _) => ScheduleReload();
        _watcher.Created += (_, _) => ScheduleReload();
        _watcher.Deleted += (_, _) => ScheduleReload();
        // Renamed covers editors that do atomic save via temp-file rename (VS Code, Notepad++).
        _watcher.Renamed += (_, _) => ScheduleReload();
    }

    private void ScheduleReload() =>
        _debounceTimer?.Change(500, Timeout.Infinite);

    private void Reload()
    {
        LoadFromFile();

        if (_syncContext is not null)
            _syncContext.Post(_ => ShortcutsChanged?.Invoke(this, EventArgs.Empty), null);
        else
            ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Built-in-only fallback ────────────────────────────────────────────────

    /// <summary>
    /// Returns a no-op implementation that exposes only the built-in shortcuts.
    /// Useful as a default when no shortcuts.json path is available (e.g. unit tests).
    /// </summary>
    public static IShortcutsService CreateBuiltInOnly() => new BuiltInOnlyService();

    private sealed class BuiltInOnlyService : IShortcutsService
    {
        private readonly IReadOnlyDictionary<string, string> _prefixes;
        private readonly IReadOnlyDictionary<string, string> _siteNames;

        public BuiltInOnlyService()
        {
            (_prefixes, _siteNames) = LoadDefaults();
        }

        public IReadOnlyDictionary<string, string> Prefixes        => _prefixes;
        public IReadOnlyDictionary<string, string> PrefixSiteNames => _siteNames;
        public event EventHandler? ShortcutsChanged { add { } remove { } }
        public void Load() { }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();
    }
}
