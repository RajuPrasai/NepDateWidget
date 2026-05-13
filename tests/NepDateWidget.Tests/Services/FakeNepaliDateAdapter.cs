using NepDateWidget.Models;
using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Fake INepaliDateAdapter for testing CalendarService and ConversionService
/// without a real NepDate dependency.
/// Uses a fixed "today" of BS 2082-12-20 (AD 2026-04-03).
/// </summary>
internal sealed class FakeNepaliDateAdapter : INepaliDateAdapter
{
    // Fixed today values
    public int TodayBsYear  { get; set; } = 2082;
    public int TodayBsMonth { get; set; } = 12;
    public int TodayBsDay   { get; set; } = 20;
    public DateTime TodayAd { get; set; } = new DateTime(2026, 4, 3);

    // Simple lookup table: (year,month) → days-in-month
    private static readonly Dictionary<(int, int), int> DaysTable = new()
    {
        { (2082, 1),  31 }, { (2082, 2),  32 }, { (2082, 3),  31 },
        { (2082, 4),  32 }, { (2082, 5),  31 }, { (2082, 6),  30 },
        { (2082, 7),  30 }, { (2082, 8),  29 }, { (2082, 9),  30 },
        { (2082, 10), 29 }, { (2082, 11), 30 }, { (2082, 12), 30 },
    };

    // Fixed first-day-of-month lookup for 2082
    private static readonly Dictionary<(int, int), DayOfWeek> FirstDayTable = new()
    {
        { (2082, 1),  DayOfWeek.Saturday }, { (2082, 2),  DayOfWeek.Monday },
        { (2082, 3),  DayOfWeek.Thursday }, { (2082, 4),  DayOfWeek.Saturday },
        { (2082, 5),  DayOfWeek.Tuesday  }, { (2082, 6),  DayOfWeek.Thursday },
        { (2082, 7),  DayOfWeek.Saturday }, { (2082, 8),  DayOfWeek.Monday   },
        { (2082, 9),  DayOfWeek.Wednesday}, { (2082, 10), DayOfWeek.Thursday },
        { (2082, 11), DayOfWeek.Saturday }, { (2082, 12), DayOfWeek.Tuesday  },
    };

    private static readonly string[] MonthNamesEn =
        { "", "Baisakh","Jestha","Ashadh","Shrawan","Bhadra","Ashwin",
          "Kartik","Mangsir","Poush","Magh","Falgun","Chaitra" };

    private static readonly string[] MonthNamesNe =
        { "", "बैसाख","जेठ","असार","श्रावण","भदौ","असोज",
          "कार्तिक","मंसिर","पुष","माघ","फागुन","चैत" };

    public (int Year, int Month, int Day) GetTodayBs()
        => (TodayBsYear, TodayBsMonth, TodayBsDay);

    public DateTime GetTodayAd() => TodayAd;

    public int GetDaysInMonth(int bsYear, int bsMonth)
        => DaysTable.TryGetValue((bsYear, bsMonth), out var d) ? d : 30;

    public DayOfWeek GetFirstDayOfMonth(int bsYear, int bsMonth)
        => FirstDayTable.TryGetValue((bsYear, bsMonth), out var dow) ? dow : DayOfWeek.Sunday;

    public DateTime? BsToAd(int bsYear, int bsMonth, int bsDay)
    {
        // Simulate NepDate validation: return null for out-of-range inputs
        if (bsYear < 1901 || bsYear > 2199) return null;
        if (bsMonth < 1 || bsMonth > 12) return null;
        if (bsDay < 1 || bsDay > 32) return null;

        if (bsYear == 2082 && bsMonth == 12 && bsDay == 20) return new DateTime(2026, 4, 3);
        if (bsYear == 2082 && bsMonth == 1  && bsDay == 1 ) return new DateTime(2025, 4, 14);

        // For month 12, produce sequential AD dates anchored to the 2082/12/20 special case.
        // This ensures BsToAd(2082, 12, 21) = Apr 4, BsToAd(2082, 12, 22) = Apr 5, etc.
        if (bsYear == 2082 && bsMonth == 12)
            return new DateTime(2026, 4, 3).AddDays(bsDay - 20);

        // Return distinct AD dates based on BS inputs so recurrence math works correctly
        int totalDays = (bsMonth - 1) * 30 + (bsDay - 1);
        return new DateTime(2025, 1, 1).AddDays(totalDays);
    }

    public (int Year, int Month, int Day)? AdToBs(DateTime adDate)
    {
        if (adDate == new DateTime(2026, 4, 3))  return (2082, 12, 20);
        if (adDate == new DateTime(2025, 4, 14)) return (2082, 1, 1);
        if (adDate.Year < 1) return null;
        return (2082, 4, 10); // generic stand-in
    }

    public string GetMonthNameEn(int bsYear, int bsMonth)
        => bsMonth >= 1 && bsMonth <= 12 ? MonthNamesEn[bsMonth] : "";

    public string GetMonthNameNe(int bsMonth)
        => bsMonth >= 1 && bsMonth <= 12 ? MonthNamesNe[bsMonth] : "";

    public string FormatBsShortEn(int y, int m, int d) => $"{y:D4}/{m:D2}/{d:D2}";
    public string FormatBsShortNe(int y, int m, int d) => $"{y}-{m}-{d} (unicode)";
    public string FormatBsLongEn(int y, int m, int d)
        => $"{GetMonthNameEn(y, m)} {d}, {y}";
    public string FormatBsLongNe(int y, int m, int d)
        => $"{GetMonthNameNe(m)} {d} {y} (long ne)";

    public DayOfWeek GetDayOfWeek(int bsYear, int bsMonth, int bsDay)
    {
        // Calculate using the first-day table + day offset
        var firstDow = GetFirstDayOfMonth(bsYear, bsMonth);
        return (DayOfWeek)(((int)firstDow + bsDay - 1) % 7);
    }

    public (int Year, int Month, int Day)? AddDays(int bsYear, int bsMonth, int bsDay, int days)
    {
        if (bsYear < 1901 || bsYear > 2199 || bsMonth < 1 || bsMonth > 12 || bsDay < 1 || bsDay > 32)
            return null;
        // Simple stub: return a fixed result
        return (bsYear, bsMonth, Math.Max(1, Math.Min(30, bsDay + (days % 30))));
    }

    public int? DiffTotalDays(int y1, int m1, int d1, int y2, int m2, int d2)
    {
        if (y1 < 1901 || y1 > 2199 || m1 < 1 || m1 > 12 || d1 < 1 || d1 > 32) return null;
        if (y2 < 1901 || y2 > 2199 || m2 < 1 || m2 > 12 || d2 < 1 || d2 > 32) return null;
        // Approximate: 365 days/year, 30 days/month
        return (y2 - y1) * 365 + (m2 - m1) * 30 + (d2 - d1);
    }

    public (int Years, int Months, int Days)? DiffBreakdown(int y1, int m1, int d1, int y2, int m2, int d2)
    {
        if (y1 < 1901 || y1 > 2199 || m1 < 1 || m1 > 12 || d1 < 1 || d1 > 32) return null;
        if (y2 < 1901 || y2 > 2199 || m2 < 1 || m2 > 12 || d2 < 1 || d2 > 32) return null;
        int years  = y2 - y1;
        int months = m2 - m1;
        int days   = d2 - d1;
        if (days   < 0) { months--; days   += 30; }
        if (months < 0) { years--;  months += 12; }
        return (years, months, days);
    }

    public (string FyLabel, int Quarter, int DaysToQuarterEnd, int DaysToYearEnd) GetFiscalYearInfo(int bsYear, int bsMonth, int bsDay)
    {
        // Nepali FY starts Shrawan (month 4); Q1=4-6, Q2=7-9, Q3=10-12, Q4=1-3
        int fyStart = bsMonth >= 4 ? bsYear : bsYear - 1;
        string label = $"{fyStart}/{(fyStart + 1) % 100:D2}";
        int quarter = bsMonth switch { >= 4 and <= 6 => 1, >= 7 and <= 9 => 2, >= 10 and <= 12 => 3, _ => 4 };
        return (label, quarter, 30, 90);
    }

    public bool TryParseSmartBsDate(string rawText, out int year, out int month, out int day)
    {
        year = month = day = 0;
        if (string.IsNullOrWhiteSpace(rawText)) return false;
        var parts = rawText.Trim().Split('-', '/', '.');
        if (parts.Length == 3
            && int.TryParse(parts[0], out year)
            && int.TryParse(parts[1], out month)
            && int.TryParse(parts[2], out day)
            && year >= 1901 && year <= 2199
            && month >= 1 && month <= 12
            && day >= 1 && day <= 32)
        {
            return true;
        }
        year = month = day = 0;
        return false;
    }

    public (int Year, int Month, int Day)? AddMonths(int y, int m, int d, int months)
    {
        if (y < 1901 || y > 2199 || m < 1 || m > 12 || d < 1 || d > 32) return null;
        m += months;
        while (m > 12) { m -= 12; y++; }
        while (m < 1)  { m += 12; y--; }
        return (y, m, 1);
    }

    public (bool IsPublicHoliday, string TithiEn, string TithiNp, string[] EventsEn, string[] EventsNp)
        GetCalendarInfo(int bsYear, int bsMonth, int bsDay)
            => (false, string.Empty, string.Empty, Array.Empty<string>(), Array.Empty<string>());
}
