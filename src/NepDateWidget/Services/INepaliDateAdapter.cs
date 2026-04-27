namespace NepDateWidget.Services;

/// <summary>
/// Low-level adapter that isolates all direct NepDate package calls.
/// Nothing outside the Services layer should reference NepDate types.
/// </summary>
public interface INepaliDateAdapter
{
    /// <summary>Returns today's BS date components.</summary>
    (int Year, int Month, int Day) GetTodayBs();

    /// <summary>Returns the AD DateTime for today.</summary>
    DateTime GetTodayAd();

    /// <summary>
    /// Returns the number of days in the given BS month.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for out-of-range inputs.
    /// </summary>
    int GetDaysInMonth(int bsYear, int bsMonth);

    /// <summary>
    /// Returns the day-of-week for the first day of the given BS month.
    /// </summary>
    DayOfWeek GetFirstDayOfMonth(int bsYear, int bsMonth);

    /// <summary>Converts a BS date to AD. Returns null if the input is invalid.</summary>
    DateTime? BsToAd(int bsYear, int bsMonth, int bsDay);

    /// <summary>Converts an AD DateTime to BS. Returns null if out of supported range.</summary>
    (int Year, int Month, int Day)? AdToBs(DateTime adDate);

    /// <summary>Returns the English month name for a BS month number (e.g. 1 → "Baisakh").</summary>
    string GetMonthNameEn(int bsYear, int bsMonth);

    /// <summary>Returns the Nepali Unicode month name for a BS month number (e.g. 1 → "बैसाख").</summary>
    string GetMonthNameNe(int bsMonth);

    /// <summary>Returns the short formatted BS string e.g. "2082/12/20".</summary>
    string FormatBsShortEn(int bsYear, int bsMonth, int bsDay);

    /// <summary>Returns the short Nepali Unicode BS string e.g. "२०८२/१२/२०".</summary>
    string FormatBsShortNe(int bsYear, int bsMonth, int bsDay);

    /// <summary>Returns the long English BS string e.g. "Chaitra 20, 2082".</summary>
    string FormatBsLongEn(int bsYear, int bsMonth, int bsDay);

    /// <summary>Returns the long Nepali Unicode BS string e.g. "चैत २०, २०८२".</summary>
    string FormatBsLongNe(int bsYear, int bsMonth, int bsDay);

    /// <summary>
    /// Returns the BS day-of-week for the given BS date.
    /// </summary>
    DayOfWeek GetDayOfWeek(int bsYear, int bsMonth, int bsDay);

    /// <summary>
    /// Adds <paramref name="days"/> to the given BS date and returns the resulting BS date.
    /// Returns null if input is invalid or result is out of range.
    /// </summary>
    (int Year, int Month, int Day)? AddDays(int bsYear, int bsMonth, int bsDay, int days);

    /// <summary>
    /// Returns the total number of days between two BS dates (d2 - d1).
    /// Returns null if either date is invalid.
    /// </summary>
    int? DiffTotalDays(int y1, int m1, int d1, int y2, int m2, int d2);

    /// <summary>
    /// Returns the difference between two BS dates as (years, months, days).
    /// Returns null if either date is invalid.
    /// </summary>
    (int Years, int Months, int Days)? DiffBreakdown(int y1, int m1, int d1, int y2, int m2, int d2);

    /// <summary>
    /// Returns fiscal year info for a given BS date:
    /// fyLabel ("2082/83"), quarter (1-4), daysToQuarterEnd, daysToYearEnd.
    /// </summary>
    (string FyLabel, int Quarter, int DaysToQuarterEnd, int DaysToYearEnd) GetFiscalYearInfo(int bsYear, int bsMonth, int bsDay);

    /// <summary>
    /// Parses a BS date string using SmartDateParser with a TryParse(autoAdjust) fallback.
    /// Returns true and sets year/month/day on success; returns false without throwing on failure.
    /// </summary>
    bool TryParseSmartBsDate(string rawText, out int year, out int month, out int day);

    /// <summary>
    /// Returns calendar metadata for a given BS date from the NepDate library.
    /// Covers 2001–2089 BS; returns empty/false values outside that range without throwing.
    /// </summary>
    (bool IsPublicHoliday, string TithiEn, string TithiNp, string[] EventsEn, string[] EventsNp)
        GetCalendarInfo(int bsYear, int bsMonth, int bsDay);

    /// <summary>
    /// Adds <paramref name="months"/> to the given BS date and returns the resulting BS date.
    /// Returns null if input is invalid or result is out of range.
    /// </summary>
    (int Year, int Month, int Day)? AddMonths(int bsYear, int bsMonth, int bsDay, int months);
}
