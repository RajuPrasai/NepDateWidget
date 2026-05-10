namespace NepDateWidget.Models;

/// <summary>
/// All data needed to render a complete calendar month grid.
/// The <see cref="Days"/> list always contains a complete set of weeks
/// (multiples of 7), starting on Sunday.
/// </summary>
public sealed class CalendarMonth
{
    /// <summary>BS year of the displayed month.</summary>
    public int BsYear { get; init; }

    /// <summary>BS month number (1-12).</summary>
    public int BsMonth { get; init; }

    /// <summary>English name of the BS month (e.g. "Baisakh").</summary>
    public string MonthNameEn { get; init; } = string.Empty;

    /// <summary>Nepali Unicode name of the BS month (e.g. "बैसाख").</summary>
    public string MonthNameNe { get; init; } = string.Empty;

    /// <summary>
    /// Flat ordered list of calendar cells - always a multiple of 7.
    /// Cells before the first of the month and after the last are padding cells.
    /// </summary>
    public IReadOnlyList<CalendarDay> Days { get; init; } = Array.Empty<CalendarDay>();

    /// <summary>Number of days in this BS month.</summary>
    public int DaysInMonth { get; init; }

    /// <summary>True when the displayed month contains today.</summary>
    public bool ContainsToday { get; init; }

    /// <summary>AD months covered by this BS month, e.g. "Mar/Apr 2026" or "Apr 2026".</summary>
    public string AdMonthLabel { get; init; } = string.Empty;
}
