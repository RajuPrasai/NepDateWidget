using NepDateWidget.Helpers;
using System.IO;
using System.Text.Json;

namespace NepDateWidget.Services;

public sealed class NotesService : INotesService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _filePath;
    private Dictionary<string, string> _notes = new();
    private DebouncedFileReloader? _reloader;
    private long _lastSelfWriteTicks;

    private readonly SynchronizationContext? _syncContext;

    public event EventHandler? NotesChanged;

    public NotesService(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _syncContext = SynchronizationContext.Current;
    }

    public string? GetNote(string dateKey)
    {
        _notes.TryGetValue(dateKey, out string? value);
        return value;
    }

    /// <summary>
    /// Returns the set of day numbers (1-based) that have a note in the given BS year/month.
    /// One dictionary pass instead of per-cell key lookups during grid refresh.
    /// </summary>
    public HashSet<int> GetHasNotesForMonth(int bsYear, int bsMonth)
    {
        var result = new HashSet<int>();
        string prefix = $"{bsYear:D4}-{bsMonth:D2}-";
        foreach (var key in _notes.Keys)
        {
            if (key.Length == prefix.Length + 2
                && key.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(key.AsSpan(prefix.Length), out int day))
            {
                result.Add(day);
            }
        }
        return result;
    }

    public static string FormatKey(int year, int month, int day) => $"{year:D4}-{month:D2}-{day:D2}";

    public void SetNote(string dateKey, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _notes.Remove(dateKey);
        }
        else
        {
            _notes[dateKey] = text.Trim();
        }
        Save();
        NotesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteNote(string dateKey)
    {
        if (_notes.Remove(dateKey))
        {
            Save();
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyDictionary<string, string> GetAll() => _notes.AsReadOnly();

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            _notes = new();
            Save();
        }
        else
        {
            LoadFromDisk();
        }
        _reloader ??= new DebouncedFileReloader(_filePath, debounceMs: 500, onReload: () =>
        {
            // Suppress reloads triggered by our own writes (self-induced FSW noise).
            var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastSelfWriteTicks));
            if (elapsed.TotalSeconds < 1.0)
            {
                return;
            }

            LoadFromDisk();
            if (_syncContext is not null)
            {
                _syncContext.Post(_ => NotesChanged?.Invoke(this, EventArgs.Empty), null);
            }
            else
            {
                NotesChanged?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            _notes = JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions)
                     ?? new();
        }
        catch (Exception ex)
        {
            Log.Error("Failed to load notes", ex);
            _notes = new();
        }
    }

    public void Save()
    {
        Interlocked.Exchange(ref _lastSelfWriteTicks, DateTime.UtcNow.Ticks);
        try
        {
            var json = JsonSerializer.Serialize(_notes, SerializerOptions);
            // Atomic write so a crash mid-write cannot leave a zero-byte file.
            if (!AtomicFile.WriteAllText(_filePath, json))
            {
                Log.Error("Failed to save notes (atomic write returned false)");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Failed to save notes", ex);
        }
    }

    public void Dispose() => _reloader?.Dispose();

    /// <summary>
    /// Migrates notes from WidgetSettings.DayNotes into this service.
    /// Existing notes in the service take precedence over settings notes.
    /// </summary>
    public void MigrateFromSettings(Dictionary<string, string> dayNotes)
    {
        if (dayNotes is null || dayNotes.Count == 0)
        {
            return;
        }

        bool changed = false;
        foreach (var (key, value) in dayNotes)
        {
            if (!_notes.ContainsKey(key) && !string.IsNullOrWhiteSpace(value))
            {
                _notes[key] = value.Trim();
                changed = true;
            }
        }
        if (changed)
        {
            Save();
        }
    }
}
