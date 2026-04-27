namespace NepDateWidget.Models;

/// <summary>
/// Represents a single cell in the calendar month grid.
/// Padding cells (days outside the displayed month) fill the start and end of weeks.
/// </summary>
public sealed class CalendarDay
{
    /// <summary>BS year, month, day - zero for padding cells.</summary>
    public int Year { get; init; }
    public int Month { get; init; }
    public int Day { get; init; }

    /// <summary>Corresponding AD day number (e.g. 15). Zero for padding cells.</summary>
    public int AdDay { get; init; }

    /// <summary>The day of the week for layout purposes.</summary>
    public DayOfWeek DayOfWeek { get; init; }

    /// <summary>True for cells that belong to the displayed month.</summary>
    public bool IsCurrentMonth { get; init; }

    /// <summary>True when this day matches today's BS date.</summary>
    public bool IsToday { get; init; }

    /// <summary>True for Saturday - the designated holiday highlight.</summary>
    public bool IsSaturday => DayOfWeek == DayOfWeek.Saturday;

    /// <summary>True for Sunday.</summary>
    public bool IsSunday => DayOfWeek == DayOfWeek.Sunday;

    /// <summary>True when the user has manually highlighted this date.</summary>
    public bool IsHighlighted { get; init; }

    /// <summary>Convenience: a padding cell has no date data.</summary>
    public bool IsPadding => !IsCurrentMonth;

    /// <summary>True when NepDate flags this date as a Nepal public holiday.</summary>
    public bool IsPublicHoliday { get; init; }

    /// <summary>Lunar day (Tithi) name in English. Empty outside NepDate data range 2001–2089 BS.</summary>
    public string TithiEn { get; init; } = string.Empty;

    /// <summary>Lunar day (Tithi) name in Nepali. Empty outside NepDate data range.</summary>
    public string TithiNp { get; init; } = string.Empty;

    /// <summary>Event names in English for this date. Empty array if none or outside data range.</summary>
    public string[] EventsEn { get; init; } = Array.Empty<string>();

    /// <summary>Event names in Nepali for this date. Empty array if none or outside data range.</summary>
    public string[] EventsNp { get; init; } = Array.Empty<string>();
}
