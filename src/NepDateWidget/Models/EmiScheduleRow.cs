namespace NepDateWidget.Models;

/// <summary>
/// One row in the EMI amortisation schedule table.
/// Year-group rows carry aggregated totals; month rows carry per-month data.
/// </summary>
public sealed class EmiScheduleRow
{
    /// <summary>True for the collapsed/expanded annual summary row.</summary>
    public bool IsYearRow { get; init; }

    /// <summary>True when a year row is expanded (month rows visible).</summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Display label for the Year column.
    /// Year row: calendar year (e.g. "2026"). Month row: abbreviated month name (e.g. "Apr").
    /// </summary>
    public string Period { get; init; } = string.Empty;

    public string Principal    { get; init; } = string.Empty;
    public string Interest     { get; init; } = string.Empty;
    public string TotalPayment { get; init; } = string.Empty;
    public string Balance      { get; init; } = string.Empty;

    /// <summary>BS date string for the payment month (month rows only).</summary>
    public string BsDate { get; init; } = string.Empty;

    /// <summary>AD date string for the payment month (month rows only).</summary>
    public string AdDate { get; init; } = string.Empty;

    /// <summary>Calendar year this row belongs to (used for grouping).</summary>
    public int CalendarYear { get; init; }
}
