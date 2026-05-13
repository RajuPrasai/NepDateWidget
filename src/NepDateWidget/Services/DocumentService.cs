using NepDateWidget.Helpers;
using NepDateWidget.Models;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace NepDateWidget.Services;

public sealed class DocumentService : IDocumentService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _filePath;
    private readonly SynchronizationContext? _syncContext;
    private List<DocumentEntry> _documents = new();
    private DebouncedFileReloader? _reloader;
    private long _lastSelfWriteTicks;

    public event EventHandler? DocumentsChanged;

    public DocumentService(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _syncContext = SynchronizationContext.Current;
    }

    public IReadOnlyList<DocumentEntry> GetAll() => _documents.AsReadOnly();

    public void Add(DocumentEntry entry)
    {
        _documents.Add(entry);
        Save();
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Update(DocumentEntry entry)
    {
        var idx = _documents.FindIndex(d => d.Id == entry.Id);
        if (idx < 0) return;
        _documents[idx] = entry;
        Save();
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Delete(string id)
    {
        if (_documents.RemoveAll(d => d.Id == id) > 0)
        {
            Save();
            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            _documents = new();
            Save();
        }
        else
        {
            LoadFromDisk();
        }

        _reloader ??= new DebouncedFileReloader(_filePath, debounceMs: 500, onReload: () =>
        {
            var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastSelfWriteTicks));
            if (elapsed.TotalSeconds < 1.0) return;
            LoadFromDisk();
            if (_syncContext is not null)
                _syncContext.Post(_ => DocumentsChanged?.Invoke(this, EventArgs.Empty), null);
            else
                DocumentsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public void Save()
    {
        Interlocked.Exchange(ref _lastSelfWriteTicks, DateTime.UtcNow.Ticks);
        try
        {
            var json = JsonSerializer.Serialize(_documents, SerializerOptions);
            if (!AtomicFile.WriteAllText(_filePath, json))
                Log.Error("Failed to save documents (atomic write returned false)");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to save documents", ex);
        }
    }

    public void Dispose() => _reloader?.Dispose();

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            _documents = JsonSerializer.Deserialize<List<DocumentEntry>>(json, SerializerOptions) ?? new();
        }
        catch (Exception ex)
        {
            Log.Error("Failed to load documents", ex);
            _documents = new();
        }
    }
}
