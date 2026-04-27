using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Test-only adapter focused on <see cref="HolidayLookupService"/>: walks BS days
/// correctly via <see cref="AddDays"/> and lets tests register holidays at specific
/// BS dates. Tracks GetCalendarInfo call count so cache behaviour can be asserted.
/// Only the members the holiday lookup actually uses are non-trivially implemented;
/// everything else returns inert defaults.
/// </summary>
internal sealed class HolidayWalkingFakeAdapter : INepaliDateAdapter
{
    private (int Y, int M, int D) _today;
    private DateTime _todayAd;

    private readonly Dictionary<(int Y, int M, int D), (string En, string Np)> _holidays = new();
    private readonly Dictionary<(int Y, int M, int D), (string[] En, string[] Np)> _holidaysMulti = new();
    private readonly HashSet<(int Y, int M, int D)> _holidaysWithoutNames = new();

    public int GetCalendarInfoCalls { get; private set; }

    public HolidayWalkingFakeAdapter((int Y, int M, int D) today)
    {
        _today = today;
        _todayAd = new DateTime(2026, 1, 1);
    }

    public void AddHoliday(int y, int m, int d, string en, string np)
        => _holidays[(y, m, d)] = (en, np);

    public void AddHolidayMulti(int y, int m, int d, string[] en, string[] np)
        => _holidaysMulti[(y, m, d)] = (en, np);

    public void AddHolidayWithoutEvents(int y, int m, int d)
        => _holidaysWithoutNames.Add((y, m, d));

    public void SetToday(int y, int m, int d, int advanceAdByDays)
    {
        _today = (y, m, d);
        _todayAd = _todayAd.AddDays(advanceAdByDays);
    }

    public (int Year, int Month, int Day) GetTodayBs() => _today;
    public DateTime GetTodayAd() => _todayAd;

    public (int Year, int Month, int Day)? AddDays(int bsYear, int bsMonth, int bsDay, int days)
    {
        // Treat each month as exactly 30 days for the test calendar. Sufficient
        // for advancing forward across month/year boundaries deterministically.
        const int monthLen = 30;
        int totalDay = (bsYear * 12 + (bsMonth - 1)) * monthLen + (bsDay - 1) + days;
        if (totalDay < 0) return null;

        int yearMonths = totalDay / monthLen;
        int day = totalDay % monthLen + 1;
        int year = yearMonths / 12;
        int month = yearMonths % 12 + 1;
        return (year, month, day);
    }

    public (bool IsPublicHoliday, string TithiEn, string TithiNp, string[] EventsEn, string[] EventsNp)
        GetCalendarInfo(int bsYear, int bsMonth, int bsDay)
    {
        GetCalendarInfoCalls++;
        var key = (bsYear, bsMonth, bsDay);
        if (_holidaysMulti.TryGetValue(key, out var multi))
            return (true, string.Empty, string.Empty, multi.En, multi.Np);
        if (_holidays.TryGetValue(key, out var ev))
            return (true, string.Empty, string.Empty, new[] { ev.En }, new[] { ev.Np });
        if (_holidaysWithoutNames.Contains(key))
            return (true, string.Empty, string.Empty, Array.Empty<string>(), Array.Empty<string>());
        return (false, string.Empty, string.Empty, Array.Empty<string>(), Array.Empty<string>());
    }

    public string FormatBsLongEn(int y, int m, int d) => $"{m}/{d}/{y}-en";
    public string FormatBsLongNe(int y, int m, int d) => $"{m}/{d}/{y}-ne";

    // ── Unused by HolidayLookupService — inert defaults ──────────────────────
    public int GetDaysInMonth(int bsYear, int bsMonth) => 30;
    public DayOfWeek GetFirstDayOfMonth(int bsYear, int bsMonth) => DayOfWeek.Sunday;
    public DateTime? BsToAd(int bsYear, int bsMonth, int bsDay) => _todayAd;
    public (int Year, int Month, int Day)? AdToBs(DateTime adDate) => _today;
    public string GetMonthNameEn(int bsYear, int bsMonth) => string.Empty;
    public string GetMonthNameNe(int bsMonth) => string.Empty;
    public string FormatBsShortEn(int y, int m, int d) => string.Empty;
    public string FormatBsShortNe(int y, int m, int d) => string.Empty;
    public DayOfWeek GetDayOfWeek(int y, int m, int d) => DayOfWeek.Sunday;
    public int? DiffTotalDays(int y1, int m1, int d1, int y2, int m2, int d2) => 0;
    public (int Years, int Months, int Days)? DiffBreakdown(int y1, int m1, int d1, int y2, int m2, int d2) => (0, 0, 0);
    public (string FyLabel, int Quarter, int DaysToQuarterEnd, int DaysToYearEnd) GetFiscalYearInfo(int y, int m, int d) => (string.Empty, 1, 0, 0);
    public bool TryParseSmartBsDate(string rawText, out int year, out int month, out int day) { year = month = day = 0; return false; }
    public (int Year, int Month, int Day)? AddMonths(int y, int m, int d, int months) => (y, m, d);
}
