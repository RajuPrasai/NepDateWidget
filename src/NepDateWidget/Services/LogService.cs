using System.IO;
using System.Text;

namespace NepDateWidget.Services;

/// <summary>
/// Writes log entries to a single <c>nepdate.log</c> file beside the executable.
/// When the file exceeds <see cref="UpdateMaxSize"/>, the oldest half is discarded so
/// the most recent activity is always retained.
/// All file operations are synchronous and protected by a lock; exceptions are silently
/// swallowed so a logging failure can never crash the app.
/// </summary>
public sealed class LogService : ILogService
{
    private readonly string _logPath;
    private long _maxBytes;
    private readonly object _fileLock = new();

    private const int MinMb = 5;
    private const int MaxMb = 100;

    public LogService(string logPath, int maxSizeMb = 10)
    {
        _logPath = logPath;
        _maxBytes = Clamp(maxSizeMb, MinMb, MaxMb) * 1024L * 1024L;
    }

    public void UpdateMaxSize(int maxSizeMb) =>
        _maxBytes = Clamp(maxSizeMb, MinMb, MaxMb) * 1024L * 1024L;

    // ── ILogService ───────────────────────────────────────────────────────────

    public void Info(string message) => Write("INFO", message);
    public void Action(string message) => Write("ACTION", message);
    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? ex = null) =>
        Write("ERROR ", ex is null ? message : $"{message} | {ex}");

    public void Fatal(string message, Exception? ex = null) =>
        Write("FATAL ", ex is null ? message : $"{message} | {ex}");

    // ── Private ───────────────────────────────────────────────────────────────

    private void Write(string level, string message)
    {
        try
        {
            lock (_fileLock)
            {
                TrimIfNeeded();
                // ISO 8601 with timezone offset so logs from any locale are unambiguous.
                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fffzzz} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(_logPath, line, Encoding.UTF8);
            }
        }
        catch { /* log failures must never propagate */ }
    }

    /// <summary>
    /// When the file exceeds the cap, truncates to the latest ~half of the content
    /// starting at the first clean line boundary, then prepends a trim banner.
    /// </summary>
    private void TrimIfNeeded()
    {
        if (!File.Exists(_logPath)) return;
        var info = new FileInfo(_logPath);
        if (info.Length < _maxBytes) return;

        // Read raw bytes so we can find the line boundary precisely.
        byte[] bytes = File.ReadAllBytes(_logPath);
        int start = bytes.Length / 2;
        while (start < bytes.Length && bytes[start] != (byte)'\n')
            start++;
        start++; // skip the newline

        byte[] banner = Encoding.UTF8.GetBytes(
            $"--- Log trimmed {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fffzzz}" +
            $" (cap {_maxBytes / 1024 / 1024} MB, older entries removed) ---{Environment.NewLine}");

        using var fs = new FileStream(_logPath, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(banner, 0, banner.Length);
        if (start < bytes.Length)
            fs.Write(bytes, start, bytes.Length - start);
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}
