namespace NepDateWidget.Models;

/// <summary>
/// Progress snapshot raised by IJobOrchestrationService.Progress.
/// </summary>
public sealed class JobProgressState
{
    public int CompletedCount { get; init; }
    public int TotalCount { get; init; }
    public string CurrentFileName { get; init; } = string.Empty;
    public long TotalSavedBytes { get; init; }
}
