namespace NepDateWidget.Models;

/// <summary>
/// A single upcoming public holiday relative to a reference BS date.
/// All values are sourced from the NepDate calendar metadata via the adapter.
/// </summary>
public sealed class UpcomingHoliday
{
    public int BsYear { get; init; }
    public int BsMonth { get; init; }
    public int BsDay { get; init; }

    /// <summary>0 = today, 1 = tomorrow, etc. Always non-negative.</summary>
    public int DaysUntil { get; init; }

    /// <summary>
    /// Holiday names in English for the BS day. May contain multiple entries
    /// when several public-holiday events coincide on the same date (the
    /// calendar header renders one line per name in that case).
    /// </summary>
    public IReadOnlyList<string> NamesEn { get; init; } = Array.Empty<string>();

    /// <summary>Holiday names in Nepali for the BS day. Same semantics as <see cref="NamesEn"/>.</summary>
    public IReadOnlyList<string> NamesNp { get; init; } = Array.Empty<string>();

    /// <summary>Long English BS label (e.g. "Asoj 25, 2082"). Pre-formatted for display.</summary>
    public string BsLongEn { get; init; } = string.Empty;

    /// <summary>Long Nepali BS label (e.g. "असोज २५, २०८२"). Pre-formatted for display.</summary>
    public string BsLongNp { get; init; } = string.Empty;
}
