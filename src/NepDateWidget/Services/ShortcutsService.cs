using NepDateWidget.Helpers;
using NepDateWidget.Models;
using System.IO;
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
    // ── Defaults (default config file) ───────────────────────────────────────

    /// <summary>
    /// Parses the default shortcuts file and returns the merged prefix/name dictionaries.
    /// Used by the no-file fallback path and test infrastructure.
    /// </summary>
    internal static (IReadOnlyDictionary<string, string> Prefixes, IReadOnlyDictionary<string, string> SiteNames) LoadDefaults(string defaultFilePath)
    {
        var prefixes  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var siteNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(defaultFilePath)) return (prefixes, siteNames);
            var items = JsonSerializer.Deserialize<List<UserShortcut>>(File.ReadAllText(defaultFilePath), JsonOptions);
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
            Log.Warn($"shortcuts defaults: failed to parse '{defaultFilePath}': {ex.Message}");
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
    private readonly string? _defaultFilePath;
    private readonly SynchronizationContext? _syncContext;
    private DebouncedFileReloader? _reloader;

    private Dictionary<string, string> _prefixes;
    private Dictionary<string, string> _siteNames;

    public IReadOnlyDictionary<string, string> Prefixes     => _prefixes;
    public IReadOnlyDictionary<string, string> PrefixSiteNames => _siteNames;

    public event EventHandler? ShortcutsChanged;

    // ── Construction ──────────────────────────────────────────────────────────

    public ShortcutsService(string path, string? defaultFilePath = null)
    {
        _path            = path;
        _defaultFilePath = defaultFilePath;
        _syncContext     = SynchronizationContext.Current;

        // Empty until Load() is called.
        _prefixes  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _siteNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    // ── IShortcutsService ─────────────────────────────────────────────────────

    public void Load()
    {
        if (!File.Exists(_path))
            SeedFile();
        MergeNewDefaults();
        LoadFromFile();
        _reloader ??= new DebouncedFileReloader(_path, debounceMs: 500, onReload: () =>
        {
            LoadFromFile();
            if (_syncContext is not null)
                _syncContext.Post(_ => ShortcutsChanged?.Invoke(this, EventArgs.Empty), null);
            else
                ShortcutsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void SeedFile()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (_defaultFilePath is not null && File.Exists(_defaultFilePath))
                File.Copy(_defaultFilePath, _path, overwrite: false);
            else
                File.WriteAllText(_path, "[]", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Log.Warn($"shortcuts.json: failed to create default file: {ex.Message}");
        }
    }

    /// <summary>
    /// Appends to the user's shortcuts.json any entry from the default file whose Key
    /// is not already present (active or disabled) in the user's file.
    /// Runs at every launch; writes to disk only when new entries are found.
    /// Hot-reload does NOT call this; merging only happens at startup.
    /// </summary>
    private void MergeNewDefaults()
    {
        if (_defaultFilePath is null || !File.Exists(_defaultFilePath) || !File.Exists(_path)) return;
        try
        {
            var defaultItems = JsonSerializer.Deserialize<List<UserShortcut>>(File.ReadAllText(_defaultFilePath), JsonOptions) ?? new();
            var userItems    = JsonSerializer.Deserialize<List<UserShortcut>>(File.ReadAllText(_path), JsonOptions) ?? new();

            // Keys already in user's file (active or explicitly disabled)
            var existingKeys = new HashSet<string>(
                userItems.Where(i => i.Key is not null).Select(i => i.Key),
                StringComparer.OrdinalIgnoreCase);

            var toAdd = defaultItems
                .Where(d => !string.IsNullOrWhiteSpace(d.Key) && !existingKeys.Contains(d.Key))
                .ToList();

            // One-time corrections for known-bad URLs shipped in defaults before they were
            // fixed.  Only fires when the user's URL is still the exact wrong shipped value,
            // meaning they never customised the entry themselves.
            var corrections = new Dictionary<string, (string OldUrl, string NewUrl, string NewName)>(StringComparer.OrdinalIgnoreCase)
            {
                ["hb"] = ("https://daraz.com/search/product?q={query}",
                          "https://hamrobazaar.com/search/product?q={query}",
                          "HamroBazaar"),
            };

            var correctedCount = 0;
            foreach (var item in userItems)
            {
                if (item.Key is null || !corrections.TryGetValue(item.Key, out var fix)) continue;
                if (!string.Equals(item.Url, fix.OldUrl, StringComparison.OrdinalIgnoreCase)) continue;
                item.Url  = fix.NewUrl;
                item.Name = fix.NewName;
                correctedCount++;
            }

            if (toAdd.Count == 0 && correctedCount == 0) return;

            userItems.AddRange(toAdd);
            var merged = JsonSerializer.Serialize(userItems, JsonOptions);
            if (!AtomicFile.WriteAllText(_path, merged))
                Log.Warn("shortcuts.json: atomic write failed during merge.");
            else
                Log.Info($"shortcuts.json: merged {toAdd.Count} new, corrected {correctedCount} entry/entries.");
        }
        catch (Exception ex)
        {
            Log.Warn($"shortcuts.json: merge failed: {ex.Message}");
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

    // ── Built-in-only fallback ────────────────────────────────────────────────

    /// <summary>
    /// Returns a no-op implementation that exposes only the built-in shortcuts.
    /// Useful as a default when no shortcuts.json path is available (e.g. unit tests).
    /// </summary>
    public static IShortcutsService CreateBuiltInOnly(string defaultFilePath) => new BuiltInOnlyService(defaultFilePath);

    private sealed class BuiltInOnlyService : IShortcutsService
    {
        private readonly IReadOnlyDictionary<string, string> _prefixes;
        private readonly IReadOnlyDictionary<string, string> _siteNames;

        public BuiltInOnlyService(string defaultFilePath)
        {
            (_prefixes, _siteNames) = ShortcutsService.LoadDefaults(defaultFilePath);
        }

        public IReadOnlyDictionary<string, string> Prefixes        => _prefixes;
        public IReadOnlyDictionary<string, string> PrefixSiteNames => _siteNames;
        public event EventHandler? ShortcutsChanged { add { } remove { } }
        public void Load() { }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _reloader?.Dispose();
    }
}
