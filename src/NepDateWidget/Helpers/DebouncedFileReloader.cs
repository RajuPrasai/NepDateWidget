using System.IO;

namespace NepDateWidget.Helpers;

/// <summary>
/// Watches a single file via <see cref="System.IO.FileSystemWatcher"/> and invokes
/// a callback after a debounce delay. The callback always runs on a thread-pool thread
/// (the timer's thread). Callers that need to update UI state must marshal specific
/// operations to their own <see cref="System.Threading.SynchronizationContext"/>.
/// Watching is silently skipped when the parent directory does not exist at construction time.
/// </summary>
public sealed class DebouncedFileReloader : IDisposable
{
    private readonly FileSystemWatcher? _watcher;
    private readonly System.Threading.Timer _debounceTimer;
    private readonly Action _onReload;
    private readonly int _debounceMs;

    public DebouncedFileReloader(
        string filePath,
        int debounceMs,
        Action onReload)
    {
        _onReload = onReload ?? throw new ArgumentNullException(nameof(onReload));
        _debounceMs = debounceMs;
        _debounceTimer = new System.Threading.Timer(_ => FireReload(),
            null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            Log.Warn($"DebouncedFileReloader: directory for '{filePath}' does not exist, watching disabled.");
            return;
        }

        _watcher = new FileSystemWatcher(dir, Path.GetFileName(filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += (_, _) => Schedule();
        _watcher.Created += (_, _) => Schedule();
        _watcher.Deleted += (_, _) => Schedule();
        // Renamed covers editors that do atomic save via temp-file rename (VS Code, Notepad++).
        _watcher.Renamed += (_, _) => Schedule();
    }

    private void Schedule() =>
        _debounceTimer.Change(_debounceMs, System.Threading.Timeout.Infinite);

    private void FireReload()
    {
        // The callback always runs on a thread-pool thread (the timer thread).
        // Callers that need to update UI state are responsible for marshalling
        // specific operations (e.g. raising changed events) to their own context.
        _onReload();
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer.Dispose();
    }
}
