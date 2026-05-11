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

    // Empty array — the user adds entries manually.
    // Supported interpreters: powershell (default), pwsh, cmd, python, wsl
    private const string DefaultScriptsJson = "[]";

    private readonly string _filePath;
    private List<ScriptEntry> _scripts = new();
    private DebouncedFileReloader? _reloader;
    private readonly SynchronizationContext? _syncContext;

    public event EventHandler? ScriptsChanged;

    public ScriptService(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _syncContext = SynchronizationContext.Current;
    }

    public IReadOnlyList<ScriptEntry> GetAll() => _scripts.AsReadOnly();

    public ScriptEntry? Find(string name)
        => _scripts.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Load()
    {
        if (!File.Exists(_filePath))
            SeedFile();
        LoadFromFile();
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
            File.WriteAllText(_filePath, DefaultScriptsJson, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Log.Warn($"scripts.json: failed to create default file: {ex.Message}");
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
