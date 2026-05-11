using NepDateWidget.Helpers;
using NepDateWidget.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace NepDateWidget.Services;

/// <summary>
/// Loads and saves user settings from/to <c>widget.settings.json</c> beside the executable.
///
/// Design rules followed:
///   • Corrupted or missing JSON → silent fallback to defaults (never crash).
///   • Unknown JSON fields are ignored (forward-compatible reads).
///   • Writes are atomic: write to .tmp → replace original → delete .bak.
///   • Schema version mismatch triggers migration, not failure.
///   • All field-level validation is delegated to <see cref="SettingsValidator"/>.
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        // Unknown fields in the JSON file are silently skipped (forward-compatibility)
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly string _settingsPath;
    private readonly SynchronizationContext? _syncContext;
    private DebouncedFileReloader? _reloader;
    private long _lastSelfWriteTicks;
    private WidgetSettings _current = new();
    private bool _isFirstLaunch;

    public event EventHandler? SettingsChanged;

    // ── Construction ─────────────────────────────────────────────────────────

    /// <summary>
    /// Production constructor: resolves path beside the running executable.
    /// </summary>
    public SettingsService()
        : this(ResolveDefaultPath()) { }

    /// <summary>
    /// Testable constructor that accepts an explicit file path.
    /// </summary>
    public SettingsService(string settingsFilePath)
    {
        if (string.IsNullOrWhiteSpace(settingsFilePath))
            throw new ArgumentException("Settings file path must not be empty.", nameof(settingsFilePath));

        _settingsPath = settingsFilePath;
        _syncContext = SynchronizationContext.Current;
    }

    // ── ISettingsService ──────────────────────────────────────────────────────

    public WidgetSettings Current => _current;
    public bool IsFirstLaunch => _isFirstLaunch;

    /// <summary>
    /// Reads the settings file from disk.
    /// On any failure (missing, corrupt, bad schema) the in-memory object is reset to defaults.
    /// </summary>
    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            _isFirstLaunch = true;
            _current = CreateDefaults();
            try { Save(); } catch { /* best-effort: startup proceeds with in-memory defaults */ }
        }
        else
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<WidgetSettings>(json, SerializerOptions);

                if (loaded is null)
                {
                    _current = CreateDefaults();
                }
                else
                {
                    loaded = Migrate(loaded, json);
                    SettingsValidator.Validate(loaded);
                    _current = loaded;
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Helpers.Log.Warn($"Settings load failed: {ex.GetType().Name}: {ex.Message}. Reverting to defaults.");
                TryBackupCorrupted();
                _current = CreateDefaults();
            }
        }

        _reloader ??= new DebouncedFileReloader(_settingsPath, debounceMs: 500, onReload: () =>
        {
            var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastSelfWriteTicks));
            if (elapsed.TotalSeconds < 1.0) return;
            ReloadFromDisk();
        });
    }

    private void TryBackupCorrupted()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return;
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var dest = _settingsPath + $".broken-{stamp}";
            File.Copy(_settingsPath, dest, overwrite: true);
            Helpers.Log.Info($"Corrupted settings backed up to: {dest}");
        }
        catch (Exception ex)
        {
            Helpers.Log.Warn($"Could not back up corrupted settings file: {ex.Message}");
        }
    }

    /// <summary>
    /// Persists the current settings atomically:
    ///   1. Serialise to a .tmp file in the same folder.
    ///   2. If the target file exists, use File.Replace (rename .tmp → target, back up old → .bak).
    ///   3. If the target file does not yet exist, use File.Move.
    ///   4. Delete the .bak file if it exists.
    /// Silently ignores I/O errors - the next successful save will win.
    /// </summary>
    public void Save()
    {
        Interlocked.Exchange(ref _lastSelfWriteTicks, DateTime.UtcNow.Ticks);
        try
        {
            SettingsValidator.Validate(_current);

            var json = JsonSerializer.Serialize(_current, SerializerOptions);
            var tmpPath = _settingsPath + ".tmp";
            var bakPath = _settingsPath + ".bak";

            File.WriteAllText(tmpPath, json);

            if (File.Exists(_settingsPath))
            {
                File.Replace(tmpPath, _settingsPath, bakPath);
                // Clean up the backup; ignore if delete fails
                TryDelete(bakPath);
            }
            else
            {
                File.Move(tmpPath, _settingsPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Non-fatal: settings will be written on the next successful save.
            // Logged so a persistent disk-full / permission issue is visible in support diagnostics.
            Helpers.Log.Warn($"Settings save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets all settings to factory defaults and persists immediately.
    /// </summary>
    public void ResetToDefaults()
    {
        _current = CreateDefaults();
        Save();
    }

    public void Dispose() => _reloader?.Dispose();

    // ── Hot-reload ────────────────────────────────────────────────────────────

    private void ReloadFromDisk()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<WidgetSettings>(json, SerializerOptions);
            if (loaded is null) return;
            loaded = Migrate(loaded, json);
            SettingsValidator.Validate(loaded);
            _current = loaded;
            if (_syncContext is not null)
                _syncContext.Post(_ => SettingsChanged?.Invoke(this, EventArgs.Empty), null);
            else
                SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Helpers.Log.Warn($"Settings hot-reload failed: {ex.GetType().Name}: {ex.Message}. Keeping current settings.");
        }
    }

    // ── Migration ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Upgrades an older settings object to the current schema version.
    /// Add a new case here for each schema version bump.
    /// Returns the (possibly mutated) settings object.
    /// </summary>
    private static WidgetSettings Migrate(WidgetSettings s, string rawJson)
    {
        // V1 → V2: DayNotes removed from WidgetSettings (day notes now live in
        // notes.json managed by NotesService). LastUpdateCheckUtc moved to
        // runtime.json (AppStateService). Both stale JSON fields are silently
        // dropped by UnmappedMemberHandling.Skip on the next save.

        // V1 → V1: ShowTime was renamed to ShowTimezone.
        // The JSON deserializer ignores the unknown "ShowTime" key, so we
        // fish it out of the raw text and map it onto the new property.
        if (rawJson.Contains("\"ShowTime\"", StringComparison.OrdinalIgnoreCase)
            && !rawJson.Contains("\"ShowTimezone\"", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
                if (doc.RootElement.TryGetProperty("ShowTime", out var showTimeElem))
                    s.ShowTimezone = showTimeElem.GetBoolean();
            }
            catch { /* best-effort */ }
        }

        return s;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static WidgetSettings CreateDefaults() => new();

    private static string ResolveDefaultPath()
    {
        // Single source of truth for data paths is AppPaths. Migration of
        // legacy beside-EXE files is handled centrally by AppPaths.MigrateLegacyData.
        return AppPaths.SettingsPath;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }
}

