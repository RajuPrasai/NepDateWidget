using NepDateWidget.Models;

namespace NepDateWidget.Services;

/// <summary>
/// Generates calendar month grids and the current date snapshot.
/// Consumes <see cref="INepaliDateAdapter"/> - never NepDate directly.
/// </summary>
public interface ICalendarService
{
    /// <summary>Returns today's date information in both BS and AD, pre-formatted for display.</summary>
    CurrentDateInfo GetCurrentDateInfo();

    /// <summary>
    /// Builds the complete month grid for the given BS year and month.
    /// </summary>
    /// <param name="bsYear">BS year (1901–2199).</param>
    /// <param name="bsMonth">BS month (1–12).</param>
    /// <param name="highlightedDays">
    /// Optional set of highlighted BS dates in "YYYY-MM-DD" format.
    /// Pass an empty collection if the user has no highlights.
    /// </param>
    CalendarMonth GetMonth(int bsYear, int bsMonth, IReadOnlyCollection<string>? highlightedDays = null);

    /// <summary>
    /// Returns the BS year/month that results from navigating
    /// <paramref name="monthsDelta"/> months forward (positive) or backward (negative).
    /// Clamps at the NepDate supported range.
    /// </summary>
    (int Year, int Month) NavigateMonth(int currentBsYear, int currentBsMonth, int monthsDelta);
}
