using NepDateWidget.Helpers;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NepDateWidget.Services;

/// <summary>
/// Localization service backed by <c>localization.json</c> in the app data directory.
/// The JSON maps each key to a per-language dictionary: <c>{ "key": { "en": "...", "ne": "..." } }</c>.
/// On first launch the file is seeded from the shipped default in Resources/configs/.
/// External edits to the file are hot-reloaded automatically.
/// </summary>
public sealed class LocalizationService : ILocalizationService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _filePath;         // AppData path (empty for test constructor)
    private readonly string _defaultFilePath;  // Shipped default file path
    private readonly SynchronizationContext? _syncContext;
    private Dictionary<string, Dictionary<string, string>> _strings = new();
    private DebouncedFileReloader? _reloader;
    private string _language = "en";

    public event EventHandler? LocalizationChanged;

    /// <summary>
    /// Production constructor: AppData path + shipped default file path.
    /// </summary>
    public LocalizationService(string filePath, string defaultFilePath)
    {
        _filePath        = filePath        ?? throw new ArgumentNullException(nameof(filePath));
        _defaultFilePath = defaultFilePath ?? throw new ArgumentNullException(nameof(defaultFilePath));
        _syncContext     = SynchronizationContext.Current;
    }

    /// <summary>
    /// Test constructor: loads strings directly from the default file into memory.
    /// No disk I/O to AppData, no hot-reload. <see cref="Load"/> is a no-op.
    /// </summary>
    public LocalizationService(string defaultFilePath)
    {
        _filePath        = string.Empty;
        _defaultFilePath = defaultFilePath ?? throw new ArgumentNullException(nameof(defaultFilePath));
        _syncContext     = null;
        LoadFromFile(_defaultFilePath);
    }

    // ── ILocalizationService ──────────────────────────────────────────────────

    public string CurrentLanguage => _language;

    public string Get(string key)
    {
        if (key is null)
            return "[]";

        if (_strings.TryGetValue(key, out var langs))
        {
            if (langs.TryGetValue(_language, out var text) && !string.IsNullOrEmpty(text))
                return text;
            if (langs.TryGetValue("en", out var fallback) && !string.IsNullOrEmpty(fallback))
                return fallback;
        }

        return $"[{key}]";
    }

    public void SetLanguage(string languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
            _language = languageCode.ToLowerInvariant();
    }

    public void Load()
    {
        // No-op for the test constructor (empty AppData path). Data is
        // already in memory from the constructor; there is no file to watch.
        if (string.IsNullOrEmpty(_filePath)) return;

        if (!File.Exists(_filePath))
            SeedFile();

        LoadFromDisk();
        MergeMissingFromDefaults();

        _reloader ??= new DebouncedFileReloader(_filePath, debounceMs: 500, onReload: () =>
        {
            LoadFromDisk();
            MergeMissingFromDefaults();
            if (_syncContext is not null)
                _syncContext.Post(_ => LocalizationChanged?.Invoke(this, EventArgs.Empty), null);
            else
                LocalizationChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public void Dispose() => _reloader?.Dispose();

    // ── Private ───────────────────────────────────────────────────────────────

    private void SeedFile()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            if (!File.Exists(_defaultFilePath))
            {
                Log.Warn($"LocalizationService: default file '{_defaultFilePath}' not found; seeding empty strings.");
                File.WriteAllText(_filePath, "{}", Encoding.UTF8);
                return;
            }
            File.Copy(_defaultFilePath, _filePath, overwrite: false);
        }
        catch (Exception ex)
        {
            Log.Error("LocalizationService: failed to seed localization.json", ex);
        }
    }

    private void LoadFromDisk() => LoadFromFile(_filePath);

    private void LoadFromFile(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var json   = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, SerializerOptions);
            _strings = loaded ?? new();
        }
        catch (Exception ex)
        {
            Log.Error($"LocalizationService: failed to load '{path}'", ex);
        }
    }

    /// <summary>
    /// In-memory merge: adds any key present in the default file but missing from
    /// the loaded dictionary. Ensures new keys from app updates are always available
    /// without requiring users to delete their localization.json.
    /// Does not write back to disk.
    /// </summary>
    private void MergeMissingFromDefaults()
    {
        if (string.IsNullOrEmpty(_defaultFilePath) || !File.Exists(_defaultFilePath)) return;
        try
        {
            var json     = File.ReadAllText(_defaultFilePath);
            var defaults = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, SerializerOptions);
            if (defaults is null) return;
            foreach (var kvp in defaults)
            {
                if (!_strings.ContainsKey(kvp.Key))
                    _strings[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception ex)
        {
            Log.Error("LocalizationService: failed to merge default strings", ex);
        }
    }
}
