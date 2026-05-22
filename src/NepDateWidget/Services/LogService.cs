using System.IO;
using System.Text;
using System.Threading.Channels;

namespace NepDateWidget.Services;

/// <summary>
/// Writes log entries to a single <c>nepdate.log</c> file.
/// All writes happen on a background consumer task via a bounded <see cref="Channel{T}"/>,
/// so callers on the UI thread return immediately without blocking on I/O.
/// When the file exceeds the configured cap the oldest half is discarded.
/// Call <see cref="Dispose"/> on shutdown (drains pending entries for up to 2 s).
/// </summary>
public sealed class LogService : ILogService, IDisposable
{
    private readonly string _logPath;
    private long _maxBytes;
    private readonly Channel<string> _channel;
    private readonly Task _consumer;

    private const int MinMb = 5;
    private const int MaxMb = 100;

    public LogService(string logPath, int maxSizeMb = 10)
    {
        _logPath = logPath;
        _maxBytes = Clamp(maxSizeMb, MinMb, MaxMb) * 1024L * 1024L;
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
        _consumer = Task.Run(ConsumeAsync);
    }

    public void UpdateMaxSize(int maxSizeMb) =>
        Interlocked.Exchange(ref _maxBytes, Clamp(maxSizeMb, MinMb, MaxMb) * 1024L * 1024L);

    // ── ILogService ───────────────────────────────────────────────────────────

    public void Info(string message) => Enqueue("INFO  ", message);
    public void Action(string message) => Enqueue("ACTION", message);
    public void Warn(string message) => Enqueue("WARN  ", message);

    public void Error(string message, Exception? ex = null) =>
        Enqueue("ERROR ", ex is null ? message : $"{message} | {ex}");

    public void Fatal(string message, Exception? ex = null) =>
        Enqueue("FATAL ", ex is null ? message : $"{message} | {ex}");

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _consumer.Wait(TimeSpan.FromSeconds(2));
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Enqueue(string level, string message)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fffzzz} [{level}] {message}{Environment.NewLine}";
            _channel.Writer.TryWrite(line);
        }
        catch { /* enqueue failures must never propagate */ }
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var line in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    TrimIfNeeded();
                    File.AppendAllText(_logPath, line, Encoding.UTF8);
                }
                catch { }
            }
        }
        catch { }
    }

    private void TrimIfNeeded()
    {
        if (!File.Exists(_logPath))
        {
            return;
        }

        long maxBytes = Interlocked.Read(ref _maxBytes);
        var info = new FileInfo(_logPath);
        if (info.Length < maxBytes)
        {
            return;
        }

        byte[] bytes = File.ReadAllBytes(_logPath);
        int start = bytes.Length / 2;
        while (start < bytes.Length && bytes[start] != (byte)'\n')
        {
            start++;
        }

        start++;

        byte[] banner = Encoding.UTF8.GetBytes(
            $"--- Log trimmed {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fffzzz}" +
            $" (cap {maxBytes / 1024 / 1024} MB, older entries removed) ---{Environment.NewLine}");

        using var fs = new FileStream(_logPath, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(banner, 0, banner.Length);
        if (start < bytes.Length)
        {
            fs.Write(bytes, start, bytes.Length - start);
        }
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}
