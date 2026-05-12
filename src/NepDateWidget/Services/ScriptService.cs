using NepDateWidget.Helpers;
using NepDateWidget.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace NepDateWidget.Services;

/// <summary>
/// Loads user-defined script commands from scripts.json.
/// File is watched for hot-reload; changes raise <see cref="ScriptsChanged"/> on the UI thread.
/// </summary>
public sealed class ScriptService : IScriptService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _filePath;
    private readonly string? _defaultFilePath;
    private List<ScriptEntry> _scripts = new();
    private DebouncedFileReloader? _reloader;
    private readonly SynchronizationContext? _syncContext;

    public event EventHandler? ScriptsChanged;

    public ScriptService(string filePath, string? defaultFilePath = null)
    {
        _filePath        = filePath        ?? throw new ArgumentNullException(nameof(filePath));
        _defaultFilePath = defaultFilePath;
        _syncContext     = SynchronizationContext.Current;
    }

    public IReadOnlyList<ScriptEntry> GetAll() => _scripts.AsReadOnly();

    public ScriptEntry? Find(string name)
        => _scripts.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Load()
    {
        if (!File.Exists(_filePath))
            SeedFile();
        LoadFromFile();
        MergeNewDefaults();
        _reloader ??= new DebouncedFileReloader(_filePath, debounceMs: 500, onReload: () =>
        {
            LoadFromFile();
            RaiseScriptsChanged();
        });
    }

    private void SeedFile()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            if (_defaultFilePath is not null && File.Exists(_defaultFilePath))
                File.Copy(_defaultFilePath, _filePath, overwrite: false);
            else
                File.WriteAllText(_filePath, "[]", System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Log.Warn($"scripts.json: failed to create default file: {ex.Message}");
        }
    }

    /// <summary>
    /// Appends to scripts.json any entry from the default file whose Name is not
    /// already present in the user's file (case-insensitive). Writes to disk if
    /// new entries were added. Runs at every launch after loading.
    /// Hot-reload does NOT call this; merging only happens at startup.
    /// </summary>
    private void MergeNewDefaults()
    {
        if (_defaultFilePath is null || !File.Exists(_defaultFilePath)) return;
        try
        {
            var defaultJson     = File.ReadAllText(_defaultFilePath);
            var defaultEntries  = JsonSerializer.Deserialize<List<ScriptEntry>>(defaultJson, SerializerOptions) ?? new();

            var existingNames = new HashSet<string>(
                _scripts.Where(s => s.Name is not null).Select(s => s.Name!),
                StringComparer.OrdinalIgnoreCase);

            var toAdd = defaultEntries
                .Where(d => !string.IsNullOrWhiteSpace(d.Name) && !existingNames.Contains(d.Name))
                .ToList();

            if (toAdd.Count == 0) return;

            _scripts.AddRange(toAdd);

            var merged = JsonSerializer.Serialize(_scripts, SerializerOptions);
            if (!AtomicFile.WriteAllText(_filePath, merged))
                Log.Warn("scripts.json: atomic write failed during merge.");
            else
                Log.Info($"scripts.json: merged {toAdd.Count} new default script(s).");
        }
        catch (Exception ex)
        {
            Log.Warn($"scripts.json: merge failed: {ex.Message}");
        }
    }

    internal void LoadFromFile()
    {
        if (!File.Exists(_filePath))
        {
            _scripts = new List<ScriptEntry>();
            return;
        }
        try
        {
            var json = File.ReadAllText(_filePath);
            _scripts = JsonSerializer.Deserialize<List<ScriptEntry>>(json, SerializerOptions)
                       ?? new List<ScriptEntry>();
            // Remove entries that have no name or no path — they cannot be executed.
            _scripts.RemoveAll(s => string.IsNullOrWhiteSpace(s.Name) || string.IsNullOrWhiteSpace(s.Path));
        }
        catch
        {
            _scripts = new List<ScriptEntry>();
        }
    }

    private void RaiseScriptsChanged()
    {
        if (_syncContext is not null)
            _syncContext.Post(_ => ScriptsChanged?.Invoke(this, EventArgs.Empty), null);
        else
            ScriptsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _reloader?.Dispose();
}
