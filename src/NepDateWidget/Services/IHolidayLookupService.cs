using NepDateWidget.Models;

namespace NepDateWidget.Services;

/// <summary>
/// Looks up upcoming public holidays from the BS calendar, starting at today
/// or a specified BS date. Backed by the NepDate calendar metadata exposed
/// through <see cref="INepaliDateAdapter"/>.
/// </summary>
public interface IHolidayLookupService
{
    /// <summary>
    /// Returns the next public holiday on or after today, or null if none is
    /// found within the configured lookahead window (covers full NepDate range).
    /// Walks day-by-day; result is cached per AD-date so repeated calls within
    /// the same day are O(1).
    /// </summary>
    UpcomingHoliday? GetNextHoliday();

    /// <summary>
    /// Returns up to <paramref name="maxCount"/> upcoming holidays in chronological
    /// order, starting today (inclusive). Used to populate the ticker tooltip.
    /// Cached per AD-date.
    /// </summary>
    IReadOnlyList<UpcomingHoliday> GetUpcomingHolidays(int maxCount);

    /// <summary>
    /// Clears the internal cache. Called automatically on AD-date rollover; tests
    /// also use it to force a re-walk after mutating the underlying adapter.
    /// </summary>
    void InvalidateCache();
}
