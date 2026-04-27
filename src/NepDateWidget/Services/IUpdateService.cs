namespace NepDateWidget.Services;

/// <summary>
/// Result of an update check.
/// </summary>
public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string? AvailableVersion,
    string? CurrentVersion,
    string? ErrorMessage = null);

/// <summary>
/// Wraps the Velopack auto-update flow. Implementations may be a no-op
/// (in tests) or wrap <c>UpdateManager</c> against a release feed
/// (production).
/// </summary>
public interface IUpdateService
{
    /// <summary>True when the host process is running under a Velopack install.</summary>
    bool IsInstalled { get; }

    /// <summary>Best-known current version string.</summary>
    string CurrentVersion { get; }

    /// <summary>
    /// Polls the release feed for a newer version. Returns metadata describing
    /// the result. Never throws; errors are returned in <see cref="UpdateCheckResult.ErrorMessage"/>.
    /// </summary>
    Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads and applies a pending update if any, then restarts the app.
    /// Returns false if no update is available or an error occurred.
    /// </summary>
    Task<bool> DownloadAndApplyAsync(CancellationToken ct = default);
}
