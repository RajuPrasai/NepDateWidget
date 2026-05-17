using System.Globalization;

namespace NepDateWidget.Helpers;

/// <summary>
/// Pure decision helper for the once-per-day events notification.
///
/// Whenever the widget starts, this helper decides whether today's calendar
/// events (excluding tithis) should be surfaced as a notification. It only
/// fires once per AD day and skips silently when:
///   • the user disabled the feature,
///   • the date has already been shown today,
///   • the date is empty/unknown,
///   • or there are no events to show.
///
/// Kept WPF-free so it can be unit-tested without spinning up a dispatcher or
/// a notification window.
/// </summary>
internal static class DailyEventsAnnouncer
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Returns true when a notification should be displayed for the given
    /// today/AD date with the given event list. Does not mutate any state.
    /// </summary>
    /// <param name="today">Today's AD date (caller resolves time zone).</param>
    /// <param name="lastShownIsoDate">
    ///   Last AD date a notification was shown for, as <c>yyyy-MM-dd</c>.
    ///   Pass null/empty when the user has never seen one.
    /// </param>
    /// <param name="isEnabled">User setting (Calendar -> "Notify me about today's events").</param>
    /// <param name="eventCount">Number of non-tithi events for today.</param>
    public static bool ShouldFire(DateTime today, string? lastShownIsoDate, bool isEnabled, int eventCount)
    {
        if (!isEnabled)
        {
            return false;
        }

        if (eventCount <= 0)
        {
            return false;
        }

        string todayIso = today.ToString(DateFormat, CultureInfo.InvariantCulture);
        return !string.Equals(lastShownIsoDate, todayIso, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the notification body as a bullet list, one event per line.
    /// Mirrors the reminder note layout (plain text, no markup) so the existing
    /// <c>NotificationPopup</c> chrome can render it without changes.
    /// Returns an empty string when the input is null/empty.
    /// </summary>
    public static string FormatBody(IReadOnlyList<string>? events)
    {
        if (events is null || events.Count == 0)
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(events.Count * 24);
        for (int i = 0; i < events.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }

            sb.Append("• ").Append(events[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Canonical ISO representation of a date used for the persisted
    /// <c>LastDailyEventsNotificationDate</c> field. Centralised so caller and
    /// helper agree on the format byte-for-byte.
    /// </summary>
    public static string ToIsoDate(DateTime date) =>
        date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
