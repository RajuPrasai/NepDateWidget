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
            MigrateVirtualPaths();
        }
        catch (Exception ex)
        {
            Log.Error("Failed to load documents", ex);
            _documents = new();
        }
    }

    /// <summary>
    /// One-time migration for MSIX builds: rewrites any FilePath values that were stored
    /// against the virtual %LOCALAPPDATA% path to the physical LocalCache path.
    ///
    /// Background: before the LocalCache fix in AppPaths, DocumentsFilesDirectory returned
    /// the virtual AppData path (e.g., C:\Users\...\AppData\Local\NepDateWidget.Store\...).  
    /// MSIX transparently redirected writes so the files physically landed in LocalCache,
    /// but the stored path was the virtual one.  When that virtual path is passed to an
    /// unpackaged process (Process.Start, explorer.exe) it cannot find the file because
    /// unpackaged processes have no MSIX merge view.  Rewriting to the physical path fixes
    /// open, show-in-explorer, and delete operations for all existing entries.
    ///
    /// The check is a no-op once all paths are physical (virtualBase != physicalBase is
    /// the fast exit; the StartsWith loop exits immediately when nothing matches).
    /// </summary>
    private void MigrateVirtualPaths()
    {
        if (!AppEnvironment.IsPackaged) return;

        // Reconstruct the OLD virtual Documents path that was used before the fix.
        // Before: Path.Combine(GetFolderPath(LocalApplicationData), DataFolderName, DataSubfolder, "data", "Documents")
        var virtualBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppEnvironment.DataFolderName,
            AppPaths.DataSubfolder,
            "data", "Documents");

        var physicalBase = AppPaths.DocumentsFilesDirectory;

        // Nothing to migrate when the two bases are the same (shouldn't happen in
        // a correctly packaged build, but guards against edge cases such as a packaged
        // debug sideload where the LocalCache path coincidentally matches the standard path).
        if (string.Equals(virtualBase, physicalBase, StringComparison.OrdinalIgnoreCase)) return;

        var changed = 0;
        foreach (var doc in _documents)
        {
            if (string.IsNullOrEmpty(doc.FilePath)) continue;
            if (!doc.FilePath.StartsWith(virtualBase, StringComparison.OrdinalIgnoreCase)) continue;
            doc.FilePath = physicalBase + doc.FilePath.Substring(virtualBase.Length);
            changed++;
        }

        if (changed == 0) return;
        Save();
        Log.Info($"documents.json: rewrote {changed} path(s) from virtual AppData to physical LocalCache.");
    }
}
