using NepDateWidget.Helpers;
using System.IO;
using System.Text.Json;
using System.Threading;

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
        LoadFromDisk();
        _reloader ??= new DebouncedFileReloader(_filePath, debounceMs: 500, onReload: () =>
        {
            // Suppress reloads triggered by our own writes (self-induced FSW noise).
            var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastSelfWriteTicks));
            if (elapsed.TotalSeconds < 1.0) return;
            LoadFromDisk();
            if (_syncContext is not null)
                _syncContext.Post(_ => NotesChanged?.Invoke(this, EventArgs.Empty), null);
            else
                NotesChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath)) return;
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
                Log.Error("Failed to save notes (atomic write returned false)");
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
        if (dayNotes is null || dayNotes.Count == 0) return;
        bool changed = false;
        foreach (var (key, value) in dayNotes)
        {
            if (!_notes.ContainsKey(key) && !string.IsNullOrWhiteSpace(value))
            {
                _notes[key] = value.Trim();
                changed = true;
            }
        }
        if (changed) Save();
    }
}
