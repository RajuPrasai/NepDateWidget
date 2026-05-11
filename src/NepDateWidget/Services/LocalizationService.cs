using NepDateWidget.Helpers;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NepDateWidget.Services;

/// <summary>
/// Localization service backed by <c>localization.json</c> in the app data directory.
/// The JSON maps each key to a per-language dictionary: <c>{ "key": { "en": "...", "ne": "..." } }</c>.
/// On first launch the file is seeded from the embedded <c>Resources/strings.json</c>.
/// External edits to the file are hot-reloaded automatically.
/// </summary>
public sealed class LocalizationService : ILocalizationService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string EmbeddedResourceName = "NepDateWidget.Resources.strings.json";

    private readonly string _filePath;
    private readonly SynchronizationContext? _syncContext;
    private Dictionary<string, Dictionary<string, string>> _strings = new();
    private DebouncedFileReloader? _reloader;
    private string _language = "en";

    public event EventHandler? LocalizationChanged;

    public LocalizationService(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _syncContext = SynchronizationContext.Current;
    }

    /// <summary>
    /// Loads strings directly from the embedded resource. No disk I/O, no hot-reload.
    /// Use this for tests or isolated consumers that do not need live updates.
    /// </summary>
    public LocalizationService()
    {
        _filePath = string.Empty;
        _syncContext = null;
        LoadFromEmbedded();
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
        // No-op for the embedded-only constructor (empty path). The data is
        // already in memory from the constructor; there is no file to watch.
        if (string.IsNullOrEmpty(_filePath)) return;

        if (!File.Exists(_filePath))
            SeedFile();

        LoadFromDisk();
        MergeMissingFromEmbedded();

        _reloader ??= new DebouncedFileReloader(_filePath, debounceMs: 500, onReload: () =>
        {
            LoadFromDisk();
            MergeMissingFromEmbedded();
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
            var content = ReadEmbeddedJson();
            if (content is null)
            {
                Log.Warn($"LocalizationService: embedded resource '{EmbeddedResourceName}' not found; seeding empty strings.");
                File.WriteAllText(_filePath, "{}", Encoding.UTF8);
                return;
            }
            File.WriteAllText(_filePath, content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Log.Error("LocalizationService: failed to seed localization.json", ex);
        }
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, SerializerOptions);
            _strings = loaded ?? new();
        }
        catch (Exception ex)
        {
            Log.Error("LocalizationService: failed to load localization.json", ex);
        }
    }

    /// <summary>
    /// Adds any keys present in the embedded strings.json but missing from the loaded
    /// in-memory dictionary. Ensures new keys introduced in app updates are always
    /// available without requiring the user to delete their localization.json.
    /// Does not write back to disk — purely an in-memory merge.
    /// </summary>
    private void MergeMissingFromEmbedded()
    {
        try
        {
            var json = ReadEmbeddedJson();
            if (json is null) return;
            var embedded = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, SerializerOptions);
            if (embedded is null) return;
            foreach (var kvp in embedded)
            {
                if (!_strings.ContainsKey(kvp.Key))
                    _strings[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception ex)
        {
            Log.Error("LocalizationService: failed to merge embedded strings", ex);
        }
    }

    private void LoadFromEmbedded()
    {
        try
        {
            var json = ReadEmbeddedJson();
            if (json is null) return;
            _strings = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, SerializerOptions) ?? new();
        }
        catch (Exception ex)
        {
            Log.Error("LocalizationService: failed to load from embedded resource", ex);
        }
    }

    private static string? ReadEmbeddedJson()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(EmbeddedResourceName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
