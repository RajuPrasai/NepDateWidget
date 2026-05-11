namespace NepDateWidget.Models;

/// <summary>
/// Transient runtime state that survives across restarts but is not a user preference.
/// Stored in <c>runtime.json</c> so it never pollutes <c>settings.json</c>.
/// All fields default to safe values so a missing or corrupt file is harmless.
/// </summary>
public sealed class AppState
{
    /// <summary>UTC timestamp of the last automatic or manual update check.</summary>
    public DateTime? LastUpdateCheckUtc { get; set; }

    /// <summary>
    /// AD date (yyyy-MM-dd) of the last day a daily events notification was shown.
    /// Empty when never shown. Enforces the once-per-day rule across restarts.
    /// </summary>
    public string LastDailyEventsNotificationDate { get; set; } = string.Empty;
}
