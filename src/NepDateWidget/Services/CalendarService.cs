using NepDateWidget.Models;
using NepDateWidget.Services;

namespace NepDateWidget.Services;

/// <summary>
/// Generates calendar month grids and current date info.
/// Consumes <see cref="INepaliDateAdapter"/> - NepDate package never referenced here.
/// </summary>
public sealed class CalendarService : ICalendarService
{
    // NepDate supports 1901/01/01 - 2199/12/30
    private const int MinBsYear = 1901;
    private const int MaxBsYear = 2199;
    private const int MinBsMonth = 1;
    private const int MaxBsMonth = 12;

    private readonly INepaliDateAdapter _adapter;

    public CalendarService(INepaliDateAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    // ── ICalendarService ──────────────────────────────────────────────────────

    public CurrentDateInfo GetCurrentDateInfo()
    {
        var (bsYear, bsMonth, bsDay) = _adapter.GetTodayBs();
        var adDate = _adapter.GetTodayAd();

        return new CurrentDateInfo
        {
            BsYear = bsYear,
            BsMonth = bsMonth,
            BsDay = bsDay,

            BsLongEn = _adapter.FormatBsLongEn(bsYear, bsMonth, bsDay),
            BsLongNe = _adapter.FormatBsLongNe(bsYear, bsMonth, bsDay),
            BsShortEn = _adapter.FormatBsShortEn(bsYear, bsMonth, bsDay),
            BsShortNe = _adapter.FormatBsShortNe(bsYear, bsMonth, bsDay),

            AdDate = adDate,
            AdLong = adDate.ToString("MMMM d, yyyy"),
            AdShort = adDate.ToString("MMM d, yyyy"),
            DayOfWeekShortEn = adDate.ToString("ddd"),
        };
    }

    public CalendarMonth GetMonth(
        int bsYear, int bsMonth)
    {
        bsYear = Math.Clamp(bsYear, MinBsYear, MaxBsYear);
        bsMonth = Math.Clamp(bsMonth, MinBsMonth, MaxBsMonth);

        var today = _adapter.GetTodayBs();
        int daysInMonth = _adapter.GetDaysInMonth(bsYear, bsMonth);
        var firstDow = _adapter.GetFirstDayOfMonth(bsYear, bsMonth);

        // Number of padding cells before day 1 (week starts on Sunday = 0)
        int leadingPad = (int)firstDow;   // Sunday=0, Monday=1, … Saturday=6

        // Total cells = leading pad + days in month, rounded up to next multiple of 7
        int totalCells = (int)Math.Ceiling((leadingPad + daysInMonth) / 7.0) * 7;

        var cells = new List<CalendarDay>(totalCells);

        for (int i = 0; i < totalCells; i++)
        {
            int dayNumber = i - leadingPad + 1;   // 1-based day within the month

            if (dayNumber < 1 || dayNumber > daysInMonth)
            {
                // Padding cell
                cells.Add(new CalendarDay
                {
                    IsCurrentMonth = false,
                    DayOfWeek = (DayOfWeek)(i % 7),
                });
            }
            else
            {
                var dow = (DayOfWeek)(i % 7);
                bool isToday = bsYear == today.Year
                              && bsMonth == today.Month
                              && dayNumber == today.Day;

                var (isHoliday, tithiEn, tithiNp, eventsEn, eventsNp,
                     adDate, bsShortEn, bsShortNe, bsLongEn, bsLongNe) =
                    _adapter.GetCellData(bsYear, bsMonth, dayNumber);

                cells.Add(new CalendarDay
                {
                    Year = bsYear,
                    Month = bsMonth,
                    Day = dayNumber,
                    AdDay = adDate?.Day ?? 0,
                    AdDate = adDate,
                    DayOfWeek = dow,
                    IsCurrentMonth = true,
                    IsToday = isToday,
                    IsHighlighted = false,
                    IsPublicHoliday = isHoliday,
                    TithiEn = tithiEn,
                    TithiNp = tithiNp,
                    EventsEn = eventsEn,
                    EventsNp = eventsNp,
                    BsShortEn = bsShortEn,
                    BsShortNe = bsShortNe,
                    BsLongEn = bsLongEn,
                    BsLongNe = bsLongNe,
                });
            }
        }

        bool containsToday = bsYear == today.Year && bsMonth == today.Month;

        // Reuse the AdDate already computed in each cell; no extra adapter calls needed.
        var firstAd = cells.FirstOrDefault(c => c.IsCurrentMonth)?.AdDate;
        var lastAd  = cells.LastOrDefault(c => c.IsCurrentMonth)?.AdDate;
        string adMonthLabel = string.Empty;
        if (firstAd.HasValue && lastAd.HasValue)
        {
            if (firstAd.Value.Month == lastAd.Value.Month)
                adMonthLabel = firstAd.Value.ToString("MMM yyyy");
            else if (firstAd.Value.Year == lastAd.Value.Year)
                adMonthLabel = $"{firstAd.Value:MMM} / {lastAd.Value:MMM} {firstAd.Value.Year}";
            else
                adMonthLabel = $"{firstAd.Value:MMM yyyy} / {lastAd.Value:MMM yyyy}";
        }

        return new CalendarMonth
        {
            BsYear = bsYear,
            BsMonth = bsMonth,
            MonthNameEn = _adapter.GetMonthNameEn(bsYear, bsMonth),
            MonthNameNe = _adapter.GetMonthNameNe(bsMonth),
            Days = cells,
            DaysInMonth = daysInMonth,
            ContainsToday = containsToday,
            AdMonthLabel = adMonthLabel,
        };
    }

    public (int Year, int Month) NavigateMonth(int currentBsYear, int currentBsMonth, int monthsDelta)
    {
        // Convert to a zero-based month index, add delta, convert back
        int total = (currentBsYear - MinBsYear) * 12 + (currentBsMonth - 1) + monthsDelta;

        // Clamp to supported range
        int minTotal = 0;
        int maxTotal = (MaxBsYear - MinBsYear) * 12 + (MaxBsMonth - 1);
        total = Math.Clamp(total, minTotal, maxTotal);

        int newYear = MinBsYear + total / 12;
        int newMonth = total % 12 + 1;

        return (newYear, newMonth);
    }

    // ── Private helpers ───────────────────────────────────────────────────────
}
