namespace NepDateWidget.Models;

/// <summary>
/// A snapshot of the current date in both BS and AD representations,
/// with pre-formatted display strings ready for the collapsed and expanded views.
/// </summary>
public sealed class CurrentDateInfo
{
    // ── BS ────────────────────────────────────────────────────────────────────
    public int BsYear { get; init; }
    public int BsMonth { get; init; }
    public int BsDay { get; init; }

    /// <summary>e.g. "Chaitra 20, 2082"</summary>
    public string BsLongEn { get; init; } = string.Empty;

    /// <summary>e.g. "चैत २०, २०८२"</summary>
    public string BsLongNe { get; init; } = string.Empty;

    /// <summary>e.g. "2082/12/20"</summary>
    public string BsShortEn { get; init; } = string.Empty;

    /// <summary>e.g. "२०८२/१२/२०"</summary>
    public string BsShortNe { get; init; } = string.Empty;

    // ── AD ────────────────────────────────────────────────────────────────────
    public DateTime AdDate { get; init; }

    /// <summary>e.g. "April 3, 2026"</summary>
    public string AdLong { get; init; } = string.Empty;

    /// <summary>e.g. "Apr 3, 2026"</summary>
    public string AdShort { get; init; } = string.Empty;

    /// <summary>e.g. "Fri"</summary>
    public string DayOfWeekShortEn { get; init; } = string.Empty;
}
