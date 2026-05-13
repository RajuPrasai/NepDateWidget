namespace NepDateWidget.Services;

/// <summary>
/// Minimal structured log interface. Implementations write to a capped log file.
/// </summary>
public interface ILogService
{
    void Info(string message);
    void Action(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
    void Fatal(string message, Exception? ex = null);

    /// <summary>Called when the user changes the log cap in Settings.</summary>
    void UpdateMaxSize(int maxSizeMb);
}
