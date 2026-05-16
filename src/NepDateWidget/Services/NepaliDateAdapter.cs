using NepDate;

namespace NepDateWidget.Services;

/// <summary>
/// Concrete adapter that calls the NepDate package.
/// This is the ONLY class in the codebase that is allowed to reference NepDate types directly.
/// All other code works through <see cref="INepaliDateAdapter"/>.
/// </summary>
public sealed class NepaliDateAdapter : INepaliDateAdapter
{
    // BS month names not returned by ToUnicodeString-style APIs as standalone strings,
    // so we keep a local lookup. Index 0 is unused; 1=Baisakh … 12=Chaitra.
    private static readonly string[] MonthNamesNe =
    {
        "",
        "बैशाख", "जेठ",  "असार",  "श्रावण",
        "भदौ",   "असोज", "कार्तिक", "मंसिर",
        "पुष",   "माघ",  "फागुन", "चैत"
    };

    // ── INepaliDateAdapter ────────────────────────────────────────────────────

    public (int Year, int Month, int Day) GetTodayBs()
    {
        var n = NepaliDate.Today;
        return (n.Year, n.Month, n.Day);
    }

    public DateTime GetTodayAd() => NepaliDate.Today.EnglishDate;

    public int GetDaysInMonth(int bsYear, int bsMonth)
    {
        ValidateBsYearMonth(bsYear, bsMonth);
        return new NepaliDate(bsYear, bsMonth, 1).MonthEndDay;
    }

    public DayOfWeek GetFirstDayOfMonth(int bsYear, int bsMonth)
    {
        ValidateBsYearMonth(bsYear, bsMonth);
        return new NepaliDate(bsYear, bsMonth, 1).DayOfWeek;
    }

    public DateTime? BsToAd(int bsYear, int bsMonth, int bsDay)
    {
        try
        {
            return new NepaliDate(bsYear, bsMonth, bsDay).EnglishDate;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public (int Year, int Month, int Day)? AdToBs(DateTime adDate)
    {
        try
        {
            var n = new NepaliDate(adDate);
            return (n.Year, n.Month, n.Day);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public string GetMonthNameEn(int bsYear, int bsMonth)
    {
        try
        {
            return new NepaliDate(bsYear, bsMonth, 1).MonthName.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    public string GetMonthNameNe(int bsMonth)
    {
        if (bsMonth < 1 || bsMonth > 12) return string.Empty;
        return MonthNamesNe[bsMonth];
    }

    public string FormatBsShortEn(int bsYear, int bsMonth, int bsDay)
    {
        try
        {
            return new NepaliDate(bsYear, bsMonth, bsDay).ToString();  // "2082/12/20"
        }
        catch { return string.Empty; }
    }

    public string FormatBsShortNe(int bsYear, int bsMonth, int bsDay)
    {
        try
        {
            return new NepaliDate(bsYear, bsMonth, bsDay).ToUnicodeString();  // "२०८२/१२/२०"
        }
        catch { return string.Empty; }
    }

    public string FormatBsLongEn(int bsYear, int bsMonth, int bsDay)
    {
        try
        {
            return new NepaliDate(bsYear, bsMonth, bsDay).ToLongDateString();  // "Chaitra 20, 2082"
        }
        catch { return string.Empty; }
    }

    public string FormatBsLongNe(int bsYear, int bsMonth, int bsDay)
    {
        try
        {
            return new NepaliDate(bsYear, bsMonth, bsDay).ToLongDateUnicodeString();  // "चैत २०, २०८२"
        }
        catch { return string.Empty; }
    }

    public DayOfWeek GetDayOfWeek(int bsYear, int bsMonth, int bsDay)
    {
        try
        {
            return new NepaliDate(bsYear, bsMonth, bsDay).DayOfWeek;
        }
        catch
        {
            // Fallback: derive from the AD date
            var ad = BsToAd(bsYear, bsMonth, bsDay);
            return ad?.DayOfWeek ?? DayOfWeek.Sunday;
        }
    }

    public (int Year, int Month, int Day)? AddDays(int bsYear, int bsMonth, int bsDay, int days)
    {
        try
        {
            var result = new NepaliDate(bsYear, bsMonth, bsDay).AddDays(days);
            return (result.Year, result.Month, result.Day);
        }
        catch { return null; }
    }

    public int? DiffTotalDays(int y1, int m1, int d1, int y2, int m2, int d2)
    {
        try
        {
            var date1 = new NepaliDate(y1, m1, d1);
            var date2 = new NepaliDate(y2, m2, d2);
            // Subtract via AD dates for exact day count
            var span = date2.EnglishDate.Date - date1.EnglishDate.Date;
            return (int)span.TotalDays;
        }
        catch { return null; }
    }

    public (int Years, int Months, int Days)? DiffBreakdown(int y1, int m1, int d1, int y2, int m2, int d2)
    {
        try
        {
            var a = new NepaliDate(y1, m1, d1);
            var b = new NepaliDate(y2, m2, d2);

            // Ensure a <= b (we store sign externally)
            if (a > b) (a, b) = (b, a);

            int years = b.Year - a.Year;
            int months = b.Month - a.Month;
            int days = b.Day - a.Day;

            if (days < 0)
            {
                months--;
                // Days in the previous month relative to b
                var prevMonth = b.Month == 1
                    ? new NepaliDate(b.Year - 1, 12, 1)
                    : new NepaliDate(b.Year, b.Month - 1, 1);
                days += prevMonth.MonthEndDay;
            }
            if (months < 0) { years--; months += 12; }

            return (years, months, days);
        }
        catch { return null; }
    }

    public (string FyLabel, int Quarter, int DaysToQuarterEnd, int DaysToYearEnd) GetFiscalYearInfo(int bsYear, int bsMonth, int bsDay)
    {
        try
        {
            var date = new NepaliDate(bsYear, bsMonth, bsDay);

            var fyStart = date.FiscalYearStartDate();
            var fyEnd = date.FiscalYearEndDate();
            var (qStart, qEnd) = date.FiscalYearQuarterStartAndEndDate();

            // FY label: "2082/83"
            string fyLabel = $"{fyStart.Year}/{(fyEnd.Year % 100):D2}";

            // Quarter (1-4): months Shrawan(5)-Ashwin(7)=Q1, Kartik(8)-Poush(10)=Q2,
            //                Magh(11)-Falgun(12)/Chaitra(1)=Q3, Chaitra(1)-Asar(3)=Q4
            // Actually NepDate defines FY Q as: Q1=Shrawan-Ashwin(5-7), Q2=Kartik-Poush(8-10),
            //                                   Q3=Magh-Chaitra(11-12,1)? No - let's derive from position
            int fyMonth = bsMonth >= 4 ? bsMonth - 3 : bsMonth + 9; // Fiscal month 1 = Shrawan(5)
            // Shrawan=5 → fyMonth=2... let me think differently
            // FY starts Shrawan (month 4 in BS). Wait - Nepal FY is Shrawan 1 (month 5 actually? Let me check)
            // Actually in BS: Baisakh=1, Jestha=2, Ashadh=3, Shrawan=4... no:
            // Baisakh(1),Jestha(2),Ashadh/Asar(3),Shrawan(4),Bhadra(5),Ashwin(6),Kartik(7),Mangsir(8),Poush(9),Magh(10),Falgun(11),Chaitra(12)
            // Nepal FY: Shrawan(4) to Ashadh(3)
            // Q1: Shrawan(4)-Ashwin(6), Q2: Kartik(7)-Poush(9), Q3: Magh(10)-Falgun(11)+Chaitra(12)? 
            // Actually: Q1=4-6, Q2=7-9, Q3=10-12, Q4=1-3
            int quarter;
            if (bsMonth >= 4 && bsMonth <= 6) quarter = 1;
            else if (bsMonth >= 7 && bsMonth <= 9) quarter = 2;
            else if (bsMonth >= 10 && bsMonth <= 12) quarter = 3;
            else quarter = 4; // 1-3

            int daysToQEnd = (int)(qEnd.EnglishDate.Date - date.EnglishDate.Date).TotalDays;
            int daysToYrEnd = (int)(fyEnd.EnglishDate.Date - date.EnglishDate.Date).TotalDays;

            return (fyLabel, quarter, daysToQEnd, daysToYrEnd);
        }
        catch
        {
            return ("-", 0, 0, 0);
        }
    }

    public (bool IsPublicHoliday, string TithiEn, string TithiNp, string[] EventsEn, string[] EventsNp)
        GetCalendarInfo(int bsYear, int bsMonth, int bsDay)
    {
        try
        {
            var info = new NepaliDate(bsYear, bsMonth, bsDay).GetCalendarInfo();
            return (
                info.IsPublicHoliday,
                info.TithiEn  ?? string.Empty,
                info.TithiNp  ?? string.Empty,
                info.EventsEn ?? Array.Empty<string>(),
                info.EventsNp ?? Array.Empty<string>());
        }
        catch
        {
            return (false, string.Empty, string.Empty, Array.Empty<string>(), Array.Empty<string>());
        }
    }

    public bool TryParseSmartBsDate(string rawText, out int year, out int month, out int day)
    {
        year = month = day = 0;
        if (string.IsNullOrWhiteSpace(rawText)) return false;
        try
        {
            if (SmartDateParser.TryParse(rawText, out NepaliDate parsed))
            {
                year = parsed.Year; month = parsed.Month; day = parsed.Day;
                return true;
            }
            if (NepaliDate.TryParse(rawText, out parsed, autoAdjust: true, monthInMiddle: false))
            {
                year = parsed.Year; month = parsed.Month; day = parsed.Day;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public (int Year, int Month, int Day)? AddMonths(int bsYear, int bsMonth, int bsDay, int months)
    {
        try
        {
            var result = new NepaliDate(bsYear, bsMonth, bsDay).AddMonths(months);
            return (result.Year, result.Month, result.Day);
        }
        catch { return null; }
    }

    public (bool IsPublicHoliday, string TithiEn, string TithiNp, string[] EventsEn, string[] EventsNp,
            DateTime? AdDate, string BsShortEn, string BsShortNe, string BsLongEn, string BsLongNe)
        GetCellData(int bsYear, int bsMonth, int bsDay)
    {
        try
        {
            // ONE NepaliDate allocation covers all six data needs:
            // calendar info, AD date, and all four BS format strings.
            var n = new NepaliDate(bsYear, bsMonth, bsDay);
            var info = n.GetCalendarInfo();
            return (
                info.IsPublicHoliday,
                info.TithiEn  ?? string.Empty,
                info.TithiNp  ?? string.Empty,
                info.EventsEn ?? Array.Empty<string>(),
                info.EventsNp ?? Array.Empty<string>(),
                n.EnglishDate,
                n.ToString(),
                n.ToUnicodeString(),
                n.ToLongDateString(),
                n.ToLongDateUnicodeString());
        }
        catch
        {
            return (false, string.Empty, string.Empty,
                    Array.Empty<string>(), Array.Empty<string>(),
                    null, string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void ValidateBsYearMonth(int year, int month)
    {
        if (year < 1901 || year > 2199)
            throw new ArgumentOutOfRangeException(nameof(year), $"BS year {year} is outside the supported range 1901-2199.");
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month), $"BS month {month} must be 1-12.");
    }
}
